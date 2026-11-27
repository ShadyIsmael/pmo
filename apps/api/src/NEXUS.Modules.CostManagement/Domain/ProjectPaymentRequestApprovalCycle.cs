using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public static class ProjectPaymentRequestApprovalCycleStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string ReturnedForRevision = "ReturnedForRevision";
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// Immutable submission snapshot for one approval attempt on a Payment Request.
/// Decisions continue to use shared ApprovalAction rows and payment-request
/// approver assignments; this row gives each resubmission a stable cycle identity.
/// </summary>
public sealed class ProjectPaymentRequestApprovalCycle : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid ApprovalRequestId { get; set; }
    public int CycleNumber { get; set; }
    public string ApprovalMode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public Guid PlannedPaymentInstallmentId { get; set; }
    public Guid ProjectAnnualBudgetId { get; set; }
    public int FiscalYear { get; set; }
    public string AnnualBudgetReference { get; set; } = string.Empty;
    public Guid ProjectAnnualBudgetLineId { get; set; }
    public string AnnualBudgetLineReference { get; set; } = string.Empty;
    public string AnnualBudgetLineDescription { get; set; } = string.Empty;
    public Guid RequesterId { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public Guid SubmitIdempotencyKey { get; set; }
    public string SubmissionFingerprint { get; set; } = string.Empty;
    public string ApproversSnapshotJson { get; set; } = "[]";
    public string Status { get; set; } = ProjectPaymentRequestApprovalCycleStatuses.Pending;
    public DateTime? CompletedAtUtc { get; set; }
    public string? DecisionComment { get; set; }
}

