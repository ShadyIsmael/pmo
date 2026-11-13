namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public static class ProjectPhaseGateReadinessStatuses
{
    public const string Ready = "Ready";
    public const string Incomplete = "Incomplete";
    public const string NotConfigured = "NotConfigured";
    public const string NotApplicable = "NotApplicable";
}

public static class ProjectPhaseGateRequirementStatuses
{
    public const string Missing = "Missing";
    public const string Satisfied = "Satisfied";
    public const string OptionalNotProvided = "OptionalNotProvided";
    public const string InvalidOrUnavailable = "InvalidOrUnavailable";
}

public static class ProjectDocumentComplianceStatuses
{
    public const string Compliant = "Compliant";
    public const string Mismatch = "Mismatch";
    public const string NotVerified = "NotVerified";
    public const string PendingValidation = "PendingValidation";
    public const string Missing = "Missing";
}

public static class ProjectPhaseTransitionStatuses
{
    public const string AwaitingTransition = "AwaitingTransition";
    public const string Completed = "Completed";
    public const string NotApplicable = "NotApplicable";
}

public sealed record ProjectPhaseGateReadinessDto(
    Guid ProjectId,
    Guid? OrganizationId,
    Guid? CurrentPhaseId,
    string CurrentPhase,
    string? CurrentPhaseLabelEn,
    string? CurrentPhaseLabelAr,
    Guid? TargetPhaseId,
    string? TargetPhase,
    string? TargetPhaseLabelEn,
    string? TargetPhaseLabelAr,
    Guid? SnapshotId,
    Guid? ConfigurationId,
    Guid? ConfigurationVersionId,
    int? ConfigurationVersionNumber,
    string ReadinessStatus,
    string PhaseTransitionStatus,
    int MandatoryRequirementCount,
    int SatisfiedMandatoryCount,
    int MissingMandatoryCount,
    int OptionalRequirementCount,
    int SatisfiedOptionalCount,
    decimal? ReadinessPercentage,
    string ProjectRowVersion,
    IReadOnlyList<ProjectPhaseGateRequirementStatusDto> Requirements,
    IReadOnlyList<ProjectPhaseGateTransitionOptionDto> AvailableTransitions);

public sealed record ProjectPhaseGateRequirementStatusDto(
    Guid RequirementSnapshotId,
    Guid SourceRequirementId,
    Guid TemplateId,
    Guid TemplateVersionId,
    string TemplateName,
    string? Category,
    string TemplateVersionLabel,
    string RequirementType,
    string Status,
    Guid? DocumentId,
    Guid? DocumentVersionId,
    string? DocumentName,
    string? OriginalFileName,
    int? DocumentVersionNumber,
    string? StatusMessage,
    string ComplianceStatus = ProjectDocumentComplianceStatuses.PendingValidation,
    string? ExpectedFileType = null,
    string? DetectedFileType = null,
    DateTime? ComparedAt = null,
    string? ValidatorVersion = null,
    IReadOnlyList<ProjectDocumentComplianceFindingDto>? ComplianceFindings = null,
    int? TemplateVersionNumber = null,
    string? TemplateFileName = null);

public sealed record ProjectDocumentComplianceFindingDto(
    string Code,
    string MessageEn,
    string MessageAr,
    string? Expected,
    string? Detected);

public sealed record ProjectPhaseGateTransitionOptionDto(
    Guid TargetPhaseId,
    string TargetPhase,
    string? TargetPhaseLabelEn,
    string? TargetPhaseLabelAr,
    Guid? ConfigurationId,
    Guid? ConfigurationVersionId,
    int? ConfigurationVersionNumber,
    string ReadinessStatus,
    bool ReadinessEvaluated);

public sealed record ProjectPhaseTransitionResultDto(
    Guid ProjectId,
    Guid PreviousPhaseId,
    Guid NewPhaseId,
    string PreviousPhase,
    string NewPhase,
    Guid? ConfigurationVersionId,
    int? ConfigurationVersionNumber,
    DateTimeOffset TransitionedAt,
    string ProjectRowVersion);

/// <summary>
/// Immutable, persisted phase-change history row. It is projected from the project
/// audit trail written at transition time; it does not read the current Phase Gate
/// configuration, so later configuration or catalog edits never rewrite history.
/// </summary>
public sealed record ProjectPhaseHistoryItemDto(
    Guid Id,
    Guid ProjectId,
    Guid PreviousPhaseId,
    Guid NewPhaseId,
    string PreviousPhase,
    string NewPhase,
    string? PreviousPhaseLabelEn,
    string? PreviousPhaseLabelAr,
    string? NewPhaseLabelEn,
    string? NewPhaseLabelAr,
    Guid? ConfigurationVersionId,
    int? ConfigurationVersionNumber,
    string EvaluationOutcome,
    int MandatoryRequirementCount,
    int SatisfiedMandatoryCount,
    int MissingMandatoryCount,
    int OptionalRequirementCount,
    int SatisfiedOptionalCount,
    Guid? ActorId,
    string? ActorName,
    DateTimeOffset TransitionedAt,
    IReadOnlyList<ProjectPhaseHistoryRequirementDto> Requirements);

/// <summary>Phase-gate requirement state exactly as it was evaluated at transition time.</summary>
public sealed record ProjectPhaseHistoryRequirementDto(
    Guid RequirementSnapshotId,
    Guid SourceRequirementId,
    Guid TemplateId,
    Guid TemplateVersionId,
    string TemplateName,
    string? Category,
    string TemplateVersionLabel,
    string RequirementType,
    string Status,
    string ComplianceStatus,
    string? ExpectedFileType,
    string? DetectedFileType,
    DateTime? ComparedAt,
    string? ValidatorVersion,
    IReadOnlyList<ProjectDocumentComplianceFindingDto> Findings);

public sealed record ChangeProjectPhaseRequestDto(Guid TargetPhaseId, string ExpectedProjectVersionToken);

public sealed record LinkPhaseGateDocumentRequestDto(Guid DocumentId, Guid DocumentVersionId);

