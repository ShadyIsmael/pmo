using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Infrastructure;

namespace NEXUS.Modules.Notifications.ModuleRegistration;

public static class NotificationsModuleRegistration
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddOptions<FirebaseOptions>().BindConfiguration("Firebase");
        services.AddOptions<NotificationDeliveryOptions>()
            .BindConfiguration("NotificationDelivery")
            .Validate(options => IsValid(options), "Notification delivery settings are invalid.")
            .ValidateOnStart();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDeviceTokenService, DeviceTokenService>();
        services.AddSingleton<FirebaseMessagingClient>();
        services.AddScoped<IPushNotificationSender, FirebaseNotificationChannel>();
        services.AddScoped<NotificationDeliveryProcessor>();
        services.AddHostedService<NotificationDeliveryWorker>();
        return services;
    }

    private static bool IsValid(NotificationDeliveryOptions options)
    {
        try { options.Validate(); return true; }
        catch (InvalidOperationException) { return false; }
    }
}
