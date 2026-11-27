using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;

namespace NEXUS.Api.Documents;

/// <summary>
/// Checks retained database history as well as active rows. Any matching record
/// protects the template and all of its versions from hard deletion.
/// </summary>
public sealed class EfTemplateDeletionReferenceValidator(AppDbContext context)
    : ITemplateDeletionReferenceValidator
{
    public async Task<TemplateDeletionReferenceCheck> CheckAsync(
        Guid tenantId,
        Guid templateId,
        IReadOnlyCollection<Guid> versionIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(versionIds);
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant ID is required for template reference checks.", nameof(tenantId));
        if (templateId == Guid.Empty)
            throw new ArgumentException("A template ID is required for template reference checks.", nameof(templateId));

        var versions = versionIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        var referenceKinds = new HashSet<string>(StringComparer.Ordinal);

        // Project documents retain their exact source identity. SourceTemplateId
        // is also checked on its own to catch legacy rows without a version ID.
        var projectDocuments = context.Set<Document>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(document => document.TenantId == tenantId
                && !document.IsTemplate);

        var hasProjectDocumentReference = await projectDocuments
            .AnyAsync(document => document.SourceTemplateId == templateId, cancellationToken);
        if (!hasProjectDocumentReference && versions.Length > 0)
        {
            hasProjectDocumentReference = await projectDocuments
                .AnyAsync(document => document.SourceTemplateVersionId.HasValue
                    && versions.Contains(document.SourceTemplateVersionId.Value), cancellationToken);
        }

        if (hasProjectDocumentReference)
            referenceKinds.Add(TemplateDeletionReferenceKinds.ProjectDocument);

        var organizationRequirements = context.Set<OrganizationPhaseGateRequirement>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(requirement => requirement.TenantId == tenantId
                && requirement.TemplateId == templateId);
        var hasOrganizationConfigurationReference = await organizationRequirements.AnyAsync(cancellationToken);
        if (!hasOrganizationConfigurationReference && versions.Length > 0)
        {
            hasOrganizationConfigurationReference = await context.Set<OrganizationPhaseGateRequirement>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(requirement => requirement.TenantId == tenantId
                    && versions.Contains(requirement.TemplateVersionId), cancellationToken);
        }

        if (hasOrganizationConfigurationReference)
            referenceKinds.Add(TemplateDeletionReferenceKinds.OrganizationPhaseGateConfiguration);

        var projectRequirements = context.Set<ProjectPhaseGateRequirementSnapshot>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(requirement => requirement.TenantId == tenantId
                && requirement.TemplateId == templateId);
        var hasProjectRequirementReference = await projectRequirements.AnyAsync(cancellationToken);
        if (!hasProjectRequirementReference && versions.Length > 0)
        {
            hasProjectRequirementReference = await context.Set<ProjectPhaseGateRequirementSnapshot>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(requirement => requirement.TenantId == tenantId
                    && versions.Contains(requirement.TemplateVersionId), cancellationToken);
        }

        if (hasProjectRequirementReference)
            referenceKinds.Add(TemplateDeletionReferenceKinds.ProjectPhaseGateRequirementSnapshot);

        // Evidence normally points to a project document and its version. Check
        // direct identity matches as well as evidence attached to a retained
        // requirement snapshot pinned to this template/version.
        var evidence = context.Set<ProjectPhaseGateEvidence>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);
        var hasDirectEvidenceReference = await evidence
            .AnyAsync(item => item.DocumentId == templateId, cancellationToken);
        if (!hasDirectEvidenceReference && versions.Length > 0)
        {
            hasDirectEvidenceReference = await evidence
                .AnyAsync(item => versions.Contains(item.DocumentVersionId), cancellationToken);
        }

        var requirementsForEvidence = versions.Length == 0
            ? projectRequirements
            : context.Set<ProjectPhaseGateRequirementSnapshot>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(requirement => requirement.TenantId == tenantId
                    && (requirement.TemplateId == templateId
                        || versions.Contains(requirement.TemplateVersionId)));
        var hasRequirementEvidenceReference = await (
            from item in evidence
            join requirement in requirementsForEvidence
                on new { item.TenantId, RequirementId = item.RequirementSnapshotId }
                equals new { requirement.TenantId, RequirementId = requirement.Id }
            select item.Id)
            .AnyAsync(cancellationToken);

        if (hasDirectEvidenceReference || hasRequirementEvidenceReference)
            referenceKinds.Add(TemplateDeletionReferenceKinds.ProjectPhaseGateEvidence);

        // Document reviews are cascade-deleted with their document, so preserve
        // them by treating even soft-deleted review rows as a protected reference.
        var hasReview = await context.Set<DocumentReview>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(review => review.TenantId == tenantId
                && review.DocumentId == templateId, cancellationToken);
        if (hasReview)
            referenceKinds.Add(TemplateDeletionReferenceKinds.DocumentReview);

        return new TemplateDeletionReferenceCheck(referenceKinds.Count > 0, referenceKinds);
    }
}
