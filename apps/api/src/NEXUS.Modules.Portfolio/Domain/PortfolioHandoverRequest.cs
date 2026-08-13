using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Domain;

public sealed class PortfolioHandoverRequest : BaseEntity
{
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;
    public Guid CurrentPortfolioManagerId { get; set; }
    public Guid NewPortfolioManagerId { get; set; }
    public DateTime EffectiveHandoverDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Status { get; set; } = ManagerHandoverStatus.Pending;
    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? RespondedBy { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? SnapshotId { get; set; }
}
