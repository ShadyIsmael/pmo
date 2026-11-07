using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

public class WorkingCalendar : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    // Legacy aggregate values remain for calendars that predate day rules.
    // New calendars must be configured explicitly; there is no universal 5x8 default.
    public int WorkingDaysPerWeek { get; set; }
    public int HoursPerDay { get; set; }
    public int PaidBreakMinutesPerDay { get; set; }
    public bool IsDefault { get; set; }
    public ICollection<Holiday> Holidays { get; set; } = new List<Holiday>();
    public ICollection<WorkingCalendarDayRule> DayRules { get; set; } = new List<WorkingCalendarDayRule>();

    public decimal ProductiveHoursPerDay => HoursPerDay - (PaidBreakMinutesPerDay / 60m);

    public decimal WeeklyProductiveHours
    {
        get
        {
            var rules = ActiveDayRules();
            return rules.Count > 0
                ? rules.Sum(rule => rule.ProductiveHours)
                : WorkingDaysPerWeek is > 0 and <= 7 && ProductiveHoursPerDay > 0m
                    ? WorkingDaysPerWeek * ProductiveHoursPerDay
                    : 0m;
        }
    }

    public bool HasValidSchedule
    {
        get
        {
            var rules = ActiveDayRules();
            if (rules.Count > 0)
                return rules.Count == 7
                    && rules.Select(rule => rule.DayOfWeek).Distinct().Count() == 7
                    && rules.Any(rule => rule.IsWorkingDay && rule.ProductiveHours > 0m)
                    && rules.Where(rule => rule.IsWorkingDay).All(rule => rule.IsValid);

            return WorkingDaysPerWeek is > 0 and <= 7 && ProductiveHoursPerDay > 0m;
        }
    }

    /// <summary>Scheduled daily hours before dated holidays and resource exceptions are applied.</summary>
    public decimal GetScheduledProductiveHours(DateTime date)
    {
        var rules = ActiveDayRules();
        if (rules.Count == 0)
            return WorkingDaysPerWeek is > 0 and <= 7 && ProductiveHoursPerDay > 0m
                ? WeeklyProductiveHours / 7m
                : 0m;

        return rules.FirstOrDefault(rule => rule.DayOfWeek == date.DayOfWeek)?.ProductiveHours ?? 0m;
    }

    public decimal GetProductiveHours(DateTime date) => IsHoliday(date)
        ? 0m
        : GetScheduledProductiveHours(date);

    public decimal GetMaximumDailyProductiveHours(DayOfWeek dayOfWeek)
    {
        var rules = ActiveDayRules();
        if (rules.Count == 0) return ProductiveHoursPerDay;
        return rules.FirstOrDefault(rule => rule.DayOfWeek == dayOfWeek)?.ProductiveHours ?? 0m;
    }

    public bool IsHoliday(DateTime date)
    {
        var day = date.Date;
        return Holidays.Any(holiday => !holiday.IsDeleted && HolidayContains(holiday, day));
    }

    private List<WorkingCalendarDayRule> ActiveDayRules() =>
        DayRules.Where(rule => !rule.IsDeleted).ToList();

    private static bool HolidayContains(Holiday holiday, DateTime day)
    {
        var start = holiday.Date.Date;
        var end = (holiday.EndDate ?? holiday.Date).Date;
        if (!holiday.IsRecurring) return day >= start && day <= end;

        var current = day.Month * 100 + day.Day;
        var recurringStart = start.Month * 100 + start.Day;
        var recurringEnd = end.Month * 100 + end.Day;
        return recurringStart <= recurringEnd
            ? current >= recurringStart && current <= recurringEnd
            : current >= recurringStart || current <= recurringEnd;
    }
}
