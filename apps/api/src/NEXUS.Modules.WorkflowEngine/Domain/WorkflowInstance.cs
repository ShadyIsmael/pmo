using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowInstance : BaseEntity
{
    public Guid WorkflowTemplateId { get; set; }
    public WorkflowTemplate WorkflowTemplate { get; set; } = null!;
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string CurrentState { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<WorkflowState> States { get; set; } = new List<WorkflowState>();
}
