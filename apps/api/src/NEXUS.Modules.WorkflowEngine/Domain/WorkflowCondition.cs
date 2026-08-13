using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowCondition : BaseEntity
{
    public Guid WorkflowTransitionId { get; set; }
    public WorkflowTransition WorkflowTransition { get; set; } = null!;
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
