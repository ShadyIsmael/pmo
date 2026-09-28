using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class PlanActivityBaselineComparisonTests
{
    [Fact]
    public void MatchingCurrentAndBaselineValuesProduceExplicitZeroVariances()
    {
        var date = new DateTime(2026, 3, 10);
        var current = new PlanActivityDto
        {
            Code = "A-1",
            StartDate = date,
            EndDate = date.AddDays(4),
            Duration = 4
        };

        PlanActivityBaselineComparison.Apply(current, new(date, date.AddDays(4), 4));

        Assert.Equal(0, current.StartVarianceDays);
        Assert.Equal(0, current.FinishVarianceDays);
        Assert.Equal(0, current.DurationVarianceDays);
    }

    [Fact]
    public void MissingComparisonDatesRemainNullWhileDurationZeroRemainsAValue()
    {
        var current = new PlanActivityDto { Code = "M-1", Duration = 0 };

        PlanActivityBaselineComparison.Apply(current, new(null, null, 0));

        Assert.Null(current.StartVarianceDays);
        Assert.Null(current.FinishVarianceDays);
        Assert.Equal(0, current.DurationVarianceDays);
    }
}
