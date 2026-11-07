using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

/// <summary>
/// Defines the protected, tenant-scoped System Admin baseline. The catalog's
/// Administration module classification and explicit baseline flag are authoritative;
/// labels and UI placement alone never add a permission to this baseline.
/// </summary>
public static class ProtectedTenantAdminPolicy
{
    private static readonly string[] ApprovedPortfolioProgramKeys =
    [
        "Portfolio.View", "Portfolio.Create", "Portfolio.Edit", "Portfolio.Archive", "Portfolio.Reactivate",
        "Program.View", "Program.Create", "Program.Edit", "Program.Archive", "Program.Reactivate",
        "Handover.View", "Handover.Manage", "Handover.Approve", "Handover.AdminInitiate"
    ];

    public static IReadOnlySet<string> TenantAdministrationPermissionKeys =>
        GetTenantAdministrationPermissionKeys(PermissionCatalog.Entries);

    public static IReadOnlySet<string> PortfolioProgramPermissionKeys =>
        GetRequiredActiveTenantKeys(PermissionCatalog.Entries, ApprovedPortfolioProgramKeys);

    public static IReadOnlySet<string> MandatoryPermissionKeys =>
        TenantAdministrationPermissionKeys
            .Concat(PortfolioProgramPermissionKeys)
            .ToHashSet(StringComparer.Ordinal);

    private static readonly string[] TenantAdminRecoveryPermissionKeys =
    [
        "Roles.ConfigurePermissions",
        "Users.AssignRoles",
        "Users.SetStatus"
    ];

    public static bool IsEffectiveTenantAdmin(IReadOnlySet<string> permissions) =>
        TenantAdminRecoveryPermissionKeys.All(permissions.Contains);

    public static IReadOnlySet<string> GetTenantAdministrationPermissionKeys(
        IEnumerable<PermissionCatalogEntry> entries)
    {
        return entries
            .Where(entry => entry.IsActive
                && entry.Scope == PermissionScope.Tenant
                && (string.Equals(entry.ModuleId, "administration", StringComparison.Ordinal)
                    || entry.IsTenantAdminBaseline))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static bool IsProtectedSystemAdminRole(Role role) =>
        role.IsSystem
        && role.TenantId != Guid.Empty
        && string.Equals(role.SystemRoleKey, Role.TenantAdminSystemRoleKey, StringComparison.Ordinal);

    private static IReadOnlySet<string> GetRequiredActiveTenantKeys(
        IEnumerable<PermissionCatalogEntry> entries,
        IEnumerable<string> requiredKeys)
    {
        var byKey = entries.ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in requiredKeys)
        {
            if (!byKey.TryGetValue(key, out var entry)
                || !entry.IsActive
                || entry.Scope != PermissionScope.Tenant)
            {
                throw new InvalidOperationException($"Required protected Admin permission '{key}' is missing, inactive, or not tenant-scoped.");
            }

            result.Add(key);
        }

        return result;
    }
}
