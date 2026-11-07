using System.Globalization;
using NEXUS.Api.RiskManagement;
using NEXUS.Modules.Organization.Domain;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class RiskActionFollowUpScheduleResolverTests
{
    [Fact]
    public void Resolve_WorkingDay_UsesConfiguredStartForThatWeekday()
    {
        var calendar = CreateCalendar((DayOfWeek.Monday, "08:00"), (DayOfWeek.Tuesday, "10:30"));

        var result = Resolve(new DateOnly(2026, 11, 3), calendar, "UTC");

        Assert.Equal(new DateTimeOffset(2026, 11, 3, 10, 30, 0, TimeSpan.Zero), Scheduled(result).DueAtUtc);
    }

    [Fact]
    public void Resolve_NonWorkingFollowUp_ShiftsToNextConfiguredWorkingDay()
    {
        var calendar = CreateCalendar((DayOfWeek.Sunday, "09:00"));

        var result = Resolve(new DateOnly(2026, 11, 6), calendar, "UTC");

        Assert.Equal(new DateTimeOffset(2026, 11, 8, 9, 0, 0, TimeSpan.Zero), Scheduled(result).DueAtUtc);
    }

    [Fact]
    public void Resolve_Holiday_ShiftsToTheNextWorkingDayAndUsesItsStartTime()
    {
        var calendar = CreateCalendar(
            (DayOfWeek.Monday, "08:00"),
            (DayOfWeek.Tuesday, "09:30"),
            (DayOfWeek.Wednesday, "08:00"));
        calendar.Holidays.Add(new Holiday
        {
            TenantId = calendar.TenantId,
            Name = "Organization holiday",
            Date = new DateTime(2026, 11, 3),
            IsRecurring = false
        });

        var result = Resolve(new DateOnly(2026, 11, 3), calendar, "UTC");

        Assert.Equal(new DateTimeOffset(2026, 11, 4, 8, 0, 0, TimeSpan.Zero), Scheduled(result).DueAtUtc);
    }

    [Fact]
    public void Resolve_UsesTheOrganizationTimezoneForUtcConversion()
    {
        var calendar = CreateCalendar((DayOfWeek.Monday, "08:00"));

        var result = Resolve(new DateOnly(2026, 1, 5), calendar, "Eastern Standard Time");

        Assert.Equal(new DateTimeOffset(2026, 1, 5, 13, 0, 0, TimeSpan.Zero), Scheduled(result).DueAtUtc);
    }

    [Fact]
    public void Resolve_MissingCalendar_ReturnsTypedInvalidResult()
    {
        var result = Resolve(new DateOnly(2026, 11, 2), null, "UTC");

        Assert.Equal(RiskActionFollowUpScheduleInvalidReason.MissingCalendar, Invalid(result).Reason);
    }

    [Fact]
    public void Resolve_IncompleteCalendar_ReturnsTypedInvalidResult()
    {
        var calendar = CreateCalendar((DayOfWeek.Monday, "08:00"));
        calendar.DayRules.Remove(calendar.DayRules.First(rule => rule.DayOfWeek == DayOfWeek.Sunday));

        var result = Resolve(new DateOnly(2026, 11, 2), calendar, "UTC");

        Assert.Equal(RiskActionFollowUpScheduleInvalidReason.IncompleteDayRules, Invalid(result).Reason);
    }

    [Fact]
    public void Resolve_InvalidTimezone_ReturnsTypedInvalidResult()
    {
        var calendar = CreateCalendar((DayOfWeek.Monday, "08:00"));

        var result = Resolve(new DateOnly(2026, 11, 2), calendar, "Not/A_Real_Zone");

        Assert.Equal(RiskActionFollowUpScheduleInvalidReason.InvalidTimeZone, Invalid(result).Reason);
    }

    [Fact]
    public void Resolve_CalendarWithNoWorkingDays_ReturnsTypedInvalidResult()
    {
        var calendar = CreateCalendar();

        var result = Resolve(new DateOnly(2026, 11, 2), calendar, "UTC");

        Assert.Equal(RiskActionFollowUpScheduleInvalidReason.NoConfiguredWorkingDays, Invalid(result).Reason);
    }

    [Fact]
    public void Resolve_DaylightSavingGap_AdvancesToFirstValidLocalMinute()
    {
        var calendar = CreateCalendar((DayOfWeek.Sunday, "02:30"));

        var result = Resolve(new DateOnly(2026, 3, 8), calendar, "Eastern Standard Time");

        Assert.Equal(new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero), Scheduled(result).DueAtUtc);
    }

    [Fact]
    public void Resolve_AmbiguousDaylightSavingTime_UsesEarliestUtcInstant()
    {
        var calendar = CreateCalendar((DayOfWeek.Sunday, "01:30"));

        var result = Resolve(new DateOnly(2026, 11, 1), calendar, "Eastern Standard Time");

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), Scheduled(result).DueAtUtc);
    }

    private static RiskActionFollowUpScheduleResult Resolve(
        DateOnly date,
        WorkingCalendar? calendar,
        string timeZoneId) =>
        RiskActionFollowUpScheduleResolver.Resolve(date, calendar, timeZoneId);

    private static RiskActionFollowUpScheduleResult.Scheduled Scheduled(RiskActionFollowUpScheduleResult result) =>
        Assert.IsType<RiskActionFollowUpScheduleResult.Scheduled>(result);

    private static RiskActionFollowUpScheduleResult.Invalid Invalid(RiskActionFollowUpScheduleResult result) =>
        Assert.IsType<RiskActionFollowUpScheduleResult.Invalid>(result);

    private static WorkingCalendar CreateCalendar(params (DayOfWeek Day, string Start)[] workingDays)
    {
        var starts = workingDays.ToDictionary(day => day.Day, day => TimeSpan.Parse(day.Start, CultureInfo.InvariantCulture));
        var calendar = new WorkingCalendar
        {
            TenantId = Guid.NewGuid(),
            Name = "Test calendar"
        };

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var isWorkingDay = starts.TryGetValue(day, out var start);
            calendar.DayRules.Add(new WorkingCalendarDayRule
            {
                TenantId = calendar.TenantId,
                WorkingCalendarId = calendar.Id,
                DayOfWeek = day,
                IsWorkingDay = isWorkingDay,
                StartTime = isWorkingDay ? start : null,
                EndTime = isWorkingDay ? start.Add(TimeSpan.FromHours(8)) : null
            });
        }

        return calendar;
    }
}
