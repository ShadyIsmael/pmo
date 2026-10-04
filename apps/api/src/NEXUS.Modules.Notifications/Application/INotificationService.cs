using NEXUS.SharedKernel;
using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Modules.Notifications.Application;

public interface INotificationService
{
    Task<PagedResult<Notification>> GetUserNotificationsAsync(Guid tenantId, Guid userId, int page, int pageSize, bool? unreadOnly = null, CancellationToken cancellationToken = default);
    Task<Result> MarkAsReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task<Result> MarkAllAsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken = default);
}
