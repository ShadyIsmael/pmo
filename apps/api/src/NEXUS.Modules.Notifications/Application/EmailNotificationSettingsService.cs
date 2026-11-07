using System.Security.Claims;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using AspNetAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;

namespace NEXUS.Modules.Notifications.Application;

public sealed class EmailNotificationSettingsService : IEmailNotificationSettingsService
{
    private const string ViewPermission = "EmailNotifications.View";
    private const string ManagePermission = "EmailNotifications.Manage";
    private const string TestPermission = "EmailNotifications.Test";
    private const string LogsPermission = "EmailNotifications.Logs";
    private const int MaximumTestMessageLength = 250_000;

    private readonly IEmailNotificationProfileStore _profiles;
    private readonly IEmailNotificationOrganizationScopeAccess _organizationAccess;
    private readonly ISmtpDestinationPolicy _destinationPolicy;
    private readonly IEmailNotificationTestOperations _testOperations;
    private readonly IUserRepository _users;
    private readonly AspNetAuthorizationService _authorization;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditService _audit;

    public EmailNotificationSettingsService(
        IEmailNotificationProfileStore profiles,
        IEmailNotificationOrganizationScopeAccess organizationAccess,
        ISmtpDestinationPolicy destinationPolicy,
        IEmailNotificationTestOperations testOperations,
        IUserRepository users,
        AspNetAuthorizationService authorization,
        IHttpContextAccessor httpContextAccessor,
        IAuditService audit)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _organizationAccess = organizationAccess ?? throw new ArgumentNullException(nameof(organizationAccess));
        _destinationPolicy = destinationPolicy ?? throw new ArgumentNullException(nameof(destinationPolicy));
        _testOperations = testOperations ?? throw new ArgumentNullException(nameof(testOperations));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public async Task<EmailNotificationSettingsAccessDto> GetAccessAsync(CancellationToken cancellationToken = default)
    {
        var principal = CurrentPrincipal();
        return new EmailNotificationSettingsAccessDto(
            await IsAuthorizedAsync(principal, ViewPermission, cancellationToken),
            await IsAuthorizedAsync(principal, ManagePermission, cancellationToken),
            await IsAuthorizedAsync(principal, TestPermission, cancellationToken),
            await IsAuthorizedAsync(principal, LogsPermission, cancellationToken),
            await IsPlatformAuthorizedAsync(principal, "PlatformSettings.Read", ViewPermission, cancellationToken),
            await IsPlatformAuthorizedAsync(principal, "PlatformSettings.Write", ManagePermission, cancellationToken),
            await IsPlatformAuthorizedAsync(principal, "PlatformSettings.Write", TestPermission, cancellationToken));
    }

    public async Task<EmailNotificationSettingsResponse> GetSettingsAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        CancellationToken cancellationToken = default)
    {
        var actor = GetActor(requireTenant: scope is not EmailNotificationProfileScope.Platform);
        ValidateScopeOwner(scope, organizationId);
        await EnsureAuthorizedAsync(scope, EmailSettingsAction.View, cancellationToken);
        await EnsureOrganizationAccessAsync(scope, organizationId, manage: false, cancellationToken);

        var exact = await _profiles.GetExactAsync(scope, OwnerTenantId(scope, actor.TenantId), OwnerOrganizationId(scope, organizationId), cancellationToken);
        var effective = await _profiles.ResolveEffectiveAsync(
            scope == EmailNotificationProfileScope.Platform ? null : actor.TenantId,
            scope == EmailNotificationProfileScope.Organization ? organizationId : null,
            cancellationToken);

        var canManage = await IsScopeAuthorizedAsync(scope, EmailSettingsAction.Manage, cancellationToken)
            && (scope != EmailNotificationProfileScope.Organization
                || await _organizationAccess.HasAccessAsync(organizationId!.Value, manage: true, cancellationToken));
        var canTest = await IsScopeAuthorizedAsync(scope, EmailSettingsAction.Test, cancellationToken);
        var canViewLogs = scope is not EmailNotificationProfileScope.Platform
            && await IsScopeAuthorizedAsync(scope, EmailSettingsAction.Logs, cancellationToken);
        var trustedRelayAvailable = effective is not null
            && _destinationPolicy.IsTrustedRelayHostAuthorized(
                OwnerTenantId(scope, actor.TenantId), OwnerOrganizationId(scope, organizationId), NormalizeHost(effective.Profile.Host));

        return new EmailNotificationSettingsResponse(
            scope,
            OwnerId(scope, actor.TenantId, organizationId),
            PersistenceAvailable: true,
            HasOverride: exact is not null,
            OverrideProfile: exact is null ? null : ToSafeView(exact),
            EffectiveProfile: effective is null ? null : ToSafeView(effective),
            EffectiveSourceScope: effective?.Scope,
            CanManage: canManage,
            CanTest: canTest,
            CanViewLogs: canViewLogs,
            TrustedRelayAvailableForEffectiveHost: trustedRelayAvailable);
    }

    public async Task<EmailNotificationSettingsResponse> UpdateSettingsAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        UpdateEmailNotificationProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = GetActor(requireTenant: true);
        ValidateScopeOwner(scope, organizationId);
        await EnsureAuthorizedAsync(scope, EmailSettingsAction.Manage, cancellationToken);
        await EnsureOrganizationAccessAsync(scope, organizationId, manage: true, cancellationToken);

        var tenantId = OwnerTenantId(scope, actor.TenantId);
        var ownerOrganizationId = OwnerOrganizationId(scope, organizationId);
        var current = await _profiles.GetExactAsync(scope, tenantId, ownerOrganizationId, cancellationToken);
        EnsureExpectedVersion(current, request.ExpectedVersion);

        if (request.AuthenticationMode == SmtpAuthenticationMode.OAuth2)
            throw Validation("OAuth2/XOAUTH2 is not available in this release. Use SMTP username and password or a deployment-authorized trusted relay.");

        var host = NormalizeHost(request.Host);
        var userName = NormalizeNullable(request.UserName);
        var password = request.ClearPassword
            ? null
            : string.IsNullOrEmpty(request.Password) ? current?.Profile.Password : request.Password;

        if (request.AuthenticationMode == SmtpAuthenticationMode.TrustedRelay)
        {
            if (!string.IsNullOrEmpty(request.Password) || !string.IsNullOrEmpty(request.UserName))
                throw Validation("Trusted relay profiles cannot contain SMTP credentials.");
            if (!_destinationPolicy.IsTrustedRelayHostAuthorized(tenantId, ownerOrganizationId, host))
                throw Validation("This SMTP host is not approved for trusted relay at the selected scope.");
            userName = null;
            password = null;
        }
        else if (request.AuthenticationMode != SmtpAuthenticationMode.UsernamePassword)
        {
            throw Validation("The selected SMTP authentication mode is not supported.");
        }

        var profile = new SmtpTransportProfile(
            request.Enabled,
            host,
            request.Port,
            request.SecurityMode,
            request.AuthenticationMode,
            userName,
            password,
            request.FromAddress?.Trim() ?? string.Empty,
            request.FromName?.Trim() ?? string.Empty,
            NormalizeNullable(request.ReplyToAddress));

        ValidateProfile(profile);
        var saved = await _profiles.UpsertAsync(scope, tenantId, ownerOrganizationId, profile, request.ExpectedVersion, cancellationToken);
        var response = await GetSettingsAsync(scope, organizationId, cancellationToken);
        return response with
        {
            HasOverride = true,
            OverrideProfile = ToSafeView(saved),
            EffectiveProfile = ToSafeView(saved),
            EffectiveSourceScope = saved.Scope,
            TrustedRelayAvailableForEffectiveHost = _destinationPolicy.IsTrustedRelayHostAuthorized(
                OwnerTenantId(scope, actor.TenantId), OwnerOrganizationId(scope, organizationId), NormalizeHost(saved.Profile.Host))
        };
    }

    public async Task<EmailNotificationSettingsResponse> ResetSettingsAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        string? expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var actor = GetActor(requireTenant: true);
        ValidateScopeOwner(scope, organizationId);
        await EnsureAuthorizedAsync(scope, EmailSettingsAction.Manage, cancellationToken);
        await EnsureOrganizationAccessAsync(scope, organizationId, manage: true, cancellationToken);
        var tenantId = OwnerTenantId(scope, actor.TenantId);
        var ownerOrganizationId = OwnerOrganizationId(scope, organizationId);
        var current = await _profiles.GetExactAsync(scope, tenantId, ownerOrganizationId, cancellationToken);
        if (current is null) throw new EmailNotificationSettingsException(404, "profileOverrideNotFound", "There is no SMTP profile override at this scope.");
        EnsureExpectedVersion(current, expectedVersion);
        await _profiles.DeleteAsync(scope, tenantId, ownerOrganizationId, expectedVersion, cancellationToken);
        return await GetSettingsAsync(scope, organizationId, cancellationToken);
    }

    public async Task<EmailNotificationOperationResultDto> TestConnectionAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        CancellationToken cancellationToken = default)
    {
        var actor = GetActor(requireTenant: true);
        ValidateScopeOwner(scope, organizationId);
        await EnsureAuthorizedAsync(scope, EmailSettingsAction.Test, cancellationToken);
        await EnsureOrganizationAccessAsync(scope, organizationId, manage: false, cancellationToken);
        var operationTenantId = OwnerTenantId(scope, actor.TenantId);
        var operationOrganizationId = OwnerOrganizationId(scope, organizationId);
        var profileRecord = await GetEffectiveProfileForOperationAsync(scope, actor.TenantId!.Value, organizationId, cancellationToken);
        ValidateProfileForOperation(profileRecord, operationTenantId, operationOrganizationId);
        await WriteTestAuditAsync(actor, profileRecord, scope, organizationId, "ConnectionTestAttempted", "Attempted", cancellationToken);

        SmtpTransportResult result;
        try
        {
            result = await _testOperations.TestConnectionAsync(
                profileRecord.Profile, operationTenantId, operationOrganizationId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            result = SmtpTransportResult.Failure("ConnectionTestFailed", "The SMTP connection test could not be completed.", isTransient: true);
        }

        await TryWriteTestOutcomeAuditAsync(actor, profileRecord, scope, organizationId, "ConnectionTest", result.Succeeded, cancellationToken);
        return new EmailNotificationOperationResultDto(
            result.Succeeded,
            result.FailureCode,
            SafeSummary(result, "The SMTP connection was verified.", "The SMTP connection could not be verified."),
            Accepted: false,
            IsTransient: result.IsTransient);
    }

    public async Task<EmailNotificationOperationResultDto> SendTestEmailAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        CancellationToken cancellationToken = default)
    {
        var actor = GetActor(requireTenant: true);
        ValidateScopeOwner(scope, organizationId);
        await EnsureAuthorizedAsync(scope, EmailSettingsAction.Test, cancellationToken);
        await EnsureOrganizationAccessAsync(scope, organizationId, manage: false, cancellationToken);
        var operationTenantId = OwnerTenantId(scope, actor.TenantId);
        var operationOrganizationId = OwnerOrganizationId(scope, organizationId);
        var profileRecord = await GetEffectiveProfileForOperationAsync(scope, actor.TenantId!.Value, organizationId, cancellationToken);
        ValidateProfileForOperation(profileRecord, operationTenantId, operationOrganizationId);

        var user = await _users.GetByIdAsync(actor.UserId, cancellationToken);
        if (user is null || user.TenantId != actor.TenantId || !user.IsActive || user.IsDeleted
            || !MailAddress.TryCreate(user.Email, out var recipient))
            throw new EmailNotificationSettingsException(403, "testRecipientUnavailable", "A valid active account address is required to send a test email.");

        await WriteTestAuditAsync(actor, profileRecord, scope, organizationId, "TestEmailAttempted", "Attempted", cancellationToken);
        var message = CreateTestMessage(CurrentLanguage(CurrentPrincipal()));
        if (message.PlainTextBody.Length > MaximumTestMessageLength)
            throw new InvalidOperationException("The fixed test message exceeded its size limit.");

        SmtpTransportResult result;
        try
        {
            result = await _testOperations.SendTestEmailAsync(
                profileRecord.Profile,
                operationTenantId,
                operationOrganizationId,
                recipient.Address,
                message,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            result = SmtpTransportResult.Failure("TestEmailFailed", "The test email could not be accepted by the SMTP server.", isTransient: true);
        }

        await TryWriteTestOutcomeAuditAsync(actor, profileRecord, scope, organizationId, "TestEmail", result.Succeeded, cancellationToken);
        return new EmailNotificationOperationResultDto(
            result.Succeeded,
            result.FailureCode,
            SafeSummary(result, "The SMTP server accepted the test email.", "The SMTP server did not accept the test email."),
            Accepted: result.Succeeded,
            IsTransient: result.IsTransient);
    }

    public async Task<IReadOnlyList<EmailNotificationDeliveryLogDto>> GetLogsAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (scope is EmailNotificationProfileScope.Platform)
            throw Validation("Platform-wide email delivery logs are not exposed from this workspace.");
        var actor = GetActor(requireTenant: true);
        ValidateScopeOwner(scope, organizationId);
        await EnsureAuthorizedAsync(scope, EmailSettingsAction.Logs, cancellationToken);
        await EnsureOrganizationAccessAsync(scope, organizationId, manage: false, cancellationToken);
        return await _profiles.GetRecentDeliveryLogsAsync(
            scope, actor.TenantId, organizationId, Math.Clamp(limit, 1, 100), cancellationToken);
    }

    public async Task<EmailNotificationTrustedRelayCheckDto> CheckTrustedRelayAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        string host,
        CancellationToken cancellationToken = default)
    {
        var actor = GetActor(requireTenant: scope is not EmailNotificationProfileScope.Platform);
        ValidateScopeOwner(scope, organizationId);
        await EnsureAuthorizedAsync(scope, EmailSettingsAction.Manage, cancellationToken);
        await EnsureOrganizationAccessAsync(scope, organizationId, manage: true, cancellationToken);
        var normalizedHost = NormalizeHost(host);
        if (!IsValidHost(normalizedHost)) throw Validation("Enter a valid SMTP hostname before checking trusted relay availability.");
        return new EmailNotificationTrustedRelayCheckDto(_destinationPolicy.IsTrustedRelayHostAuthorized(
            OwnerTenantId(scope, actor.TenantId), OwnerOrganizationId(scope, organizationId), normalizedHost));
    }

    private async Task<EmailNotificationProfileRecord> GetEffectiveProfileForOperationAsync(
        EmailNotificationProfileScope scope,
        Guid tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var result = await _profiles.ResolveEffectiveAsync(
            scope == EmailNotificationProfileScope.Platform ? null : tenantId,
            scope == EmailNotificationProfileScope.Organization ? organizationId : null,
            cancellationToken);
        if (result is null)
            throw new EmailNotificationSettingsException(409, "profileNotConfigured", "No SMTP profile is configured at this scope or an inherited scope.");
        return result;
    }

    private void ValidateProfileForOperation(
        EmailNotificationProfileRecord record,
        Guid? operationTenantId,
        Guid? operationOrganizationId)
    {
        ValidateProfile(record.Profile);
        if (!record.Profile.Enabled)
            throw new EmailNotificationSettingsException(409, "profileDisabled", "The effective SMTP profile is disabled.");
        if (record.Profile.AuthenticationMode == SmtpAuthenticationMode.OAuth2)
            throw new EmailNotificationSettingsException(409, "oauthUnavailable", "OAuth2/XOAUTH2 is not available in this release.");
        if (record.Profile.AuthenticationMode == SmtpAuthenticationMode.TrustedRelay
            && !_destinationPolicy.IsTrustedRelayHostAuthorized(operationTenantId, operationOrganizationId, NormalizeHost(record.Profile.Host)))
            throw new EmailNotificationSettingsException(409, "trustedRelayNotAuthorized", "The trusted relay is no longer approved for this SMTP host and scope.");
    }

    private async Task EnsureAuthorizedAsync(
        EmailNotificationProfileScope scope,
        EmailSettingsAction action,
        CancellationToken cancellationToken)
    {
        if (scope == EmailNotificationProfileScope.Platform)
        {
            var capability = action == EmailSettingsAction.View ? "PlatformSettings.Read"
                : action is EmailSettingsAction.Manage or EmailSettingsAction.Test ? "PlatformSettings.Write" : null;
            var permission = action switch
            {
                EmailSettingsAction.View => ViewPermission,
                EmailSettingsAction.Manage => ManagePermission,
                EmailSettingsAction.Test => TestPermission,
                _ => null
            };
            if (capability is null || permission is null
                || !await IsPlatformAuthorizedAsync(CurrentPrincipal(), capability, permission, cancellationToken))
                throw new EmailNotificationSettingsException(403, "emailNotificationAccessDenied", "The required email notification and Platform settings permissions are not both granted.");
            return;
        }

        var policy = action switch
            {
                EmailSettingsAction.View => ViewPermission,
                EmailSettingsAction.Manage => ManagePermission,
                EmailSettingsAction.Test => TestPermission,
                EmailSettingsAction.Logs => LogsPermission,
                _ => null
            };

        if (policy is null || !await IsAuthorizedAsync(CurrentPrincipal(), policy, cancellationToken))
            throw new EmailNotificationSettingsException(403, "emailNotificationAccessDenied", "The required email notification permission is not granted.");
    }

    private async Task<bool> IsScopeAuthorizedAsync(
        EmailNotificationProfileScope scope,
        EmailSettingsAction action,
        CancellationToken cancellationToken)
    {
        if (scope == EmailNotificationProfileScope.Platform)
        {
            var capability = action == EmailSettingsAction.View ? "PlatformSettings.Read"
                : action is EmailSettingsAction.Manage or EmailSettingsAction.Test ? "PlatformSettings.Write" : null;
            var permission = action switch
            {
                EmailSettingsAction.View => ViewPermission,
                EmailSettingsAction.Manage => ManagePermission,
                EmailSettingsAction.Test => TestPermission,
                _ => null
            };
            return capability is not null && permission is not null
                && await IsPlatformAuthorizedAsync(CurrentPrincipal(), capability, permission, cancellationToken);
        }

        var policy = action switch
            {
                EmailSettingsAction.View => ViewPermission,
                EmailSettingsAction.Manage => ManagePermission,
                EmailSettingsAction.Test => TestPermission,
                EmailSettingsAction.Logs => LogsPermission,
                _ => null
            };
        return policy is not null && await IsAuthorizedAsync(CurrentPrincipal(), policy, cancellationToken);
    }

    private async Task EnsureOrganizationAccessAsync(
        EmailNotificationProfileScope scope,
        Guid? organizationId,
        bool manage,
        CancellationToken cancellationToken)
    {
        if (scope != EmailNotificationProfileScope.Organization) return;
        if (organizationId is null || !await _organizationAccess.HasAccessAsync(organizationId.Value, manage, cancellationToken))
            throw new EmailNotificationSettingsException(403, "organizationSettingsAccessDenied", "An explicit same-tenant Organization Settings grant is required.");
    }

    private async Task<bool> IsAuthorizedAsync(ClaimsPrincipal principal, string policy, CancellationToken cancellationToken)
    {
        var result = await _authorization.AuthorizeAsync(principal, resource: null, policyName: policy);
        return result.Succeeded;
    }

    private async Task<bool> IsPlatformAuthorizedAsync(
        ClaimsPrincipal principal,
        string platformCapability,
        string emailNotificationPermission,
        CancellationToken cancellationToken) =>
        await IsAuthorizedAsync(principal, platformCapability, cancellationToken)
        && await IsAuthorizedAsync(principal, emailNotificationPermission, cancellationToken);

    private async Task WriteTestAuditAsync(
        ActorScope actor,
        EmailNotificationProfileRecord profile,
        EmailNotificationProfileScope requestedScope,
        Guid? requestedOrganizationId,
        string action,
        string outcome,
        CancellationToken cancellationToken)
    {
        var entry = BuildTestAuditEntry(actor, profile, requestedScope, requestedOrganizationId, action, outcome);
        var result = await _audit.LogAsync(entry, cancellationToken);
        if (result.IsFailure)
            throw new EmailNotificationSettingsException(503, "emailNotificationAuditUnavailable", "The test action could not be recorded, so it was not run.");
    }

    private async Task TryWriteTestOutcomeAuditAsync(
        ActorScope actor,
        EmailNotificationProfileRecord profile,
        EmailNotificationProfileScope requestedScope,
        Guid? requestedOrganizationId,
        string operation,
        bool succeeded,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = BuildTestAuditEntry(actor, profile, requestedScope, requestedOrganizationId,
                $"{operation}{(succeeded ? "Accepted" : "Failed")}", succeeded ? "Succeeded" : "Failed");
            await _audit.LogAsync(entry, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The attempt audit is already durable. An outcome audit failure must not hide the
            // sanitized provider result or claim that the test was not sent.
        }
    }

    private AuditEntry BuildTestAuditEntry(
        ActorScope actor,
        EmailNotificationProfileRecord profile,
        EmailNotificationProfileScope requestedScope,
        Guid? requestedOrganizationId,
        string action,
        string outcome)
    {
        var ownerId = requestedOrganizationId ?? actor.TenantId ?? actor.UserId;
        var correlationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? _httpContextAccessor.HttpContext?.TraceIdentifier;
        return new AuditEntry
        {
            TenantId = actor.TenantId ?? throw new EmailNotificationSettingsException(403, "tenantContextRequired", "A trusted tenant context is required for audited test operations."),
            EntityType = "EmailNotificationProfile",
            EntityId = ownerId,
            Action = action,
            ChangedFields = "Scope,OrganizationId,Outcome",
            Description = $"Email notification {action} at {requestedScope} scope: {outcome}.",
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                scope = requestedScope.ToString(),
                organizationId = requestedOrganizationId,
                outcome
            }),
            Source = "User",
            CorrelationId = correlationId,
            UserId = actor.UserId,
            CreatedBy = actor.UserId.ToString()
        };
    }

    private ActorScope GetActor(bool requireTenant)
    {
        var principal = CurrentPrincipal();
        if (principal.Identity?.IsAuthenticated != true)
            throw new EmailNotificationSettingsException(401, "authenticationRequired", "Authentication is required.");
        if (!TryGetActorId(principal, out var actorId))
            throw new EmailNotificationSettingsException(401, "actorUnavailable", "The authenticated actor could not be resolved.");
        var tenantId = Guid.TryParse(principal.FindFirstValue("tenantId"), out var parsedTenant) && parsedTenant != Guid.Empty
            ? parsedTenant
            : (Guid?)null;
        if (requireTenant && tenantId is null)
            throw new EmailNotificationSettingsException(403, "tenantContextRequired", "A trusted tenant context is required for this operation.");
        return new ActorScope(actorId, tenantId);
    }

    private ClaimsPrincipal CurrentPrincipal() => _httpContextAccessor.HttpContext?.User
        ?? throw new EmailNotificationSettingsException(401, "authenticationRequired", "Authentication is required.");

    private static bool TryGetActorId(ClaimsPrincipal principal, out Guid actorId) =>
        (Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId)
            || Guid.TryParse(principal.FindFirstValue("sub"), out actorId))
        && actorId != Guid.Empty;

    private static void ValidateScopeOwner(EmailNotificationProfileScope scope, Guid? organizationId)
    {
        if (scope == EmailNotificationProfileScope.Organization)
        {
            if (organizationId is null || organizationId == Guid.Empty)
                throw Validation("An Organization ID is required for Organization scope.");
        }
        else if (organizationId.HasValue)
        {
            throw Validation("An Organization ID is only valid for Organization scope.");
        }
    }

    private static void EnsureExpectedVersion(EmailNotificationProfileRecord? current, string? expectedVersion)
    {
        if (current is null && !string.IsNullOrWhiteSpace(expectedVersion))
            throw new EmailNotificationSettingsException(409, "profileConcurrencyConflict", "The SMTP profile changed. Reload the settings and try again.");
        if (current is not null && (string.IsNullOrWhiteSpace(expectedVersion)
            || !string.Equals(expectedVersion, current.Version, StringComparison.Ordinal)))
            throw new EmailNotificationSettingsException(409, "profileConcurrencyConflict", "The SMTP profile changed. Reload the settings and try again.");
    }

    private static void ValidateProfile(SmtpTransportProfile profile)
    {
        if (!IsValidHost(profile.Host)) throw Validation("Enter a valid SMTP hostname.");
        if (profile.Port is < 1 or > 65535) throw Validation("SMTP port must be from 1 through 65535.");
        if (profile.SecurityMode is not (SmtpSecurityMode.StartTlsRequired or SmtpSecurityMode.ImplicitTls))
            throw Validation("SMTP security must require STARTTLS or use implicit TLS.");
        if (profile.AuthenticationMode == SmtpAuthenticationMode.OAuth2)
            throw Validation("OAuth2/XOAUTH2 is not available in this release.");
        if (profile.AuthenticationMode == SmtpAuthenticationMode.UsernamePassword)
        {
            if (profile.UserName is { Length: > 512 }) throw Validation("SMTP username cannot exceed 512 characters.");
            if (profile.Enabled && string.IsNullOrWhiteSpace(profile.UserName)) throw Validation("SMTP username is required when username/password authentication is selected.");
            if (profile.Enabled && string.IsNullOrWhiteSpace(profile.Password)) throw Validation("A saved SMTP password is required when the profile is enabled.");
        }
        else if (profile.AuthenticationMode == SmtpAuthenticationMode.TrustedRelay)
        {
            if (!string.IsNullOrWhiteSpace(profile.UserName) || !string.IsNullOrWhiteSpace(profile.Password))
                throw Validation("Trusted relay profiles cannot contain SMTP credentials.");
        }
        else throw Validation("The selected SMTP authentication mode is not supported.");

        if (!IsValidAddress(profile.FromAddress)) throw Validation("Enter a valid verified sender email address.");
        if (profile.FromName.Length > 200 || ContainsHeaderControl(profile.FromName))
            throw Validation("Sender name cannot exceed 200 characters or include control characters.");
        if (profile.ReplyToAddress is { Length: > 320 } || profile.ReplyToAddress is not null && !IsValidAddress(profile.ReplyToAddress))
            throw Validation("Enter a valid reply-to email address or leave it blank.");
        if (profile.Password is { Length: > 4096 }) throw Validation("SMTP password cannot exceed 4096 characters.");
    }

    private static EmailNotificationProfileView ToSafeView(EmailNotificationProfileRecord record) => new(
        record.Profile.Enabled,
        record.Profile.Host,
        record.Profile.Port,
        record.Profile.SecurityMode,
        record.Profile.AuthenticationMode,
        record.Profile.UserName,
        !string.IsNullOrEmpty(record.Profile.Password),
        record.Profile.FromAddress,
        record.Profile.FromName,
        record.Profile.ReplyToAddress,
        record.Version);

    private static EmailNotificationSettingsException Validation(string detail) =>
        new(400, "emailNotificationValidationFailed", detail);

    private static Guid? OwnerTenantId(EmailNotificationProfileScope scope, Guid? tenantId) =>
        scope is EmailNotificationProfileScope.Tenant or EmailNotificationProfileScope.Organization ? tenantId : null;

    private static Guid? OwnerOrganizationId(EmailNotificationProfileScope scope, Guid? organizationId) =>
        scope == EmailNotificationProfileScope.Organization ? organizationId : null;

    private static Guid? OwnerId(EmailNotificationProfileScope scope, Guid? tenantId, Guid? organizationId) =>
        scope switch
        {
            EmailNotificationProfileScope.Platform => null,
            EmailNotificationProfileScope.Tenant => tenantId,
            EmailNotificationProfileScope.Organization => organizationId,
            _ => null
        };

    private static string NormalizeHost(string? host) => (host ?? string.Empty).Trim().TrimEnd('.');
    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsValidHost(string? host) => !string.IsNullOrWhiteSpace(host)
        && host.Length <= 253
        && Uri.CheckHostName(host) != UriHostNameType.Unknown
        && !host.Any(char.IsWhiteSpace)
        && !host.Contains("://", StringComparison.Ordinal)
        && !ContainsHeaderControl(host);
    private static bool IsValidAddress(string? value) => !string.IsNullOrWhiteSpace(value) && MailAddress.TryCreate(value, out _);
    private static bool ContainsHeaderControl(string value) => value.Any(character => char.IsControl(character));
    private static string SafeSummary(SmtpTransportResult result, string successFallback, string failureFallback) =>
        string.IsNullOrWhiteSpace(result.SafeSummary) ? (result.Succeeded ? successFallback : failureFallback) : result.SafeSummary!;

    private static SmtpEmailMessage CreateTestMessage(string language)
    {
        var isArabic = language.Equals("ar", StringComparison.OrdinalIgnoreCase);
        var subject = isArabic ? "اختبار إعدادات البريد - FOPX PM" : "Email settings test - FOPX PM";
        var body = isArabic
            ? "هذه رسالة اختبار من FOPX PM للتحقق من قبول خادم SMTP للرسائل."
            : "This is a test email from FOPX PM to verify that the SMTP server accepts messages.";
        var html = isArabic
            ? "<!doctype html><html lang=\"ar\" dir=\"rtl\"><body><p>هذه رسالة اختبار من FOPX PM للتحقق من قبول خادم SMTP للرسائل.</p></body></html>"
            : "<!doctype html><html lang=\"en\" dir=\"ltr\"><body><p>This is a test email from FOPX PM to verify that the SMTP server accepts messages.</p></body></html>";
        return new SmtpEmailMessage(subject, body, html);
    }

    private static string CurrentLanguage(ClaimsPrincipal principal)
    {
        var locale = principal.FindFirstValue("locale") ?? principal.FindFirstValue("preferred_language");
        return string.Equals(locale, "ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
    }

    private sealed record ActorScope(Guid UserId, Guid? TenantId);
    private enum EmailSettingsAction { View, Manage, Test, Logs }
}

