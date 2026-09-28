using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed record NormalizedTimeEntryDuration(
    TimeSpan StartTime,
    TimeSpan EndTime,
    int ElapsedSeconds,
    int ActualSeconds,
    int BreakSeconds,
    int PauseSeconds);

public static class TimeEntryDuration
{
    public static Result<NormalizedTimeEntryDuration> Normalize(
        TimeSpan? start,
        TimeSpan? end,
        int? actualSeconds,
        int breakSeconds,
        int pauseSeconds)
    {
        if (breakSeconds < 0 || pauseSeconds < 0)
            return Result<NormalizedTimeEntryDuration>.Failure(Error.Validation("Break and pause duration cannot be negative"));
        if (breakSeconds > 86400 || pauseSeconds > 86400)
            return Result<NormalizedTimeEntryDuration>.Failure(Error.Validation("Break and pause duration is invalid"));
        if ((start.HasValue ? 1 : 0) + (end.HasValue ? 1 : 0) + (actualSeconds.HasValue ? 1 : 0) < 2)
            return Result<NormalizedTimeEntryDuration>.Failure(Error.Validation("At least two of start, end, and actual duration are required"));
        if (actualSeconds.HasValue && actualSeconds.Value <= 0)
            return Result<NormalizedTimeEntryDuration>.Failure(Error.Validation("Actual work duration must be greater than zero"));

        if (start.HasValue && (start.Value < TimeSpan.Zero || start.Value >= TimeSpan.FromDays(1)) ||
            end.HasValue && (end.Value < TimeSpan.Zero || end.Value >= TimeSpan.FromDays(1)))
            return Result<NormalizedTimeEntryDuration>.Failure(Error.Validation("Start and end must be within the same day"));

        if (!start.HasValue && end.HasValue && actualSeconds.HasValue)
            start = end.Value - TimeSpan.FromSeconds(actualSeconds.Value + breakSeconds + pauseSeconds);
        if (start.HasValue && !end.HasValue && actualSeconds.HasValue)
            end = start.Value + TimeSpan.FromSeconds(actualSeconds.Value + breakSeconds + pauseSeconds);
        if (!start.HasValue || !end.HasValue)
            return Result<NormalizedTimeEntryDuration>.Failure(Error.Validation("Start and end could not be resolved"));
        if (start.Value >= end.Value)
            return Result<NormalizedTimeEntryDuration>.Failure(Error.Validation("Cross-midnight or zero-length manual entries are not allowed"));
        if (end.Value >= TimeSpan.FromDays(1))
            return Result<NormalizedTimeEntryDuration>.Failure(Error.Validation("Manual entries must end on the same work date"));

        var elapsed = checked((int)Math.Round((end.Value - start.Value).TotalSeconds, MidpointRounding.AwayFromZero));
        var actual = actualSeconds ?? elapsed - breakSeconds - pauseSeconds;
        if (actual <= 0)
            return Result<NormalizedTimeEntryDuration>.Failure(Error.Validation("Actual work duration must be greater than zero"));
        if (actual + breakSeconds + pauseSeconds != elapsed)
            return Result<NormalizedTimeEntryDuration>.Failure(Error.Validation("Elapsed duration must equal actual work plus break and pause"));
        if (breakSeconds + pauseSeconds >= elapsed)
            return Result<NormalizedTimeEntryDuration>.Failure(Error.Validation("Break and pause must be less than elapsed duration"));

        return Result<NormalizedTimeEntryDuration>.Success(new(start.Value, end.Value, elapsed, actual, breakSeconds, pauseSeconds));
    }
}
