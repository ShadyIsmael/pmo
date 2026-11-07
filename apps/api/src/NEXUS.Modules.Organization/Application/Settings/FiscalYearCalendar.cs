using System.Text.Json;

namespace NEXUS.Modules.Organization.Application.Settings;

public static class FiscalYearSetting
{
    public const string StartMonthKey = "financial.fiscalYearStartMonth";
    public const int DefaultStartMonth = 1;
}

public sealed record FiscalYearPeriod(
    int FiscalYear,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly EndDateExclusive)
{
    public string Label => $"FY {FiscalYear}";

    public bool Contains(DateOnly date) => date >= StartDate && date < EndDateExclusive;
}

public readonly record struct FiscalYearTimestampRange(
    DateTimeOffset StartInclusive,
    DateTimeOffset EndExclusive);

public sealed record TenantFiscalYearPreview(
    int StartMonth,
    string TimeZoneId,
    int FiscalYear,
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly EndDateExclusive);

public interface IFiscalYearCalendarService
{
    FiscalYearPeriod Resolve(DateOnly date, int startMonth);
    FiscalYearPeriod ForFiscalYear(int fiscalYear, int startMonth);
    bool IsInFiscalYear(DateOnly date, int fiscalYear, int startMonth);
    FiscalYearTimestampRange ToTimestampRange(FiscalYearPeriod period, TimeZoneInfo timeZone);
}

public sealed class FiscalYearCalendarService : IFiscalYearCalendarService
{
    public FiscalYearPeriod Resolve(DateOnly date, int startMonth)
    {
        ValidateStartMonth(startMonth);
        var fiscalYear = startMonth == 1
            ? date.Year
            : date.Month >= startMonth ? date.Year + 1 : date.Year;
        return ForFiscalYear(fiscalYear, startMonth);
    }

    public FiscalYearPeriod ForFiscalYear(int fiscalYear, int startMonth)
    {
        ValidateStartMonth(startMonth);
        if (fiscalYear < 1 || fiscalYear > 9999)
            throw new ArgumentOutOfRangeException(nameof(fiscalYear), "Fiscal year must be between 1 and 9999.");

        var startYear = startMonth == 1 ? fiscalYear : fiscalYear - 1;
        if (startYear < 1)
            throw new ArgumentOutOfRangeException(nameof(fiscalYear), "The requested fiscal year is outside the supported date range.");

        var startDate = new DateOnly(startYear, startMonth, 1);
        var endDateExclusive = startDate.AddYears(1);
        return new FiscalYearPeriod(fiscalYear, startDate, endDateExclusive.AddDays(-1), endDateExclusive);
    }

    public bool IsInFiscalYear(DateOnly date, int fiscalYear, int startMonth) =>
        ForFiscalYear(fiscalYear, startMonth).Contains(date);

    public FiscalYearTimestampRange ToTimestampRange(FiscalYearPeriod period, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var startUtc = ToUtcBoundary(period.StartDate, timeZone);
        var endUtc = ToUtcBoundary(period.EndDateExclusive, timeZone);
        return new FiscalYearTimestampRange(
            new DateTimeOffset(startUtc, TimeSpan.Zero),
            new DateTimeOffset(endUtc, TimeSpan.Zero));
    }

    private static DateTime ToUtcBoundary(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // A few Tenant time zones move their clocks at midnight. When midnight does not
        // exist, use the first valid local instant on that date as the half-open boundary.
        for (var minute = 0; timeZone.IsInvalidTime(local) && minute < 24 * 60; minute++)
            local = local.AddMinutes(1);
        if (timeZone.IsInvalidTime(local))
            throw new InvalidTimeZoneException($"No valid local time exists on {date:yyyy-MM-dd} in {timeZone.Id}.");

        if (timeZone.IsAmbiguousTime(local))
        {
            var earliestUtc = timeZone.GetAmbiguousTimeOffsets(local)
                .Select(offset => new DateTimeOffset(local, offset).UtcDateTime)
                .Min();
            return DateTime.SpecifyKind(earliestUtc, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    public static string? ValidateStartMonth(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var month) || month is < 1 or > 12)
            return "Fiscal year start month must be an integer from 1 through 12.";
        return null;
    }

    public static void ValidateStartMonth(int startMonth)
    {
        if (startMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(startMonth), "Fiscal year start month must be from 1 through 12.");
    }
}

public interface ITenantFiscalYearService
{
    Task<int> GetStartMonthAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<FiscalYearPeriod> ResolveForDateAsync(Guid tenantId, DateOnly date, CancellationToken cancellationToken = default);
    Task<FiscalYearPeriod> ResolveForTimestampAsync(Guid tenantId, DateTimeOffset timestamp, CancellationToken cancellationToken = default);
    Task<TenantFiscalYearPreview> GetPreviewAsync(
        Guid tenantId,
        int? proposedStartMonth = null,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);
}

public sealed class TenantFiscalYearService : ITenantFiscalYearService
{
    private readonly ISettingsRepository _repository;
    private readonly EffectiveConfigurationResolver _resolver;
    private readonly ISettingDefinitionCatalog _catalog;
    private readonly IFiscalYearCalendarService _calendar;

    public TenantFiscalYearService(
        ISettingsRepository repository,
        EffectiveConfigurationResolver resolver,
        ISettingDefinitionCatalog catalog,
        IFiscalYearCalendarService calendar)
    {
        _repository = repository;
        _resolver = resolver;
        _catalog = catalog;
        _calendar = calendar;
    }

    public async Task<int> GetStartMonthAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        (await ReadEffectiveConfigurationAsync(tenantId, cancellationToken)).StartMonth;

    public async Task<FiscalYearPeriod> ResolveForDateAsync(
        Guid tenantId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var configuration = await ReadEffectiveConfigurationAsync(tenantId, cancellationToken);
        return _calendar.Resolve(date, configuration.StartMonth);
    }

    public async Task<FiscalYearPeriod> ResolveForTimestampAsync(
        Guid tenantId,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
        var configuration = await ReadEffectiveConfigurationAsync(tenantId, cancellationToken);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration.TimeZoneId);
        var tenantDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, timeZone).DateTime);
        return _calendar.Resolve(tenantDate, configuration.StartMonth);
    }

    public async Task<TenantFiscalYearPreview> GetPreviewAsync(
        Guid tenantId,
        int? proposedStartMonth = null,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = await ReadEffectiveConfigurationAsync(tenantId, cancellationToken);
        var startMonth = proposedStartMonth ?? configuration.StartMonth;
        FiscalYearCalendarService.ValidateStartMonth(startMonth);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration.TimeZoneId);
        var tenantDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(asOf ?? DateTimeOffset.UtcNow, timeZone).DateTime);
        var period = _calendar.Resolve(tenantDate, startMonth);
        return new TenantFiscalYearPreview(startMonth, configuration.TimeZoneId, period.FiscalYear,
            period.Label, period.StartDate, period.EndDate, period.EndDateExclusive);
    }

    private async Task<(int StartMonth, string TimeZoneId)> ReadEffectiveConfigurationAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant identifier is required.", nameof(tenantId));

        var platform = await _repository.ReadAsync(SettingsScopeIdentity.Platform(), cancellationToken);
        var tenant = await _repository.ReadAsync(SettingsScopeIdentity.Tenant(tenantId), cancellationToken);
        var resolved = _resolver.ResolveTenant(_catalog, platform, tenant)
            .ToDictionary(setting => setting.Definition.Key, setting => setting.Value, StringComparer.Ordinal);

        var startMonth = resolved.TryGetValue(FiscalYearSetting.StartMonthKey, out var monthValue)
            && monthValue.TryGetInt32(out var storedMonth)
            ? storedMonth
            : FiscalYearSetting.DefaultStartMonth;
        FiscalYearCalendarService.ValidateStartMonth(startMonth);

        var timeZoneId = resolved.TryGetValue("regional.defaultTimeZone", out var zoneValue)
            ? zoneValue.GetString()
            : "UTC";
        if (string.IsNullOrWhiteSpace(timeZoneId)) timeZoneId = "UTC";
        return (startMonth, timeZoneId);
    }
}
