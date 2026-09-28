using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class AutoSchedulingEngineTests
{
    private static readonly Guid CalendarId = Guid.NewGuid();
    private readonly AutoSchedulingEngine _engine = new();

    [Fact]
    public void FinishToStart_RecalculatesOnlyTheDownstreamChain()
    {
        var first = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1, SchedulingAnchor.Start);
        var second = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1);
        var third = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1);
        var unrelated = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1);

        var result = Schedule(
            [first, second, third, unrelated],
            [Link(first, second, PlanDependencyType.FinishToStart), Link(second, third, PlanDependencyType.FinishToStart)],
            [first.Id]);

        Assert.False(result.HasCycle);
        Assert.Equal(new DateTime(2026, 1, 6, 8, 0, 0), Changed(result, second).Start);
        Assert.Equal(new DateTime(2026, 1, 7, 8, 0, 0), Changed(result, third).Start);
        Assert.DoesNotContain(result.ChangedTasks, item => item.Id == unrelated.Id);
    }

    [Theory]
    [InlineData(PlanDependencyType.StartToStart, 0, 5)]
    [InlineData(PlanDependencyType.FinishToFinish, 0, 5)]
    [InlineData(PlanDependencyType.StartToFinish, 0, 5)]
    [InlineData(PlanDependencyType.FinishToStart, 1, 7)]
    [InlineData(PlanDependencyType.FinishToStart, -1, 5)]
    public void DependencyTypesAndLagLead_AreApplied(PlanDependencyType type, int lag, int expectedStartDay)
    {
        var predecessor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1, SchedulingAnchor.Start);
        var successor = Task(new DateTime(2026, 1, 1, 8, 0, 0), 1);

        var result = Schedule([predecessor, successor], [Link(predecessor, successor, type, lag)], [predecessor.Id]);

        Assert.Equal(expectedStartDay, Changed(result, successor).Start!.Value.Day);
    }

    [Fact]
    public void Holiday_IsSkipped()
    {
        var predecessor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1, SchedulingAnchor.Start);
        var successor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1);
        var calendar = Calendar([new CalendarHolidayDefinition(new DateTime(2026, 1, 6), false)]);

        var result = _engine.Recalculate(
            [predecessor, successor],
            [Link(predecessor, successor, PlanDependencyType.FinishToStart)],
            [calendar], CalendarId, [predecessor.Id]);

        Assert.Equal(new DateTime(2026, 1, 7, 8, 0, 0), Changed(result, successor).Start);
    }

    [Fact]
    public void NonWorkingWeekday_IsSkipped()
    {
        var predecessor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 2, SchedulingAnchor.Start);
        var successor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1);
        var days = Enum.GetValues<DayOfWeek>().Select(day => new CalendarDayDefinition(
            day,
            day is not (DayOfWeek.Wednesday or DayOfWeek.Saturday or DayOfWeek.Sunday),
            new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), null, null)).ToList();

        var result = _engine.Recalculate(
            [predecessor, successor],
            [Link(predecessor, successor, PlanDependencyType.FinishToStart)],
            [new AutoScheduleCalendar(CalendarId, days, [])], CalendarId, [predecessor.Id]);

        Assert.Equal(new DateTime(2026, 1, 8, 8, 0, 0), Changed(result, successor).Start);
    }

    [Fact]
    public void HolidayRange_IsSkipped()
    {
        var predecessor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1, SchedulingAnchor.Start);
        var successor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1);
        var holiday = new CalendarHolidayDefinition(new DateTime(2026, 1, 6), new DateTime(2026, 1, 7), false);

        var result = _engine.Recalculate(
            [predecessor, successor],
            [Link(predecessor, successor, PlanDependencyType.FinishToStart)],
            [Calendar([holiday])], CalendarId, [predecessor.Id]);

        Assert.Equal(new DateTime(2026, 1, 8, 8, 0, 0), Changed(result, successor).Start);
    }

    [Fact]
    public void AssignedCalendar_OverridesThePlanDefault()
    {
        var assignedCalendarId = Guid.NewGuid();
        var predecessor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1, SchedulingAnchor.Start);
        var successor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1) with { CalendarId = assignedCalendarId };
        var planDefault = Calendar([new CalendarHolidayDefinition(new DateTime(2026, 1, 6), false)]);
        var assigned = Calendar() with { Id = assignedCalendarId };

        var result = _engine.Recalculate(
            [predecessor, successor],
            [Link(predecessor, successor, PlanDependencyType.FinishToStart)],
            [planDefault, assigned], CalendarId, [predecessor.Id]);

        Assert.Equal(new DateTime(2026, 1, 6, 8, 0, 0), Changed(result, successor).Start);
    }

    [Fact]
    public void Milestone_RemainsZeroDurationAtItsCalculatedDate()
    {
        var predecessor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1, SchedulingAnchor.Start);
        var milestone = new AutoScheduleTask(Guid.NewGuid(), PlanActivityType.Milestone, 4, null, null, CalendarId);

        var result = Schedule([predecessor, milestone], [Link(predecessor, milestone, PlanDependencyType.FinishToStart)], [predecessor.Id]);
        var changed = Changed(result, milestone);

        Assert.Equal(0, changed.Duration);
        Assert.Equal(changed.Start, changed.Finish);
        Assert.Equal(new DateTime(2026, 1, 6, 8, 0, 0), changed.Start);
    }

    [Fact]
    public void FinishDateOnly_IsLinkedToDurationOnTheSelectedCalendar()
    {
        var task = new AutoScheduleTask(
            Guid.NewGuid(),
            PlanActivityType.Task,
            2,
            null,
            new DateTime(2026, 1, 7),
            CalendarId,
            SchedulingAnchor.Finish);
        var calendar = new PlanningWorkingCalendar(Calendar());

        var result = Schedule([task], [], [task.Id]);
        var changed = Changed(result, task);
        var expectedFinish = calendar.NormalizeFinish(new DateTime(2026, 1, 7).Date.AddDays(1).AddTicks(-1));

        Assert.Equal(expectedFinish, changed.Finish);
        Assert.Equal(calendar.SubtractDuration(expectedFinish, task.Duration), changed.Start);
    }

    [Fact]
    public void DurationChangeFromStart_RecalculatesFinishWithoutChangingTheAnchor()
    {
        var task = Task(new DateTime(2026, 1, 5, 8, 0, 0), 3, SchedulingAnchor.Start);
        var calendar = new PlanningWorkingCalendar(Calendar());

        var result = Schedule([task], [], [task.Id]);
        var changed = Changed(result, task);

        Assert.Equal(calendar.NormalizeStart(task.Start!.Value), changed.Start);
        Assert.Equal(calendar.AddDuration(changed.Start!.Value, task.Duration), changed.Finish);
    }

    [Fact]
    public void DurationChange_KeepsStartEvenWhenPredecessorWouldSuggestAnotherStart()
    {
        var predecessor = Task(new DateTime(2026, 1, 12, 8, 0, 0), 1, SchedulingAnchor.Start);
        var successor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 2, SchedulingAnchor.Duration);
        var calendar = new PlanningWorkingCalendar(Calendar());

        var result = Schedule(
            [predecessor, successor],
            [Link(predecessor, successor, PlanDependencyType.FinishToStart)],
            [successor.Id]);
        var changed = Changed(result, successor);

        Assert.Equal(calendar.NormalizeStart(successor.Start!.Value), changed.Start);
        Assert.Equal(calendar.AddDuration(changed.Start!.Value, successor.Duration), changed.Finish);
    }

    [Fact]
    public void ManualTask_RetainsItsDatesWhenSchedulingIsTriggered()
    {
        var predecessor = Task(new DateTime(2026, 1, 12, 8, 0, 0), 1, SchedulingAnchor.Start);
        var manual = Task(new DateTime(2026, 1, 5, 8, 0, 0), 2) with
        {
            SchedulingMode = PlanSchedulingMode.Manual
        };

        var result = Schedule(
            [predecessor, manual],
            [Link(predecessor, manual, PlanDependencyType.FinishToStart)],
            [predecessor.Id]);

        Assert.DoesNotContain(result.ChangedTasks, item => item.Id == manual.Id);
    }

    [Theory]
    [InlineData(PlanConstraintType.MustStartOn)]
    [InlineData(PlanConstraintType.StartNoEarlierThan)]
    public void StartConstraints_AreApplied(PlanConstraintType constraint)
    {
        var task = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1, SchedulingAnchor.Start) with
        {
            ConstraintType = constraint,
            ConstraintDate = new DateTime(2026, 1, 7)
        };

        var result = Schedule([task], [], [task.Id]);

        Assert.Null(result.ValidationError);
        Assert.Equal(new DateTime(2026, 1, 7, 8, 0, 0), Changed(result, task).Start);
    }

    [Fact]
    public void FinishNoLaterThan_ReturnsValidationErrorWhenDependencyCannotFit()
    {
        var predecessor = Task(new DateTime(2026, 1, 12, 8, 0, 0), 1, SchedulingAnchor.Start);
        var successor = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1) with
        {
            ConstraintType = PlanConstraintType.FinishNoLaterThan,
            ConstraintDate = new DateTime(2026, 1, 7)
        };

        var result = Schedule([predecessor, successor], [Link(predecessor, successor, PlanDependencyType.FinishToStart)], [predecessor.Id]);

        Assert.Contains("Finish No Later", result.ValidationError);
        Assert.Empty(result.ChangedTasks);
    }

    [Fact]
    public void MustFinishOn_CanSeedAnUndatedTask()
    {
        var task = new AutoScheduleTask(Guid.NewGuid(), PlanActivityType.Task, 1, null, null, CalendarId)
        {
            ConstraintType = PlanConstraintType.MustFinishOn,
            ConstraintDate = new DateTime(2026, 1, 8)
        };

        var result = Schedule([task], [], [task.Id]);

        Assert.Null(result.ValidationError);
        Assert.Equal(8, Changed(result, task).Finish!.Value.Day);
    }

    [Fact]
    public void Cycle_IsReportedWithoutDateChanges()
    {
        var first = Task(new DateTime(2026, 1, 5, 8, 0, 0), 1);
        var second = Task(new DateTime(2026, 1, 6, 8, 0, 0), 1);

        var result = Schedule(
            [first, second],
            [Link(first, second, PlanDependencyType.FinishToStart), Link(second, first, PlanDependencyType.FinishToStart)],
            [first.Id]);

        Assert.True(result.HasCycle);
        Assert.Empty(result.ChangedTasks);
        Assert.Equal(2, result.CycleActivityIds.Count);
    }

    private AutoScheduleResult Schedule(IReadOnlyCollection<AutoScheduleTask> tasks, IReadOnlyCollection<AutoScheduleDependency> links, IReadOnlyCollection<Guid> seeds) =>
        _engine.Recalculate(tasks, links, [Calendar()], CalendarId, seeds);

    private static AutoScheduleTask Task(DateTime start, int duration, SchedulingAnchor anchor = SchedulingAnchor.None) =>
        new(Guid.NewGuid(), PlanActivityType.Task, duration, start, start.AddDays(duration), CalendarId, anchor);

    private static AutoScheduleDependency Link(AutoScheduleTask predecessor, AutoScheduleTask successor, PlanDependencyType type, int lag = 0) =>
        new(predecessor.Id, successor.Id, type, lag);

    private static ScheduledTask Changed(AutoScheduleResult result, AutoScheduleTask task) =>
        Assert.Single(result.ChangedTasks, item => item.Id == task.Id);

    private static AutoScheduleCalendar Calendar(IReadOnlyCollection<CalendarHolidayDefinition>? holidays = null) => new(
        CalendarId,
        Enum.GetValues<DayOfWeek>().Select(day => new CalendarDayDefinition(
            day,
            day is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
            new TimeSpan(8, 0, 0),
            new TimeSpan(17, 0, 0),
            new TimeSpan(12, 0, 0),
            new TimeSpan(13, 0, 0))).ToList(),
        holidays ?? []);
}
