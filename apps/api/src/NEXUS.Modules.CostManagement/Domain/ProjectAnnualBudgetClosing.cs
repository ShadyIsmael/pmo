using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public static class ProjectAnnualBudgetClosingStatuses
{
    public const string Review = "Review";
    public const string PendingApproval = "PendingApproval";
    public const string Rejected = "Rejected";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// Immutable project and fiscal-year closing statement plus its shared ApprovalRequest link.
/// Only the final approved transition may close the source budget or create funding transfers.
/// </summary>
public sealed class ProjectAnnualBudgetClosing : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid SourceAnnualBudgetId { get; set; }
    public int FiscalYear { get; set; }
    public DateOnly FiscalYearStartDate { get; set; }
    public DateOnly FiscalYearEndDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public Guid? DestinationAnnualBudgetId { get; set; }
    public int? DestinationFiscalYear { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public string Status { get; set; } = ProjectAnnualBudgetClosingStatuses.Review;
    public string? ApprovalMode { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string SnapshotFingerprint { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = "{}";

    public decimal ApprovedAnnualBudget { get; set; }
    public decimal NewFunding { get; set; }
    public decimal IncomingCarryForward { get; set; }
    public decimal ApprovedAdjustments { get; set; }
    public decimal ApprovedPaymentRequestAllocations { get; set; }
    public decimal ApprovedReversalReleases { get; set; }
    public decimal EffectiveActiveAllocations { get; set; }
    public decimal ActualSettlementsInFiscalYear { get; set; }
    public decimal OutstandingCommitments { get; set; }
    public decimal FreeAvailableBudget { get; set; }
    public decimal TotalUnspentBudget { get; set; }
    public decimal ProposedFreeCarryForward { get; set; }
    public bool CarryOutstandingCommitments { get; set; }
    public decimal ApprovedFreeCarryForward { get; set; }
    public decimal ApprovedReservedCarryForward { get; set; }
    public decimal NonCarriedFreeBalance { get; set; }
    public decimal NextFiscalYearNewFunding { get; set; }
    public decimal NextFiscalYearAdjustments { get; set; }

    public int PendingPaymentRequestCount { get; set; }
    public int PendingReversalRequestCount { get; set; }
    public int UnresolvedLegacyRequestCount { get; set; }
    public int IntegrityIssueCount { get; set; }
    public string? IntegrityIssuesJson { get; set; }

    public Guid? PreparedBy { get; set; }
    public DateTime? PreparedAtUtc { get; set; }
    public Guid? SubmittedBy { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? CompletedBy { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
