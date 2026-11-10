using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Api.Finance;
using NEXUS.Api.Management;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Finance;

public sealed class ProjectAnnualBudgetLifecycleDeletionTests
{
    [Fact]
    public async Task Authorized_never_submitted_draft_is_deleted_with_audit_and_without_changing_project_ceiling()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var budget = NewBudget(tenantId, projectId, 2026, ProjectAnnualBudgetStatuses.Draft);
        budget.DraftApprovalConfigurationJson = "{\"overrideReason\":\"audited draft plan\"}";
        await using var context = CreateContext();
        context.AddRange(NewProject(tenantId, projectId, 100_000m), budget);
        await context.SaveChangesAsync();
        budget.RowVersion = [1, 2, 3];
        var (workflow, audit) = CreateWorkflow(context, tenantId, projectId, actorId,
            "ProjectAnnualBudget.View", "ProjectAnnualBudget.Delete");

        var result = await workflow.DeleteDraftAsync(tenantId, projectId, budget.Id, actorId,
            "Lifecycle Tester", new DeleteProjectAnnualBudgetDto(budget.RowVersion), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(await context.ProjectAnnualBudgets.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == budget.Id));
        Assert.Equal(100_000m, (await context.Projects.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == projectId)).Budget);

        audit.Verify(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        var recorded = Assert.IsType<AuditEntry>(audit.Invocations
            .Single(invocation => invocation.Method.Name == nameof(IAuditService.AppendAsync)).Arguments[0]);
        Assert.Equal(actorId, recorded.UserId);
        Assert.Equal("Lifecycle Tester", recorded.UserName);
        Assert.Equal("DeletedDraft", recorded.Action);
        Assert.Equal(projectId, recorded.ProjectId);
        Assert.Equal(budget.Id, recorded.EntityId);
        Assert.Contains("DraftApprovalConfigurationJson", recorded.OriginalValues!);
        Assert.Contains("draftApprovalConfiguration", recorded.Changes!);

        var page = await workflow.GetPageAsync(tenantId, projectId, actorId, CancellationToken.None);
        Assert.True(page.IsSuccess, page.Error?.Message);
        Assert.Empty(page.Value!.Items);
    }

    [Fact]
    public async Task User_without_delete_permission_cannot_delete_a_draft()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var budget = NewBudget(tenantId, projectId, 2026, ProjectAnnualBudgetStatuses.Draft);
        await using var context = CreateContext();
        context.AddRange(NewProject(tenantId, projectId, 100_000m), budget);
        await context.SaveChangesAsync();
        budget.RowVersion = [1, 2, 3];
        var (workflow, _) = CreateWorkflow(context, tenantId, projectId, actorId,
            "ProjectAnnualBudget.View");

        var page = await workflow.GetPageAsync(tenantId, projectId, actorId, CancellationToken.None);
        Assert.True(page.IsSuccess, page.Error?.Message);
        var pageItem = Assert.Single(page.Value!.Items);
        Assert.False(pageItem.CanDeleteDraft);
        Assert.Equal("permission-required", pageItem.DeleteDraftBlockReason);

        var result = await workflow.DeleteDraftAsync(tenantId, projectId, budget.Id, actorId,
            "Read Only", new DeleteProjectAnnualBudgetDto(budget.RowVersion), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
        Assert.True(await context.ProjectAnnualBudgets.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == budget.Id && !item.IsDeleted));
    }

    [Fact]
    public async Task Delete_permission_without_view_permission_cannot_delete_a_draft()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var budget = NewBudget(tenantId, projectId, 2026, ProjectAnnualBudgetStatuses.Draft);
        await using var context = CreateContext();
        context.AddRange(NewProject(tenantId, projectId, 100_000m), budget);
        await context.SaveChangesAsync();
        var (workflow, _) = CreateWorkflow(context, tenantId, projectId, actorId,
            "ProjectAnnualBudget.Delete");

        var result = await workflow.DeleteDraftAsync(tenantId, projectId, budget.Id, actorId,
            "Delete Without View", new DeleteProjectAnnualBudgetDto(budget.RowVersion), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
        Assert.Equal("ProjectAnnualBudget.View is required to delete an Annual Budget Draft.", result.Error.Message);
        Assert.True(await context.ProjectAnnualBudgets.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == budget.Id && !item.IsDeleted));
    }

    [Fact]
    public async Task Approved_budget_cannot_be_deleted_even_with_delete_permission()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var budget = NewBudget(tenantId, projectId, 2026, ProjectAnnualBudgetStatuses.Approved);
        budget.ApprovedAt = DateTime.UtcNow.AddDays(-1);
        budget.ApprovedBy = Guid.NewGuid();
        await using var context = CreateContext();
        context.AddRange(NewProject(tenantId, projectId, 100_000m), budget);
        await context.SaveChangesAsync();
        budget.RowVersion = [1, 2, 3];
        var (workflow, _) = CreateWorkflow(context, tenantId, projectId, actorId,
            "ProjectAnnualBudget.View", "ProjectAnnualBudget.Delete");

        var page = await workflow.GetPageAsync(tenantId, projectId, actorId, CancellationToken.None);
        Assert.True(page.IsSuccess, page.Error?.Message);
        Assert.Equal("not-draft", Assert.Single(page.Value!.Items).DeleteDraftBlockReason);

        var result = await workflow.DeleteDraftAsync(tenantId, projectId, budget.Id, actorId,
            "Lifecycle Tester", new DeleteProjectAnnualBudgetDto(budget.RowVersion), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.True(await context.ProjectAnnualBudgets.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == budget.Id && !item.IsDeleted));
    }

    [Fact]
    public async Task Draft_created_by_approved_closing_cannot_be_deleted_or_erase_carry_forward_provenance()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var source = NewBudget(tenantId, projectId, 2025, ProjectAnnualBudgetStatuses.Approved);
        source.ApprovedCarryForward = 2_000m;
        var destination = NewBudget(tenantId, projectId, 2026, ProjectAnnualBudgetStatuses.Draft);
        destination.ApprovedCarryForward = 2_000m;
        var closing = new ProjectAnnualBudgetClosing
        {
            TenantId = tenantId,
            ProjectId = projectId,
            SourceAnnualBudgetId = source.Id,
            FiscalYear = source.FiscalYear,
            FiscalYearStartDate = source.FiscalYearStartDate,
            FiscalYearEndDate = source.FiscalYearEndDate,
            DestinationAnnualBudgetId = destination.Id,
            DestinationFiscalYear = destination.FiscalYear,
            Currency = "USD",
            Status = ProjectAnnualBudgetClosingStatuses.Closed,
            ApprovedFreeCarryForward = 2_000m
        };
        source.ClosedByClosingId = closing.Id;
        source.ClosedAtUtc = DateTime.UtcNow.AddDays(-1);
        source.ClosedBy = actorId;

        await using var context = CreateContext();
        context.AddRange(NewProject(tenantId, projectId, 100_000m), source, destination, closing);
        await context.SaveChangesAsync();
        destination.RowVersion = [1, 2, 3];
        var (workflow, _) = CreateWorkflow(context, tenantId, projectId, actorId,
            "ProjectAnnualBudget.View", "ProjectAnnualBudget.Delete");

        var result = await workflow.DeleteDraftAsync(tenantId, projectId, destination.Id, actorId,
            "Lifecycle Tester", new DeleteProjectAnnualBudgetDto(destination.RowVersion), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.True(await context.ProjectAnnualBudgets.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == destination.Id && !item.IsDeleted));
        Assert.True(await context.ProjectAnnualBudgetClosings.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == closing.Id && item.Status == ProjectAnnualBudgetClosingStatuses.Closed));
        Assert.Equal(2_000m, (await context.ProjectAnnualBudgets.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == destination.Id)).ApprovedCarryForward);
    }

    [Fact]
    public async Task Draft_with_carry_forward_or_orphaned_closing_metadata_cannot_be_deleted()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var carryForwardDraft = NewBudget(tenantId, projectId, 2026, ProjectAnnualBudgetStatuses.Draft);
        carryForwardDraft.ApprovedCarryForward = 1_000m;
        carryForwardDraft.ApprovedReservedCarryForward = 250m;
        var orphanedClosingDraft = NewBudget(tenantId, projectId, 2027, ProjectAnnualBudgetStatuses.Draft);
        await using var context = CreateContext();
        orphanedClosingDraft.ClosedByClosingId = null;
        context.AddRange(NewProject(tenantId, projectId, 100_000m), carryForwardDraft, orphanedClosingDraft);
        await context.SaveChangesAsync();
        // Simulate a legacy orphaned closing reference without attempting to
        // write an invalid FK through the relational persistence layer.
        orphanedClosingDraft.ClosedByClosingId = Guid.NewGuid();
        orphanedClosingDraft.ClosedAtUtc = DateTime.UtcNow.AddDays(-1);
        orphanedClosingDraft.ClosedBy = Guid.NewGuid();
        var (workflow, _) = CreateWorkflow(context, tenantId, projectId, actorId,
            "ProjectAnnualBudget.View", "ProjectAnnualBudget.Delete");

        var carryResult = await workflow.DeleteDraftAsync(tenantId, projectId, carryForwardDraft.Id, actorId,
            "Lifecycle Tester", new DeleteProjectAnnualBudgetDto(carryForwardDraft.RowVersion), CancellationToken.None);
        var orphanResult = await workflow.DeleteDraftAsync(tenantId, projectId, orphanedClosingDraft.Id, actorId,
            "Lifecycle Tester", new DeleteProjectAnnualBudgetDto(orphanedClosingDraft.RowVersion), CancellationToken.None);

        Assert.True(carryResult.IsFailure);
        Assert.Equal(409, carryResult.Error!.StatusCode);
        Assert.True(orphanResult.IsFailure);
        Assert.Equal(409, orphanResult.Error!.StatusCode);
        Assert.Equal(1_000m, (await context.ProjectAnnualBudgets.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == carryForwardDraft.Id)).ApprovedCarryForward);
        Assert.NotNull((await context.ProjectAnnualBudgets.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == orphanedClosingDraft.Id)).ClosedByClosingId);
    }

    [Fact]
    public async Task Draft_with_pending_approval_work_is_not_deleted_or_silently_cancelled()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var budget = NewBudget(tenantId, projectId, 2026, ProjectAnnualBudgetStatuses.Draft);
        var approval = PendingReview(tenantId, budget.Id, Guid.NewGuid());
        var assignment = new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverUserId = Guid.NewGuid(),
            State = PaymentRequestAssignmentStates.Pending
        };
        await using var context = CreateContext();
        context.AddRange(NewProject(tenantId, projectId, 100_000m), budget, approval, assignment);
        await context.SaveChangesAsync();
        budget.RowVersion = [1, 2, 3];
        var (workflow, _) = CreateWorkflow(context, tenantId, projectId, actorId,
            "ProjectAnnualBudget.View", "ProjectAnnualBudget.Delete");

        var page = await workflow.GetPageAsync(tenantId, projectId, actorId, CancellationToken.None);
        Assert.True(page.IsSuccess, page.Error?.Message);
        Assert.Equal("pending-review", Assert.Single(page.Value!.Items).DeleteDraftBlockReason);

        var result = await workflow.DeleteDraftAsync(tenantId, projectId, budget.Id, actorId,
            "Lifecycle Tester", new DeleteProjectAnnualBudgetDto(budget.RowVersion), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error!.StatusCode);
        Assert.True(await context.ProjectAnnualBudgets.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == budget.Id && !item.IsDeleted));
        Assert.Equal(PaymentRequestStatuses.Pending, (await context.ApprovalRequests.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == approval.Id)).Status);
        Assert.Equal(PaymentRequestAssignmentStates.Pending, (await context.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == assignment.Id)).State);
    }

    [Theory]
    [InlineData("Returned")]
    [InlineData("Rejected")]
    public async Task Returned_or_rejected_draft_with_stale_pending_step_can_be_deleted_and_approval_history_is_retained(
        string approvalStatus)
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var budget = NewBudget(tenantId, projectId, 2026, ProjectAnnualBudgetStatuses.Draft);
        var approval = PendingReview(tenantId, budget.Id, Guid.NewGuid());
        approval.Status = approvalStatus;
        approval.CurrentApproverId = null;
        var assignment = new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverUserId = Guid.NewGuid(),
            State = PaymentRequestAssignmentStates.Rejected
        };
        var stalePendingAssignment = new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverUserId = Guid.NewGuid(),
            Position = 2,
            State = PaymentRequestAssignmentStates.Pending
        };
        var history = new ApprovalAction
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverId = assignment.ApproverUserId,
            ApproverAssignmentId = assignment.Id,
            Action = approvalStatus,
            Comments = "Please revise before resubmitting",
            ActionedAt = DateTime.UtcNow.AddMinutes(-5),
            ApprovalLevel = 1
        };
        await using var context = CreateContext();
        context.AddRange(NewProject(tenantId, projectId, 100_000m), budget, approval,
            assignment, stalePendingAssignment, history);
        await context.SaveChangesAsync();
        var (workflow, _) = CreateWorkflow(context, tenantId, projectId, actorId,
            "ProjectAnnualBudget.View", "ProjectAnnualBudget.Delete");

        var page = await workflow.GetPageAsync(tenantId, projectId, actorId, CancellationToken.None);
        Assert.True(page.IsSuccess, page.Error?.Message);
        var pageItem = Assert.Single(page.Value!.Items);
        Assert.Equal(approvalStatus, pageItem.ReviewStatus);
        Assert.True(pageItem.CanDeleteDraft);
        Assert.Null(pageItem.DeleteDraftBlockReason);

        var result = await workflow.DeleteDraftAsync(tenantId, projectId, budget.Id, actorId,
            "Lifecycle Tester", new DeleteProjectAnnualBudgetDto(budget.RowVersion), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(await context.ProjectAnnualBudgets.IgnoreQueryFilters()
            .AnyAsync(item => item.Id == budget.Id));
        Assert.Equal(approvalStatus, (await context.ApprovalRequests.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == approval.Id)).Status);
        Assert.Equal("Please revise before resubmitting", (await context.ApprovalActions.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == history.Id)).Comments);
        Assert.Equal(PaymentRequestAssignmentStates.Rejected,
            (await context.PaymentRequestApproverAssignments.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == assignment.Id)).State);
        Assert.Equal(PaymentRequestAssignmentStates.Pending,
            (await context.PaymentRequestApproverAssignments.IgnoreQueryFilters()
                .SingleAsync(item => item.Id == stalePendingAssignment.Id)).State);
    }

    [Fact]
    public async Task Withdrawing_pending_review_cancels_only_open_steps_and_preserves_history()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var submitterId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var actorId = reviewerId;
        var budget = NewBudget(tenantId, projectId, 2026, ProjectAnnualBudgetStatuses.Draft);
        var approval = PendingReview(tenantId, budget.Id, submitterId);
        approval.CurrentApproverId = reviewerId;
        var priorAssignment = new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverUserId = Guid.NewGuid(),
            Position = 1,
            State = PaymentRequestAssignmentStates.Approved
        };
        var pendingAssignment = new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverUserId = reviewerId,
            Position = 2,
            State = PaymentRequestAssignmentStates.Pending
        };
        var historicalAction = new ApprovalAction
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverId = priorAssignment.ApproverUserId,
            ApproverAssignmentId = priorAssignment.Id,
            Action = "Approved",
            Comments = "First approval retained",
            ActionedAt = DateTime.UtcNow.AddMinutes(-5),
            ApprovalLevel = 1
        };

        await using var context = CreateContext();
        context.AddRange(NewProject(tenantId, projectId, 100_000m), budget, approval,
            priorAssignment, pendingAssignment, historicalAction);
        await context.SaveChangesAsync();
        budget.RowVersion = [1, 2, 3];
        var originalApprovalVersion = approval.RowVersion.ToArray();
        var (workflow, audit) = CreateWorkflow(context, tenantId, projectId, actorId,
            "ProjectAnnualBudget.View", "ProjectAnnualBudget.Withdraw", "ProjectAnnualBudget.Approve");

        var result = await workflow.WithdrawSubmissionAsync(tenantId, projectId, budget.Id, actorId,
            "Lifecycle Tester", new WithdrawProjectAnnualBudgetSubmissionDto(budget.RowVersion, originalApprovalVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ProjectAnnualBudgetStatuses.Draft, result.Value!.Status);
        var persistedApproval = await context.ApprovalRequests.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == approval.Id);
        Assert.Equal(PaymentRequestStatuses.Cancelled, persistedApproval.Status);
        Assert.Null(persistedApproval.CurrentApproverId);
        var assignments = await context.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .Where(item => item.ApprovalRequestId == approval.Id).ToListAsync();
        Assert.Contains(assignments, item => item.Id == priorAssignment.Id && !item.IsDeleted
            && item.State == PaymentRequestAssignmentStates.Approved);
        Assert.Contains(assignments, item => item.Id == pendingAssignment.Id && item.IsDeleted);
        Assert.Contains(await context.ApprovalActions.IgnoreQueryFilters()
            .Where(item => item.ApprovalRequestId == approval.Id).ToListAsync(),
            item => item.Id == historicalAction.Id && item.Comments == "First approval retained");
        Assert.Contains(await context.ApprovalActions.IgnoreQueryFilters()
            .Where(item => item.ApprovalRequestId == approval.Id).ToListAsync(), item => item.Action == "Withdrawn");
        audit.Verify(service => service.AppendAsync(
            It.Is<AuditEntry>(entry => entry.Action == "SubmissionWithdrawn"
                && entry.EntityId == budget.Id && entry.UserId == actorId),
            It.IsAny<CancellationToken>()), Times.Once);

        var staleDecision = await workflow.DecideReviewAsync(tenantId, projectId, budget.Id, approval.Id,
            reviewerId, "Reviewer", new DecideProjectAnnualBudgetReviewDto(
                "Approved", null, budget.RowVersion, originalApprovalVersion), CancellationToken.None);
        Assert.True(staleDecision.IsFailure);
        Assert.Equal(409, staleDecision.Error!.StatusCode);
        Assert.Equal(2, await context.ApprovalActions.IgnoreQueryFilters()
            .CountAsync(item => item.ApprovalRequestId == approval.Id));
    }

    [Fact]
    public async Task Withdrawing_pending_approved_revision_preserves_approved_budget_values()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var budget = NewBudget(tenantId, projectId, 2026, ProjectAnnualBudgetStatuses.Approved);
        budget.NewApprovedFunding = 40_000m;
        budget.ApprovedAdjustments = 3_000m;
        budget.ApprovedBy = reviewerId;
        budget.ApprovedAt = DateTime.UtcNow.AddDays(-3);
        var approval = PendingReview(tenantId, budget.Id, reviewerId);
        approval.Type = "ProjectAnnualBudgetRevision";
        approval.CurrentApproverId = actorId;
        var pendingAssignment = new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverUserId = actorId,
            State = PaymentRequestAssignmentStates.Pending
        };

        await using var context = CreateContext();
        context.AddRange(NewProject(tenantId, projectId, 100_000m), budget, approval, pendingAssignment);
        await context.SaveChangesAsync();
        budget.RowVersion = [4, 5, 6];
        var approvedTotal = budget.TotalAnnualBudget;
        var (workflow, _) = CreateWorkflow(context, tenantId, projectId, actorId,
            "ProjectAnnualBudget.View", "ProjectAnnualBudget.Withdraw");

        var result = await workflow.WithdrawSubmissionAsync(tenantId, projectId, budget.Id, actorId,
            "Lifecycle Tester", new WithdrawProjectAnnualBudgetSubmissionDto(
                budget.RowVersion, approval.RowVersion.ToArray()), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ProjectAnnualBudgetStatuses.Approved, result.Value!.Status);
        Assert.Equal(approvedTotal, result.Value.TotalAnnualBudget);
        Assert.Equal(40_000m, result.Value.NewApprovedFunding);
        Assert.Equal(3_000m, result.Value.ApprovedAdjustments);
        Assert.Equal(PaymentRequestStatuses.Cancelled,
            (await context.ApprovalRequests.IgnoreQueryFilters().SingleAsync(item => item.Id == approval.Id)).Status);
        Assert.True((await context.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == pendingAssignment.Id)).IsDeleted);
        Assert.Contains(await context.ApprovalActions.IgnoreQueryFilters()
            .Where(item => item.ApprovalRequestId == approval.Id).ToListAsync(), item => item.Action == "Withdrawn");
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        return new AppDbContext(options.Options);
    }

    private static (ProjectAnnualBudgetWorkflow Workflow, Mock<IAuditService> Audit) CreateWorkflow(
        AppDbContext context, Guid tenantId, Guid projectId, Guid actorId, params string[] effectivePermissions)
    {
        var calendar = new FiscalYearCalendarService();
        var fiscalPeriod = calendar.ForFiscalYear(2026, 1);
        var tenantFiscalYear = new Mock<ITenantFiscalYearService>();
        tenantFiscalYear.Setup(service => service.GetStartMonthAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        tenantFiscalYear.Setup(service => service.ResolveForTimestampAsync(tenantId,
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fiscalPeriod);
        var permissionService = new Mock<IUserEffectivePermissionService>();
        permissionService.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(effectivePermissions.ToHashSet(StringComparer.Ordinal));
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(actorId, tenantId, ManagementPerspective.ProjectManager,
            ManagementViewMode.Native, null, null, projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return (new ProjectAnnualBudgetWorkflow(context, tenantFiscalYear.Object, calendar,
            permissionService.Object, management, audit.Object,
            new NoopProjectAnnualBudgetApprovalNotificationPublisher(),
            Mock.Of<IManagementScopeResolver>()), audit);
    }

    private static Project NewProject(Guid tenantId, Guid projectId, decimal ceiling) => new()
    {
        Id = projectId,
        TenantId = tenantId,
        Name = "Annual Budget Lifecycle",
        Code = "ABL",
        Budget = ceiling,
        Currency = "USD"
    };

    private static ProjectAnnualBudget NewBudget(Guid tenantId, Guid projectId, int fiscalYear, string status)
    {
        var period = new FiscalYearCalendarService().ForFiscalYear(fiscalYear, 1);
        return new ProjectAnnualBudget
        {
            TenantId = tenantId,
            ProjectId = projectId,
            FiscalYear = fiscalYear,
            FiscalYearStartDate = period.StartDate,
            FiscalYearEndDate = period.EndDate,
            FinancialReference = $"ABL-FY{fiscalYear}",
            Currency = "USD",
            NewApprovedFunding = 50_000m,
            Status = status,
            RowVersion = [1, 2, 3]
        };
    }

    private static ApprovalRequest PendingReview(Guid tenantId, Guid budgetId, Guid submitterId) => new()
    {
        TenantId = tenantId,
        EntityType = "ProjectAnnualBudget",
        EntityId = budgetId,
        Type = "ProjectAnnualBudgetReview",
        Title = "Annual Budget FY 2026",
        Status = PaymentRequestStatuses.Pending,
        Priority = "Normal",
        Amount = 50_000m,
        Currency = "USD",
        RequestedBy = submitterId,
        RequestedAt = DateTime.UtcNow.AddMinutes(-10),
        CurrentApproverId = Guid.NewGuid()
    };
}
