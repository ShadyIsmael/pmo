using NEXUS.Modules.Identity.Application;
using NEXUS.Api.ProgramManagement;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class PermissionCatalogArchitectureTests
{
    private static readonly HashSet<string> AssignedReviewExceptions = new(StringComparer.Ordinal)
    {
        "ChangeRequest.Approve",
        "ProjectAnnualBudget.Approve",
        "ProjectPaymentRequest.Approve",
        "ProjectPaymentReversal.Approve",
        "ProjectFinancialCeiling.ApproveChange"
    };

    private static readonly HashSet<string> ExternalDataPermissionExceptions = new(StringComparer.Ordinal)
    {
        "CostUtilization.Export"
    };

    [Fact]
    public void Catalog_has_unique_keys_and_seed_ids()
    {
        Assert.True(PermissionCatalog.HasUniqueKeys());
        Assert.Equal(PermissionCatalog.Entries.Count,
            PermissionCatalog.Entries.Select(item => item.SeedId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Catalog_matches_the_active_source_inventory()
    {
        var entries = PermissionCatalog.Entries;
        var activeEntries = entries.Where(item => item.IsActive).ToArray();

        Assert.Equal(171, entries.Count);
        Assert.Equal(171, entries.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(171, activeEntries.Length);
        Assert.Equal(46, activeEntries.Count(item => item.Level == PermissionLevel.View));
        Assert.Equal(125, activeEntries.Count(item => item.Level == PermissionLevel.Action));
        Assert.Empty(entries.Where(item => !item.IsActive));
        Assert.DoesNotContain("Resource.Allocate", PermissionCatalog.ByKey.Keys);
        Assert.DoesNotContain("Resource.ManageRates", PermissionCatalog.ByKey.Keys);
        Assert.Contains("Resource.ViewCostRate", PermissionCatalog.ByKey["Resource.EditCostRate"].RequiredPermissionKeys);
    }

    [Fact]
    public void Project_plan_api_and_ui_controls_use_separate_create_and_edit_permissions()
    {
        var root = FindRepositoryRoot();
        var planController = File.ReadAllText(Path.Combine(root, "apps/api/src/NEXUS.Modules.ProjectPlanning/Api/PlanController.cs"));
        var importController = File.ReadAllText(Path.Combine(root, "apps/api/src/NEXUS.Modules.ProjectPlanning/Api/PlanImportController.cs"));
        var planService = File.ReadAllText(Path.Combine(root, "apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanService.cs"));
        var importService = File.ReadAllText(Path.Combine(root, "apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanningImportService.cs"));
        var planningUi = File.ReadAllText(Path.Combine(root, "apps/web-react/src/features/projects/ProjectPlanningTabFull.tsx"));
        var importUi = File.ReadAllText(Path.Combine(root, "apps/web-react/src/features/projects/components/PlanningImportSheet.tsx"));

        Assert.Contains("[Authorize(Policy = \"ProjectPlan.Create\")]", planController, StringComparison.Ordinal);
        Assert.Contains("HasClaim(\"permission\", \"ProjectPlan.Create\")", planService, StringComparison.Ordinal);
        Assert.Contains("if (!User.HasClaim(\"permission\", \"Planning.Edit\")", importController, StringComparison.Ordinal);
        Assert.Contains("!request.TargetPlanId.HasValue && !User.HasClaim(\"permission\", \"ProjectPlan.Create\")", importController, StringComparison.Ordinal);
        Assert.Contains("HasClaim(\"permission\", \"Planning.Edit\")", importService, StringComparison.Ordinal);
        Assert.Contains("HasClaim(\"permission\", \"ProjectPlan.Create\")", importService, StringComparison.Ordinal);

        Assert.Contains("hasPermission(user.permissions, Permissions.ProjectPlan.Create)", planningUi, StringComparison.Ordinal);
        Assert.Contains("hasPermission(user.permissions, Permissions.Planning.Edit)", planningUi, StringComparison.Ordinal);
        Assert.Contains("{canCreateProjectPlan ? <Button", planningUi, StringComparison.Ordinal);
        Assert.Contains("editable={editable}", planningUi, StringComparison.Ordinal);
        Assert.Contains("canCreatePlan={canCreateProjectPlan}", planningUi, StringComparison.Ordinal);
        Assert.Contains("canEditPlan={canManagePlanning}", planningUi, StringComparison.Ordinal);
        Assert.Contains("const canImportAsNewPlan = canCreatePlan && canEditPlan;", importUi, StringComparison.Ordinal);
        Assert.Contains("const canImportIntoExistingPlan = canEditPlan && plans.length > 0;", importUi, StringComparison.Ordinal);
    }

    [Fact]
    public void Active_catalog_entries_have_consistent_module_and_screen_taxonomy()
    {
        var activeEntries = PermissionCatalog.Entries.Where(item => item.IsActive).ToArray();

        Assert.All(activeEntries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.ModuleId));
            Assert.False(string.IsNullOrWhiteSpace(entry.ModuleNameEn));
            Assert.False(string.IsNullOrWhiteSpace(entry.ModuleNameAr));
            Assert.False(string.IsNullOrWhiteSpace(entry.ScreenId));
            Assert.False(string.IsNullOrWhiteSpace(entry.ScreenNameEn));
            Assert.False(string.IsNullOrWhiteSpace(entry.ScreenNameAr));
        });

        Assert.All(activeEntries.GroupBy(item => item.ModuleId), module =>
        {
            Assert.Single(module.Select(item => item.ModuleNameEn).Distinct(StringComparer.Ordinal));
            Assert.Single(module.Select(item => item.ModuleNameAr).Distinct(StringComparer.Ordinal));
        });

        Assert.All(activeEntries.GroupBy(item => (item.ModuleId, item.ScreenId)), screen =>
        {
            Assert.Single(screen.Select(item => item.ScreenNameEn).Distinct(StringComparer.Ordinal));
            Assert.Single(screen.Select(item => item.ScreenNameAr).Distinct(StringComparer.Ordinal));
        });
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                    && Directory.Exists(Path.Combine(directory.FullName, "apps")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for source contract assertions.");
    }

    [Fact]
    public void Permission_seed_contains_only_active_catalog_keys_and_stable_ids()
    {
        var activeEntries = PermissionCatalog.Entries.Where(item => item.IsActive).ToArray();
        var seeded = PermissionCatalog.CreateSeedPermissions(Guid.NewGuid());

        Assert.Equal(activeEntries.Select(item => item.Key).OrderBy(key => key, StringComparer.Ordinal),
            seeded.Select(permission => permission.Name).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(activeEntries.Select(item => item.SeedId).OrderBy(id => id, StringComparer.Ordinal),
            seeded.Select(permission => permission.Id.ToString()).OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void Protected_admin_baseline_tracks_active_tenant_administration_catalog_entries_only()
    {
        var template = PermissionCatalog.ByKey["Roles.Create"];
        var futureTenantPermission = template with
        {
            SeedId = Guid.NewGuid().ToString(),
            Key = "TenantAdmin.FutureAction",
            Resource = "TenantAdmin",
            Action = "FutureAction",
            Scope = PermissionScope.Tenant
        };
        var organizationPermission = futureTenantPermission with
        {
            Key = "TenantAdmin.OrganizationAction",
            Action = "OrganizationAction",
            Scope = PermissionScope.Organization
        };
        var platformPermission = futureTenantPermission with
        {
            Key = "TenantAdmin.PlatformAction",
            Action = "PlatformAction",
            Scope = PermissionScope.Platform
        };
        var inactivePermission = futureTenantPermission with
        {
            Key = "TenantAdmin.InactiveAction",
            Action = "InactiveAction",
            IsActive = false
        };

        var baseline = ProtectedTenantAdminPolicy.GetTenantAdministrationPermissionKeys(
            [futureTenantPermission, organizationPermission, platformPermission, inactivePermission]);

        Assert.Contains("TenantAdmin.FutureAction", baseline);
        Assert.DoesNotContain("TenantAdmin.OrganizationAction", baseline);
        Assert.DoesNotContain("TenantAdmin.PlatformAction", baseline);
        Assert.DoesNotContain("TenantAdmin.InactiveAction", baseline);
    }

    [Fact]
    public void Protected_admin_baseline_contains_approved_portfolio_program_and_handover_keys_but_not_cross_portfolio_scope()
    {
        var baseline = ProtectedTenantAdminPolicy.MandatoryPermissionKeys;

        Assert.Contains("Portfolio.Create", baseline);
        Assert.Contains("Portfolio.Edit", baseline);
        Assert.Contains("Portfolio.Archive", baseline);
        Assert.Contains("Program.Create", baseline);
        Assert.Contains("Program.Edit", baseline);
        Assert.Contains("Program.Archive", baseline);
        Assert.Contains("Handover.AdminInitiate", baseline);
        Assert.DoesNotContain("Program.CreateAcrossPortfolios", baseline);
        Assert.DoesNotContain("Program.Manage", baseline);
        Assert.DoesNotContain("Portfolio.Manage", baseline);
    }

    [Fact]
    public void Protected_admin_baseline_includes_active_tenant_reference_data_permissions()
    {
        var mandatory = ProtectedTenantAdminPolicy.MandatoryPermissionKeys;

        Assert.Equal(47, mandatory.Count);
        Assert.Contains("ReferenceData.View", mandatory);
        Assert.Contains("ReferenceData.Create", mandatory);
        Assert.Contains("ReferenceData.Edit", mandatory);
        Assert.Contains("ReferenceData.Delete", mandatory);
        Assert.Contains("Plugin.View", mandatory);
        Assert.Contains("Directory.ManageConnection", mandatory);
        Assert.DoesNotContain("ReferenceData.Import", mandatory);
        Assert.DoesNotContain("ReferenceData.Export", mandatory);
    }

    [Fact]
    public void New_issue_and_plugin_read_capabilities_are_tenant_scoped()
    {
        var issue = PermissionCatalog.ByKey["Issue.View"];
        var plugin = PermissionCatalog.ByKey["Plugin.View"];

        Assert.True(issue.IsActive);
        Assert.Equal(PermissionScope.Tenant, issue.Scope);
        Assert.Equal(PermissionLevel.View, issue.Level);
        Assert.True(plugin.IsActive);
        Assert.Equal(PermissionScope.Tenant, plugin.Scope);
        Assert.Equal(PermissionLevel.View, plugin.Level);
        Assert.DoesNotContain("Issue.View", ProtectedTenantAdminPolicy.MandatoryPermissionKeys);
        Assert.Contains("Plugin.View", ProtectedTenantAdminPolicy.MandatoryPermissionKeys);
    }

    [Fact]
    public void Project_plan_create_is_separate_from_plan_content_edit_and_has_narrow_role_eligibility()
    {
        var create = PermissionCatalog.ByKey["ProjectPlan.Create"];
        var edit = PermissionCatalog.ByKey["Planning.Edit"];
        var view = PermissionCatalog.ByKey["Planning.View"];

        Assert.True(create.IsActive);
        Assert.True(create.IsAssignable);
        Assert.Equal(PermissionScope.Project, create.Scope);
        Assert.Equal(PermissionLevel.Action, create.Level);
        Assert.Contains("Planning.View", create.RequiredPermissionKeys);
        Assert.Equal(new[] { RoleCategories.Management }, create.AllowedCategories);

        Assert.True(edit.IsActive);
        Assert.Equal(PermissionScope.Project, edit.Scope);
        Assert.Contains("Planning.View", edit.RequiredPermissionKeys);
        Assert.Equal(new[] { RoleCategories.Management, RoleCategories.Operation }, edit.AllowedCategories);

        Assert.False(string.IsNullOrWhiteSpace(create.LabelEn));
        Assert.False(string.IsNullOrWhiteSpace(create.LabelAr));
        Assert.False(string.IsNullOrWhiteSpace(create.DescriptionEn));
        Assert.False(string.IsNullOrWhiteSpace(create.DescriptionAr));

        Assert.Equal(PermissionLevel.View, view.Level);
        Assert.DoesNotContain("ProjectPlan.Create", ProtectedTenantAdminPolicy.MandatoryPermissionKeys);
        Assert.DoesNotContain("Planning.Edit", ProtectedTenantAdminPolicy.MandatoryPermissionKeys);
        Assert.DoesNotContain("Planning.View", ProtectedTenantAdminPolicy.MandatoryPermissionKeys);
        Assert.False(create.IsTenantAdminBaseline);
        Assert.False(edit.IsTenantAdminBaseline);
        Assert.False(view.IsTenantAdminBaseline);
    }

    [Fact]
    public void Finance_actions_are_canonical_and_do_not_grant_unrelated_decisions()
    {
        const string createKey = "ProjectAnnualBudget.Create";
        const string viewKey = "ProjectAnnualBudget.View";
        var sensitive = new[]
        {
            "ProjectAnnualBudget.Approve", "ProjectPaymentRequest.Approve", "PaymentSettlement.Record",
            "ProjectPaymentReversal.Approve", "ProjectAnnualBudget.CloseApprove", "ProjectApproval.Override",
            "ProjectAnnualBudget.ManageApprovalConfiguration", "ProjectFinancialCeiling.ApproveChange"
        };

        var create = PermissionCatalog.ByKey[createKey];
        Assert.True(create.IsActive);
        Assert.Equal(PermissionScope.Project, create.Scope);
        Assert.Contains(viewKey, create.RequiredPermissionKeys);
        var effective = PermissionCatalog.EnforceDependencies([createKey, viewKey]);
        Assert.Contains(createKey, effective);
        Assert.Contains(viewKey, effective);
        foreach (var decision in sensitive) Assert.DoesNotContain(decision, effective);
        Assert.DoesNotContain("ProjectFinance.Manage", PermissionCatalog.ByKey.Keys);
        Assert.DoesNotContain(createKey, PermissionCatalog.EnforceDependencies(["ProjectFinance.Manage", viewKey]));

        Assert.True(PermissionCatalog.ByKey["ProjectFinancialAuditTrail.View"].IsActive);
        Assert.Equal(PermissionLevel.View, PermissionCatalog.ByKey["ProjectFinancialAuditTrail.View"].Level);
        Assert.DoesNotContain("ProjectFinancialAuditTrail.Manage", PermissionCatalog.ByKey.Keys);
        Assert.DoesNotContain("ProjectAnnualBudget.Manage", PermissionCatalog.ByKey.Keys);
        Assert.DoesNotContain("ProjectPaymentRequest.Manage", PermissionCatalog.ByKey.Keys);
        Assert.DoesNotContain("ProjectSettlement.Manage", PermissionCatalog.ByKey.Keys);
        Assert.DoesNotContain("ProjectAnnualBudgetClosing.Manage", PermissionCatalog.ByKey.Keys);
    }

    [Fact]
    public void Annual_budget_configuration_permissions_remain_sensitive_and_outside_admin_baseline()
    {
        var view = PermissionCatalog.ByKey["ProjectAnnualBudget.View"];
        var manageApprovalConfiguration = PermissionCatalog.ByKey["ProjectAnnualBudget.ManageApprovalConfiguration"];

        Assert.Equal(PermissionScope.Project, view.Scope);
        Assert.Equal(PermissionLevel.View, view.Level);
        Assert.True(view.IsActive);
        Assert.True(view.IsAssignable);
        Assert.True(view.IsSensitive);
        Assert.False(view.IsTenantAdminBaseline);

        Assert.Equal(PermissionScope.Project, manageApprovalConfiguration.Scope);
        Assert.Equal(PermissionLevel.Action, manageApprovalConfiguration.Level);
        Assert.True(manageApprovalConfiguration.IsActive);
        Assert.True(manageApprovalConfiguration.IsAssignable);
        Assert.True(manageApprovalConfiguration.IsSensitive);
        Assert.False(manageApprovalConfiguration.IsTenantAdminBaseline);
        Assert.Contains(view.Key, manageApprovalConfiguration.RequiredPermissionKeys);

        Assert.DoesNotContain(view.Key, ProtectedTenantAdminPolicy.MandatoryPermissionKeys);
        Assert.DoesNotContain(manageApprovalConfiguration.Key, ProtectedTenantAdminPolicy.MandatoryPermissionKeys);

        var projectAnnualBudgetKeys = new[]
        {
            "ProjectAnnualBudget.Approve", "ProjectAnnualBudget.Create", "ProjectAnnualBudget.EditDraft",
            "ProjectAnnualBudget.SubmitForReview", "ProjectAnnualBudget.ReviseApproved", "ProjectApproval.Override"
        };
        foreach (var key in projectAnnualBudgetKeys)
        {
            var permission = PermissionCatalog.ByKey[key];
            Assert.Equal(PermissionScope.Project, permission.Scope);
            Assert.False(permission.IsTenantAdminBaseline);
        }
    }

    [Fact]
    public void New_financial_read_and_export_permissions_have_narrow_assignable_scopes_and_no_admin_baseline()
    {
        var foundation = PermissionCatalog.ByKey["ProjectFinancialFoundation.View"];
        Assert.Equal(PermissionScope.Project, foundation.Scope);
        Assert.Equal(PermissionLevel.View, foundation.Level);
        Assert.Equal("financial-management", foundation.ModuleId);
        Assert.Equal("financial-overview", foundation.ScreenId);
        Assert.True(foundation.IsAssignable);
        Assert.True(foundation.IsSensitive);
        Assert.False(foundation.IsTenantAdminBaseline);
        Assert.Empty(foundation.RequiredPermissionKeys);

        var export = PermissionCatalog.ByKey["CostUtilization.Export"];
        Assert.Equal(PermissionScope.Tenant, export.Scope);
        Assert.Equal(PermissionLevel.Action, export.Level);
        Assert.Equal("capacity", export.ModuleId);
        Assert.Equal("cost-utilization", export.ScreenId);
        Assert.True(export.IsAssignable);
        Assert.True(export.IsSensitive);
        Assert.False(export.IsTenantAdminBaseline);
        Assert.Contains("Resource.View", export.RequiredPermissionKeys);

        Assert.DoesNotContain(foundation.Key, ProtectedTenantAdminPolicy.MandatoryPermissionKeys);
        Assert.DoesNotContain(export.Key, ProtectedTenantAdminPolicy.MandatoryPermissionKeys);
    }

    [Fact]
    public void Financial_rollup_permissions_are_distinct_parent_scoped_and_excluded_from_admin_baseline()
    {
        var program = PermissionCatalog.ByKey["ProgramFinancialRollup.View"];
        Assert.Equal(PermissionScope.Program, program.Scope);
        Assert.Equal(PermissionLevel.View, program.Level);
        Assert.Equal("financial-management", program.ModuleId);
        Assert.Equal("program-financial-rollup", program.ScreenId);
        Assert.True(program.IsAssignable);
        Assert.True(program.IsSensitive);
        Assert.False(program.IsTenantAdminBaseline);
        Assert.Empty(program.RequiredPermissionKeys);

        var portfolio = PermissionCatalog.ByKey["PortfolioFinancialRollup.View"];
        Assert.Equal(PermissionScope.Portfolio, portfolio.Scope);
        Assert.Equal(PermissionLevel.View, portfolio.Level);
        Assert.Equal("financial-management", portfolio.ModuleId);
        Assert.Equal("portfolio-financial-rollup", portfolio.ScreenId);
        Assert.True(portfolio.IsAssignable);
        Assert.True(portfolio.IsSensitive);
        Assert.False(portfolio.IsTenantAdminBaseline);
        Assert.Empty(portfolio.RequiredPermissionKeys);

        Assert.DoesNotContain("ProjectFinancialRollup.View", PermissionCatalog.ByKey.Keys);
        Assert.DoesNotContain(program.Key, ProtectedTenantAdminPolicy.MandatoryPermissionKeys);
        Assert.DoesNotContain(portfolio.Key, ProtectedTenantAdminPolicy.MandatoryPermissionKeys);
    }

    [Fact]
    public void Legacy_and_umbrella_permission_keys_are_absent_from_the_catalog()
    {
        var retiredKeys = new[]
        {
            "projects.read", "projects.write", "projects.delete", "finances.read", "finances.write",
            "reports.read", "settings.read", "settings.write", "users.read", "users.write",
            "users.reset-password", "StatusReport.CreateOrView", "Handover.Access", "Handover.Capabilities",
            "Directory.GroupCatalog",
            "ReferenceData.Manage", "Program.Manage", "Portfolio.Manage", "Planning.Manage",
            "Directory.Manage", "Resource.Manage", "Team.Manage", "Roles.Manage",
            "ProjectAnnualBudget.Manage", "ProjectPaymentRequest.Manage", "ProjectSettlement.Manage",
            "ProjectAnnualBudgetClosing.Manage"
        };
        foreach (var key in retiredKeys) Assert.DoesNotContain(key, PermissionCatalog.ByKey.Keys);
    }

    [Fact]
    public void Planning_edit_requires_planning_view()
    {
        var edit = PermissionCatalog.ByKey["Planning.Edit"];
        Assert.Equal(PermissionScope.Project, edit.Scope);
        Assert.Contains("Planning.View", edit.RequiredPermissionKeys);
    }

    [Fact]
    public void Time_tracking_management_depends_on_its_own_resource_scope_view_permission()
    {
        Assert.Contains("TimeTracking.ViewOwn", PermissionCatalog.ByKey["TimeTracking.ManageOwn"].RequiredPermissionKeys);
        Assert.Contains("TimeTracking.ViewTeam", PermissionCatalog.ByKey["TimeTracking.ManageTeam"].RequiredPermissionKeys);

        var ownViewDenied = PermissionCatalog.EnforceDependencies(["TimeTracking.ManageOwn", "TimeTracking.ViewTeam"]);
        Assert.DoesNotContain("TimeTracking.ManageOwn", ownViewDenied);

        var teamViewDenied = PermissionCatalog.EnforceDependencies(["TimeTracking.ManageTeam", "TimeTracking.ViewOwn"]);
        Assert.DoesNotContain("TimeTracking.ManageTeam", teamViewDenied);
    }

    [Fact]
    public void Active_entries_have_complete_labels_descriptions_and_valid_dependencies()
    {
        var entries = PermissionCatalog.Entries;
        var byKey = entries.ToDictionary(item => item.Key, StringComparer.Ordinal);

        foreach (var entry in entries.Where(item => item.IsActive))
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.ModuleId), entry.Key);
            Assert.False(string.IsNullOrWhiteSpace(entry.ScreenId), entry.Key);
            Assert.False(string.IsNullOrWhiteSpace(entry.LabelEn), entry.Key);
            Assert.False(string.IsNullOrWhiteSpace(entry.LabelAr), entry.Key);
            Assert.False(string.IsNullOrWhiteSpace(entry.DescriptionEn), entry.Key);
            Assert.False(string.IsNullOrWhiteSpace(entry.DescriptionAr), entry.Key);
            Assert.DoesNotContain('?', entry.ModuleNameAr);
            Assert.DoesNotContain('?', entry.ScreenNameAr);
            Assert.DoesNotContain('?', entry.LabelAr);
            Assert.DoesNotContain('?', entry.DescriptionAr);
            Assert.False(entry.LabelEn.Contains('\uFFFD'), entry.Key);
            Assert.False(entry.LabelAr.Contains('\uFFFD'), entry.Key);
            Assert.False(entry.DescriptionEn.Contains('\uFFFD'), entry.Key);
            Assert.False(entry.DescriptionAr.Contains('\uFFFD'), entry.Key);

            foreach (var dependency in entry.RequiredPermissionKeys)
                Assert.Contains(dependency, byKey.Keys);
        }
    }

    [Fact]
    public void Active_actions_require_their_screen_view_except_assigned_review_actions()
    {
        var entries = PermissionCatalog.Entries.Where(item => item.IsActive).ToDictionary(item => item.Key, StringComparer.Ordinal);

        foreach (var action in entries.Values.Where(item => item.Level == PermissionLevel.Action
            && !AssignedReviewExceptions.Contains(item.Key)
            && !ExternalDataPermissionExceptions.Contains(item.Key)))
        {
            Assert.Contains(action.RequiredPermissionKeys, dependency => entries.TryGetValue(dependency, out var view) && view.Level == PermissionLevel.View && view.ScreenId == action.ScreenId);
        }
    }

    [Fact]
    public void Effective_permission_resolution_removes_actions_with_missing_dependencies()
    {
        var missingView = PermissionCatalog.EnforceDependencies(["Program.Create"]);
        Assert.DoesNotContain("Program.Create", missingView);

        var completeGrant = PermissionCatalog.EnforceDependencies(["Program.Create", "Program.View"]);
        Assert.Contains("Program.Create", completeGrant);
        Assert.Contains("Program.View", completeGrant);

        var reviewerGrant = PermissionCatalog.EnforceDependencies(["ProjectAnnualBudget.Approve"]);
        Assert.Contains("ProjectAnnualBudget.Approve", reviewerGrant);

        var unqualifiedCeilingApproval = PermissionCatalog.EnforceDependencies(["ProjectFinancialCeiling.ApproveChange"]);
        Assert.DoesNotContain("ProjectFinancialCeiling.ApproveChange", unqualifiedCeilingApproval);
        var qualifiedCeilingApproval = PermissionCatalog.EnforceDependencies([
            "ProjectFinancialCeiling.ApproveChange", "ChangeRequest.Approve"]);
        Assert.Contains("ProjectFinancialCeiling.ApproveChange", qualifiedCeilingApproval);
    }

    [Fact]
    public void Inactive_and_uncatalogued_keys_never_become_effective()
    {
        Assert.DoesNotContain("Program.Manage", PermissionCatalog.ByKey.Keys);
        Assert.DoesNotContain("uncatalogued.permission", PermissionCatalog.EnforceDependencies([
            "uncatalogued.permission"]));
    }

    [Fact]
    public void Budget_revision_approval_is_independent_of_general_financial_write()
    {
        var view = PermissionCatalog.ByKey["BudgetRevision.View"];
        var approve = PermissionCatalog.ByKey["BudgetRevision.Approve"];

        Assert.True(view.IsActive);
        Assert.Equal(PermissionLevel.View, view.Level);
        Assert.True(approve.IsActive);
        Assert.Equal(PermissionLevel.Action, approve.Level);
        Assert.Contains("BudgetRevision.View", approve.RequiredPermissionKeys);
        Assert.Equal(new[] { "BudgetRevision.View" }, approve.RequiredPermissionKeys);
        Assert.DoesNotContain("BudgetRevision.Approve", PermissionCatalog.EnforceDependencies(["ProjectFinance.Manage", "BudgetRevision.View"]));
    }

    [Fact]
    public void Budget_revision_approval_uses_its_own_screen_view_and_action_keys()
    {
        var view = PermissionCatalog.ByKey["BudgetRevision.View"];
        var approve = PermissionCatalog.ByKey["BudgetRevision.Approve"];

        Assert.Equal(view.ScreenId, approve.ScreenId);
        Assert.Contains("BudgetRevision.View", approve.RequiredPermissionKeys);
        Assert.Contains("BudgetRevision.Approve", PermissionCatalog.EnforceDependencies([
            "BudgetRevision.View", "BudgetRevision.Approve"]));
        Assert.DoesNotContain("BudgetRevision.Approve", PermissionCatalog.EnforceDependencies([
            "BudgetRevision.Approve"]));
    }

    [Fact]
    public void Financial_rollup_authorization_helpers_use_the_matching_distinct_permission_key()
    {
        Assert.True(ProgramPortfolioFinancialRollupAuthorization.HasProgramRollupView(
            new HashSet<string>(["ProgramFinancialRollup.View"], StringComparer.Ordinal)));
        Assert.False(ProgramPortfolioFinancialRollupAuthorization.HasProgramRollupView(
            new HashSet<string>(["PortfolioFinancialRollup.View"], StringComparer.Ordinal)));
        Assert.True(ProgramPortfolioFinancialRollupAuthorization.HasPortfolioRollupView(
            new HashSet<string>(["PortfolioFinancialRollup.View"], StringComparer.Ordinal)));
        Assert.False(ProgramPortfolioFinancialRollupAuthorization.HasPortfolioRollupView(
            new HashSet<string>(["ProgramFinancialRollup.View"], StringComparer.Ordinal)));
    }

    [Fact]
    public void User_account_profile_and_status_changes_are_separate_actions()
    {
        var accountManagement = PermissionCatalog.ByKey["Users.ManageAccounts"];
        var statusManagement = PermissionCatalog.ByKey["Users.SetStatus"];

        Assert.Equal(PermissionLevel.Action, accountManagement.Level);
        Assert.Contains("Users.View", accountManagement.RequiredPermissionKeys);
        Assert.False(accountManagement.DescriptionEn.Contains("activate", StringComparison.OrdinalIgnoreCase));
        Assert.False(accountManagement.DescriptionEn.Contains("deactivate", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(accountManagement.ScreenId, statusManagement.ScreenId);
        Assert.Equal(PermissionLevel.Action, statusManagement.Level);
        Assert.Contains("Users.View", statusManagement.RequiredPermissionKeys);
        Assert.True(statusManagement.IsSensitive);
    }
}


