using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using DomainNotification = NEXUS.Modules.Notifications.Domain.Notification;

namespace NEXUS.Modules.Notifications.Infrastructure;

public sealed class FirebaseNotificationChannel : INotificationChannel
{
    private readonly DbContext _context;
    private readonly FirebaseMessagingClient _client;
    private readonly ILogger<FirebaseNotificationChannel> _logger;

    public FirebaseNotificationChannel(DbContext context, FirebaseMessagingClient client, ILogger<FirebaseNotificationChannel> logger)
    {
        _context = context;
        _client = client;
        _logger = logger;
    }

    public async Task SendAsync(DomainNotification notification, CancellationToken cancellationToken = default)
    {
        if (!notification.UserId.HasValue) return;

        var devices = await _context.Set<UserDeviceToken>()
            .Where(device => device.TenantId == notification.TenantId && device.UserId == notification.UserId.Value && device.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var device in devices)
        {
            try { await _client.SendAsync(device.Token, notification, cancellationToken); }
            catch (FirebaseMessagingException exception) when (exception.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                device.IsActive = false;
                device.DeactivatedAt = DateTime.UtcNow;
                device.UpdatedAt = DateTime.UtcNow;
                device.UpdatedBy = "Firebase";
                _logger.LogInformation("Deactivated an unregistered push device for UserId={UserId}.", device.UserId);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "FCM delivery failed for UserId={UserId}; the in-app notification is retained.", device.UserId);
            }
        }

        if (devices.Any(device => !device.IsActive)) await _context.SaveChangesAsync(cancellationToken);
    }
}
