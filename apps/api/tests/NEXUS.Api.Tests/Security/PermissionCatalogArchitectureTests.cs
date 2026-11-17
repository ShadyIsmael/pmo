using System.Runtime.CompilerServices;
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

    private static readonly HashSet<string> CrossScreenViewExceptions = new(StringComparer.Ordinal)
    {
        "CostUtilization.Export",
        "PaymentSettlement.Record",
        "ProjectPaymentReversal.Create",
        "ProjectAnnualBudget.ManageApprovalConfiguration"
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

        Assert.Equal(190, entries.Count);
        Assert.Equal(190, entries.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(190, activeEntries.Length);
        Assert.Equal(50, activeEntries.Count(item => item.Level == PermissionLevel.View));
        Assert.Equal(140, activeEntries.Count(item => item.Level == PermissionLevel.Action));
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
        var wbsController = File.ReadAllText(Path.Combine(root, "apps/api/src/NEXUS.Modules.ProjectPlanning/Api/PlanWbsController.cs"));
        var importController = File.ReadAllText(Path.Combine(root, "apps/api/src/NEXUS.Modules.ProjectPlanning/Api/PlanImportController.cs"));
        var planService = File.ReadAllText(Path.Combine(root, "apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanService.cs"));
        var importService = File.ReadAllText(Path.Combine(root, "apps/api/src/NEXUS.Modules.ProjectPlanning/Application/PlanningImportService.cs"));
        var planningUi = File.ReadAllText(Path.Combine(root, "apps/web-react/src/features/projects/ProjectPlanningTabFull.tsx"));
        var importUi = File.ReadAllText(Path.Combine(root, "apps/web-react/src/features/projects/components/PlanningImportSheet.tsx"));

        Assert.Contains("[Authorize(Policy = \"ProjectPlan.Create\")]", planController, StringComparison.Ordinal);
        Assert.Matches(@"\[Authorize\(Policy\s*=\s*""Planning\.Edit""\)\]\s*\[HttpPost\]\s*public async Task<IActionResult>\s+Create", wbsController);
        Assert.Contains("HasClaim(\"permission\", \"ProjectPlan.Create\")", planService, StringComparison.Ordinal);
        Assert.Contains("if (!User.HasClaim(\"permission\", \"Planning.Edit\")", importController, StringComparison.Ordinal);
        Assert.Contains("!request.TargetPlanId.HasValue && !User.HasClaim(\"permission\", \"ProjectPlan.Create\")", importController, StringComparison.Ordinal);
        Assert.Contains("HasClaim(\"permission\", \"Planning.Edit\")", importService, StringComparison.Ordinal);
        Assert.Contains("HasClaim(\"permission\", \"ProjectPlan.Create\")", importService, StringComparison.Ordinal);
        Assert.Contains("HasClaim(\"permission\", \"Resource.Create\")", importService, StringComparison.Ordinal);
        Assert.Matches(@"\[Authorize\(Policy\s*=\s*""Planning\.Edit""\)\]\s*\[HttpPost\(""sessions""\)\]", importController);

        Assert.Contains("hasPermission(user.permissions, Permissions.ProjectPlan.Create)", planningUi, StringComparison.Ordinal);
        Assert.Contains("hasPermission(user.permissions, Permissions.Planning.Edit)", planningUi, StringComparison.Ordinal);
        Assert.Contains("{canCreateProjectPlan ? <Button", planningUi, StringComparison.Ordinal);
        Assert.Contains("editable={editable}", planningUi, StringComparison.Ordinal);
        Assert.Contains("canCreatePlan={canCreateProjectPlan}", planningUi, StringComparison.Ordinal);
        Assert.Contains("canEditPlan={canManagePlanning}", planningUi, StringComparison.Ordinal);
        Assert.Contains("canCreateResource={canCreateImportedResources}", planningUi, StringComparison.Ordinal);
        Assert.Contains("&& (!hasNewResourceResolution || canCreateResource)", importUi, StringComparison.Ordinal);
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

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var starts = new[]
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
            Path.GetDirectoryName(sourceFilePath)
        };

        foreach (var start in starts)
        {
            if (string.IsNullOrWhiteSpace(start)) continue;

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
    public void Default_permission_protection_is_empty_and_category_metadata_is_non_restrictive()
    {
        var policy = PermissionProtectionPolicy.Empty;

        Assert.Empty(policy.ProtectedPermissionKeys);
        Assert.All(PermissionCatalog.Entries.Where(entry => entry.IsActive), entry =>
        {
            Assert.True(policy.IsGrantable(entry), entry.Key);
            Assert.Equal(RoleCategories.Selectable, entry.AllowedCategories);
        });
    }
    [Fact]
    public void Document_category_permissions_are_granular_localized_and_grantable_to_all_tenant_roles()
    {
        var expected = new Dictionary<string, PermissionLevel>(StringComparer.Ordinal)
        {
            ["DocumentCategory.View"] = PermissionLevel.View,
            ["DocumentCategory.Create"] = PermissionLevel.Action,
            ["DocumentCategory.Edit"] = PermissionLevel.Action,
            ["DocumentCategory.ActivateDeactivate"] = PermissionLevel.Action
        };

        foreach (var (key, level) in expected)
        {
            var permission = PermissionCatalog.ByKey[key];
            Assert.True(permission.IsActive);
            Assert.True(permission.IsAssignable);
            Assert.Equal(PermissionScope.Tenant, permission.Scope);
            Assert.Equal(level, permission.Level);
            Assert.Equal("governance", permission.ModuleId);
            Assert.Equal("document-center-categories", permission.ScreenId);
            Assert.False(string.IsNullOrWhiteSpace(permission.LabelEn));
            Assert.False(string.IsNullOrWhiteSpace(permission.LabelAr));
            Assert.False(string.IsNullOrWhiteSpace(permission.DescriptionEn));
            Assert.False(string.IsNullOrWhiteSpace(permission.DescriptionAr));
            Assert.Equal(RoleCategories.Selectable, permission.AllowedCategories);
            Assert.False(PermissionProtectionPolicy.Empty.IsProtected(permission.Key));
            Assert.True(PermissionProtectionPolicy.Empty.IsGrantable(permission));

            if (level == PermissionLevel.Action)
                Assert.Contains("DocumentCategory.View", permission.RequiredPermissionKeys);
        }
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
        Assert.DoesNotContain("Issue.View", PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
        Assert.DoesNotContain("Plugin.View", PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
        Assert.True(PermissionProtectionPolicy.Empty.IsGrantable(plugin));
    }

    [Fact]
    public void Project_plan_create_is_separate_from_plan_content_edit_and_role_category_is_descriptive()
    {
        var create = PermissionCatalog.ByKey["ProjectPlan.Create"];
        var edit = PermissionCatalog.ByKey["Planning.Edit"];
        var view = PermissionCatalog.ByKey["Planning.View"];

        Assert.True(create.IsActive);
        Assert.True(create.IsAssignable);
        Assert.Equal(PermissionScope.Project, create.Scope);
        Assert.Equal(PermissionLevel.Action, create.Level);
        Assert.Contains("Planning.View", create.RequiredPermissionKeys);
        Assert.Equal(RoleCategories.Selectable, create.AllowedCategories);

        Assert.True(edit.IsActive);
        Assert.Equal(PermissionScope.Project, edit.Scope);
        Assert.Contains("Planning.View", edit.RequiredPermissionKeys);
        Assert.Equal(RoleCategories.Selectable, edit.AllowedCategories);

        Assert.False(string.IsNullOrWhiteSpace(create.LabelEn));
        Assert.False(string.IsNullOrWhiteSpace(create.LabelAr));
        Assert.False(string.IsNullOrWhiteSpace(create.DescriptionEn));
        Assert.False(string.IsNullOrWhiteSpace(create.DescriptionAr));

        Assert.Equal(PermissionLevel.View, view.Level);
        Assert.DoesNotContain("ProjectPlan.Create", PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
        Assert.DoesNotContain("Planning.Edit", PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
        Assert.DoesNotContain("Planning.View", PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(create.Key));
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(edit.Key));
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(view.Key));
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
            "ProjectAnnualBudget.ManageApprovalConfiguration", "ProjectAnnualBudget.ConfigureDraftApproval",
            "ProjectFinancialCeiling.ApproveChange"
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
        var configureDraftApproval = PermissionCatalog.ByKey["ProjectAnnualBudget.ConfigureDraftApproval"];

        Assert.Equal(PermissionScope.Project, view.Scope);
        Assert.Equal(PermissionLevel.View, view.Level);
        Assert.True(view.IsActive);
        Assert.True(view.IsAssignable);
        Assert.True(view.IsSensitive);
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(view.Key));

        Assert.Equal(PermissionScope.Project, manageApprovalConfiguration.Scope);
        Assert.Equal(PermissionLevel.Action, manageApprovalConfiguration.Level);
        Assert.True(manageApprovalConfiguration.IsActive);
        Assert.True(manageApprovalConfiguration.IsAssignable);
        Assert.True(manageApprovalConfiguration.IsSensitive);
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(manageApprovalConfiguration.Key));
        Assert.Contains(view.Key, manageApprovalConfiguration.RequiredPermissionKeys);

        Assert.Equal(PermissionScope.Project, configureDraftApproval.Scope);
        Assert.Equal(PermissionLevel.Action, configureDraftApproval.Level);
        Assert.True(configureDraftApproval.IsActive);
        Assert.True(configureDraftApproval.IsAssignable);
        Assert.True(configureDraftApproval.IsSensitive);
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(configureDraftApproval.Key));
        Assert.Contains(view.Key, configureDraftApproval.RequiredPermissionKeys);
        Assert.Contains("ProjectAnnualBudget.EditDraft", configureDraftApproval.RequiredPermissionKeys);

        Assert.DoesNotContain(view.Key, PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
        Assert.DoesNotContain(manageApprovalConfiguration.Key, PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
        Assert.DoesNotContain(configureDraftApproval.Key, PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);

        var projectAnnualBudgetKeys = new[]
        {
            "ProjectAnnualBudget.Approve", "ProjectAnnualBudget.Create", "ProjectAnnualBudget.EditDraft",
            "ProjectAnnualBudget.SubmitForReview", "ProjectAnnualBudget.ReviseApproved", "ProjectApproval.Override"
        };
        foreach (var key in projectAnnualBudgetKeys)
        {
            var permission = PermissionCatalog.ByKey[key];
            Assert.Equal(PermissionScope.Project, permission.Scope);
            Assert.False(PermissionProtectionPolicy.Empty.IsProtected(permission.Key));
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
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(foundation.Key));
        Assert.Empty(foundation.RequiredPermissionKeys);

        var export = PermissionCatalog.ByKey["CostUtilization.Export"];
        Assert.Equal(PermissionScope.Tenant, export.Scope);
        Assert.Equal(PermissionLevel.Action, export.Level);
        Assert.Equal("capacity", export.ModuleId);
        Assert.Equal("cost-utilization", export.ScreenId);
        Assert.True(export.IsAssignable);
        Assert.True(export.IsSensitive);
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(export.Key));
        Assert.Contains("Resource.View", export.RequiredPermissionKeys);

        Assert.DoesNotContain(foundation.Key, PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
        Assert.DoesNotContain(export.Key, PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
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
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(program.Key));
        Assert.Empty(program.RequiredPermissionKeys);

        var portfolio = PermissionCatalog.ByKey["PortfolioFinancialRollup.View"];
        Assert.Equal(PermissionScope.Portfolio, portfolio.Scope);
        Assert.Equal(PermissionLevel.View, portfolio.Level);
        Assert.Equal("financial-management", portfolio.ModuleId);
        Assert.Equal("portfolio-financial-rollup", portfolio.ScreenId);
        Assert.True(portfolio.IsAssignable);
        Assert.True(portfolio.IsSensitive);
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(portfolio.Key));
        Assert.Empty(portfolio.RequiredPermissionKeys);

        Assert.DoesNotContain("ProjectFinancialRollup.View", PermissionCatalog.ByKey.Keys);
        Assert.DoesNotContain(program.Key, PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
        Assert.DoesNotContain(portfolio.Key, PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
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
    public void Active_permission_dependency_graph_has_no_cycles()
    {
        var entries = PermissionCatalog.Entries
            .Where(item => item.IsActive)
            .ToDictionary(item => item.Key, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string key)
        {
            if (visited.Contains(key)) return;
            Assert.True(visiting.Add(key), $"Permission dependency cycle includes '{key}'.");

            foreach (var dependency in entries[key].RequiredPermissionKeys)
            {
                Assert.Contains(dependency, entries.Keys);
                Visit(dependency);
            }

            visiting.Remove(key);
            visited.Add(key);
        }

        foreach (var key in entries.Keys) Visit(key);
    }

    [Fact]
    public void Active_actions_require_their_screen_view_except_assigned_review_actions()
    {
        var entries = PermissionCatalog.Entries.Where(item => item.IsActive).ToDictionary(item => item.Key, StringComparer.Ordinal);

        foreach (var action in entries.Values.Where(item => item.Level == PermissionLevel.Action
            && !AssignedReviewExceptions.Contains(item.Key)
            && !CrossScreenViewExceptions.Contains(item.Key)))
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
        Assert.DoesNotContain("ProjectAnnualBudget.Approve", reviewerGrant);
        var completeReviewerGrant = PermissionCatalog.EnforceDependencies([
            "ProjectAnnualBudget.Approve", "ProjectAnnualBudget.View"]);
        Assert.Contains("ProjectAnnualBudget.Approve", completeReviewerGrant);
        Assert.Contains("ProjectAnnualBudget.View", completeReviewerGrant);

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


