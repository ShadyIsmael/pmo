using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public class Budget : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? PortfolioId { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal ForecastAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int? FiscalQuarter { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Variance => ApprovedAmount - ActualAmount;
    public decimal VariancePercentage => ApprovedAmount > 0 ? (Variance / ApprovedAmount) * 100 : 0;
}
