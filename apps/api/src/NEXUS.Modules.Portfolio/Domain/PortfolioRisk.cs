using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Domain;

public class PortfolioRisk : BaseEntity
{
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Likelihood { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? PotentialCost { get; set; }
    public string? MitigationPlan { get; set; }
}
