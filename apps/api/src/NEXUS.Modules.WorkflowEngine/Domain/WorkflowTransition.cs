using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowTransition : BaseEntity
{
    public Guid WorkflowDefinitionVersionId { get; set; }
    public WorkflowDefinitionVersion WorkflowDefinitionVersion { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public Guid FromStateId { get; set; }
    public WorkflowState FromState { get; set; } = null!;
    public Guid ToStateId { get; set; }
    public WorkflowState ToState { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<WorkflowTransitionGuard> Guards { get; set; } = new List<WorkflowTransitionGuard>();
    public ICollection<WorkflowTransitionAction> Actions { get; set; } = new List<WorkflowTransitionAction>();
}
