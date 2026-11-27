using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>
/// Immutable per-project copy of the applicable Organization configuration identity.
/// A snapshot is created when the first checklist is requested and is never rewritten.
/// </summary>
public sealed class ProjectPhaseGateSnapshot : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid OrganizationId { get; set; }
    /// <summary>Persistent catalog identities for the transition. Codes below are immutable compatibility snapshots for the Organization gate API.</summary>
    public Guid SourcePhaseId { get; set; }
    public Guid TargetPhaseId { get; set; }
    public string SourcePhase { get; set; } = string.Empty;
    public string TargetPhase { get; set; } = string.Empty;
    public Guid ConfigurationId { get; set; }
    public Guid ConfigurationVersionId { get; set; }
    public int ConfigurationVersionNumber { get; set; }
}
