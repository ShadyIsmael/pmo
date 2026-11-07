using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public class PaymentSettlement : BaseEntity
{
    /// <summary>Legacy planned-payment owner. Exactly one settlement owner is required.</summary>
    public Guid? PaymentObligationId { get; set; }
    public PaymentObligation? PaymentObligation { get; set; }
    /// <summary>Approved project Payment Request owner for the unified settlement ledger.</summary>
    public Guid? ProjectPaymentRequestId { get; set; }
    public ProjectPaymentRequestDetail? ProjectPaymentRequest { get; set; }
    /// <summary>Current funding assignment when this payment is made against a carried obligation.</summary>
    public Guid? FundingTransferId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public decimal Amount { get; set; }
    /// <summary>
    /// Historical obligation settlements may not have a provable currency because
    /// the legacy schema stored none and obligation currency could later change.
    /// New Payment Request settlements always persist the approved request currency.
    /// </summary>
    public string? Currency { get; set; }
    public DateTime SettlementDate { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Notes { get; set; }
}
