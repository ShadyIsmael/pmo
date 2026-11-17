namespace NEXUS.SharedKernel;

public sealed record UserOrganizationOption(Guid Id, string Name);

/// <summary>Tenant-scoped organization lookups for user membership administration.</summary>
public interface IUserOrganizationDirectory
{
    /// <summary>Active organizations the actor may assign, optionally limited to an authorized scope.</summary>
    Task<IReadOnlyList<UserOrganizationOption>> GetAssignableOptionsAsync(
        Guid tenantId,
        IReadOnlySet<Guid>? authorizedOrganizationIds,
        CancellationToken cancellationToken = default);

    /// <summary>Names for active, non-deleted tenant organizations in the requested id set.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetActiveNamesAsync(
        Guid tenantId,
        IEnumerable<Guid> organizationIds,
        CancellationToken cancellationToken = default);
}
