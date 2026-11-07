using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Api.RiskManagement;

public enum RiskActionFollowUpScheduleInvalidReason
{
    MissingCalendar,
    DeletedCalendar,
    InvalidCalendar,
    MissingDayRules,
    IncompleteDayRules,
    InvalidDayRule,
    NoConfiguredWorkingDays,
    MissingTimeZone,
    InvalidTimeZone,
    NoWorkingDayFound,
    InvalidLocalTime
}

public abstract record RiskActionFollowUpScheduleResult
{
    private RiskActionFollowUpScheduleResult() { }

    public sealed record Scheduled(DateTimeOffset DueAtUtc) : RiskActionFollowUpScheduleResult;

    public sealed record Invalid(RiskActionFollowUpScheduleInvalidReason Reason) : RiskActionFollowUpScheduleResult;
}

/// <summary>
/// Resolves a date-only risk follow-up to the start of the first configured working day
/// on or after that date. The supplied calendar and timezone must already be scoped to
/// the project's tenant and effective Organization configuration.
/// </summary>
public static class RiskActionFollowUpScheduleResolver
{
    // The Gregorian weekday/leap-year pattern repeats after 400 years. A full cycle lets
    // us report calendars whose recurring holidays cover every configured working day.
    private const int GregorianCycleDays = 146_097;
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    public static RiskActionFollowUpScheduleResult Resolve(
        DateOnly followUpDate,
        WorkingCalendar? calendar,
        string? validatedTimeZoneId)
    {
        if (calendar is null)
            return Invalid(RiskActionFollowUpScheduleInvalidReason.MissingCalendar);
        if (calendar.IsDeleted)
            return Invalid(RiskActionFollowUpScheduleInvalidReason.DeletedCalendar);
        if (calendar.TenantId == Guid.Empty)
            return Invalid(RiskActionFollowUpScheduleInvalidReason.InvalidCalendar);
        if (calendar.DayRules is null || calendar.Holidays is null)
            return Invalid(RiskActionFollowUpScheduleInvalidReason.InvalidCalendar);
        if (calendar.Holidays.Any(holiday => !holiday.IsDeleted
            && !holiday.IsRecurring
            && (holiday.EndDate ?? holiday.Date).Date < holiday.Date.Date))
        {
            return Invalid(RiskActionFollowUpScheduleInvalidReason.InvalidCalendar);
        }

        var activeRules = calendar.DayRules.Where(rule => !rule.IsDeleted).ToArray();
        if (activeRules.Length == 0)
            return Invalid(RiskActionFollowUpScheduleInvalidReason.MissingDayRules);
        if (activeRules.Length != 7
            || activeRules.Any(rule => !Enum.IsDefined(typeof(DayOfWeek), rule.DayOfWeek))
            || activeRules.Select(rule => rule.DayOfWeek).Distinct().Count() != 7)
        {
            return Invalid(RiskActionFollowUpScheduleInvalidReason.IncompleteDayRules);
        }

        foreach (var rule in activeRules.Where(rule => rule.IsWorkingDay))
        {
            if (!rule.IsValid
                || !rule.StartTime.HasValue
                || !IsTimeWithinDay(rule.StartTime)
                || !IsTimeWithinDay(rule.EndTime)
                || !IsTimeWithinDay(rule.BreakStartTime)
                || !IsTimeWithinDay(rule.BreakEndTime))
            {
                return Invalid(RiskActionFollowUpScheduleInvalidReason.InvalidDayRule);
            }
        }

        if (!activeRules.Any(rule => rule.IsWorkingDay))
            return Invalid(RiskActionFollowUpScheduleInvalidReason.NoConfiguredWorkingDays);

        if (string.IsNullOrWhiteSpace(validatedTimeZoneId))
            return Invalid(RiskActionFollowUpScheduleInvalidReason.MissingTimeZone);

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(validatedTimeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException
                                          or InvalidTimeZoneException
                                          or ArgumentException)
        {
            return Invalid(RiskActionFollowUpScheduleInvalidReason.InvalidTimeZone);
        }

        var rulesByDay = activeRules.ToDictionary(rule => rule.DayOfWeek);
        var candidateDate = followUpDate;
        var cycleDaysSearched = 0;

        while (true)
        {
            if (rulesByDay.TryGetValue(candidateDate.DayOfWeek, out var rule)
                && rule.IsWorkingDay)
            {
                var holidays = HolidaysOn(calendar, candidateDate).ToArray();
                if (holidays.Length == 0)
                    return ResolveAtStart(candidateDate, rule.StartTime!.Value, timeZone);

                // A finite holiday may cover many years. Skip its whole inclusive range;
                // recurring holidays are checked against each candidate date below.
                var finiteHolidayEnd = holidays
                    .Where(holiday => !holiday.IsRecurring)
                    .Select(holiday => (DateOnly?)DateOnly.FromDateTime((holiday.EndDate ?? holiday.Date).Date))
                    .Max();
                if (finiteHolidayEnd.HasValue)
                {
                    if (finiteHolidayEnd.Value == DateOnly.MaxValue)
                        return Invalid(RiskActionFollowUpScheduleInvalidReason.NoWorkingDayFound);
                    candidateDate = finiteHolidayEnd.Value.AddDays(1);
                    cycleDaysSearched = 0;
                    continue;
                }
            }

            if (candidateDate == DateOnly.MaxValue)
                return Invalid(RiskActionFollowUpScheduleInvalidReason.NoWorkingDayFound);

            candidateDate = candidateDate.AddDays(1);
            cycleDaysSearched++;
            if (cycleDaysSearched >= GregorianCycleDays)
                return Invalid(RiskActionFollowUpScheduleInvalidReason.NoWorkingDayFound);
        }
    }

    private static RiskActionFollowUpScheduleResult ResolveAtStart(
        DateOnly date,
        TimeSpan startTime,
        TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(TimeOnly.FromTimeSpan(startTime), DateTimeKind.Unspecified);

        // Match the existing fiscal calendar policy: for a clock gap, use the first
        // valid local minute; for an ambiguous time, choose the earliest UTC instant.
        for (var minute = 0; timeZone.IsInvalidTime(local) && minute < 24 * 60; minute++)
        {
            if (local > DateTime.MaxValue.AddMinutes(-1))
                return Invalid(RiskActionFollowUpScheduleInvalidReason.InvalidLocalTime);
            local = local.AddMinutes(1);
        }
        if (timeZone.IsInvalidTime(local))
            return Invalid(RiskActionFollowUpScheduleInvalidReason.InvalidLocalTime);

        DateTime utc;
        if (timeZone.IsAmbiguousTime(local))
        {
            utc = timeZone.GetAmbiguousTimeOffsets(local)
                .Select(offset => new DateTimeOffset(local, offset).UtcDateTime)
                .Min();
        }
        else
        {
            utc = TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
        }

        return new RiskActionFollowUpScheduleResult.Scheduled(
            new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeSpan.Zero));
    }

    private static IEnumerable<Holiday> HolidaysOn(WorkingCalendar calendar, DateOnly date)
    {
        var current = date.Month * 100 + date.Day;
        foreach (var holiday in calendar.Holidays.Where(holiday => !holiday.IsDeleted))
        {
            var start = DateOnly.FromDateTime(holiday.Date.Date);
            var end = DateOnly.FromDateTime((holiday.EndDate ?? holiday.Date).Date);
            if (holiday.IsRecurring)
            {
                var recurringStart = start.Month * 100 + start.Day;
                var recurringEnd = end.Month * 100 + end.Day;
                var occurs = recurringStart <= recurringEnd
                    ? current >= recurringStart && current <= recurringEnd
                    : current >= recurringStart || current <= recurringEnd;
                if (occurs) yield return holiday;
            }
            else if (date >= start && date <= end)
            {
                yield return holiday;
            }
        }
    }

    private static bool IsTimeWithinDay(TimeSpan? value) =>
        !value.HasValue || value.Value >= TimeSpan.Zero && value.Value < OneDay;

    private static RiskActionFollowUpScheduleResult.Invalid Invalid(
        RiskActionFollowUpScheduleInvalidReason reason) => new(reason);
}
