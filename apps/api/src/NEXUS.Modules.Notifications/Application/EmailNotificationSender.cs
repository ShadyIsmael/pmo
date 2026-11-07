using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Modules.Notifications.Application;

public enum EmailFailureKind
{
    Transient,
    Permanent,
}

public sealed record EmailSendResult(
    bool Succeeded,
    EmailFailureKind? FailureKind = null,
    string? FailureCategory = null,
    string? SafeFailureSummary = null);

public interface IEmailNotificationSender
{
    Task<bool> IsConfiguredAsync(Guid tenantId, Guid? organizationId, CancellationToken cancellationToken = default);
    Task<EmailSendResult> SendAsync(Notification notification, string recipientEmail, CancellationToken cancellationToken = default);
}
