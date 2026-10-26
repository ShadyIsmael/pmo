namespace NEXUS.Modules.ProjectPlanning.Domain.Scheduling;

public sealed record CriticalPathTaskMetrics(
    Guid Id,
    decimal? TotalSlack,
    decimal? FreeSlack,
    bool Critical,
    DateTime? EarlyStart = null,
    DateTime? EarlyFinish = null,
    DateTime? LateStart = null,
    DateTime? LateFinish = null);

public sealed record CriticalPathAnalysisResult(
    IReadOnlyDictionary<Guid, CriticalPathTaskMetrics> ActivityMetrics,
    IReadOnlyList<Guid> CriticalPathActivityIds,
    IReadOnlyList<Guid> CycleActivityIds,
    IReadOnlyList<Guid> CriticalPathDependencyIds,
    DateTime? ProjectFinishDate = null)
{
    public bool HasCycle => CycleActivityIds.Count > 0;
}
