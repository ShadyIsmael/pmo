using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Notifications.Infrastructure;

public sealed class NotificationDeliveryProcessor
{
    private readonly DbContext _context;
    private readonly IPushNotificationSender _pushSender;
    private readonly IEmailNotificationSender? _emailSender;
    private readonly NotificationDeliveryOptions _options;
    public NotificationDeliveryProcessor(
        DbContext context,
        IPushNotificationSender pushSender,
        IOptions<NotificationDeliveryOptions> options,
        IEmailNotificationSender? emailSender = null)
    {
        _context = context;
        _pushSender = pushSender;
        _emailSender = emailSender;
        _options = options.Value;
    }

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiredBefore = now - _options.ProcessingLease;
        var candidates = await _context.Set<NotificationDelivery>().IgnoreQueryFilters()
            .Where(IsClaimable(now, expiredBefore))
            .OrderBy(item => item.NextAttemptAt)
            .ThenBy(item => item.CreatedAt)
            .Select(item => item.Id)
            .Take(_options.BatchSize)
            .ToArrayAsync(cancellationToken);
        if (candidates.Length == 0) return 0;

        var leaseId = Guid.NewGuid();
        var claimed = await _context.Set<NotificationDelivery>().IgnoreQueryFilters()
            .Where(item => candidates.Contains(item.Id)
                && (((item.Status == NotificationDeliveryStatus.Pending || item.Status == NotificationDeliveryStatus.RetryScheduled)
                     && item.NextAttemptAt <= now)
                    || (item.Status == NotificationDeliveryStatus.Processing && item.ProcessingStartedAt < expiredBefore)))
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, NotificationDeliveryStatus.Processing)
                .SetProperty(item => item.ProcessingStartedAt, now)
                .SetProperty(item => item.LastAttemptAt, now)
                .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1)
                .SetProperty(item => item.LeaseId, leaseId)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (claimed == 0) return 0;

        var deliveries = await _context.Set<NotificationDelivery>().IgnoreQueryFilters()
            .Where(item => item.LeaseId == leaseId && item.Status == NotificationDeliveryStatus.Processing)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        foreach (var delivery in deliveries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var trustedTenant = TrustedTenantExecutionContext.Enter(delivery.TenantId);
            await ProcessAsync(delivery, cancellationToken);
        }
        return deliveries.Count;
    }

    private async Task ProcessAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        var notification = await _context.Set<Notification>().IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == delivery.NotificationId && item.TenantId == delivery.TenantId, cancellationToken);
        if (notification is null)
        {
            Finish(delivery, NotificationDeliveryStatus.FailedPermanent, "MissingNotification", "The notification is unavailable.");
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }
        if (delivery.Channel == NotificationDeliveryChannel.Email)
        {
            await ProcessEmailAsync(delivery, notification, cancellationToken);
            return;
        }
        if (!notification.UserId.HasValue)
        {
            Finish(delivery, NotificationDeliveryStatus.FailedPermanent, "MissingRecipient", "The notification recipient is unavailable.");
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }
        if (delivery.Channel != NotificationDeliveryChannel.Push)
        {
            Finish(delivery, NotificationDeliveryStatus.Cancelled, "ChannelDeferred", "This delivery channel is not enabled yet.");
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var devices = await _context.Set<UserDeviceToken>().IgnoreQueryFilters().Where(device =>
                device.TenantId == delivery.TenantId && device.UserId == notification.UserId.Value && device.IsActive)
            .OrderBy(device => device.CreatedAt)
            .ToListAsync(cancellationToken);
        var attempts = await _context.Set<NotificationDeliveryAttempt>().IgnoreQueryFilters()
            .Where(attempt => attempt.TenantId == delivery.TenantId && attempt.NotificationDeliveryId == delivery.Id)
            .ToListAsync(cancellationToken);
        if (devices.Count == 0)
        {
            foreach (var attempt in attempts.Where(attempt => attempt.Status is NotificationAttemptStatus.Pending or NotificationAttemptStatus.Processing or NotificationAttemptStatus.RetryScheduled))
            {
                attempt.Status = NotificationAttemptStatus.Cancelled;
                attempt.FailureCategory = "DeviceInactive";
                attempt.LastError = "The device is no longer active.";
            }
            if (attempts.Any(attempt => attempt.Status == NotificationAttemptStatus.Delivered))
            {
                Finish(delivery, NotificationDeliveryStatus.Delivered, "PartialDeviceFailure", "Push was accepted by a device before all remaining devices became inactive.");
                delivery.DeliveredAt ??= DateTime.UtcNow;
            }
            else
            {
                Finish(delivery, NotificationDeliveryStatus.FailedPermanent, "NoActiveDevices", "No active push devices are registered for this user.");
            }
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var deviceIds = devices.Select(device => device.Id).ToArray();
        foreach (var attempt in attempts.Where(attempt => !deviceIds.Contains(attempt.DeviceTokenId)
                     && attempt.Status is NotificationAttemptStatus.Pending or NotificationAttemptStatus.RetryScheduled))
        {
            attempt.Status = NotificationAttemptStatus.Cancelled;
            attempt.FailureCategory = "DeviceInactive";
            attempt.LastError = "The device is no longer active.";
        }
        foreach (var device in devices.Where(device => attempts.All(attempt => attempt.DeviceTokenId != device.Id)))
        {
            var attempt = new NotificationDeliveryAttempt
            {
                TenantId = delivery.TenantId,
                NotificationDeliveryId = delivery.Id,
                DeviceTokenId = device.Id,
                Status = NotificationAttemptStatus.Pending,
                NextAttemptAt = DateTime.UtcNow,
                CreatedBy = "NotificationDeliveryWorker",
            };
            attempts.Add(attempt);
            _context.Set<NotificationDeliveryAttempt>().Add(attempt);
        }
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var device in devices)
        {
            var attempt = attempts.Single(item => item.DeviceTokenId == device.Id);
            if (attempt.Status is NotificationAttemptStatus.Delivered or NotificationAttemptStatus.FailedPermanent or NotificationAttemptStatus.Cancelled)
                continue;
            if (attempt.Status == NotificationAttemptStatus.RetryScheduled && attempt.NextAttemptAt > DateTime.UtcNow)
                continue;
            if (attempt.AttemptCount >= delivery.MaxAttempts)
            {
                attempt.Status = NotificationAttemptStatus.FailedPermanent;
                attempt.FailureCategory ??= "MaxAttempts";
                attempt.LastError ??= "The maximum number of push attempts was reached.";
                await _context.SaveChangesAsync(cancellationToken);
                continue;
            }

            attempt.Status = NotificationAttemptStatus.Processing;
            attempt.AttemptCount++;
            attempt.LastAttemptAt = DateTime.UtcNow;
            attempt.UpdatedBy = "NotificationDeliveryWorker";
            delivery.ProcessingStartedAt = DateTime.UtcNow;
            delivery.UpdatedAt = delivery.ProcessingStartedAt;
            await _context.SaveChangesAsync(cancellationToken);

            PushSendResult result;
            try
            {
                result = await _pushSender.SendAsync(notification, device, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Leave the lease and Processing state intact. The next worker recovers it after expiry.
                throw;
            }
            catch (Exception)
            {
                result = new PushSendResult(false, PushFailureKind.Transient, "ProviderError", "The push provider could not complete the request.");
            }

            ApplyResult(attempt, device, result, delivery.MaxAttempts);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var currentAttempts = await _context.Set<NotificationDeliveryAttempt>().IgnoreQueryFilters().AsNoTracking()
            .Where(attempt => attempt.TenantId == delivery.TenantId && attempt.NotificationDeliveryId == delivery.Id)
            .ToListAsync(cancellationToken);
        var retryAttempts = currentAttempts.Where(attempt => attempt.Status == NotificationAttemptStatus.RetryScheduled).ToArray();
        if (retryAttempts.Length > 0)
        {
            delivery.Status = NotificationDeliveryStatus.RetryScheduled;
            delivery.NextAttemptAt = retryAttempts.Min(attempt => attempt.NextAttemptAt);
            delivery.FailureCategory = retryAttempts[0].FailureCategory;
            delivery.LastError = retryAttempts[0].LastError;
            ClearLease(delivery);
        }
        else if (currentAttempts.Any(attempt => attempt.Status == NotificationAttemptStatus.Delivered))
        {
            Finish(delivery, NotificationDeliveryStatus.Delivered,
                currentAttempts.Any(attempt => attempt.Status == NotificationAttemptStatus.FailedPermanent) ? "PartialDeviceFailure" : null,
                currentAttempts.Any(attempt => attempt.Status == NotificationAttemptStatus.FailedPermanent) ? "Push was accepted by some devices; another device failed permanently." : null);
            delivery.DeliveredAt = DateTime.UtcNow;
        }
        else
        {
            var failure = currentAttempts.FirstOrDefault(attempt => attempt.Status == NotificationAttemptStatus.FailedPermanent);
            Finish(delivery, NotificationDeliveryStatus.FailedPermanent,
                failure?.FailureCategory ?? "NoDeliverableDevice",
                failure?.LastError ?? "No device accepted the push notification.");
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessEmailAsync(NotificationDelivery delivery, Notification notification, CancellationToken cancellationToken)
    {
        var emailSender = _emailSender;
        if (emailSender is null)
        {
            Finish(delivery, NotificationDeliveryStatus.Cancelled, "EmailUnavailable", "Email delivery is not configured.");
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!notification.UserId.HasValue)
        {
            Finish(delivery, NotificationDeliveryStatus.FailedPermanent, "MissingRecipient", "Email delivery requires an authenticated tenant user.");
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var user = await _context.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == notification.UserId.Value
                && candidate.TenantId == delivery.TenantId && !candidate.IsDeleted && candidate.IsActive, cancellationToken);
        if (user is null)
        {
            Finish(delivery, NotificationDeliveryStatus.FailedPermanent, "MissingRecipient", "The notification recipient is unavailable.");
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var preference = await _context.Set<NotificationPreference>().IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == delivery.TenantId
                && item.UserId == user.Id && item.Type == notification.Type && !item.IsDeleted, cancellationToken);
        if (!(preference?.EmailEnabled ?? NotificationPreferenceDefaults.EmailEnabled(notification.Type)))
        {
            Finish(delivery, NotificationDeliveryStatus.Cancelled, "PreferenceDisabled", "The recipient has disabled email delivery for this notification type.");
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        EmailSendResult? result = null;
        bool profileConfigured;
        try
        {
            profileConfigured = await emailSender.IsConfiguredAsync(delivery.TenantId, notification.OrganizationId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            profileConfigured = false;
            result = new(false, EmailFailureKind.Transient, "EmailProfileUnavailable", "Email delivery configuration is temporarily unavailable.");
        }

        if (result is null && !profileConfigured)
        {
            Finish(delivery, NotificationDeliveryStatus.Cancelled, "EmailUnavailable", "The effective email profile is disabled or unavailable.");
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (result is null)
        {
            try
            {
                result = await emailSender.SendAsync(notification, user.Email, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                result = new(false, EmailFailureKind.Transient, "EmailDeliveryUnavailable", "Email delivery is temporarily unavailable.");
            }
        }

        var sendResult = result ?? new(false, EmailFailureKind.Transient, "ProviderError", "Email delivery could not complete.");
        if (sendResult.Succeeded)
        {
            Finish(delivery, NotificationDeliveryStatus.Accepted, null, null);
            delivery.AcceptedAt = DateTime.UtcNow;
        }
        else if (sendResult.FailureKind == EmailFailureKind.Transient && delivery.AttemptCount < delivery.MaxAttempts)
        {
            delivery.Status = NotificationDeliveryStatus.RetryScheduled;
            delivery.NextAttemptAt = NotificationRetryPolicy.NextAttempt(DateTime.UtcNow, delivery.AttemptCount, _options);
            delivery.FailureCategory = sendResult.FailureCategory ?? "ProviderError";
            delivery.LastError = sendResult.SafeFailureSummary ?? "The email provider could not complete the request.";
            ClearLease(delivery);
        }
        else
        {
            Finish(delivery, NotificationDeliveryStatus.FailedPermanent,
                sendResult.FailureCategory ?? "ProviderRejected",
                sendResult.SafeFailureSummary ?? "The email provider could not complete the request.");
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private void ApplyResult(NotificationDeliveryAttempt attempt, UserDeviceToken device, PushSendResult result, int maxAttempts)
    {
        if (result.Succeeded)
        {
            attempt.Status = NotificationAttemptStatus.Delivered;
            attempt.DeliveredAt = DateTime.UtcNow;
            attempt.ProviderMessageId = result.ProviderMessageId;
            attempt.FailureCategory = null;
            attempt.LastError = null;
            return;
        }

        attempt.FailureCategory = result.FailureCategory ?? "ProviderError";
        attempt.LastError = result.SafeFailureSummary ?? "The push provider could not complete the request.";
        if (result.FailureKind == PushFailureKind.Unregistered)
        {
            device.IsActive = false;
            device.DeactivatedAt = DateTime.UtcNow;
            device.UpdatedBy = "Firebase";
            attempt.Status = NotificationAttemptStatus.FailedPermanent;
            return;
        }
        if (result.FailureKind == PushFailureKind.Permanent || attempt.AttemptCount >= maxAttempts)
        {
            attempt.Status = NotificationAttemptStatus.FailedPermanent;
            if (attempt.AttemptCount >= maxAttempts) attempt.FailureCategory = "MaxAttempts";
            return;
        }

        attempt.Status = NotificationAttemptStatus.RetryScheduled;
        attempt.NextAttemptAt = NotificationRetryPolicy.NextAttempt(DateTime.UtcNow, attempt.AttemptCount, _options);
    }

    private static void Finish(NotificationDelivery delivery, NotificationDeliveryStatus status, string? category, string? error)
    {
        delivery.Status = status;
        delivery.FailureCategory = category;
        delivery.LastError = error;
        delivery.UpdatedAt = DateTime.UtcNow;
        ClearLease(delivery);
    }

    private static void ClearLease(NotificationDelivery delivery)
    {
        delivery.ProcessingStartedAt = null;
        delivery.LeaseId = null;
    }

    private static System.Linq.Expressions.Expression<Func<NotificationDelivery, bool>> IsClaimable(DateTime now, DateTime expiredBefore) =>
        item => ((item.Status == NotificationDeliveryStatus.Pending || item.Status == NotificationDeliveryStatus.RetryScheduled)
                 && item.NextAttemptAt <= now)
                || (item.Status == NotificationDeliveryStatus.Processing && item.ProcessingStartedAt < expiredBefore);

}
