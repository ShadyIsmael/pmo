using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

/// <summary>
/// Stable organization/transition identity. Each edit appends an immutable configuration version;
/// CurrentVersionNumber and RowVersion provide the current pointer and optimistic concurrency token.
/// </summary>
public sealed class OrganizationPhaseGateConfiguration : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid SourcePhaseId { get; set; }
    public Guid TargetPhaseId { get; set; }
    /// <summary>Stable code snapshot retained for compatibility and historical display.</summary>
    public string SourcePhase { get; set; } = string.Empty;
    /// <summary>Stable code snapshot retained for compatibility and historical display.</summary>
    public string TargetPhase { get; set; } = string.Empty;
    public int CurrentVersionNumber { get; set; }
    public ICollection<OrganizationPhaseGateConfigurationVersion> Versions { get; set; } =
        new List<OrganizationPhaseGateConfigurationVersion>();
}

/// <summary>An immutable snapshot of phase-gate requirements at one configuration version.</summary>
public sealed class OrganizationPhaseGateConfigurationVersion : BaseEntity
{
    public Guid ConfigurationId { get; set; }
    public OrganizationPhaseGateConfiguration Configuration { get; set; } = null!;
    public int VersionNumber { get; set; }
    public ICollection<OrganizationPhaseGateRequirement> Requirements { get; set; } =
        new List<OrganizationPhaseGateRequirement>();
}

/// <summary>
/// A pinned organization-approved template version and its display metadata. This snapshot is
/// intentionally immutable so later edits or archives in the Document Center do not rewrite history.
/// </summary>
public sealed class OrganizationPhaseGateRequirement : BaseEntity
{
    public Guid ConfigurationVersionId { get; set; }
    public OrganizationPhaseGateConfigurationVersion ConfigurationVersion { get; set; } = null!;
    public Guid TemplateId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string TemplateVersionLabel { get; set; } = string.Empty;
    public string RequirementType { get; set; } = string.Empty;
}
