using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence;

/// <summary>
/// Lists active, scoped phase pairs for optional Phase Gate document requirements. These pairs
/// describe where governance checks may apply; they do not restrict project phase changes.
/// </summary>
public sealed class EfProjectPhaseGatePhasePairCatalog(AppDbContext db) : IProjectPhaseGatePhasePairCatalog
{
    public async Task<IReadOnlyList<ProjectPhaseGatePhasePairDto>> GetPhasePairsAsync(
        Guid tenantId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || organizationId == Guid.Empty)
            return Array.Empty<ProjectPhaseGatePhasePairDto>();

        var candidates = await db.Set<ProjectPhaseCatalogEntry>()
            .AsNoTracking()
            .Where(phase => phase.TenantId == tenantId && !phase.IsDeleted && phase.IsActive
                && (phase.OrganizationId == null || phase.OrganizationId == organizationId))
            .ToListAsync(cancellationToken);

        // An organization-specific catalog entry overrides a tenant entry with the same code.
        var phases = candidates
            .GroupBy(phase => phase.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(phase => phase.OrganizationId == organizationId)
                .ThenBy(phase => phase.Id)
                .First())
            .OrderBy(phase => phase.DisplayOrder)
            .ThenBy(phase => phase.Code, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return phases
            .SelectMany(source => phases
                .Where(target => target.Id != source.Id)
                .Select(target => new ProjectPhaseGatePhasePairDto(
                    source.Id, source.Code, source.NameEn, source.NameAr,
                    target.Id, target.Code, target.NameEn, target.NameAr)))
            .ToArray();
    }

    public async Task<ProjectPhaseGatePhasePairDto?> GetPhasePairAsync(
        Guid tenantId,
        Guid organizationId,
        Guid sourcePhaseId,
        Guid targetPhaseId,
        CancellationToken cancellationToken = default)
    {
        if (sourcePhaseId == Guid.Empty || targetPhaseId == Guid.Empty || sourcePhaseId == targetPhaseId)
            return null;

        var pairs = await GetPhasePairsAsync(tenantId, organizationId, cancellationToken);
        return pairs.FirstOrDefault(pair =>
            pair.SourcePhaseId == sourcePhaseId && pair.TargetPhaseId == targetPhaseId);
    }
}
