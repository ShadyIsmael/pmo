using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class SchedulingExplanationBuilderTests
{
    [Fact]
    public void ReportsManualPredecessorCalendarAndDeadlineReasons()
    {
        var predecessorId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var predecessor = new AutoScheduleTask(predecessorId, PlanActivityType.Task, 1, new DateTime(2026, 1, 5, 8, 0, 0), new DateTime(2026, 1, 6, 8, 0, 0), null);
        var task = new AutoScheduleTask(taskId, PlanActivityType.Task, 1, new DateTime(2026, 1, 6, 8, 0, 0), new DateTime(2026, 1, 8, 8, 0, 0), Guid.NewGuid(), SchedulingAnchor.None, PlanSchedulingMode.Manual, PlanConstraintType.StartNoEarlierThan, new DateTime(2026, 1, 6), new DateTime(2026, 1, 7));
        var result = SchedulingExplanationBuilder.Build(task, [predecessor, task], [new AutoScheduleDependency(predecessorId, taskId, PlanDependencyType.FinishToStart, 0)], [], null);

        Assert.True(result.CalendarOverride);
        Assert.True(result.HasPredecessors);
        Assert.True(result.DeadlineExceeded);
        Assert.Contains(result.Reasons, reason => reason.Code == SchedulingReasonCode.ManualDatesPreserved);
        Assert.Contains(result.Reasons, reason => reason.Code == SchedulingReasonCode.MovedByPredecessor && reason.RelatedActivityId == predecessorId);
        Assert.Contains(result.Reasons, reason => reason.Code == SchedulingReasonCode.DeadlineExceeded);
    }
}
