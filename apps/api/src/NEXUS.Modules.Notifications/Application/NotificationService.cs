using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Notifications.Application;

public class NotificationService : INotificationService
{
    private readonly DbContext _context;

    public NotificationService(DbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<Notification>> GetUserNotificationsAsync(Guid userId, int page, int pageSize, bool? unreadOnly = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Notification>().Where(n => n.UserId == userId);
        if (unreadOnly == true) query = query.Where(n => !n.IsRead);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(n => n.SentAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Notification>(items, page, pageSize, total);
    }

    public async Task<Result> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Set<Notification>().FindAsync(new object[] { notificationId }, cancellationToken);
        if (notification is null) return Result.Failure(Error.NotFound());
        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _context.Set<Notification>().Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
