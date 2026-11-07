using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Domain;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class ResourceWorkingCapacityCalculatorTests
{
    private static WorkingCalendar StandardCalendar()
    {
        var calendar = new WorkingCalendar
        {
            Name = "Standard",
            WorkingDaysPerWeek = 5,
            HoursPerDay = 8
        };
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var isWorkingDay = day is DayOfWeek.Sunday or DayOfWeek.Monday or DayOfWeek.Tuesday
                or DayOfWeek.Wednesday or DayOfWeek.Thursday;
            calendar.DayRules.Add(new WorkingCalendarDayRule
            {
                DayOfWeek = day,
                IsWorkingDay = isWorkingDay,
                StartTime = isWorkingDay ? TimeSpan.FromHours(8) : null,
                EndTime = isWorkingDay ? TimeSpan.FromHours(16) : null
            });
        }

        return calendar;
    }

    [Fact]
    public void ExtendedResourceCapacityIsNotCappedByStandardDailyInterval()
    {
        var calendar = StandardCalendar();
        var resource = new Resource { HoursPerDay = 12m, HoursPerWeek = 60m };

        var weeklyCapacity = ResourceWorkingCapacityCalculator.GetWeeklyCapacity(resource, calendar, calendar);
        var dailyCapacity = ResourceWorkingCapacityCalculator.GetScheduledDailyCapacity(
            resource, calendar, calendar, new DateTime(2026, 1, 5));

        Assert.Equal(60m, weeklyCapacity);
        Assert.Equal(12m, dailyCapacity);
        Assert.Equal(30m, weeklyCapacity * 50m / 100m);
    }

    [Fact]
    public void ExplicitFortyEightHourWeekIsNotReplacedByStandardFortyHourWeek()
    {
        var calendar = StandardCalendar();
        var resource = new Resource { HoursPerWeek = 48m };

        var weeklyCapacity = ResourceWorkingCapacityCalculator.GetWeeklyCapacity(resource, calendar, calendar);
        var dailyCapacity = ResourceWorkingCapacityCalculator.GetScheduledDailyCapacity(
            resource, calendar, calendar, new DateTime(2026, 1, 5));

        Assert.Equal(48m, weeklyCapacity);
        Assert.Equal(9.6m, dailyCapacity);
        Assert.Equal(24m, weeklyCapacity * 50m / 100m);
    }

    [Fact]
    public void DailyAndWeeklyCapacityMustMatchConfiguredWorkingDays()
    {
        var calendar = StandardCalendar();

        Assert.Null(ResourceWorkingCapacityCalculator.ValidateConfiguration(12m, 60m, calendar));
        var mismatch = ResourceWorkingCapacityCalculator.ValidateConfiguration(12m, 48m, calendar);
        Assert.Equal("RESOURCE_CAPACITY_DAILY_WEEKLY_MISMATCH", mismatch?.Code);
        Assert.Equal("HoursPerWeek", mismatch?.FieldName);
    }

    [Fact]
    public void ValidateConfiguration_EnforcesOneToTwentyFourDailyHoursAndPreservesLegacyNullFallback()
    {
        var calendar = StandardCalendar();

        Assert.Null(ResourceWorkingCapacityCalculator.ValidateConfiguration(1m, 5m, calendar));
        Assert.Null(ResourceWorkingCapacityCalculator.ValidateConfiguration(8m, 40m, calendar));
        Assert.Null(ResourceWorkingCapacityCalculator.ValidateConfiguration(24m, 120m, calendar));
        Assert.Null(ResourceWorkingCapacityCalculator.ValidateConfiguration(1.25m, 6.25m, calendar));
        Assert.Null(ResourceWorkingCapacityCalculator.ValidateConfiguration(null, null, calendar));

        Assert.NotNull(ResourceWorkingCapacityCalculator.ValidateConfiguration(0m, null, calendar));
        Assert.NotNull(ResourceWorkingCapacityCalculator.ValidateConfiguration(0.99m, null, calendar));
        Assert.NotNull(ResourceWorkingCapacityCalculator.ValidateConfiguration(24.01m, null, calendar));
        Assert.NotNull(ResourceWorkingCapacityCalculator.ValidateConfiguration(25m, null, calendar));
        Assert.Null(ResourceWorkingCapacityCalculator.ValidateConfiguration(8m, null, null));
        var missingCalendar = ResourceWorkingCapacityCalculator.ValidateConfiguration(8m, 40m, null);
        Assert.Equal(ResourceWorkingCapacityCalculator.WorkingCalendarRequiredCode, missingCalendar?.Code);
        Assert.Equal("BaseCalendarId", missingCalendar?.FieldName);
    }
}
