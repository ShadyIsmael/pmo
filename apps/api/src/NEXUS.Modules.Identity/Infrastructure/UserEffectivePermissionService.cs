using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class UserEffectivePermissionService : IUserEffectivePermissionService
{
    private readonly DbContext _context;
    private readonly IUserOrganizationSettingsPermissionLookup? _organizationSettingsPermissions;

    public UserEffectivePermissionService(
        DbContext context,
        ILogger<UserEffectivePermissionService>? logger = null,
        IUserOrganizationSettingsPermissionLookup? organizationSettingsPermissions = null)
    {
        _context = context;
        _ = logger;
        _organizationSettingsPermissions = organizationSettingsPermissions;
    }

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        => await GetEffectivePermissionsCoreAsync(userId, tenantId, organizationId: null, cancellationToken);

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsForOrganizationAsync(
        Guid userId,
        Guid tenantId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
            return new HashSet<string>(StringComparer.Ordinal);
        return await GetEffectivePermissionsCoreAsync(userId, tenantId, organizationId, cancellationToken);
    }

    private async Task<IReadOnlySet<string>> GetEffectivePermissionsCoreAsync(
        Guid userId,
        Guid tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
            return new HashSet<string>(StringComparer.Ordinal);

        var activeUserExists = await _context.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.TenantId == tenantId && !user.IsDeleted && user.IsActive, cancellationToken);

        if (!activeUserExists)
            return new HashSet<string>(StringComparer.Ordinal);

        // Login and refresh can run before a tenant claim is established, so this query
        // deliberately bypasses ambient filters and repeats every tenant/deletion check.
        var assignedGrants = await (
                from assignment in _context.Set<UserRole>().IgnoreQueryFilters().AsNoTracking()
                where assignment.UserId == userId && assignment.TenantId == tenantId && !assignment.IsDeleted
                      && assignment.User.TenantId == tenantId && !assignment.User.IsDeleted && assignment.User.IsActive
                      && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                from permission in assignment.Role.Permissions
                where permission.TenantId == tenantId && !permission.IsDeleted
                select new
                {
                    Key = permission.Resource + "." + permission.Action,
                    assignment.Role.OrganizationId,
                    assignment.Role.IsSystem,
                    assignment.Role.RoleKey,
                    assignment.Role.SystemRoleKey,
                    assignment.Role.IsLegacyOrganizationOwnershipUnresolved
                })
            .ToListAsync(cancellationToken);

        var hasOrganizationAdminSystemRole = organizationId.HasValue && assignedGrants.Any(grant =>
            grant.IsSystem
            && grant.RoleKey == Role.OrganizationAdminRoleKey
            && grant.SystemRoleKey == Role.OrganizationAdminSystemRoleKey
            && grant.OrganizationId is null);
        var organizationAdminGrant = hasOrganizationAdminSystemRole && _organizationSettingsPermissions is not null
            ? await _organizationSettingsPermissions.GetAsync(tenantId, userId, organizationId!.Value, cancellationToken)
            : null;

        var inherited = assignedGrants
            .Where(grant => IsRolePermissionEffective(grant.Key, grant.OrganizationId,
                grant.IsSystem, grant.RoleKey, grant.SystemRoleKey,
                grant.IsLegacyOrganizationOwnershipUnresolved, organizationId,
                organizationAdminGrant?.CanRead == true, organizationAdminGrant?.CanManage == true))
            .Select(grant => grant.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var overrides = await _context.Set<UserPermissionOverride>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.TenantId == tenantId && !item.IsDeleted
                           && item.Permission.TenantId == tenantId && !item.Permission.IsDeleted)
            .Select(item => new
            {
                Permission = item.Permission.Resource + "." + item.Permission.Action,
                item.Effect
            })
            .ToListAsync(cancellationToken);

        var effective = EffectivePermissionResolver.Resolve(
            inherited,
            overrides
                .Where(item => IsOverrideEffective(item.Permission, item.Effect))
                .Select(item => (item.Permission, item.Effect)));

        // Existing persisted roles are not rewritten when the catalog adds View
        // dependencies. Their invalid Action grants simply do not become effective
        // until an administrator explicitly configures the matching View capability.
        return PermissionCatalog.EnforceDependencies(effective);
    }

    private static bool IsRolePermissionEffective(
        string key,
        Guid? roleOrganizationId,
        bool isSystem,
        string? roleKey,
        string? systemRoleKey,
        bool isLegacyOrganizationOwnershipUnresolved,
        Guid? requestedOrganizationId,
        bool organizationAdminCanRead,
        bool organizationAdminCanManage)
    {
        if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition) || !definition.IsActive)
            return false;

        // Reserved scopes stay outside this authorization model for every role.
        if (definition.Scope is PermissionScope.Platform
            or PermissionScope.CrossTenant
            or PermissionScope.SystemOwner)
            return false;

        var isProtectedOrganizationAdmin = isSystem
            && roleOrganizationId is null
            && roleKey == Role.OrganizationAdminRoleKey
            && systemRoleKey == Role.OrganizationAdminSystemRoleKey;
        if (isProtectedOrganizationAdmin)
        {
            if (!requestedOrganizationId.HasValue)
                return false;
            return key switch
            {
                "OrganizationConfiguration.View" => organizationAdminCanRead || organizationAdminCanManage,
                "OrganizationConfiguration.Manage" => organizationAdminCanManage,
                _ => false
            };
        }

        // An unresolved legacy Role is retained for reconciliation only and grants
        // no effective access until its ownership state is resolved.
        if (isLegacyOrganizationOwnershipUnresolved)
            return false;

        // A null OrganizationId on an ordinary Role is its explicit Tenant-owned
        // scope. Such grants remain inside the authenticated Tenant and all native
        // resource authorization checks still apply.
        if (roleOrganizationId is null)
            return true;

        // Organization-owned grants are active only while the request is
        // explicitly resolved to the Role's immutable owning Organization.
        return requestedOrganizationId == roleOrganizationId;
    }

    private static bool IsOverrideEffective(string key, PermissionOverrideEffect effect)
    {
        if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition) || !definition.IsActive)
            return false;

        // User overrides have no Organization key. Tenant Allows remain supported;
        // a Deny remains globally restrictive so it can never widen access.
        return definition.Scope == PermissionScope.Tenant || effect == PermissionOverrideEffect.Deny;
    }

    public async Task<IReadOnlySet<string>> GetEffectiveRoleNamesAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
            return new HashSet<string>(StringComparer.Ordinal);

        var roles = await (
                from assignment in _context.Set<UserRole>().IgnoreQueryFilters().AsNoTracking()
                where assignment.UserId == userId && assignment.TenantId == tenantId && !assignment.IsDeleted
                      && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                      && assignment.User.TenantId == tenantId && !assignment.User.IsDeleted && assignment.User.IsActive
                      // Role-name claims are global to the request and have no Organization
                      // qualifier. Only the canonical Tenant Admin identity may be exposed
                      // through ClaimTypes.Role; Organization Roles and unresolved legacy
                      // Roles must never satisfy an unscoped workflow RequireRole guard.
                      && assignment.Role.IsSystem
                      && assignment.Role.OrganizationId == null
                      && !assignment.Role.IsLegacyOrganizationOwnershipUnresolved
                      && assignment.Role.RoleKey == Role.TenantAdminRoleKey
                      && assignment.Role.SystemRoleKey == Role.TenantAdminRoleKey
                select assignment.Role.Name)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new HashSet<string>(roles, StringComparer.Ordinal);
    }
}
