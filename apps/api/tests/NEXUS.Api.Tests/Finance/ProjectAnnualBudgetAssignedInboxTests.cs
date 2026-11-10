using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Api.Finance;
using NEXUS.Api.Management;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Finance;

public sealed class ProjectAnnualBudgetAssignedInboxTests
{
    [Fact]
    public async Task Sequential_inbox_shows_only_current_step_and_assigned_sponsor_can_review_without_resource()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectPublicId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var sponsorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var context = CreateContext();
        var project = Project(tenantId, projectId, projectPublicId, sponsorId, managerId);
        var budget = Budget(tenantId, projectId, now.Year);
        var request = Approval(tenantId, budget.Id, requesterId, sponsorId, now.AddHours(-2));
        var first = Assignment(tenantId, request.Id, sponsorId, 1);
        var second = Assignment(tenantId, request.Id, managerId, 2);
        var requester = User(tenantId, requesterId, "Budget", "Requester");
        var sponsor = User(tenantId, sponsorId, "System", "Sponsor");
        var manager = User(tenantId, managerId, "Project", "Manager");
        var outsider = User(tenantId, outsiderId, "Unassigned", "Reviewer");
        var systemRole = Role(tenantId, "Tenant Admin", "System");
        var managementRole = Role(tenantId, "Finance Reviewer", "Management");
        context.AddRange(project, budget, request, first, second, requester, sponsor, manager, outsider,
            systemRole, managementRole,
            UserRole(tenantId, sponsor, systemRole), UserRole(tenantId, manager, managementRole));
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await context.SaveChangesAsync();

        var permissionService = PermissionService(
            (requesterId, []),
            (sponsorId, ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"]),
            (managerId, ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"]),
            (outsiderId, ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"]));

        var sponsorWorkflow = CreateWorkflow(context, tenantId, projectId, sponsorId, permissionService.Object);
        var sponsorInbox = await sponsorWorkflow.GetMyActionableApprovalsAsync(tenantId, sponsorId, CancellationToken.None);
        Assert.True(sponsorInbox.IsSuccess, sponsorInbox.Error?.Message);
        var sponsorTask = Assert.Single(sponsorInbox.Value!);
        Assert.Equal(request.Id, sponsorTask.RequestId);
        Assert.Equal(budget.Id, sponsorTask.BudgetId);
        Assert.Equal(projectPublicId, sponsorTask.ProjectPublicId);
        Assert.Equal("Sequential", sponsorTask.ApprovalMode);
        Assert.Equal(request.RequestedAt, sponsorTask.ActionableAtUtc);

        var managerWorkflow = CreateWorkflow(context, tenantId, projectId, managerId, permissionService.Object);
        var futureStepInbox = await managerWorkflow.GetMyActionableApprovalsAsync(tenantId, managerId, CancellationToken.None);
        Assert.True(futureStepInbox.IsSuccess, futureStepInbox.Error?.Message);
        Assert.Empty(futureStepInbox.Value!);

        var review = await sponsorWorkflow.GetAssignedReviewAsync(
            tenantId, projectId, budget.Id, request.Id, sponsorId, CancellationToken.None);
        Assert.True(review.IsSuccess, review.Error?.Message);

        var activeDetail = await sponsorWorkflow.GetMyAssignedApprovalDetailAsync(tenantId, sponsorId, request.Id, CancellationToken.None);
        Assert.True(activeDetail.IsSuccess, activeDetail.Error?.Message);
        Assert.True(activeDetail.Value!.CanDecide);
        Assert.Equal(100_000m, activeDetail.Value.ProjectBudget);
        Assert.Equal(10_000m, activeDetail.Value.TotalAnnualBudget);
        Assert.Equal("Sequential", activeDetail.Value.ApprovalMode);
        Assert.Equal("Active", Assert.Single(activeDetail.Value.Steps.Where(step => step.UserId == sponsorId)).WorkflowState);
        Assert.Equal("Waiting", Assert.Single(activeDetail.Value.Steps.Where(step => step.UserId == managerId)).WorkflowState);

        var futureDetail = await managerWorkflow.GetMyAssignedApprovalDetailAsync(tenantId, managerId, request.Id, CancellationToken.None);
        Assert.True(futureDetail.IsSuccess, futureDetail.Error?.Message);
        Assert.False(futureDetail.Value!.CanDecide);
        Assert.Equal("waiting_for_previous_approvers", futureDetail.Value.DisabledReason);

        var firstDecision = await sponsorWorkflow.DecideReviewAsync(
            tenantId,
            projectId,
            budget.Id,
            request.Id,
            sponsorId,
            "System Sponsor",
            new NEXUS.Modules.CostManagement.Application.DTOs.DecideProjectAnnualBudgetReviewDto(
                "Approved", null, budget.RowVersion, request.RowVersion),
            CancellationToken.None);
        Assert.True(firstDecision.IsSuccess, firstDecision.Error?.Message);
        Assert.Equal(managerId, request.CurrentApproverId);
        var firstActionTime = await context.ApprovalActions.IgnoreQueryFilters()
            .Where(action => action.ApproverAssignmentId == first.Id)
            .Select(action => action.ActionedAt)
            .SingleAsync();

        sponsorInbox = await sponsorWorkflow.GetMyActionableApprovalsAsync(tenantId, sponsorId, CancellationToken.None);
        futureStepInbox = await managerWorkflow.GetMyActionableApprovalsAsync(tenantId, managerId, CancellationToken.None);
        Assert.Empty(sponsorInbox.Value!);
        var newlyActiveTask = Assert.Single(futureStepInbox.Value!);
        Assert.Equal(managerId, request.CurrentApproverId);
        Assert.Equal(firstActionTime, newlyActiveTask.ActionableAtUtc);

        var decision = await managerWorkflow.DecideReviewAsync(
            tenantId,
            projectId,
            budget.Id,
            request.Id,
            managerId,
            "Project Manager",
            new NEXUS.Modules.CostManagement.Application.DTOs.DecideProjectAnnualBudgetReviewDto(
                "Approved", null, budget.RowVersion, request.RowVersion),
            CancellationToken.None);
        Assert.True(decision.IsSuccess, decision.Error?.Message);
        Assert.Equal(PaymentRequestStatuses.Approved, request.Status);
        Assert.Equal(PaymentRequestAssignmentStates.Approved, second.State);
        Assert.Equal(ProjectAnnualBudgetStatuses.Approved, budget.Status);
        Assert.Equal(100_000m, (await context.Projects.IgnoreQueryFilters().SingleAsync(row => row.Id == projectId)).Budget);
        var annualBudgetPage = await managerWorkflow.GetPageAsync(tenantId, projectId, managerId, CancellationToken.None);
        Assert.True(annualBudgetPage.IsSuccess, annualBudgetPage.Error?.Message);
        Assert.Equal(100_000m, annualBudgetPage.Value!.TotalProjectFinancialCeiling);
        Assert.Equal(10_000m, Assert.Single(annualBudgetPage.Value.Items).TotalAnnualBudget);

        var completedHistory = await sponsorWorkflow.GetMyAssignedApprovalHistoryAsync(tenantId, sponsorId, CancellationToken.None);
        Assert.True(completedHistory.IsSuccess, completedHistory.Error?.Message);
        var completed = Assert.Single(completedHistory.Value!);
        Assert.Equal(PaymentRequestStatuses.Approved, completed.Status);
        Assert.Equal("Approved", completed.AssignedStepState);
        Assert.False(completed.CanDecide);
        var completedDetail = await sponsorWorkflow.GetMyAssignedApprovalDetailAsync(tenantId, sponsorId, request.Id, CancellationToken.None);
        Assert.True(completedDetail.IsSuccess, completedDetail.Error?.Message);
        Assert.False(completedDetail.Value!.CanDecide);
        Assert.Equal("request_completed", completedDetail.Value.DisabledReason);
    }

    [Theory]
    [InlineData("Rejected", "Rejected")]
    [InlineData("Returned", "Returned")]
    public async Task Negative_review_decisions_keep_lifetime_ceiling_and_existing_annual_values(string decision, string expectedApprovalStatus)
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sponsorId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var context = CreateContext();
        var project = Project(tenantId, projectId, Guid.NewGuid(), sponsorId, Guid.NewGuid());
        var budget = Budget(tenantId, projectId, now.Year);
        var request = Approval(tenantId, budget.Id, requesterId, sponsorId, now);
        var assignment = Assignment(tenantId, request.Id, sponsorId, 1);
        var sponsor = User(tenantId, sponsorId, "System", "Sponsor");
        var requester = User(tenantId, requesterId, "Budget", "Requester");
        var role = Role(tenantId, "Tenant Admin", "System");
        context.AddRange(project, budget, request, assignment, sponsor, requester, role,
            UserRole(tenantId, sponsor, role));
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await context.SaveChangesAsync();

        var workflow = CreateWorkflow(context, tenantId, projectId, sponsorId,
            PermissionService((sponsorId, ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"])).Object);
        var result = await workflow.DecideReviewAsync(tenantId, projectId, budget.Id, request.Id,
            sponsorId, "System Sponsor",
            new NEXUS.Modules.CostManagement.Application.DTOs.DecideProjectAnnualBudgetReviewDto(
                decision, "Review decision reason", budget.RowVersion, request.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(expectedApprovalStatus, request.Status);
        Assert.Equal(PaymentRequestAssignmentStates.Rejected, assignment.State);
        Assert.Equal(ProjectAnnualBudgetStatuses.Draft, budget.Status);
        Assert.Equal(10_000m, budget.NewApprovedFunding);
        Assert.Equal(100_000m, (await context.Projects.IgnoreQueryFilters().SingleAsync(row => row.Id == projectId)).Budget);
        var action = await context.ApprovalActions.IgnoreQueryFilters().SingleAsync(row => row.ApproverAssignmentId == assignment.Id);
        Assert.Equal(decision, action.Action);
        Assert.Equal("Review decision reason", action.Comments);
    }

    [Fact]
    public async Task Assigned_revision_detail_uses_the_persisted_proposal_and_keeps_lifetime_ceiling_separate()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sponsorId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var context = CreateContext();
        var project = Project(tenantId, projectId, Guid.NewGuid(), sponsorId, Guid.NewGuid());
        var budget = Budget(tenantId, projectId, now.Year);
        budget.Status = ProjectAnnualBudgetStatuses.Approved;
        budget.NewApprovedFunding = 10_000m;
        budget.ApprovedCarryForward = 1_000m;
        var request = Approval(tenantId, budget.Id, requesterId, sponsorId, now);
        request.Type = "ProjectAnnualBudgetRevision";
        request.Title = "Revise Annual Budget FY";
        request.Amount = 25_000m;
        request.Description = "{\"NewApprovedFunding\":20000,\"ApprovedAdjustments\":5000,\"Reason\":\"Updated estimate\"}";
        var assignment = Assignment(tenantId, request.Id, sponsorId, null);
        var sponsor = User(tenantId, sponsorId, "System", "Sponsor");
        var requester = User(tenantId, requesterId, "Budget", "Requester");
        var role = Role(tenantId, "Tenant Admin", "System");
        context.AddRange(project, budget, request, assignment, sponsor, requester, role,
            UserRole(tenantId, sponsor, role));
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await context.SaveChangesAsync();

        var workflow = CreateWorkflow(context, tenantId, projectId, sponsorId,
            PermissionService((sponsorId, ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"])).Object);
        var result = await workflow.GetMyAssignedApprovalDetailAsync(tenantId, sponsorId, request.Id, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("ProjectAnnualBudgetRevision", result.Value!.RequestType);
        Assert.Equal("Revise Annual Budget FY", result.Value.RequestTitle);
        Assert.Equal("Updated estimate", result.Value.Reason);
        Assert.Equal(20_000m, result.Value.NewFunding);
        Assert.Equal(1_000m, result.Value.ApprovedCarryForward);
        Assert.Equal(5_000m, result.Value.ApprovedAdjustments);
        Assert.Equal(26_000m, result.Value.TotalAnnualBudget);
        Assert.Equal(100_000m, result.Value.ProjectBudget);
        Assert.Equal("EGP", result.Value.ProjectCurrency);
    }

    [Fact]
    public async Task Parallel_inbox_is_assignment_only_and_requires_both_view_and_approve()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sponsorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var deniedId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var context = CreateContext();
        var project = Project(tenantId, projectId, Guid.NewGuid(), sponsorId, managerId);
        var budget = Budget(tenantId, projectId, now.Year);
        var request = Approval(tenantId, budget.Id, requesterId, null, now);
        var requester = User(tenantId, requesterId, "Budget", "Requester");
        var sponsor = User(tenantId, sponsorId, "System", "Sponsor");
        var manager = User(tenantId, managerId, "Project", "Manager");
        var outsider = User(tenantId, outsiderId, "Unassigned", "User");
        var denied = User(tenantId, deniedId, "Denied", "Reviewer");
        var systemRole = Role(tenantId, "System Reviewer", "System");
        var managementRole = Role(tenantId, "Project Manager", "Management");
        var deniedResource = new Resource
        {
            TenantId = tenantId,
            UserId = deniedId,
            Name = "Denied reviewer",
            Type = "Employee",
            Status = "Active",
            IsActive = true
        };
        var deniedMembership = new ProjectMember
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ResourceId = deniedResource.Id,
            Role = "Finance Reviewer",
            Status = "Active",
            StartDate = now.Date.AddDays(-1),
            EndDate = now.Date.AddDays(1)
        };
        context.AddRange(project, budget, request,
            Assignment(tenantId, request.Id, sponsorId, null),
            Assignment(tenantId, request.Id, managerId, null),
            Assignment(tenantId, request.Id, requesterId, null), // Separation of duties hides self-assignment.
            requester, sponsor, manager, outsider, denied, systemRole, managementRole, deniedResource, deniedMembership);
        context.AddRange(UserRole(tenantId, sponsor, systemRole), UserRole(tenantId, manager, managementRole),
            UserRole(tenantId, denied, systemRole), Assignment(tenantId, request.Id, deniedId, null));
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await context.SaveChangesAsync();

        var permissionService = PermissionService(
            (sponsorId, ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"]),
            (managerId, ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"]),
            (requesterId, ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"]),
            (outsiderId, ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"]),
            (deniedId, ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"]));
        permissionService.Setup(service => service.GetEffectivePermissionsAsync(
                deniedId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EffectivePermissionResolver.Resolve(
                ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"],
                [("ProjectAnnualBudget.Approve", PermissionOverrideEffect.Deny)]));

        var sponsorWorkflow = CreateWorkflow(context, tenantId, projectId, sponsorId, permissionService.Object);
        var managerWorkflow = CreateWorkflow(context, tenantId, projectId, managerId, permissionService.Object);
        var requesterWorkflow = CreateWorkflow(context, tenantId, projectId, requesterId, permissionService.Object);
        var outsiderWorkflow = CreateWorkflow(context, tenantId, projectId, outsiderId, permissionService.Object);
        var deniedWorkflow = CreateWorkflow(context, tenantId, projectId, deniedId, permissionService.Object);

        var sponsorInbox = await sponsorWorkflow.GetMyActionableApprovalsAsync(tenantId, sponsorId, CancellationToken.None);
        var managerInbox = await managerWorkflow.GetMyActionableApprovalsAsync(tenantId, managerId, CancellationToken.None);
        var requesterInbox = await requesterWorkflow.GetMyActionableApprovalsAsync(tenantId, requesterId, CancellationToken.None);
        var outsiderInbox = await outsiderWorkflow.GetMyActionableApprovalsAsync(tenantId, outsiderId, CancellationToken.None);
        var deniedInbox = await deniedWorkflow.GetMyActionableApprovalsAsync(tenantId, deniedId, CancellationToken.None);
        Assert.Single(sponsorInbox.Value!);
        Assert.Single(managerInbox.Value!);
        Assert.Empty(requesterInbox.Value!);
        Assert.Empty(outsiderInbox.Value!);
        Assert.True(deniedInbox.IsFailure);
        Assert.Equal(403, deniedInbox.Error!.StatusCode);

        var noView = PermissionService((sponsorId, ["ProjectAnnualBudget.Approve"]));
        var noViewWorkflow = CreateWorkflow(context, tenantId, projectId, sponsorId, noView.Object);
        var deniedView = await noViewWorkflow.GetMyActionableApprovalsAsync(tenantId, sponsorId, CancellationToken.None);
        Assert.True(deniedView.IsFailure);
        Assert.Equal(403, deniedView.Error!.StatusCode);

        var noProjectScopeWorkflow = CreateWorkflow(context, tenantId, projectId, sponsorId, permissionService.Object,
            new HashSet<Guid>());
        var outOfScope = await noProjectScopeWorkflow.GetMyActionableApprovalsAsync(tenantId, sponsorId, CancellationToken.None);
        Assert.True(outOfScope.IsSuccess, outOfScope.Error?.Message);
        Assert.Empty(outOfScope.Value!);

        var accessDeniedWorkflow = CreateWorkflow(context, tenantId, projectId, sponsorId, permissionService.Object,
            new HashSet<Guid> { projectId }, canAccessProject: false);
        var rejectedByScopeResolver = await accessDeniedWorkflow.GetMyActionableApprovalsAsync(
            tenantId, sponsorId, CancellationToken.None);
        Assert.True(rejectedByScopeResolver.IsSuccess, rejectedByScopeResolver.Error?.Message);
        Assert.Empty(rejectedByScopeResolver.Value!);

        var deniedDetail = await deniedWorkflow.GetMyAssignedApprovalDetailAsync(tenantId, deniedId, request.Id, CancellationToken.None);
        Assert.True(deniedDetail.IsSuccess, deniedDetail.Error?.Message);
        Assert.False(deniedDetail.Value!.CanDecide);
        Assert.Equal("approval_permission_required", deniedDetail.Value.DisabledReason);

        var viewOnlyWorkflow = CreateWorkflow(context, tenantId, projectId, sponsorId,
            PermissionService((sponsorId, ["ProjectAnnualBudget.View"])).Object);
        var viewOnlyDetail = await viewOnlyWorkflow.GetMyAssignedApprovalDetailAsync(
            tenantId, sponsorId, request.Id, CancellationToken.None);
        Assert.True(viewOnlyDetail.IsSuccess, viewOnlyDetail.Error?.Message);
        Assert.False(viewOnlyDetail.Value!.CanDecide);
        Assert.Equal("approval_permission_required", viewOnlyDetail.Value.DisabledReason);

        var unassignedDetail = await outsiderWorkflow.GetMyAssignedApprovalDetailAsync(
            tenantId, outsiderId, request.Id, CancellationToken.None);
        Assert.True(unassignedDetail.IsFailure);
        Assert.Equal(404, unassignedDetail.Error!.StatusCode);

        var foreignTenantDetail = await sponsorWorkflow.GetMyAssignedApprovalDetailAsync(
            Guid.NewGuid(), sponsorId, request.Id, CancellationToken.None);
        Assert.True(foreignTenantDetail.IsFailure);
        Assert.Equal(404, foreignTenantDetail.Error!.StatusCode);

        var outOfScopeDetail = await noProjectScopeWorkflow.GetMyAssignedApprovalDetailAsync(
            tenantId, sponsorId, request.Id, CancellationToken.None);
        Assert.True(outOfScopeDetail.IsFailure);
        Assert.Equal(404, outOfScopeDetail.Error!.StatusCode);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static ProjectAnnualBudgetWorkflow CreateWorkflow(
        AppDbContext context,
        Guid tenantId,
        Guid projectId,
        Guid actorId,
        IUserEffectivePermissionService permissions,
        HashSet<Guid>? scopedProjects = null,
        bool? canAccessProject = null)
    {
        var management = new ManagementContextAccessor();
        var projectScope = scopedProjects ?? new HashSet<Guid> { projectId };
        management.Set(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), projectScope)));
        var scopeResolver = new Mock<IManagementScopeResolver>();
        scopeResolver.Setup(resolver => resolver.GetPerspectiveSnapshotAsync(
                actorId, tenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagementPerspectiveSnapshot(
                [ManagementPerspective.ProjectManager],
                [],
                ManagementPerspective.ProjectManager,
                false,
                new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), projectScope),
                null));
        scopeResolver.Setup(resolver => resolver.CanAccessProjectAsync(
                actorId, tenantId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(canAccessProject ?? projectScope.Contains(projectId));
        var fiscal = Mock.Of<ITenantFiscalYearService>();
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<NEXUS.Modules.Audit.Domain.AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var notificationPublisher = new Mock<IProjectAnnualBudgetApprovalNotificationPublisher>();
        notificationPublisher.Setup(service => service.QueueActionableStepsAsync(
                It.IsAny<Project>(), It.IsAny<ProjectAnnualBudget>(), It.IsAny<ApprovalRequest>(),
                It.IsAny<IReadOnlyCollection<PaymentRequestApproverAssignment>>(),
                It.IsAny<IReadOnlyCollection<ApprovalAction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return new ProjectAnnualBudgetWorkflow(context, fiscal, new FiscalYearCalendarService(), permissions,
            management, audit.Object, notificationPublisher.Object, scopeResolver.Object);
    }

    private static Mock<IUserEffectivePermissionService> PermissionService(
        params (Guid UserId, string[] Permissions)[] users)
    {
        var service = new Mock<IUserEffectivePermissionService>();
        foreach (var (userId, permissions) in users)
            service.Setup(item => item.GetEffectivePermissionsAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(permissions.ToHashSet(StringComparer.Ordinal));
        return service;
    }

    private static Project Project(Guid tenantId, Guid projectId, Guid publicId, Guid sponsorId, Guid managerId) => new()
    {
        Id = projectId,
        PublicId = publicId,
        TenantId = tenantId,
        Name = "Sponsor approval project",
        Code = "SPONSOR-APPROVAL",
        Currency = "EGP",
        Budget = 100_000m,
        SponsorId = sponsorId,
        ProjectManagerId = managerId
    };

    private static ProjectAnnualBudget Budget(Guid tenantId, Guid projectId, int fiscalYear) => new()
    {
        TenantId = tenantId,
        ProjectId = projectId,
        FiscalYear = fiscalYear,
        FiscalYearStartDate = new DateOnly(fiscalYear, 1, 1),
        FiscalYearEndDate = new DateOnly(fiscalYear, 12, 31),
        FinancialReference = $"FY{fiscalYear}-SPONSOR-01",
        Currency = "EGP",
        NewApprovedFunding = 10_000m,
        Status = ProjectAnnualBudgetStatuses.Draft,
        RowVersion = [1, 2, 3]
    };

    private static ApprovalRequest Approval(Guid tenantId, Guid budgetId, Guid requesterId, Guid? currentApproverId, DateTime requestedAt) => new()
    {
        TenantId = tenantId,
        Title = "Review Annual Budget",
        EntityType = "ProjectAnnualBudget",
        EntityId = budgetId,
        EntityNumber = "FY-APPROVAL",
        Type = "ProjectAnnualBudgetReview",
        Status = PaymentRequestStatuses.Pending,
        Priority = "Normal",
        Amount = 10_000m,
        Currency = "EGP",
        RequestedBy = requesterId,
        RequestedAt = requestedAt,
        CurrentApproverId = currentApproverId,
        RowVersion = [4, 5, 6]
    };

    private static PaymentRequestApproverAssignment Assignment(Guid tenantId, Guid requestId, Guid userId, int? position) => new()
    {
        TenantId = tenantId,
        ApprovalRequestId = requestId,
        ApproverUserId = userId,
        Position = position,
        Requirement = PaymentRequestAssignmentRequirements.Mandatory,
        State = PaymentRequestAssignmentStates.Pending
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
        Category = category,
        IsSystem = category == "System"
    };

    private static UserRole UserRole(Guid tenantId, User user, Role role) => new()
    {
        TenantId = tenantId,
        UserId = user.Id,
        User = user,
        RoleId = role.Id,
        Role = role
    };
}
