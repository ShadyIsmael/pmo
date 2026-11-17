namespace NEXUS.Modules.Identity.Application;

/// <summary>
/// Applies the default-open protection policy to Organization-owned Roles
/// while keeping reserved Platform and cross-tenant capabilities unavailable.
/// EffectivePermissionService activates these grants only for their owning
/// Organization.
/// </summary>
internal static class OrganizationRolePermissionPolicy
{
    public static bool CanGrant(
        PermissionCatalogEntry permission,
        IPermissionProtectionPolicy? protection = null)
    {
        protection ??= PermissionProtectionPolicy.Empty;
        return protection.IsGrantable(permission)
            && permission.Scope is PermissionScope.Tenant
                or PermissionScope.Organization
                or PermissionScope.Portfolio
                or PermissionScope.Program
                or PermissionScope.Project;
    }
}
