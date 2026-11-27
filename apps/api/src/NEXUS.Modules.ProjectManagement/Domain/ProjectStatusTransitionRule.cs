using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>Explicit status transition edge and its reason requirement.</summary>
public sealed class ProjectStatusTransitionRule : BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid SourceStatusId { get; set; }
    public Guid TargetStatusId { get; set; }
    public bool RequiresReason { get; set; }
    public bool IsActive { get; set; } = true;
}
