using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public ICollection<WorkflowVersion> Versions { get; set; } = new List<WorkflowVersion>();
}
