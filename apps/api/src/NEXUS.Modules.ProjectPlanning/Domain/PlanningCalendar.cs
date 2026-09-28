using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>Shared calendar definition owned by Project Planning.</summary>
public abstract class PlanningCalendar : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<CalendarDayRule> DayRules { get; set; } = new List<CalendarDayRule>();
    public ICollection<CalendarHoliday> Holidays { get; set; } = new List<CalendarHoliday>();
}

/// <summary>A tenant-level reusable calendar definition.</summary>
public class CalendarTemplate : PlanningCalendar
{
    public ICollection<PlanCalendar> ProjectOverrides { get; set; } = new List<PlanCalendar>();
}

/// <summary>
/// A calendar used by a specific plan version. It may reference a tenant
/// template, while storing the version's effective rule snapshot locally.
/// </summary>
public class PlanCalendar : PlanningCalendar
{
    public Guid ProjectId { get; set; }
    public Guid PlanVersionId { get; set; }
    public PlanVersion? PlanVersion { get; set; }

    public Guid? TemplateId { get; set; }
    public CalendarTemplate? Template { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Default calendar for activities without a task-specific assignment.</summary>
    public bool IsDefault { get; set; }

    public ICollection<PlanActivity> Activities { get; set; } = new List<PlanActivity>();
}

/// <summary>One working/non-working rule for a calendar day of week.</summary>
public class CalendarDayRule : BaseEntity
{
    public Guid PlanningCalendarId { get; set; }
    public PlanningCalendar? PlanningCalendar { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
}

/// <summary>A dated or recurring non-working day in a planning calendar.</summary>
public class CalendarHoliday : BaseEntity
{
    public Guid PlanningCalendarId { get; set; }
    public PlanningCalendar? PlanningCalendar { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsRecurring { get; set; }
}
