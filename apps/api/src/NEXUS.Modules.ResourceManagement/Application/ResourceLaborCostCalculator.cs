using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Application;

public sealed record ResourceDayCostInput(
    ResourceCostProfile? Profile,
    decimal CapacityHours,
    decimal PlannedHours,
    decimal RegularActualHours,
    decimal ExcessHours,
    decimal CompensationPeriodCapacityHours);

public sealed record ResourceDayCostResult(
    string? Currency,
    decimal CapacityHours,
    decimal PlannedHours,
    decimal ActualHours,
    decimal RegularActualHours,
    decimal ExcessHours,
    decimal OvertimeHours,
    decimal PlanningGapHours,
    decimal ExecutionGapHours,
    decimal UnusedCapacityHours,
    decimal? PlannedUtilization,
    decimal? ActualUtilization,
    decimal? BaseEffectiveHourlyCost,
    decimal? PlannedLaborCost,
    decimal? RegularActualLaborCost,
    decimal? NonEligibleExcessCost,
    decimal? OvertimeCost,
    decimal? TotalActualLaborCost,
    decimal? CommittedCapacityCost,
    decimal? UnusedCapacityCost,
    decimal? TotalResourceCost,
    bool CostProfileMissing,
    bool CostCoverageComplete,
    decimal UncostedActualHours,
    decimal UncostedPlannedHours);

/// <summary>
/// Canonical, side-effect-free labor cost calculation. The analytics query
/// supplies capacity and the full compensation-period capacity so fixed
/// Weekly/Monthly profiles never fall back to a hard-coded 40/160-hour base.
/// </summary>
public sealed class ResourceLaborCostCalculator
{
    public ResourceDayCostResult Calculate(ResourceDayCostInput input)
    {
        var capacity = Math.Max(0m, input.CapacityHours);
        var planned = Math.Max(0m, input.PlannedHours);
        var regular = Math.Clamp(input.RegularActualHours, 0m, Math.Max(0m, input.RegularActualHours + input.ExcessHours));
        var excess = Math.Max(0m, input.ExcessHours);
        var actual = regular + excess;
        var planningGap = capacity - planned;
        var executionGap = planned - actual;
        var unused = Math.Max(capacity - regular, 0m);
        decimal? plannedUtilization = capacity > 0m ? planned / capacity * 100m : null;
        decimal? actualUtilization = capacity > 0m ? actual / capacity * 100m : null;

        if (input.Profile is null)
        {
            return new ResourceDayCostResult(
                null, capacity, planned, actual, regular, excess, 0m,
                planningGap, executionGap, unused, plannedUtilization, actualUtilization,
                null, null, null, null, null, null, null, null, null, true, false, actual, planned);
        }

        var profile = input.Profile;
        var compensationCapacity = Math.Max(0m, input.CompensationPeriodCapacityHours);
        var baseHourlyCost = GetEffectiveHourlyCost(profile, compensationCapacity);
        var overtimeHours = profile.OvertimeEligible ? excess : 0m;
        var regularCost = regular * baseHourlyCost;
        var nonEligibleExcessCost = !profile.OvertimeEligible && profile.CompensationType == CompensationType.Hourly
            ? excess * baseHourlyCost
            : 0m;
        var overtimeRate = profile.OvertimeEligible
            ? GetOvertimeRate(profile, baseHourlyCost)
            : 0m;
        var overtimeCost = overtimeHours * overtimeRate;
        var totalActual = regularCost + nonEligibleExcessCost + overtimeCost;
        var committed = profile.CompensationType == CompensationType.Hourly || compensationCapacity <= 0m
            ? 0m
            : profile.Amount * capacity / compensationCapacity;
        var unusedCost = profile.CompensationType == CompensationType.Hourly
            ? 0m
            : unused * baseHourlyCost;

        return new ResourceDayCostResult(
            profile.Currency,
            capacity,
            planned,
            actual,
            regular,
            excess,
            overtimeHours,
            planningGap,
            executionGap,
            unused,
            plannedUtilization,
            actualUtilization,
            baseHourlyCost,
            planned * baseHourlyCost,
            regularCost,
            nonEligibleExcessCost,
            overtimeCost,
            totalActual,
            committed,
            unusedCost,
            totalActual + unusedCost,
            false,
            true,
            0m,
            0m);
    }

    private static decimal GetEffectiveHourlyCost(ResourceCostProfile profile, decimal compensationPeriodCapacityHours)
        => profile.CompensationType == CompensationType.Hourly
            ? profile.Amount
            : compensationPeriodCapacityHours > 0m ? profile.Amount / compensationPeriodCapacityHours : 0m;

    private static decimal GetOvertimeRate(ResourceCostProfile profile, decimal baseHourlyCost)
        => profile.OvertimeRateType switch
        {
            OvertimeRateTypeConstants.Multiplier => baseHourlyCost * Math.Max(0m, profile.OvertimeRateValue ?? 0m),
            OvertimeRateTypeConstants.FixedHourlyRate => Math.Max(0m, profile.OvertimeRateValue ?? 0m),
            _ => baseHourlyCost
        };
}
