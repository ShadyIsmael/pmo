using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Infrastructure;

/// <summary>
/// Narrow first-grant delegation for explicitly selected finance capabilities.
/// It gives qualifying tenant role administrators delegation authority only; it
/// does not add the capabilities to their own effective permission set.
/// </summary>
internal static class BootstrapPermissionDelegationPolicy
{
    private static readonly string[] PermissionKeys =
    [
        // A tenant administrator may delegate the annual-budget View dependency
        // without receiving it personally on a fresh tenant.
        "ProjectAnnualBudget.View",
        "ProjectAnnualBudget.ManageApprovalConfiguration",
        "ProjectFinancialFoundation.View",
        "CostUtilization.Export",
        "ProgramFinancialRollup.View",
        "PortfolioFinancialRollup.View"
    ];

    public static IReadOnlySet<string> Keys { get; } = PermissionKeys.ToHashSet(StringComparer.Ordinal);

    public static async Task<IReadOnlySet<string>> GetDelegableKeysAsync(
        DbContext context,
        Guid actorId,
        Guid tenantId,
        IReadOnlySet<string> actorPermissions,
        CancellationToken cancellationToken)
    {
        if (!actorPermissions.Contains("Roles.ConfigurePermissions")
            || !actorPermissions.Contains("TenantAdministration.ManageScope"))
            return EmptyPermissionKeys;

        var deniedKeys = await context.Set<UserPermissionOverride>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.UserId == actorId && item.TenantId == tenantId && !item.IsDeleted
                && item.Effect == PermissionOverrideEffect.Deny
                && item.Permission.TenantId == tenantId && !item.Permission.IsDeleted
                && PermissionKeys.Contains(item.Permission.Resource + "." + item.Permission.Action))
            .Select(item => item.Permission.Resource + "." + item.Permission.Action)
            .Distinct()
            .ToListAsync(cancellationToken);

        return Keys
            .Where(key => PermissionCatalog.ByKey.TryGetValue(key, out var definition)
                && definition.IsActive
                && definition.IsAssignable
                && !deniedKeys.Contains(key, StringComparer.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static readonly IReadOnlySet<string> EmptyPermissionKeys = new HashSet<string>(StringComparer.Ordinal);
}
