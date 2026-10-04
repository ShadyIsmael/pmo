using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Infrastructure;

namespace NEXUS.Modules.Notifications.ModuleRegistration;

public static class NotificationsModuleRegistration
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddOptions<FirebaseOptions>().BindConfiguration("Firebase");
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDeviceTokenService, DeviceTokenService>();
        services.AddSingleton<FirebaseMessagingClient>();
        services.AddScoped<INotificationChannel, FirebaseNotificationChannel>();
        return services;
    }
}
