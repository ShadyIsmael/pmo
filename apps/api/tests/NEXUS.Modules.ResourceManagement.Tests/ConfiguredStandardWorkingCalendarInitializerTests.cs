using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Persistence;

namespace NEXUS.Modules.ResourceManagement.Tests;

public sealed class ConfiguredStandardWorkingCalendarInitializerTests
{
    [Fact]
    public async Task EnsureAsync_CreatesConfiguredCalendarOnceAndPreservesItOnSubsequentRuns()
    {
        var tenantId = Guid.NewGuid();
        var settings = new Dictionary<string, string?>
        {
            ["Organization:StandardWorkingCalendar:Enabled"] = "true",
            ["Organization:StandardWorkingCalendar:TenantId"] = tenantId.ToString(),
            ["Organization:StandardWorkingCalendar:Name"] = "Configured Standard",
            ["Organization:StandardWorkingCalendar:Days:0:DayOfWeek"] = "Sunday",
            ["Organization:StandardWorkingCalendar:Days:0:IsWorkingDay"] = "false",
            ["Organization:StandardWorkingCalendar:Days:1:DayOfWeek"] = "Monday",
            ["Organization:StandardWorkingCalendar:Days:1:IsWorkingDay"] = "true",
            ["Organization:StandardWorkingCalendar:Days:1:StartTime"] = "09:00",
            ["Organization:StandardWorkingCalendar:Days:1:EndTime"] = "17:00",
            ["Organization:StandardWorkingCalendar:Days:1:BreakStartTime"] = "12:00",
            ["Organization:StandardWorkingCalendar:Days:1:BreakEndTime"] = "13:00",
            ["Organization:StandardWorkingCalendar:Days:2:DayOfWeek"] = "Tuesday",
            ["Organization:StandardWorkingCalendar:Days:2:IsWorkingDay"] = "true",
            ["Organization:StandardWorkingCalendar:Days:2:StartTime"] = "09:00",
            ["Organization:StandardWorkingCalendar:Days:2:EndTime"] = "17:00",
            ["Organization:StandardWorkingCalendar:Days:2:BreakStartTime"] = "12:00",
            ["Organization:StandardWorkingCalendar:Days:2:BreakEndTime"] = "13:00",
            ["Organization:StandardWorkingCalendar:Days:3:DayOfWeek"] = "Wednesday",
            ["Organization:StandardWorkingCalendar:Days:3:IsWorkingDay"] = "true",
            ["Organization:StandardWorkingCalendar:Days:3:StartTime"] = "09:00",
            ["Organization:StandardWorkingCalendar:Days:3:EndTime"] = "17:00",
            ["Organization:StandardWorkingCalendar:Days:3:BreakStartTime"] = "12:00",
            ["Organization:StandardWorkingCalendar:Days:3:BreakEndTime"] = "13:00",
            ["Organization:StandardWorkingCalendar:Days:4:DayOfWeek"] = "Thursday",
            ["Organization:StandardWorkingCalendar:Days:4:IsWorkingDay"] = "true",
            ["Organization:StandardWorkingCalendar:Days:4:StartTime"] = "09:00",
            ["Organization:StandardWorkingCalendar:Days:4:EndTime"] = "17:00",
            ["Organization:StandardWorkingCalendar:Days:4:BreakStartTime"] = "12:00",
            ["Organization:StandardWorkingCalendar:Days:4:BreakEndTime"] = "13:00",
            ["Organization:StandardWorkingCalendar:Days:5:DayOfWeek"] = "Friday",
            ["Organization:StandardWorkingCalendar:Days:5:IsWorkingDay"] = "true",
            ["Organization:StandardWorkingCalendar:Days:5:StartTime"] = "09:00",
            ["Organization:StandardWorkingCalendar:Days:5:EndTime"] = "17:00",
            ["Organization:StandardWorkingCalendar:Days:5:BreakStartTime"] = "12:00",
            ["Organization:StandardWorkingCalendar:Days:5:BreakEndTime"] = "13:00",
            ["Organization:StandardWorkingCalendar:Days:6:DayOfWeek"] = "Saturday",
            ["Organization:StandardWorkingCalendar:Days:6:IsWorkingDay"] = "false"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);

        var created = await ConfiguredStandardWorkingCalendarInitializer.EnsureAsync(context, configuration, NullLogger.Instance);
        var duplicateCreated = await ConfiguredStandardWorkingCalendarInitializer.EnsureAsync(context, configuration, NullLogger.Instance);
        var calendars = await context.WorkingCalendars.IgnoreQueryFilters()
            .Include(calendar => calendar.DayRules)
            .Where(calendar => calendar.TenantId == tenantId)
            .ToListAsync();

        Assert.True(created);
        Assert.False(duplicateCreated);
        var calendar = Assert.Single(calendars);
        Assert.True(calendar.IsDefault);
        Assert.True(calendar.HasValidSchedule);
        Assert.Equal(7, calendar.DayRules.Count);
        Assert.Equal(35m, calendar.WeeklyProductiveHours);
    }
}
