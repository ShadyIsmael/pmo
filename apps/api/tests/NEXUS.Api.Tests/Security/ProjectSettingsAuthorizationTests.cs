using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Api.Finance;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Api;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProjectSettingsAuthorizationTests
{
    [Fact]
    public void Project_settings_catalog_entries_are_canonical_project_scoped_and_separate_from_sensitive_configuration()
    {
        var view = PermissionCatalog.ByKey["ProjectSettings.View"];
        var edit = PermissionCatalog.ByKey["ProjectSettings.Edit"];

        Assert.Equal(PermissionScope.Project, view.Scope);
        Assert.Equal(PermissionScope.Project, edit.Scope);
        Assert.Equal(PermissionLevel.View, view.Level);
        Assert.Equal(PermissionLevel.Action, edit.Level);
        Assert.Equal("projects", view.ModuleId);
        Assert.Equal("settings", view.ScreenId);
        Assert.Contains("ProjectSettings.View", edit.RequiredPermissionKeys);
        Assert.False(view.IsTenantAdminBaseline);
        Assert.False(edit.IsTenantAdminBaseline);
        Assert.False(string.IsNullOrWhiteSpace(view.LabelEn));
        Assert.False(string.IsNullOrWhiteSpace(view.LabelAr));
        Assert.False(string.IsNullOrWhiteSpace(view.DescriptionEn));
        Assert.False(string.IsNullOrWhiteSpace(view.DescriptionAr));
        Assert.Contains("ProjectAnnualBudget.ManageApprovalConfiguration", PermissionCatalog.ByKey.Keys);
    }

    [Fact]
    public void Workflow_settings_controller_requires_view_for_reads_and_both_view_and_edit_for_writes()
    {
        Assert.Equal("ProjectSettings.View", typeof(ProjectWorkflowController)
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy);

        var save = typeof(ProjectWorkflowController).GetMethod(nameof(ProjectWorkflowController.Save));
        Assert.NotNull(save);
        Assert.Equal("ProjectSettings.Edit", save!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Contains(save.GetCustomAttributes<HttpPutAttribute>(), attribute => attribute.Template == "{domain}");
    }

    [Fact]
    public void Financial_settings_endpoints_keep_settings_and_operation_specific_policies()
    {
        AssertPolicies(typeof(ProjectAnnualBudgetController), nameof(ProjectAnnualBudgetController.GetApprovalConfiguration),
            "ProjectSettings.View", "ProjectAnnualBudget.View");
        AssertPolicies(typeof(ProjectAnnualBudgetController), nameof(ProjectAnnualBudgetController.SaveApprovalConfiguration),
            "ProjectSettings.View", "ProjectSettings.Edit", "ProjectAnnualBudget.ManageApprovalConfiguration");
        AssertPolicies(typeof(ProjectFinancialFoundationController), nameof(ProjectFinancialFoundationController.Get),
            "ProjectSettings.View", "ProjectFinancialFoundation.View");
        AssertPolicies(typeof(ProjectFinancialFoundationController), nameof(ProjectFinancialFoundationController.UpdateApprovalConfiguration),
            "ProjectSettings.View", "ProjectSettings.Edit", "ProjectFinancialFoundation.View", "ProjectAnnualBudget.ManageApprovalConfiguration");
    }

    private static void AssertPolicies(Type controllerType, string actionName, params string[] requiredPolicies)
    {
        var action = controllerType.GetMethod(actionName);
        Assert.NotNull(action);
        var policies = action!.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var requiredPolicy in requiredPolicies)
            Assert.Contains(requiredPolicy, policies);
    }

    [Fact]
    public void Project_annual_budget_list_keeps_its_own_view_permission()
    {
        var method = typeof(ProjectAnnualBudgetController).GetMethod(nameof(ProjectAnnualBudgetController.GetPage));
        Assert.NotNull(method);
        Assert.Equal("ProjectAnnualBudget.View", method!.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.DoesNotContain(method.GetCustomAttributes<AuthorizeAttribute>(), attribute => attribute.Policy == "ProjectSettings.View");
    }

    [Fact]
    public async Task Scoped_viewer_can_read_project_workflow_settings()
    {
        var projectId = Guid.NewGuid();
        await using var context = CreateContext();
        var service = CreateService(context, projectId, ["ProjectSettings.View"]);

        var result = await service.GetAsync(projectId);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.Domains.Count);
    }

    [Fact]
    public async Task Project_settings_editor_can_save_only_inside_the_authorized_project_scope()
    {
        var projectId = Guid.NewGuid();
        await using var context = CreateContext();
        var service = CreateService(context, projectId, ["ProjectSettings.View", "ProjectSettings.Edit"]);

        var result = await service.SaveAsync(projectId, "Task", new SaveProjectWorkflowConfigurationRequest(), "settings-editor");

        Assert.True(result.IsSuccess);
        Assert.Equal(projectId, result.Value!.ProjectId);
        Assert.Equal(1, await context.Set<NEXUS.Modules.WorkflowEngine.Domain.ProjectWorkflowConfiguration>().CountAsync());
    }

    [Fact]
    public async Task Project_edit_does_not_substitute_for_project_settings_edit()
    {
        var projectId = Guid.NewGuid();
        await using var context = CreateContext();
        var service = CreateService(context, projectId, ["ProjectSettings.View", "Project.Edit"]);

        var result = await service.SaveAsync(projectId, "Task", new SaveProjectWorkflowConfigurationRequest(), "project-editor");

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Empty(context.ChangeTracker.Entries<NEXUS.Modules.WorkflowEngine.Domain.ProjectWorkflowConfiguration>());
    }

    [Fact]
    public async Task Project_settings_edit_without_view_is_rejected_by_the_service_dependency()
    {
        var projectId = Guid.NewGuid();
        await using var context = CreateContext();
        var service = CreateService(context, projectId, ["ProjectSettings.Edit"]);

        var result = await service.SaveAsync(projectId, "Task", new SaveProjectWorkflowConfigurationRequest(), "editor-without-view");

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Empty(context.ChangeTracker.Entries<NEXUS.Modules.WorkflowEngine.Domain.ProjectWorkflowConfiguration>());
    }

    [Fact]
    public async Task Custom_project_workflow_requires_workflow_view_and_rejects_reporting_domain()
    {
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var request = new SaveProjectWorkflowConfigurationRequest { Mode = ProjectWorkflowModes.Custom, WorkflowDefinitionId = Guid.NewGuid() };

        var withoutWorkflowView = CreateService(context, projectId, ["ProjectSettings.View", "ProjectSettings.Edit"], tenantId: tenantId);
        var denied = await withoutWorkflowView.SaveAsync(projectId, "Task", request, "settings-editor");
        Assert.True(denied.IsFailure);
        Assert.Equal(403, denied.Error?.StatusCode);

        var withWorkflowView = CreateService(context, projectId, ["ProjectSettings.View", "ProjectSettings.Edit", "Workflow.View"], tenantId: tenantId);
        var unsupportedDomain = await withWorkflowView.SaveAsync(projectId, "Reporting", new SaveProjectWorkflowConfigurationRequest(), "settings-editor");
        Assert.True(unsupportedDomain.IsFailure);
        Assert.Equal(400, unsupportedDomain.Error?.StatusCode);
        Assert.Empty(context.ChangeTracker.Entries<ProjectWorkflowConfiguration>());
    }

    [Fact]
    public async Task Project_workflow_configuration_saves_reloads_and_applies_published_workflow_for_new_records()
    {
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        await using var context = CreateContext();
        context.AddRange(
            new WorkflowDefinition
            {
                Id = definitionId, TenantId = tenantId, Code = "TASK-APPROVAL", Name = "Task approval",
                Domain = WorkflowDomains.Task, EntityType = WorkflowDomains.EntityTypeFor(WorkflowDomains.Task),
                IsActive = true, ActivePublishedVersionId = versionId
            },
            new WorkflowDefinitionVersion
            {
                Id = versionId, TenantId = tenantId, WorkflowDefinitionId = definitionId, VersionNumber = 1,
                LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Published
            },
            new WorkflowState
            {
                TenantId = tenantId, WorkflowDefinitionVersionId = versionId, Code = "Draft", NameEn = "Draft",
                NameAr = "مسودة", IsInitial = true, IsActive = true
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, projectId, ["ProjectSettings.View", "ProjectSettings.Edit", "Workflow.View"], tenantId: tenantId);
        var saved = await service.SaveAsync(projectId, "Task", new SaveProjectWorkflowConfigurationRequest
        {
            Mode = ProjectWorkflowModes.Custom, WorkflowDefinitionId = definitionId
        }, "settings-editor");

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal(ProjectWorkflowModes.Custom, saved.Value!.Mode);
        Assert.Equal(versionId, saved.Value.EffectiveWorkflowVersionId);

        var reloaded = await service.GetAsync(projectId);
        Assert.True(reloaded.IsSuccess);
        var taskSettings = Assert.Single(reloaded.Value!.Domains, item => item.Domain == WorkflowDomains.Task);
        Assert.Equal(definitionId, taskSettings.WorkflowDefinitionId);
        Assert.Equal(versionId, taskSettings.EffectiveWorkflowVersionId);

        var resolved = await service.ResolveForNewRecordAsync(
            projectId, WorkflowDomains.Task, WorkflowDomains.EntityTypeFor(WorkflowDomains.Task),
            new WorkflowExecutionContext(tenantId, "creator"));
        Assert.True(resolved.IsSuccess);
        Assert.Equal(definitionId, resolved.Value!.WorkflowDefinitionId);
        Assert.Equal(versionId, resolved.Value.WorkflowVersionId);
        Assert.Equal("Draft", resolved.Value.InitialStateCode);
    }

    [Fact]
    public async Task Project_workflow_definition_and_execution_must_stay_inside_authenticated_tenant()
    {
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        await using var context = CreateContext();
        context.AddRange(
            new WorkflowDefinition
            {
                Id = definitionId, TenantId = foreignTenantId, Code = "FOREIGN", Name = "Foreign workflow",
                Domain = WorkflowDomains.Task, EntityType = WorkflowDomains.EntityTypeFor(WorkflowDomains.Task),
                IsActive = true, ActivePublishedVersionId = versionId
            },
            new WorkflowDefinitionVersion
            {
                Id = versionId, TenantId = foreignTenantId, WorkflowDefinitionId = definitionId, VersionNumber = 1,
                LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Published
            },
            new WorkflowState
            {
                TenantId = foreignTenantId, WorkflowDefinitionVersionId = versionId, Code = "Draft", NameEn = "Draft",
                NameAr = "مسودة", IsInitial = true, IsActive = true
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, projectId, ["ProjectSettings.View", "ProjectSettings.Edit", "Workflow.View"], tenantId: tenantId);
        var save = await service.SaveAsync(projectId, WorkflowDomains.Task, new SaveProjectWorkflowConfigurationRequest
        {
            Mode = ProjectWorkflowModes.Custom, WorkflowDefinitionId = definitionId
        }, "settings-editor");
        var resolveWrongTenant = await service.ResolveForNewRecordAsync(
            projectId, WorkflowDomains.Task, WorkflowDomains.EntityTypeFor(WorkflowDomains.Task),
            new WorkflowExecutionContext(foreignTenantId, "creator"));

        Assert.True(save.IsFailure);
        Assert.Equal(404, save.Error?.StatusCode);
        Assert.True(resolveWrongTenant.IsFailure);
        Assert.Equal(403, resolveWrongTenant.Error?.StatusCode);
        Assert.Empty(await context.Set<ProjectWorkflowConfiguration>().ToListAsync());
    }

    [Fact]
    public async Task Explicit_deny_removes_settings_access_even_when_a_role_grants_it()
    {
        var projectId = Guid.NewGuid();
        var effective = EffectivePermissionResolver.Resolve(
            ["ProjectSettings.View", "ProjectSettings.Edit"],
            [("ProjectSettings.View", PermissionOverrideEffect.Deny), ("ProjectSettings.Edit", PermissionOverrideEffect.Deny)]);
        Assert.DoesNotContain("ProjectSettings.View", effective);
        Assert.DoesNotContain("ProjectSettings.Edit", effective);

        await using var context = CreateContext();
        var service = CreateService(context, projectId, effective);
        var read = await service.GetAsync(projectId);
        var save = await service.SaveAsync(projectId, "Task", new SaveProjectWorkflowConfigurationRequest(), "denied-editor");

        Assert.True(read.IsFailure);
        Assert.Equal(403, read.Error?.StatusCode);
        Assert.True(save.IsFailure);
        Assert.Equal(403, save.Error?.StatusCode);
        Assert.Empty(context.ChangeTracker.Entries<NEXUS.Modules.WorkflowEngine.Domain.ProjectWorkflowConfiguration>());
    }

    [Fact]
    public async Task Project_settings_permission_does_not_bypass_management_scope()
    {
        var requestedProjectId = Guid.NewGuid();
        var inScopeProjectId = Guid.NewGuid();
        await using var context = CreateContext();
        var service = CreateService(context, requestedProjectId, ["ProjectSettings.View", "ProjectSettings.Edit"], new HashSet<Guid> { inScopeProjectId });

        var read = await service.GetAsync(requestedProjectId);
        var save = await service.SaveAsync(requestedProjectId, "Task", new SaveProjectWorkflowConfigurationRequest(), "out-of-scope-editor");

        Assert.True(read.IsFailure);
        Assert.Equal(403, read.Error?.StatusCode);
        Assert.True(save.IsFailure);
        Assert.Equal(403, save.Error?.StatusCode);
        Assert.Empty(context.ChangeTracker.Entries<NEXUS.Modules.WorkflowEngine.Domain.ProjectWorkflowConfiguration>());
    }

    [Fact]
    public async Task Annual_budget_approval_configuration_service_requires_settings_and_financial_permissions()
    {
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var context = CreateContext();
        var dto = new SaveProjectAnnualBudgetApprovalConfigurationDto("Sequential", [], null, []);

        var noSettingsView = CreateAnnualBudgetWorkflow(context, projectId, tenantId, actorId,
            ["ProjectAnnualBudget.View"]);
        var readWithoutSettingsView = await noSettingsView.GetApprovalConfigurationAsync(tenantId, projectId, actorId, CancellationToken.None);
        Assert.Equal(403, readWithoutSettingsView.Error?.StatusCode);

        var noSettingsEdit = CreateAnnualBudgetWorkflow(context, projectId, tenantId, actorId,
            ["ProjectSettings.View", "ProjectAnnualBudget.View", "ProjectAnnualBudget.ManageApprovalConfiguration"]);
        var saveWithoutSettingsEdit = await noSettingsEdit.SaveApprovalConfigurationAsync(tenantId, projectId, actorId, "actor", dto, CancellationToken.None);
        Assert.Equal(403, saveWithoutSettingsEdit.Error?.StatusCode);

        var noSensitivePermission = CreateAnnualBudgetWorkflow(context, projectId, tenantId, actorId,
            ["ProjectSettings.View", "ProjectSettings.Edit", "ProjectAnnualBudget.View"]);
        var saveWithoutSensitivePermission = await noSensitivePermission.SaveApprovalConfigurationAsync(tenantId, projectId, actorId, "actor", dto, CancellationToken.None);
        Assert.Equal(403, saveWithoutSensitivePermission.Error?.StatusCode);

        var deniedEdit = EffectivePermissionResolver.Resolve(
            ["ProjectSettings.View", "ProjectSettings.Edit", "ProjectAnnualBudget.View", "ProjectAnnualBudget.ManageApprovalConfiguration"],
            [("ProjectSettings.Edit", PermissionOverrideEffect.Deny)]);
        var explicitDeny = CreateAnnualBudgetWorkflow(context, projectId, tenantId, actorId, deniedEdit);
        var saveWithDeniedEdit = await explicitDeny.SaveApprovalConfigurationAsync(tenantId, projectId, actorId, "actor", dto, CancellationToken.None);
        Assert.Equal(403, saveWithDeniedEdit.Error?.StatusCode);

        var fullyAuthorized = CreateAnnualBudgetWorkflow(context, projectId, tenantId, actorId,
            ["ProjectSettings.View", "ProjectSettings.Edit", "ProjectAnnualBudget.View", "ProjectAnnualBudget.ManageApprovalConfiguration"]);
        var saveWithAllPermissions = await fullyAuthorized.SaveApprovalConfigurationAsync(tenantId, projectId, actorId, "actor", dto, CancellationToken.None);
        Assert.Equal(400, saveWithAllPermissions.Error?.StatusCode);
        Assert.Contains("Project approval settings", saveWithAllPermissions.Error?.Message);
    }

    [Fact]
    public async Task Annual_budget_approval_configuration_respects_active_deny_and_tenant_project_scope()
    {
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var context = CreateContext();
        context.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            Name = "Scoped approval project",
            FinanceApprovalMode = "Both"
        });
        await context.SaveChangesAsync();

        var granted = new[]
        {
            "ProjectSettings.View", "ProjectSettings.Edit", "ProjectAnnualBudget.View",
            "ProjectAnnualBudget.ManageApprovalConfiguration"
        };
        var deniedManage = EffectivePermissionResolver.Resolve(
            granted,
            [("ProjectAnnualBudget.ManageApprovalConfiguration", PermissionOverrideEffect.Deny)]);
        var deniedService = CreateAnnualBudgetWorkflow(context, projectId, tenantId, actorId, deniedManage);
        var save = await deniedService.SaveApprovalConfigurationAsync(
            tenantId,
            projectId,
            actorId,
            "denied editor",
            new SaveProjectAnnualBudgetApprovalConfigurationDto("Sequential", [], null, []),
            CancellationToken.None);
        Assert.Equal(403, save.Error?.StatusCode);

        var outsideProjectScope = CreateAnnualBudgetWorkflow(
            context, projectId, tenantId, actorId, granted,
            scopedProjectIds: new HashSet<Guid> { Guid.NewGuid() });
        var outOfScopeRead = await outsideProjectScope.GetApprovalConfigurationAsync(
            tenantId, projectId, actorId, CancellationToken.None);
        var outOfScopeSave = await outsideProjectScope.SaveApprovalConfigurationAsync(
            tenantId,
            projectId,
            actorId,
            "out of scope editor",
            new SaveProjectAnnualBudgetApprovalConfigurationDto("Sequential", [], null, []),
            CancellationToken.None);
        Assert.Equal(404, outOfScopeRead.Error?.StatusCode);
        Assert.Equal(404, outOfScopeSave.Error?.StatusCode);

        var authorized = CreateAnnualBudgetWorkflow(context, projectId, tenantId, actorId, granted);
        var foreignTenantRead = await authorized.GetApprovalConfigurationAsync(
            Guid.NewGuid(), projectId, actorId, CancellationToken.None);
        Assert.Equal(404, foreignTenantRead.Error?.StatusCode);
    }

    [Fact]
    public async Task Settings_viewer_can_load_scoped_approver_choices_without_submit_or_users_view_permissions()
    {
        var projectId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        await using var context = CreateContext();
        context.Add(new Project
        {
            Id = projectId,
            TenantId = tenantId,
            Name = "Scoped project",
            FinanceApprovalMode = "Both"
        });
        context.Add(new Resource
        {
            Id = resourceId,
            TenantId = tenantId,
            UserId = approverId,
            Name = "Eligible reviewer",
            IsActive = true
        });
        var approver = new User
        {
            Id = approverId,
            TenantId = tenantId,
            Email = "reviewer@example.test",
            FirstName = "Eligible",
            LastName = "Reviewer",
            IsActive = true
        };
        var approverRole = new Role
        {
            TenantId = tenantId,
            Name = "Project Management Reviewer",
            Category = "Management"
        };
        var today = DateTime.UtcNow.Date;
        context.AddRange(approver, approverRole, new UserRole
        {
            TenantId = tenantId,
            UserId = approverId,
            User = approver,
            RoleId = approverRole.Id,
            Role = approverRole
        }, new ProjectMember
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ResourceId = resourceId,
            Role = "Finance Reviewer",
            Status = "Active",
            StartDate = today.AddDays(-1),
            EndDate = today.AddDays(1)
        });
        await context.SaveChangesAsync();

        var permissionService = new Mock<IUserEffectivePermissionService>();
        permissionService.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["ProjectSettings.View", "ProjectAnnualBudget.View"], StringComparer.Ordinal));
        permissionService.Setup(service => service.GetEffectivePermissionsAsync(approverId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["ProjectAnnualBudget.Approve"], StringComparer.Ordinal));
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        var workflow = new ProjectAnnualBudgetWorkflow(
            context,
            Mock.Of<ITenantFiscalYearService>(),
            Mock.Of<IFiscalYearCalendarService>(),
            permissionService.Object,
            management,
            Mock.Of<IAuditService>(),
            Mock.Of<IProjectAnnualBudgetApprovalNotificationPublisher>(),
            Mock.Of<IManagementScopeResolver>());

        var result = await workflow.GetApprovalConfigurationAsync(tenantId, projectId, actorId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value!.EligibleApprovers, candidate => candidate.UserId == approverId);
    }

    [Fact]
    public async Task Saved_annual_budget_defaults_reload_and_are_snapshotted_for_a_new_submission()
    {
        var databaseName = $"project-settings-{Guid.NewGuid():N}";
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var firstApproverId = Guid.NewGuid();
        var secondApproverId = Guid.NewGuid();
        var firstResourceId = Guid.NewGuid();
        var secondResourceId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        await using (var context = CreateContext(databaseName, ignoreTransactionWarning: true))
        {
            var project = new Project
            {
                Id = projectId,
                TenantId = tenantId,
                Name = "Defaults persistence project",
                Code = "DEFAULTS",
                Budget = 50_000m,
                Currency = "EGP",
                FinanceApprovalMode = "Both",
                RowVersion = [1]
            };
            var annualBudget = new ProjectAnnualBudget
            {
                Id = budgetId,
                TenantId = tenantId,
                ProjectId = projectId,
                FiscalYear = today.Year,
                FiscalYearStartDate = new DateOnly(today.Year, 1, 1),
                FiscalYearEndDate = new DateOnly(today.Year, 12, 31),
                FinancialReference = "DEFAULTS-FY-DRAFT",
                Currency = "EGP",
                NewApprovedFunding = 5_000m,
                Status = ProjectAnnualBudgetStatuses.Draft,
                RowVersion = [2]
            };
            var firstResource = new Resource
            {
                Id = firstResourceId,
                TenantId = tenantId,
                UserId = firstApproverId,
                Name = "First reviewer",
                Type = "Employee",
                Status = "Active",
                IsActive = true
            };
            var secondResource = new Resource
            {
                Id = secondResourceId,
                TenantId = tenantId,
                UserId = secondApproverId,
                Name = "Second reviewer",
                Type = "Employee",
                Status = "Active",
                IsActive = true
            };
            var firstApprover = new User
            {
                Id = firstApproverId,
                TenantId = tenantId,
                Email = "first@example.test",
                FirstName = "First",
                LastName = "Reviewer"
            };
            var secondApprover = new User
            {
                Id = secondApproverId,
                TenantId = tenantId,
                Email = "second@example.test",
                FirstName = "Second",
                LastName = "Reviewer"
            };
            var managementRole = new Role
            {
                TenantId = tenantId,
                Name = "Project Management Reviewer",
                Category = "Management"
            };

            context.AddRange(
                project,
                annualBudget,
                firstResource,
                secondResource,
                firstApprover,
                secondApprover,
                managementRole,
                new UserRole
                {
                    TenantId = tenantId,
                    UserId = firstApproverId,
                    User = firstApprover,
                    RoleId = managementRole.Id,
                    Role = managementRole
                },
                new UserRole
                {
                    TenantId = tenantId,
                    UserId = secondApproverId,
                    User = secondApprover,
                    RoleId = managementRole.Id,
                    Role = managementRole
                },
                new ProjectMember
                {
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ResourceId = firstResourceId,
                    Role = "Finance Reviewer",
                    Status = "Active",
                    StartDate = today.AddDays(-1),
                    EndDate = today.AddDays(1)
                },
                new ProjectMember
                {
                    TenantId = tenantId,
                    ProjectId = projectId,
                    ResourceId = secondResourceId,
                    Role = "Finance Reviewer",
                    Status = "Active",
                    StartDate = today.AddDays(-1),
                    EndDate = today.AddDays(1)
                });
            await context.SaveChangesAsync();
        }

        var actorPermissions = new HashSet<string>(
        [
            "ProjectSettings.View", "ProjectSettings.Edit", "ProjectAnnualBudget.View",
            "ProjectAnnualBudget.ManageApprovalConfiguration", "ProjectAnnualBudget.SubmitForReview"
        ], StringComparer.Ordinal);
        var permissionService = new Mock<IUserEffectivePermissionService>();
        permissionService.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actorPermissions);
        permissionService.Setup(service => service.GetEffectivePermissionsAsync(firstApproverId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["ProjectAnnualBudget.Approve"], StringComparer.Ordinal));
        permissionService.Setup(service => service.GetEffectivePermissionsAsync(secondApproverId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["ProjectAnnualBudget.Approve"], StringComparer.Ordinal));

        var auditEntries = new List<AuditEntry>();
        var auditService = new Mock<IAuditService>();
        auditService.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => auditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var tenantFiscalYear = new Mock<ITenantFiscalYearService>();
        var currentPeriod = new FiscalYearCalendarService().ForFiscalYear(today.Year, 1);
        tenantFiscalYear.Setup(service => service.ResolveForTimestampAsync(tenantId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentPeriod);
        tenantFiscalYear.Setup(service => service.GetStartMonthAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        ProjectAnnualBudgetApprovalConfigurationDto reloadedConfiguration;
        await using (var context = CreateContext(databaseName, ignoreTransactionWarning: true))
        {
            var workflow = CreateAnnualBudgetWorkflow(context, projectId, tenantId, actorId,
                actorPermissions, permissionService.Object, auditService.Object, tenantFiscalYear.Object);
            var project = await context.Projects.IgnoreQueryFilters().SingleAsync(item => item.Id == projectId);
            var saved = await workflow.SaveApprovalConfigurationAsync(
                tenantId,
                projectId,
                actorId,
                "Settings Admin",
                new SaveProjectAnnualBudgetApprovalConfigurationDto(
                    "Sequential", [firstApproverId, secondApproverId], null, project.RowVersion),
                CancellationToken.None);

            Assert.True(saved.IsSuccess, saved.Error?.Message);
            Assert.Equal("Sequential", saved.Value!.ApprovalMode);
            Assert.Equal([firstApproverId, secondApproverId], saved.Value.ConfiguredApprovers.Select(item => item.UserId));
            Assert.Equal([1, 2], saved.Value.ConfiguredApprovers.Select(item => item.Position));
            Assert.All(saved.Value.ConfiguredApprovers, item => Assert.True(item.IsEligible));

            // EF InMemory does not emulate SQL Server rowversion generation; seed a stable token for the next service call.
            var storedConfiguration = await context.Set<ProjectAnnualBudgetApprovalConfiguration>()
                .IgnoreQueryFilters()
                .SingleAsync(item => item.ProjectId == projectId);
            if (storedConfiguration.RowVersion.Length == 0)
            {
                storedConfiguration.RowVersion = [3];
                await context.SaveChangesAsync();
            }
        }

        await using (var context = CreateContext(databaseName, ignoreTransactionWarning: true))
        {
            var workflow = CreateAnnualBudgetWorkflow(context, projectId, tenantId, actorId,
                actorPermissions, permissionService.Object, auditService.Object, tenantFiscalYear.Object);
            var loaded = await workflow.GetApprovalConfigurationAsync(tenantId, projectId, actorId, CancellationToken.None);

            Assert.True(loaded.IsSuccess, loaded.Error?.Message);
            reloadedConfiguration = loaded.Value!;
            Assert.Equal("Sequential", reloadedConfiguration.ApprovalMode);
            Assert.Equal([firstApproverId, secondApproverId], reloadedConfiguration.ConfiguredApprovers.Select(item => item.UserId));
            Assert.Equal([1, 2], reloadedConfiguration.ConfiguredApprovers.Select(item => item.Position));
            Assert.All(reloadedConfiguration.ConfiguredApprovers, item => Assert.True(item.IsEligible));
            Assert.NotEmpty(reloadedConfiguration.RowVersion!);

            var draft = await context.ProjectAnnualBudgets.IgnoreQueryFilters().SingleAsync(item => item.Id == budgetId);
            var submitted = await workflow.SubmitForReviewAsync(
                tenantId,
                projectId,
                budgetId,
                actorId,
                "Settings Admin",
                new SubmitProjectAnnualBudgetReviewDto(
                    null,
                    null,
                    draft.RowVersion,
                    ApprovalConfigurationRowVersion: reloadedConfiguration.RowVersion,
                    ProjectSettingsRowVersion: reloadedConfiguration.ProjectSettingsRowVersion),
                CancellationToken.None);

            Assert.True(submitted.IsSuccess, submitted.Error?.Message);

            var approval = await context.Set<ApprovalRequest>().IgnoreQueryFilters()
                .SingleAsync(item => item.EntityId == budgetId && item.Type == "ProjectAnnualBudgetReview");
            Assert.Equal("Pending", approval.Status);
            Assert.Equal(firstApproverId, approval.CurrentApproverId);

            var assignments = await context.Set<PaymentRequestApproverAssignment>().IgnoreQueryFilters()
                .Where(item => item.ApprovalRequestId == approval.Id)
                .OrderBy(item => item.Position)
                .ToListAsync();
            Assert.Equal([firstApproverId, secondApproverId], assignments.Select(item => item.ApproverUserId));
            Assert.Equal([1, 2], assignments.Select(item => item.Position));
            Assert.All(assignments, item => Assert.Equal(PaymentRequestAssignmentRequirements.Mandatory, item.Requirement));
        }

        var overrideBudgetId = Guid.NewGuid();
        await using (var context = CreateContext(databaseName, ignoreTransactionWarning: true))
        {
            context.ProjectAnnualBudgets.Add(new ProjectAnnualBudget
            {
                Id = overrideBudgetId,
                TenantId = tenantId,
                ProjectId = projectId,
                FiscalYear = today.Year + 1,
                FiscalYearStartDate = new DateOnly(today.Year + 1, 1, 1),
                FiscalYearEndDate = new DateOnly(today.Year + 1, 12, 31),
                FinancialReference = "DEFAULTS-FY-OVERRIDE",
                Currency = "EGP",
                NewApprovedFunding = 4_000m,
                Status = ProjectAnnualBudgetStatuses.Draft,
                RowVersion = [4]
            });
            await context.SaveChangesAsync();
        }

        const string overrideReason = "Temporary review route for this submission";
        await using (var context = CreateContext(databaseName, ignoreTransactionWarning: true))
        {
            var workflow = CreateAnnualBudgetWorkflow(context, projectId, tenantId, actorId,
                actorPermissions, permissionService.Object, auditService.Object, tenantFiscalYear.Object);
            var draft = await context.ProjectAnnualBudgets.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == overrideBudgetId);
            var unauthorized = await workflow.SubmitForReviewAsync(
                tenantId,
                projectId,
                overrideBudgetId,
                actorId,
                "Settings Admin",
                new SubmitProjectAnnualBudgetReviewDto(
                    "Parallel",
                    [
                        new PaymentRequestApproverSelectionDto(firstApproverId, null, PaymentRequestApproverRequirements.Mandatory),
                        new PaymentRequestApproverSelectionDto(secondApproverId, null, PaymentRequestApproverRequirements.Mandatory)
                    ],
                    draft.RowVersion,
                    CustomizeForThisSubmission: true,
                    OverrideReason: overrideReason,
                    ApprovalConfigurationRowVersion: reloadedConfiguration.RowVersion,
                    ProjectSettingsRowVersion: reloadedConfiguration.ProjectSettingsRowVersion),
                CancellationToken.None);
            Assert.Equal(403, unauthorized.Error?.StatusCode);
            Assert.False(await context.ApprovalRequests.IgnoreQueryFilters()
                .AnyAsync(item => item.EntityId == overrideBudgetId));

            actorPermissions.Add("ProjectApproval.Override");
            var authorizedWorkflow = CreateAnnualBudgetWorkflow(context, projectId, tenantId, actorId,
                actorPermissions, permissionService.Object, auditService.Object, tenantFiscalYear.Object);
            var authorized = await authorizedWorkflow.SubmitForReviewAsync(
                tenantId,
                projectId,
                overrideBudgetId,
                actorId,
                "Settings Admin",
                new SubmitProjectAnnualBudgetReviewDto(
                    "Parallel",
                    [
                        new PaymentRequestApproverSelectionDto(firstApproverId, null, PaymentRequestApproverRequirements.Mandatory),
                        new PaymentRequestApproverSelectionDto(secondApproverId, null, PaymentRequestApproverRequirements.Mandatory)
                    ],
                    draft.RowVersion,
                    CustomizeForThisSubmission: true,
                    OverrideReason: overrideReason,
                    ApprovalConfigurationRowVersion: reloadedConfiguration.RowVersion,
                    ProjectSettingsRowVersion: reloadedConfiguration.ProjectSettingsRowVersion),
                CancellationToken.None);
            Assert.True(authorized.IsSuccess, authorized.Error?.Message);

            var loadedDefaults = await authorizedWorkflow.GetApprovalConfigurationAsync(
                tenantId, projectId, actorId, CancellationToken.None);
            Assert.True(loadedDefaults.IsSuccess, loadedDefaults.Error?.Message);
            Assert.Equal("Sequential", loadedDefaults.Value!.ApprovalMode);
            Assert.Equal([1, 2], loadedDefaults.Value.ConfiguredApprovers.Select(item => item.Position));

            var overrideApproval = await context.ApprovalRequests.IgnoreQueryFilters()
                .SingleAsync(item => item.EntityId == overrideBudgetId && item.Type == "ProjectAnnualBudgetReview");
            Assert.Equal("Pending", overrideApproval.Status);
            Assert.Null(overrideApproval.CurrentApproverId);
            var overrideAssignments = await context.PaymentRequestApproverAssignments.IgnoreQueryFilters()
                .Where(item => item.ApprovalRequestId == overrideApproval.Id)
                .ToListAsync();
            Assert.Equal(2, overrideAssignments.Count);
            Assert.All(overrideAssignments, item => Assert.Null(item.Position));
            Assert.All(overrideAssignments, item => Assert.Equal(PaymentRequestAssignmentRequirements.Mandatory, item.Requirement));
        }

        var submissionAudit = Assert.Single(auditEntries,
            entry => entry.Action == "SubmittedForReview" && entry.EntityId == budgetId);
        using var snapshot = JsonDocument.Parse(submissionAudit.NewValues!);
        var root = snapshot.RootElement;
        Assert.Equal("ProjectSettings", root.GetProperty("approvalConfigurationSource").GetString());
        var effectivePlan = root.GetProperty("effectiveApprovalPlan");
        Assert.Equal("ProjectSettings", effectivePlan.GetProperty("ConfigurationSource").GetString());
        Assert.Equal([firstApproverId, secondApproverId], effectivePlan.GetProperty("Approvers")
            .EnumerateArray().Select(item => item.GetProperty("UserId").GetGuid()));

        var overrideAudit = Assert.Single(auditEntries,
            entry => entry.Action == "SubmittedForReview" && entry.EntityId == overrideBudgetId);
        using var overrideSnapshot = JsonDocument.Parse(overrideAudit.NewValues!);
        var overrideRoot = overrideSnapshot.RootElement;
        Assert.Equal("Sequential", overrideRoot.GetProperty("defaultApprovalPlan").GetProperty("ApprovalMode").GetString());
        Assert.Equal("SubmissionOverride", overrideRoot.GetProperty("effectiveApprovalPlan").GetProperty("ConfigurationSource").GetString());
        Assert.Equal("Parallel", overrideRoot.GetProperty("effectiveApprovalPlan").GetProperty("ApprovalMode").GetString());
        Assert.Equal(overrideReason, overrideRoot.GetProperty("override").GetProperty("reason").GetString());
    }

    private static AppDbContext CreateContext(string? databaseName = null, bool ignoreTransactionWarning = false)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString());
        if (ignoreTransactionWarning)
            options.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        return new AppDbContext(options.Options);
    }

    private static ProjectWorkflowConfigurationService CreateService(
        AppDbContext context,
        Guid requestedProjectId,
        IEnumerable<string> permissions,
        IReadOnlySet<Guid>? scopedProjectIds = null,
        Guid? tenantId = null)
    {
        tenantId ??= Guid.NewGuid();
        var projectLookup = new Mock<IProjectReferenceLookup>();
        projectLookup.Setup(item => item.FindAsync(requestedProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectReference(requestedProjectId, "Scoped project"));

        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            Guid.NewGuid(), tenantId.Value, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, scopedProjectIds?.SingleOrDefault(),
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), scopedProjectIds ?? new HashSet<Guid> { requestedProjectId })));

        var claims = permissions.Select(permission => new Claim("permission", permission))
            .Append(new Claim("tenantId", tenantId.Value.ToString()));
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        AppDbContext.ConfigureTenantResolution(httpContextAccessor);

        return new ProjectWorkflowConfigurationService(context, projectLookup.Object, management, httpContextAccessor);
    }

    private static ProjectAnnualBudgetWorkflow CreateAnnualBudgetWorkflow(
        AppDbContext context,
        Guid projectId,
        Guid tenantId,
        Guid actorId,
        IEnumerable<string> permissions,
        IUserEffectivePermissionService? effectivePermissionService = null,
        IAuditService? auditService = null,
        ITenantFiscalYearService? tenantFiscalYearService = null,
        IReadOnlySet<Guid>? scopedProjectIds = null)
    {
        if (effectivePermissionService is null)
        {
            var permissionService = new Mock<IUserEffectivePermissionService>();
            permissionService.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(permissions.ToHashSet(StringComparer.Ordinal));
            effectivePermissionService = permissionService.Object;
        }
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), scopedProjectIds ?? new HashSet<Guid> { projectId })));

        return new ProjectAnnualBudgetWorkflow(
            context,
            tenantFiscalYearService ?? Mock.Of<ITenantFiscalYearService>(),
            Mock.Of<IFiscalYearCalendarService>(),
            effectivePermissionService,
            management,
            auditService ?? Mock.Of<IAuditService>(),
            Mock.Of<IProjectAnnualBudgetApprovalNotificationPublisher>(),
            Mock.Of<IManagementScopeResolver>());
    }
}
