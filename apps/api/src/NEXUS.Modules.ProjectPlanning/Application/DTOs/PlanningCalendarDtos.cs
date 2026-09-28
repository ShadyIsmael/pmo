using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

public class CalendarDayRuleDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }
}

public class CalendarHolidayDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsRecurring { get; set; }
}

public class CreateCalendarTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CalendarDayRuleDto> DayRules { get; set; } = new();
    public List<CalendarHolidayDto> Holidays { get; set; } = new();
}

public class UpdateCalendarTemplateDto : CreateCalendarTemplateDto
{
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class CalendarTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<CalendarDayRuleDto> DayRules { get; set; } = Array.Empty<CalendarDayRuleDto>();
    public IReadOnlyList<CalendarHolidayDto> Holidays { get; set; } = Array.Empty<CalendarHolidayDto>();
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class CreatePlanCalendarDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? TemplateId { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool? IsDefault { get; set; }
    public List<CalendarDayRuleDto> DayRules { get; set; } = new();
    public List<CalendarHolidayDto> Holidays { get; set; } = new();
}

public class UpdatePlanCalendarDto : CreatePlanCalendarDto
{
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class PlanCalendarDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid PlanVersionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? TemplateId { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsDefault { get; set; }
    public IReadOnlyList<CalendarDayRuleDto> DayRules { get; set; } = Array.Empty<CalendarDayRuleDto>();
    public IReadOnlyList<CalendarHolidayDto> Holidays { get; set; } = Array.Empty<CalendarHolidayDto>();
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
