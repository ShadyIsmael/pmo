using System.Net;

namespace NEXUS.Modules.Notifications.Application;

/// <summary>
/// The transport boundary receives already-resolved, in-memory profile values. Secret fields
/// must be decrypted by the configuration owner immediately before use and must never be
/// returned from an API or written to logs.
/// </summary>
public interface ISmtpEmailNotificationTransport
{
    Task<SmtpTransportResult> TestConnectionAsync(
        SmtpTransportProfile profile,
        Guid? tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken = default);

    Task<SmtpTransportResult> SendAsync(
        SmtpTransportProfile profile,
        Guid? tenantId,
        Guid? organizationId,
        string recipientAddress,
        SmtpEmailMessage message,
        CancellationToken cancellationToken = default);
}

/// <summary>Deployment-managed SMTP egress policy. A profile cannot authorize its own destination.</summary>
public interface ISmtpDestinationPolicy
{
    /// <summary>
    /// Returns whether a resolved address is permitted for the effective Tenant/Organization
    /// scope. Public addresses can be permitted by the default policy; private and reserved
    /// targets require a matching deployment-managed scoped allowlist.
    /// </summary>
    bool IsDestinationAllowed(
        Guid? tenantId,
        Guid? organizationId,
        string normalizedHost,
        IPAddress resolvedAddress,
        SmtpAuthenticationMode authenticationMode);

    /// <summary>Used by the settings API to expose Trusted Relay only for an approved host/scope.</summary>
    bool IsTrustedRelayHostAuthorized(Guid? tenantId, Guid? organizationId, string host);
}

/// <summary>Provider-neutral message content. HTML should already be safe, encoded application output.</summary>
public sealed record SmtpEmailMessage(string Subject, string PlainTextBody, string? HtmlBody = null)
{
    public const int MaximumSubjectLength = 500;
    public const int MaximumBodyLength = 250_000;
}

/// <summary>Sanitized result safe to return from the administration API.</summary>
public sealed record SmtpTransportResult(
    bool Succeeded,
    string? FailureCode = null,
    string? SafeSummary = null,
    bool IsTransient = false)
{
    public static SmtpTransportResult Success() => new(true);

    public static SmtpTransportResult Failure(string code, string summary, bool isTransient = false) =>
        new(false, code, summary, isTransient);
}

/// <summary>
/// Separate operations keep a connection/authentication check from sending a real message.
/// The API layer must restrict the test recipient to the authenticated user's verified address.
/// </summary>
public interface IEmailNotificationTestOperations
{
    Task<SmtpTransportResult> TestConnectionAsync(
        SmtpTransportProfile profile,
        Guid? tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken = default);

    Task<SmtpTransportResult> SendTestEmailAsync(
        SmtpTransportProfile profile,
        Guid? tenantId,
        Guid? organizationId,
        string recipientAddress,
        SmtpEmailMessage message,
        CancellationToken cancellationToken = default);
}
