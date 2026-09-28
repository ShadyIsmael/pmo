using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowDefinition : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid? ActivePublishedVersionId { get; set; }
    public WorkflowDefinitionVersion? ActivePublishedVersion { get; set; }
    public ICollection<WorkflowDefinitionVersion> Versions { get; set; } = new List<WorkflowDefinitionVersion>();
    public ICollection<WorkflowInstance> Instances { get; set; } = new List<WorkflowInstance>();
}
