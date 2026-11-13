using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

/// <summary>
/// Defines the protected, tenant-scoped System Admin baseline. The catalog's
/// Administration module classification and explicit baseline flag are authoritative;
/// labels and UI placement alone never add a permission to this baseline.
///
/// In addition, every active Tenant-scoped permission in the catalog is automatically
/// inherited by the protected Admin role. This invariant survives future catalog
/// additions without manual SQL or repeated recovery tasks, because SeedDataProvider
/// reconciliation (in SeedDataProvider.cs) synchronizes the protected Admin baseline
/// against the current permission catalog every seed cycle.
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
        // Mandatory = all active Tenant-scoped permissions (auto-inherited by protected Admin)
        // + approved Portfolio/Program baseline keys
        GetAllTenantPermissionKeys(PermissionCatalog.Entries)
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

    /// <summary>
    /// Returns ALL active Tenant-scoped permission keys from the catalog.
    /// This is the auto-inherited baseline for the protected Admin role.
    /// </summary>
    public static IReadOnlySet<string> GetAllTenantPermissionKeys(
        IEnumerable<PermissionCatalogEntry> entries)
    {
        return entries
            .Where(entry => entry.IsActive
                && entry.Scope == PermissionScope.Tenant)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns the intersection of active Tenant-scoped keys and the legacy
    /// (administration-module-or-IsTenantAdminBaseline) set, preserved for
    /// backward-compatibility with any code that still references
    /// TenantAdministrationPermissionKeys directly.
    /// </summary>
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
