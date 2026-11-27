namespace NEXUS.SharedKernel;

/// <summary>Resolves the active Organizations a user belongs to through Organization-owned roles.</summary>
public interface IUserOrganizationScopeLookup
{
    Task<IReadOnlySet<Guid>> GetOrganizationIdsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
