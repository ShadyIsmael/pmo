using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

/// <summary>
/// Identifies the two canonical administrator System Roles. The independent
/// last-admin safeguard preserves an active assignment to the canonical Tenant
/// Admin role; it does not depend on a hardcoded permission baseline.
/// </summary>
public static class ProtectedTenantAdminPolicy
{
    public static bool IsProtectedSystemAdminRole(Role role) =>
        IsCanonicalSystemRole(role, Role.TenantAdminRoleKey);

    public static bool IsOrganizationAdminSystemRole(Role role) =>
        IsCanonicalSystemRole(role, Role.OrganizationAdminRoleKey);

    public static bool IsCanonicalSystemRole(Role role, string expectedRoleKey) =>
        role.TenantId != Guid.Empty
        && role.IsSystem
        && role.OrganizationId is null
        && !role.IsLegacyOrganizationOwnershipUnresolved
        && string.Equals(role.RoleKey, expectedRoleKey, StringComparison.Ordinal)
        && string.Equals(role.SystemRoleKey, expectedRoleKey, StringComparison.Ordinal);

    /// <summary>
    /// Validates role identity independently of its ownership scope. Ordinary
    /// roles with no OrganizationId are Tenant-owned; unresolved legacy records
    /// remain inactive until their owner is reconciled.
    /// </summary>
    public static bool HasValidSystemRoleIdentity(Role role)
    {
        if (role.TenantId == Guid.Empty)
            return false;

        if (!role.IsSystem)
            return !role.IsLegacyOrganizationOwnershipUnresolved
                && role.SystemRoleKey is null
                && !IsReservedAdministratorRoleKey(role.RoleKey);

        return role.OrganizationId is null
            && !role.IsLegacyOrganizationOwnershipUnresolved
            && (role.SystemRoleKey is Role.TenantAdminRoleKey or Role.OrganizationAdminRoleKey)
            && string.Equals(role.RoleKey, role.SystemRoleKey, StringComparison.Ordinal);
    }

    public static bool IsReservedAdministratorRoleKey(string? roleKey) =>
        string.Equals(roleKey, Role.TenantAdminRoleKey, StringComparison.Ordinal)
        || string.Equals(roleKey, Role.OrganizationAdminRoleKey, StringComparison.Ordinal);

    public static bool HasReservedAdministratorIdentity(Role role) =>
        IsReservedAdministratorRoleKey(role.RoleKey)
        || IsReservedAdministratorRoleKey(role.SystemRoleKey);
}
