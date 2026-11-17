namespace NEXUS.SharedKernel;

/// <summary>Validates that user-organization membership changes do not violate existing tenant-scoped integrity constraints.</summary>
public interface IUserOrganizationMembershipIntegrity
{
    Task<IReadOnlyList<Guid>> GetOrganizationsBlockingRemovalAsync(
        Guid tenantId,
        Guid userId,
        IEnumerable<Guid> organizationIds,
        CancellationToken cancellationToken = default);
}
