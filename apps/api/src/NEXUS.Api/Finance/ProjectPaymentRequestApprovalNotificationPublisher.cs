using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Finance;

public interface IProjectPaymentRequestApprovalNotificationPublisher
{
    Task<Result> QueueActionableStepsAsync(
        Project project,
        ApprovalRequest approval,
        Guid approvalCycleId,
        string approvalMode,
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments,
        IReadOnlySet<Guid> eligibleReviewerUserIds,
        CancellationToken cancellationToken);
}

/// <summary>Queues only actionable assigned reviewers through the existing notification pipeline.</summary>
public sealed class ProjectPaymentRequestApprovalNotificationPublisher(
    AppDbContext context,
    INotificationService notifications) : IProjectPaymentRequestApprovalNotificationPublisher
{
    private const string CreatedBy = "ProjectPaymentRequestApprovalNotificationPublisher";

    public async Task<Result> QueueActionableStepsAsync(
        Project project,
        ApprovalRequest approval,
        Guid approvalCycleId,
        string approvalMode,
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments,
        IReadOnlySet<Guid> eligibleReviewerUserIds,
        CancellationToken cancellationToken)
    {
        if (project.Id == Guid.Empty || project.TenantId == Guid.Empty
            || approval.TenantId != project.TenantId || approval.Id == Guid.Empty
            || approvalCycleId == Guid.Empty
            || !string.Equals(approval.Status, PaymentRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase)
            || approvalMode is not (PaymentRequestApprovalModes.Sequential or PaymentRequestApprovalModes.Parallel))
            return Result.Failure(Error.Validation("The Payment Request approval notification scope is invalid."));

        var pending = assignments.Where(item => item.TenantId == project.TenantId
                && item.ProjectId == project.Id && item.ApprovalRequestId == approval.Id
                && item.ApprovalCycleId == approvalCycleId && !item.IsDeleted
                && item.State == PaymentRequestAssignmentStates.Pending
                && item.ApproverUserId != approval.RequestedBy
                && eligibleReviewerUserIds.Contains(item.ApproverUserId))
            .ToArray();
        if (string.Equals(approvalMode, PaymentRequestApprovalModes.Sequential, StringComparison.Ordinal))
        {
            var activeApprover = approval.CurrentApproverId ?? pending.OrderBy(item => item.Position)
                .ThenBy(item => item.Id).Select(item => (Guid?)item.ApproverUserId).FirstOrDefault();
            pending = activeApprover.HasValue
                ? pending.Where(item => item.ApproverUserId == activeApprover.Value).ToArray()
                : [];
        }
        if (pending.Length == 0) return Result.Success();

        var recipients = pending.Select(item => item.ApproverUserId).Distinct().ToArray();
        var activeUsers = await context.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == project.TenantId && recipients.Contains(user.Id)
                && user.IsActive && !user.IsDeleted)
            .Select(user => user.Id).ToListAsync(cancellationToken);
        var actionable = pending.Where(item => activeUsers.Contains(item.ApproverUserId))
            .GroupBy(item => item.ApproverUserId).Select(group => group.First()).ToArray();
        var amount = approval.Amount?.ToString("0.####", CultureInfo.InvariantCulture) ?? "0";
        var title = "Payment Request Approval Required\n\u0645\u0637\u0644\u0648\u0628 \u0627\u0639\u062a\u0645\u0627\u062f \u0637\u0644\u0628 \u0627\u0644\u062f\u0641\u0639";
        var body = $"Your approval is required for {approval.EntityNumber} on {project.Name} (cycle {approvalCycleId:D}), {approval.Currency} {amount}.\n\n\u0645\u0637\u0644\u0648\u0628 \u0627\u0639\u062a\u0645\u0627\u062f\u0643 \u0644\u0637\u0644\u0628 \u0627\u0644\u062f\u0641\u0639 {approval.EntityNumber} \u0641\u064a {project.Name} (\u062f\u0648\u0631\u0629 {approvalCycleId:D})\u060c {approval.Currency} {amount}.";
        foreach (var assignment in actionable)
        {
            var notification = new Notification
            {
                Id = CreateNotificationId(project.TenantId, approval.Id, approvalCycleId, assignment.Id, assignment.ApproverUserId),
                TenantId = project.TenantId,
                UserId = assignment.ApproverUserId,
                Type = NotificationTypes.ProjectPaymentRequestApproval,
                ReferenceType = "Project",
                ReferenceId = project.Id,
                ReferencePublicId = project.PublicId,
                Title = title,
                Body = body,
                IsInAppVisible = true,
                SentAt = DateTime.UtcNow,
                CreatedBy = CreatedBy
            };
            var queued = await notifications.SendIdempotentAsync(notification, allowPush: true, allowEmail: true, cancellationToken);
            if (queued.IsFailure)
                return Result.Failure(queued.Error ?? Error.Validation("The Payment Request approval notification could not be queued."));
        }
        return Result.Success();
    }

    private static Guid CreateNotificationId(Guid tenantId, Guid approvalId, Guid cycleId, Guid assignmentId, Guid recipientId)
    {
        var identity = $"payment-request-approval:{tenantId:D}:{approvalId:D}:{cycleId:D}:{assignmentId:D}:{recipientId:D}";
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16));
    }
}
