using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public class BudgetRevision : BaseEntity
{
    public enum Statuses
    {
        Draft,
        Submitted,
        Approved,
        Rejected,
        Cancelled
    }

    public Guid ProjectId { get; set; }
    public Guid BudgetId { get; set; }
    public Budget? Budget { get; set; }
    public decimal PreviousApprovedAmount { get; set; }
    public decimal ProposedApprovedAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public Statuses Status { get; set; } = Statuses.Draft;
    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
}
