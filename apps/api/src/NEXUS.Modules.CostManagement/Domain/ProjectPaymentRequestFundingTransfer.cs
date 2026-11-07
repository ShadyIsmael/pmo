using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

/// <summary>
/// Append-only transfer of the remaining unpaid portion of one approved request
/// from its current annual budget to the next fiscal year's budget.
/// </summary>
public sealed class ProjectPaymentRequestFundingTransfer : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid PaymentRequestId { get; set; }
    public Guid SourceAnnualBudgetId { get; set; }
    public int SourceFiscalYear { get; set; }
    public Guid DestinationAnnualBudgetId { get; set; }
    public int DestinationFiscalYear { get; set; }
    public Guid ClosingId { get; set; }
    public Guid? SupersedesFundingTransferId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public Guid ApprovedBy { get; set; }
    public DateTime ApprovedAtUtc { get; set; }
}
