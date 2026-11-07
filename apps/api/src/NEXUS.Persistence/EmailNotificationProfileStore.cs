using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Integrations.Infrastructure;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Persistence;

/// <summary>
/// SQL Server-backed scoped SMTP profile store. Plaintext credentials exist only in returned
/// in-memory profiles; persistence always uses the existing integration secret protector.
/// This entity has no ambient tenant filter, so every query includes explicit owner predicates.
/// </summary>
public sealed class EmailNotificationProfileStore : IEmailNotificationProfileStore
{
    private readonly AppDbContext _context;
    private readonly IIntegrationSecretProtector _secrets;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EmailNotificationProfileStore(
        AppDbContext context,
        IIntegrationSecretProtector secrets,
        IAuditService audit,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public async Task<EmailNotificationProfileRecord?> GetExactAsync(
        EmailNotificationProfileScope scope,
        Guid? tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken = default)
    {
        var ownerId = ValidateScopeOwner(scope, tenantId, organizationId);
        var row = await ActiveProfiles()
            .AsNoTracking()
            .SingleOrDefaultAsync(profile =>
                profile.Scope == ScopeName(scope)
                && profile.TenantId == tenantId
                && profile.OwnerId == ownerId
                && profile.OrganizationId == organizationId,
                cancellationToken);
        return row is null ? null : ToRecord(row);
    }

    public async Task<EmailNotificationProfileRecord?> ResolveEffectiveAsync(
        Guid? tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || organizationId == Guid.Empty || (tenantId is null && organizationId is not null))
            return null;

        if (organizationId.HasValue && !await OrganizationBelongsToTenantAsync(tenantId!.Value, organizationId.Value, cancellationToken))
            return null;

        var rows = await ActiveProfiles()
            .AsNoTracking()
            .Where(profile =>
                profile.Scope == ScopeName(EmailNotificationProfileScope.Platform)
                || (tenantId.HasValue && profile.Scope == ScopeName(EmailNotificationProfileScope.Tenant) && profile.TenantId == tenantId)
                || (tenantId.HasValue && organizationId.HasValue
                    && profile.Scope == ScopeName(EmailNotificationProfileScope.Organization)
                    && profile.TenantId == tenantId
                    && profile.OrganizationId == organizationId))
            .ToListAsync(cancellationToken);

        var effective = organizationId.HasValue
            ? rows.SingleOrDefault(profile => profile.Scope == ScopeName(EmailNotificationProfileScope.Organization)
                && profile.TenantId == tenantId && profile.OrganizationId == organizationId)
            : null;
        effective ??= tenantId.HasValue
            ? rows.SingleOrDefault(profile => profile.Scope == ScopeName(EmailNotificationProfileScope.Tenant)
                && profile.TenantId == tenantId)
            : null;
        effective ??= rows.SingleOrDefault(profile => profile.Scope == ScopeName(EmailNotificationProfileScope.Platform));
        return effective is null ? null : ToRecord(effective);
    }

    public async Task<EmailNotificationProfileRecord> UpsertAsync(
        EmailNotificationProfileScope scope,
        Guid? tenantId,
        Guid? organizationId,
        SmtpTransportProfile profile,
        string? expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var ownerId = ValidateScopeOwner(scope, tenantId, organizationId);
        ValidatePersistableProfile(profile);
        if (scope == EmailNotificationProfileScope.Organization
            && !await OrganizationBelongsToTenantAsync(tenantId!.Value, organizationId!.Value, cancellationToken))
            throw new EmailNotificationProfileConcurrencyException("The selected Organization is unavailable in this Tenant.");

        var actor = GetAuditActor();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var row = await ActiveProfiles()
                .AsTracking()
                .SingleOrDefaultAsync(existing =>
                    existing.Scope == ScopeName(scope)
                    && existing.TenantId == tenantId
                    && existing.OwnerId == ownerId
                    && existing.OrganizationId == organizationId,
                    cancellationToken);

            var previousVersion = row is null ? null : EncodeVersion(row.RowVersion);
            EnsureExpectedVersion(previousVersion, expectedVersion);
            var previous = row is null ? null : Snapshot(row);
            var now = DateTime.UtcNow;

            if (row is null)
            {
                row = new EmailNotificationProfileEntity
                {
                    Id = Guid.NewGuid(),
                    Scope = ScopeName(scope),
                    TenantId = tenantId,
                    OwnerId = ownerId,
                    OrganizationId = organizationId,
                    CreatedAt = now,
                    CreatedBy = actor.UserId.ToString()
                };
                _context.EmailNotificationProfiles.Add(row);
            }
            else
            {
                row.UpdatedAt = now;
                row.UpdatedBy = actor.UserId.ToString();
            }

            ApplyProfile(row, profile);
            await _context.SaveChangesAsync(cancellationToken);
            await AppendProfileAuditAsync(actor, row, previous, "Updated", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToRecord(row);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new EmailNotificationProfileConcurrencyException();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new EmailNotificationProfileConcurrencyException();
        }
    }

    public async Task DeleteAsync(
        EmailNotificationProfileScope scope,
        Guid? tenantId,
        Guid? organizationId,
        string? expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var ownerId = ValidateScopeOwner(scope, tenantId, organizationId);
        var actor = GetAuditActor();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var row = await ActiveProfiles()
                .AsTracking()
                .SingleOrDefaultAsync(existing =>
                    existing.Scope == ScopeName(scope)
                    && existing.TenantId == tenantId
                    && existing.OwnerId == ownerId
                    && existing.OrganizationId == organizationId,
                    cancellationToken);

            if (row is null)
                throw new EmailNotificationProfileConcurrencyException("The SMTP profile override no longer exists.");
            EnsureExpectedVersion(EncodeVersion(row.RowVersion), expectedVersion);

            var previous = Snapshot(row);
            var now = DateTime.UtcNow;
            row.DeletedAt = now;
            row.DeletedBy = actor.UserId.ToString();
            row.UpdatedAt = now;
            row.UpdatedBy = actor.UserId.ToString();

            await _context.SaveChangesAsync(cancellationToken);
            await AppendProfileAuditAsync(actor, row, previous, "Reset", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new EmailNotificationProfileConcurrencyException();
        }
    }

    public async Task<IReadOnlyList<EmailNotificationDeliveryLogDto>> GetRecentDeliveryLogsAsync(
        EmailNotificationProfileScope scope,
        Guid? tenantId,
        Guid? organizationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        ValidateScopeOwner(scope, tenantId, organizationId);
        if (scope == EmailNotificationProfileScope.Platform || !tenantId.HasValue)
            return Array.Empty<EmailNotificationDeliveryLogDto>();

        var deliveries = await (
            from delivery in _context.NotificationDeliveries.IgnoreQueryFilters().AsNoTracking()
            join notification in _context.Notifications.IgnoreQueryFilters().AsNoTracking()
                on new { delivery.TenantId, delivery.NotificationId } equals new { notification.TenantId, NotificationId = notification.Id }
            where delivery.TenantId == tenantId.Value
                && notification.TenantId == tenantId.Value
                && !delivery.IsDeleted
                && !notification.IsDeleted
                && delivery.Channel == NotificationDeliveryChannel.Email
                && (scope != EmailNotificationProfileScope.Organization || notification.OrganizationId == organizationId)
            orderby delivery.CreatedAt descending
            select new EmailNotificationDeliveryLogDto(
                delivery.Id,
                delivery.CreatedAt,
                notification.Type,
                DeliveryStatusName(delivery.Status),
                delivery.AttemptCount,
                delivery.LastAttemptAt,
                delivery.FailureCategory,
                notification.OrganizationId,
                false))
            .Take(limit)
            .ToListAsync(cancellationToken);

        var ownerId = scope == EmailNotificationProfileScope.Organization ? organizationId : tenantId;
        var testEvents = await _context.AuditEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(entry => entry.TenantId == tenantId.Value
                && !entry.IsDeleted
                && entry.EntityType == "EmailNotificationProfile"
                && entry.EntityId == ownerId
                && (entry.Action.StartsWith("ConnectionTest") || entry.Action.StartsWith("TestEmail")))
            .OrderByDescending(entry => entry.Timestamp)
            .Take(limit)
            .Select(entry => new EmailNotificationDeliveryLogDto(
                entry.Id,
                entry.Timestamp,
                entry.Action.StartsWith("ConnectionTest") ? "ConnectionTest" : "TestEmail",
                TestStatusName(entry.Action),
                0,
                entry.Timestamp,
                entry.Action.EndsWith("Failed") ? "TestFailed" : null,
                scope == EmailNotificationProfileScope.Organization ? organizationId : null,
                true))
            .ToListAsync(cancellationToken);

        return deliveries.Concat(testEvents)
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Take(limit)
            .ToArray();
    }

    private IQueryable<EmailNotificationProfileEntity> ActiveProfiles() =>
        _context.EmailNotificationProfiles.Where(profile => profile.DeletedAt == null);

    private async Task<bool> OrganizationBelongsToTenantAsync(Guid tenantId, Guid organizationId, CancellationToken cancellationToken) =>
        await _context.Organizations.IgnoreQueryFilters().AnyAsync(organization =>
            organization.Id == organizationId && organization.TenantId == tenantId
            && organization.IsActive && !organization.IsDeleted,
            cancellationToken);

    private EmailNotificationProfileRecord ToRecord(EmailNotificationProfileEntity row)
    {
        if (!Enum.TryParse<EmailNotificationProfileScope>(row.Scope, ignoreCase: false, out var scope)
            || !Enum.IsDefined(scope)
            || !Enum.TryParse<SmtpSecurityMode>(row.SecurityMode, ignoreCase: false, out var securityMode)
            || !Enum.IsDefined(securityMode)
            || !Enum.TryParse<SmtpAuthenticationMode>(row.AuthenticationMode, ignoreCase: false, out var authenticationMode)
            || !Enum.IsDefined(authenticationMode))
            throw new InvalidOperationException("The stored SMTP profile contains an unsupported setting.");

        string? password = null;
        if (!string.IsNullOrEmpty(row.ProtectedPassword)
            && !_secrets.TryUnprotect(row.ProtectedPassword, out password))
            throw new InvalidOperationException("The stored SMTP credential cannot be decrypted. Re-enter the credential after verifying the application key ring.");

        var profile = new SmtpTransportProfile(
            row.Enabled,
            row.Host,
            row.Port,
            securityMode,
            authenticationMode,
            row.UserName,
            password,
            row.FromAddress,
            row.FromName,
            row.ReplyToAddress);
        return new EmailNotificationProfileRecord(scope, row.TenantId, row.OrganizationId, EncodeVersion(row.RowVersion), profile);
    }

    private void ApplyProfile(EmailNotificationProfileEntity row, SmtpTransportProfile profile)
    {
        row.Enabled = profile.Enabled;
        row.Host = profile.Host.Trim().TrimEnd('.');
        row.Port = profile.Port;
        row.SecurityMode = profile.SecurityMode.ToString();
        row.AuthenticationMode = profile.AuthenticationMode.ToString();
        row.UserName = profile.AuthenticationMode == SmtpAuthenticationMode.UsernamePassword
            ? Normalize(profile.UserName)
            : null;
        row.ProtectedPassword = profile.AuthenticationMode == SmtpAuthenticationMode.UsernamePassword
            ? _secrets.Protect(NormalizeSecret(profile.Password))
            : null;
        row.FromAddress = profile.FromAddress.Trim();
        row.FromName = profile.FromName.Trim();
        row.ReplyToAddress = Normalize(profile.ReplyToAddress);
    }

    private async Task AppendProfileAuditAsync(
        AuditActor actor,
        EmailNotificationProfileEntity row,
        ProfileAuditSnapshot? previous,
        string action,
        CancellationToken cancellationToken)
    {
        var profileOwner = row.OrganizationId ?? row.TenantId;
        var fields = new Dictionary<string, object?>
        {
            ["Scope"] = row.Scope,
            ["OwnerId"] = profileOwner,
            ["Enabled"] = row.Enabled,
            ["HostConfigured"] = !string.IsNullOrWhiteSpace(row.Host),
            ["Port"] = row.Port,
            ["SecurityMode"] = row.SecurityMode,
            ["AuthenticationMode"] = row.AuthenticationMode,
            ["CredentialConfigured"] = !string.IsNullOrWhiteSpace(row.ProtectedPassword),
            ["SenderConfigured"] = !string.IsNullOrWhiteSpace(row.FromAddress),
            ["ReplyToConfigured"] = !string.IsNullOrWhiteSpace(row.ReplyToAddress)
        };
        var changes = previous is null
            ? JsonSerializer.Serialize(fields.ToDictionary(pair => pair.Key, pair => new { from = (object?)null, to = pair.Value }))
            : JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["Enabled"] = new { from = previous.Enabled, to = row.Enabled },
                ["Port"] = new { from = previous.Port, to = row.Port },
                ["SecurityMode"] = new { from = previous.SecurityMode, to = row.SecurityMode },
                ["AuthenticationMode"] = new { from = previous.AuthenticationMode, to = row.AuthenticationMode },
                ["HostConfigured"] = new { from = !string.IsNullOrWhiteSpace(previous.Host), to = !string.IsNullOrWhiteSpace(row.Host) },
                ["CredentialConfigured"] = new { from = !string.IsNullOrWhiteSpace(previous.ProtectedPassword), to = !string.IsNullOrWhiteSpace(row.ProtectedPassword) },
                ["SenderConfigured"] = new { from = !string.IsNullOrWhiteSpace(previous.FromAddress), to = !string.IsNullOrWhiteSpace(row.FromAddress) },
                ["ReplyToConfigured"] = new { from = !string.IsNullOrWhiteSpace(previous.ReplyToAddress), to = !string.IsNullOrWhiteSpace(row.ReplyToAddress) }
            });

        var entry = new AuditEntry
        {
            TenantId = actor.TenantId,
            EntityType = "EmailNotificationProfile",
            EntityId = row.Id,
            Action = action,
            Changes = changes,
            ChangedFields = string.Join(',', fields.Keys),
            Description = $"Email notification SMTP profile {action.ToLowerInvariant()} at {row.Scope} scope.",
            Source = "User",
            UserId = actor.UserId,
            UserName = actor.UserName,
            CreatedBy = actor.UserId.ToString()
        };
        var result = await _audit.AppendAsync(entry, cancellationToken);
        if (result.IsFailure)
            throw new InvalidOperationException("The SMTP profile change could not be recorded in the audit trail.");
    }

    private AuditActor GetAuditActor()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            throw new InvalidOperationException("An authenticated actor is required to change SMTP settings.");

        var actorText = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(actorText, out var actorId) || actorId == Guid.Empty
            || !Guid.TryParse(principal.FindFirstValue("tenantId"), out var tenantId) || tenantId == Guid.Empty)
            throw new InvalidOperationException("A trusted actor and tenant are required to change SMTP settings.");

        return new AuditActor(actorId, tenantId, principal.FindFirstValue(ClaimTypes.Name) ?? principal.Identity?.Name);
    }

    private static Guid? ValidateScopeOwner(EmailNotificationProfileScope scope, Guid? tenantId, Guid? organizationId)
    {
        if (!Enum.IsDefined(scope))
            throw new ArgumentOutOfRangeException(nameof(scope));

        return scope switch
        {
            EmailNotificationProfileScope.Platform when tenantId is null && organizationId is null => null,
            EmailNotificationProfileScope.Tenant when tenantId is Guid tenant && tenant != Guid.Empty && organizationId is null => tenant,
            EmailNotificationProfileScope.Organization when tenantId is Guid tenant && tenant != Guid.Empty
                && organizationId is Guid organization && organization != Guid.Empty => organization,
            _ => throw new ArgumentException("The SMTP profile scope and owner identifiers do not match.")
        };
    }

    private static void ValidatePersistableProfile(SmtpTransportProfile profile)
    {
        if (profile.AuthenticationMode == SmtpAuthenticationMode.OAuth2)
            throw new InvalidOperationException("OAuth2/XOAUTH2 is reserved and unavailable in this release.");
        if (profile.AuthenticationMode == SmtpAuthenticationMode.TrustedRelay
            && (!string.IsNullOrWhiteSpace(profile.UserName) || !string.IsNullOrWhiteSpace(profile.Password)))
            throw new InvalidOperationException("Trusted relay profiles cannot store SMTP credentials.");
        if (profile.AuthenticationMode == SmtpAuthenticationMode.UsernamePassword
            && profile.Enabled
            && (string.IsNullOrWhiteSpace(profile.UserName) || string.IsNullOrWhiteSpace(profile.Password)))
            throw new InvalidOperationException("Enabled username/password profiles require a saved SMTP credential.");
    }

    private static void EnsureExpectedVersion(string? currentVersion, string? expectedVersion)
    {
        if (currentVersion is null)
        {
            if (!string.IsNullOrWhiteSpace(expectedVersion))
                throw new EmailNotificationProfileConcurrencyException();
            return;
        }

        if (string.IsNullOrWhiteSpace(expectedVersion)
            || !string.Equals(currentVersion, expectedVersion, StringComparison.Ordinal))
            throw new EmailNotificationProfileConcurrencyException();
    }

    private static string ScopeName(EmailNotificationProfileScope scope) => scope.ToString();
    private static string EncodeVersion(byte[] version) => Convert.ToBase64String(version);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeSecret(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string DeliveryStatusName(NotificationDeliveryStatus status) => status switch
    {
        NotificationDeliveryStatus.Pending => "Pending",
        NotificationDeliveryStatus.Accepted => "Accepted",
        NotificationDeliveryStatus.RetryScheduled => "Retrying",
        NotificationDeliveryStatus.FailedPermanent => "Failed",
        NotificationDeliveryStatus.Cancelled => "Cancelled",
        NotificationDeliveryStatus.Processing => "Pending",
        _ => "Unavailable"
    };

    private static string TestStatusName(string action)
    {
        if (action.EndsWith("Attempted", StringComparison.Ordinal)) return "Pending";
        if (action.StartsWith("ConnectionTest", StringComparison.Ordinal))
            return action.EndsWith("Accepted", StringComparison.Ordinal) ? "Succeeded"
                : action.EndsWith("Failed", StringComparison.Ordinal) ? "Failed" : "Unavailable";
        return action.EndsWith("Accepted", StringComparison.Ordinal) ? "Accepted"
            : action.EndsWith("Failed", StringComparison.Ordinal) ? "Failed" : "Unavailable";
    }

    private static ProfileAuditSnapshot Snapshot(EmailNotificationProfileEntity row) => new(
        row.Enabled,
        row.Host,
        row.Port,
        row.SecurityMode,
        row.AuthenticationMode,
        row.ProtectedPassword,
        row.FromAddress,
        row.ReplyToAddress);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException sqlException
        && sqlException.Number is 2601 or 2627;

    private sealed record AuditActor(Guid UserId, Guid TenantId, string? UserName);
    private sealed record ProfileAuditSnapshot(
        bool Enabled,
        string Host,
        int Port,
        string SecurityMode,
        string AuthenticationMode,
        string? ProtectedPassword,
        string FromAddress,
        string? ReplyToAddress);
}
