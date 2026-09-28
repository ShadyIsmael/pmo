namespace NEXUS.SharedKernel;

public sealed record ProjectReference(Guid Id, string Name, string Currency = "GBP", decimal? CurrentApprovedProjectBudget = null);

public interface IProjectReferenceLookup
{
    Task<IReadOnlyList<ProjectReference>> GetFinancialReferencesAsync(CancellationToken cancellationToken = default);
    Task<ProjectReference?> FindAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> projectIds, CancellationToken cancellationToken = default);
}
