using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Organization.Application;

namespace NEXUS.Persistence;

/// <summary>
/// Resolves phase-gate template references against the authoritative Document Center
/// catalog. New configuration writes request active eligibility; historical
/// configuration reads use the immutable metadata snapshots persisted by the caller.
/// </summary>
internal sealed class EfApprovedTemplateCatalog(AppDbContext context) : IApprovedTemplateCatalog
{
    public async Task<ApprovedTemplateVersionSnapshot?> GetVersionAsync(
        Guid tenantId,
        Guid organizationId,
        Guid templateId,
        Guid templateVersionId,
        bool requireActive,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty
            || organizationId == Guid.Empty
            || templateId == Guid.Empty
            || templateVersionId == Guid.Empty)
        {
            return null;
        }

        // The tenant query filters remain enabled. These explicit predicates make
        // the port's requested scope an additional constraint, never a filter bypass.
        if (requireActive)
        {
            var organizationIsActive = await context.Organizations
                .AsNoTracking()
                .AnyAsync(organization => organization.Id == organizationId
                    && organization.TenantId == tenantId
                    && organization.IsActive, ct);

            if (!organizationIsActive)
                return null;
        }

        var template = await context.Documents
            .AsNoTracking()
            .Where(document => document.Id == templateId
                && document.TenantId == tenantId
                && document.OrganizationId == organizationId
                && document.IsTemplate)
            .Select(document => new
            {
                document.Id,
                document.Name,
                document.CategoryId,
                document.IsArchived,
                document.Status
            })
            .SingleOrDefaultAsync(ct);

        if (template is null
            || (requireActive && (template.IsArchived
                || !string.Equals(template.Status, "Active", StringComparison.OrdinalIgnoreCase))))
        {
            return null;
        }

        var version = await context.DocumentVersions
            .AsNoTracking()
            .Where(documentVersion => documentVersion.Id == templateVersionId
                && documentVersion.DocumentId == templateId
                && documentVersion.TenantId == tenantId)
            .Select(documentVersion => new
            {
                documentVersion.Id,
                documentVersion.VersionNumber
            })
            .SingleOrDefaultAsync(ct);

        if (version is null)
            return null;

        string? category = null;
        if (template.CategoryId is Guid categoryId)
        {
            category = await context.DocumentCategories
                .AsNoTracking()
                .Where(documentCategory => documentCategory.Id == categoryId
                    && documentCategory.TenantId == tenantId)
                .Select(documentCategory => documentCategory.Name)
                .SingleOrDefaultAsync(ct);
        }

        return new ApprovedTemplateVersionSnapshot(
            template.Id,
            version.Id,
            template.Name,
            category,
            version.VersionNumber.ToString(CultureInfo.InvariantCulture));
    }

    public async Task<IReadOnlyList<ApprovedTemplateCategoryGroup>> GetTemplatesByCategoryAsync(
        Guid tenantId,
        Guid organizationId,
        Guid? categoryId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || organizationId == Guid.Empty)
            return Array.Empty<ApprovedTemplateCategoryGroup>();

        var categoriesQuery = context.DocumentCategories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId
                && c.OrganizationId == organizationId
                && c.IsActive
                && !c.IsDeleted);

        if (categoryId.HasValue)
            categoriesQuery = categoriesQuery.Where(c => c.Id == categoryId.Value);

        var categories = await categoriesQuery
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(ct);

        if (categories.Count == 0)
            return Array.Empty<ApprovedTemplateCategoryGroup>();

        var categoryIds = categories.Select(c => c.Id).ToArray();

        var templates = await context.Documents
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId
                && d.OrganizationId == organizationId
                && d.IsTemplate
                && !d.IsArchived
                && string.Equals(d.Status, "Active", StringComparison.OrdinalIgnoreCase)
                && d.CategoryId.HasValue
                && categoryIds.Contains(d.CategoryId.Value))
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.CategoryId,
                d.Version
            })
            .ToListAsync(ct);

        var templateIds = templates.Select(t => t.Id).ToArray();
        var versions = new Dictionary<Guid, int>();
        if (templateIds.Length > 0)
        {
            var currentVersions = await context.DocumentVersions
                .AsNoTracking()
                .Where(v => v.TenantId == tenantId
                    && templateIds.Contains(v.DocumentId)
                    && v.IsCurrent)
                .Select(v => new { v.DocumentId, v.VersionNumber })
                .ToListAsync(ct);
            foreach (var v in currentVersions)
                versions[v.DocumentId] = v.VersionNumber;
        }

        var result = new List<ApprovedTemplateCategoryGroup>();
        foreach (var cat in categories)
        {
            var catTemplates = templates.Where(t => t.CategoryId == cat.Id).ToList();
            if (catTemplates.Count == 0)
                continue;

            var snapshots = catTemplates.Select(t => new ApprovedTemplateVersionSnapshot(
                t.Id,
                Guid.Empty, // VersionId not needed for listing; will be fetched on selection
                t.Name,
                cat.Name,
                versions.TryGetValue(t.Id, out var vnum) ? vnum.ToString(CultureInfo.InvariantCulture) : "1"
            )).ToList();

            result.Add(new ApprovedTemplateCategoryGroup(
                cat.Id,
                cat.Name,
                cat.NameAr,
                snapshots));
        }

        return result;
    }
}
