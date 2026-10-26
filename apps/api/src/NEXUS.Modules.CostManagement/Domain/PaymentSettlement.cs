using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public class PaymentSettlement : BaseEntity
{
    public Guid PaymentObligationId { get; set; }
    public PaymentObligation? PaymentObligation { get; set; }
    public Guid IdempotencyKey { get; set; }
    public decimal Amount { get; set; }
    public DateTime SettlementDate { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
