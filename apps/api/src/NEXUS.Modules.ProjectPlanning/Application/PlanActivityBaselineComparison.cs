using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;

namespace NEXUS.Modules.ProjectPlanning.Application;

/// <summary>
/// Applies the governed baseline snapshot to the current activity projection.
/// A zero is a valid calculated variance; null is reserved for a missing side
/// of a comparison (for example, a task without a scheduled start).
/// </summary>
public static class PlanActivityBaselineComparison
{
    public readonly record struct Snapshot(DateTime? Start, DateTime? Finish, int Duration);

    public static PlanActivityDto Apply(PlanActivityDto current, Snapshot baseline, PlanningWorkingCalendar? calendar = null)
    {
        current.BaselineStartDate = baseline.Start;
        current.BaselineFinishDate = baseline.Finish;
        current.BaselineDuration = baseline.Duration;
        current.StartVarianceDays = current.StartDate.HasValue && baseline.Start.HasValue
            ? calendar?.WorkingDaysBetween(baseline.Start.Value, current.StartDate.Value)
                ?? (int)(current.StartDate.Value.Date - baseline.Start.Value.Date).TotalDays
            : null;
        current.FinishVarianceDays = current.EndDate.HasValue && baseline.Finish.HasValue
            ? calendar?.WorkingDaysBetween(baseline.Finish.Value, current.EndDate.Value)
                ?? (int)(current.EndDate.Value.Date - baseline.Finish.Value.Date).TotalDays
            : null;
        current.DurationVarianceDays = current.Duration - baseline.Duration;
        return current;
    }
}
