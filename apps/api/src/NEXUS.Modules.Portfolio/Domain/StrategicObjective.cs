using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Domain;

public class StrategicObjective : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int SortOrder { get; set; }
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;
    public string? Status { get; set; }
    public decimal? Progress { get; set; }
    public DateTime? TargetDate { get; set; }
}
