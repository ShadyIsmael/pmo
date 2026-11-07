using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Notifications.Application;

public class NotificationService : INotificationService
{
    private readonly DbContext _context;
    private readonly IPushNotificationSender _pushSender;
    private readonly IEmailNotificationSender? _emailSender;
    private readonly NotificationDeliveryOptions _deliveryOptions;

    public NotificationService(
        DbContext context,
        IPushNotificationSender pushSender,
        IOptions<NotificationDeliveryOptions> deliveryOptions,
        IEmailNotificationSender? emailSender = null)
    {
        _context = context;
        _pushSender = pushSender;
        _emailSender = emailSender;
        _deliveryOptions = deliveryOptions.Value;
    }

    public async Task<PagedResult<Notification>> GetUserNotificationsAsync(Guid tenantId, Guid userId, int page, int pageSize, bool? unreadOnly = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Notification>().Where(n => n.TenantId == tenantId && n.UserId == userId && n.IsInAppVisible);
        if (unreadOnly == true) query = query.Where(n => !n.IsRead);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(n => n.SentAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Notification>(items, page, pageSize, total);
    }

    public async Task<Result> MarkAsReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Set<Notification>()
            .SingleOrDefaultAsync(item => item.Id == notificationId && item.TenantId == tenantId && item.UserId == userId && item.IsInAppVisible, cancellationToken);
        if (notification is null) return Result.Failure(Error.NotFound());
        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> MarkAllAsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _context.Set<Notification>()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.IsInAppVisible && !item.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        if (notifications.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public Task<int> GetUnreadCountAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
        _context.Set<Notification>().CountAsync(item => item.TenantId == tenantId && item.UserId == userId && item.IsInAppVisible && !item.IsRead, cancellationToken);

    public async Task<NotificationPreferencesResponse> GetPreferencesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var saved = await _context.Set<NotificationPreference>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Type, cancellationToken);
        var items = NotificationTypes.Supported.Select(type => saved.TryGetValue(type, out var preference)
            ? new NotificationPreferenceDto(type, preference.InAppEnabled, preference.PushEnabled, preference.EmailEnabled)
            : new NotificationPreferenceDto(type,
                NotificationPreferenceDefaults.InAppEnabled(type),
                NotificationPreferenceDefaults.PushEnabled(type),
                NotificationPreferenceDefaults.EmailEnabled(type))).ToArray();
        var emailAvailable = await IsEmailAvailableAsync(tenantId, organizationId: null, cancellationToken);
        return new NotificationPreferencesResponse(items, _pushSender.IsConfigured, emailAvailable);
    }

    public async Task<Result> UpdatePreferencesAsync(Guid tenantId, Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count != NotificationTypes.Supported.Count
            || request.Items.Select(item => item.Type).Distinct(StringComparer.Ordinal).Count() != request.Items.Count
            || request.Items.Any(item => !NotificationTypes.Supported.Contains(item.Type, StringComparer.Ordinal)))
            return Result.Failure(Error.Validation("Provide one preference for each supported notification type."));

        var existing = await _context.Set<NotificationPreference>()
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.IsDeleted)
            .ToListAsync(cancellationToken);
        var existingByType = existing.GroupBy(item => item.Type, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var value in request.Items)
        {
            if (!existingByType.TryGetValue(value.Type, out var preference))
            {
                preference = new NotificationPreference
                {
                    TenantId = tenantId,
                    UserId = userId,
                    Type = value.Type,
                    CreatedBy = userId.ToString(),
                };
                _context.Set<NotificationPreference>().Add(preference);
                existingByType.Add(value.Type, preference);
            }
            preference.IsDeleted = false;
            preference.InAppEnabled = value.InAppEnabled;
            preference.PushEnabled = value.PushEnabled;
            preference.EmailEnabled = value.EmailEnabled;
            preference.UpdatedBy = userId.ToString();
        }
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyDictionary<Guid, NotificationChannelEligibility>> GetChannelEligibilityAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        string type,
        CancellationToken cancellationToken = default,
        Guid? organizationId = null)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, NotificationChannelEligibility>();

        var emailAvailable = await IsEmailAvailableAsync(tenantId, organizationId, cancellationToken);

        var saved = await _context.Set<NotificationPreference>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && ids.Contains(item.UserId) && item.Type == type && !item.IsDeleted)
            .ToDictionaryAsync(item => item.UserId, cancellationToken);

        return ids.ToDictionary(userId => userId, userId => saved.TryGetValue(userId, out var preference)
            ? new NotificationChannelEligibility(preference.InAppEnabled, preference.PushEnabled, preference.EmailEnabled, _pushSender.IsConfigured, emailAvailable)
            : new NotificationChannelEligibility(
                NotificationPreferenceDefaults.InAppEnabled(type),
                NotificationPreferenceDefaults.PushEnabled(type),
                NotificationPreferenceDefaults.EmailEnabled(type),
                _pushSender.IsConfigured,
                emailAvailable));
    }

    public async Task<Result<bool>> SendIdempotentAsync(Notification notification, bool allowPush, bool allowEmail, CancellationToken cancellationToken = default)
    {
        if (notification.Id == Guid.Empty || !notification.UserId.HasValue)
            return Result<bool>.Failure(Error.Validation("An idempotent notification requires a stable ID and an authenticated recipient."));

        var existing = await FindExistingAsync(notification.TenantId, notification.Id, cancellationToken);
        if (existing is not null) return Matches(existing, notification)
            ? Result<bool>.Success(false)
            : Result<bool>.Failure(Error.Conflict("The idempotency key was already used for different notification content."));

        try
        {
            var queued = await SendAsync(notification, allowPush, allowEmail, cancellationToken);
            return queued.IsSuccess ? Result<bool>.Success(true) : Result<bool>.Failure(queued.Error ?? Error.Validation("Notification could not be queued."));
        }
        catch (DbUpdateException)
        {
            // A concurrent retry can win the notification primary-key insert. Detach only this
            // request's Added rows, then treat an identical persisted row as an idempotent retry.
            foreach (var entry in _context.ChangeTracker.Entries<NotificationDelivery>()
                         .Where(entry => entry.State == EntityState.Added && entry.Entity.NotificationId == notification.Id).ToArray())
                entry.State = EntityState.Detached;
            foreach (var entry in _context.ChangeTracker.Entries<Notification>()
                         .Where(entry => entry.State == EntityState.Added && entry.Entity.Id == notification.Id).ToArray())
                entry.State = EntityState.Detached;

            existing = await FindExistingAsync(notification.TenantId, notification.Id, cancellationToken);
            if (existing is not null && Matches(existing, notification)) return Result<bool>.Success(false);
            throw;
        }
    }

    public Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken = default) =>
        SendAsync(notification, allowPush: true, allowEmail: true, cancellationToken: cancellationToken);

    private async Task<Result> SendAsync(Notification notification, bool allowPush, bool allowEmail, CancellationToken cancellationToken)
    {
        NotificationPreference? preference = null;
        if (notification.UserId.HasValue)
        {
            preference = await _context.Set<NotificationPreference>()
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(item =>
                    item.TenantId == notification.TenantId
                    && item.UserId == notification.UserId.Value
                    && item.Type == notification.Type
                    && !item.IsDeleted,
                    cancellationToken);
        }

        notification.IsInAppVisible = !notification.UserId.HasValue
            ? false
            : notification.IsInAppVisible && (preference?.InAppEnabled ?? NotificationPreferenceDefaults.InAppEnabled(notification.Type));
        _context.Set<Notification>().Add(notification);
        if (notification.UserId.HasValue
            && allowPush
            && (preference?.PushEnabled ?? NotificationPreferenceDefaults.PushEnabled(notification.Type))
            && _pushSender.IsConfigured)
        {
            _context.Set<NotificationDelivery>().Add(new NotificationDelivery
            {
                TenantId = notification.TenantId,
                NotificationId = notification.Id,
                Channel = NotificationDeliveryChannel.Push,
                Status = NotificationDeliveryStatus.Pending,
                MaxAttempts = _deliveryOptions.MaxAttempts,
                NextAttemptAt = DateTime.UtcNow,
                CreatedBy = "NotificationService",
            });
        }
        if (notification.UserId.HasValue
            && allowEmail
            && _emailSender is not null
            && (preference?.EmailEnabled ?? NotificationPreferenceDefaults.EmailEnabled(notification.Type))
            && await IsEmailAvailableAsync(notification.TenantId, notification.OrganizationId, cancellationToken))
        {
            _context.Set<NotificationDelivery>().Add(new NotificationDelivery
            {
                TenantId = notification.TenantId,
                NotificationId = notification.Id,
                Channel = NotificationDeliveryChannel.Email,
                Status = NotificationDeliveryStatus.Pending,
                MaxAttempts = _deliveryOptions.MaxAttempts,
                NextAttemptAt = DateTime.UtcNow,
                CreatedBy = "NotificationService",
            });
        }

        // EF saves the canonical row and its delivery intent in one database transaction.
        // Provider calls happen later in the hosted dispatcher.
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private Task<Notification?> FindExistingAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) =>
        _context.Set<Notification>().IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);

    private async Task<bool> IsEmailAvailableAsync(Guid tenantId, Guid? organizationId, CancellationToken cancellationToken)
    {
        if (_emailSender is null) return false;
        try
        {
            return await _emailSender.IsConfiguredAsync(tenantId, organizationId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Email profile lookup must not prevent the durable in-app notification from being saved.
            return false;
        }
    }

    private static bool Matches(Notification existing, Notification requested) =>
        existing.TenantId == requested.TenantId
        && existing.OrganizationId == requested.OrganizationId
        && existing.UserId == requested.UserId
        && existing.Type == requested.Type
        && existing.Title == requested.Title
        && existing.Body == requested.Body
        && existing.ReferenceType == requested.ReferenceType
        && existing.ReferenceId == requested.ReferenceId
        && existing.ReferencePublicId == requested.ReferencePublicId
        && existing.CreatedBy == requested.CreatedBy;
}
