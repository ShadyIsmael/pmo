using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Domain;

public class Program : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;
    public string? Status { get; set; }
    public Guid? ProgramManagerId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
    public decimal? ActualSpend { get; set; }
    public string? Health { get; set; }
}
