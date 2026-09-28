using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public enum WorkflowDefinitionVersionLifecycleStatus
{
    Draft,
    Published,
    Retired
}

public class WorkflowDefinitionVersion : BaseEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public int VersionNumber { get; set; }
    public WorkflowDefinitionVersionLifecycleStatus LifecycleStatus { get; set; } = WorkflowDefinitionVersionLifecycleStatus.Draft;
    public string? ChangeLog { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public ICollection<WorkflowState> States { get; set; } = new List<WorkflowState>();
    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
    public ICollection<WorkflowInstance> Instances { get; set; } = new List<WorkflowInstance>();
}
