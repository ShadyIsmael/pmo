using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence;

/// <summary>
/// Tenant-wide, history-preserving reference checks for a permanent Document Category delete.
/// </summary>
public sealed class EfDocumentCategoryReferenceValidator(AppDbContext context) : IDocumentCategoryReferenceValidator
{
    public async Task<bool> HasReferencesAsync(
        Guid tenantId,
        Guid categoryId,
        string nameEn,
        string? nameAr,
        Guid? organizationId,
        CancellationToken cancellationToken = default)
    {
        var categoryDocuments = await context.Documents.IgnoreQueryFilters().AsNoTracking()
            .Where(document => document.TenantId == tenantId && document.CategoryId == categoryId)
            .Select(document => new CategoryDocumentReference(document.Id, document.IsTemplate, document.ProjectId))
            .ToListAsync(cancellationToken);

        var documentIds = categoryDocuments.Select(document => document.Id).ToArray();
        var templateIds = categoryDocuments.Where(document => document.IsTemplate).Select(document => document.Id).ToArray();
        var projectDocumentIds = categoryDocuments.Where(document => document.ProjectId.HasValue).Select(document => document.Id).ToArray();
        var versionIds = documentIds.Length == 0
            ? []
            : await context.DocumentVersions.IgnoreQueryFilters().AsNoTracking()
                .Where(version => version.TenantId == tenantId && documentIds.Contains(version.DocumentId))
                .Select(version => version.Id)
                .ToArrayAsync(cancellationToken);

        var hasChildCategories = await context.Set<DocumentCategory>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(category => category.TenantId == tenantId && category.ParentId == categoryId, cancellationToken);
        var hasPhaseCatalogReference = await context.Set<ProjectPhaseCatalogEntry>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(phase => phase.TenantId == tenantId && phase.DocumentCategoryId == categoryId, cancellationToken);

        var hasOrganizationGateReference = await context.Set<OrganizationPhaseGateRequirement>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(requirement => requirement.TenantId == tenantId
                && ((templateIds.Contains(requirement.TemplateId) && versionIds.Contains(requirement.TemplateVersionId))
                    || ((requirement.Category == nameEn || (nameAr != null && requirement.Category == nameAr))
                        && (organizationId == null
                            || requirement.ConfigurationVersion.Configuration.OrganizationId == organizationId))),
                cancellationToken);

        var hasProjectGateReference = await context.Set<ProjectPhaseGateRequirementSnapshot>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(requirement => requirement.TenantId == tenantId
                && ((templateIds.Contains(requirement.TemplateId) && versionIds.Contains(requirement.TemplateVersionId))
                    || ((requirement.Category == nameEn || (nameAr != null && requirement.Category == nameAr))
                        && context.Set<ProjectPhaseGateSnapshot>().IgnoreQueryFilters().Any(snapshot =>
                            snapshot.TenantId == tenantId
                            && snapshot.Id == requirement.SnapshotId
                            && (organizationId == null || snapshot.OrganizationId == organizationId)))),
                cancellationToken);

        var hasProjectEvidenceReference = await context.Set<ProjectPhaseGateEvidence>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(evidence => evidence.TenantId == tenantId
                && projectDocumentIds.Contains(evidence.DocumentId)
                && versionIds.Contains(evidence.DocumentVersionId), cancellationToken);

        // A document row covers ordinary documents, templates, and Project Documents. Its
        // versions, pinned gate requirements/evidence, and category-to-phase assignments are
        // checked separately so historical associations remain protected as well.
        return categoryDocuments.Count > 0
            || versionIds.Length > 0
            || hasChildCategories
            || hasPhaseCatalogReference
            || hasOrganizationGateReference
            || hasProjectGateReference
            || hasProjectEvidenceReference;
    }

    private sealed record CategoryDocumentReference(Guid Id, bool IsTemplate, Guid? ProjectId);
}
