namespace NEXUS.Modules.ProjectPlanning.Application;

/// <summary>
/// Resolves the immutable planned-hour total from explicit hours or from a
/// weekly commitment and its scheduled date range.
/// </summary>
internal static class PlanAssignmentHoursCalculator
{
    public static decimal? ResolvePlannedHours(
        decimal? plannedHours,
        decimal? hoursPerWeek,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (plannedHours.HasValue) return plannedHours.Value;
        if (!hoursPerWeek.HasValue || hoursPerWeek.Value <= 0m
            || !startDate.HasValue || !endDate.HasValue
            || endDate.Value.Date < startDate.Value.Date)
            return null;

        var scheduledDays = (endDate.Value.Date - startDate.Value.Date).TotalDays;
        var scheduleWeeks = Math.Max(1, (int)Math.Ceiling(scheduledDays / 7d));
        return hoursPerWeek.Value * scheduleWeeks;
    }
}
