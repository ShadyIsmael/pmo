using NEXUS.Modules.ProjectManagement.Application;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class ProjectEvmCalculatorTests
{
    [Fact]
    public void Calculates_standard_EVM_variances_and_indices_from_values()
    {
        var metrics = ProjectEvmCalculator.Calculate(
            plannedValue: 120m,
            earnedValue: 90m,
            actualCost: 100m,
            plannedValueUnavailableReason: null,
            earnedValueUnavailableReason: null,
            actualCostUnavailableReason: null);

        Assert.Equal(-30m, metrics.ScheduleVariance);
        Assert.Equal(-10m, metrics.CostVariance);
        Assert.Equal(0.75m, metrics.SchedulePerformanceIndex);
        Assert.Equal(0.9m, metrics.CostPerformanceIndex);
        Assert.Null(metrics.CostVarianceUnavailableReason);
    }

    [Fact]
    public void Does_not_compare_unbaselined_direct_costs_to_labor_only_earned_value()
    {
        var metrics = ProjectEvmCalculator.Calculate(
            plannedValue: 120m,
            earnedValue: 90m,
            actualCost: 150m,
            plannedValueUnavailableReason: null,
            earnedValueUnavailableReason: null,
            actualCostUnavailableReason: null,
            costComparisonUnavailableReason: "NON_LABOR_COSTS_NOT_BASELINED");

        Assert.Equal(-30m, metrics.ScheduleVariance);
        Assert.Null(metrics.CostVariance);
        Assert.Null(metrics.CostPerformanceIndex);
        Assert.Equal("NON_LABOR_COSTS_NOT_BASELINED", metrics.CostVarianceUnavailableReason);
        Assert.Equal("NON_LABOR_COSTS_NOT_BASELINED", metrics.CostPerformanceIndexUnavailableReason);
    }

    [Fact]
    public void Reports_why_an_index_cannot_be_calculated_instead_of_using_fallbacks()
    {
        var metrics = ProjectEvmCalculator.Calculate(
            plannedValue: 0m,
            earnedValue: 0m,
            actualCost: 0m,
            plannedValueUnavailableReason: null,
            earnedValueUnavailableReason: null,
            actualCostUnavailableReason: null);

        Assert.Null(metrics.SchedulePerformanceIndex);
        Assert.Null(metrics.CostPerformanceIndex);
        Assert.Equal("ZERO_PLANNED_VALUE", metrics.SchedulePerformanceIndexUnavailableReason);
        Assert.Equal("ZERO_ACTUAL_COST", metrics.CostPerformanceIndexUnavailableReason);
    }
}
