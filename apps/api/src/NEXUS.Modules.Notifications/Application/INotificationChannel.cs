using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Modules.Notifications.Application;

public interface INotificationChannel
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
}
