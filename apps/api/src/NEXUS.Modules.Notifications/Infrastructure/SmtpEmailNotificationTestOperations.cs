using NEXUS.Modules.Notifications.Application;

namespace NEXUS.Modules.Notifications.Infrastructure;

/// <summary>Exposes connection verification and actual test delivery as separate operations.</summary>
public sealed class SmtpEmailNotificationTestOperations(ISmtpEmailNotificationTransport transport)
    : IEmailNotificationTestOperations
{
    public Task<SmtpTransportResult> TestConnectionAsync(
        SmtpTransportProfile profile,
        Guid? tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken = default) =>
        transport.TestConnectionAsync(profile, tenantId, organizationId, cancellationToken);

    public Task<SmtpTransportResult> SendTestEmailAsync(
        SmtpTransportProfile profile,
        Guid? tenantId,
        Guid? organizationId,
        string recipientAddress,
        SmtpEmailMessage message,
        CancellationToken cancellationToken = default) =>
        transport.SendAsync(profile, tenantId, organizationId, recipientAddress, message, cancellationToken);
}
