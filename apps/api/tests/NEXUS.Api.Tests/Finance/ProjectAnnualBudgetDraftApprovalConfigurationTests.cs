using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Api.Finance;
using NEXUS.Api.Management;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Finance;

public sealed class ProjectAnnualBudgetDraftApprovalConfigurationTests
{
    [Fact]
    public async Task Draft_override_saves_and_reopens_without_changing_project_defaults()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var projectManagerId = actorId;
        var defaultReviewerId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        await using var context = CreateContext();
        var managementRole = Role(tenantId, "Project Management", "Management");
        var manager = User(tenantId, projectManagerId, "Project", "Manager");
        var defaultReviewer = User(tenantId, defaultReviewerId, "Default", "Reviewer");
        var project = new Project
        {
            Id = projectId,
            TenantId = tenantId,
            Name = "Draft approval configuration",
            Code = "DRAFT-APPROVAL",
            FinanceApprovalMode = "Both",
            ProjectManagerId = projectManagerId,
            Currency = "EGP",
            RowVersion = [1]
        };
        var budget = new ProjectAnnualBudget
        {
            Id = budgetId,
            TenantId = tenantId,
            ProjectId = projectId,
            FiscalYear = today.Year,
            FiscalYearStartDate = new DateOnly(today.Year, 1, 1),
            FiscalYearEndDate = new DateOnly(today.Year, 12, 31),
            FinancialReference = "DRAFT-APPROVAL-FY",
            Currency = "EGP",
            NewApprovedFunding = 10_000m,
            Status = ProjectAnnualBudgetStatuses.Draft,
            RowVersion = [2]
        };
        var defaults = new ProjectAnnualBudgetApprovalConfiguration
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ApprovalMode = "Sequential",
            RowVersion = [3]
        };
        defaults.Approvers.Add(new ProjectAnnualBudgetApprovalApprover
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ConfigurationId = defaults.Id,
            ApproverUserId = defaultReviewerId,
            Position = 1,
            Configuration = defaults,
            RowVersion = [4]
        });
        var resource = new Resource
        {
            TenantId = tenantId,
            UserId = defaultReviewerId,
            Name = "Default reviewer resource",
            Type = "Employee",
            Status = "Active",
            IsActive = true
        };
        context.AddRange(
            project,
            budget,
            defaults,
            managementRole,
            manager,
            defaultReviewer,
            resource,
            new ProjectMember
            {
                TenantId = tenantId,
                ProjectId = projectId,
                ResourceId = resource.Id,
                Role = "Finance Reviewer",
                Status = "Active",
                StartDate = today.AddDays(-1),
                EndDate = today.AddDays(1)
            },
            new UserRole
            {
                TenantId = tenantId,
                UserId = projectManagerId,
                User = manager,
                RoleId = managementRole.Id,
                Role = managementRole
            },
            new UserRole
            {
                TenantId = tenantId,
                UserId = defaultReviewerId,
                User = defaultReviewer,
                RoleId = managementRole.Id,
                Role = managementRole
            });
        await context.SaveChangesAsync();

        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Permissions("ProjectAnnualBudget.View", "ProjectAnnualBudget.EditDraft",
                "ProjectAnnualBudget.ConfigureDraftApproval", "ProjectAnnualBudget.Approve"));
        permissions.Setup(service => service.GetEffectivePermissionsAsync(defaultReviewerId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Permissions("ProjectAnnualBudget.Approve"));
        var managementContext = new ManagementContextAccessor();
        managementContext.Set(new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var configurationService = new ProjectAnnualBudgetDraftApprovalConfigurationService(
            context, permissions.Object, managementContext, audit.Object);

        var defaultsPage = await configurationService.GetAsync(tenantId, projectId, budgetId, actorId, CancellationToken.None);
        Assert.True(defaultsPage.IsSuccess, defaultsPage.Error?.Message);
        Assert.Equal(ProjectAnnualBudgetDraftApprovalConfigurationSources.ProjectSettings,
            defaultsPage.Value!.ConfigurationSource);
        Assert.Contains(defaultsPage.Value.EligibleApprovers,
            candidate => candidate.UserId == projectManagerId && candidate.SelectionBlockReason is null);

        var saved = await configurationService.SaveAsync(
            tenantId, projectId, budgetId, actorId, "Project Manager",
            new SaveProjectAnnualBudgetDraftApprovalConfigurationDto(
                "Sequential", [projectManagerId], [2], [1], [3], "Use the project manager for this draft."),
            CancellationToken.None);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal(ProjectAnnualBudgetDraftApprovalConfigurationSources.DraftOverride,
            saved.Value!.ConfigurationSource);
        Assert.Equal("Use the project manager for this draft.", saved.Value.OverrideReason);
        Assert.Equal(projectManagerId, Assert.Single(saved.Value.ConfiguredApprovers).UserId);
        Assert.Equal("submitter_separation_of_duties", saved.Value.ConfiguredApprovers[0].IssueCode);
        Assert.True(saved.Value.IsValid, "The Draft plan is structurally valid; separation is checked against the actual submitter.");

        var persistedBudget = await context.ProjectAnnualBudgets.IgnoreQueryFilters().SingleAsync(item => item.Id == budgetId);
        Assert.True(ProjectAnnualBudgetDraftApprovalConfigurationSnapshot.TryRead(
            persistedBudget.DraftApprovalConfigurationJson, out var persisted));
        Assert.Equal("Sequential", persisted!.ApprovalMode);
        Assert.Equal(projectManagerId, Assert.Single(persisted.Approvers).UserId);
        Assert.Equal(actorId, persisted.UpdatedBy);
        Assert.Equal("Use the project manager for this draft.", persisted.OverrideReason);

        var persistedDefaults = await context.Set<ProjectAnnualBudgetApprovalConfiguration>().IgnoreQueryFilters()
            .SingleAsync(item => item.ProjectId == projectId);
        Assert.Equal("Sequential", persistedDefaults.ApprovalMode);
        Assert.Equal(defaultReviewerId, Assert.Single(persistedDefaults.Approvers).ApproverUserId);
        audit.Verify(service => service.AppendAsync(
            It.Is<AuditEntry>(entry => entry.Action == "DraftApprovalConfigurationSaved"
                && entry.EntityId == budgetId && entry.EntityType == "ProjectAnnualBudget"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pending_or_approved_annual_budget_cannot_be_reconfigured()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var context = CreateContext();
        context.AddRange(
            new Project { Id = projectId, TenantId = tenantId, Name = "State check", Code = "STATE", RowVersion = [1] },
            Budget(tenantId, projectId, budgetId, ProjectAnnualBudgetStatuses.Approved));
        await context.SaveChangesAsync();
        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Permissions("ProjectAnnualBudget.View", "ProjectAnnualBudget.EditDraft",
                "ProjectAnnualBudget.ConfigureDraftApproval"));
        var management = Management(actorId, tenantId, projectId);
        var service = new ProjectAnnualBudgetDraftApprovalConfigurationService(
            context, permissions.Object, management, Mock.Of<IAuditService>());

        var result = await service.GetAsync(tenantId, projectId, budgetId, actorId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.Contains("Draft", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submitter_uses_saved_draft_override_without_transient_plan_fields_or_configure_permission()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var submitterId = Guid.NewGuid();
        var savedReviewerId = Guid.NewGuid();
        var defaultReviewerId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;
        const int fiscalYear = 2026;

        await using var context = CreateContext();
        var managementRole = Role(tenantId, "Project Management", "Management");
        var submitter = User(tenantId, submitterId, "Budget", "Submitter");
        var savedReviewer = User(tenantId, savedReviewerId, "Draft", "Reviewer");
        var defaultReviewer = User(tenantId, defaultReviewerId, "Project", "Default");
        var project = new Project
        {
            Id = projectId,
            TenantId = tenantId,
            Name = "Saved Draft approval plan",
            Code = "SAVED-PLAN",
            FinanceApprovalMode = "Both",
            ProjectManagerId = savedReviewerId,
            Budget = 100_000m,
            Currency = "EGP",
            RowVersion = [1]
        };
        var calendar = new FiscalYearCalendarService();
        var period = calendar.ForFiscalYear(fiscalYear, 1);
        var budget = new ProjectAnnualBudget
        {
            Id = budgetId,
            TenantId = tenantId,
            ProjectId = projectId,
            FiscalYear = fiscalYear,
            FiscalYearStartDate = period.StartDate,
            FiscalYearEndDate = period.EndDate,
            FinancialReference = "SAVED-PLAN-FY2026",
            Currency = "EGP",
            NewApprovedFunding = 10_000m,
            Status = ProjectAnnualBudgetStatuses.Draft,
            RowVersion = [2]
        };
        var defaults = new ProjectAnnualBudgetApprovalConfiguration
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ApprovalMode = "Parallel",
            RowVersion = [3]
        };
        defaults.Approvers.Add(new ProjectAnnualBudgetApprovalApprover
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ConfigurationId = defaults.Id,
            ApproverUserId = defaultReviewerId,
            Position = null,
            Configuration = defaults,
            RowVersion = [4]
        });
        var defaultResource = new Resource
        {
            TenantId = tenantId,
            UserId = defaultReviewerId,
            Name = "Project default reviewer",
            Type = "Employee",
            Status = "Active",
            IsActive = true
        };
        var savedPlan = new ProjectAnnualBudgetDraftApprovalConfigurationSnapshot(
            ProjectAnnualBudgetDraftApprovalConfigurationSnapshot.CurrentVersion,
            "Sequential",
            [new PaymentRequestApproverSelectionDto(
                savedReviewerId, 1, PaymentRequestApproverRequirements.Mandatory)],
            "Use this reviewer for this Draft only.",
            project.RowVersion,
            defaults.RowVersion,
            Guid.NewGuid(),
            DateTime.UtcNow);
        budget.DraftApprovalConfigurationJson = System.Text.Json.JsonSerializer.Serialize(savedPlan);
        context.AddRange(
            project,
            budget,
            defaults,
            managementRole,
            submitter,
            savedReviewer,
            defaultReviewer,
            defaultResource,
            new ProjectMember
            {
                TenantId = tenantId,
                ProjectId = projectId,
                ResourceId = defaultResource.Id,
                Role = "Finance Reviewer",
                Status = "Active",
                StartDate = today.AddDays(-1),
                EndDate = today.AddDays(1)
            },
            new UserRole
            {
                TenantId = tenantId,
                UserId = savedReviewerId,
                User = savedReviewer,
                RoleId = managementRole.Id,
                Role = managementRole
            },
            new UserRole
            {
                TenantId = tenantId,
                UserId = defaultReviewerId,
                User = defaultReviewer,
                RoleId = managementRole.Id,
                Role = managementRole
            });
        await context.SaveChangesAsync();

        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsAsync(submitterId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Permissions("ProjectAnnualBudget.View", "ProjectAnnualBudget.SubmitForReview"));
        permissions.Setup(service => service.GetEffectivePermissionsAsync(savedReviewerId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Permissions("ProjectAnnualBudget.Approve"));
        permissions.Setup(service => service.GetEffectivePermissionsAsync(defaultReviewerId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Permissions("ProjectAnnualBudget.Approve"));
        var management = Management(submitterId, tenantId, projectId);
        var auditEntries = new List<AuditEntry>();
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => auditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var tenantFiscalYear = new Mock<ITenantFiscalYearService>();
        tenantFiscalYear.Setup(service => service.ResolveForTimestampAsync(
                tenantId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendar.ForFiscalYear(fiscalYear, 1));
        tenantFiscalYear.Setup(service => service.GetStartMonthAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var workflow = new ProjectAnnualBudgetWorkflow(
            context, tenantFiscalYear.Object, calendar, permissions.Object, management, audit.Object,
            new NoopProjectAnnualBudgetApprovalNotificationPublisher(),
            Mock.Of<IManagementScopeResolver>());

        var submitted = await workflow.SubmitForReviewAsync(
            tenantId, projectId, budgetId, submitterId, "Budget Submitter",
            new SubmitProjectAnnualBudgetReviewDto(null, null, [2]),
            CancellationToken.None);

        Assert.True(submitted.IsSuccess, submitted.Error?.Message);
        var approval = await context.ApprovalRequests.IgnoreQueryFilters()
            .SingleAsync(request => request.EntityId == budgetId && request.Type == "ProjectAnnualBudgetReview");
        Assert.Equal(savedReviewerId, approval.CurrentApproverId);
        var assignments = await context.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .Where(assignment => assignment.ApprovalRequestId == approval.Id)
            .OrderBy(assignment => assignment.Position)
            .ToArrayAsync();
        Assert.Equal([savedReviewerId], assignments.Select(assignment => assignment.ApproverUserId));
        Assert.Equal([1], assignments.Select(assignment => assignment.Position));
        Assert.Equal(PaymentRequestStatuses.Pending, approval.Status);

        using var auditJson = System.Text.Json.JsonDocument.Parse(
            Assert.Single(auditEntries.Where(entry => entry.Action == "SubmittedForReview")).NewValues!);
        var submittedPlan = auditJson.RootElement.GetProperty("effectiveApprovalPlan");
        Assert.Equal("DraftOverride", submittedPlan.GetProperty("ConfigurationSource").GetString());
        Assert.Equal("Sequential", submittedPlan.GetProperty("ApprovalMode").GetString());
        Assert.Contains("Use this reviewer for this Draft only.",
            auditJson.RootElement.GetProperty("override").GetProperty("reason").GetString());

        var unchangedDefaults = await context.Set<ProjectAnnualBudgetApprovalConfiguration>().IgnoreQueryFilters()
            .SingleAsync(configuration => configuration.ProjectId == projectId);
        Assert.Equal("Parallel", unchangedDefaults.ApprovalMode);
        Assert.Equal(defaultReviewerId, Assert.Single(unchangedDefaults.Approvers).ApproverUserId);
        var submitterPermissions = await permissions.Object.GetEffectivePermissionsAsync(
            submitterId, tenantId, CancellationToken.None);
        Assert.Contains("ProjectAnnualBudget.SubmitForReview", submitterPermissions);
        Assert.DoesNotContain("ProjectAnnualBudget.ConfigureDraftApproval", submitterPermissions);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static ProjectAnnualBudget Budget(Guid tenantId, Guid projectId, Guid budgetId, string status) => new()
    {
        Id = budgetId,
        TenantId = tenantId,
        ProjectId = projectId,
        FiscalYear = 2026,
        FiscalYearStartDate = new DateOnly(2026, 1, 1),
        FiscalYearEndDate = new DateOnly(2026, 12, 31),
        FinancialReference = "STATE-FY2026",
        Currency = "EGP",
        NewApprovedFunding = 1_000m,
        Status = status,
        RowVersion = [2]
    };

    private static User User(Guid tenantId, Guid id, string firstName, string lastName) => new()
    {
        Id = id,
        TenantId = tenantId,
        Email = $"{id:N}@example.test",
        FirstName = firstName,
        LastName = lastName,
        IsActive = true
    };

    private static Role Role(Guid tenantId, string name, string category) => new()
    {
        TenantId = tenantId,
        Name = name,
        Category = category
    };

    private static ManagementContextAccessor Management(Guid actorId, Guid tenantId, Guid projectId)
    {
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        return management;
    }

    private static HashSet<string> Permissions(params string[] values) => values.ToHashSet(StringComparer.Ordinal);
}
