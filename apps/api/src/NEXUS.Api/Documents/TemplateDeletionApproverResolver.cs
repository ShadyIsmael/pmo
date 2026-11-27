using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Persistence;

namespace NEXUS.Api.Documents;

/// <summary>
/// Resolves deletion approvers from current per-user authority and effective permissions.
/// Permission resolution applies direct Deny overrides before a candidate is returned.
/// </summary>
public sealed class TemplateDeletionApproverResolver(
    AppDbContext context,
    IUserEffectivePermissionService effectivePermissions) : ITemplateDeletionApproverResolver
{
    private static readonly HashSet<string> SupportedPermissions =
    [
        TemplateDeletionPermissions.View,
        TemplateDeletionPermissions.Approve,
        TemplateDeletionPermissions.Reject,
    ];

    public async Task<IReadOnlyCollection<Guid>> GetEligibleApproverIdsAsync(
        Guid tenantId,
        Guid? ownerOrganizationId,
        Guid excludeUserId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || ownerOrganizationId == Guid.Empty)
            return Array.Empty<Guid>();

        var candidateIds = await GetAdministratorCandidateIdsAsync(tenantId, ownerOrganizationId, cancellationToken);
        var eligible = new List<Guid>(candidateIds.Count);
        foreach (var candidateId in candidateIds)
        {
            if (candidateId == Guid.Empty || candidateId == excludeUserId)
                continue;

            if (await IsEligibleApproverAsync(candidateId, tenantId, ownerOrganizationId, cancellationToken))
            {
                eligible.Add(candidateId);
            }
        }

        return eligible;
    }

    public async Task<bool> IsAuthorizedAdministratorAsync(
        Guid userId,
        Guid tenantId,
        Guid? ownerOrganizationId,
        string permission,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty || ownerOrganizationId == Guid.Empty
            || !SupportedPermissions.Contains(permission))
        {
            return false;
        }

        var permissions = await GetUserPermissionsAsync(userId, tenantId, cancellationToken);
        return permissions is not null
            && permissions.Contains(permission)
            && permissions.Contains(TemplateDeletionPermissions.View)
            && permissions.Contains(DocumentPermissions.View)
            && await HasOwnerAdministratorScopeAsync(userId, tenantId, ownerOrganizationId, permissions, cancellationToken);
    }

    private async Task<bool> IsEligibleApproverAsync(
        Guid userId,
        Guid tenantId,
        Guid? ownerOrganizationId,
        CancellationToken cancellationToken)
    {
        var permissions = await GetUserPermissionsAsync(userId, tenantId, cancellationToken);
        return permissions is not null
            && (permissions.Contains(TemplateDeletionPermissions.Approve)
                || permissions.Contains(TemplateDeletionPermissions.Reject))
            && permissions.Contains(TemplateDeletionPermissions.View)
            && permissions.Contains(DocumentPermissions.View)
            && await HasOwnerAdministratorScopeAsync(userId, tenantId, ownerOrganizationId, permissions, cancellationToken);
    }

    private async Task<IReadOnlySet<string>?> GetUserPermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var activeUser = await context.Users.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(user => user.Id == userId
                && user.TenantId == tenantId
                && user.IsActive
                && !user.IsDeleted,
                cancellationToken);
        if (!activeUser)
            return null;

        // This resolver returns only effective permissions; Active Deny and unmet
        // catalog dependencies therefore remove an otherwise inherited capability.
        return await effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);
    }

    private async Task<bool> HasOwnerAdministratorScopeAsync(
        Guid userId,
        Guid tenantId,
        Guid? ownerOrganizationId,
        IReadOnlySet<string> permissions,
        CancellationToken cancellationToken)
    {
        if (ownerOrganizationId == Guid.Empty)
            return false;

        if (ownerOrganizationId.HasValue)
        {
            var organizationId = ownerOrganizationId.Value;
            var organizationIsActive = await context.Set<Organization>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(organization => organization.Id == organizationId
                    && organization.TenantId == tenantId
                    && organization.IsActive
                    && !organization.IsDeleted,
                    cancellationToken);
            if (!organizationIsActive
                || !permissions.Contains("OrganizationConfiguration.View")
                || !permissions.Contains("OrganizationConfiguration.Manage"))
            {
                return false;
            }

            return await context.Set<OrganizationSettingsGrant>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(grant => grant.TenantId == tenantId
                    && grant.OrganizationId == organizationId
                    && grant.UserId == userId
                    && grant.CanManage
                    && !grant.IsDeleted,
                    cancellationToken);
        }

        return permissions.Contains("TenantAdministration.ManageScope");
    }

    private async Task<List<Guid>> GetAdministratorCandidateIdsAsync(
        Guid tenantId,
        Guid? ownerOrganizationId,
        CancellationToken cancellationToken)
    {
        if (ownerOrganizationId.HasValue)
        {
            var organizationId = ownerOrganizationId.Value;
            var organizationIsActive = await context.Set<Organization>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(organization => organization.Id == organizationId
                    && organization.TenantId == tenantId
                    && organization.IsActive
                    && !organization.IsDeleted,
                    cancellationToken);
            if (!organizationIsActive)
                return [];

            return await context.Set<OrganizationSettingsGrant>().IgnoreQueryFilters().AsNoTracking()
                .Where(grant => grant.TenantId == tenantId
                    && grant.OrganizationId == organizationId
                    && grant.CanManage
                    && !grant.IsDeleted
                    && context.Users.IgnoreQueryFilters().Any(user => user.Id == grant.UserId
                        && user.TenantId == tenantId
                        && user.IsActive
                        && !user.IsDeleted))
                .Select(grant => grant.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        return await context.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == tenantId
                && user.IsActive
                && !user.IsDeleted
                && (context.UserRoles.IgnoreQueryFilters().Any(assignment => assignment.TenantId == tenantId
                        && assignment.UserId == user.Id
                        && !assignment.IsDeleted
                        && assignment.Role.TenantId == tenantId
                        && !assignment.Role.IsDeleted
                        && assignment.Role.Permissions.Any(permission => permission.TenantId == tenantId
                            && !permission.IsDeleted
                            && permission.Resource == "Document.TemplateDeletion"
                            && (permission.Action == "Approve" || permission.Action == "Reject")))
                    || context.UserPermissionOverrides.IgnoreQueryFilters().Any(overrider => overrider.TenantId == tenantId
                        && overrider.UserId == user.Id
                        && !overrider.IsDeleted
                        && overrider.Effect == PermissionOverrideEffect.Allow
                        && overrider.Permission.TenantId == tenantId
                        && !overrider.Permission.IsDeleted
                        && overrider.Permission.Resource == "Document.TemplateDeletion"
                        && (overrider.Permission.Action == "Approve" || overrider.Permission.Action == "Reject"))))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
    }
}
