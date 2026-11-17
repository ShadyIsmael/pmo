namespace NEXUS.SharedKernel;

/// <summary>Finds Organization memberships that are still required by active user responsibilities.</summary>
public interface IUserOrganizationResponsibilityLookup
{
    Task<IReadOnlyList<Guid>> GetOrganizationsWithActiveResponsibilitiesAsync(
        Guid tenantId,
        Guid userId,
        IEnumerable<Guid> organizationIds,
        CancellationToken cancellationToken = default);
}
