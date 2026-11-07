using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Notifications.Infrastructure;

/// <summary>Firebase SDK boundary. No Firebase types escape into application services.</summary>
public sealed class FirebaseNotificationChannel : IPushNotificationSender
{
    private const string ProductName = "FOPX PM";
    private readonly FirebaseMessagingClient _client;
    private readonly NotificationDeliveryOptions _options;
    private readonly ILogger<FirebaseNotificationChannel> _logger;
    private readonly IProjectPublicIdLookup _projectPublicIdLookup;

    public FirebaseNotificationChannel(FirebaseMessagingClient client, IOptions<NotificationDeliveryOptions> options, ILogger<FirebaseNotificationChannel> logger, IProjectPublicIdLookup projectPublicIdLookup)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
        _projectPublicIdLookup = projectPublicIdLookup;
    }

    public bool IsConfigured => _client.IsConfigured;

    public async Task<PushSendResult> SendAsync(NEXUS.Modules.Notifications.Domain.Notification notification, UserDeviceToken device, CancellationToken cancellationToken = default)
    {
        // Push may appear on a lock screen. Keep payload content generic. Project links
        // use only the tenant-scoped public identifier; the API rechecks user access.
        var referenceId = notification.ReferenceId?.ToString() ?? string.Empty;
        if (string.Equals(notification.ReferenceType, "Project", StringComparison.OrdinalIgnoreCase))
        {
            referenceId = notification.ReferenceId.HasValue
                ? (await _projectPublicIdLookup.FindPublicIdAsync(notification.ReferenceId.Value, notification.TenantId, cancellationToken))?.ToString("D") ?? string.Empty
                : string.Empty;
        }
        var data = new Dictionary<string, string>
        {
            ["notificationId"] = notification.Id.ToString(),
            ["type"] = notification.Type,
            ["referenceType"] = notification.ReferenceType ?? string.Empty,
            ["referenceId"] = referenceId,
        };

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.SendTimeout);
            var messageId = await _client.SendAsync(device.Token, $"You have a {ProductName} update", $"Open {ProductName} to review your notification.", data, timeout.Token);
            return new PushSendResult(true, ProviderMessageId: messageId);
        }
        catch (FirebaseMessagingException exception)
        {
            var result = Classify(exception.MessagingErrorCode);
            if (result.FailureKind == PushFailureKind.Unregistered)
                _logger.LogInformation("FCM reported an unregistered device. DeviceTokenId={DeviceTokenId}", device.Id);
            else
                _logger.LogWarning("FCM delivery failed. DeviceTokenId={DeviceTokenId} FailureCategory={FailureCategory}", device.Id, result.FailureCategory);
            return result;
        }
        catch (FirebaseMessagingUnavailableException)
        {
            return new PushSendResult(false, PushFailureKind.Permanent, "FirebaseConfiguration", "Push delivery is unavailable because Firebase is not configured.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new PushSendResult(false, PushFailureKind.Transient, "Timeout", "The push provider timed out.");
        }
        catch (Exception)
        {
            // Provider exceptions can contain registration tokens; never persist or log their text.
            return new PushSendResult(false, PushFailureKind.Transient, "ProviderError", "The push provider could not complete the request.");
        }
    }

    private static PushSendResult Classify(MessagingErrorCode? errorCode) => errorCode switch
    {
        MessagingErrorCode.Unregistered => new(false, PushFailureKind.Unregistered, "UnregisteredToken", "This device token is no longer registered."),
        // The generated payload is fixed, bounded, and uses only valid data keys.
        // With that contract, FCM InvalidArgument is treated as a stale/invalid
        // registration so the device is deactivated instead of retried for every event.
        MessagingErrorCode.InvalidArgument => new(false, PushFailureKind.Unregistered, "InvalidRegistrationToken", "This device registration was rejected by the push provider."),
        MessagingErrorCode.SenderIdMismatch => new(false, PushFailureKind.Permanent, "SenderMismatch", "This device token belongs to a different Firebase project."),
        MessagingErrorCode.ThirdPartyAuthError => new(false, PushFailureKind.Permanent, "ProviderConfiguration", "Firebase rejected the push authentication configuration."),
        MessagingErrorCode.Unavailable or MessagingErrorCode.Internal or MessagingErrorCode.QuotaExceeded =>
            new(false, PushFailureKind.Transient, errorCode.ToString(), "The push provider is temporarily unavailable."),
        _ => new(false, PushFailureKind.Transient, "ProviderError", "The push provider could not complete the request."),
    };
}
