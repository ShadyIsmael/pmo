namespace NEXUS.Modules.Notifications.Application;

public enum EmailNotificationProfileScope
{
    Platform,
    Tenant,
    Organization
}

/// <summary>SMTP requires authenticated TLS for every supported profile.</summary>
public enum SmtpSecurityMode
{
    StartTlsRequired,
    ImplicitTls
}

public enum SmtpAuthenticationMode
{
    UsernamePassword,
    TrustedRelay,
    /// <summary>Reserved for a future generic OAuth2/XOAUTH2 implementation; rejected for now.</summary>
    OAuth2
}

/// <summary>
/// Server-side SMTP settings. Password is an in-memory secret only and must never be projected
/// into an API response, browser cache, audit payload, or log.
/// </summary>
public sealed record SmtpTransportProfile(
    bool Enabled,
    string Host,
    int Port,
    SmtpSecurityMode SecurityMode,
    SmtpAuthenticationMode AuthenticationMode,
    string? UserName,
    string? Password,
    string FromAddress,
    string FromName,
    string? ReplyToAddress);

/// <summary>Persisted exact or resolved profile; secrets are available only to trusted server services.</summary>
public sealed record EmailNotificationProfileRecord(
    EmailNotificationProfileScope Scope,
    Guid? TenantId,
    Guid? OrganizationId,
    string Version,
    SmtpTransportProfile Profile);

public interface IEmailNotificationProfileStore
{
    Task<EmailNotificationProfileRecord?> GetExactAsync(
        EmailNotificationProfileScope scope,
        Guid? tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves Organization → Tenant → Platform, preserving a disabled override.</summary>
    Task<EmailNotificationProfileRecord?> ResolveEffectiveAsync(
        Guid? tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken = default);

    Task<EmailNotificationProfileRecord> UpsertAsync(
        EmailNotificationProfileScope scope,
        Guid? tenantId,
        Guid? organizationId,
        SmtpTransportProfile profile,
        string? expectedVersion,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        EmailNotificationProfileScope scope,
        Guid? tenantId,
        Guid? organizationId,
        string? expectedVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailNotificationDeliveryLogDto>> GetRecentDeliveryLogsAsync(
        EmailNotificationProfileScope scope,
        Guid? tenantId,
        Guid? organizationId,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>Adapter to the existing per-user Organization Settings grants.</summary>
public interface IEmailNotificationOrganizationScopeAccess
{
    Task<bool> HasAccessAsync(Guid organizationId, bool manage, CancellationToken cancellationToken = default);
}

public sealed record EmailNotificationDeliveryLogDto(
    Guid Id,
    DateTime CreatedAt,
    string EventType,
    string Status,
    int AttemptCount,
    DateTime? LastAttemptAt,
    string? FailureCategory,
    Guid? OrganizationId,
    bool IsTest);

public sealed record EmailNotificationProfileView(
    bool Enabled,
    string Host,
    int Port,
    SmtpSecurityMode SecurityMode,
    SmtpAuthenticationMode AuthenticationMode,
    string? UserName,
    bool HasPassword,
    string FromAddress,
    string FromName,
    string? ReplyToAddress,
    string Version);

public sealed record EmailNotificationSettingsResponse(
    EmailNotificationProfileScope Scope,
    Guid? OwnerId,
    bool PersistenceAvailable,
    bool HasOverride,
    EmailNotificationProfileView? OverrideProfile,
    EmailNotificationProfileView? EffectiveProfile,
    EmailNotificationProfileScope? EffectiveSourceScope,
    bool CanManage,
    bool CanTest,
    bool CanViewLogs,
    bool TrustedRelayAvailableForEffectiveHost,
    bool OAuth2Available = false);

public sealed record EmailNotificationSettingsAccessDto(
    bool CanReadTenant,
    bool CanManageTenant,
    bool CanTestTenant,
    bool CanReadTenantLogs,
    bool CanReadPlatform,
    bool CanManagePlatform,
    bool CanTestPlatform,
    bool OAuth2Available = false);

public sealed record UpdateEmailNotificationProfileRequest(
    bool Enabled,
    string Host,
    int Port,
    SmtpSecurityMode SecurityMode,
    SmtpAuthenticationMode AuthenticationMode,
    string? UserName,
    string? Password,
    bool ClearPassword,
    string FromAddress,
    string FromName,
    string? ReplyToAddress,
    string? ExpectedVersion);

public sealed record EmailNotificationOperationResultDto(
    bool Succeeded,
    string? FailureCode,
    string Summary,
    bool Accepted = false,
    bool IsTransient = false);

public sealed record EmailNotificationTrustedRelayCheckRequest(string Host);
public sealed record EmailNotificationTrustedRelayCheckDto(bool IsAuthorized);
public sealed record ResetEmailNotificationProfileRequest(string? ExpectedVersion);

public interface IEmailNotificationSettingsService
{
    Task<EmailNotificationSettingsAccessDto> GetAccessAsync(CancellationToken cancellationToken = default);

    Task<EmailNotificationSettingsResponse> GetSettingsAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        CancellationToken cancellationToken = default);

    Task<EmailNotificationSettingsResponse> UpdateSettingsAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        UpdateEmailNotificationProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<EmailNotificationSettingsResponse> ResetSettingsAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        string? expectedVersion,
        CancellationToken cancellationToken = default);

    Task<EmailNotificationOperationResultDto> TestConnectionAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        CancellationToken cancellationToken = default);

    Task<EmailNotificationOperationResultDto> SendTestEmailAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailNotificationDeliveryLogDto>> GetLogsAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<EmailNotificationTrustedRelayCheckDto> CheckTrustedRelayAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        string host,
        CancellationToken cancellationToken = default);
}

public sealed class EmailNotificationSettingsException : Exception
{
    public EmailNotificationSettingsException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }
    public string Code { get; }
}

public sealed class EmailNotificationProfileConcurrencyException : Exception
{
    public EmailNotificationProfileConcurrencyException(string message = "The SMTP profile changed after it was loaded.")
        : base(message) { }
}

