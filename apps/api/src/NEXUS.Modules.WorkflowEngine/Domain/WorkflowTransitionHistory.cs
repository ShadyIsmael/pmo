using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowTransitionHistory : BaseEntity
{
    public Guid WorkflowInstanceId { get; set; }
    public WorkflowInstance WorkflowInstance { get; set; } = null!;
    public Guid WorkflowTransitionId { get; set; }
    public WorkflowTransition WorkflowTransition { get; set; } = null!;
    public Guid FromStateId { get; set; }
    public WorkflowState FromState { get; set; } = null!;
    public Guid ToStateId { get; set; }
    public WorkflowState ToState { get; set; } = null!;
    public string ExecutedBy { get; set; } = string.Empty;
    public string? ExecutedByNameSnapshot { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public string? Comment { get; set; }
    public string? MetadataJson { get; set; }
    public string? CorrelationId { get; set; }
}
