using Microsoft.EntityFrameworkCore;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence;

/// <summary>Resolves organization scope from a user's active, organization-owned role assignments.</summary>
public sealed class UserOrganizationScopeLookup(AppDbContext context) : IUserOrganizationScopeLookup
{
    public async Task<IReadOnlySet<Guid>> GetOrganizationIdsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
            return new HashSet<Guid>();

        var organizationIds = await (
            from userRole in context.UserRoles.IgnoreQueryFilters().AsNoTracking()
            join user in context.Users.IgnoreQueryFilters().AsNoTracking()
                on new { userRole.TenantId, userRole.UserId }
                equals new { user.TenantId, UserId = user.Id }
            join role in context.Roles.IgnoreQueryFilters().AsNoTracking()
                on new { userRole.TenantId, userRole.RoleId }
                equals new { role.TenantId, RoleId = role.Id }
            join organization in context.Organizations.IgnoreQueryFilters().AsNoTracking()
                on new { role.TenantId, OrganizationId = role.OrganizationId!.Value }
                equals new { organization.TenantId, OrganizationId = organization.Id }
            where userRole.TenantId == tenantId
                && userRole.UserId == userId
                && !userRole.IsDeleted
                && user.IsActive
                && !user.IsDeleted
                && !role.IsDeleted
                && role.OrganizationId.HasValue
                && !role.IsLegacyOrganizationOwnershipUnresolved
                && organization.IsActive
                && !organization.IsDeleted
            select organization.Id)
            .Distinct()
            .ToHashSetAsync(cancellationToken);

        return organizationIds;
    }
}
