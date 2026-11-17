using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class RoleAssignmentAuthorizationService : IRoleAssignmentAuthorizationService
{
    private readonly DbContext _context;
    private readonly IUserEffectivePermissionService _effectivePermissions;
    private readonly IUserOrganizationDirectory? _organizationDirectory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPermissionProtectionPolicy _permissionProtection;

    public RoleAssignmentAuthorizationService(
        DbContext context,
        IUserEffectivePermissionService effectivePermissions,
        IHttpContextAccessor httpContextAccessor,
        IUserOrganizationDirectory? organizationDirectory = null,
        IPermissionProtectionPolicy? permissionProtection = null)
    {
        _context = context;
        _effectivePermissions = effectivePermissions;
        _httpContextAccessor = httpContextAccessor;
        _organizationDirectory = organizationDirectory;
        _permissionProtection = permissionProtection ?? PermissionProtectionPolicy.Empty;
    }

    public async Task<Result<IReadOnlyList<Role>>> ResolveAndAuthorizeAsync(
        IReadOnlyCollection<Guid> roleIds,
        Guid actorUserId,
        Guid tenantId,
        Guid? targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrentActorAndTenant(actorUserId, tenantId))
            return Result<IReadOnlyList<Role>>.Failure(Error.Forbidden("A verified Tenant actor is required."));

        if (roleIds.Any(id => id == Guid.Empty) || roleIds.Distinct().Count() != roleIds.Count)
            return Result<IReadOnlyList<Role>>.Failure(new Error(
                RoleAssignmentContract.InvalidRoleIds, "Role IDs must be valid and unique.", 400, "roleIds"));

        if (roleIds.Count == 0)
            return Result<IReadOnlyList<Role>>.Success(Array.Empty<Role>());

        var roles = await _context.Set<Role>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(role => roleIds.Contains(role.Id) && role.TenantId == tenantId && !role.IsDeleted)
            .Include(role => role.Permissions)
            .ToListAsync(cancellationToken);
        if (roles.Count != roleIds.Count)
            return Result<IReadOnlyList<Role>>.Failure(new Error(
                RoleAssignmentContract.OutOfScope, "One or more selected Roles are unavailable in this Tenant.", 403, "roleIds"));

        var organizationIds = roles
            .Where(role => role.OrganizationId.HasValue)
            .Select(role => role.OrganizationId!.Value)
            .Distinct()
            .ToArray();
        if (organizationIds.Length > 0 && _organizationDirectory is null)
            return Result<IReadOnlyList<Role>>.Failure(
                new Error("ORGANIZATION_LOOKUP_UNAVAILABLE", "Organization lookup is unavailable.", StatusCodes.Status503ServiceUnavailable));
        var activeOrganizations = organizationIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _organizationDirectory!.GetActiveNamesAsync(tenantId, organizationIds, cancellationToken);
        if (activeOrganizations.Count != organizationIds.Length)
            return Result<IReadOnlyList<Role>>.Failure(new Error(
                RoleAssignmentContract.OutOfScope, "A selected Role belongs to an inactive or unavailable Organization.", 403, "roleIds"));

        foreach (var organizationGroup in roles.GroupBy(role => role.OrganizationId))
        {
            IReadOnlySet<string> actorPermissions;
            if (organizationGroup.Key is Guid organizationId)
            {
                if (!activeOrganizations.ContainsKey(organizationId))
                    return Result<IReadOnlyList<Role>>.Failure(new Error(
                        RoleAssignmentContract.OutOfScope,
                        "A selected Role belongs to an inactive or unavailable Organization.",
                        403,
                        "roleIds"));
                actorPermissions = await _effectivePermissions.GetEffectivePermissionsForOrganizationAsync(
                    actorUserId, tenantId, organizationId, cancellationToken);
            }
            else
            {
                actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(
                    actorUserId, tenantId, cancellationToken);
            }
            if (!actorPermissions.Contains("Users.AssignRoles"))
                return Result<IReadOnlyList<Role>>.Failure(new Error(
                    RoleAssignmentContract.OutOfScope,
                    "Users.AssignRoles is not effective in the selected Role scope.",
                    403,
                    "roleIds"));

            var failure = RoleAssignmentAuthorizationPolicy.Evaluate(
                organizationGroup.ToArray(), actorUserId, tenantId, targetUserId,
                actorPermissions, _permissionProtection);
            if (failure is not null)
            {
                var reason = failure.ReasonCode;
                if (reason is "protected-admin-role-assignment" or "organization-admin-appointment-unapproved"
                    or "role-not-assignable" or "permission-protected")
                    return Result<IReadOnlyList<Role>>.Failure(new Error(
                        RoleAssignmentContract.ProtectedRole,
                        "The selected Role is protected or unavailable for ordinary assignment.",
                        403,
                        "roleIds"));
                if (reason is "permission-dependency-missing" or "permission-tenant-or-deleted")
                    return Result<IReadOnlyList<Role>>.Failure(new Error(
                        RoleAssignmentContract.OutOfScope,
                        "The selected Role's permission set is not valid for assignment.",
                        409,
                        "roleIds"));
                return Result<IReadOnlyList<Role>>.Failure(new Error(
                    RoleAssignmentContract.OutOfScope,
                    "You cannot assign one or more selected Roles within their Organization scope.",
                    403,
                    "roleIds"));
            }
        }

        return Result<IReadOnlyList<Role>>.Success(roles);
    }

    private bool IsCurrentActorAndTenant(Guid actorUserId, Guid tenantId)
    {
        if (actorUserId == Guid.Empty || tenantId == Guid.Empty)
            return false;

        var principal = _httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true
            && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var currentActorId)
            && currentActorId == actorUserId
            && Guid.TryParse(principal.FindFirstValue("tenantId"), out var currentTenantId)
            && currentTenantId == tenantId;
    }
}
