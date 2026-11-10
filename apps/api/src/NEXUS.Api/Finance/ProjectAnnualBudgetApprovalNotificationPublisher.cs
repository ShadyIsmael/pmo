using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Finance;

public interface IProjectAnnualBudgetApprovalNotificationPublisher
{
    Task<Result> QueueActionableStepsAsync(
        Project project,
        ProjectAnnualBudget budget,
        ApprovalRequest approval,
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments,
        IReadOnlyCollection<ApprovalAction> actions,
        IReadOnlySet<Guid> eligibleReviewerUserIds,
        CancellationToken cancellationToken);
}

/// <summary>
/// Publishes only the currently actionable reviewers for an Annual Budget approval
/// through the existing preference and delivery pipeline. The caller owns the
/// transaction so a notification is committed with the approval transition.
/// </summary>
public sealed class ProjectAnnualBudgetApprovalNotificationPublisher(
    AppDbContext context,
    INotificationService notifications) : IProjectAnnualBudgetApprovalNotificationPublisher
{
    private const string CreatedBy = "ProjectAnnualBudgetApprovalNotificationPublisher";
    private const string BudgetEntityType = "ProjectAnnualBudget";
    private const string ReviewApprovalType = "ProjectAnnualBudgetReview";
    private const string RevisionApprovalType = "ProjectAnnualBudgetRevision";

    public async Task<Result> QueueActionableStepsAsync(
        Project project,
        ProjectAnnualBudget budget,
        ApprovalRequest approval,
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments,
        IReadOnlyCollection<ApprovalAction> actions,
        IReadOnlySet<Guid> eligibleReviewerUserIds,
        CancellationToken cancellationToken)
    {
        if (project.Id == Guid.Empty || project.TenantId == Guid.Empty
            || budget.Id == Guid.Empty || budget.ProjectId != project.Id || budget.TenantId != project.TenantId
            || approval.TenantId != project.TenantId || approval.EntityType != BudgetEntityType
            || approval.EntityId != budget.Id
            || approval.Type is not (ReviewApprovalType or RevisionApprovalType)
            || approval.Id == Guid.Empty)
            return Result.Failure(Error.Validation("The Annual Budget approval notification scope is invalid."));

        if (!string.Equals(approval.Status, PaymentRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            return Result.Success();

        var actionedAssignmentIds = actions
            .Where(action => action.ApproverAssignmentId.HasValue)
            .Select(action => action.ApproverAssignmentId!.Value)
            .ToHashSet();
        var pendingAssignments = assignments
            .Where(assignment => assignment.TenantId == project.TenantId
                && assignment.ApprovalRequestId == approval.Id
                && !assignment.IsDeleted
                && assignment.State == PaymentRequestAssignmentStates.Pending
                && approval.RequestedBy != assignment.ApproverUserId
                && eligibleReviewerUserIds.Contains(assignment.ApproverUserId)
                && !actionedAssignmentIds.Contains(assignment.Id))
            .ToArray();

        IEnumerable<PaymentRequestApproverAssignment> actionableAssignments;
        if (pendingAssignments.Any(assignment => assignment.Position.HasValue))
        {
            var activeApproverId = approval.CurrentApproverId
                ?? pendingAssignments.OrderBy(assignment => assignment.Position)
                    .ThenBy(assignment => assignment.Id)
                    .Select(assignment => (Guid?)assignment.ApproverUserId)
                    .FirstOrDefault();
            actionableAssignments = activeApproverId.HasValue
                ? pendingAssignments.Where(assignment => assignment.ApproverUserId == activeApproverId.Value)
                : [];
        }
        else
        {
            // Parallel requests have no CurrentApproverId; every unacted assignment is actionable.
            actionableAssignments = pendingAssignments;
        }

        var recipients = actionableAssignments.Select(assignment => assignment.ApproverUserId)
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (recipients.Length == 0) return Result.Success();

        // Resolve by the assigned System User identity. Project Sponsors and Admin users
        // do not need a Resource profile to receive their assigned notification.
        var activeUsers = await context.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == project.TenantId && recipients.Contains(user.Id)
                && user.IsActive && !user.IsDeleted)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        var actionableByUser = actionableAssignments
            .Where(assignment => activeUsers.Contains(assignment.ApproverUserId))
            .GroupBy(assignment => assignment.ApproverUserId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var (recipientId, assignment) in actionableByUser)
        {
            var amount = approval.Amount?.ToString("0.00##", System.Globalization.CultureInfo.InvariantCulture) ?? "0";
            var title = "Annual Budget Approval Required\n\u0645\u0637\u0644\u0648\u0628 \u0627\u0639\u062A\u0645\u0627\u062F \u0627\u0644\u0645\u0648\u0627\u0632\u0646\u0629 \u0627\u0644\u0633\u0646\u0648\u064A\u0629";
            var body = $"Your review is required for FY {budget.FiscalYear}, reference {approval.EntityNumber} ({approval.Currency} {amount}).\n\n\u0645\u0631\u0627\u062C\u0639\u062A\u0643 \u0645\u0637\u0644\u0648\u0628\u0629 \u0644\u0645\u0648\u0627\u0632\u0646\u0629 \u0627\u0644\u0633\u0646\u0629 \u0627\u0644\u0645\u0627\u0644\u064A\u0629 {budget.FiscalYear} \u0627\u0644\u0645\u0631\u062C\u0639 {approval.EntityNumber} ({approval.Currency} {amount}).";
            var notification = new Notification
            {
                Id = CreateNotificationId(project.TenantId, approval.Id, assignment.Id, recipientId),
                TenantId = project.TenantId,
                UserId = recipientId,
                Type = NotificationTypes.ProjectAnnualBudgetApproval,
                ReferenceType = "Project",
                ReferenceId = project.Id,
                Title = title,
                Body = body,
                IsInAppVisible = true,
                SentAt = DateTime.UtcNow,
                CreatedBy = CreatedBy,
            };

            var queued = await notifications.SendIdempotentAsync(
                notification, allowPush: true, allowEmail: true, cancellationToken);
            if (queued.IsFailure)
                return Result.Failure(queued.Error ?? Error.Validation("The Annual Budget approval notification could not be queued."));
        }

        return Result.Success();
    }

    private static Guid CreateNotificationId(Guid tenantId, Guid approvalId, Guid assignmentId, Guid recipientId)
    {
        var identity = $"annual-budget-approval:{tenantId:D}:{approvalId:D}:{assignmentId:D}:{recipientId:D}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new Guid(hash.AsSpan(0, 16));
    }
}
