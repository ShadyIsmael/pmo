using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Application;

public sealed class UserOrganizationSettingsPermissionLookup : IUserOrganizationSettingsPermissionLookup
{
    private readonly DbContext _context;

    public UserOrganizationSettingsPermissionLookup(DbContext context) => _context = context;

    public async Task<UserOrganizationSettingsPermissions?> GetAsync(
        Guid tenantId,
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty || organizationId == Guid.Empty)
            return null;

        var grant = await _context.Set<OrganizationSettingsGrant>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId
                && item.OrganizationId == organizationId && !item.IsDeleted)
            .Select(item => new UserOrganizationSettingsPermissions(item.CanRead, item.CanManage))
            .FirstOrDefaultAsync(cancellationToken);

        return grant;
    }
}
