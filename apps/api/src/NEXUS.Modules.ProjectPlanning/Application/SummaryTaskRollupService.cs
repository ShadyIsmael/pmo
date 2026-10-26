using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;

namespace NEXUS.Modules.ProjectPlanning.Application;

/// <summary>
/// Calculates the current values of summary activities from the WBS hierarchy.
/// The calculation is deliberately kept separate from persistence and mapping so
/// every write path can apply the same rules before its transaction is committed.
/// </summary>
public sealed class SummaryTaskRollupService
{
    private readonly DbContext _context;

    public SummaryTaskRollupService(DbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlySet<Guid>> RecalculateAsync(
        Guid planVersionId,
        string userId,
        IReadOnlyCollection<Guid>? forceLeafActivityIds = null,
        CancellationToken cancellationToken = default)
    {
        var activities = await _context.Set<PlanActivity>()
            .Where(activity => activity.PlanVersionId == planVersionId)
            .ToListAsync(cancellationToken);
        MergeAdded(activities, planVersionId);
        activities = activities.Where(activity => !activity.IsDeleted).DistinctBy(activity => activity.Id).ToList();

        var nodes = await _context.Set<PlanWbsNode>()
            .Where(node => node.PlanVersionId == planVersionId)
            .ToListAsync(cancellationToken);
        MergeAdded(nodes, planVersionId);
        nodes = nodes.Where(node => !node.IsDeleted).DistinctBy(node => node.Id).ToList();

        var calendars = await _context.Set<PlanCalendar>()
            .Include(calendar => calendar.DayRules)
            .Include(calendar => calendar.Holidays)
            .Where(calendar => calendar.PlanVersionId == planVersionId && !calendar.IsDeleted)
            .ToListAsync(cancellationToken);
        MergeAdded(calendars, planVersionId);
        calendars = calendars.Where(calendar => !calendar.IsDeleted).DistinctBy(calendar => calendar.Id).ToList();

        var calendarById = calendars.ToDictionary(calendar => calendar.Id, calendar => new PlanningWorkingCalendar(ToCalendar(calendar)));
        var fallbackCalendar = new PlanningWorkingCalendar(null);
        var defaultCalendar = calendars
            .OrderByDescending(calendar => calendar.IsDefault)
            .ThenBy(calendar => calendar.Name)
            .Select(calendar => calendarById[calendar.Id])
            .FirstOrDefault() ?? fallbackCalendar;

        var nodeById = nodes.ToDictionary(node => node.Id);
        var childNodes = nodes
            .Where(node => node.ParentWbsId.HasValue && nodeById.ContainsKey(node.ParentWbsId.Value))
            .GroupBy(node => node.ParentWbsId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var activitiesByWbs = activities
            .Where(activity => activity.WbsId.HasValue && nodeById.ContainsKey(activity.WbsId.Value))
            .GroupBy(activity => activity.WbsId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(activity => activity.SortOrder).ThenBy(activity => activity.Code).ToList());
        var activityById = activities.ToDictionary(activity => activity.Id);
        var resultByNode = new Dictionary<Guid, RollupResult>();
        var visiting = new HashSet<Guid>();
        var summaryActivityIds = new HashSet<Guid>();
        bool HasEffectiveTask(Guid nodeId, HashSet<Guid>? path = null)
        {
            if (FindActivity(nodeId) is not null) return true;
            path ??= [];
            if (!path.Add(nodeId)) return false;
            var result = (childNodes.GetValueOrDefault(nodeId) ?? []).Any(child => HasEffectiveTask(child.Id, path));
            path.Remove(nodeId);
            return result;
        }

        RollupResult EvaluateNode(Guid nodeId)
        {
            if (resultByNode.TryGetValue(nodeId, out var cached)) return cached;
            if (!visiting.Add(nodeId)) return RollupResult.Empty;

            var children = (childNodes.GetValueOrDefault(nodeId) ?? []).Where(child => HasEffectiveTask(child.Id)).ToList();
            var childResults = children.Select(child => EvaluateNode(child.Id)).ToList();
            var own = FindActivity(nodeId);
            RollupResult result;

            if (children.Count == 0)
            {
                result = own is null
                    ? RollupResult.Empty
                    : RollupResult.FromActivity(own);
            }
            else
            {
                if (own is not null) summaryActivityIds.Add(own.Id);
                result = Aggregate(childResults, own is null ? defaultCalendar : CalendarFor(own));
                if (own is not null) Apply(own, result, userId);
            }

            visiting.Remove(nodeId);
            resultByNode[nodeId] = result;
            return result;
        }

        foreach (var node in nodes.Where(node => !node.ParentWbsId.HasValue || !nodeById.ContainsKey(node.ParentWbsId.Value)))
            EvaluateNode(node.Id);

        // Activities whose WBS link is malformed or points at a removed node are
        // treated as ordinary leaf activities; their explicit values are kept.
        foreach (var activity in activities.Where(activity => activity.WbsId.HasValue && !summaryActivityIds.Contains(activity.Id)))
        {
            if (activity.Type == PlanActivityType.Milestone) activity.Duration = 0;
        }

        foreach (var activityId in forceLeafActivityIds ?? [])
        {
            if (!summaryActivityIds.Contains(activityId) && activityById.TryGetValue(activityId, out var formerSummary))
            {
                formerSummary.Type = PlanActivityType.Task;
                formerSummary.StartDate = null;
                formerSummary.EndDate = null;
                formerSummary.Duration = 0;
                formerSummary.Progress = null;
                formerSummary.UpdatedBy = userId;
            }
        }

        return summaryActivityIds;

        PlanActivity? FindActivity(Guid nodeId)
        {
            var node = nodeById[nodeId];
            if (node.ActivityId.HasValue && activityById.TryGetValue(node.ActivityId.Value, out var linked)) return linked;
            return activitiesByWbs.GetValueOrDefault(nodeId)?.FirstOrDefault();
        }

        PlanningWorkingCalendar CalendarFor(PlanActivity activity) =>
            activity.CalendarId.HasValue && calendarById.TryGetValue(activity.CalendarId.Value, out var selected)
                ? selected
                : defaultCalendar;

    }

    public async Task<bool> IsSummaryTaskAsync(Guid activityId, Guid planVersionId, CancellationToken cancellationToken = default)
    {
        var wbsId = await _context.Set<PlanActivity>()
            .Where(activity => activity.Id == activityId && activity.PlanVersionId == planVersionId && !activity.IsDeleted)
            .Select(activity => activity.WbsId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!wbsId.HasValue) return false;
        return await _context.Set<PlanWbsNode>()
            .AnyAsync(node => node.Id == wbsId.Value && node.PlanVersionId == planVersionId && !node.IsDeleted &&
                              _context.Set<PlanWbsNode>().Any(child => child.ParentWbsId == node.Id && !child.IsDeleted), cancellationToken);
    }

    private static RollupResult Aggregate(
        IReadOnlyCollection<RollupResult> children,
        PlanningWorkingCalendar calendar)
    {
        var dated = children.Where(child => child.Start.HasValue || child.Finish.HasValue).ToList();
        var start = dated.Where(child => child.Start.HasValue).Select(child => child.Start!.Value).DefaultIfEmpty().Min();
        var finish = dated.Where(child => child.Finish.HasValue).Select(child => child.Finish!.Value).DefaultIfEmpty().Max();
        DateTime? effectiveStart = dated.Any(child => child.Start.HasValue) ? start : null;
        DateTime? effectiveFinish = dated.Any(child => child.Finish.HasValue) ? finish : null;
        var durationStart = dated.Any(child => child.Start.HasValue) ? start : (DateTime?)null;
        var duration = durationStart.HasValue && effectiveFinish.HasValue
            ? Math.Max(0, (int)Math.Round(calendar.WorkingMinutesBetween(durationStart.Value, effectiveFinish.Value) / (double)calendar.WorkingDayMinutes, MidpointRounding.AwayFromZero))
            : 0;

        var weighted = children.Where(child => child.Duration > 0 && child.Progress.HasValue).ToList();
        decimal? progress;
        if (weighted.Count > 0)
        {
            var denominator = weighted.Sum(child => (decimal)child.Duration);
            progress = denominator == 0 ? null : weighted.Sum(child => child.Duration * child.Progress!.Value) / denominator;
        }
        else
        {
            var valid = children.Where(child => child.Progress.HasValue).Select(child => child.Progress!.Value).ToList();
            progress = valid.Count == 0 ? null : valid.Average();
        }

        return new RollupResult(effectiveStart, effectiveFinish, duration, Clamp(progress));
    }

    private static void Apply(PlanActivity activity, RollupResult result, string userId)
    {
        activity.Type = PlanActivityType.Task;
        activity.StartDate = result.Start;
        activity.EndDate = result.Finish;
        activity.Duration = result.Duration;
        activity.Progress = result.Progress;
        activity.UpdatedBy = userId;
    }

    private static decimal? Clamp(decimal? value) => value.HasValue ? Math.Clamp(value.Value, 0m, 100m) : null;

    private static AutoScheduleCalendar ToCalendar(PlanCalendar calendar) => new(
        calendar.Id,
        calendar.DayRules.Where(rule => !rule.IsDeleted).Select(rule => new CalendarDayDefinition(rule.DayOfWeek, rule.IsWorkingDay, rule.StartTime, rule.EndTime, rule.BreakStartTime, rule.BreakEndTime)).ToList(),
        calendar.Holidays.Where(holiday => !holiday.IsDeleted).Select(holiday => new CalendarHolidayDefinition(holiday.Date, holiday.EndDate, holiday.IsRecurring)).ToList());

    private void MergeAdded<T>(ICollection<T> entities, Guid planVersionId) where T : NEXUS.SharedKernel.BaseEntity
    {
        foreach (var entry in _context.ChangeTracker.Entries<T>().Where(entry => entry.State == EntityState.Added))
        {
            var versionId = entry.Entity switch
            {
                PlanActivity activity => activity.PlanVersionId,
                PlanWbsNode node => node.PlanVersionId,
                PlanCalendar calendar => calendar.PlanVersionId,
                _ => Guid.Empty
            };
            if (versionId == planVersionId && entities.All(entity => entity.Id != entry.Entity.Id)) entities.Add(entry.Entity);
        }
    }

    private sealed record RollupResult(DateTime? Start, DateTime? Finish, int Duration, decimal? Progress)
    {
        public static RollupResult Empty { get; } = new(null, null, 0, null);
        public static RollupResult FromActivity(PlanActivity activity) => new(activity.StartDate, activity.EndDate, Math.Max(0, activity.Type == PlanActivityType.Milestone ? 0 : activity.Duration), Clamp(activity.Progress));
    }
}
