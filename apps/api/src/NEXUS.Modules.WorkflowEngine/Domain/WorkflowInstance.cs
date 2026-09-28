using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowInstance : BaseEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public Guid WorkflowDefinitionVersionId { get; set; }
    public WorkflowDefinitionVersion WorkflowDefinitionVersion { get; set; } = null!;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid CurrentStateId { get; set; }
    public WorkflowState CurrentState { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public string StartedBy { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<WorkflowTransitionHistory> History { get; set; } = new List<WorkflowTransitionHistory>();
}
