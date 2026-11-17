using NEXUS.Api.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Api.Finance;
using NEXUS.Modules.Approvals.Domain;
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

public sealed class ProjectAnnualBudgetClosingRecoveryTests
{
    [Fact]
    public void Recovery_routes_require_close_approval_and_assignment_recovery_policies()
    {
        foreach (var methodName in new[]
                 {
                     nameof(ProjectAnnualBudgetClosingController.GetRecoveryApprovers),
                     nameof(ProjectAnnualBudgetClosingController.ReassignApprover),
                     nameof(ProjectAnnualBudgetClosingController.RecoveryCancel)
                 })
        {
            var method = typeof(ProjectAnnualBudgetClosingController).GetMethod(methodName);
            Assert.NotNull(method);
            var policies = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Select(attribute => attribute.Policy)
                .ToArray();
            Assert.Contains("ProjectAnnualBudget.CloseApprove", policies);
            Assert.Contains("Governance.AssignmentRecovery.Manage", policies);
        }

        Assert.Contains("ProjectAnnualBudget.CloseApprove", PermissionCatalog.ByKey.Keys);
        Assert.Contains("Governance.AssignmentRecovery.Manage", PermissionCatalog.ByKey.Keys);
    }

    [Fact]
    public async Task Recovery_options_exclude_the_preparer_and_any_already_assigned_approver()
    {
        await using var fixture = await CreateFixtureAsync(includeReplacement: true, includePreparerAsCandidate: true);
        using var tenant = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var result = await fixture.Workflow.GetEligibleClosingRecoveryApproversAsync(
            fixture.TenantId, fixture.ProjectId, fixture.ClosingId, fixture.AssignmentId,
            fixture.ActorId, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value!);
        Assert.Equal(fixture.ReplacementId, result.Value![0].UserId);
        Assert.DoesNotContain(result.Value, item => item.UserId == fixture.PreparerId);
        Assert.DoesNotContain(result.Value, item => item.UserId == fixture.FormerApproverId);
    }

    [Fact]
    public async Task Recovery_reassigns_the_pending_sequential_approver_and_writes_atomic_audit()
    {
        await using var fixture = await CreateFixtureAsync(includeReplacement: true);
        using var tenant = TrustedTenantExecutionContext.Enter(fixture.TenantId);
        var previousApprovalVersion = fixture.Approval.RowVersion.ToArray();

        var result = await fixture.Workflow.ReassignClosingApproverAsync(
            fixture.TenantId, fixture.ProjectId, fixture.ClosingId, fixture.ActorId, "Recovery Manager",
            new ReassignProjectAnnualBudgetClosingApproverDto(
                fixture.AssignmentId, fixture.ReplacementId, "Assigned reviewer is inactive",
                fixture.Closing.RowVersion, fixture.Approval.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.ReplacementId, fixture.Assignment.ApproverUserId);
        Assert.Equal(fixture.ReplacementId, fixture.Approval.CurrentApproverId);
        var audit = await fixture.Context.AuditEntries.IgnoreQueryFilters()
            .SingleAsync(item => item.EntityId == fixture.ClosingId && item.Action == "ClosingApproverReassigned");
        Assert.Contains("Assigned reviewer is inactive", audit.Description);
        Assert.Contains(fixture.FormerApproverId.ToString("D"), audit.OriginalValues ?? string.Empty);
        Assert.Contains(fixture.ReplacementId.ToString("D"), audit.NewValues ?? string.Empty);
        Assert.False(result.Value!.CanApprove);
        Assert.True(result.Value.CanReassignApprover);
        Assert.NotNull(result.Value.ApprovalRowVersion);
        Assert.False(previousApprovalVersion.SequenceEqual(result.Value.ApprovalRowVersion!));
    }

    [Fact]
    public async Task Recovery_rejects_the_closing_preparer_as_a_replacement()
    {
        await using var fixture = await CreateFixtureAsync(includeReplacement: true, includePreparerAsCandidate: true);
        using var tenant = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var result = await fixture.Workflow.ReassignClosingApproverAsync(
            fixture.TenantId, fixture.ProjectId, fixture.ClosingId, fixture.ActorId, null,
            new ReassignProjectAnnualBudgetClosingApproverDto(
                fixture.AssignmentId, fixture.PreparerId, "Avoid self-approval", fixture.Closing.RowVersion,
                fixture.Approval.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error?.StatusCode);
        Assert.Equal(fixture.FormerApproverId, fixture.Assignment.ApproverUserId);
        Assert.Empty(await fixture.Context.AuditEntries.IgnoreQueryFilters()
            .Where(item => item.EntityId == fixture.ClosingId && item.Action == "ClosingApproverReassigned")
            .ToListAsync());
    }

    [Fact]
    public async Task Recovery_cannot_reassign_an_approver_who_remains_eligible()
    {
        await using var fixture = await CreateFixtureAsync(
            includeReplacement: true, includeFormerApproverAsCandidate: true);
        using var tenant = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var result = await fixture.Workflow.ReassignClosingApproverAsync(
            fixture.TenantId, fixture.ProjectId, fixture.ClosingId, fixture.ActorId, "Recovery Manager",
            new ReassignProjectAnnualBudgetClosingApproverDto(
                fixture.AssignmentId, fixture.ReplacementId, "Attempted reassignment before eligibility loss",
                fixture.Closing.RowVersion, fixture.Approval.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error?.StatusCode);
        Assert.Equal(fixture.FormerApproverId, fixture.Assignment.ApproverUserId);
        Assert.Empty(await fixture.Context.AuditEntries.IgnoreQueryFilters()
            .Where(item => item.EntityId == fixture.ClosingId && item.Action == "ClosingApproverReassigned")
            .ToListAsync());
    }

    [Fact]
    public async Task Recovery_controls_are_not_offered_when_the_assigned_reviewer_remains_eligible()
    {
        await using var fixture = await CreateFixtureAsync(
            includeReplacement: false, includeFormerApproverAsCandidate: true);
        using var tenant = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var result = await fixture.Workflow.GetClosingsAsync(
            fixture.TenantId, fixture.ProjectId, fixture.ActorId, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var closing = Assert.Single(result.Value!.Items);
        Assert.False(closing.CanRecoverCancel);
        Assert.False(closing.CanReassignApprover);
        var assignment = Assert.Single(closing.ApproverDecisions);
        Assert.False(assignment.CanReassignAssignment);
        Assert.False(assignment.CanRecoverCancelAssignment);
    }

    [Fact]
    public async Task Reassignment_requires_the_assignment_recovery_permission()
    {
        await using var fixture = await CreateFixtureAsync(includeReplacement: true, grantRecoveryPermission: false);
        using var tenant = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var result = await fixture.Workflow.ReassignClosingApproverAsync(
            fixture.TenantId, fixture.ProjectId, fixture.ClosingId, fixture.ActorId, null,
            new ReassignProjectAnnualBudgetClosingApproverDto(
                fixture.AssignmentId, fixture.ReplacementId, "Recovery test", fixture.Closing.RowVersion,
                fixture.Approval.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Equal(fixture.FormerApproverId, fixture.Assignment.ApproverUserId);
        Assert.Empty(await fixture.Context.AuditEntries.IgnoreQueryFilters()
            .Where(item => item.EntityId == fixture.ClosingId && item.Action == "ClosingApproverReassigned")
            .ToListAsync());
    }

    [Fact]
    public async Task Reassignment_rejects_a_replacement_outside_the_current_eligible_team()
    {
        await using var fixture = await CreateFixtureAsync(includeReplacement: false);
        using var tenant = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var result = await fixture.Workflow.ReassignClosingApproverAsync(
            fixture.TenantId, fixture.ProjectId, fixture.ClosingId, fixture.ActorId, null,
            new ReassignProjectAnnualBudgetClosingApproverDto(
                fixture.AssignmentId, fixture.ReplacementId, "Replacement is not eligible", fixture.Closing.RowVersion,
                fixture.Approval.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error?.StatusCode);
        Assert.Equal(fixture.FormerApproverId, fixture.Assignment.ApproverUserId);
    }

    [Fact]
    public async Task Preparer_cancellation_updates_all_pending_assignments_and_preserves_completed_steps()
    {
        await using var fixture = await CreateFixtureAsync(includeReplacement: true);
        using var tenant = TrustedTenantExecutionContext.Enter(fixture.TenantId);
        var secondPending = new PaymentRequestApproverAssignment
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, ApprovalRequestId = fixture.Approval.Id,
            ApproverUserId = fixture.ReplacementId, Position = 2, Requirement = PaymentRequestAssignmentRequirements.Mandatory,
            State = PaymentRequestAssignmentStates.Pending
        };
        var completed = new PaymentRequestApproverAssignment
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, ApprovalRequestId = fixture.Approval.Id,
            ApproverUserId = Guid.NewGuid(), Position = 0, Requirement = PaymentRequestAssignmentRequirements.Mandatory,
            State = PaymentRequestAssignmentStates.Approved
        };
        fixture.Context.PaymentRequestApproverAssignments.AddRange(secondPending, completed);
        fixture.Context.ApprovalActions.Add(new ApprovalAction
        {
            TenantId = fixture.TenantId, ApprovalRequestId = fixture.Approval.Id,
            ApproverId = completed.ApproverUserId, ApproverAssignmentId = completed.Id,
            Action = PaymentRequestAssignmentStates.Approved, Comments = "Completed before cancellation",
            ActionedAt = DateTime.UtcNow.AddMinutes(-5), ApprovalLevel = 0, CreatedBy = completed.ApproverUserId.ToString("D")
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Management.Set(new ManagementContext(
            fixture.PreparerId, fixture.TenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, fixture.ProjectId, new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(),
                new HashSet<Guid> { fixture.ProjectId })));

        var result = await fixture.Workflow.CancelClosingAsync(
            fixture.TenantId, fixture.ProjectId, fixture.ClosingId, fixture.PreparerId, "Closing Preparer",
            new CancelProjectAnnualBudgetClosingDto(fixture.Closing.RowVersion, fixture.Approval.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ProjectAnnualBudgetClosingStatuses.Cancelled, fixture.Closing.Status);
        Assert.Equal(PaymentRequestStatuses.Cancelled, fixture.Approval.Status);
        Assert.Null(fixture.Approval.CurrentApproverId);
        Assert.Equal(PaymentRequestAssignmentStates.Cancelled, fixture.Assignment.State);
        Assert.Equal(PaymentRequestAssignmentStates.Cancelled, secondPending.State);
        Assert.Equal(PaymentRequestAssignmentStates.Approved, completed.State);
        var actions = await fixture.Context.ApprovalActions.IgnoreQueryFilters()
            .Where(item => item.ApprovalRequestId == fixture.Approval.Id).ToListAsync();
        Assert.Equal(3, actions.Count);
        Assert.Equal(2, actions.Count(item => item.Action == PaymentRequestAssignmentStates.Cancelled));
        Assert.Single(actions.Where(item => item.Action == PaymentRequestAssignmentStates.Approved));
        Assert.Contains(actions, item => item.ApproverAssignmentId == secondPending.Id
            && item.Action == PaymentRequestAssignmentStates.Cancelled);
        Assert.Single(await fixture.Context.AuditEntries.IgnoreQueryFilters()
            .Where(item => item.EntityId == fixture.ClosingId && item.Action == "YearEndClosingCancelled").ToListAsync());
    }

    [Fact]
    public async Task Recovery_cancellation_terminates_pending_steps_when_no_eligible_replacement_exists()
    {
        await using var fixture = await CreateFixtureAsync(includeReplacement: false);
        using var tenant = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var result = await fixture.Workflow.RecoverCancelClosingAsync(
            fixture.TenantId, fixture.ProjectId, fixture.ClosingId, fixture.ActorId, "Recovery Manager",
            new RecoverCancelProjectAnnualBudgetClosingDto(
                fixture.AssignmentId, fixture.Closing.RowVersion, fixture.Approval.RowVersion,
                "No active eligible closing approver remains"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ProjectAnnualBudgetClosingStatuses.Cancelled, fixture.Closing.Status);
        Assert.Equal(PaymentRequestStatuses.Cancelled, fixture.Approval.Status);
        Assert.Null(fixture.Approval.CurrentApproverId);
        Assert.Equal(PaymentRequestAssignmentStates.Cancelled, fixture.Assignment.State);
        var action = await fixture.Context.ApprovalActions.IgnoreQueryFilters()
            .SingleAsync(item => item.ApproverAssignmentId == fixture.AssignmentId);
        Assert.Equal(PaymentRequestAssignmentStates.Cancelled, action.Action);
        Assert.Equal("No active eligible closing approver remains", action.Comments);
        var audit = await fixture.Context.AuditEntries.IgnoreQueryFilters()
            .SingleAsync(item => item.EntityId == fixture.ClosingId && item.Action == "YearEndClosingRecoveryCancelled");
        Assert.Contains("No active eligible closing approver remains", audit.Description);
        Assert.False(result.Value!.CanRecoverCancel);
        Assert.False(result.Value.CanReassignApprover);
    }

    [Fact]
    public async Task Recovery_cancellation_requires_reason_and_does_not_change_the_pending_closing()
    {
        await using var fixture = await CreateFixtureAsync(includeReplacement: false);
        using var tenant = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var result = await fixture.Workflow.RecoverCancelClosingAsync(
            fixture.TenantId, fixture.ProjectId, fixture.ClosingId, fixture.ActorId, null,
            new RecoverCancelProjectAnnualBudgetClosingDto(
                fixture.AssignmentId, fixture.Closing.RowVersion, fixture.Approval.RowVersion, " "),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error?.StatusCode);
        Assert.Equal(ProjectAnnualBudgetClosingStatuses.PendingApproval, fixture.Closing.Status);
        Assert.Equal(PaymentRequestStatuses.Pending, fixture.Approval.Status);
        Assert.Equal(PaymentRequestAssignmentStates.Pending, fixture.Assignment.State);
    }

    [Fact]
    public async Task Recovery_cancellation_is_blocked_when_an_eligible_replacement_exists()
    {
        await using var fixture = await CreateFixtureAsync(includeReplacement: true);
        using var tenant = TrustedTenantExecutionContext.Enter(fixture.TenantId);

        var result = await fixture.Workflow.RecoverCancelClosingAsync(
            fixture.TenantId, fixture.ProjectId, fixture.ClosingId, fixture.ActorId, null,
            new RecoverCancelProjectAnnualBudgetClosingDto(
                fixture.AssignmentId, fixture.Closing.RowVersion, fixture.Approval.RowVersion,
                "Use reassignment while an eligible replacement exists"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error?.StatusCode);
        Assert.Equal(ProjectAnnualBudgetClosingStatuses.PendingApproval, fixture.Closing.Status);
        Assert.Equal(PaymentRequestStatuses.Pending, fixture.Approval.Status);
        Assert.Equal(PaymentRequestAssignmentStates.Pending, fixture.Assignment.State);
    }

    private static async Task<RecoveryFixture> CreateFixtureAsync(
        bool includeReplacement,
        bool includePreparerAsCandidate = false,
        bool grantRecoveryPermission = true,
        bool includeFormerApproverAsCandidate = false)
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var preparerId = Guid.NewGuid();
        var formerApproverId = Guid.NewGuid();
        var replacementId = Guid.NewGuid();
        var closingId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var context = CreateContext();
        var project = new Project
        {
            Id = projectId,
            TenantId = tenantId,
            PublicId = Guid.NewGuid(),
            Name = "Closing recovery project",
            Code = "CLOSE-RECOVERY",
            Currency = "EGP",
            Budget = 50_000m
        };
        var preparer = User(tenantId, preparerId, "Closing", "Preparer");
        var formerApprover = User(tenantId, formerApproverId, "Former", "Approver");
        formerApprover.IsActive = false;
        var request = new ApprovalRequest
        {
            Id = approvalId,
            TenantId = tenantId,
            Title = "Year-end closing approval",
            EntityType = "Project",
            EntityId = projectId,
            Type = "ProjectAnnualBudgetClosing",
            Status = PaymentRequestStatuses.Pending,
            Priority = "Normal",
            RequestedBy = preparerId,
            RequestedAt = now,
            CurrentApproverId = formerApproverId,
            RowVersion = [1, 2, 3]
        };
        var closing = new ProjectAnnualBudgetClosing
        {
            Id = closingId,
            TenantId = tenantId,
            ProjectId = projectId,
            SourceAnnualBudgetId = Guid.NewGuid(),
            FiscalYear = 2026,
            FiscalYearStartDate = new DateOnly(2025, 7, 1),
            FiscalYearEndDate = new DateOnly(2026, 6, 30),
            Currency = "EGP",
            Status = ProjectAnnualBudgetClosingStatuses.PendingApproval,
            ApprovalMode = PaymentRequestApprovalModes.Sequential,
            ApprovalRequestId = approvalId,
            PreparedBy = preparerId,
            SubmittedBy = preparerId,
            PreparedAtUtc = now.AddHours(-2),
            SubmittedAtUtc = now.AddHours(-1),
            SnapshotFingerprint = "snapshot-fingerprint",
            SnapshotJson = "{}",
            RowVersion = [8, 9, 10]
        };
        var assignment = new PaymentRequestApproverAssignment
        {
            Id = assignmentId,
            TenantId = tenantId,
            ApprovalRequestId = approvalId,
            ApproverUserId = formerApproverId,
            Position = 1,
            Requirement = PaymentRequestAssignmentRequirements.Mandatory,
            State = PaymentRequestAssignmentStates.Pending
        };
        var rows = new List<object> { project, preparer, formerApprover, request, closing, assignment };
        var permissionMap = new Dictionary<Guid, string[]>
        {
            [actorId] = grantRecoveryPermission
                ? ["ProjectAnnualBudgetClosing.View", "ProjectAnnualBudget.CloseApprove", "Governance.AssignmentRecovery.Manage"]
                : ["ProjectAnnualBudgetClosing.View", "ProjectAnnualBudget.CloseApprove"],
            [preparerId] = ["ProjectAnnualBudget.CloseApprove", "ProjectAnnualBudget.Close"]
        };
        if (includeReplacement)
        {
            var replacement = User(tenantId, replacementId, "Current", "Approver");
            rows.Add(replacement);
            var linked = ResourceMember(tenantId, projectId, replacementId, now);
            rows.Add(linked.Resource);
            rows.Add(linked.Member);
            permissionMap[replacementId] = ["ProjectAnnualBudget.CloseApprove"];
        }
        if (includeFormerApproverAsCandidate)
        {
            formerApprover.IsActive = true;
            var linked = ResourceMember(tenantId, projectId, formerApproverId, now);
            rows.Add(linked.Resource);
            rows.Add(linked.Member);
            permissionMap[formerApproverId] = ["ProjectAnnualBudget.CloseApprove"];
        }
        if (includePreparerAsCandidate)
        {
            var linked = ResourceMember(tenantId, projectId, preparerId, now);
            rows.Add(linked.Resource);
            rows.Add(linked.Member);
        }

        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        context.AddRange(rows);
        await context.SaveChangesAsync();

        var permissions = new Mock<IUserEffectivePermissionService>();
        foreach (var pair in permissionMap)
        {
            var userId = pair.Key;
            var keys = new HashSet<string>(pair.Value, StringComparer.Ordinal);
            permissions.Setup(service => service.GetEffectivePermissionsAsync(
                    userId, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(keys);
        }
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        var workflow = new ProjectPaymentRequestWorkflow(
            context,
            Mock.Of<IProjectMilestoneReferenceLookup>(),
            Mock.Of<ITenantFiscalYearService>(),
            new FiscalYearCalendarService(),
            permissions.Object,
            management);
        return new RecoveryFixture(context, workflow, management, tenantId, projectId, actorId, preparerId,
            formerApproverId, replacementId, closing, request, assignment, closingId, assignmentId);
    }

    private static (ProjectMember Member, Resource Resource) ResourceMember(Guid tenantId, Guid projectId, Guid userId, DateTime now)
    {
        var resource = new Resource
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Project approver",
            Type = "Employee",
            Status = "Active",
            IsActive = true
        };
        var member = new ProjectMember
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ResourceId = resource.Id,
            Role = "Finance Approver",
            Status = "Active",
            StartDate = now.Date.AddDays(-1),
            EndDate = now.Date.AddDays(1)
        };
        return (member, resource);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static User User(Guid tenantId, Guid userId, string firstName, string lastName) => new()
    {
        Id = userId,
        TenantId = tenantId,
        Email = $"{userId:N}@example.test",
        FirstName = firstName,
        LastName = lastName,
        IsActive = true
    };

    private sealed class RecoveryFixture(
        AppDbContext context,
        ProjectPaymentRequestWorkflow workflow,
        IManagementContextAccessor management,
        Guid tenantId,
        Guid projectId,
        Guid actorId,
        Guid preparerId,
        Guid formerApproverId,
        Guid replacementId,
        ProjectAnnualBudgetClosing closing,
        ApprovalRequest approval,
        PaymentRequestApproverAssignment assignment,
        Guid closingId,
        Guid assignmentId) : IAsyncDisposable
    {
        public AppDbContext Context { get; } = context;
        public ProjectPaymentRequestWorkflow Workflow { get; } = workflow;
        public IManagementContextAccessor Management { get; } = management;
        public Guid TenantId { get; } = tenantId;
        public Guid ProjectId { get; } = projectId;
        public Guid ActorId { get; } = actorId;
        public Guid PreparerId { get; } = preparerId;
        public Guid FormerApproverId { get; } = formerApproverId;
        public Guid ReplacementId { get; } = replacementId;
        public ProjectAnnualBudgetClosing Closing { get; } = closing;
        public ApprovalRequest Approval { get; } = approval;
        public PaymentRequestApproverAssignment Assignment { get; } = assignment;
        public Guid ClosingId { get; } = closingId;
        public Guid AssignmentId { get; } = assignmentId;

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
