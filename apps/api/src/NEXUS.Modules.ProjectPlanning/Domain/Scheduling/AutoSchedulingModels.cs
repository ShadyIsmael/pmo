namespace NEXUS.Modules.ProjectPlanning.Domain.Scheduling;

public enum SchedulingAnchor
{
    None,
    Start,
    Finish,
    Duration
}

public sealed record AutoScheduleTask(
    Guid Id,
    PlanActivityType Type,
    int Duration,
    DateTime? Start,
    DateTime? Finish,
    Guid? CalendarId,
    SchedulingAnchor Anchor = SchedulingAnchor.None,
    PlanSchedulingMode SchedulingMode = PlanSchedulingMode.Automatic,
    PlanConstraintType ConstraintType = PlanConstraintType.AsSoonAsPossible,
    DateTime? ConstraintDate = null,
    DateTime? Deadline = null);

public sealed record AutoScheduleDependency(
    Guid PredecessorId,
    Guid SuccessorId,
    PlanDependencyType Type,
    int LagDays);

public sealed record CalendarDayDefinition(
    DayOfWeek DayOfWeek,
    bool IsWorkingDay,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    TimeSpan? BreakStartTime,
    TimeSpan? BreakEndTime);

public sealed record CalendarHolidayDefinition(DateTime Date, DateTime? EndDate, bool IsRecurring)
{
    public CalendarHolidayDefinition(DateTime date, bool isRecurring)
        : this(date, null, isRecurring) { }
}

public sealed record AutoScheduleCalendar(
    Guid Id,
    IReadOnlyCollection<CalendarDayDefinition> Days,
    IReadOnlyCollection<CalendarHolidayDefinition> Holidays);

public sealed record ScheduledTask(Guid Id, DateTime? Start, DateTime? Finish, int Duration);

public sealed record AutoScheduleResult(
    IReadOnlyList<ScheduledTask> ChangedTasks,
    IReadOnlyList<Guid> CycleActivityIds,
    string? ValidationError = null)
{
    public bool HasCycle => CycleActivityIds.Count > 0;
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);
}
