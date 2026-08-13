using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class WorkflowVersion : BaseEntity
{
    public Guid WorkflowTemplateId { get; set; }
    public WorkflowTemplate WorkflowTemplate { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string? ChangeLog { get; set; }
    public bool IsCurrent { get; set; }
    public string? WorkflowData { get; set; }
}
