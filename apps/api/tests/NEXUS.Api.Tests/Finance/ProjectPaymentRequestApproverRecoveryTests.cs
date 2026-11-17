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

public sealed class ProjectPaymentRequestApproverRecoveryTests
{
    [Fact]
    public void Reassignment_endpoint_requires_approval_and_recovery_policies()
    {
        var method = typeof(ProjectPaymentRequestController).GetMethod(nameof(ProjectPaymentRequestController.ReassignApprover));
        Assert.NotNull(method);
        var policies = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();

        Assert.Contains("ProjectPaymentRequest.Approve", policies);
        Assert.Contains("Governance.AssignmentRecovery.Manage", policies);
        Assert.Contains("ProjectPaymentRequest.Approve", PermissionCatalog.ByKey.Keys);
        Assert.Contains("Governance.AssignmentRecovery.Manage", PermissionCatalog.ByKey.Keys);
    }

    [Fact]
    public async Task Reassignment_replaces_ineligible_reviewer_and_audits_reason()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var formerApproverId = Guid.NewGuid();
        var replacementId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var context = CreateContext();

        var project = new Project
        {
            Id = projectId,
            TenantId = tenantId,
            PublicId = Guid.NewGuid(),
            Name = "Approver recovery project",
            Code = "APPROVER-RECOVERY",
            Currency = "EGP",
            Budget = 100_000m
        };
        var request = new ApprovalRequest
        {
            Id = requestId,
            TenantId = tenantId,
            Title = "Payment Request recovery",
            EntityType = "Project",
            EntityId = projectId,
            EntityNumber = "PR-RECOVERY-001",
            Type = "ProjectPaymentRequest",
            Status = PaymentRequestStatuses.Pending,
            Priority = "Normal",
            Amount = 5_000m,
            Currency = "EGP",
            RequestedBy = requesterId,
            RequestedAt = now.AddHours(-1),
            CurrentApproverId = formerApproverId,
            RowVersion = [1, 2, 3]
        };
        var detail = new ProjectPaymentRequestDetail
        {
            ApprovalRequestId = requestId,
            TenantId = tenantId,
            ProjectId = projectId,
            ApprovalMode = PaymentRequestApprovalModes.Sequential,
            SubmittedAt = now.AddMinutes(-30),
            RowVersion = [1, 2, 3]
        };
        var formerAssignment = new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = requestId,
            ApproverUserId = formerApproverId,
            Position = 1,
            Requirement = PaymentRequestAssignmentRequirements.Mandatory,
            State = PaymentRequestAssignmentStates.Pending
        };
        var requester = User(tenantId, requesterId, "Payment", "Requester");
        var actor = User(tenantId, actorId, "Recovery", "Manager");
        var formerApprover = User(tenantId, formerApproverId, "Former", "Reviewer");
        var replacement = User(tenantId, replacementId, "Current", "Reviewer");
        var formerResource = new Resource
        {
            TenantId = tenantId, UserId = formerApproverId, Name = "Former Reviewer",
            Type = "Employee", Status = "Active", IsActive = true
        };
        var formerMembership = new ProjectMember
        {
            TenantId = tenantId, ProjectId = projectId, ResourceId = formerResource.Id,
            Role = "Finance Reviewer", Status = "Active", StartDate = now.Date.AddDays(-1), EndDate = now.Date.AddDays(1)
        };
        var resource = new Resource
        {
            TenantId = tenantId,
            UserId = replacementId,
            Name = "Current Reviewer",
            Type = "Employee",
            Status = "Active",
            IsActive = true
        };
        var membership = new ProjectMember
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ResourceId = resource.Id,
            Role = "Finance Reviewer",
            Status = "Active",
            StartDate = now.Date.AddDays(-1),
            EndDate = now.Date.AddDays(1)
        };
        context.AddRange(project, request, detail, formerAssignment, requester, actor,
            formerApprover, replacement, formerResource, formerMembership, resource, membership);
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await context.SaveChangesAsync();

        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsAsync(
                actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>([
                "ProjectPaymentRequest.Approve",
                "ProjectPaymentRequest.View",
                "Governance.AssignmentRecovery.Manage"
            ], StringComparer.Ordinal));
        permissions.Setup(service => service.GetEffectivePermissionsAsync(
                replacementId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["ProjectPaymentRequest.Approve"], StringComparer.Ordinal));
        permissions.Setup(service => service.GetEffectivePermissionsAsync(
                formerApproverId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["ProjectPaymentRequest.Approve"], StringComparer.Ordinal));
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

        var beforeEligibilityLoss = await workflow.GetByIdAsync(tenantId, projectId, requestId, actorId, CancellationToken.None);
        Assert.True(beforeEligibilityLoss.IsSuccess, beforeEligibilityLoss.Error?.Message);
        Assert.False(Assert.Single(beforeEligibilityLoss.Value!.Approvers).CanReassign);

        var stillEligible = await workflow.ReassignApproverAsync(
            tenantId, projectId, requestId, actorId, "Recovery Manager",
            new ReassignPaymentRequestApproverDto(
                formerApproverId, replacementId, "Attempted reassignment before eligibility loss", request.RowVersion),
            CancellationToken.None);
        Assert.True(stillEligible.IsFailure);
        Assert.Equal(409, stillEligible.Error?.StatusCode);
        Assert.Equal(formerApproverId, formerAssignment.ApproverUserId);

        formerApprover.IsActive = false;
        await context.SaveChangesAsync();

        var afterEligibilityLoss = await workflow.GetByIdAsync(tenantId, projectId, requestId, actorId, CancellationToken.None);
        Assert.True(afterEligibilityLoss.IsSuccess, afterEligibilityLoss.Error?.Message);
        Assert.True(Assert.Single(afterEligibilityLoss.Value!.Approvers).CanReassign);

        var recoveryOptions = await workflow.GetEligibleRecoveryApproversAsync(
            tenantId, projectId, requestId, formerApproverId, actorId, CancellationToken.None);
        Assert.True(recoveryOptions.IsSuccess, recoveryOptions.Error?.Message);
        Assert.Equal(replacementId, Assert.Single(recoveryOptions.Value!).UserId);

        var result = await workflow.ReassignApproverAsync(
            tenantId,
            projectId,
            requestId,
            actorId,
            "Recovery Manager",
            new ReassignPaymentRequestApproverDto(
                formerApproverId,
                replacementId,
                "Former reviewer is inactive",
                request.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(replacementId, formerAssignment.ApproverUserId);
        Assert.Equal(replacementId, request.CurrentApproverId);
        var afterReassignment = await workflow.GetByIdAsync(tenantId, projectId, requestId, actorId, CancellationToken.None);
        Assert.True(afterReassignment.IsSuccess, afterReassignment.Error?.Message);
        Assert.False(Assert.Single(afterReassignment.Value!.Approvers).CanReassign);
        var audit = await context.AuditEntries.IgnoreQueryFilters()
            .SingleAsync(item => item.EntityId == requestId && item.Action == "ApproverReassigned");
        Assert.Contains("Former reviewer is inactive", audit.Description);
        Assert.Contains(replacementId.ToString("D"), audit.NewValues ?? string.Empty);
    }

    [Fact]
    public async Task Reassignment_requires_recovery_permission_even_when_actor_can_approve()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        await using var context = CreateContext();
        var request = new ApprovalRequest
        {
            Id = requestId,
            TenantId = tenantId,
            Title = "Pending request",
            EntityType = "Project",
            EntityId = projectId,
            Type = "ProjectPaymentRequest",
            Status = PaymentRequestStatuses.Pending,
            RequestedBy = Guid.NewGuid(),
            RowVersion = [4, 5, 6]
        };
        context.AddRange(
            new Project { Id = projectId, TenantId = tenantId, PublicId = Guid.NewGuid(), Name = "Project", Code = "PRJ", Currency = "EGP" },
            request,
            new ProjectPaymentRequestDetail { ApprovalRequestId = requestId, TenantId = tenantId, ProjectId = projectId, ApprovalMode = PaymentRequestApprovalModes.Parallel },
            User(tenantId, actorId, "Approver", "Only"));
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await context.SaveChangesAsync();

        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["ProjectPaymentRequest.Approve"], StringComparer.Ordinal));
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId, new ManagementScope(
                new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        var workflow = new ProjectPaymentRequestWorkflow(
            context, Mock.Of<IProjectMilestoneReferenceLookup>(), Mock.Of<ITenantFiscalYearService>(),
            new FiscalYearCalendarService(), permissions.Object, management);

        var result = await workflow.ReassignApproverAsync(
            tenantId, projectId, requestId, actorId, null,
            new ReassignPaymentRequestApproverDto(Guid.NewGuid(), Guid.NewGuid(), "Recovery needed", request.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
    }

    [Fact]
    public async Task Reassignment_rechecks_recovery_permission_inside_tenant_mutation_guard()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var context = CreateContext();
        var permissions = new Mock<IUserEffectivePermissionService>();
        var granted = new HashSet<string>([
            "ProjectPaymentRequest.Approve",
            "Governance.AssignmentRecovery.Manage"
        ], StringComparer.Ordinal);
        permissions.SetupSequence(service => service.GetEffectivePermissionsAsync(
                actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted)
            .ReturnsAsync(granted)
            .ReturnsAsync(new HashSet<string>(["ProjectPaymentRequest.Approve"], StringComparer.Ordinal));
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId, new ManagementScope(
                new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        var workflow = new ProjectPaymentRequestWorkflow(
            context, Mock.Of<IProjectMilestoneReferenceLookup>(), Mock.Of<ITenantFiscalYearService>(),
            new FiscalYearCalendarService(), permissions.Object, management);

        var result = await workflow.ReassignApproverAsync(
            tenantId, projectId, Guid.NewGuid(), actorId, null,
            new ReassignPaymentRequestApproverDto(Guid.NewGuid(), Guid.NewGuid(), "Permission revoked", [1]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
    }

    [Fact]
    public async Task Reversal_approver_lookup_excludes_requester_without_saved_policy()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var context = CreateContext();
        var project = new Project
        {
            Id = projectId,
            TenantId = tenantId,
            PublicId = Guid.NewGuid(),
            Name = "Reversal self-approval project",
            Code = "REV-SELF-APPROVAL",
            Currency = "EGP"
        };
        var requester = User(tenantId, requesterId, "Request", "Creator");
        var reviewer = User(tenantId, reviewerId, "Eligible", "Reviewer");
        var requesterResource = new Resource
        {
            TenantId = tenantId, UserId = requesterId, Name = "Request Creator",
            Type = "Employee", Status = "Active", IsActive = true
        };
        var reviewerResource = new Resource
        {
            TenantId = tenantId, UserId = reviewerId, Name = "Eligible Reviewer",
            Type = "Employee", Status = "Active", IsActive = true
        };
        context.AddRange(project, requester, reviewer, requesterResource, reviewerResource,
            new ProjectMember
            {
                TenantId = tenantId, ProjectId = projectId, ResourceId = requesterResource.Id,
                Role = "Finance Reviewer", Status = "Active", StartDate = now.Date.AddDays(-1), EndDate = now.Date.AddDays(1)
            },
            new ProjectMember
            {
                TenantId = tenantId, ProjectId = projectId, ResourceId = reviewerResource.Id,
                Role = "Finance Reviewer", Status = "Active", StartDate = now.Date.AddDays(-1), EndDate = now.Date.AddDays(1)
            });
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await context.SaveChangesAsync();

        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsAsync(
                requesterId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>([
                "ProjectPaymentReversal.Create", "ProjectPaymentReversal.Approve"
            ], StringComparer.Ordinal));
        permissions.Setup(service => service.GetEffectivePermissionsAsync(
                reviewerId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["ProjectPaymentReversal.Approve"], StringComparer.Ordinal));
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            requesterId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId, new ManagementScope(
                new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        var workflow = new ProjectPaymentRequestWorkflow(
            context, Mock.Of<IProjectMilestoneReferenceLookup>(), Mock.Of<ITenantFiscalYearService>(),
            new FiscalYearCalendarService(), permissions.Object, management);

        var result = await workflow.GetEligibleReversalApproversAsync(
            tenantId, projectId, requesterId, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.DoesNotContain(result.Value!, item => item.UserId == requesterId);
        Assert.Contains(result.Value!, item => item.UserId == reviewerId);
    }

    [Fact]
    public async Task Reversal_reassignment_replaces_ineligible_reviewer_and_audits_reason()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var formerApproverId = Guid.NewGuid();
        var replacementId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var reversalId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var context = CreateContext();

        var project = new Project
        {
            Id = projectId,
            TenantId = tenantId,
            PublicId = Guid.NewGuid(),
            Name = "Reversal recovery project",
            Code = "REV-RECOVERY",
            Currency = "EGP",
            Budget = 20_000m
        };
        var original = new ApprovalRequest
        {
            Id = requestId,
            TenantId = tenantId,
            Title = "Approved Payment Request",
            EntityType = "Project",
            EntityId = projectId,
            EntityNumber = "PR-REV-RECOVERY-001",
            Type = "ProjectPaymentRequest",
            Status = PaymentRequestStatuses.Approved,
            Amount = 5_000m,
            Currency = "EGP",
            RequestedBy = requesterId,
            RequestedAt = now.AddDays(-1)
        };
        var originalDetail = new ProjectPaymentRequestDetail
        {
            ApprovalRequestId = requestId,
            TenantId = tenantId,
            ProjectId = projectId,
            RequestKind = ProjectPaymentRequestKinds.PaymentRequest
        };
        var reversal = new ApprovalRequest
        {
            Id = reversalId,
            TenantId = tenantId,
            Title = "Pending Reversal Request",
            EntityType = "Project",
            EntityId = projectId,
            EntityNumber = "PPR-REV-RECOVERY-001",
            Type = "ProjectPaymentRequestReversal",
            Status = PaymentRequestStatuses.Pending,
            Amount = 1_000m,
            Currency = "EGP",
            RequestedBy = requesterId,
            RequestedAt = now.AddHours(-1),
            CurrentApproverId = formerApproverId,
            RowVersion = [7, 8, 9]
        };
        var reversalDetail = new ProjectPaymentRequestDetail
        {
            ApprovalRequestId = reversalId,
            TenantId = tenantId,
            ProjectId = projectId,
            RequestKind = ProjectPaymentRequestKinds.ReversalRequest,
            ReversesApprovalRequestId = requestId,
            ApprovalMode = PaymentRequestApprovalModes.Sequential
        };
        var assignment = new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = reversalId,
            ApproverUserId = formerApproverId,
            Position = 1,
            Requirement = PaymentRequestAssignmentRequirements.Mandatory,
            State = PaymentRequestAssignmentStates.Pending
        };
        var actor = User(tenantId, actorId, "Recovery", "Manager");
        var requester = User(tenantId, requesterId, "Reversal", "Requester");
        var formerApprover = User(tenantId, formerApproverId, "Former", "Reviewer");
        var replacement = User(tenantId, replacementId, "Current", "Reviewer");
        var formerResource = new Resource
        {
            TenantId = tenantId, UserId = formerApproverId, Name = "Former Reviewer",
            Type = "Employee", Status = "Active", IsActive = true
        };
        var formerMembership = new ProjectMember
        {
            TenantId = tenantId, ProjectId = projectId, ResourceId = formerResource.Id,
            Role = "Finance Reviewer", Status = "Active", StartDate = now.Date.AddDays(-1), EndDate = now.Date.AddDays(1)
        };
        var resource = new Resource
        {
            TenantId = tenantId,
            UserId = replacementId,
            Name = "Current Reviewer",
            Type = "Employee",
            Status = "Active",
            IsActive = true
        };
        var membership = new ProjectMember
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ResourceId = resource.Id,
            Role = "Finance Reviewer",
            Status = "Active",
            StartDate = now.Date.AddDays(-1),
            EndDate = now.Date.AddDays(1)
        };
        context.AddRange(project, original, originalDetail, reversal, reversalDetail, assignment,
            actor, requester, formerApprover, replacement, formerResource, formerMembership, resource, membership);
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await context.SaveChangesAsync();

        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsAsync(
                actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>([
                "ProjectPaymentReversal.Approve",
                "ProjectPaymentRequest.View",
                "Governance.AssignmentRecovery.Manage"
            ], StringComparer.Ordinal));
        permissions.Setup(service => service.GetEffectivePermissionsAsync(
                replacementId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["ProjectPaymentReversal.Approve"], StringComparer.Ordinal));
        permissions.Setup(service => service.GetEffectivePermissionsAsync(
                formerApproverId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["ProjectPaymentReversal.Approve"], StringComparer.Ordinal));
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            actorId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, projectId, new ManagementScope(
                new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));
        var workflow = new ProjectPaymentRequestWorkflow(
            context, Mock.Of<IProjectMilestoneReferenceLookup>(), Mock.Of<ITenantFiscalYearService>(),
            new FiscalYearCalendarService(), permissions.Object, management);

        var beforeEligibilityLoss = await workflow.GetReversalsAsync(tenantId, projectId, requestId, actorId, CancellationToken.None);
        Assert.True(beforeEligibilityLoss.IsSuccess, beforeEligibilityLoss.Error?.Message);
        Assert.False(Assert.Single(Assert.Single(beforeEligibilityLoss.Value!.Items).Approvers).CanReassign);

        var stillEligible = await workflow.ReassignReversalApproverAsync(
            tenantId, projectId, requestId, reversalId, actorId, "Recovery Manager",
            new ReassignPaymentRequestApproverDto(
                formerApproverId, replacementId, "Attempted reassignment before eligibility loss", reversal.RowVersion),
            CancellationToken.None);
        Assert.True(stillEligible.IsFailure);
        Assert.Equal(409, stillEligible.Error?.StatusCode);
        Assert.Equal(formerApproverId, assignment.ApproverUserId);

        formerApprover.IsActive = false;
        await context.SaveChangesAsync();

        var afterEligibilityLoss = await workflow.GetReversalsAsync(tenantId, projectId, requestId, actorId, CancellationToken.None);
        Assert.True(afterEligibilityLoss.IsSuccess, afterEligibilityLoss.Error?.Message);
        Assert.True(Assert.Single(Assert.Single(afterEligibilityLoss.Value!.Items).Approvers).CanReassign);

        var recoveryOptions = await workflow.GetEligibleReversalRecoveryApproversAsync(
            tenantId, projectId, requestId, reversalId, formerApproverId, actorId, CancellationToken.None);
        Assert.True(recoveryOptions.IsSuccess, recoveryOptions.Error?.Message);
        Assert.Equal(replacementId, Assert.Single(recoveryOptions.Value!).UserId);

        var result = await workflow.ReassignReversalApproverAsync(
            tenantId, projectId, requestId, reversalId, actorId, "Recovery Manager",
            new ReassignPaymentRequestApproverDto(
                formerApproverId, replacementId, "Former reviewer is inactive", reversal.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(replacementId, assignment.ApproverUserId);
        Assert.Equal(replacementId, reversal.CurrentApproverId);
        var afterReassignment = await workflow.GetReversalsAsync(tenantId, projectId, requestId, actorId, CancellationToken.None);
        Assert.True(afterReassignment.IsSuccess, afterReassignment.Error?.Message);
        Assert.False(Assert.Single(Assert.Single(afterReassignment.Value!.Items).Approvers).CanReassign);
        var audit = await context.AuditEntries.IgnoreQueryFilters()
            .SingleAsync(item => item.EntityId == reversalId && item.Action == "ApproverReassigned");
        Assert.Contains("Former reviewer is inactive", audit.Description);
        Assert.Contains(replacementId.ToString("D"), audit.NewValues ?? string.Empty);
    }

    [Fact]
    public void Reversal_reassignment_endpoint_requires_approval_and_recovery_policies()
    {
        var method = typeof(ProjectPaymentRequestController).GetMethod(
            nameof(ProjectPaymentRequestController.ReassignReversalApprover));
        Assert.NotNull(method);
        var policies = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();

        Assert.Contains("ProjectPaymentReversal.Approve", policies);
        Assert.Contains("Governance.AssignmentRecovery.Manage", policies);
    }

    [Theory]
    [InlineData(nameof(ProjectPaymentRequestController.GetPaymentRequestRecoveryApprovers), "ProjectPaymentRequest.Approve")]
    [InlineData(nameof(ProjectPaymentRequestController.GetPaymentRequestRecoveryApprovers), "Governance.AssignmentRecovery.Manage")]
    [InlineData(nameof(ProjectPaymentRequestController.GetReversalRecoveryApprovers), "ProjectPaymentReversal.Approve")]
    [InlineData(nameof(ProjectPaymentRequestController.GetReversalRecoveryApprovers), "Governance.AssignmentRecovery.Manage")]
    public void Recovery_option_endpoints_require_approval_and_recovery_policies(string methodName, string expectedPolicy)
    {
        var method = typeof(ProjectPaymentRequestController).GetMethod(methodName);
        Assert.NotNull(method);
        var policies = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();

        Assert.Contains(expectedPolicy, policies);
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
}
