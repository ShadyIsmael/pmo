using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Infrastructure;

/// <summary>
/// Authoritative delegation eligibility check backed by the identity store.
/// Permission eligibility is resolved through <see cref="IUserEffectivePermissionService"/>,
/// the same effective-permission pipeline that issues permission claims, so role grants,
/// direct Allow/Deny overrides, the protected Admin baseline, and catalog dependency
/// enforcement (Active Deny included) are honoured without re-implementing any of them here.
/// </summary>
public sealed class ApprovalDelegationEligibilityLookup : IApprovalDelegationEligibilityLookup
{
    private readonly DbContext _context;
    private readonly IUserEffectivePermissionService _effectivePermissions;

    public ApprovalDelegationEligibilityLookup(DbContext context, IUserEffectivePermissionService effectivePermissions)
    {
        _context = context;
        _effectivePermissions = effectivePermissions;
    }

    public async Task<ApprovalDelegationEligibility> CheckAsync(
        Guid candidateUserId,
        Guid tenantId,
        string requiredPermissionKey,
        CancellationToken cancellationToken = default)
    {
        if (candidateUserId == Guid.Empty || tenantId == Guid.Empty || string.IsNullOrWhiteSpace(requiredPermissionKey))
            return new ApprovalDelegationEligibility(false, false, false, false);

        // IgnoreQueryFilters so a candidate outside the caller's tenant or a soft-deleted
        // candidate is reported as an explicit ineligible outcome instead of silently
        // collapsing into "does not exist" behind the global tenant filter.
        var candidate = await _context.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.Id == candidateUserId)
            .Select(user => new { user.TenantId, user.IsActive, user.IsDeleted })
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate is null)
            return new ApprovalDelegationEligibility(false, false, false, false);

        var userExists = !candidate.IsDeleted;
        var isActive = candidate.IsActive;
        var isSameTenant = candidate.TenantId == tenantId;

        var hasRequiredPermission = false;
        if (userExists && isActive && isSameTenant)
        {
            var effectivePermissions = await _effectivePermissions.GetEffectivePermissionsAsync(
                candidateUserId, tenantId, cancellationToken);
            hasRequiredPermission = effectivePermissions.Contains(requiredPermissionKey);
        }

        return new ApprovalDelegationEligibility(userExists, isActive, isSameTenant, hasRequiredPermission);
    }
}
