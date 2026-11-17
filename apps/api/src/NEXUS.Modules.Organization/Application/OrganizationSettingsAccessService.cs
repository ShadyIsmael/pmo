using System.Data.Common;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Organization.Domain;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Modules.Organization.Application;

public sealed class OrganizationSettingsAccessService : IOrganizationSettingsAccessService
{
    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditService _auditService;
    private readonly IUserEffectivePermissionService _effectivePermissions;

    public OrganizationSettingsAccessService(
        DbContext context,
        IHttpContextAccessor httpContextAccessor,
        IAuditService auditService,
        IUserEffectivePermissionService effectivePermissions)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _auditService = auditService;
        _effectivePermissions = effectivePermissions;
    }

    public async Task<IReadOnlyList<Guid>> GetReadableOrganizationIdsAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var tenantId, out var userId)) return Array.Empty<Guid>();
        try
        {
            if (await IsProtectedTenantAdminAsync(tenantId, userId, cancellationToken))
            {
                return await _context.Set<OrganizationEntity>()
                    .IgnoreQueryFilters()
                    .Where(organization => organization.TenantId == tenantId && !organization.IsDeleted && organization.IsActive)
                    .OrderBy(organization => organization.Id)
                    .Select(organization => organization.Id)
                    .ToListAsync(cancellationToken);
            }

            var effectivePermissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);
            var canViewSettings = effectivePermissions.Contains("Settings.View");
            var canViewOrganizationConfiguration = effectivePermissions.Contains("OrganizationConfiguration.View");
            if (!canViewSettings && !canViewOrganizationConfiguration) return Array.Empty<Guid>();

            var grantedOrganizationIds = await _context.Set<OrganizationSettingsGrant>()
                .IgnoreQueryFilters()
                .Where(grant => grant.TenantId == tenantId && grant.UserId == userId && !grant.IsDeleted
                    && (grant.CanRead || grant.CanManage))
                .Join(_context.Set<OrganizationEntity>().IgnoreQueryFilters().Where(organization =>
                        organization.TenantId == tenantId && !organization.IsDeleted && organization.IsActive),
                    grant => grant.OrganizationId,
                    organization => organization.Id,
                    (_, organization) => organization.Id)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (canViewSettings || !canViewOrganizationConfiguration) return grantedOrganizationIds;

            var authorizedOrganizationIds = new List<Guid>();
            foreach (var organizationId in grantedOrganizationIds)
            {
                if (await HasScopedOrganizationAdminAuthorityAsync(organizationId, manage: false, cancellationToken))
                    authorizedOrganizationIds.Add(organizationId);
            }
            return authorizedOrganizationIds;
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new SettingsPersistenceUnavailableException("Organization settings access storage is unavailable or its schema has not been deployed.", exception);
        }
    }

    public async Task<IReadOnlyList<Guid>> GetManageableOrganizationIdsAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var tenantId, out var userId)) return Array.Empty<Guid>();
        try
        {
            if (await IsProtectedTenantAdminAsync(tenantId, userId, cancellationToken))
            {
                return await _context.Set<OrganizationEntity>()
                    .IgnoreQueryFilters()
                    .Where(organization => organization.TenantId == tenantId && !organization.IsDeleted && organization.IsActive)
                    .OrderBy(organization => organization.Id)
                    .Select(organization => organization.Id)
                    .ToListAsync(cancellationToken);
            }

            var effectivePermissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);
            var canManageTenantConfiguration = effectivePermissions.Contains("Settings.ManageTenantConfiguration");
            var canManageOrganizationConfiguration = effectivePermissions.Contains("OrganizationConfiguration.Manage");
            if (!canManageTenantConfiguration && !canManageOrganizationConfiguration) return Array.Empty<Guid>();

            var grantedOrganizationIds = await _context.Set<OrganizationSettingsGrant>()
                .IgnoreQueryFilters()
                .Where(grant => grant.TenantId == tenantId && grant.UserId == userId && !grant.IsDeleted && grant.CanManage)
                .Join(_context.Set<OrganizationEntity>().IgnoreQueryFilters().Where(organization =>
                        organization.TenantId == tenantId && !organization.IsDeleted && organization.IsActive),
                    grant => grant.OrganizationId,
                    organization => organization.Id,
                    (_, organization) => organization.Id)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (canManageTenantConfiguration || !canManageOrganizationConfiguration) return grantedOrganizationIds;

            var authorizedOrganizationIds = new List<Guid>();
            foreach (var organizationId in grantedOrganizationIds)
            {
                if (await HasScopedOrganizationAdminAuthorityAsync(organizationId, manage: true, cancellationToken))
                    authorizedOrganizationIds.Add(organizationId);
            }
            return authorizedOrganizationIds;
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new SettingsPersistenceUnavailableException("Organization settings access storage is unavailable or its schema has not been deployed.", exception);
        }
    }

    public async Task<bool> HasAccessAsync(Guid organizationId, bool manage, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || !TryGetActor(out var tenantId, out var userId)) return false;
        try
        {
            var organizationExists = await _context.Set<OrganizationEntity>()
                .IgnoreQueryFilters()
                .AnyAsync(organization => organization.Id == organizationId && organization.TenantId == tenantId
                    && !organization.IsDeleted && organization.IsActive, cancellationToken);
            if (!organizationExists) return false;

            if (await IsProtectedTenantAdminAsync(tenantId, userId, cancellationToken)) return true;

            var effectivePermissions = await _effectivePermissions.GetEffectivePermissionsAsync(
                userId, tenantId, cancellationToken);
            var hasTenantSettingsAuthority = manage
                ? effectivePermissions.Contains("Settings.ManageTenantConfiguration")
                : effectivePermissions.Contains("Settings.View");
            if (hasTenantSettingsAuthority
                && await _context.Set<OrganizationSettingsGrant>()
                    .IgnoreQueryFilters()
                    .AnyAsync(grant => grant.TenantId == tenantId && grant.OrganizationId == organizationId
                        && grant.UserId == userId && !grant.IsDeleted
                        && (manage ? grant.CanManage : grant.CanRead || grant.CanManage), cancellationToken))
                return true;

            var hasOrganizationConfigurationAuthority = effectivePermissions.Contains(
                manage ? "OrganizationConfiguration.Manage" : "OrganizationConfiguration.View");
            return hasOrganizationConfigurationAuthority
                && await HasScopedOrganizationAdminAuthorityAsync(organizationId, manage, cancellationToken);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new SettingsPersistenceUnavailableException("Organization settings access storage is unavailable or its schema has not been deployed.", exception);
        }
    }

    /// <summary>
    /// Organization Admin is a tenant role identity whose configuration reach is
    /// constrained by an explicit per-user/per-Organization settings grant. The
    /// role itself never supplies an Organization scope. Appointment mutations
    /// remain governed by the separate role-assignment policy.
    /// </summary>
    public async Task<bool> HasScopedOrganizationAdminAuthorityAsync(
        Guid organizationId,
        bool manage,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || !TryGetActor(out var tenantId, out var userId)) return false;
        try
        {
            var organizationExists = await _context.Set<OrganizationEntity>()
                .IgnoreQueryFilters()
                .AnyAsync(organization => organization.Id == organizationId && organization.TenantId == tenantId
                    && !organization.IsDeleted && organization.IsActive, cancellationToken);
            if (!organizationExists) return false;

            var hasOrganizationAdminRole = await (
                    from assignment in _context.Set<UserRole>().IgnoreQueryFilters().AsNoTracking()
                    where assignment.UserId == userId && assignment.TenantId == tenantId && !assignment.IsDeleted
                          && assignment.User.TenantId == tenantId && !assignment.User.IsDeleted && assignment.User.IsActive
                          && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                          && assignment.Role.IsSystem
                          && assignment.Role.SystemRoleKey == Role.OrganizationAdminRoleKey
                          && assignment.Role.RoleKey == Role.OrganizationAdminRoleKey
                    select assignment.RoleId)
                .AnyAsync(cancellationToken);
            if (!hasOrganizationAdminRole) return false;

            // Re-read effective authorization from persistence for each sensitive
            // settings operation. Effective permission evaluation applies Active
            // Deny and catalog dependency rules, so a stale token or grant cannot
            // preserve Organization Configuration authority after revocation.
            var effectivePermissions = await _effectivePermissions.GetEffectivePermissionsAsync(
                userId, tenantId, cancellationToken);
            if (!effectivePermissions.Contains("OrganizationConfiguration.View")) return false;
            if (manage && !effectivePermissions.Contains("OrganizationConfiguration.Manage")) return false;

            return await _context.Set<OrganizationSettingsGrant>()
                .IgnoreQueryFilters()
                .AnyAsync(grant => grant.TenantId == tenantId && grant.OrganizationId == organizationId
                    && grant.UserId == userId && !grant.IsDeleted
                    && (manage ? grant.CanManage : grant.CanRead || grant.CanManage), cancellationToken);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new SettingsPersistenceUnavailableException("Organization settings access storage is unavailable or its schema has not been deployed.", exception);
        }
    }

    public async Task<IReadOnlyList<OrganizationSettingsGrantDto>> GetGrantsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var tenantId, out _)) return Array.Empty<OrganizationSettingsGrantDto>();
        try
        {
            var organizationExists = await _context.Set<OrganizationEntity>()
                .IgnoreQueryFilters()
                .AnyAsync(organization => organization.Id == organizationId && organization.TenantId == tenantId
                    && !organization.IsDeleted, cancellationToken);
            if (!organizationExists) return Array.Empty<OrganizationSettingsGrantDto>();

            var grants = await _context.Set<OrganizationSettingsGrant>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(grant => grant.TenantId == tenantId && grant.OrganizationId == organizationId && !grant.IsDeleted)
                .Join(_context.Set<User>().IgnoreQueryFilters().Where(user => user.TenantId == tenantId && !user.IsDeleted),
                    grant => grant.UserId,
                    user => user.Id,
                    (grant, user) => new
                    {
                        grant.Id,
                        grant.OrganizationId,
                        grant.UserId,
                        user.FirstName,
                        user.LastName,
                        user.Email,
                        grant.CanRead,
                        grant.CanManage,
                        grant.RowVersion
                    })
                .OrderBy(grant => grant.FirstName)
                .ThenBy(grant => grant.LastName)
                .ToListAsync(cancellationToken);
            return grants.Select(grant => new OrganizationSettingsGrantDto(
                grant.Id, grant.OrganizationId, grant.UserId, $"{grant.FirstName} {grant.LastName}", grant.Email,
                grant.CanRead, grant.CanManage, Convert.ToBase64String(grant.RowVersion))).ToArray();
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new SettingsPersistenceUnavailableException("Organization settings access storage is unavailable or its schema has not been deployed.", exception);
        }
    }

    public async Task<IReadOnlyList<OrganizationSettingsGrantTargetUserDto>> GetGrantTargetsAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var tenantId, out _)) return Array.Empty<OrganizationSettingsGrantTargetUserDto>();
        try
        {
            return await _context.Set<User>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => user.TenantId == tenantId && !user.IsDeleted && user.IsActive)
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .Select(user => new OrganizationSettingsGrantTargetUserDto(
                    user.Id, user.FirstName + " " + user.LastName, user.Email))
                .ToListAsync(cancellationToken);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new SettingsPersistenceUnavailableException("Organization settings access storage is unavailable or its schema has not been deployed.", exception);
        }
    }

    public async Task<OrganizationSettingsGrantOperationResult> SaveGrantAsync(
        Guid organizationId,
        UpdateOrganizationSettingsGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var tenantId, out var actorId))
            return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.TenantContextMissing);
        if (organizationId == Guid.Empty || request.UserId == Guid.Empty)
            return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.ValidationFailed,
                Error: "A valid organization and user are required.");
        if (!TryParseVersion(request.ExpectedVersion, out var expectedVersion))
            return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.ValidationFailed,
                Error: "Expected version must be an 8-byte base64 rowversion token.");

        try
        {
            var organizationExists = await _context.Set<OrganizationEntity>().IgnoreQueryFilters()
                .AnyAsync(organization => organization.Id == organizationId && organization.TenantId == tenantId
                    && !organization.IsDeleted && organization.IsActive, cancellationToken);
            var targetUser = await _context.Set<User>().IgnoreQueryFilters().AsNoTracking()
                .Where(user => user.Id == request.UserId && user.TenantId == tenantId
                    && !user.IsDeleted && user.IsActive)
                .Select(user => new { user.FirstName, user.LastName, user.Email })
                .SingleOrDefaultAsync(cancellationToken);
            if (!organizationExists || targetUser is null)
                return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.NotFound);

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var grant = await _context.Set<OrganizationSettingsGrant>().IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.OrganizationId == organizationId
                    && item.UserId == request.UserId && !item.IsDeleted, cancellationToken);
            var previousRead = grant?.CanRead ?? false;
            var previousManage = grant?.CanManage ?? false;

            if (grant is null)
            {
                if (expectedVersion is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.ConcurrencyConflict);
                }
                grant = new OrganizationSettingsGrant
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    OrganizationId = organizationId,
                    UserId = request.UserId,
                    CanRead = request.CanRead || request.CanManage,
                    CanManage = request.CanManage,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actorId.ToString()
                };
                _context.Set<OrganizationSettingsGrant>().Add(grant);
            }
            else
            {
                if (expectedVersion is null || !expectedVersion.AsSpan().SequenceEqual(grant.RowVersion))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.ConcurrencyConflict);
                }
                grant.CanRead = request.CanRead || request.CanManage;
                grant.CanManage = request.CanManage;
                grant.UpdatedAt = DateTime.UtcNow;
                grant.UpdatedBy = actorId.ToString();
            }

            await _context.SaveChangesAsync(cancellationToken);
            var auditResult = await _auditService.AppendAsync(CreateAudit(
                tenantId, actorId, organizationId, request.UserId, "OrganizationSettingsAccess", "Updated",
                new { canRead = previousRead, canManage = previousManage },
                new { canRead = grant.CanRead, canManage = grant.CanManage } ), cancellationToken);
            if (auditResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.PersistenceUnavailable,
                    Error: auditResult.Error?.Message ?? "The audit event could not be recorded.");
            }
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var savedGrant = new OrganizationSettingsGrantDto(grant.Id, grant.OrganizationId, grant.UserId,
                $"{targetUser.FirstName} {targetUser.LastName}", targetUser.Email, grant.CanRead, grant.CanManage,
                Convert.ToBase64String(grant.RowVersion));
            return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.Saved, savedGrant);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // Another request created this active organization/user grant first.
            return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.ConcurrencyConflict);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new SettingsPersistenceUnavailableException("Organization settings access storage is unavailable or its schema has not been deployed.", exception);
        }
    }

    public async Task<OrganizationSettingsGrantOperationResult> RevokeGrantAsync(
        Guid organizationId,
        Guid userId,
        RevokeOrganizationSettingsGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var tenantId, out var actorId))
            return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.TenantContextMissing);
        if (!TryParseVersion(request.ExpectedVersion, out var expectedVersion) || expectedVersion is null)
            return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.ValidationFailed,
                Error: "A current 8-byte base64 rowversion token is required.");

        try
        {
            var grant = await _context.Set<OrganizationSettingsGrant>().IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.OrganizationId == organizationId
                    && item.UserId == userId && !item.IsDeleted, cancellationToken);
            if (grant is null) return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.NotFound);
            if (!expectedVersion.AsSpan().SequenceEqual(grant.RowVersion))
                return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.ConcurrencyConflict);

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var oldRead = grant.CanRead;
            var oldManage = grant.CanManage;
            grant.IsDeleted = true;
            grant.UpdatedAt = DateTime.UtcNow;
            grant.UpdatedBy = actorId.ToString();
            await _context.SaveChangesAsync(cancellationToken);
            var auditResult = await _auditService.AppendAsync(CreateAudit(
                tenantId, actorId, organizationId, userId, "OrganizationSettingsAccess", "Revoked",
                new { canRead = oldRead, canManage = oldManage }, null), cancellationToken);
            if (auditResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.PersistenceUnavailable,
                    Error: auditResult.Error?.Message ?? "The audit event could not be recorded.");
            }
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.Revoked);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new OrganizationSettingsGrantOperationResult(OrganizationSettingsGrantOperationCode.ConcurrencyConflict);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new SettingsPersistenceUnavailableException("Organization settings access storage is unavailable or its schema has not been deployed.", exception);
        }
    }

    private AuditEntry CreateAudit(Guid tenantId, Guid actorId, Guid organizationId, Guid targetUserId,
        string entityType, string action, object? oldValue, object? newValue) => new()
    {
        TenantId = tenantId,
        EntityType = entityType,
        EntityId = organizationId,
        Action = action,
        ChangedFields = "CanRead,CanManage",
        Description = $"Organization settings access for user {targetUserId} was {action.ToLowerInvariant()}.",
        OriginalValues = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
        NewValues = newValue is null ? null : JsonSerializer.Serialize(newValue),
        Changes = JsonSerializer.Serialize(new { organizationId, userId = targetUserId, from = oldValue, to = newValue }),
        Source = AuditSources.User,
        CorrelationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? _httpContextAccessor.HttpContext?.TraceIdentifier,
        UserId = actorId,
        CreatedBy = actorId.ToString()
    };

    private bool TryGetActor(out Guid tenantId, out Guid userId)
    {
        tenantId = Guid.Empty;
        userId = Guid.Empty;
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirstValue("tenantId"), out tenantId)
            || tenantId == Guid.Empty) return false;
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
            && !Guid.TryParse(principal.FindFirstValue("sub"), out userId)) return false;
        return userId != Guid.Empty;
    }

    private async Task<bool> IsProtectedTenantAdminAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) => await (
            from assignment in _context.Set<UserRole>().IgnoreQueryFilters().AsNoTracking()
            where assignment.UserId == userId && assignment.TenantId == tenantId && !assignment.IsDeleted
                  && assignment.User.TenantId == tenantId && !assignment.User.IsDeleted && assignment.User.IsActive
                  && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                  && assignment.Role.IsSystem
                  && assignment.Role.SystemRoleKey == Role.TenantAdminRoleKey
                  && assignment.Role.RoleKey == Role.TenantAdminRoleKey
            select assignment.RoleId)
        .AnyAsync(cancellationToken);

    private static bool TryParseVersion(string? value, out byte[]? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        try
        {
            version = Convert.FromBase64String(value);
            return version.Length == 8;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException or DbUpdateException
        || exception.InnerException is not null && IsDatabaseFailure(exception.InnerException);

    private static bool IsUniqueConstraintViolation(Exception exception) =>
        exception is DbException databaseException
        && databaseException.GetType().GetProperty("Number", BindingFlags.Public | BindingFlags.Instance)?.GetValue(databaseException) is int number
        && number is 2601 or 2627
        || exception.InnerException is not null && IsUniqueConstraintViolation(exception.InnerException);
}
