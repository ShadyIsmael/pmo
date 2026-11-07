using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Application;

public sealed record ResourceCapacityValidationError(string Code, string Message, string FieldName);

/// <summary>
/// Resolves resource capacity independently from the calendar's standard daily interval.
/// Calendars choose working dates; explicit resource capacity may extend the hours available on those dates.
/// </summary>
public static class ResourceWorkingCapacityCalculator
{
    public const string WorkingCalendarRequiredCode = "RESOURCE_WORKING_CALENDAR_REQUIRED";
    public static int GetWorkingDaysPerWeek(WorkingCalendar? calendar)
    {
        if (calendar?.HasValidSchedule != true) return 0;
        var dayRules = calendar.DayRules.Where(rule => !rule.IsDeleted).ToList();
        return dayRules.Count > 0
            ? dayRules.Count(rule => rule.IsWorkingDay && rule.ProductiveHours > 0m)
            : calendar.WorkingDaysPerWeek;
    }

    public static decimal GetWeeklyCapacity(Resource resource, WorkingCalendar? capacityCalendar, WorkingCalendar? scheduleCalendar = null) =>
        GetWeeklyCapacity(resource.HoursPerDay, resource.HoursPerWeek, capacityCalendar, scheduleCalendar);

    public static decimal GetWeeklyCapacity(
        decimal? hoursPerDay,
        decimal? hoursPerWeek,
        WorkingCalendar? capacityCalendar,
        WorkingCalendar? scheduleCalendar = null)
    {
        if (hoursPerWeek is > 0m) return hoursPerWeek.Value;
        if (hoursPerDay is > 0m)
        {
            var basis = capacityCalendar?.HasValidSchedule == true ? capacityCalendar : scheduleCalendar;
            return hoursPerDay.Value * GetWorkingDaysPerWeek(basis);
        }
        return scheduleCalendar?.HasValidSchedule == true ? scheduleCalendar.WeeklyProductiveHours : 0m;
    }

    public static decimal GetScheduledDailyCapacity(
        Resource resource,
        WorkingCalendar? capacityCalendar,
        WorkingCalendar? scheduleCalendar,
        DateTime date,
        bool allowCalendarNonWorkingDay = false) =>
        GetScheduledDailyCapacity(resource.HoursPerDay, resource.HoursPerWeek, capacityCalendar, scheduleCalendar, date, allowCalendarNonWorkingDay);

    public static decimal GetScheduledDailyCapacity(
        decimal? hoursPerDay,
        decimal? hoursPerWeek,
        WorkingCalendar? capacityCalendar,
        WorkingCalendar? scheduleCalendar,
        DateTime date,
        bool allowCalendarNonWorkingDay = false)
    {
        if (scheduleCalendar?.HasValidSchedule != true) return 0m;
        var scheduledHours = scheduleCalendar.GetScheduledProductiveHours(date);
        if (!allowCalendarNonWorkingDay && scheduledHours <= 0m) return 0m;

        var hasExplicitCapacity = hoursPerDay.HasValue || hoursPerWeek.HasValue;
        if (!hasExplicitCapacity) return scheduledHours;

        // Legacy calendars contain only weekly totals and cannot identify weekdays. Their
        // schedule model distributes productive hours over all seven dates, so preserve
        // that distribution while keeping the explicit weekly resource capacity intact.
        var dayRules = scheduleCalendar.DayRules.Where(rule => !rule.IsDeleted).ToList();
        var workingDays = dayRules.Count > 0
            ? dayRules.Count(rule => rule.IsWorkingDay && rule.ProductiveHours > 0m)
            : 7;
        var weeklyCapacity = GetWeeklyCapacity(hoursPerDay, hoursPerWeek, capacityCalendar, scheduleCalendar);
        if (workingDays == 0 || weeklyCapacity <= 0m) return 0m;

        var weeklySharePerWorkingDay = weeklyCapacity / workingDays;
        return hoursPerDay.HasValue
            ? Math.Min(hoursPerDay.Value, weeklySharePerWorkingDay)
            : weeklySharePerWorkingDay;
    }

    public static decimal GetAvailableDailyCapacity(
        Resource resource,
        WorkingCalendar? capacityCalendar,
        WorkingCalendar? scheduleCalendar,
        DateTime date,
        ResourceCalendar? exception = null) =>
        GetAvailableDailyCapacity(resource.HoursPerDay, resource.HoursPerWeek, capacityCalendar, scheduleCalendar, date, exception);

    public static decimal GetAvailableDailyCapacity(
        decimal? hoursPerDay,
        decimal? hoursPerWeek,
        WorkingCalendar? capacityCalendar,
        WorkingCalendar? scheduleCalendar,
        DateTime date,
        ResourceCalendar? exception = null)
    {
        if (scheduleCalendar?.HasValidSchedule != true) return 0m;
        if (exception is null)
            return scheduleCalendar.IsHoliday(date)
                ? 0m
                : GetScheduledDailyCapacity(hoursPerDay, hoursPerWeek, capacityCalendar, scheduleCalendar, date);

        var type = exception.DayType.Trim();
        var isWorking = string.Equals(type, "WorkingDay", StringComparison.OrdinalIgnoreCase);
        var isUnavailable = IsUnavailableDayType(type);
        if (!isWorking && !isUnavailable)
            return scheduleCalendar.IsHoliday(date)
                ? 0m
                : GetScheduledDailyCapacity(hoursPerDay, hoursPerWeek, capacityCalendar, scheduleCalendar, date);

        if (exception.AvailableHours is < 0m or > 24m) return 0m;
        if (exception.AvailableHours.HasValue) return exception.AvailableHours.Value;
        if (isUnavailable) return 0m;

        return GetScheduledDailyCapacity(
            hoursPerDay,
            hoursPerWeek,
            capacityCalendar,
            scheduleCalendar,
            date,
            allowCalendarNonWorkingDay: true);
    }

    public static bool IsUnavailableDayType(string dayType) =>
        string.Equals(dayType, "NonWorking", StringComparison.OrdinalIgnoreCase)
        || string.Equals(dayType, "NonWorkingDay", StringComparison.OrdinalIgnoreCase)
        || string.Equals(dayType, "Holiday", StringComparison.OrdinalIgnoreCase)
        || string.Equals(dayType, "Leave", StringComparison.OrdinalIgnoreCase)
        || string.Equals(dayType, "SickLeave", StringComparison.OrdinalIgnoreCase)
        || string.Equals(dayType, "PersonalLeave", StringComparison.OrdinalIgnoreCase);

    public static ResourceCapacityValidationError? ValidateConfiguration(
        decimal? hoursPerDay,
        decimal? hoursPerWeek,
        WorkingCalendar? capacityCalendar)
    {
        if (hoursPerDay is < 1m or > 24m)
            return new("RESOURCE_HOURS_PER_DAY_INVALID", "Hours per day must be between 1 and 24.", "HoursPerDay");
        if (hoursPerWeek is <= 0m or > 168m)
            return new("RESOURCE_HOURS_PER_WEEK_INVALID", "Hours per week must be greater than 0 and no more than 168.", "HoursPerWeek");

        var workingDays = GetWorkingDaysPerWeek(capacityCalendar);
        if (hoursPerWeek.HasValue && workingDays > 0 && hoursPerWeek.Value > workingDays * 24m)
            return new(
                "RESOURCE_CAPACITY_EXCEEDS_CALENDAR",
                "Hours per week cannot exceed 24 hours for each working day in the selected calendar.",
                "HoursPerWeek");
        if (!hoursPerDay.HasValue || !hoursPerWeek.HasValue) return null;

        if (workingDays == 0)
            return new(
                WorkingCalendarRequiredCode,
                "A configured working calendar is required to validate daily and weekly resource capacity together.",
                "BaseCalendarId");

        var expectedWeeklyHours = hoursPerDay.Value * workingDays;
        return Math.Abs(expectedWeeklyHours - hoursPerWeek.Value) <= 0.01m
            ? null
            : new(
                "RESOURCE_CAPACITY_DAILY_WEEKLY_MISMATCH",
                "Hours per week must equal hours per day multiplied by the selected calendar's working days.",
                "HoursPerWeek");
    }
}
