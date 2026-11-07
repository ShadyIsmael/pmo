using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence;

/// <summary>
/// Initializes a tenant Standard calendar only when an administrator has supplied
/// a complete schedule in centralized application configuration. It never invents
/// business hours and never edits an existing calendar.
/// </summary>
public static class ConfiguredStandardWorkingCalendarInitializer
{
    private const string SectionPath = "Organization:StandardWorkingCalendar";

    public static async Task<bool> EnsureAsync(
        AppDbContext context,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var section = configuration.GetSection(SectionPath);
        if (!section.GetValue<bool>("Enabled")) return false;

        if (!Guid.TryParse(section["TenantId"], out var tenantId)
            || string.IsNullOrWhiteSpace(section["Name"]))
        {
            logger.LogError("{Section} is enabled but requires a valid TenantId and Name; no calendar was created.", SectionPath);
            return false;
        }

        var dayRules = ParseDayRules(section.GetSection("Days"));
        if (dayRules.Count != 7
            || dayRules.Select(rule => rule.DayOfWeek).Distinct().Count() != 7
            || dayRules.Any(rule => !rule.IsValid)
            || dayRules.All(rule => !rule.IsWorkingDay || rule.ProductiveHours <= 0m))
        {
            logger.LogError("{Section}:Days must define all seven weekdays with a valid schedule; no calendar was created.", SectionPath);
            return false;
        }

        var holidays = ParseHolidays(section.GetSection("Holidays"));
        if (holidays is null)
        {
            logger.LogError("{Section}:Holidays contains an invalid date range; no calendar was created.", SectionPath);
            return false;
        }

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var existingDefaults = await context.WorkingCalendars.IgnoreQueryFilters()
            .AsNoTracking()
            .Include(calendar => calendar.DayRules)
            .Where(calendar => calendar.TenantId == tenantId && calendar.IsDefault && !calendar.IsDeleted)
            .ToListAsync(cancellationToken);
        if (existingDefaults.Count > 0)
        {
            if (!existingDefaults.Any(calendar => calendar.HasValidSchedule))
                logger.LogWarning("Tenant {TenantId} already has a Standard calendar, but its schedule is incomplete. It was preserved without modification.", tenantId);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var nameAlreadyExists = await context.WorkingCalendars.IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(calendar => calendar.TenantId == tenantId
                && !calendar.IsDeleted
                && calendar.Name == section["Name"], cancellationToken);
        if (nameAlreadyExists)
        {
            logger.LogWarning("Tenant {TenantId} already has a calendar named {CalendarName}; the initializer will not create a duplicate.", tenantId, section["Name"]);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var calendarId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var calendar = new WorkingCalendar
        {
            Id = calendarId,
            TenantId = tenantId,
            Name = section["Name"]!.Trim(),
            Description = section["Description"],
            IsDefault = true,
            WorkingDaysPerWeek = dayRules.Count(rule => rule.IsWorkingDay),
            HoursPerDay = 0,
            CreatedAt = now,
            CreatedBy = "System",
            DayRules = dayRules.Select(rule => new WorkingCalendarDayRule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkingCalendarId = calendarId,
                DayOfWeek = rule.DayOfWeek,
                IsWorkingDay = rule.IsWorkingDay,
                StartTime = rule.StartTime,
                EndTime = rule.EndTime,
                BreakStartTime = rule.BreakStartTime,
                BreakEndTime = rule.BreakEndTime,
                CreatedAt = now,
                CreatedBy = "System"
            }).ToList(),
            Holidays = holidays.Select(holiday => new Holiday
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkingCalendarId = calendarId,
                Name = holiday.Name,
                Date = holiday.Date,
                EndDate = holiday.EndDate,
                IsRecurring = holiday.IsRecurring,
                CreatedAt = now,
                CreatedBy = "System"
            }).ToList()
        };

        context.WorkingCalendars.Add(calendar);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Created the configured Standard calendar for tenant {TenantId}.", tenantId);
        return true;
    }

    private static List<WorkingCalendarDayRule> ParseDayRules(IConfigurationSection section)
    {
        var rules = new List<WorkingCalendarDayRule>();
        foreach (var item in section.GetChildren())
        {
            if (!Enum.TryParse<DayOfWeek>(item["DayOfWeek"], true, out var dayOfWeek)
                || !bool.TryParse(item["IsWorkingDay"], out var isWorkingDay))
                return new List<WorkingCalendarDayRule>();

            if (!TryParseOptionalTime(item["StartTime"], out var start)
                || !TryParseOptionalTime(item["EndTime"], out var end)
                || !TryParseOptionalTime(item["BreakStartTime"], out var breakStart)
                || !TryParseOptionalTime(item["BreakEndTime"], out var breakEnd))
                return new List<WorkingCalendarDayRule>();

            rules.Add(new WorkingCalendarDayRule
            {
                DayOfWeek = dayOfWeek,
                IsWorkingDay = isWorkingDay,
                StartTime = start,
                EndTime = end,
                BreakStartTime = breakStart,
                BreakEndTime = breakEnd
            });
        }
        return rules;
    }

    private static bool TryParseOptionalTime(string? value, out TimeSpan? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed)) return false;
        result = parsed;
        return true;
    }

    private static List<ConfiguredHoliday>? ParseHolidays(IConfigurationSection section)
    {
        var holidays = new List<ConfiguredHoliday>();
        foreach (var item in section.GetChildren())
        {
            if (string.IsNullOrWhiteSpace(item["Name"])
                || !DateTime.TryParseExact(item["Date"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return null;

            DateTime? endDate = null;
            if (!string.IsNullOrWhiteSpace(item["EndDate"]))
            {
                if (!DateTime.TryParseExact(item["EndDate"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedEnd)
                    || parsedEnd.Date < date.Date)
                    return null;
                endDate = parsedEnd.Date;
            }

            if (!bool.TryParse(item["IsRecurring"] ?? "false", out var isRecurring)) return null;
            holidays.Add(new ConfiguredHoliday(item["Name"]!.Trim(), date.Date, endDate, isRecurring));
        }
        return holidays;
    }

    private sealed record ConfiguredHoliday(string Name, DateTime Date, DateTime? EndDate, bool IsRecurring);
}
