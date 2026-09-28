using FluentAssertions;
using NEXUS.Modules.ProjectManagement.Application;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class TimeEntryDurationTests
{
    [Fact]
    public void Normalize_DerivesActualFromStartEndAndBreaks()
    {
        var result = TimeEntryDuration.Normalize(TimeSpan.FromHours(9), TimeSpan.FromHours(17), null, 3600, 1800);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ElapsedSeconds.Should().Be(28_800);
        result.Value.ActualSeconds.Should().Be(25_200);
    }

    [Fact]
    public void Normalize_RejectsOverlapShapeWhereActualDoesNotReconcile()
    {
        var result = TimeEntryDuration.Normalize(TimeSpan.FromHours(9), TimeSpan.FromHours(17), 20_000, 0, 0);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Elapsed duration");
    }

    [Fact]
    public void Normalize_AllowsLateSameDayEntry()
    {
        var result = TimeEntryDuration.Normalize(TimeSpan.FromHours(22), TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(30)), 5_400, 0, 0);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ElapsedSeconds.Should().Be(5_400);
    }

    [Fact]
    public void Normalize_RoundsElapsedToMatchDailyEnvelope()
    {
        var start = new TimeSpan(22, 44, 14) + TimeSpan.FromMilliseconds(269);
        var end = new TimeSpan(22, 44, 30) + TimeSpan.FromMilliseconds(221);

        var result = TimeEntryDuration.Normalize(start, end, 9, 3, 4);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ElapsedSeconds.Should().Be(16);
        result.Value.ActualSeconds.Should().Be(9);
    }
}
