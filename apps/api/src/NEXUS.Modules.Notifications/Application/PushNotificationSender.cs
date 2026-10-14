using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Modules.Notifications.Application;

public enum PushFailureKind
{
    Transient,
    Permanent,
    Unregistered,
}

public sealed record PushSendResult(
    bool Succeeded,
    PushFailureKind? FailureKind = null,
    string? FailureCategory = null,
    string? SafeFailureSummary = null,
    string? ProviderMessageId = null);

public interface IPushNotificationSender
{
    bool IsConfigured { get; }
    Task<PushSendResult> SendAsync(Notification notification, UserDeviceToken device, CancellationToken cancellationToken = default);
}
