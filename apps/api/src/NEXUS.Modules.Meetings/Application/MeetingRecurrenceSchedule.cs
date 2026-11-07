using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Application;

public static class MeetingRecurrenceSchedule
{
    public const int MaximumRangeDays = 366;
    public const int MaximumOccurrences = 100;
    public const int PreviewOccurrences = 8;

    public static Result<MeetingRecurrencePreviewDto> Build(MeetingRecurrenceRule? rule)
    {
        if (rule is null) return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation("A recurrence rule is required."));
        if (rule.Frequency is not (MeetingRecurrenceFrequencies.Daily or MeetingRecurrenceFrequencies.EveryTwoDays
            or MeetingRecurrenceFrequencies.Weekly or MeetingRecurrenceFrequencies.Monthly))
            return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation("Choose a supported recurrence frequency."));
        if (rule.StartDate == default || rule.EndDate == default || rule.EndDate < rule.StartDate)
            return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation("A valid recurrence start date and end date are required."));
        if (rule.EndDate.DayNumber - rule.StartDate.DayNumber >= MaximumRangeDays)
            return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation($"Recurrence must end within {MaximumRangeDays} calendar days."));
        if (rule.DurationMinutes is < 1 or > 1440)
            return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation("Meeting duration must be between 1 and 1440 minutes."));
        if (rule.TimeZoneId is null || rule.TimeZoneId.Trim().Length is < 1 or > 200)
            return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation("A valid time zone is required."));
        if (rule.Frequency == MeetingRecurrenceFrequencies.Weekly
            && (rule.Weekdays.Count == 0 || rule.Weekdays.Any(day => !Enum.IsDefined(day)) || rule.Weekdays.Distinct().Count() != rule.Weekdays.Count))
            return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation("Select one or more unique weekdays for weekly recurrence."));
        if (rule.Frequency != MeetingRecurrenceFrequencies.Weekly && rule.Weekdays.Count > 0)
            return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation("Weekdays are supported only for weekly recurrence."));

        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(rule.TimeZoneId.Trim()); }
        catch (TimeZoneNotFoundException) { return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation("The selected time zone is not available on the server.")); }
        catch (InvalidTimeZoneException) { return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation("The selected time zone is invalid.")); }

        var selectedDays = rule.Weekdays.ToHashSet();
        var dates = new List<DateOnly>();
        for (var date = rule.StartDate; date <= rule.EndDate; date = date.AddDays(1))
        {
            var include = rule.Frequency switch
            {
                MeetingRecurrenceFrequencies.Daily => true,
                MeetingRecurrenceFrequencies.EveryTwoDays => (date.DayNumber - rule.StartDate.DayNumber) % 2 == 0,
                MeetingRecurrenceFrequencies.Weekly => selectedDays.Contains(date.DayOfWeek),
                MeetingRecurrenceFrequencies.Monthly => date.Day == rule.StartDate.Day,
                _ => false,
            };
            if (include) dates.Add(date);
        }

        var occurrences = new List<MeetingRecurrenceOccurrencePreview>(Math.Min(dates.Count, MaximumOccurrences));
        var skippedDaylightSavingCount = 0;
        foreach (var date in dates)
        {
            var localStart = date.ToDateTime(rule.LocalStartTime, DateTimeKind.Unspecified);
            // A wall-clock time inside the spring DST gap has no valid instant; omit that date.
            if (zone.IsInvalidTime(localStart))
            {
                skippedDaylightSavingCount++;
                continue;
            }

            // During the autumn overlap, consistently choose the earlier UTC instant.
            var offset = zone.IsAmbiguousTime(localStart)
                ? zone.GetAmbiguousTimeOffsets(localStart).Max()
                : zone.GetUtcOffset(localStart);
            var startUtc = new DateTimeOffset(localStart, offset).UtcDateTime;
            occurrences.Add(new MeetingRecurrenceOccurrencePreview(
                date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                startUtc,
                startUtc.AddMinutes(rule.DurationMinutes),
                zone.Id));
            if (occurrences.Count > MaximumOccurrences)
                return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation($"This rule creates more than {MaximumOccurrences} occurrences. Shorten the recurrence end date."));
        }

        if (occurrences.Count == 0)
            return Result<MeetingRecurrencePreviewDto>.Failure(Error.Validation("This recurrence rule does not produce a valid meeting occurrence."));

        return Result<MeetingRecurrencePreviewDto>.Success(new MeetingRecurrencePreviewDto(
            occurrences.ToArray(), occurrences.Count, skippedDaylightSavingCount, zone.Id));
    }
}
