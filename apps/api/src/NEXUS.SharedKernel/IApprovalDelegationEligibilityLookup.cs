namespace NEXUS.SharedKernel;

/// <summary>
/// Outcome of an approval delegation eligibility check for a candidate delegate target.
/// </summary>
/// <param name="UserExists">The candidate exists as a non-deleted user.</param>
/// <param name="IsActive">The candidate's account is active.</param>
/// <param name="IsSameTenant">The candidate belongs to the tenant the delegation runs in.</param>
/// <param name="HasRequiredPermission">The candidate holds the required permission in that
/// tenant, resolved through the authoritative permission pipeline so Active Deny overrides
/// are respected.</param>
public sealed record ApprovalDelegationEligibility(
    bool UserExists,
    bool IsActive,
    bool IsSameTenant,
    bool HasRequiredPermission)
{
    /// <summary>Eligible only when the candidate exists, is active, is in the tenant, and holds the permission.</summary>
    public bool IsEligible => UserExists && IsActive && IsSameTenant && HasRequiredPermission;
}

/// <summary>
/// Resolves whether a user is eligible to receive a delegated approval before the
/// delegate target is assigned. Implemented by the Identity module against the
/// authoritative user and permission stores, so the Approvals module can validate
/// delegation targets without referencing Identity or Persistence directly.
/// </summary>
public interface IApprovalDelegationEligibilityLookup
{
    /// <summary>
    /// Checks <paramref name="candidateUserId"/> against existence, account status,
    /// tenant membership, and the effective grant of <paramref name="requiredPermissionKey"/>
    /// (for example <c>ApprovalRequest.Approve</c>) inside <paramref name="tenantId"/>.
    /// </summary>
    Task<ApprovalDelegationEligibility> CheckAsync(
        Guid candidateUserId,
        Guid tenantId,
        string requiredPermissionKey,
        CancellationToken cancellationToken = default);
}
