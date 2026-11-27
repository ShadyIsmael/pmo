using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>Explicit, configurable phase transition edge. No ordinal sequence is implied.</summary>
public sealed class ProjectPhaseTransitionRule : BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid SourcePhaseId { get; set; }
    public Guid TargetPhaseId { get; set; }
    public bool IsActive { get; set; } = true;
}
