using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Domain.Scheduling;

/// <summary>
/// Calculates CPM metrics from already scheduled task dates. This class never
/// changes task dates and has no persistence or presentation dependencies.
/// </summary>
public sealed class CriticalPathAnalyzer
{
    private const decimal Epsilon = 0.000001m;

    public CriticalPathAnalysisResult Analyze(
        IReadOnlyCollection<AutoScheduleTask> tasks,
        IReadOnlyCollection<AutoScheduleDependency> dependencies,
        IReadOnlyCollection<AutoScheduleCalendar> calendars,
        Guid? defaultCalendarId)
    {
        var taskById = tasks.ToDictionary(task => task.Id);
        var links = dependencies
            .Where(link => taskById.ContainsKey(link.PredecessorId) && taskById.ContainsKey(link.SuccessorId))
            .ToList();
        var cycle = FindCycleNodes(taskById.Keys, links);
        if (cycle.Count > 0)
            return new CriticalPathAnalysisResult(new Dictionary<Guid, CriticalPathTaskMetrics>(), [], cycle);

        var order = TopologicalOrder(taskById.Keys, links);
        var outgoing = links.GroupBy(link => link.PredecessorId).ToDictionary(group => group.Key, group => group.ToList());
        var incoming = links.GroupBy(link => link.SuccessorId).ToDictionary(group => group.Key, group => group.ToList());
        var calendarById = calendars.ToDictionary(calendar => calendar.Id, calendar => new PlanningWorkingCalendar(calendar));
        var fallback = new PlanningWorkingCalendar(null);

        PlanningWorkingCalendar CalendarFor(AutoScheduleTask task) =>
            task.CalendarId.HasValue && calendarById.TryGetValue(task.CalendarId.Value, out var assigned) ? assigned :
            defaultCalendarId.HasValue && calendarById.TryGetValue(defaultCalendarId.Value, out var planDefault) ? planDefault : fallback;

        var datedTasks = tasks.Where(task => task.Start.HasValue && task.Finish.HasValue).ToList();
        var projectFinish = datedTasks.Select(task => task.Finish!.Value).DefaultIfEmpty().Max();
        if (datedTasks.Count == 0)
            return new CriticalPathAnalysisResult(new Dictionary<Guid, CriticalPathTaskMetrics>(), [], []);

        var lateStart = new Dictionary<Guid, DateTime>();
        var lateFinish = new Dictionary<Guid, DateTime>();
        foreach (var activityId in order.AsEnumerable().Reverse())
        {
            var task = taskById[activityId];
            if (!task.Start.HasValue || !task.Finish.HasValue) continue;
            var calendar = CalendarFor(task);
            var duration = task.Type == PlanActivityType.Milestone ? 0 : Math.Max(0, task.Duration);

            void ApplyStartBound(DateTime bound)
            {
                var candidate = lateStart.TryGetValue(activityId, out var current) && current < bound ? current : bound;
                lateStart[activityId] = candidate;
                var derivedFinish = duration == 0 ? candidate : calendar.AddDuration(candidate, duration);
                if (!lateFinish.TryGetValue(activityId, out var currentFinish) || derivedFinish < currentFinish)
                    lateFinish[activityId] = derivedFinish;
            }

            void ApplyFinishBound(DateTime bound)
            {
                var candidate = lateFinish.TryGetValue(activityId, out var current) && current < bound ? current : bound;
                lateFinish[activityId] = candidate;
                var derivedStart = duration == 0 ? candidate : calendar.SubtractDuration(candidate, duration);
                if (!lateStart.TryGetValue(activityId, out var currentStart) || derivedStart < currentStart)
                    lateStart[activityId] = derivedStart;
            }

            if (!outgoing.TryGetValue(activityId, out var successors) || successors.Count == 0)
            {
                ApplyFinishBound(projectFinish);
                continue;
            }

            foreach (var link in successors)
            {
                if (!lateStart.TryGetValue(link.SuccessorId, out var successorLateStart) ||
                    !lateFinish.TryGetValue(link.SuccessorId, out var successorLateFinish)) continue;

                var successorCalendar = CalendarFor(taskById[link.SuccessorId]);
                switch (link.Type)
                {
                    case PlanDependencyType.FinishToStart:
                        ApplyFinishBound(InverseFinishToStart(successorLateStart, link.LagDays, successorCalendar));
                        break;
                    case PlanDependencyType.StartToStart:
                        ApplyStartBound(InverseStartBoundary(successorLateStart, link.LagDays, successorCalendar));
                        break;
                    case PlanDependencyType.FinishToFinish:
                        ApplyFinishBound(InverseFinishBoundary(successorLateFinish, link.LagDays, successorCalendar));
                        break;
                    case PlanDependencyType.StartToFinish:
                        ApplyStartBound(InverseFinishBoundary(successorLateFinish, link.LagDays, successorCalendar));
                        break;
                }
            }
        }

        var metrics = new Dictionary<Guid, CriticalPathTaskMetrics>();
        foreach (var task in tasks)
        {
            if (!task.Start.HasValue || !task.Finish.HasValue || !lateStart.TryGetValue(task.Id, out var taskLateStart))
            {
                metrics[task.Id] = new CriticalPathTaskMetrics(task.Id, null, null, false);
                continue;
            }

            var calendar = CalendarFor(task);
            var totalSlack = ToDays(calendar, task.Start.Value, taskLateStart);
            var freeSlack = CalculateFreeSlack(task, outgoing.GetValueOrDefault(task.Id) ?? [], taskById, CalendarFor, projectFinish);
            metrics[task.Id] = new CriticalPathTaskMetrics(task.Id, totalSlack, freeSlack, totalSlack <= Epsilon);
        }

        var criticalPath = order.Where(activityId => metrics.TryGetValue(activityId, out var metric) && metric.Critical).ToList();
        return new CriticalPathAnalysisResult(metrics, criticalPath, []);
    }

    private static decimal? CalculateFreeSlack(
        AutoScheduleTask task,
        IReadOnlyCollection<AutoScheduleDependency> successors,
        IReadOnlyDictionary<Guid, AutoScheduleTask> taskById,
        Func<AutoScheduleTask, PlanningWorkingCalendar> calendarFor,
        DateTime projectFinish)
    {
        if (!task.Start.HasValue || !task.Finish.HasValue) return null;
        var taskCalendar = calendarFor(task);
        var bounds = new List<(DateTime Boundary, bool FinishBased)>();
        foreach (var link in successors)
        {
            var successor = taskById[link.SuccessorId];
            var successorCalendar = calendarFor(successor);
            DateTime? bound = link.Type switch
            {
                PlanDependencyType.FinishToStart when successor.Start.HasValue => InverseFinishToStart(successor.Start.Value, link.LagDays, successorCalendar),
                PlanDependencyType.StartToStart when successor.Start.HasValue => InverseStartBoundary(successor.Start.Value, link.LagDays, successorCalendar),
                PlanDependencyType.FinishToFinish when successor.Finish.HasValue => InverseFinishBoundary(successor.Finish.Value, link.LagDays, successorCalendar),
                PlanDependencyType.StartToFinish when successor.Finish.HasValue => InverseFinishBoundary(successor.Finish.Value, link.LagDays, successorCalendar),
                _ => null
            };
            if (bound.HasValue) bounds.Add((bound.Value, link.Type is PlanDependencyType.FinishToStart or PlanDependencyType.FinishToFinish));
        }

        if (bounds.Count == 0) bounds.Add((projectFinish, true));
        var slackMinutes = bounds.Select(bound => taskCalendar.WorkingMinutesBetween(bound.FinishBased ? task.Finish.Value : task.Start.Value, bound.Boundary)).Min();
        return decimal.Round(slackMinutes / (decimal)taskCalendar.WorkingDayMinutes, 2, MidpointRounding.AwayFromZero);
    }

    private static DateTime InverseFinishToStart(DateTime successorStart, int lagDays, PlanningWorkingCalendar calendar) =>
        calendar.ShiftWorkingDays(calendar.PreviousWorkingEndBefore(successorStart), -lagDays, false);

    private static DateTime InverseStartBoundary(DateTime successorStart, int lagDays, PlanningWorkingCalendar calendar) =>
        calendar.ShiftWorkingDays(successorStart, -lagDays, true);

    private static DateTime InverseFinishBoundary(DateTime successorFinish, int lagDays, PlanningWorkingCalendar calendar) =>
        calendar.ShiftWorkingDays(successorFinish, -lagDays, false);

    private static decimal ToDays(PlanningWorkingCalendar calendar, DateTime from, DateTime to) =>
        decimal.Round(calendar.WorkingMinutesBetween(from, to) / (decimal)calendar.WorkingDayMinutes, 2, MidpointRounding.AwayFromZero);

    private static IReadOnlyList<Guid> TopologicalOrder(IEnumerable<Guid> taskIds, IReadOnlyCollection<AutoScheduleDependency> links)
    {
        var indegree = taskIds.ToDictionary(id => id, _ => 0);
        var outgoing = new Dictionary<Guid, List<Guid>>();
        foreach (var link in links)
        {
            indegree[link.SuccessorId]++;
            if (!outgoing.TryGetValue(link.PredecessorId, out var next)) outgoing[link.PredecessorId] = next = [];
            next.Add(link.SuccessorId);
        }

        var queue = new Queue<Guid>(indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var order = new List<Guid>(indegree.Count);
        while (queue.TryDequeue(out var current))
        {
            order.Add(current);
            if (!outgoing.TryGetValue(current, out var next)) continue;
            foreach (var child in next)
                if (--indegree[child] == 0) queue.Enqueue(child);
        }
        return order;
    }

    private static IReadOnlyList<Guid> FindCycleNodes(IEnumerable<Guid> taskIds, IReadOnlyCollection<AutoScheduleDependency> links)
    {
        var indegree = taskIds.ToDictionary(id => id, _ => 0);
        var outgoing = new Dictionary<Guid, List<Guid>>();
        foreach (var link in links)
        {
            indegree[link.SuccessorId]++;
            if (!outgoing.TryGetValue(link.PredecessorId, out var next)) outgoing[link.PredecessorId] = next = [];
            next.Add(link.SuccessorId);
        }

        var queue = new Queue<Guid>(indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        while (queue.TryDequeue(out var current))
        {
            if (!outgoing.TryGetValue(current, out var next)) continue;
            foreach (var child in next)
                if (--indegree[child] == 0) queue.Enqueue(child);
        }
        return indegree.Where(pair => pair.Value > 0).Select(pair => pair.Key).ToList();
    }
}
