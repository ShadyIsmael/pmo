using FluentAssertions;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Domain;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public sealed class ResourceLaborCostCalculatorTests
{
    private readonly ResourceLaborCostCalculator _calculator = new();

    [Fact]
    public void HourlyProfile_UsesAmountAsBaseRate()
    {
        var result = Calculate(new ResourceCostProfile { CompensationType = CompensationType.Hourly, Amount = 200, Currency = "EGP" }, 7, 5, 5, 0);

        result.BaseEffectiveHourlyCost.Should().Be(200);
        result.RegularActualLaborCost.Should().Be(1000);
        result.ExcessHours.Should().Be(0);
        result.TotalActualLaborCost.Should().Be(1000);
    }

    [Fact]
    public void HourlyNonEligibleExcess_IsStillCostedAtBaseRate()
    {
        var result = Calculate(new ResourceCostProfile { CompensationType = CompensationType.Hourly, Amount = 200, OvertimeEligible = false }, 7, 0, 7, 2);

        result.RegularActualHours.Should().Be(7);
        result.ExcessHours.Should().Be(2);
        result.OvertimeHours.Should().Be(0);
        result.NonEligibleExcessCost.Should().Be(400);
        result.TotalActualLaborCost.Should().Be(1800);
        result.UnusedCapacityCost.Should().Be(0);
    }

    [Fact]
    public void OvertimeMultiplier_UsesFullOvertimeRateOnce()
    {
        var result = Calculate(new ResourceCostProfile
        {
            CompensationType = CompensationType.Hourly,
            Amount = 200,
            OvertimeEligible = true,
            OvertimeRateType = OvertimeRateTypeConstants.Multiplier,
            OvertimeRateValue = 1.5m
        }, 7, 0, 7, 1);

        result.OvertimeHours.Should().Be(1);
        result.OvertimeCost.Should().Be(300);
        result.TotalActualLaborCost.Should().Be(1700);
    }

    [Fact]
    public void OvertimeFixedRate_UsesProfileCurrencyRate()
    {
        var result = Calculate(new ResourceCostProfile
        {
            CompensationType = CompensationType.Weekly,
            Amount = 3500,
            Currency = "EGP",
            OvertimeEligible = true,
            OvertimeRateType = OvertimeRateTypeConstants.FixedHourlyRate,
            OvertimeRateValue = 300
        }, 7, 0, 7, 2, 35);

        result.BaseEffectiveHourlyCost.Should().Be(100);
        result.OvertimeCost.Should().Be(600);
        result.TotalActualLaborCost.Should().Be(1300);
    }

    [Fact]
    public void FixedWeeklyProfile_ReconcilesRegularAndUnusedCapacityToCommittedCost()
    {
        var result = Calculate(new ResourceCostProfile { CompensationType = CompensationType.Weekly, Amount = 3500, Currency = "EGP" }, 7, 0, 5, 0, 35);

        result.RegularActualLaborCost.Should().Be(500);
        result.UnusedCapacityHours.Should().Be(2);
        result.UnusedCapacityCost.Should().Be(200);
        result.CommittedCapacityCost.Should().Be(700);
        result.TotalResourceCost.Should().Be(700);
    }

    [Fact]
    public void MonthlyProfile_UsesProvidedProductiveCapacityNotHardCodedHours()
    {
        var result = Calculate(new ResourceCostProfile { CompensationType = CompensationType.Monthly, Amount = 20000, Currency = "EGP" }, 7, 7, 7, 0, 140);

        result.BaseEffectiveHourlyCost.Should().Be(20000m / 140m);
        result.UnusedCapacityCost.Should().Be(0);
        result.CommittedCapacityCost.Should().Be(1000);
    }

    [Fact]
    public void ZeroCapacity_WithActualWork_IsExcessAndDoesNotDivideByZero()
    {
        var result = Calculate(new ResourceCostProfile
        {
            CompensationType = CompensationType.Hourly,
            Amount = 200,
            OvertimeEligible = true,
            OvertimeRateType = OvertimeRateTypeConstants.Multiplier,
            OvertimeRateValue = 2
        }, 0, 0, 0, 3);

        result.ActualUtilization.Should().BeNull();
        result.RegularActualHours.Should().Be(0);
        result.ExcessHours.Should().Be(3);
        result.OvertimeHours.Should().Be(3);
        result.TotalActualLaborCost.Should().Be(1200);
    }

    [Fact]
    public void MissingProfile_PreservesHoursAndMarksCoverageUnavailable()
    {
        var result = Calculate(null, 7, 4, 4, 0);

        result.PlannedHours.Should().Be(4);
        result.ActualHours.Should().Be(4);
        result.PlannedLaborCost.Should().BeNull();
        result.TotalActualLaborCost.Should().BeNull();
        result.CostProfileMissing.Should().BeTrue();
        result.CostCoverageComplete.Should().BeFalse();
    }

    private ResourceDayCostResult Calculate(
        ResourceCostProfile? profile,
        decimal capacity,
        decimal planned,
        decimal regular,
        decimal excess,
        decimal compensationCapacity = 1)
        => _calculator.Calculate(new ResourceDayCostInput(profile, capacity, planned, regular, excess, compensationCapacity));
}
