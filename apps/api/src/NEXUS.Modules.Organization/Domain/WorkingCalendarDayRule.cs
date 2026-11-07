using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

/// <summary>Explicit workday and daily interval for one weekday in a working calendar.</summary>
public class WorkingCalendarDayRule : BaseEntity
{
    public Guid WorkingCalendarId { get; set; }
    public WorkingCalendar WorkingCalendar { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public TimeSpan? BreakStartTime { get; set; }
    public TimeSpan? BreakEndTime { get; set; }

    public bool IsValid
    {
        get
        {
            if (!IsWorkingDay) return true;
            if (!StartTime.HasValue || !EndTime.HasValue || EndTime <= StartTime) return false;
            var hasBreakStart = BreakStartTime.HasValue;
            var hasBreakEnd = BreakEndTime.HasValue;
            if (hasBreakStart != hasBreakEnd) return false;
            return !hasBreakStart
                || (BreakEndTime > BreakStartTime
                    && BreakStartTime >= StartTime
                    && BreakEndTime <= EndTime);
        }
    }

    public decimal ProductiveHours
    {
        get
        {
            if (!IsWorkingDay || !IsValid || !StartTime.HasValue || !EndTime.HasValue) return 0m;
            var interval = (decimal)(EndTime.Value - StartTime.Value).TotalMinutes;
            var breakMinutes = BreakStartTime.HasValue && BreakEndTime.HasValue
                ? (decimal)(BreakEndTime.Value - BreakStartTime.Value).TotalMinutes
                : 0m;
            return Math.Max(0m, interval - breakMinutes) / 60m;
        }
    }
}
