using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Application;

public sealed class UserOrganizationMembershipIntegrity : IUserOrganizationMembershipIntegrity
{
    private readonly DbContext _context;
    private readonly IUserOrganizationResponsibilityLookup? _responsibilityLookup;

    public UserOrganizationMembershipIntegrity(
        DbContext context,
        IUserOrganizationResponsibilityLookup? responsibilityLookup = null)
    {
        _context = context;
        _responsibilityLookup = responsibilityLookup;
    }

    public async Task<IReadOnlyList<Guid>> GetOrganizationsBlockingRemovalAsync(
        Guid tenantId,
        Guid userId,
        IEnumerable<Guid> organizationIds,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
            return Array.Empty<Guid>();

        var ids = organizationIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
            return Array.Empty<Guid>();

        var blocked = await _context.Set<OrganizationSettingsGrant>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(grant => grant.TenantId == tenantId && grant.UserId == userId && !grant.IsDeleted
                && ids.Contains(grant.OrganizationId))
            .Select(grant => grant.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (_responsibilityLookup is null)
            return blocked;

        var responsibilityOrganizations = await _responsibilityLookup
            .GetOrganizationsWithActiveResponsibilitiesAsync(tenantId, userId, ids, cancellationToken);
        return blocked.Concat(responsibilityOrganizations).Distinct().ToArray();
    }
}
