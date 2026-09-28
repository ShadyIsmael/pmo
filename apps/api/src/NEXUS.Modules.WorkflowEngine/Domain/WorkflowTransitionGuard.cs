using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowTransitionGuard : BaseEntity
{
    public Guid WorkflowTransitionId { get; set; }
    public WorkflowTransition WorkflowTransition { get; set; } = null!;
    public string GuardType { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = "{}";
    public int ExecutionOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
