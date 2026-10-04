using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.Import;

/// <summary>
/// Format-neutral schedule produced by an import provider. Provider-specific
/// types must not escape into the Planning application or domain layers.
/// </summary>
public sealed class PlanningImportDocument
{
    public string Format { get; init; } = string.Empty;
    public string? ProjectName { get; init; }
    public List<PlanningImportTask> Tasks { get; init; } = new();
    public List<PlanningImportDependency> Dependencies { get; init; } = new();
    public List<PlanningImportResource> Resources { get; init; } = new();
    public List<PlanningImportAssignment> Assignments { get; init; } = new();
    public List<PlanningImportCalendar> Calendars { get; init; } = new();
    public List<PlanningImportNotice> Warnings { get; init; } = new();
    public List<PlanningImportNotice> UnsupportedFields { get; init; } = new();
}

public sealed class PlanningImportTask
{
    public required string SourceId { get; init; }
    public string? ParentSourceId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int OutlineLevel { get; init; }
    public int SortOrder { get; init; }
    public bool IsMilestone { get; init; }
    public PlanSchedulingMode SchedulingMode { get; init; } = PlanSchedulingMode.Automatic;
    public PlanConstraintType ConstraintType { get; init; } = PlanConstraintType.AsSoonAsPossible;
    public DateTime? ConstraintDate { get; init; }
    public DateTime? Deadline { get; init; }
    public int DurationDays { get; init; }
    public DateTime? Start { get; init; }
    public DateTime? Finish { get; init; }
    public DateTime? ActualStart { get; init; }
    public DateTime? ActualFinish { get; init; }
    public decimal? PercentComplete { get; init; }
    public PlanTextFormat? TextFormat { get; init; }
}

public sealed class PlanningImportDependency
{
    public required string SuccessorSourceId { get; init; }
    public required string PredecessorSourceId { get; init; }
    public PlanDependencyType Type { get; init; }
    public int? LagDays { get; init; }
}

public sealed class PlanningImportResource
{
    public required string SourceId { get; init; }
    public required string Name { get; init; }
    public string? Email { get; init; }
}

public sealed class PlanningImportAssignment
{
    public string SourceId { get; init; } = string.Empty;
    public required string TaskSourceId { get; init; }
    public required string ResourceSourceId { get; init; }
    public decimal? AllocationPercent { get; init; }
    public decimal? HoursPerWeek { get; init; }
    public decimal? PlannedHours { get; init; }
    public string WorkConversionStatus { get; init; } = "NotProvided";
    public DateTime? Start { get; init; }
    public DateTime? Finish { get; init; }
}

public sealed class PlanningImportCalendar
{
    public string SourceId { get; init; } = string.Empty;
    public required string Name { get; init; }
    public bool IsDefault { get; init; }
    public List<PlanningImportCalendarDay> Days { get; init; } = new();
    public List<PlanningImportHoliday> Holidays { get; init; } = new();
}

public sealed class PlanningImportCalendarDay
{
    public DayOfWeek DayOfWeek { get; init; }
    public bool IsWorkingDay { get; init; }
    public TimeSpan? StartTime { get; init; }
    public TimeSpan? EndTime { get; init; }
    public TimeSpan? BreakStartTime { get; init; }
    public TimeSpan? BreakEndTime { get; init; }
}

public sealed class PlanningImportHoliday
{
    public required string Name { get; init; }
    public DateTime Date { get; init; }
}

public sealed class PlanningImportNotice
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public int Count { get; init; }
}
