namespace NEXUS.Modules.Identity.Application;

/// <summary>
/// The categories tenant administrators may assign to custom roles. System is a
/// reserved category for protected, code-defined roles and is not a selectable
/// custom-role category.
/// </summary>
public static class RoleCategories
{
    public const string Tenant = "Tenant";
    public const string Organization = "Organization";
    public const string Management = "Management";
    public const string Operation = "Operation";
    public const string Business = "Business";
    public const string System = "System";

    public static IReadOnlyList<string> Selectable { get; } = Array.AsReadOnly(
        new[] { Tenant, Organization, Management, Operation, Business });

    public static bool IsSelectable(string? category) =>
        category is not null && Selectable.Contains(category, StringComparer.Ordinal);
}

/// <summary>
/// Explicit role-category eligibility for each permission key. This metadata
/// constrains role configuration only; it does not grant runtime access or
/// replace permission dependencies, delegation, or resource scope checks.
/// </summary>
public static class PermissionRoleCategoryPolicy
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedByPermission = Build();

    public static IReadOnlyList<string> GetAllowedCategories(string permissionKey) =>
        AllowedByPermission.TryGetValue(permissionKey, out var categories) ? categories : [];

    public static bool IsAllowed(string permissionKey, string? category) =>
        category is not null
        && AllowedByPermission.TryGetValue(permissionKey, out var categories)
        && categories.Contains(category, StringComparer.Ordinal);

    public static IReadOnlyList<string> UnmappedActivePermissionKeys() => PermissionCatalog.Entries
        .Where(entry => entry.IsActive && !AllowedByPermission.ContainsKey(entry.Key))
        .Select(entry => entry.Key)
        .OrderBy(key => key, StringComparer.Ordinal)
        .ToArray();

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Build()
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        Add(map, [RoleCategories.Tenant],
            "Audit.View",
            "Directory.ExecuteSync", "Directory.ManageMappings", "Directory.ManageProvisioningPolicy",
            "Directory.PreviewSync", "Directory.TestConnection", "Directory.View", "Directory.ManageConnection",
            "Plugin.View",
            "Roles.View", "Roles.Create", "Roles.EditDetails", "Roles.ConfigurePermissions", "Roles.Delete",
            "Settings.View", "Settings.ManageTenantConfiguration", "TenantAdministration.ManageScope",
            "EmailNotifications.View", "EmailNotifications.Manage", "EmailNotifications.Test", "EmailNotifications.Logs",
            "Users.View", "Users.ResetPassword", "Users.ManageAccounts", "Users.SetStatus", "Users.AssignRoles",
            "UserPermissions.Manage");

        Add(map, [RoleCategories.Tenant, RoleCategories.Organization],
            "ReferenceData.View", "ReferenceData.Create", "ReferenceData.Edit", "ReferenceData.Delete",
            "Team.View", "Team.Create", "Team.Edit", "Team.Delete");

        Add(map, [RoleCategories.Organization, RoleCategories.Management, RoleCategories.Operation],
            "Resource.View");
        Add(map, [RoleCategories.Organization, RoleCategories.Management],
            "Resource.Create", "Resource.Edit", "Resource.Delete");
        Add(map, [RoleCategories.Management, RoleCategories.Operation],
            "Resource.Assign");
        Add(map, [RoleCategories.Business, RoleCategories.Management],
            "Resource.ViewCostRate", "Resource.EditCostRate");

        Add(map, [RoleCategories.Management, RoleCategories.Operation],
            "ChangeRequest.Manage", "ChangeRequest.View",
            "Document.Archive", "Document.Create", "Document.Edit", "Document.TemplateManage", "Document.View",
            "Issue.View",
            "Meeting.Create", "Meeting.Delete", "Meeting.Edit", "Meeting.Manage", "Meeting.View",
            "Planning.Edit", "Planning.View",
            "Risk.Create", "Risk.Delete", "Risk.Edit", "Risk.Mitigate", "Risk.View",
            "Stakeholders.Manage", "Stakeholders.Read",
            "StatusReport.Create", "StatusReport.Edit", "StatusReport.Export", "StatusReport.ManageConfiguration", "StatusReport.Publish",
            "StatusReport.View",
            "TimeTracking.ManageTeam", "TimeTracking.ViewTeam",
            "Workflow.View",
            "WorkItem.Create", "WorkItem.Delete", "WorkItem.Edit", "WorkItem.View");

        Add(map, [RoleCategories.Management],
            "ProjectPlan.Create",
            "ChangeRequest.Approve",
            "Governance.AssignmentRecovery.Initiate", "Governance.AssignmentRecovery.Manage", "Governance.AssignmentRecovery.View",
            "Handover.AdminInitiate", "Handover.Approve", "Handover.Manage", "Handover.View",
            "Planning.Lifecycle",
            "Portfolio.Create", "Portfolio.Edit", "Portfolio.Archive", "Portfolio.Reactivate",
            "Program.Create", "Program.Edit", "Program.Archive", "Program.Reactivate",
            "Project.Create", "Project.Delete", "Project.Edit",
            "ProjectAnnualBudget.Approve", "ProjectAnnualBudget.CarryForward", "ProjectAnnualBudget.Close",
            "ProjectAnnualBudget.CloseApprove", "ProjectAnnualBudget.ManageApprovalConfiguration",
            "ProjectApproval.Override",
            "ProjectFinancialCeiling.ApproveChange",
            "ProjectPaymentReversal.Approve",
            "BudgetRevision.Approve",
            "Workflow.Manage", "Workflow.Publish");

        Add(map, [RoleCategories.Tenant, RoleCategories.Management],
            "Program.CreateAcrossPortfolios");

        Add(map, [RoleCategories.Management, RoleCategories.Organization, RoleCategories.Operation, RoleCategories.Business],
            "Portfolio.View", "Program.View");
        Add(map, [RoleCategories.Management, RoleCategories.Organization, RoleCategories.Operation, RoleCategories.Business],
            "Project.View");
        Add(map, [RoleCategories.Management], "ProjectSettings.View", "ProjectSettings.Edit");

        Add(map, [RoleCategories.Management, RoleCategories.Operation],
            "TimeTracking.ManageOwn", "TimeTracking.ViewOwn");

        Add(map, [RoleCategories.Business, RoleCategories.Management, RoleCategories.Operation],
            "Budget.View", "BudgetRevision.View", "Cost.View", "Commitment.View", "PaymentObligation.View",
            "ProjectFinance.View", "ProjectAnnualBudget.View", "ProjectPaymentRequest.View",
            "Report.View");

        Add(map, [RoleCategories.Business, RoleCategories.Operation],
            "Budget.Create", "Budget.Edit", "BudgetRevision.Create", "BudgetRevision.EditDraft",
            "BudgetRevision.SubmitForReview", "BudgetRevision.Cancel",
            "Cost.Create", "Cost.Edit", "Commitment.Create", "Commitment.Edit", "Commitment.Cancel",
            "PaymentObligation.Create", "PaymentObligation.Edit",
            "PaymentSettlement.Record",
            "ProjectAnnualBudget.Create", "ProjectAnnualBudget.EditDraft", "ProjectAnnualBudget.SubmitForReview",
            "ProjectAnnualBudget.ReviseApproved",
            "ProjectPaymentRequest.Create", "ProjectPaymentRequest.EditDraft", "ProjectPaymentRequest.Submit",
            "ProjectPaymentRequest.Cancel", "ProjectPaymentReversal.Create",
            "ApprovalRequest.Submit", "ApprovalRequest.Cancel");

        Add(map, [RoleCategories.Business],
            "Budget.Delete", "BudgetRevision.Delete", "Cost.Delete", "Commitment.Close", "Commitment.Delete",
            "PaymentObligation.Delete", "PaymentObligationSettlement.Record");

        Add(map, [RoleCategories.Business, RoleCategories.Management],
            "ProjectAnnualBudgetClosing.View", "CostUtilization.Export", "ProjectFinancialAuditTrail.View",
            "ProjectFinancialFoundation.View", "ProjectSettlement.View", "ProgramFinancialRollup.View",
            "PortfolioFinancialRollup.View");

        Add(map, [RoleCategories.Management],
            "ProjectPaymentRequest.Approve");

        Add(map, [RoleCategories.Management, RoleCategories.Business, RoleCategories.Operation],
            "ApprovalRequest.View");
        Add(map, [RoleCategories.Management],
            "ApprovalRequest.Approve", "ApprovalRequest.Reject", "ApprovalRequest.Delegate");

        Add(map, RoleCategories.Selectable,
            "Dashboard.View");

        return map;
    }

    private static void Add(
        IDictionary<string, IReadOnlyList<string>> map,
        IReadOnlyList<string> allowedCategories,
        params string[] permissionKeys)
    {
        foreach (var permissionKey in permissionKeys)
        {
            if (!map.TryAdd(permissionKey, Array.AsReadOnly(allowedCategories.ToArray())))
                throw new InvalidOperationException($"Permission role-category policy defines '{permissionKey}' more than once.");
        }
    }
}
