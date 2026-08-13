using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Domain;

public sealed class PortfolioSnapshot : BaseEntity
{
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;
    public int SnapshotNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;
    public DateTime EffectiveEndDate { get; set; }
    public Guid PreviousPortfolioManagerId { get; set; }
    public Guid NewPortfolioManagerId { get; set; }
    public string HandoverReason { get; set; } = string.Empty;
    public string? HandoverNotes { get; set; }
    public string? Data { get; set; }
}
