using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NEXUS.Api.Finance;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Finance;

public sealed class ProjectPaymentRequestApprovalNotificationPublisherTests
{
    [Fact]
    public async Task Sequential_submission_notifies_only_the_current_assigned_reviewer_and_reuses_cycle_identity()
    {
        var tenantId = Guid.NewGuid();
        var project = Project(tenantId);
        var requestor = User(tenantId, "Requestor");
        var prior = User(tenantId, "Prior Reviewer");
        var current = User(tenantId, "Current Reviewer");
        var next = User(tenantId, "Next Reviewer");
        var approval = Approval(tenantId, requestor.Id, current.Id);
        var cycleId = Guid.NewGuid();
        var assignments = new[]
        {
            Assignment(tenantId, project.Id, approval.Id, cycleId, prior.Id, 1, PaymentRequestAssignmentStates.Approved),
            Assignment(tenantId, project.Id, approval.Id, cycleId, current.Id, 2, PaymentRequestAssignmentStates.Pending),
            Assignment(tenantId, project.Id, approval.Id, cycleId, next.Id, 3, PaymentRequestAssignmentStates.Pending),
        };

        await using var context = CreateContext();
        context.Users.AddRange(requestor, prior, current, next);
        await context.SaveChangesAsync();
        var notifications = CreateNotificationServiceMock();
        var publisher = new ProjectPaymentRequestApprovalNotificationPublisher(context, notifications.Object);

        var first = await publisher.QueueActionableStepsAsync(project, approval, cycleId,
            PaymentRequestApprovalModes.Sequential, assignments,
            new HashSet<Guid> { requestor.Id, prior.Id, current.Id, next.Id }, CancellationToken.None);
        var firstNotification = Assert.Single(CapturedNotifications(notifications));
        var retry = await publisher.QueueActionableStepsAsync(project, approval, cycleId,
            PaymentRequestApprovalModes.Sequential, assignments,
            new HashSet<Guid> { requestor.Id, prior.Id, current.Id, next.Id }, CancellationToken.None);
        var allNotifications = CapturedNotifications(notifications);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(current.Id, firstNotification.UserId);
        Assert.Equal(NotificationTypes.ProjectPaymentRequestApproval, firstNotification.Type);
        Assert.Equal("Project", firstNotification.ReferenceType);
        Assert.Equal(project.Id, firstNotification.ReferenceId);
        Assert.Equal(project.PublicId, firstNotification.ReferencePublicId);
        Assert.Equal(firstNotification.Id, allNotifications[1].Id);
        Assert.DoesNotContain(allNotifications, item => item.UserId == requestor.Id || item.UserId == next.Id);
    }

    [Fact]
    public async Task Parallel_submission_notifies_all_pending_eligible_active_recipients_only()
    {
        var tenantId = Guid.NewGuid();
        var project = Project(tenantId);
        var requestor = User(tenantId, "Requestor");
        var first = User(tenantId, "First Reviewer");
        var second = User(tenantId, "Second Reviewer");
        var inactive = User(tenantId, "Inactive Reviewer");
        inactive.IsActive = false;
        var ineligible = User(tenantId, "Ineligible Reviewer");
        var approval = Approval(tenantId, requestor.Id, currentApproverId: null);
        var cycleId = Guid.NewGuid();
        var assignments = new[]
        {
            Assignment(tenantId, project.Id, approval.Id, cycleId, requestor.Id, null, PaymentRequestAssignmentStates.Pending),
            Assignment(tenantId, project.Id, approval.Id, cycleId, first.Id, null, PaymentRequestAssignmentStates.Pending),
            Assignment(tenantId, project.Id, approval.Id, cycleId, second.Id, null, PaymentRequestAssignmentStates.Pending),
            Assignment(tenantId, project.Id, approval.Id, cycleId, inactive.Id, null, PaymentRequestAssignmentStates.Pending),
            Assignment(tenantId, project.Id, approval.Id, cycleId, ineligible.Id, null, PaymentRequestAssignmentStates.Pending),
        };

        await using var context = CreateContext();
        context.Users.AddRange(requestor, first, second, inactive, ineligible);
        await context.SaveChangesAsync();
        var notifications = CreateNotificationServiceMock();
        var publisher = new ProjectPaymentRequestApprovalNotificationPublisher(context, notifications.Object);

        var result = await publisher.QueueActionableStepsAsync(project, approval, cycleId,
            PaymentRequestApprovalModes.Parallel, assignments,
            new HashSet<Guid> { requestor.Id, first.Id, second.Id, inactive.Id }, CancellationToken.None);
        var captured = CapturedNotifications(notifications);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id),
            captured.Select(item => item.UserId!.Value).OrderBy(id => id));
        Assert.All(captured, item => Assert.True(item.IsInAppVisible));
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static Mock<INotificationService> CreateNotificationServiceMock()
    {
        var service = new Mock<INotificationService>();
        service.Setup(item => item.SendIdempotentAsync(It.IsAny<Notification>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));
        return service;
    }

    private static IReadOnlyList<Notification> CapturedNotifications(Mock<INotificationService> service) =>
        service.Invocations.Where(invocation => invocation.Method.Name == nameof(INotificationService.SendIdempotentAsync))
            .Select(invocation => (Notification)invocation.Arguments[0]!)
            .ToArray();

    private static Project Project(Guid tenantId) => new()
    {
        TenantId = tenantId,
        Name = "Payment Request Project",
        Code = "PRP",
        Currency = "EGP",
    };

    private static User User(Guid tenantId, string firstName) => new()
    {
        TenantId = tenantId,
        FirstName = firstName,
        LastName = "Reviewer",
        Email = $"{Guid.NewGuid():N}@example.test",
        IsActive = true,
    };

    private static ApprovalRequest Approval(Guid tenantId, Guid requesterId, Guid? currentApproverId) => new()
    {
        TenantId = tenantId,
        EntityType = "ProjectPaymentRequest",
        EntityNumber = "PRP-PAY-0001",
        Type = "ProjectPaymentRequest",
        Status = PaymentRequestStatuses.Pending,
        RequestedBy = requesterId,
        CurrentApproverId = currentApproverId,
        Amount = 1250m,
        Currency = "EGP",
        Title = "Installment request",
    };

    private static PaymentRequestApproverAssignment Assignment(
        Guid tenantId, Guid projectId, Guid approvalId, Guid cycleId, Guid userId, int? position, string state) => new()
    {
        TenantId = tenantId,
        ProjectId = projectId,
        ApprovalRequestId = approvalId,
        ApprovalCycleId = cycleId,
        ApproverUserId = userId,
        Position = position,
        Requirement = PaymentRequestApproverRequirements.Mandatory,
        State = state,
    };
}
