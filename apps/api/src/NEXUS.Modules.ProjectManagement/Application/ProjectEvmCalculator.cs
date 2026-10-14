namespace NEXUS.Modules.ProjectManagement.Application;

public sealed record ProjectEvmMetrics(
    decimal? ScheduleVariance,
    decimal? CostVariance,
    decimal? SchedulePerformanceIndex,
    decimal? CostPerformanceIndex,
    string? SchedulePerformanceIndexUnavailableReason,
    string? CostPerformanceIndexUnavailableReason,
    string? CostVarianceUnavailableReason);

/// <summary>Applies the standard EVM variance and index formulas without fallback values.</summary>
public static class ProjectEvmCalculator
{
    public static ProjectEvmMetrics Calculate(
        decimal? plannedValue,
        decimal? earnedValue,
        decimal? actualCost,
        string? plannedValueUnavailableReason,
        string? earnedValueUnavailableReason,
        string? actualCostUnavailableReason,
        string? costComparisonUnavailableReason = null)
    {
        var scheduleVariance = earnedValue.HasValue && plannedValue.HasValue ? earnedValue - plannedValue : null;
        var costVariance = earnedValue.HasValue && actualCost.HasValue && costComparisonUnavailableReason is null
            ? earnedValue - actualCost : null;
        var spi = earnedValue.HasValue && plannedValue.HasValue && plannedValue.Value != 0m
            ? earnedValue / plannedValue : null;
        var cpi = earnedValue.HasValue && actualCost.HasValue && actualCost.Value != 0m
            && costComparisonUnavailableReason is null
            ? earnedValue / actualCost : null;
        var spiReason = spi.HasValue ? null
            : !earnedValue.HasValue ? earnedValueUnavailableReason
            : !plannedValue.HasValue ? plannedValueUnavailableReason
            : "ZERO_PLANNED_VALUE";
        var cpiReason = cpi.HasValue ? null
            : !earnedValue.HasValue ? earnedValueUnavailableReason
            : !actualCost.HasValue ? actualCostUnavailableReason
            : costComparisonUnavailableReason ?? "ZERO_ACTUAL_COST";
        var costVarianceReason = costVariance.HasValue ? null
            : !earnedValue.HasValue ? earnedValueUnavailableReason
            : !actualCost.HasValue ? actualCostUnavailableReason
            : costComparisonUnavailableReason ?? "EVM_DATA_INCOMPLETE";

        return new ProjectEvmMetrics(scheduleVariance, costVariance, spi, cpi, spiReason, cpiReason, costVarianceReason);
    }
}
