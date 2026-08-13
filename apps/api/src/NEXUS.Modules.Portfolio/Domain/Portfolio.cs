using NEXUS.SharedKernel;
using System.Text.Json.Serialization;

namespace NEXUS.Modules.Portfolio.Domain;

public class Portfolio : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? OrganizationId { get; set; }
    // Legacy columns remain for schema compatibility; portfolio summaries are not populated from them.
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? TotalBudget { get; set; }
    public decimal? ActualSpend { get; set; }
    public string? Health { get; set; }
    public string? Priority { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid? SponsorId { get; set; }
    public string? ConfidenceLevel { get; set; }
    public string? StrategicObjective { get; set; }
    public string? Notes { get; set; }
    [JsonIgnore]
    public ICollection<Program> Programs { get; set; } = new List<Program>();
    public ICollection<PortfolioHealth> HealthHistory { get; set; } = new List<PortfolioHealth>();
    public ICollection<PortfolioBudget> Budgets { get; set; } = new List<PortfolioBudget>();
}
