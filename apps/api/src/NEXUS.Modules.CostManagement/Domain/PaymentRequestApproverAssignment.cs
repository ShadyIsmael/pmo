using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

/// <summary>
/// Snapshot of one eligible project-team approver selected for a submitted
/// Payment Request. Submission freezes the assignments; later decisions are
/// recorded in the shared ApprovalAction table.
/// </summary>
public sealed class PaymentRequestApproverAssignment : BaseEntity
{
    public Guid ApprovalRequestId { get; set; }
    /// <summary>Project and cycle are nullable only for legacy assignments created before cycle snapshots.</summary>
    public Guid? ProjectId { get; set; }
    public Guid? ApprovalCycleId { get; set; }
    public Guid ApproverUserId { get; set; }

    /// <summary>Sequential position; null for Parallel assignments.</summary>
    public int? Position { get; set; }

    public string Requirement { get; set; } = PaymentRequestAssignmentRequirements.Mandatory;
    public string State { get; set; } = PaymentRequestAssignmentStates.Pending;
}
