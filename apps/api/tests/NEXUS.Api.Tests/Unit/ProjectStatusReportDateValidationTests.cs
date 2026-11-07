using System.Reflection;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class ProjectStatusReportDateValidationTests
{
    private static readonly MethodInfo ValidateDraft = typeof(ProjectStatusReportService)
        .GetMethod("ValidateDraft", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Error? Invoke(DateTime today, DateTime periodFrom, DateTime periodTo, decimal? progress) =>
        (Error?)ValidateDraft.Invoke(null, [today, periodFrom, periodTo, progress])!;

    [Fact]
    public void Rejects_period_to_in_the_future()
    {
        var today = DateTime.Today;
        var future = today.AddDays(1);

        var result = Invoke(today, today, future, 50m);

        Assert.NotNull(result);
        Assert.Equal("Reporting period end must not be later than today", result.Message);
    }

    [Fact]
    public void Accepts_period_to_equal_to_today()
    {
        var today = DateTime.Today;

        var result = Invoke(today, today, today, 50m);

        Assert.Null(result);
    }

    [Fact]
    public void Accepts_period_to_in_the_past()
    {
        var today = DateTime.Today;
        var past = today.AddDays(-30);

        var result = Invoke(today, past.AddDays(-15), past, 50m);

        Assert.Null(result);
    }

    [Fact]
    public void Rejects_period_from_after_period_to()
    {
        var today = DateTime.Today;

        var result = Invoke(today, today.AddDays(1), today, 50m);

        Assert.NotNull(result);
        Assert.Equal("Reporting period start must be before its end", result.Message);
    }

    [Fact]
    public void Rejects_default_period_dates()
    {
        var today = DateTime.Today;

        var result1 = Invoke(today, default, today, 50m);
        Assert.NotNull(result1);
        Assert.Equal("Reporting period is required", result1.Message);

        var result2 = Invoke(today, today, default, 50m);
        Assert.NotNull(result2);
        Assert.Equal("Reporting period is required", result2.Message);

        var result3 = Invoke(today, default, default, 50m);
        Assert.NotNull(result3);
        Assert.Equal("Reporting period is required", result3.Message);
    }

    [Fact]
    public void Rejects_invalid_progress()
    {
        var today = DateTime.Today;

        var result1 = Invoke(today, today, today, -1m);
        Assert.NotNull(result1);
        Assert.Equal("Overall progress must be between 0 and 100", result1.Message);

        var result2 = Invoke(today, today, today, 101m);
        Assert.NotNull(result2);
        Assert.Equal("Overall progress must be between 0 and 100", result2.Message);
    }

    [Fact]
    public void Accepts_boundary_progress_values()
    {
        var today = DateTime.Today;

        Assert.Null(Invoke(today, today, today, 0m));
        Assert.Null(Invoke(today, today, today, 100m));
    }
}
