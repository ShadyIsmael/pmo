namespace NEXUS.Modules.ProjectPlanning.Domain.Scheduling;

/// <summary>Stable, localizable reason codes for explaining a calculated schedule.</summary>
public enum SchedulingReasonCode
{
    ManualDatesPreserved,
    MovedByPredecessor,
    ConstraintApplied,
    SkippedNonWorkingTime,
    TaskCalendarOverride,
    DeadlineExceeded,
    Critical,
    HasPredecessors,
    HasSuccessors,
    SchedulingConflict,
    DependencyCycle
}

public sealed record SchedulingReason(
    SchedulingReasonCode Code,
    Guid? RelatedActivityId = null,
    PlanDependencyType? DependencyType = null,
    int? LagDays = null,
    int? WorkingDays = null,
    PlanConstraintType? ConstraintType = null);

public sealed record SchedulingIssue(
    string Code,
    string Message,
    Guid ActivityId,
    Guid? RelatedActivityId = null);

public sealed record SchedulingExplanation(
    Guid ActivityId,
    PlanSchedulingMode SchedulingMode,
    PlanConstraintType ConstraintType,
    DateTime? ConstraintDate,
    DateTime? Deadline,
    Guid? EffectiveCalendarId,
    bool CalendarOverride,
    DateTime? Start,
    DateTime? Finish,
    int Duration,
    decimal? TotalSlack,
    decimal? FreeSlack,
    bool Critical,
    bool DeadlineExceeded,
    bool HasPredecessors,
    bool HasSuccessors,
    IReadOnlyList<SchedulingReason> Reasons,
    IReadOnlyList<SchedulingIssue> Issues);
