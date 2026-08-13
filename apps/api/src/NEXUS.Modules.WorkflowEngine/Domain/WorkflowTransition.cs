using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowTransition : BaseEntity
{
    public Guid WorkflowTemplateId { get; set; }
    public WorkflowTemplate WorkflowTemplate { get; set; } = null!;
    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ConditionExpression { get; set; }
    public int SortOrder { get; set; }
    public ICollection<WorkflowCondition> Conditions { get; set; } = new List<WorkflowCondition>();
}
