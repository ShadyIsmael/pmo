namespace NEXUS.Modules.ProjectPlanning.Domain.Scheduling;

public sealed record CriticalPathTaskMetrics(
    Guid Id,
    decimal? TotalSlack,
    decimal? FreeSlack,
    bool Critical);

public sealed record CriticalPathAnalysisResult(
    IReadOnlyDictionary<Guid, CriticalPathTaskMetrics> ActivityMetrics,
    IReadOnlyList<Guid> CriticalPathActivityIds,
    IReadOnlyList<Guid> CycleActivityIds)
{
    public bool HasCycle => CycleActivityIds.Count > 0;
}
