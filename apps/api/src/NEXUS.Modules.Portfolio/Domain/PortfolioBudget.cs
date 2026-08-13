using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Domain;

public class PortfolioBudget : BaseEntity
{
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public int FiscalYear { get; set; }
    public int? FiscalQuarter { get; set; }
}
