namespace NEXUS.Modules.ProjectPlanning.Domain.Scheduling;

/// <summary>
/// Builds non-persisted scheduling diagnostics from the same task, dependency
/// and calendar inputs used by the scheduling/critical-path engines.
/// </summary>
public static class SchedulingExplanationBuilder
{
    public static SchedulingExplanation Build(
        AutoScheduleTask task,
        IReadOnlyCollection<AutoScheduleTask> tasks,
        IReadOnlyCollection<AutoScheduleDependency> dependencies,
        IReadOnlyCollection<AutoScheduleCalendar> calendars,
        Guid? defaultCalendarId,
        CriticalPathTaskMetrics? metrics = null,
        bool hasCycle = false)
    {
        var predecessors = dependencies.Where(link => link.SuccessorId == task.Id).ToList();
        var successors = dependencies.Where(link => link.PredecessorId == task.Id).ToList();
        var effectiveCalendarId = task.CalendarId ?? defaultCalendarId;
        var calendar = calendars.FirstOrDefault(item => item.Id == effectiveCalendarId);
        var workingCalendar = new PlanningWorkingCalendar(calendar);
        var reasons = new List<SchedulingReason>();
        var issues = new List<SchedulingIssue>();

        if (task.SchedulingMode == PlanSchedulingMode.Manual)
            reasons.Add(new(SchedulingReasonCode.ManualDatesPreserved));
        if (task.CalendarId.HasValue)
            reasons.Add(new(SchedulingReasonCode.TaskCalendarOverride));
        if (predecessors.Count > 0)
        {
            reasons.Add(new(SchedulingReasonCode.HasPredecessors));
            foreach (var predecessor in predecessors)
            {
                reasons.Add(new(SchedulingReasonCode.MovedByPredecessor, predecessor.PredecessorId, predecessor.Type, predecessor.LagDays));
                AddDependencyIssue(task, tasks.FirstOrDefault(item => item.Id == predecessor.PredecessorId), predecessor, issues, workingCalendar);
            }
        }
        if (successors.Count > 0)
            reasons.Add(new(SchedulingReasonCode.HasSuccessors));

        if (task.ConstraintType is not PlanConstraintType.AsSoonAsPossible and not PlanConstraintType.AsLateAsPossible && task.ConstraintDate.HasValue)
        {
            reasons.Add(new(SchedulingReasonCode.ConstraintApplied, ConstraintType: task.ConstraintType));
            AddConstraintIssue(task, issues);
        }

        if (task.Start.HasValue && task.Finish.HasValue && task.Finish.Value > task.Start.Value &&
            workingCalendar.WorkingMinutesBetween(task.Start.Value, task.Finish.Value) < (task.Finish.Value - task.Start.Value).TotalMinutes)
            reasons.Add(new(SchedulingReasonCode.SkippedNonWorkingTime));

        var deadlineExceeded = task.Deadline.HasValue && task.Finish.HasValue && task.Finish.Value.Date > task.Deadline.Value.Date;
        if (deadlineExceeded)
        {
            var workingDays = Math.Max(1, (int)Math.Ceiling(Math.Abs(workingCalendar.WorkingMinutesBetween(task.Deadline!.Value, task.Finish!.Value)) / (decimal)workingCalendar.WorkingDayMinutes));
            reasons.Add(new(SchedulingReasonCode.DeadlineExceeded, WorkingDays: workingDays));
            issues.Add(new("DeadlineExceeded", $"Deadline exceeded by {workingDays} working day(s)", task.Id));
        }
        if (metrics?.Critical == true)
            reasons.Add(new(SchedulingReasonCode.Critical));
        if (hasCycle)
        {
            reasons.Add(new(SchedulingReasonCode.DependencyCycle));
            issues.Add(new("DependencyCycle", "A dependency cycle affects this task", task.Id));
        }
        if (issues.Count > 0)
            reasons.Add(new(SchedulingReasonCode.SchedulingConflict));

        return new SchedulingExplanation(task.Id, task.SchedulingMode, task.ConstraintType, task.ConstraintDate,
            task.Deadline, effectiveCalendarId, task.CalendarId.HasValue, task.Start, task.Finish, task.Duration,
            metrics?.TotalSlack, metrics?.FreeSlack, metrics?.Critical ?? false, deadlineExceeded,
            predecessors.Count > 0, successors.Count > 0, reasons, issues);
    }

    private static void AddConstraintIssue(AutoScheduleTask task, List<SchedulingIssue> issues)
    {
        if (!task.ConstraintDate.HasValue) return;
        var date = task.ConstraintDate.Value;
        var invalid = task.ConstraintType switch
        {
            PlanConstraintType.MustStartOn => !task.Start.HasValue || task.Start.Value.Date != date.Date,
            PlanConstraintType.MustFinishOn => !task.Finish.HasValue || task.Finish.Value.Date != date.Date,
            PlanConstraintType.StartNoEarlierThan => task.Start.HasValue && task.Start.Value < date,
            PlanConstraintType.StartNoLaterThan => task.Start.HasValue && task.Start.Value > date,
            PlanConstraintType.FinishNoEarlierThan => task.Finish.HasValue && task.Finish.Value < date,
            PlanConstraintType.FinishNoLaterThan => task.Finish.HasValue && task.Finish.Value > date,
            _ => false
        };
        if (invalid)
            issues.Add(new("ConstraintConflict", $"{task.ConstraintType} is not satisfied", task.Id));
    }

    private static void AddDependencyIssue(
        AutoScheduleTask task,
        AutoScheduleTask? predecessor,
        AutoScheduleDependency link,
        List<SchedulingIssue> issues,
        PlanningWorkingCalendar calendar)
    {
        if (predecessor is null || !task.Start.HasValue || !task.Finish.HasValue) return;
        var boundary = link.Type switch
        {
            PlanDependencyType.FinishToStart when predecessor.Finish.HasValue => calendar.ShiftWorkingDays(predecessor.Finish.Value, link.LagDays, true),
            PlanDependencyType.StartToStart when predecessor.Start.HasValue => calendar.ShiftWorkingDays(predecessor.Start.Value, link.LagDays, true),
            PlanDependencyType.FinishToFinish when predecessor.Finish.HasValue => calendar.ShiftWorkingDays(predecessor.Finish.Value, link.LagDays, false),
            PlanDependencyType.StartToFinish when predecessor.Start.HasValue => calendar.ShiftWorkingDays(predecessor.Start.Value, link.LagDays, false),
            _ => (DateTime?)null
        };
        var violated = link.Type switch
        {
            PlanDependencyType.FinishToStart or PlanDependencyType.StartToStart => boundary.HasValue && task.Start.Value < boundary.Value,
            _ => boundary.HasValue && task.Finish.Value < boundary.Value
        };
        if (violated)
            issues.Add(new("DependencyConflict", $"{link.Type} dependency is not satisfied", task.Id, predecessor.Id));
    }
}
