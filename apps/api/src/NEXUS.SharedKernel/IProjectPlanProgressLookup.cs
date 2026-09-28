namespace NEXUS.SharedKernel;

/// <summary>
/// Provides the current percentage of completion from the project's active
/// working plan without exposing Project Planning entities to other modules.
/// </summary>
public interface IProjectPlanProgressLookup
{
    Task<decimal?> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
}
