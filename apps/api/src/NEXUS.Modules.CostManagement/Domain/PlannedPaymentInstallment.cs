using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

/// <summary>
/// A scheduled payment component within a Planned Payment. Advance Payments
/// are represented as ordinary installments; no separate aggregate is needed.
/// </summary>
public sealed class PlannedPaymentInstallment : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid PlannedPaymentId { get; set; }
    public int Sequence { get; set; }
    public decimal Amount { get; set; }
    public DateTime? DueDateUtc { get; set; }

    /// <summary>Actual amount confirmed settled against this installment.</summary>
    public decimal ActualPaid { get; set; }

    /// <summary>Outstanding balance: Amount minus ActualPaid.</summary>
    public decimal OutstandingBalance => Math.Max(0m, Amount - ActualPaid);

    /// <summary>Optional active working-plan milestone owned by the same project.</summary>
    public Guid? PlanActivityId { get; set; }

    public string? Description { get; set; }

    public PlannedPayment? PlannedPayment { get; set; }
}
