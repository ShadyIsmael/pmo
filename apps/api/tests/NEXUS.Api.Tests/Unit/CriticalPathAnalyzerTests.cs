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
