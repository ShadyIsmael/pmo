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
/// Provides the next upcoming, incomplete milestones from the active Working
/// version of the project's default plan. Plan content remains tenant filtered
/// and is scoped to the requested project.
/// </summary>
public interface IProjectPlanMilestoneLookup
{
    Task<IReadOnlyList<PlanMilestoneLookupItem>> GetUpcomingAsync(Guid projectId, DateTime statusDate, CancellationToken cancellationToken = default);
}
