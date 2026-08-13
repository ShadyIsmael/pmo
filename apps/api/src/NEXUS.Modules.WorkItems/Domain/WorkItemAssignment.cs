using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkItems.Domain;

public class WorkItemAssignment : BaseEntity
{
    public Guid WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnassignedAt { get; set; }
}
