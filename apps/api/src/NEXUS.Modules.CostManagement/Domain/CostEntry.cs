using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public class CostEntry : BaseEntity
{
    public Guid? BudgetId { get; set; }
    public Budget? Budget { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ResourceId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public decimal? TaxAmount { get; set; }
    public DateTime IncurredDate { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? VendorName { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? IncurredBy { get; set; }
}
