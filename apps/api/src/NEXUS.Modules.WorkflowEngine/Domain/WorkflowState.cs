using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowState : BaseEntity
{
    public Guid WorkflowDefinitionVersionId { get; set; }
    public WorkflowDefinitionVersion WorkflowDefinitionVersion { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<WorkflowTransition> IncomingTransitions { get; set; } = new List<WorkflowTransition>();
    public ICollection<WorkflowTransition> OutgoingTransitions { get; set; } = new List<WorkflowTransition>();
}
