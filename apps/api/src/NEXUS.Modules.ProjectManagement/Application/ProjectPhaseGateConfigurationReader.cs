namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Provider-neutral internal port over the current immutable Organization Phase Gate configuration.
/// Implementations must scope every read by Tenant, Organization, and transition; this port does not
/// perform Organization Settings authorization because the caller has already passed Project access.
/// </summary>
public interface IProjectPhaseGateConfigurationReader
{
    Task<ProjectPhaseGateConfigurationDto?> GetCurrentAsync(
        Guid tenantId,
        Guid organizationId,
        Guid sourcePhaseId,
        Guid targetPhaseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectPhaseGateConfigurationDto>> GetCurrentBySourceAsync(
        Guid tenantId,
        Guid organizationId,
        Guid sourcePhaseId,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectPhaseGateConfigurationDto(
    Guid Id,
    Guid VersionId,
    int VersionNumber,
    Guid OrganizationId,
    Guid SourcePhaseId,
    string SourcePhase,
    Guid TargetPhaseId,
    string TargetPhase,
    IReadOnlyList<ProjectPhaseGateConfigurationRequirementDto> Requirements);

public sealed record ProjectPhaseGateConfigurationRequirementDto(
    Guid Id,
    Guid TemplateId,
    Guid TemplateVersionId,
    string TemplateName,
    string? Category,
    string TemplateVersionLabel,
    string RequirementType);
