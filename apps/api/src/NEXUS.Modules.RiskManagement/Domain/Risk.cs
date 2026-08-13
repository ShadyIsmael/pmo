using NEXUS.SharedKernel;

namespace NEXUS.Modules.RiskManagement.Domain;

public class Risk : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? PortfolioId { get; set; }
    public Guid? CategoryId { get; set; }
    public RiskCategory? Category { get; set; }
    public string Impact { get; set; } = string.Empty;
    public string Likelihood { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? PotentialCost { get; set; }
    public decimal? PotentialDelay { get; set; }
    public string? RiskResponse { get; set; }
    public Guid? OwnerId { get; set; }
    public DateTime? IdentifiedDate { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public ICollection<RiskMitigation> Mitigations { get; set; } = new List<RiskMitigation>();
    public ICollection<RiskContingency> Contingencies { get; set; } = new List<RiskContingency>();
}
