using NEXUS.SharedKernel;
using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Modules.Notifications.Application;

public interface INotificationService
{
    Task<PagedResult<Notification>> GetUserNotificationsAsync(Guid tenantId, Guid userId, int page, int pageSize, bool? unreadOnly = null, CancellationToken cancellationToken = default);
    Task<Result> MarkAsReadAsync(Guid tenantId, Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task<Result> MarkAllAsReadAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<NotificationPreferencesResponse> GetPreferencesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> UpdatePreferencesAsync(Guid tenantId, Guid userId, UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default);
    Task<Result> SendAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, NotificationChannelEligibility>> GetChannelEligibilityAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, string type, CancellationToken cancellationToken = default, Guid? organizationId = null);
    /// <summary>Persists a notification and its enabled delivery intents once for a deterministic notification ID.</summary>
    Task<Result<bool>> SendIdempotentAsync(Notification notification, bool allowPush, bool allowEmail, CancellationToken cancellationToken = default);
}
