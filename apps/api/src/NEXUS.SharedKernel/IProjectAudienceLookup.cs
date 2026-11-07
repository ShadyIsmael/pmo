namespace NEXUS.SharedKernel;

public sealed record ProjectAudienceUserReference(Guid UserId, string FullName, string Email);

/// <summary>Searches active users within a tenant and resolves meeting/project audiences.</summary>
public interface IProjectAudienceLookup
{
    Task<IReadOnlyList<ProjectAudienceUserReference>> SearchTenantUsersAsync(
        Guid tenantId,
        string? search,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectAudienceUserReference>> SearchProjectUsersAsync(
        Guid tenantId,
        Guid projectId,
        string? search,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, ProjectAudienceUserReference>> GetProjectUsersAsync(
        Guid tenantId,
        Guid projectId,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, ProjectAudienceUserReference>> GetActiveUsersAsync(
        Guid tenantId,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default);
}
