namespace NEXUS.SharedKernel;

public sealed record ProjectReference(Guid Id, string Name, string Currency = "", decimal? CurrentApprovedProjectBudget = null, Guid PublicId = default, string? Code = null, Guid TenantId = default);

public interface IProjectReferenceLookup
{
    Task<IReadOnlyList<ProjectReference>> GetFinancialReferencesAsync(CancellationToken cancellationToken = default);
    Task<ProjectReference?> FindAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, ProjectReference>> GetReferencesAsync(IEnumerable<Guid> projectIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> projectIds, CancellationToken cancellationToken = default);
}

public interface IProjectPublicIdLookup
{
    Task<Guid?> FindPublicIdAsync(Guid projectId, Guid tenantId, CancellationToken cancellationToken = default);
}
