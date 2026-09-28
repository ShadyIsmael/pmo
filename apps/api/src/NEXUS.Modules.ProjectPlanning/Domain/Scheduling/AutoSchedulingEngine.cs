namespace NEXUS.Modules.ProjectPlanning.Domain.Scheduling;

/// <summary>Pure dependency-graph scheduler with no persistence concerns.</summary>
public sealed class AutoSchedulingEngine
{
    public AutoScheduleResult Recalculate(
        IReadOnlyCollection<AutoScheduleTask> tasks,
        IReadOnlyCollection<AutoScheduleDependency> dependencies,
        IReadOnlyCollection<AutoScheduleCalendar> calendars,
        Guid? defaultCalendarId,
        IReadOnlyCollection<Guid> seedActivityIds)
    {
        var taskById = tasks.ToDictionary(task => task.Id);
        var links = dependencies.Where(link => taskById.ContainsKey(link.PredecessorId) && taskById.ContainsKey(link.SuccessorId)).ToList();
        var cycle = FindCycleNodes(taskById.Keys, links);
        if (cycle.Count > 0) return new AutoScheduleResult([], cycle);

        var outgoing = links.GroupBy(link => link.PredecessorId).ToDictionary(group => group.Key, group => group.Select(link => link.SuccessorId).Distinct().ToList());
        var affected = Descendants(seedActivityIds.Where(taskById.ContainsKey), outgoing);
        if (affected.Count == 0) return new AutoScheduleResult([], []);

        var incoming = links.GroupBy(link => link.SuccessorId).ToDictionary(group => group.Key, group => group.ToList());
        var order = TopologicalOrder(affected, links);
        var calendarById = calendars.ToDictionary(calendar => calendar.Id, calendar => new PlanningWorkingCalendar(calendar));
        var fallback = new PlanningWorkingCalendar(null);
        var scheduled = taskById.ToDictionary(pair => pair.Key, pair => pair.Value);
        var changed = new List<ScheduledTask>();

        PlanningWorkingCalendar CalendarFor(AutoScheduleTask task) =>
            task.CalendarId.HasValue && calendarById.TryGetValue(task.CalendarId.Value, out var assigned) ? assigned :
            defaultCalendarId.HasValue && calendarById.TryGetValue(defaultCalendarId.Value, out var planDefault) ? planDefault : fallback;

        foreach (var activityId in order)
        {
            var task = scheduled[activityId];
            var calendar = CalendarFor(task);
            var duration = task.Type == PlanActivityType.Milestone ? 0 : Math.Max(0, task.Duration);
            var candidates = new List<DateTime>();

            if (incoming.TryGetValue(activityId, out var predecessors))
            {
                foreach (var link in predecessors)
                {
                    var predecessor = scheduled[link.PredecessorId];
                    var candidate = StartConstraint(predecessor, link, duration, calendar);
                    if (candidate.HasValue) candidates.Add(candidate.Value);
                }
            }

            var dependencyStart = candidates.Count > 0 ? calendar.NormalizeStart(candidates.Max()) : (DateTime?)null;
            DateTime? start;
            DateTime? finish;
            if (task.SchedulingMode == PlanSchedulingMode.Manual)
            {
                start = task.Start;
                finish = duration == 0 ? start ?? task.Finish : task.Finish;
            }
            else if (task.Anchor == SchedulingAnchor.Duration && task.Start.HasValue)
            {
                // A duration edit keeps the task's start fixed. Its finish,
                // and the dates of downstream tasks, are recalculated from
                // the new duration and the selected working calendar.
                start = calendar.NormalizeStart(task.Start.Value);
                finish = duration == 0 ? start : calendar.AddDuration(start.Value, duration);
            }
            else if (task.ConstraintType == PlanConstraintType.AsLateAsPossible && task.Finish.HasValue)
            {
                finish = duration == 0 ? calendar.NormalizeStart(task.Finish.Value) : NormalizeTaskFinish(task.Finish.Value, calendar);
                start = duration == 0 ? finish : calendar.SubtractDuration(finish.Value, duration);
            }
            else if (task.Anchor == SchedulingAnchor.Finish && task.Finish.HasValue)
            {
                // An explicit Finish edit is an anchor for the edited task.
                // ApplyConstraint below still validates that the resulting
                // start does not violate an incoming dependency; do not let
                // that dependency silently discard the requested Finish.
                finish = duration == 0 ? calendar.NormalizeStart(task.Finish.Value) : NormalizeTaskFinish(task.Finish.Value, calendar);
                start = duration == 0 ? finish : calendar.SubtractDuration(finish.Value, duration);
            }
            else if (dependencyStart.HasValue)
            {
                start = dependencyStart;
                finish = duration == 0 ? start : calendar.AddDuration(start.Value, duration);
            }
            else if (task.Start.HasValue)
            {
                start = calendar.NormalizeStart(task.Start.Value);
                finish = duration == 0 ? start : calendar.AddDuration(start.Value, duration);
            }
            else if (task.Finish.HasValue)
            {
                finish = duration == 0 ? calendar.NormalizeStart(task.Finish.Value) : NormalizeTaskFinish(task.Finish.Value, calendar);
                start = duration == 0 ? finish : calendar.SubtractDuration(finish.Value, duration);
            }
            else
            {
                start = null;
                finish = null;
            }

            if (task.SchedulingMode == PlanSchedulingMode.Automatic)
            {
                if ((!start.HasValue || !finish.HasValue) && task.ConstraintDate.HasValue)
                {
                    var constraintStart = calendar.NormalizeStart(task.ConstraintDate.Value);
                    var constraintFinish = NormalizeTaskFinish(task.ConstraintDate.Value, calendar);
                    if (task.ConstraintType is PlanConstraintType.MustStartOn or PlanConstraintType.StartNoEarlierThan or PlanConstraintType.StartNoLaterThan)
                    {
                        start = constraintStart;
                        finish = duration == 0 ? start : calendar.AddDuration(start.Value, duration);
                    }
                    else if (task.ConstraintType is PlanConstraintType.MustFinishOn or PlanConstraintType.FinishNoEarlierThan or PlanConstraintType.FinishNoLaterThan)
                    {
                        finish = constraintFinish;
                        start = duration == 0 ? calendar.NormalizeStart(finish.Value) : calendar.SubtractDuration(finish.Value, duration);
                    }
                }
                // An explicit duration edit anchors this task's start even if
                // an existing predecessor would otherwise suggest a later
                // start. The dependency still participates in downstream
                // scheduling; it must not invalidate the anchored edit here.
                var constraintDependencyStart = task.Anchor == SchedulingAnchor.Duration ? null : dependencyStart;
                var constrained = ApplyConstraint(task, start, finish, duration, constraintDependencyStart, calendar);
                if (constrained.ValidationError is not null)
                    return new AutoScheduleResult([], [], constrained.ValidationError);
                start = constrained.Start;
                finish = constrained.Finish;
            }

            var updated = task with { Start = start, Finish = finish, Duration = duration, Anchor = SchedulingAnchor.None };
            scheduled[activityId] = updated;
            if (task.Start != start || task.Finish != finish || task.Duration != duration)
                changed.Add(new ScheduledTask(activityId, start, finish, duration));
        }

        return new AutoScheduleResult(changed, []);
    }

    private static (DateTime? Start, DateTime? Finish, string? ValidationError) ApplyConstraint(
        AutoScheduleTask task,
        DateTime? start,
        DateTime? finish,
        int duration,
        DateTime? dependencyStart,
        PlanningWorkingCalendar calendar)
    {
        if (!start.HasValue || !finish.HasValue)
            return (start, finish, null);

        if (dependencyStart.HasValue && start < dependencyStart.Value)
            return (start, finish, $"Activity {task.Id} has conflicting dependency and {task.ConstraintType} constraints");

        if (!task.ConstraintDate.HasValue || task.ConstraintType is PlanConstraintType.AsSoonAsPossible or PlanConstraintType.AsLateAsPossible)
            return (start, finish, null);

        var constraintStart = calendar.NormalizeStart(task.ConstraintDate.Value);
        var constraintFinish = NormalizeTaskFinish(task.ConstraintDate.Value, calendar);
        switch (task.ConstraintType)
        {
            case PlanConstraintType.MustStartOn:
                start = constraintStart;
                finish = duration == 0 ? start : calendar.AddDuration(start.Value, duration);
                break;
            case PlanConstraintType.MustFinishOn:
                finish = constraintFinish;
                start = duration == 0 ? calendar.NormalizeStart(finish.Value) : calendar.SubtractDuration(finish.Value, duration);
                break;
            case PlanConstraintType.StartNoEarlierThan:
                if (start < constraintStart)
                {
                    start = constraintStart;
                    finish = duration == 0 ? start : calendar.AddDuration(start.Value, duration);
                }
                break;
            case PlanConstraintType.StartNoLaterThan:
                if (start > constraintStart)
                    return (start, finish, $"Activity {task.Id} cannot satisfy Start No Later Than {task.ConstraintDate:yyyy-MM-dd}");
                break;
            case PlanConstraintType.FinishNoEarlierThan:
                if (finish < constraintFinish)
                {
                    finish = constraintFinish;
                    start = duration == 0 ? calendar.NormalizeStart(finish.Value) : calendar.SubtractDuration(finish.Value, duration);
                    if (dependencyStart.HasValue && start < dependencyStart.Value)
                    {
                        start = dependencyStart.Value;
                        finish = duration == 0 ? start : calendar.AddDuration(start.Value, duration);
                    }
                }
                break;
            case PlanConstraintType.FinishNoLaterThan:
                if (finish > constraintFinish)
                    return (start, finish, $"Activity {task.Id} cannot satisfy Finish No Later Than {task.ConstraintDate:yyyy-MM-dd}");
                break;
        }

        if (dependencyStart.HasValue && start < dependencyStart.Value)
            return (start, finish, $"Activity {task.Id} has conflicting dependency and {task.ConstraintType} constraints");
        if (finish < start)
            return (start, finish, $"Activity {task.Id} has an impossible {task.ConstraintType} constraint");
        return (start, finish, null);
    }

    /// <summary>
    /// The Planning date editors send date-only values for Finish. Treat a
    /// midnight value as the end of that calendar date; otherwise subtracting
    /// a duration from midnight would incorrectly move the task to the prior
    /// working day.
    /// </summary>
    private static DateTime NormalizeTaskFinish(DateTime value, PlanningWorkingCalendar calendar) =>
        calendar.NormalizeFinish(value.TimeOfDay == TimeSpan.Zero
            ? value.Date.AddDays(1).AddTicks(-1)
            : value);

    private static DateTime? StartConstraint(AutoScheduleTask predecessor, AutoScheduleDependency link, int successorDuration, PlanningWorkingCalendar calendar)
    {
        return link.Type switch
        {
            PlanDependencyType.FinishToStart when predecessor.Finish.HasValue =>
                calendar.ShiftWorkingDays(calendar.NextWorkingStartAfter(predecessor.Finish.Value), link.LagDays, true),
            PlanDependencyType.StartToStart when predecessor.Start.HasValue =>
                calendar.ShiftWorkingDays(predecessor.Start.Value, link.LagDays, true),
            PlanDependencyType.FinishToFinish when predecessor.Finish.HasValue =>
                FinishConstraint(calendar.ShiftWorkingDays(predecessor.Finish.Value, link.LagDays, false), successorDuration, calendar),
            PlanDependencyType.StartToFinish when predecessor.Start.HasValue =>
                FinishConstraint(calendar.ShiftWorkingDays(predecessor.Start.Value, link.LagDays, false), successorDuration, calendar),
            _ => null
        };
    }

    private static DateTime FinishConstraint(DateTime requiredFinish, int duration, PlanningWorkingCalendar calendar)
    {
        var start = calendar.SubtractDuration(requiredFinish, duration);
        return duration > 0 && calendar.AddDuration(start, duration) < requiredFinish
            ? calendar.NormalizeStart(requiredFinish)
            : start;
    }

    private static HashSet<Guid> Descendants(IEnumerable<Guid> seeds, IReadOnlyDictionary<Guid, List<Guid>> outgoing)
    {
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>(seeds.Distinct());
        while (queue.TryDequeue(out var current))
        {
            if (!result.Add(current)) continue;
            if (outgoing.TryGetValue(current, out var next))
                foreach (var child in next) queue.Enqueue(child);
        }
        return result;
    }

    private static IReadOnlyList<Guid> TopologicalOrder(HashSet<Guid> affected, IReadOnlyCollection<AutoScheduleDependency> links)
    {
        var indegree = affected.ToDictionary(id => id, _ => 0);
        var outgoing = new Dictionary<Guid, List<Guid>>();
        foreach (var link in links.Where(link => affected.Contains(link.PredecessorId) && affected.Contains(link.SuccessorId)))
        {
            indegree[link.SuccessorId]++;
            if (!outgoing.TryGetValue(link.PredecessorId, out var next)) outgoing[link.PredecessorId] = next = [];
            next.Add(link.SuccessorId);
        }
        var queue = new Queue<Guid>(indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var order = new List<Guid>(affected.Count);
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
