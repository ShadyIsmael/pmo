using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowState : BaseEntity
{
    public Guid WorkflowInstanceId { get; set; }
    public WorkflowInstance WorkflowInstance { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExitedAt { get; set; }
    public string? EnteredBy { get; set; }
}
