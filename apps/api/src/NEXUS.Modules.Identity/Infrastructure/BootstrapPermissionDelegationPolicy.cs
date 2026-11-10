using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Infrastructure;

/// <summary>
/// Narrow first-grant delegation for explicitly selected workflow capabilities.
/// It gives qualifying tenant role administrators delegation authority only; it
/// does not add the capabilities to their own effective permission set.
/// </summary>
internal static class BootstrapPermissionDelegationPolicy
{
    private static readonly string[] PermissionKeys =
    [
        // A scoped tenant role administrator can make Project Planning available
        // to custom roles without receiving those rights. Dependencies, Project
        // scope, and Active Deny remain enforced by the catalog and resolver.
        "ProjectPlan.Create",
        "Planning.Edit",
        "Planning.View",

        // A tenant role administrator may explicitly configure the complete
        // project-scoped Annual Budget and Closing permission set without
        // receiving those workflow permissions personally. Dependencies,
        // Active Deny, and Project scope remain independently enforced by the
        // catalog, role service, and effective resolver.
        "ProjectAnnualBudget.View",
        "ProjectAnnualBudget.Create",
        "ProjectAnnualBudget.EditDraft",
        "ProjectAnnualBudget.ConfigureDraftApproval",
        "ProjectAnnualBudget.SubmitForReview",
        "ProjectAnnualBudget.Approve",
        "ProjectAnnualBudget.ReviseApproved",
        "ProjectAnnualBudget.ManageApprovalConfiguration",
        "ProjectAnnualBudget.Delete",
        "ProjectAnnualBudget.Withdraw",
        "ProjectAnnualBudgetClosing.View",
        "ProjectAnnualBudget.Close",
        "ProjectAnnualBudget.CloseApprove",
        "ProjectAnnualBudget.CarryForward",
        "ProjectFinancialFoundation.View",
        "CostUtilization.Export",
        "ProgramFinancialRollup.View",
        "PortfolioFinancialRollup.View"
    ];

    public static IReadOnlySet<string> Keys { get; } = PermissionKeys.ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> ProtectedAdminAnnualBudgetPermissionKeys =
    [
        "ProjectAnnualBudget.View",
        "ProjectAnnualBudget.Create",
        "ProjectAnnualBudget.EditDraft",
        "ProjectAnnualBudget.SubmitForReview",
        "ProjectAnnualBudget.Approve",
        "ProjectAnnualBudget.ReviseApproved",
        "ProjectAnnualBudget.ManageApprovalConfiguration",
        "ProjectAnnualBudget.Delete",
        "ProjectAnnualBudget.Withdraw",
        "ProjectAnnualBudgetClosing.View",
        "ProjectAnnualBudget.Close",
        "ProjectAnnualBudget.CloseApprove",
        "ProjectAnnualBudget.CarryForward"
    ];

    public static bool CanAddToProtectedTenantAdminRole(string permissionKey) =>
        ProtectedAdminAnnualBudgetPermissionKeys.Contains(permissionKey);

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
                && definition.Scope is not (PermissionScope.Platform or PermissionScope.CrossTenant or PermissionScope.SystemOwner)
                && !deniedKeys.Contains(key, StringComparer.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static readonly IReadOnlySet<string> EmptyPermissionKeys = new HashSet<string>(StringComparer.Ordinal);
}
