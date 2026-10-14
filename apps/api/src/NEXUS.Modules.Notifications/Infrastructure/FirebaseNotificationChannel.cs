using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Modules.Notifications.Infrastructure;

/// <summary>Firebase SDK boundary. No Firebase types escape into application services.</summary>
public sealed class FirebaseNotificationChannel : IPushNotificationSender
{
    private readonly FirebaseMessagingClient _client;
    private readonly NotificationDeliveryOptions _options;
    private readonly ILogger<FirebaseNotificationChannel> _logger;

    public FirebaseNotificationChannel(FirebaseMessagingClient client, IOptions<NotificationDeliveryOptions> options, ILogger<FirebaseNotificationChannel> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _client.IsConfigured;

    public async Task<PushSendResult> SendAsync(NEXUS.Modules.Notifications.Domain.Notification notification, UserDeviceToken device, CancellationToken cancellationToken = default)
    {
        // Push may appear on a lock screen. Keep payload content generic and send only
        // internal reference metadata; the authenticated NEXUS route performs access checks.
        var data = new Dictionary<string, string>
        {
            ["notificationId"] = notification.Id.ToString(),
            ["type"] = notification.Type,
            ["referenceType"] = notification.ReferenceType ?? string.Empty,
            ["referenceId"] = notification.ReferenceId?.ToString() ?? string.Empty,
        };

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.SendTimeout);
            var messageId = await _client.SendAsync(device.Token, "You have a NEXUS update", "Open NEXUS to review your notification.", data, timeout.Token);
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
