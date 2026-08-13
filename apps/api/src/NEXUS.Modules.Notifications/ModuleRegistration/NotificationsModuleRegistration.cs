using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Notifications.Application;

namespace NEXUS.Modules.Notifications.ModuleRegistration;

public static class NotificationsModuleRegistration
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
