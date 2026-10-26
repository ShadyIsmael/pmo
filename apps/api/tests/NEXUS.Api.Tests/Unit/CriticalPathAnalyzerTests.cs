using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class CriticalPathAnalyzerTests
{
    private static readonly Guid CalendarId = Guid.NewGuid();
    private readonly CriticalPathAnalyzer _analyzer = new();

    [Fact]
    public void CalculatesTotalAndFreeSlackWithoutChangingScheduledDates()
    {
        var first = Task("A", new DateTime(2026, 1, 5, 8, 0, 0), new DateTime(2026, 1, 5, 17, 0, 0));
        var critical = Task("B", new DateTime(2026, 1, 6, 8, 0, 0), new DateTime(2026, 1, 6, 17, 0, 0));
        var last = Task("C", new DateTime(2026, 1, 7, 8, 0, 0), new DateTime(2026, 1, 7, 17, 0, 0));
        var nonCritical = Task("D", new DateTime(2026, 1, 5, 8, 0, 0), new DateTime(2026, 1, 5, 17, 0, 0));
        var originalStart = nonCritical.Start;
        var originalFinish = nonCritical.Finish;

        var result = _analyzer.Analyze(
            [first, critical, last, nonCritical],
            [Link(first, critical), Link(critical, last)],
            [Calendar()],
            CalendarId);

        Assert.False(result.HasCycle);
        Assert.Equal(0m, result.ActivityMetrics[first.Id].TotalSlack);
        Assert.Equal(0m, result.ActivityMetrics[critical.Id].TotalSlack);
        Assert.Equal(0m, result.ActivityMetrics[last.Id].TotalSlack);
        Assert.True(result.ActivityMetrics[nonCritical.Id].TotalSlack >= 1m);
        Assert.Equal(result.ActivityMetrics[nonCritical.Id].TotalSlack, result.ActivityMetrics[nonCritical.Id].FreeSlack);
        Assert.Equal(new[] { first.Id, critical.Id, last.Id }, result.CriticalPathActivityIds);
        Assert.Equal(originalStart, nonCritical.Start);
        Assert.Equal(originalFinish, nonCritical.Finish);
    }

    [Theory]
    [InlineData(PlanDependencyType.FinishToStart)]
    [InlineData(PlanDependencyType.StartToStart)]
    [InlineData(PlanDependencyType.FinishToFinish)]
    [InlineData(PlanDependencyType.StartToFinish)]
    public void SupportsAllDependencyTypesAndLagLead(PlanDependencyType type)
    {
        var predecessor = Task("A", new DateTime(2026, 1, 5, 8, 0, 0), new DateTime(2026, 1, 5, 17, 0, 0));
        var successor = Task("B", new DateTime(2026, 1, 6, 8, 0, 0), new DateTime(2026, 1, 6, 17, 0, 0));

        var result = _analyzer.Analyze([predecessor, successor], [Link(predecessor, successor, type, -1)], [Calendar()], CalendarId);

        Assert.False(result.HasCycle);
        Assert.All(result.ActivityMetrics.Values, metric => Assert.NotNull(metric.TotalSlack));
    }

    [Fact]
    public void MilestoneIsZeroSlackWhenItDefinesTheProjectFinish()
    {
        var milestone = new AutoScheduleTask(Guid.NewGuid(), PlanActivityType.Milestone, 0,
            new DateTime(2026, 1, 7, 8, 0, 0), new DateTime(2026, 1, 7, 8, 0, 0), CalendarId);

        var result = _analyzer.Analyze([milestone], [], [Calendar()], CalendarId);

        Assert.Equal(0m, result.ActivityMetrics[milestone.Id].TotalSlack);
        Assert.True(result.ActivityMetrics[milestone.Id].Critical);
    }

    [Fact]
    public void IdentifiesMultipleCriticalPathsAndOnlyDrivingDependencyLinks()
    {
        var monday = new DateTime(2026, 1, 5, 8, 0, 0);
        var tuesday = new DateTime(2026, 1, 6, 8, 0, 0);
        var wednesday = new DateTime(2026, 1, 7, 8, 0, 0);
        var a = Task("A", monday, monday.AddHours(9));
        var b = Task("B", tuesday, tuesday.AddHours(9));
        var d = Task("D", monday, monday.AddHours(9));
        var e = Task("E", tuesday, tuesday.AddHours(9));
        var shortPath = Task("SHORT", monday, monday.AddHours(9));
        var finish = new AutoScheduleTask(Guid.NewGuid(), PlanActivityType.Milestone, 0, wednesday, wednesday, CalendarId);
        var aToB = LinkWithId(a, b);
        var bToFinish = LinkWithId(b, finish);
        var dToE = LinkWithId(d, e);
        var eToFinish = LinkWithId(e, finish);
        var shortToFinish = LinkWithId(shortPath, finish);
        var nonDrivingShortcut = LinkWithId(a, finish);

        var result = _analyzer.Analyze(
            [a, b, d, e, shortPath, finish],
            [aToB, bToFinish, dToE, eToFinish, shortToFinish, nonDrivingShortcut],
            [Calendar()],
            CalendarId);

        Assert.False(result.HasCycle);
        Assert.Contains(a.Id, result.CriticalPathActivityIds);
        Assert.Contains(b.Id, result.CriticalPathActivityIds);
        Assert.Contains(d.Id, result.CriticalPathActivityIds);
        Assert.Contains(e.Id, result.CriticalPathActivityIds);
        Assert.Contains(finish.Id, result.CriticalPathActivityIds);
        Assert.True(result.ActivityMetrics[shortPath.Id].TotalSlack > 0m);
        Assert.Equal(monday, result.ActivityMetrics[a.Id].EarlyStart);
        Assert.Equal(monday.AddHours(9), result.ActivityMetrics[a.Id].EarlyFinish);
        Assert.Equal(monday, result.ActivityMetrics[a.Id].LateStart);
        Assert.Equal(monday.AddHours(9), result.ActivityMetrics[a.Id].LateFinish);
        Assert.Equal(new[] { aToB.Id!.Value, bToFinish.Id!.Value, dToE.Id!.Value, eToFinish.Id!.Value }.Order(), result.CriticalPathDependencyIds.Order());
        Assert.DoesNotContain(shortToFinish.Id!.Value, result.CriticalPathDependencyIds);
        Assert.DoesNotContain(nonDrivingShortcut.Id!.Value, result.CriticalPathDependencyIds);
    }

    [Fact]
    public void ReanalysisAfterDurationRescheduleCanChangeTheCriticalPath()
    {
        var monday = new DateTime(2026, 1, 5, 8, 0, 0);
        var tuesday = new DateTime(2026, 1, 6, 8, 0, 0);
        var wednesday = new DateTime(2026, 1, 7, 8, 0, 0);
        var first = Task("A", monday, monday.AddHours(9));
        var second = Task("B", tuesday, tuesday.AddHours(9));
        var alternate = new AutoScheduleTask(Guid.NewGuid(), PlanActivityType.Task, 2, monday, tuesday.AddHours(9), CalendarId);
        var finish = new AutoScheduleTask(Guid.NewGuid(), PlanActivityType.Milestone, 0, wednesday, wednesday, CalendarId);
        var firstToSecond = LinkWithId(first, second);
        var secondToFinish = LinkWithId(second, finish);
        var alternateToFinish = LinkWithId(alternate, finish);
        var links = new[] { firstToSecond, secondToFinish, alternateToFinish };
        var calendar = Calendar();

        var before = _analyzer.Analyze([first, second, alternate, finish], links, [calendar], CalendarId);
        Assert.Contains(alternate.Id, before.CriticalPathActivityIds);

        var shorterAlternate = alternate with { Duration = 1, Anchor = SchedulingAnchor.Duration };
        var scheduled = new AutoSchedulingEngine().Recalculate(
            [first, second, shorterAlternate, finish],
            links,
            [calendar],
            CalendarId,
            [alternate.Id]);
        var revisedTasks = new[] { first, second, shorterAlternate, finish }
            .Select(task => scheduled.ChangedTasks.FirstOrDefault(changed => changed.Id == task.Id) is { } changed
                ? task with { Start = changed.Start, Finish = changed.Finish, Duration = changed.Duration, Anchor = SchedulingAnchor.None }
                : task)
            .ToArray();
        var after = _analyzer.Analyze(revisedTasks, links, [calendar], CalendarId);

        Assert.DoesNotContain(alternate.Id, after.CriticalPathActivityIds);
        Assert.Contains(first.Id, after.CriticalPathActivityIds);
        Assert.Contains(second.Id, after.CriticalPathActivityIds);
        Assert.DoesNotContain(alternateToFinish.Id!.Value, after.CriticalPathDependencyIds);
        Assert.Contains(secondToFinish.Id!.Value, after.CriticalPathDependencyIds);
    }

    [Fact]
    public void CycleIsReportedWithoutProducingMetrics()
    {
        var first = Task("A", new DateTime(2026, 1, 5, 8, 0, 0), new DateTime(2026, 1, 5, 17, 0, 0));
        var second = Task("B", new DateTime(2026, 1, 6, 8, 0, 0), new DateTime(2026, 1, 6, 17, 0, 0));

        var result = _analyzer.Analyze([first, second], [Link(first, second), Link(second, first)], [Calendar()], CalendarId);

        Assert.True(result.HasCycle);
        Assert.Empty(result.ActivityMetrics);
    }

    private static AutoScheduleTask Task(string code, DateTime start, DateTime finish) =>
        new(Guid.NewGuid(), PlanActivityType.Task, 1, start, finish, CalendarId);

    private static AutoScheduleDependency Link(AutoScheduleTask predecessor, AutoScheduleTask successor, PlanDependencyType type = PlanDependencyType.FinishToStart, int lag = 0) =>
        new(predecessor.Id, successor.Id, type, lag);

    private static AutoScheduleDependency LinkWithId(AutoScheduleTask predecessor, AutoScheduleTask successor, PlanDependencyType type = PlanDependencyType.FinishToStart, int lag = 0) =>
        new(predecessor.Id, successor.Id, type, lag, Guid.NewGuid());

    private static AutoScheduleCalendar Calendar() => new(
        CalendarId,
        Enum.GetValues<DayOfWeek>().Select(day => new CalendarDayDefinition(
            day,
            day is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
            new TimeSpan(8, 0, 0),
            new TimeSpan(17, 0, 0),
            new TimeSpan(12, 0, 0),
            new TimeSpan(13, 0, 0))).ToList(),
        []);
}
