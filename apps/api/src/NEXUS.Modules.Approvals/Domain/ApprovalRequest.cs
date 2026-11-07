using NEXUS.SharedKernel;

namespace NEXUS.Modules.Approvals.Domain;

public class ApprovalRequest : BaseEntity
{
    public const string StatusDraft = "Draft";
    public const string StatusSubmitted = "Submitted";
    public const string StatusApproved = "Approved";
    public const string StatusRejected = "Rejected";
    public const string StatusCancelled = "Cancelled";

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? EntityNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    // Currency is explicit for financial requests. Callers must use the owning
    // project's configured currency instead of silently inheriting GBP.
    public string? Currency { get; set; }
    public Guid? RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RequiredBy { get; set; }
    public Guid? CurrentApproverId { get; set; }
    public int ApprovalLevel { get; set; }
    public ICollection<ApprovalAction> Actions { get; set; } = new List<ApprovalAction>();

    public void Submit(string userId)
    {
        if (!string.Equals(Status, StatusDraft, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Only a {StatusDraft} request can be submitted. Current status: {Status}.");
        Status = StatusSubmitted;
        RequestedAt = RequestedAt == default ? DateTime.UtcNow : RequestedAt;
        UpdatedBy = userId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve(Guid approverId, string? comments, string userId)
    {
        if (!string.Equals(Status, StatusSubmitted, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Only a {StatusSubmitted} request can be approved. Current status: {Status}.");
        if (CurrentApproverId is null || !CurrentApproverId.Equals(approverId))
            throw new InvalidOperationException("Only the current assignee can approve this request.");
        Status = StatusApproved;
        RecordAction(approverId, "Approve", comments, ApprovalLevel + 1, userId);
        CurrentApproverId = null;
        UpdatedBy = userId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(Guid approverId, string? comments, string userId)
    {
        if (!string.Equals(Status, StatusSubmitted, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Only a {StatusSubmitted} request can be rejected. Current status: {Status}.");
        if (CurrentApproverId is null || !CurrentApproverId.Equals(approverId))
            throw new InvalidOperationException("Only the current assignee can reject this request.");
        if (string.IsNullOrWhiteSpace(comments))
            throw new InvalidOperationException("Rejection requires a comment.");
        Status = StatusRejected;
        RecordAction(approverId, "Reject", comments, ApprovalLevel + 1, userId);
        CurrentApproverId = null;
        UpdatedBy = userId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delegate(Guid toUserId, string? reason, string userId)
    {
        if (string.Equals(Status, StatusApproved, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Status, StatusRejected, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Status, StatusCancelled, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Cannot delegate a {Status} request.");
        if (toUserId == Guid.Empty)
            throw new InvalidOperationException("Delegate target user is required.");
        CurrentApproverId = toUserId;
        RecordAction(toUserId, "Delegate", reason, ApprovalLevel, userId);
        UpdatedBy = userId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(string userId)
    {
        if (!string.Equals(Status, StatusDraft, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Status, StatusSubmitted, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Only {StatusDraft} or {StatusSubmitted} requests can be cancelled. Current status: {Status}.");
        Status = StatusCancelled;
        UpdatedBy = userId;
        UpdatedAt = DateTime.UtcNow;
    }

    private void RecordAction(Guid approverId, string action, string? comments, int approvalLevel, string userId)
    {
        Actions.Add(new ApprovalAction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ApprovalRequestId = Id,
            ApproverId = approverId,
            Action = action,
            Comments = comments,
            ActionedAt = DateTime.UtcNow,
            ApprovalLevel = approvalLevel,
            CreatedBy = userId
        });
    }
}
