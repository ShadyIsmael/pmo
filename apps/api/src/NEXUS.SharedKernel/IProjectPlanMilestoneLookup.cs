namespace NEXUS.SharedKernel;

/// <summary>
/// A milestone as registered in the project's active working plan. Exposed
/// without leaking Project Planning entities to other modules.
/// </summary>
public sealed record PlanMilestoneLookupItem(
    Guid Id,
    string Name,
    DateTime? DueDate,
    DateTime? CompletedDate,
    decimal? Progress);

/// <summary>
/// Provides the milestones of the project's active working plan (the same
/// version rendered by the Project Plan workspace) so that overview surfaces
/// reflect the plan of record instead of the legacy unversioned Milestones table.
/// </summary>
public interface IProjectPlanMilestoneLookup
{
    Task<IReadOnlyList<PlanMilestoneLookupItem>> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
}
