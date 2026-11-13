using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>
/// Historical association between one project checklist requirement and one exact stored document version.
/// IsDeleted marks a prior link as inactive while preserving its history.
/// </summary>
public sealed class ProjectPhaseGateEvidence : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid RequirementSnapshotId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }

    /// <summary>Advisory comparison result for the linked document version and pinned template version.</summary>
    public string ComplianceStatus { get; set; } = "PendingValidation";
    public string? ExpectedFileType { get; set; }
    public string? DetectedFileType { get; set; }
    public string? ComplianceFindingsJson { get; set; }
    public DateTime? ComparedAt { get; set; }
    public string? ValidatorVersion { get; set; }
}
