using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? VendorName { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public decimal Amount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal TotalAmount => Amount + (TaxAmount ?? 0);
    public string Currency { get; set; } = "GBP";
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? BudgetId { get; set; }
    public Guid? ProjectId { get; set; }
}
