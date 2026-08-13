using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Domain;

public class PortfolioHealth : BaseEntity
{
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;
    public string Status { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public decimal? ScheduleVariance { get; set; }
    public decimal? CostVariance { get; set; }
    public Guid? RecordedBy { get; set; }
}
