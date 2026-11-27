using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public class PaymentSettlement : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid? PaymentObligationId { get; set; }
    public PaymentObligation? PaymentObligation { get; set; }
    public Guid? ProjectPaymentRequestId { get; set; }
    public ProjectPaymentRequestDetail? ProjectPaymentRequestDetail { get; set; }
    public Guid? PlannedPaymentInstallmentId { get; set; }
    public PlannedPaymentInstallment? PlannedPaymentInstallment { get; set; }
    public Guid? FundingTransferId { get; set; }
    public ProjectPaymentRequestFundingTransfer? FundingTransfer { get; set; }
    public Guid IdempotencyKey { get; set; }
    public decimal Amount { get; set; }
    public DateTime SettlementDate { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Currency { get; set; }
    public string? Description { get; set; }
}
