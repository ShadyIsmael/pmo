using NEXUS.SharedKernel;

namespace NEXUS.Modules.Approvals.Domain;

public class ApprovalRequest : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? EntityNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? Currency { get; set; } = "GBP";
    public Guid? RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RequiredBy { get; set; }
    public Guid? CurrentApproverId { get; set; }
    public int ApprovalLevel { get; set; }
    public ICollection<ApprovalAction> Actions { get; set; } = new List<ApprovalAction>();
}
