using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;

namespace NEXUS.Persistence;

/// <summary>
/// Reads the current immutable organization phase-gate version for a project lifecycle
/// transition. This adapter deliberately performs no user authorization; the project
/// management application service enforces the caller's project scope before calling it.
/// </summary>
public sealed class EfProjectPhaseGateConfigurationReader(AppDbContext db)
    : IProjectPhaseGateConfigurationReader
{
    public Task<ProjectPhaseGateConfigurationDto?> GetCurrentAsync(
        Guid tenantId,
        Guid organizationId,
        Guid sourcePhaseId,
        Guid targetPhaseId,
        CancellationToken cancellationToken = default)
        => ReadCurrentAsync(tenantId, organizationId, sourcePhaseId, targetPhaseId, cancellationToken);

    public async Task<IReadOnlyList<ProjectPhaseGateConfigurationDto>> GetCurrentBySourceAsync(
        Guid tenantId,
        Guid organizationId,
        Guid sourcePhaseId,
        CancellationToken cancellationToken = default)
    {
        var configurations = await db.OrganizationPhaseGateConfigurations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(configuration => configuration.TenantId == tenantId
                && configuration.OrganizationId == organizationId
                && configuration.SourcePhaseId == sourcePhaseId
                && !configuration.IsDeleted)
            .OrderBy(configuration => configuration.TargetPhase)
            .ToListAsync(cancellationToken);

        var results = new List<ProjectPhaseGateConfigurationDto>(configurations.Count);
        foreach (var configuration in configurations)
        {
            var current = await ReadCurrentVersionAsync(configuration, cancellationToken);
            if (current is not null)
            {
                results.Add(current);
            }
        }

        return results;
    }

    private async Task<ProjectPhaseGateConfigurationDto?> ReadCurrentAsync(
        Guid tenantId,
        Guid organizationId,
        Guid sourcePhaseId,
        Guid targetPhaseId,
        CancellationToken cancellationToken)
    {
        var configuration = await db.OrganizationPhaseGateConfigurations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId
                && candidate.OrganizationId == organizationId
                && candidate.SourcePhaseId == sourcePhaseId
                && candidate.TargetPhaseId == targetPhaseId
                && !candidate.IsDeleted,
                cancellationToken);

        return configuration is null
            ? null
            : await ReadCurrentVersionAsync(configuration, cancellationToken);
    }

    private async Task<ProjectPhaseGateConfigurationDto?> ReadCurrentVersionAsync(
        OrganizationPhaseGateConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var version = await db.OrganizationPhaseGateConfigurationVersions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.TenantId == configuration.TenantId
                && candidate.ConfigurationId == configuration.Id
                && candidate.VersionNumber == configuration.CurrentVersionNumber
                && !candidate.IsDeleted,
                cancellationToken);

        if (version is null)
        {
            // A broken current-version pointer is not evidence of compliance.
            return null;
        }

        var requirements = await db.OrganizationPhaseGateRequirements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(requirement => requirement.TenantId == configuration.TenantId
                && requirement.ConfigurationVersionId == version.Id
                && !requirement.IsDeleted)
            .OrderBy(requirement => requirement.TemplateName)
            .ThenBy(requirement => requirement.Id)
            .Select(requirement => new ProjectPhaseGateConfigurationRequirementDto(
                requirement.Id,
                requirement.TemplateId,
                requirement.TemplateVersionId,
                requirement.TemplateName,
                requirement.Category,
                requirement.TemplateVersionLabel,
                requirement.RequirementType))
            .ToListAsync(cancellationToken);

        return new ProjectPhaseGateConfigurationDto(
            configuration.Id,
            version.Id,
            version.VersionNumber,
            configuration.OrganizationId,
            configuration.SourcePhaseId,
            configuration.SourcePhase,
            configuration.TargetPhaseId,
            configuration.TargetPhase,
            requirements);
    }
}
