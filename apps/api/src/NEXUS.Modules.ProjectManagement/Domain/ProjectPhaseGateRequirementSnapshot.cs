using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>Pinned requirement metadata copied from an immutable Organization configuration version.</summary>
public sealed class ProjectPhaseGateRequirementSnapshot : BaseEntity
{
    public Guid SnapshotId { get; set; }
    public Guid SourceRequirementId { get; set; }
    public Guid TemplateId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string TemplateVersionLabel { get; set; } = string.Empty;
    public string RequirementType { get; set; } = string.Empty;
}
