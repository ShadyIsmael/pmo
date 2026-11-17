using Microsoft.EntityFrameworkCore;
using NEXUS.SharedKernel;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Modules.Organization.Application;

public sealed class UserOrganizationDirectory : IUserOrganizationDirectory
{
    private readonly DbContext _context;

    public UserOrganizationDirectory(DbContext context) => _context = context;

    public async Task<IReadOnlyList<UserOrganizationOption>> GetAssignableOptionsAsync(
        Guid tenantId,
        IReadOnlySet<Guid>? authorizedOrganizationIds,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return Array.Empty<UserOrganizationOption>();

        var query = ActiveTenantOrganizations(tenantId);
        if (authorizedOrganizationIds is not null)
        {
            if (authorizedOrganizationIds.Count == 0) return Array.Empty<UserOrganizationOption>();
            query = query.Where(organization => authorizedOrganizationIds.Contains(organization.Id));
        }

        return await query
            .OrderBy(organization => organization.Name)
            .ThenBy(organization => organization.Id)
            .Select(organization => new UserOrganizationOption(organization.Id, organization.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetActiveNamesAsync(
        Guid tenantId,
        IEnumerable<Guid> organizationIds,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return new Dictionary<Guid, string>();

        var ids = organizationIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, string>();

        return await ActiveTenantOrganizations(tenantId)
            .Where(organization => ids.Contains(organization.Id))
            .Select(organization => new { organization.Id, organization.Name })
            .ToDictionaryAsync(organization => organization.Id, organization => organization.Name, cancellationToken);
    }

    private IQueryable<OrganizationEntity> ActiveTenantOrganizations(Guid tenantId) =>
        _context.Set<OrganizationEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(organization => organization.TenantId == tenantId && !organization.IsDeleted && organization.IsActive);
}
