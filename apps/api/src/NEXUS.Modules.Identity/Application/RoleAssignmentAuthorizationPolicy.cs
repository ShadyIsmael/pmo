using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

internal sealed record RoleAssignmentPolicyFailure(
    string ReasonCode,
    Role? Role = null,
    string? PermissionKey = null,
    PermissionCatalogEntry? Definition = null);

/// <summary>
/// Enforces role identity, permission scope, explicit protection, dependencies,
/// and self-escalation for ordinary role assignment.
/// </summary>
internal static class RoleAssignmentAuthorizationPolicy
{
    public static bool IsGenerallyAssignable(Role role) =>
        !role.IsDeleted
        && !role.IsLegacyOrganizationOwnershipUnresolved
        && ProtectedTenantAdminPolicy.HasValidSystemRoleIdentity(role)
        && !ProtectedTenantAdminPolicy.HasReservedAdministratorIdentity(role);

    public static RoleAssignmentPolicyFailure? Evaluate(
        IReadOnlyCollection<Role> roles,
        Guid actorUserId,
        Guid tenantId,
        Guid? targetUserId,
        IReadOnlySet<string> actorPermissions,
        IPermissionProtectionPolicy? protection = null)
    {
        protection ??= PermissionProtectionPolicy.Empty;
        if (actorUserId == Guid.Empty || tenantId == Guid.Empty)
            return new RoleAssignmentPolicyFailure("invalid-identity");

        if (roles.Any(role => role.TenantId != tenantId || role.IsDeleted))
            return new RoleAssignmentPolicyFailure("role-tenant-or-deleted");

        var selectedPermissionKeys = roles
            .SelectMany(role => role.Permissions)
            .Where(permission => permission.TenantId == tenantId && !permission.IsDeleted)
            .Select(PermissionKey)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var role in roles)
        {
            if (!ProtectedTenantAdminPolicy.HasValidSystemRoleIdentity(role))
                return new RoleAssignmentPolicyFailure("role-system-identity-invalid", role);

            if (ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role))
                return new RoleAssignmentPolicyFailure("protected-admin-role-assignment", role);

            if (ProtectedTenantAdminPolicy.IsOrganizationAdminSystemRole(role))
                return new RoleAssignmentPolicyFailure("organization-admin-appointment-unapproved", role);

            if (!IsGenerallyAssignable(role))
                return new RoleAssignmentPolicyFailure("role-not-assignable", role);

            foreach (var permission in role.Permissions)
            {
                if (permission.TenantId != tenantId || permission.IsDeleted)
                    return new RoleAssignmentPolicyFailure(
                        "permission-tenant-or-deleted", role, PermissionKey(permission));

                var key = PermissionKey(permission);
                if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition) || !definition.IsActive)
                    return new RoleAssignmentPolicyFailure(
                        "permission-uncatalogued-or-inactive", role, key, definition);

                if (!IsRoleAssignableScope(definition.Scope))
                    return new RoleAssignmentPolicyFailure("reserved-scope", role, key, definition);

                if (protection.IsProtected(key))
                    return new RoleAssignmentPolicyFailure("permission-protected", role, key, definition);

                var missingDependencies = definition.RequiredPermissionKeys
                    .Where(requiredKey => !selectedPermissionKeys.Contains(requiredKey))
                    .ToArray();
                if (missingDependencies.Length > 0)
                    return new RoleAssignmentPolicyFailure("permission-dependency-missing", role, key, definition);

                if (targetUserId == actorUserId && !actorPermissions.Contains(key))
                    return new RoleAssignmentPolicyFailure("self-escalation", role, key, definition);
            }
        }

        return null;
    }

    private static string PermissionKey(Permission permission) => $"{permission.Resource}.{permission.Action}";

    private static bool IsRoleAssignableScope(PermissionScope scope) =>
        scope is PermissionScope.Tenant
            or PermissionScope.Organization
            or PermissionScope.Portfolio
            or PermissionScope.Program
            or PermissionScope.Project;
}
