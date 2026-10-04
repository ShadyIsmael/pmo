using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Notifications.Application;

public class NotificationService : INotificationService
{
    private readonly DbContext _context;
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        DbContext context,
        IEnumerable<INotificationChannel> channels,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _channels = channels;
        _logger = logger;
    }

    public async Task<PagedResult<Notification>> GetUserNotificationsAsync(Guid tenantId, Guid userId, int page, int pageSize, bool? unreadOnly = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Notification>().Where(n => n.TenantId == tenantId && n.UserId == userId);
        if (unreadOnly == true) query = query.Where(n => !n.IsRead);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(n => n.SentAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Notification>(items, page, pageSize, total);
    }

    public async Task<Result> MarkAsReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Set<Notification>()
            .SingleOrDefaultAsync(item => item.Id == notificationId && item.TenantId == tenantId && item.UserId == userId, cancellationToken);
        if (notification is null) return Result.Failure(Error.NotFound());
        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> MarkAllAsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _context.Set<Notification>()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && !item.IsRead)
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

    public async Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _context.Set<Notification>().Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var channel in _channels)
        {
            try
            {
                await channel.SendAsync(notification, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Notification channel {Channel} failed; the in-app notification is retained.", channel.GetType().Name);
            }
        }

        return Result.Success();
    }
}
