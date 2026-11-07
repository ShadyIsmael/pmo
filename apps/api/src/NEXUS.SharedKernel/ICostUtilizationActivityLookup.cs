namespace NEXUS.SharedKernel;

/// <summary>
/// Tenant-scoped names for planning activities and work items referenced by
/// the Cost Utilization report's already-authorized work slices.
/// </summary>
public sealed record CostUtilizationActivityReferences(
    IReadOnlyDictionary<Guid, string> PlanActivities,
    IReadOnlyDictionary<Guid, string> WorkItems);

public interface ICostUtilizationActivityLookup
{
    Task<CostUtilizationActivityReferences> GetNamesAsync(
        Guid tenantId,
        IEnumerable<Guid> planActivityIds,
        IEnumerable<Guid> workItemIds,
        CancellationToken cancellationToken = default);
}
