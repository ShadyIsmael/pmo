using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Api.Finance;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Finance;

public sealed class ProjectAnnualBudgetApprovalNotificationPublisherTests
{
    [Fact]
    public async Task Sequential_activation_notifies_only_the_current_assigned_system_user_without_resource_profile()
    {
        var tenantId = Guid.NewGuid();
        var project = Project(tenantId);
        var budget = Budget(tenantId, project.Id);
        var firstReviewer = User(tenantId, "First Reviewer");
        var currentReviewer = User(tenantId, "System Admin Sponsor");
        var approval = Approval(tenantId, budget.Id, currentReviewer.Id);
        var firstAssignment = Assignment(tenantId, approval.Id, firstReviewer.Id, 1, PaymentRequestAssignmentStates.Approved);
        var currentAssignment = Assignment(tenantId, approval.Id, currentReviewer.Id, 2, PaymentRequestAssignmentStates.Pending);
        var firstAction = new ApprovalAction
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverId = firstReviewer.Id,
            ApproverAssignmentId = firstAssignment.Id,
            Action = "Approved",
        };

        await using var context = CreateContext();
        context.AddRange(project, budget, firstReviewer, currentReviewer, approval, firstAssignment, currentAssignment, firstAction);
        await context.SaveChangesAsync();
        var publisher = CreatePublisher(context);

        var result = await publisher.QueueActionableStepsAsync(
            project, budget, approval, [firstAssignment, currentAssignment], [firstAction],
            new HashSet<Guid> { firstReviewer.Id, currentReviewer.Id }, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var notification = await context.Set<Notification>().SingleAsync();
        Assert.Equal(currentReviewer.Id, notification.UserId);
        Assert.Equal(NotificationTypes.ProjectAnnualBudgetApproval, notification.Type);
        Assert.Equal("Project", notification.ReferenceType);
        Assert.Equal(project.Id, notification.ReferenceId);
        Assert.True(notification.IsInAppVisible);
        Assert.DoesNotContain(await context.Set<Notification>().ToListAsync(), item => item.UserId == firstReviewer.Id);
    }

    [Fact]
    public async Task Parallel_activation_notifies_every_unacted_reviewer_and_retries_idempotently()
    {
        var tenantId = Guid.NewGuid();
        var project = Project(tenantId);
        var budget = Budget(tenantId, project.Id);
        var reviewers = new[] { User(tenantId, "Reviewer One"), User(tenantId, "Reviewer Two") };
        var ineligibleReviewer = User(tenantId, "Denied Reviewer");
        var approval = Approval(tenantId, budget.Id, currentApproverId: null);
        var assignments = reviewers.Append(ineligibleReviewer).Select(user => Assignment(
            tenantId, approval.Id, user.Id, position: null, PaymentRequestAssignmentStates.Pending)).ToArray();

        await using var context = CreateContext();
        context.AddRange(project, budget, approval);
        context.Users.AddRange(reviewers.Append(ineligibleReviewer));
        context.Set<PaymentRequestApproverAssignment>().AddRange(assignments);
        await context.SaveChangesAsync();
        var publisher = CreatePublisher(context);

        var firstResult = await publisher.QueueActionableStepsAsync(
            project, budget, approval, assignments, [], reviewers.Select(user => user.Id).ToHashSet(), CancellationToken.None);
        var firstIds = await context.Set<Notification>().Select(item => item.Id).ToArrayAsync();
        var retryResult = await publisher.QueueActionableStepsAsync(
            project, budget, approval, assignments, [], reviewers.Select(user => user.Id).ToHashSet(), CancellationToken.None);

        Assert.True(firstResult.IsSuccess, firstResult.Error?.Message);
        Assert.True(retryResult.IsSuccess, retryResult.Error?.Message);
        Assert.Equal(2, firstIds.Length);
        Assert.Equal(2, await context.Set<Notification>().CountAsync());
        Assert.Equal(reviewers.Select(user => user.Id).OrderBy(id => id).ToArray(),
            (await context.Set<Notification>().Select(item => item.UserId!.Value).ToArrayAsync())
                .OrderBy(id => id).ToArray());
        Assert.Equal(firstIds.OrderBy(id => id).ToArray(),
            (await context.Set<Notification>().Select(item => item.Id).ToArrayAsync()).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task Disabled_in_app_preference_is_preserved_while_canonical_notification_is_recorded()
    {
        var tenantId = Guid.NewGuid();
        var project = Project(tenantId);
        var budget = Budget(tenantId, project.Id);
        var reviewer = User(tenantId, "Preference Reviewer");
        var approval = Approval(tenantId, budget.Id, reviewer.Id);
        var assignment = Assignment(tenantId, approval.Id, reviewer.Id, 1, PaymentRequestAssignmentStates.Pending);

        await using var context = CreateContext();
        context.AddRange(project, budget, reviewer, approval, assignment);
        context.Set<NotificationPreference>().Add(new NotificationPreference
        {
            TenantId = tenantId,
            UserId = reviewer.Id,
            Type = NotificationTypes.ProjectAnnualBudgetApproval,
            InAppEnabled = false,
        });
        await context.SaveChangesAsync();

        var result = await CreatePublisher(context).QueueActionableStepsAsync(
            project, budget, approval, [assignment], [], new HashSet<Guid> { reviewer.Id }, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var notification = await context.Set<Notification>().SingleAsync();
        Assert.Equal(reviewer.Id, notification.UserId);
        Assert.False(notification.IsInAppVisible);
    }

    private static ProjectAnnualBudgetApprovalNotificationPublisher CreatePublisher(AppDbContext context)
    {
        var push = new Mock<IPushNotificationSender>();
        push.SetupGet(sender => sender.IsConfigured).Returns(false);
        var service = new NotificationService(context, push.Object,
            Options.Create(new NotificationDeliveryOptions()));
        return new ProjectAnnualBudgetApprovalNotificationPublisher(context, service);
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static Project Project(Guid tenantId) => new()
    {
        TenantId = tenantId,
        Name = "Budget Notification Project",
        Code = "BNP",
        Currency = "USD",
    };

    private static ProjectAnnualBudget Budget(Guid tenantId, Guid projectId) => new()
    {
        TenantId = tenantId,
        ProjectId = projectId,
        FiscalYear = 2027,
        FinancialReference = "BNP-FY2027-AB",
        Currency = "USD",
    };

    private static User User(Guid tenantId, string fullName)
    {
        var parts = fullName.Split(' ', 2);
        return new User
        {
            TenantId = tenantId,
            FirstName = parts[0],
            LastName = parts.Length > 1 ? parts[1] : string.Empty,
            Email = $"{Guid.NewGuid():N}@example.test",
            IsActive = true,
        };
    }

    private static ApprovalRequest Approval(Guid tenantId, Guid budgetId, Guid? currentApproverId) => new()
    {
        TenantId = tenantId,
        EntityId = budgetId,
        EntityType = "ProjectAnnualBudget",
        Type = "ProjectAnnualBudgetReview",
        Status = PaymentRequestStatuses.Pending,
        CurrentApproverId = currentApproverId,
        Amount = 125_000m,
        Currency = "USD",
        Title = "Review Annual Budget BNP-FY2027-AB",
    };

    private static PaymentRequestApproverAssignment Assignment(
        Guid tenantId, Guid approvalId, Guid userId, int? position, string state) => new()
    {
        TenantId = tenantId,
        ApprovalRequestId = approvalId,
        ApproverUserId = userId,
        Position = position,
        Requirement = PaymentRequestAssignmentRequirements.Mandatory,
        State = state,
    };
}

internal sealed class NoopProjectAnnualBudgetApprovalNotificationPublisher
    : IProjectAnnualBudgetApprovalNotificationPublisher
{
    public Task<Result> QueueActionableStepsAsync(
        Project project,
        ProjectAnnualBudget budget,
        ApprovalRequest approval,
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments,
        IReadOnlyCollection<ApprovalAction> actions,
        IReadOnlySet<Guid> eligibleReviewerUserIds,
        CancellationToken cancellationToken) => Task.FromResult(Result.Success());
}
