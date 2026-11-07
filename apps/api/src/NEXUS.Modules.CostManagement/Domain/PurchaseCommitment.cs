using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public class PurchaseCommitment : BaseEntity
{
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? VendorName { get; set; }
    public decimal Amount { get; set; }
    public decimal? InvoicedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CommitmentDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? BudgetId { get; set; }
    public Guid? ProjectId { get; set; }
}
