using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.Mapping;

public static class PlanningCalendarMapping
{
    public static CalendarTemplateDto ToDto(this CalendarTemplate calendar) => new()
    {
        Id = calendar.Id,
        Name = calendar.Name,
        Description = calendar.Description,
        DayRules = calendar.DayRules.Where(r => !r.IsDeleted).OrderBy(r => r.DayOfWeek).Select(r => r.ToDto()).ToList(),
        Holidays = calendar.Holidays.Where(h => !h.IsDeleted).OrderBy(h => h.Date).Select(h => h.ToDto()).ToList(),
        RowVersion = calendar.RowVersion.ToArray()
    };

    public static PlanCalendarDto ToDto(this PlanCalendar calendar) => new()
    {
        Id = calendar.Id,
        ProjectId = calendar.ProjectId,
        PlanVersionId = calendar.PlanVersionId,
        Name = calendar.Name,
        Description = calendar.Description,
        TemplateId = calendar.TemplateId,
        EffectiveFrom = calendar.EffectiveFrom,
        EffectiveTo = calendar.EffectiveTo,
        IsDefault = calendar.IsDefault,
        DayRules = calendar.DayRules.Where(r => !r.IsDeleted).OrderBy(r => r.DayOfWeek).Select(r => r.ToDto()).ToList(),
        Holidays = calendar.Holidays.Where(h => !h.IsDeleted).OrderBy(h => h.Date).Select(h => h.ToDto()).ToList(),
        RowVersion = calendar.RowVersion.ToArray()
    };

    private static CalendarDayRuleDto ToDto(this CalendarDayRule rule) => new()
    {
        DayOfWeek = rule.DayOfWeek,
        IsWorkingDay = rule.IsWorkingDay,
        StartTime = rule.StartTime,
        EndTime = rule.EndTime,
        BreakStartTime = rule.BreakStartTime,
        BreakEndTime = rule.BreakEndTime
    };

    private static CalendarHolidayDto ToDto(this CalendarHoliday holiday) => new()
    {
        Name = holiday.Name,
        Description = holiday.Description,
        Date = holiday.Date,
        EndDate = holiday.EndDate,
        IsRecurring = holiday.IsRecurring
    };
}
