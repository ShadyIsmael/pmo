using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Validates archived Document Centre templates only when their exact version is
/// pinned to a persisted Phase Gate requirement for the requested project scope.
/// </summary>
public sealed class ProjectPhaseGateArchivedTemplateReferenceValidator : IArchivedProjectTemplateReferenceValidator
{
    private readonly DbContext _context;
    private readonly IProjectOrganizationLookup _projectOrganizationLookup;

    public ProjectPhaseGateArchivedTemplateReferenceValidator(
        DbContext context,
        IProjectOrganizationLookup projectOrganizationLookup)
    {
        _context = context;
        _projectOrganizationLookup = projectOrganizationLookup;
    }

    public async Task<bool> IsExactPinnedReferenceAsync(
        Guid tenantId,
        Guid projectId,
        Guid organizationId,
        Guid templateId,
        Guid templateVersionId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || organizationId == Guid.Empty
            || templateId == Guid.Empty || templateVersionId == Guid.Empty)
            return false;

        var resolvedOrganizationId = await _projectOrganizationLookup.ResolveOrganizationIdAsync(
            tenantId,
            projectId,
            cancellationToken);
        if (resolvedOrganizationId != organizationId)
            return false;

        var projectSnapshots = _context.Set<ProjectPhaseGateSnapshot>().AsNoTracking()
            .Where(snapshot => snapshot.TenantId == tenantId
                && !snapshot.IsDeleted
                && snapshot.ProjectId == projectId
                && snapshot.OrganizationId == organizationId);

        var pinnedRequirements = _context.Set<ProjectPhaseGateRequirementSnapshot>().AsNoTracking()
            .Where(requirement => requirement.TenantId == tenantId
                && !requirement.IsDeleted
                && requirement.TemplateId == templateId
                && requirement.TemplateVersionId == templateVersionId);

        return await pinnedRequirements
            .Join(
                projectSnapshots,
                requirement => new { requirement.TenantId, SnapshotId = requirement.SnapshotId },
                snapshot => new { snapshot.TenantId, SnapshotId = snapshot.Id },
                (requirement, snapshot) => requirement.Id)
            .AnyAsync(cancellationToken);
    }
}
