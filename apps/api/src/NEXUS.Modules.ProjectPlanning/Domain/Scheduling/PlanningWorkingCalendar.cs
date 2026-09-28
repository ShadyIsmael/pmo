namespace NEXUS.Modules.ProjectPlanning.Domain.Scheduling;

/// <summary>
/// Calendar arithmetic used by auto scheduling. Durations are stored as whole
/// working days and converted to the calendar's normal working-day minutes.
/// </summary>
public sealed class PlanningWorkingCalendar
{
    private const int SearchLimitDays = 3660;
    private readonly IReadOnlyDictionary<DayOfWeek, CalendarDayDefinition> _days;
    private readonly IReadOnlyCollection<CalendarHolidayDefinition> _holidays;

    public PlanningWorkingCalendar(AutoScheduleCalendar? definition)
    {
        var days = definition?.Days.Where(IsUsable).ToList() ?? [];
        if (days.Count == 0)
        {
            days = Enum.GetValues<DayOfWeek>()
                .Select(day => new CalendarDayDefinition(
                    day,
                    day is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
                    new TimeSpan(8, 0, 0),
                    new TimeSpan(17, 0, 0),
                    new TimeSpan(12, 0, 0),
                    new TimeSpan(13, 0, 0)))
                .ToList();
        }

        _days = days.GroupBy(day => day.DayOfWeek).ToDictionary(group => group.Key, group => group.First());
        _holidays = definition?.Holidays ?? [];
        WorkingDayMinutes = Math.Max(1, days.Where(day => day.IsWorkingDay).Select(WorkingMinutes).DefaultIfEmpty(480).Max());
    }

    public int WorkingDayMinutes { get; }

    /// <summary>Returns signed working-day distance between two planning dates.</summary>
    public int WorkingDaysBetween(DateTime from, DateTime to)
    {
        if (from.Date == to.Date) return 0;

        var direction = to.Date > from.Date ? 1 : -1;
        var cursor = from.Date;
        var days = 0;
        while (cursor != to.Date)
        {
            cursor = cursor.AddDays(direction);
            if (IsWorkingDate(cursor)) days += direction;
        }

        return days;
    }

    /// <summary>Returns signed working minutes between two calendar instants.</summary>
    public int WorkingMinutesBetween(DateTime from, DateTime to)
    {
        if (from == to) return 0;
        if (to < from) return -WorkingMinutesBetween(to, from);

        var total = 0d;
        for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
        {
            foreach (var interval in Intervals(date))
            {
                var start = from > interval.Start ? from : interval.Start;
                var end = to < interval.End ? to : interval.End;
                if (end > start) total += (end - start).TotalMinutes;
            }
        }

        return (int)Math.Round(total, MidpointRounding.AwayFromZero);
    }

    /// <summary>Returns the last working instant strictly before the supplied value.</summary>
    public DateTime PreviousWorkingEndBefore(DateTime value) => NormalizeFinish(value.AddTicks(-1));

    public DateTime NormalizeStart(DateTime value)
    {
        var cursor = value;
        for (var day = 0; day < SearchLimitDays; day++)
        {
            foreach (var interval in Intervals(cursor.Date))
            {
                if (cursor <= interval.Start) return interval.Start;
                if (cursor < interval.End) return cursor;
            }
            cursor = cursor.Date.AddDays(1);
        }
        throw new InvalidOperationException("Planning calendar has no available working time");
    }

    public DateTime NormalizeFinish(DateTime value)
    {
        var cursor = value;
        for (var day = 0; day < SearchLimitDays; day++)
        {
            var intervals = Intervals(cursor.Date);
            for (var index = intervals.Count - 1; index >= 0; index--)
            {
                var interval = intervals[index];
                if (cursor >= interval.End) return interval.End;
                if (cursor > interval.Start) return cursor;
                if (cursor == interval.Start) return cursor;
            }
            cursor = cursor.Date.AddTicks(-1);
        }
        throw new InvalidOperationException("Planning calendar has no available working time");
    }

    public DateTime NextWorkingStartAfter(DateTime value) => NormalizeStart(value.AddTicks(1));

    public DateTime ShiftWorkingDays(DateTime value, int days, bool startBoundary)
    {
        if (days == 0) return startBoundary ? NormalizeStart(value) : value;

        var direction = Math.Sign(days);
        var remaining = Math.Abs(days);
        var date = value.Date;
        while (remaining > 0)
        {
            date = date.AddDays(direction);
            if (IsWorkingDate(date)) remaining--;
        }

        var shifted = date.Add(value.TimeOfDay);
        return startBoundary ? NormalizeStart(shifted) : ClampToWorkingTime(shifted);
    }

    public DateTime AddDuration(DateTime start, int durationDays) =>
        AddWorkingMinutes(NormalizeStart(start), Math.Max(0, durationDays) * WorkingDayMinutes);

    public DateTime SubtractDuration(DateTime finish, int durationDays) =>
        SubtractWorkingMinutes(finish, Math.Max(0, durationDays) * WorkingDayMinutes);

    private DateTime AddWorkingMinutes(DateTime start, int minutes)
    {
        var cursor = NormalizeStart(start);
        if (minutes == 0) return cursor;

        var remaining = minutes;
        for (var guard = 0; guard < SearchLimitDays * 4; guard++)
        {
            var interval = Intervals(cursor.Date).FirstOrDefault(item => cursor >= item.Start && cursor < item.End);
            if (interval == default)
            {
                cursor = NormalizeStart(cursor.AddTicks(1));
                continue;
            }

            var available = (int)Math.Round((interval.End - cursor).TotalMinutes);
            if (remaining <= available) return cursor.AddMinutes(remaining);
            remaining -= available;
            cursor = NormalizeStart(interval.End.AddTicks(1));
        }
        throw new InvalidOperationException("Planning duration exceeds calendar search range");
    }

    private DateTime SubtractWorkingMinutes(DateTime finish, int minutes)
    {
        var cursor = finish;
        if (minutes == 0) return NormalizeStart(cursor);

        var remaining = minutes;
        for (var guard = 0; guard < SearchLimitDays * 4; guard++)
        {
            var interval = Intervals(cursor.Date).LastOrDefault(item => cursor > item.Start && cursor <= item.End);
            if (interval == default)
            {
                cursor = NormalizeFinish(cursor.AddTicks(-1));
                continue;
            }

            var available = (int)Math.Round((cursor - interval.Start).TotalMinutes);
            if (remaining <= available) return cursor.AddMinutes(-remaining);
            remaining -= available;
            cursor = NormalizeFinish(interval.Start.AddTicks(-1));
        }
        throw new InvalidOperationException("Planning duration exceeds calendar search range");
    }

    private DateTime ClampToWorkingTime(DateTime value)
    {
        var intervals = Intervals(value.Date);
        if (intervals.Count == 0) return value > value.Date ? NormalizeFinish(value) : NormalizeStart(value);
        foreach (var interval in intervals)
        {
            if (value >= interval.Start && value <= interval.End) return value;
            if (value < interval.Start) return interval.Start;
        }
        return intervals[^1].End;
    }

    private bool IsWorkingDate(DateTime date) => Intervals(date).Count > 0;

    private List<(DateTime Start, DateTime End)> Intervals(DateTime date)
    {
        if (!_days.TryGetValue(date.DayOfWeek, out var day) || !day.IsWorkingDay || IsHoliday(date)) return [];
        var start = date.Date.Add(day.StartTime!.Value);
        var end = date.Date.Add(day.EndTime!.Value);
        if (day.BreakStartTime is not { } breakStart || day.BreakEndTime is not { } breakEnd || breakStart <= day.StartTime || breakEnd >= day.EndTime || breakStart >= breakEnd)
            return [(start, end)];
        return [(start, date.Date.Add(breakStart)), (date.Date.Add(breakEnd), end)];
    }

    private bool IsHoliday(DateTime date) => _holidays.Any(holiday =>
        holiday.IsRecurring
            ? IsRecurringHolidayDate(holiday, date)
            : date.Date >= holiday.Date.Date && date.Date <= (holiday.EndDate ?? holiday.Date).Date);

    private static bool IsRecurringHolidayDate(CalendarHolidayDefinition holiday, DateTime date)
    {
        var start = holiday.Date.Month * 100 + holiday.Date.Day;
        var endDate = holiday.EndDate ?? holiday.Date;
        var end = endDate.Month * 100 + endDate.Day;
        var current = date.Month * 100 + date.Day;
        return start <= end ? current >= start && current <= end : current >= start || current <= end;
    }

    private static bool IsUsable(CalendarDayDefinition day) => !day.IsWorkingDay ||
        day.StartTime.HasValue && day.EndTime.HasValue && day.StartTime.Value < day.EndTime.Value;

    private static int WorkingMinutes(CalendarDayDefinition day)
    {
        if (!day.IsWorkingDay || !day.StartTime.HasValue || !day.EndTime.HasValue) return 0;
        var minutes = (int)(day.EndTime.Value - day.StartTime.Value).TotalMinutes;
        if (day.BreakStartTime is { } start && day.BreakEndTime is { } end && end > start)
            minutes -= (int)(end - start).TotalMinutes;
        return Math.Max(0, minutes);
    }
}
