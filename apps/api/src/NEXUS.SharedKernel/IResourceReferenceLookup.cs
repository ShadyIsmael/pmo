namespace NEXUS.SharedKernel;

public interface IResourceReferenceLookup
{
    Task<bool> ExistsAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> resourceIds, CancellationToken cancellationToken = default);
    Task<ResourceReference?> FindAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<ResourceReference?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, ResourceReference>> GetDetailsAsync(IEnumerable<Guid> resourceIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, ResourceReference>> GetDetailsByUserIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
}

public interface IResourceIdentityLinker
{
    Task<Result<ResourceReference>> LinkUserAsync(Guid resourceId, Guid userId, string actorId, CancellationToken cancellationToken = default);
}

public sealed record ResourceReference(
    Guid Id,
    Guid? UserId,
    string Name,
    string? Email,
    string? JobTitle,
    string Type,
    string Status,
    bool IsActive);
