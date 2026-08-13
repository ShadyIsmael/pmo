using NEXUS.SharedKernel;

namespace NEXUS.Modules.Approvals.Domain;

public class ApprovalAction : BaseEntity
{
    public Guid ApprovalRequestId { get; set; }
    public ApprovalRequest ApprovalRequest { get; set; } = null!;
    public Guid ApproverId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime ActionedAt { get; set; } = DateTime.UtcNow;
    public int ApprovalLevel { get; set; }
}
