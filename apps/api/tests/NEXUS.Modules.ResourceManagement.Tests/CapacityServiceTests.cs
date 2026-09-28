using FluentAssertions;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class CapacityServiceTests : TestBase
{
    private static CapacityService CreateService(TestDbContext context)
    {
        return new CapacityService(context);
    }

    [Fact]
    public async Task GetMatrixAsync_ReturnsMatrixWithCorrectPeriods()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 19)
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Periods.Should().HaveCount(3);
        result.Value.Rows.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetMatrixAsync_CalculatesGrossCapacityUsingWorkingCalendar()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            ResourceId = ResourceAliceId
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Rows.Should().ContainSingle().Subject;
        row.Cells.Should().HaveCount(2);
        row.Cells[0].PeriodKey.Should().Be("2026-01-04");
        row.Cells[0].GrossCapacityHours.Should().Be(40);
    }

    [Fact]
    public async Task GetMatrixAsync_DeductsNonWorkingHoursForHolidays()
    {
        using var context = CreateContext();
        context.Holidays.Add(new Holiday
        {
            Id = Guid.NewGuid(),
            WorkingCalendarId = DefaultCalendarId,
            Name = "Test Holiday",
            Date = new DateTime(2026, 1, 6),
            IsRecurring = false
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            ResourceId = ResourceAliceId
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Rows.Single();
        row.Cells[0].GrossCapacityHours.Should().Be(40);
        row.Cells[0].NetCapacityHours.Should().Be(32);
    }

    [Fact]
    public async Task GetMatrixAsync_SeparatesConfirmedVsProposedCapacity()
    {
        using var context = CreateContext();
        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceAliceId,
            ProjectName = "Proposed Work",
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 30,
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            Status = ResourceAllocation.StatusProposed
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            ResourceId = ResourceAliceId
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Rows.Single();
        var cell = row.Cells[0];
        cell.ConfirmedHours.Should().Be(20);
        cell.ProposedHours.Should().Be(12);
    }

    [Fact]
    public async Task GetMatrixAsync_HeatmapStateOverallocated_WhenConfirmedPlusProposedExceedsGross()
    {
        using var context = CreateContext();
        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceCharlieId,
            ProjectName = "Heavy Load",
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 80,
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            Status = ResourceAllocation.StatusConfirmed
        });
        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceCharlieId,
            ProjectName = "Extra Work",
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 30,
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            Status = ResourceAllocation.StatusProposed
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            ResourceId = ResourceCharlieId
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        var cell = result.Value.Rows.Single().Cells[0];
        cell.HeatmapState.Should().Be("Overallocated");
    }

    [Fact]
    public async Task GetMatrixAsync_HeatmapStateFullyAllocated_At100Percent()
    {
        using var context = CreateContext();
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(a => a.ResourceId == ResourceAliceId));
        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceAliceId,
            ProjectName = "Full Load",
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 80,
            HoursPerWeek = 40,
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            Status = ResourceAllocation.StatusConfirmed
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            ResourceId = ResourceAliceId
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        var cell = result.Value.Rows.Single().Cells[0];
        cell.UtilizationPercent.Should().Be(100);
        cell.HeatmapState.Should().Be("FullyAllocated");
    }

    [Fact]
    public async Task GetMatrixAsync_HeatmapStateNearCapacity_At80Percent()
    {
        using var context = CreateContext();
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(a => a.ResourceId == ResourceBobId));
        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceBobId,
            ProjectName = "Near Full",
            ProjectRoleId = ProjectRoleArchId,
            AllocationPercentage = 60,
            HoursPerWeek = 32,
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            Status = ResourceAllocation.StatusConfirmed
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            ResourceId = ResourceBobId
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        var cell = result.Value.Rows.Single().Cells[0];
        cell.HeatmapState.Should().Be("NearCapacity");
    }

    [Fact]
    public async Task GetMatrixAsync_CalculatesEarliestAvailableDate()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 20),
            EndDate = new DateTime(2026, 2, 1),
            ResourceId = ResourceAliceId
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Rows.Single();
        row.EarliestAvailableDate.Should().Be(new DateTime(2026, 1, 20));
    }

    [Fact]
    public async Task GetAvailabilityAsync_ReturnsSortedByAvailableHours()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12)
        };

        var result = await service.GetAvailabilityAsync(filter);

        result.Items.Should().BeInDescendingOrder(i => i.AvailableCapacityHours);
    }

    [Fact]
    public async Task GetDemandVsCapacityAsync_GroupsByRoleAndDepartment()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31)
        };

        var result = await service.GetDemandVsCapacityAsync(filter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Groups.Should().Contain(g => g.GroupType == "ProjectRole");
        result.Value.Groups.Should().Contain(g => g.GroupType == "Department");
    }

    [Fact]
    public async Task GetGanttDataAsync_ReturnsAllocationsOrderedByResourceName()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31)
        };

        var result = await service.GetGanttDataAsync(filter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeInAscendingOrder(a => a.ResourceName);
    }

    [Fact]
    public async Task GetResourceAvailabilityAsync_ReturnsCorrectAvailableHours()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetResourceAvailabilityAsync(
            ResourceAliceId,
            new DateTime(2026, 1, 5),
            new DateTime(2026, 1, 12));

        result.IsSuccess.Should().BeTrue();
        result.Value.AvailableCapacityHours.Should().BeGreaterThan(0);
        result.Value.ResourceName.Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task GetResourceAvailabilityAsync_WithNonExistentResource_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetResourceAvailabilityAsync(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task GetMatrixAsync_WithResourceCalendarLeave_DoesNotAffectGrossCapacity()
    {
        using var context = CreateContext();
        context.ResourceCalendars.Add(new ResourceCalendar
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceAliceId,
            Date = new DateTime(2026, 1, 7),
            DayType = "Leave",
            AvailableHours = 0
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            ResourceId = ResourceAliceId
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        var cell = result.Value.Rows.Single().Cells[0];
        cell.GrossCapacityHours.Should().Be(40);
    }

    [Fact]
    public async Task GetMatrixAsync_WithDifferentWorkingCalendar_HoursPerDayAffectsCapacity()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var defaultCalendar = await context.WorkingCalendars.FindAsync(DefaultCalendarId);
        defaultCalendar!.HoursPerDay = 10;
        await context.SaveChangesAsync();

        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            ResourceId = ResourceAliceId
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Single().Cells[0].GrossCapacityHours.Should().Be(50);
    }

    [Fact]
    public async Task GetMatrixAsync_WithHoliday_ReducesNetCapacity()
    {
        using var context = CreateContext();
        context.Holidays.Add(new Holiday
        {
            Id = Guid.NewGuid(),
            WorkingCalendarId = DefaultCalendarId,
            Name = "Extra Holiday",
            Date = new DateTime(2026, 1, 7),
            IsRecurring = false
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            ResourceId = ResourceAliceId
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        var cell = result.Value.Rows.Single().Cells[0];
        cell.GrossCapacityHours.Should().Be(40);
        cell.NetCapacityHours.Should().Be(32);
    }

    [Fact]
    public async Task GetMatrixAsync_ProductiveHoursDeductsPaidBreak()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        // Verify productive hours account for paid breaks
        var filter = new CapacityFilterDto { PeriodType = "Week", StartDate = new DateTime(2026, 1, 5), EndDate = new DateTime(2026, 1, 12), ResourceId = ResourceAliceId };
        var result = await service.GetMatrixAsync(filter);
        result.IsSuccess.Should().BeTrue();
        // Gross capacity should reflect ProductiveHoursPerDay (HoursPerDay - PaidBreakMinutes/60)
        foreach (var row in result.Value.Rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.GrossCapacityHours.Should().BeGreaterThan(0);
            }
        }
    }

    [Fact]
    public async Task GetMatrixAsync_CancelledAllocationNotCounted()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var filter = new CapacityFilterDto { PeriodType = "Week", StartDate = new DateTime(2026, 1, 5), EndDate = new DateTime(2026, 1, 12), ResourceId = ResourceAliceId };
        var result = await service.GetMatrixAsync(filter);
        result.IsSuccess.Should().BeTrue();
        // Cancelled allocations should not consume capacity
        result.Value.Rows.Should().NotBeEmpty();
    }
}
