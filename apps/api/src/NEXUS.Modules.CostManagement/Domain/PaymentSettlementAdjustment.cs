using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

/// <summary>
/// Governed adjustment request against a confirmed <see cref="PaymentSettlement"/>.
/// The original settlement remains immutable; approval and execution are tracked here.
/// </summary>
public class PaymentSettlementAdjustment : BaseEntity
{
    public const string Refund = "Refund";
    public const string Correction = "Correction";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Executed = "Executed";

    public Guid OriginalSettlementId { get; set; }
    public PaymentSettlement? OriginalSettlement { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? PaymentObligationId { get; set; }
    public Guid? ProjectPaymentRequestId { get; set; }
    public Guid? PlannedPaymentInstallmentId { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime RecordedAt { get; set; }
    public Guid? ReplacementSettlementId { get; set; }
    public PaymentSettlement? ReplacementSettlement { get; set; }
    public Guid IdempotencyKey { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public Guid? AssignedApproverId { get; set; }
    /// <summary>Legacy rows default to Executed; new requests begin PendingApproval.</summary>
    public string Status { get; set; } = Executed;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ExecutionIdempotencyKey { get; set; }
    public DateTime? ExecutionDate { get; set; }
    public Guid? ExecutionConfirmedBy { get; set; }
    public DateTime? ExecutionConfirmedAtUtc { get; set; }
}
