namespace NEXUS.Modules.Organization.Application;

/// <summary>
/// Provider-neutral snapshot returned by the Governance Document Center catalog. The values are
/// copied into immutable phase-gate configuration versions so archive or later template changes
/// cannot alter the meaning shown for an existing configuration version.
/// </summary>
public sealed record ApprovedTemplateVersionSnapshot(
    Guid TemplateId,
    Guid TemplateVersionId,
    string Name,
    string? Category,
    string VersionLabel);

/// <summary>
/// Template grouped by its document category for phase-based selection.
/// </summary>
public sealed record ApprovedTemplateCategoryGroup(
    Guid CategoryId,
    string CategoryName,
    string? CategoryNameAr,
    IReadOnlyList<ApprovedTemplateVersionSnapshot> Templates);

public interface IApprovedTemplateCatalog
{
    /// <summary>
    /// Returns a version only when it belongs to the supplied tenant and organization. When
    /// requireActive is true, the template must also be approved, active, and unarchived.
    /// </summary>
    Task<ApprovedTemplateVersionSnapshot?> GetVersionAsync(
        Guid tenantId,
        Guid organizationId,
        Guid templateId,
        Guid templateVersionId,
        bool requireActive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active approved templates grouped by category for the given organization and optional category filter.
    /// </summary>
    Task<IReadOnlyList<ApprovedTemplateCategoryGroup>> GetTemplatesByCategoryAsync(
        Guid tenantId,
        Guid organizationId,
        Guid? categoryId,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectPhaseGatePhasePairDto(
    Guid SourcePhaseId,
    string SourcePhase,
    string SourceLabelEn,
    string SourceLabelAr,
    Guid TargetPhaseId,
    string TargetPhase,
    string TargetLabelEn,
    string TargetLabelAr);

/// <summary>Lists scoped phase pairs for document readiness configuration; it does not define allowed project movement.</summary>
public interface IProjectPhaseGatePhasePairCatalog
{
    Task<IReadOnlyList<ProjectPhaseGatePhasePairDto>> GetPhasePairsAsync(
        Guid tenantId, Guid organizationId, CancellationToken cancellationToken = default);

    Task<ProjectPhaseGatePhasePairDto?> GetPhasePairAsync(
        Guid tenantId, Guid organizationId, Guid sourcePhaseId, Guid targetPhaseId,
        CancellationToken cancellationToken = default);
}

public enum PhaseGateRequirementType
{
    Mandatory = 1,
    Optional = 2
}

public sealed class SavePhaseGateRequirementRequest
{
    public Guid TemplateId { get; init; }
    public Guid TemplateVersionId { get; init; }
    public PhaseGateRequirementType RequirementType { get; init; }
}

public sealed class SavePhaseGateConfigurationRequest
{
    /// <summary>Base64 SQL rowversion of the current configuration; null only when creating it.</summary>
    public string? ExpectedVersionToken { get; init; }
    public IReadOnlyList<SavePhaseGateRequirementRequest> Requirements { get; init; } = Array.Empty<SavePhaseGateRequirementRequest>();
}

public sealed record PhaseGateRequirementDto(
    Guid Id,
    Guid TemplateId,
    Guid TemplateVersionId,
    string TemplateName,
    string? Category,
    string Version,
    PhaseGateRequirementType RequirementType);

public sealed record PhaseGateConfigurationVersionDto(
    Guid ConfigurationVersionId,
    int ConfigurationVersion,
    DateTime CreatedAt,
    string CreatedBy,
    IReadOnlyList<PhaseGateRequirementDto> Requirements);

public sealed record PhaseGateConfigurationDto(
    Guid? ConfigurationId,
    Guid OrganizationId,
    Guid SourcePhaseId,
    string SourcePhase,
    Guid TargetPhaseId,
    string TargetPhase,
    Guid? ConfigurationVersionId,
    int ConfigurationVersion,
    string? VersionToken,
    DateTime? CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    IReadOnlyList<PhaseGateRequirementDto> Requirements);

public sealed record PhaseGateConfigurationHistoryDto(
    Guid ConfigurationId,
    Guid OrganizationId,
    Guid SourcePhaseId,
    string SourcePhase,
    Guid TargetPhaseId,
    string TargetPhase,
    IReadOnlyList<PhaseGateConfigurationVersionDto> Versions);

public enum PhaseGateConfigurationOperationCode
{
    Success,
    NotFound,
    AccessDenied,
    TenantContextMissing,
    ValidationFailed,
    ConcurrencyConflict,
    AuditFailed,
    PersistenceUnavailable
}

public sealed record PhaseGateConfigurationOperationResult(
    PhaseGateConfigurationOperationCode Code,
    PhaseGateConfigurationDto? Configuration = null,
    PhaseGateConfigurationHistoryDto? History = null,
    string? Error = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public interface IPhaseGateConfigurationService
{
    Task<(PhaseGateConfigurationOperationCode Code, IReadOnlyList<ProjectPhaseGatePhasePairDto> PhasePairs)> GetPhasePairsAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    Task<PhaseGateConfigurationOperationResult> GetConfigurationAsync(
        Guid organizationId, Guid sourcePhaseId, Guid targetPhaseId, CancellationToken cancellationToken = default);

    Task<PhaseGateConfigurationOperationResult> GetHistoryAsync(
        Guid organizationId, Guid sourcePhaseId, Guid targetPhaseId, CancellationToken cancellationToken = default);

    Task<PhaseGateConfigurationOperationResult> SaveConfigurationAsync(
        Guid organizationId, Guid sourcePhaseId, Guid targetPhaseId,
        SavePhaseGateConfigurationRequest request, CancellationToken cancellationToken = default);

    Task<PhaseGateConfigurationOperationResult> GetTemplatesByCategoryAsync(
        Guid organizationId, Guid sourcePhaseId, Guid? categoryId, CancellationToken cancellationToken = default);
}
