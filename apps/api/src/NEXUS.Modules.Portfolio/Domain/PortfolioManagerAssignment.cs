using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Domain;

public sealed class PortfolioManagerAssignment : BaseEntity
{
    public Guid PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;
    public Guid PortfolioManagerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public Guid? HandoverFromPortfolioManagerId { get; set; }
    public DateTime? HandoverDate { get; set; }
    public string? HandoverReason { get; set; }
    public string? HandoverNotes { get; set; }
}
