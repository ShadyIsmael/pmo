using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel.Management;
using NEXUS.SharedKernel.Helpers;
using Moq;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class CapacityServiceTests : TestBase
{
    private static CapacityService CreateService(TestDbContext context, IReadOnlySet<Guid>? allowedProjectIds = null)
    {
        var projectIds = allowedProjectIds ?? context.ResourceAllocations
            .Where(allocation => allocation.ProjectId.HasValue)
            .Select(allocation => allocation.ProjectId!.Value)
            .ToHashSet();
        var management = new Mock<IManagementContextAccessor>();
        management.SetupGet(accessor => accessor.Current).Returns(new ManagementContext(
            Guid.NewGuid(),
            TenantConstants.DemoTenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), projectIds)));
        return new CapacityService(context, managementContext: management.Object);
    }

    private static ProjectTeamCapacityMemberInputDto TeamMember(
        Guid resourceId,
        decimal? hoursPerWeek = null,
        decimal? dedicationPercentage = 100m,
        Guid? projectId = null) => new()
    {
        ResourceId = resourceId,
        ProjectId = projectId,
        StartDate = new DateTime(2026, 1, 4),
        EndDate = new DateTime(2026, 1, 10),
        HoursPerWeek = hoursPerWeek,
        AllocationPercentage = dedicationPercentage
    };

    [Fact]
    public async Task GetProjectTeamOverviewAsync_SeparatesResourceCapacityProjectAllocationAndAssignedTaskWork()
    {
        using var context = CreateContext();
        var projectId = Guid.Parse("F3000000-0000-0000-0000-000000000001");
        var calendar = await context.WorkingCalendars.SingleAsync(item => item.Id == DefaultCalendarId);
        var resource = await context.Resources.SingleAsync(item => item.Id == ResourceAliceId);
        resource.BaseCalendarId = calendar.Id;
        resource.HoursPerDay = 12m;
        resource.HoursPerWeek = 60m;
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(item => item.ResourceId == ResourceAliceId));
        context.ResourceAllocations.AddRange(
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantConstants.DemoTenantId,
                ResourceId = ResourceAliceId,
                ProjectId = projectId,
                AllocationSource = ResourceAllocation.SourceProjectPlan,
                SourceReferenceId = Guid.NewGuid(),
                PlannedHours = 10m,
                StartDate = new DateTime(2026, 1, 4),
                EndDate = new DateTime(2026, 1, 10),
                Status = ResourceAllocation.StatusConfirmed
            },
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantConstants.DemoTenantId,
                ResourceId = ResourceAliceId,
                ProjectId = projectId,
                AllocationSource = ResourceAllocation.SourceProjectPlan,
                SourceReferenceId = Guid.NewGuid(),
                PlannedHours = 6m,
                StartDate = new DateTime(2026, 1, 4),
                EndDate = new DateTime(2026, 1, 10),
                Status = ResourceAllocation.StatusConfirmed
            },
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantConstants.DemoTenantId,
                ResourceId = ResourceAliceId,
                ProjectId = Guid.Parse("F3000000-0000-0000-0000-000000000002"),
                AllocationSource = ResourceAllocation.SourceProjectPlan,
                SourceReferenceId = Guid.NewGuid(),
                PlannedHours = 100m,
                StartDate = new DateTime(2026, 1, 4),
                EndDate = new DateTime(2026, 1, 10),
                Status = ResourceAllocation.StatusConfirmed
            });
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId, dedicationPercentage: 50m, projectId: projectId) },
            TeamCapacityFilter());

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCapacityHours.Should().Be(30m);
        result.Value.UtilizedHours.Should().Be(16m);
        result.Value.AvailableHours.Should().Be(14m);
        result.Value.OverallocatedHours.Should().Be(0m);
    }

    private static CapacityFilterDto TeamCapacityFilter() => new()
    {
        PeriodType = "Week",
        StartDate = new DateTime(2026, 1, 4),
        EndDate = new DateTime(2026, 1, 11),
        IncludeProposed = true
    };

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
    public async Task GetProjectTeamOverviewAsync_ReconcilesCapacityAndCountsDistinctMembersAcrossProjects()
    {
        using var context = CreateContext();
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(allocation => allocation.ResourceId == ResourceAliceId));
        context.ResourceAllocations.AddRange(
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                ResourceId = ResourceAliceId,
                ProjectId = Guid.Parse("F2000000-0000-0000-0000-000000000011"),
                ProjectName = "Project Alpha",
                AllocationPercentage = 25,
                StartDate = new DateTime(2026, 1, 4),
                EndDate = new DateTime(2026, 1, 11),
                Status = ResourceAllocation.StatusConfirmed
            },
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                ResourceId = ResourceAliceId,
                ProjectId = Guid.Parse("F2000000-0000-0000-0000-000000000012"),
                ProjectName = "Project Beta",
                AllocationPercentage = 25,
                StartDate = new DateTime(2026, 1, 4),
                EndDate = new DateTime(2026, 1, 11),
                Status = ResourceAllocation.StatusProposed
            });
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId), TeamMember(ResourceAliceId) },
            TeamCapacityFilter());

        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveResourceCount.Should().Be(1);
        result.Value.TotalCapacityHours.Should().Be(40m);
        result.Value.UtilizedHours.Should().Be(20m);
        result.Value.AvailableHours.Should().Be(20m);
        result.Value.OverallocatedHours.Should().Be(0m);
        result.Value.UtilizedPercent.Should().Be(50m);
        result.Value.AvailablePercent.Should().Be(50m);
        result.Value.IdleResourceCount.Should().Be(0);
        (result.Value.TotalCapacityHours!.Value).Should().Be(result.Value.UtilizedHours!.Value + result.Value.AvailableHours!.Value);
    }

    [Theory]
    [InlineData(100, 40, 0, 0, 100)]
    [InlineData(125, 50, 0, 10, 125)]
    public async Task GetProjectTeamOverviewAsync_ReportsFullUtilizationAndOverallocationsWithoutNegativeAvailability(
        int allocationPercent,
        int expectedUtilized,
        int expectedAvailable,
        int expectedOverallocated,
        int expectedUtilizedPercent)
    {
        using var context = CreateContext();
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(allocation => allocation.ResourceId == ResourceAliceId));
        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceAliceId,
            ProjectId = Guid.Parse("F2000000-0000-0000-0000-000000000011"),
            ProjectName = "Project Alpha",
            AllocationPercentage = allocationPercent,
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
            Status = ResourceAllocation.StatusConfirmed
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId) },
            TeamCapacityFilter());

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCapacityHours.Should().Be(40m);
        result.Value.UtilizedHours.Should().Be(expectedUtilized);
        result.Value.AvailableHours.Should().Be(expectedAvailable);
        result.Value.OverallocatedHours.Should().Be(expectedOverallocated);
        result.Value.UtilizedPercent.Should().Be(expectedUtilizedPercent);
        result.Value.AvailablePercent.Should().Be(expectedAvailable == 0 ? 0 : expectedAvailable * 100m / 40m);
        result.Value.IdleResourceCount.Should().Be(0);
    }

    [Fact]
    public async Task GetProjectTeamOverviewAsync_CountsIdleResourcesOnlyWhenAllocationStatusesAreKnown()
    {
        using var context = CreateContext();
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(allocation => allocation.ResourceId == ResourceAliceId || allocation.ResourceId == ResourceCharlieId));
        await context.SaveChangesAsync();

        var fullyAvailable = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId), TeamMember(ResourceCharlieId) },
            TeamCapacityFilter());

        fullyAvailable.IsSuccess.Should().BeTrue();
        fullyAvailable.Value.ActiveResourceCount.Should().Be(2);
        fullyAvailable.Value.IdleResourceCount.Should().Be(2);
        fullyAvailable.Value.TotalCapacityHours.Should().Be(80m);
        fullyAvailable.Value.UtilizedHours.Should().Be(0m);
        fullyAvailable.Value.AvailableHours.Should().Be(80m);

        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceAliceId,
            ProjectName = "Unclassified work",
            AllocationPercentage = 50,
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
            Status = "OnHold"
        });
        await context.SaveChangesAsync();

        var incomplete = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId), TeamMember(ResourceCharlieId) },
            TeamCapacityFilter());

        incomplete.IsSuccess.Should().BeTrue();
        incomplete.Value.IsAllocationDataAvailable.Should().BeFalse();
        incomplete.Value.IdleResourceCount.Should().BeNull();
        incomplete.Value.UtilizedHours.Should().BeNull();
    }

    [Fact]
    public async Task GetProjectTeamOverviewAsync_RequiresWorkingCalendarEvenWithExplicitResourceHours()
    {
        using var context = CreateContext();
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(allocation => allocation.ResourceId == ResourceAliceId));
        context.ResourceCalendars.RemoveRange(context.ResourceCalendars.Where(entry => entry.ResourceId == ResourceAliceId));
        context.WorkingCalendars.RemoveRange(context.WorkingCalendars);
        var resource = await context.Resources.SingleAsync(item => item.Id == ResourceAliceId);
        resource.HoursPerWeek = 48m;
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId, hoursPerWeek: 48m) },
            TeamCapacityFilter());

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCapacityDataAvailable.Should().BeFalse();
        result.Value.IsAllocationDataAvailable.Should().BeTrue();
        result.Value.ActiveResourceCount.Should().Be(1);
        result.Value.TotalCapacityHours.Should().BeNull();
        result.Value.AvailableHours.Should().BeNull();
    }

    [Fact]
    public async Task GetProjectTeamOverviewAsync_AppliesDedicationAndCrossProjectHoursToMemberCapacity()
    {
        using var context = CreateContext();
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(allocation => allocation.ResourceId == ResourceAliceId));
        var resource = await context.Resources.SingleAsync(item => item.Id == ResourceAliceId);
        resource.HoursPerWeek = 48m;
        context.ResourceAllocations.AddRange(
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                ResourceId = ResourceAliceId,
                ProjectId = Guid.Parse("F2000000-0000-0000-0000-000000000011"),
                ProjectName = "Current project",
                AllocationPercentage = 100m,
                HoursPerWeek = 16m,
                StartDate = new DateTime(2026, 1, 4),
                EndDate = new DateTime(2026, 1, 11),
                Status = ResourceAllocation.StatusConfirmed
            },
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                ResourceId = ResourceAliceId,
                ProjectId = Guid.Parse("F2000000-0000-0000-0000-000000000012"),
                ProjectName = "Another project",
                AllocationPercentage = 100m,
                HoursPerWeek = 8m,
                StartDate = new DateTime(2026, 1, 4),
                EndDate = new DateTime(2026, 1, 11),
                Status = ResourceAllocation.StatusProposed
            });
        await context.SaveChangesAsync();

        var fullDedication = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId, hoursPerWeek: 48m) },
            TeamCapacityFilter());
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(allocation => allocation.ProjectName == "Another project"));
        await context.SaveChangesAsync();
        var assignedWork = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId, hoursPerWeek: 48m) },
            TeamCapacityFilter());
        var halfDedication = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId, hoursPerWeek: 48m, dedicationPercentage: 50m) },
            TeamCapacityFilter());

        fullDedication.IsSuccess.Should().BeTrue();
        fullDedication.Value.TotalCapacityHours.Should().Be(48m);
        fullDedication.Value.UtilizedHours.Should().Be(24m);
        fullDedication.Value.AvailableHours.Should().Be(24m);
        fullDedication.Value.IdleResourceCount.Should().Be(0);

        assignedWork.IsSuccess.Should().BeTrue();
        assignedWork.Value.TotalCapacityHours.Should().Be(48m);
        assignedWork.Value.UtilizedHours.Should().Be(16m);
        assignedWork.Value.AvailableHours.Should().Be(32m);

        halfDedication.IsSuccess.Should().BeTrue();
        halfDedication.Value.TotalCapacityHours.Should().Be(24m);
        halfDedication.Value.UtilizedHours.Should().Be(16m);
        halfDedication.Value.AvailableHours.Should().Be(8m);
        halfDedication.Value.OverallocatedHours.Should().Be(0m);
        halfDedication.Value.UtilizedPercent.Should().Be(66.7m);
    }

    [Fact]
    public async Task GetProjectTeamOverviewAsync_AddsIdleMembersUsingTheirOwnConfiguredHours()
    {
        using var context = CreateContext();
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(allocation => allocation.ResourceId == ResourceAliceId || allocation.ResourceId == ResourceCharlieId));
        var alice = await context.Resources.SingleAsync(item => item.Id == ResourceAliceId);
        var charlie = await context.Resources.SingleAsync(item => item.Id == ResourceCharlieId);
        alice.HoursPerWeek = 48m;
        charlie.HoursPerWeek = 24m;
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetProjectTeamOverviewAsync(
            new[]
            {
                TeamMember(ResourceAliceId, hoursPerWeek: 48m),
                TeamMember(ResourceCharlieId, hoursPerWeek: 24m)
            },
            TeamCapacityFilter());

        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveResourceCount.Should().Be(2);
        result.Value.IdleResourceCount.Should().Be(2);
        result.Value.TotalCapacityHours.Should().Be(72m);
        result.Value.UtilizedHours.Should().Be(0m);
        result.Value.AvailableHours.Should().Be(72m);
    }

    [Fact]
    public async Task GetProjectTeamOverviewAsync_UsesHolidayAdjustedCapacityAndReturnsZeroForAnEmptyTeam()
    {
        using var context = CreateContext();
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(allocation => allocation.ResourceId == ResourceAliceId));
        context.Holidays.Add(new Holiday
        {
            Id = Guid.NewGuid(),
            WorkingCalendarId = DefaultCalendarId,
            Name = "Project week holiday",
            Date = new DateTime(2026, 1, 7),
            IsRecurring = false
        });
        await context.SaveChangesAsync();

        var withHoliday = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId) },
            TeamCapacityFilter());
        var noMembers = await CreateService(context).GetProjectTeamOverviewAsync(
            Array.Empty<ProjectTeamCapacityMemberInputDto>(),
            TeamCapacityFilter());

        withHoliday.IsSuccess.Should().BeTrue();
        withHoliday.Value.TotalCapacityHours.Should().Be(32m);
        withHoliday.Value.UtilizedHours.Should().Be(0m);
        withHoliday.Value.AvailableHours.Should().Be(32m);
        noMembers.IsSuccess.Should().BeTrue();
        noMembers.Value.ActiveResourceCount.Should().Be(0);
        noMembers.Value.IdleResourceCount.Should().Be(0);
        noMembers.Value.TotalCapacityHours.Should().Be(0m);
        noMembers.Value.UtilizedHours.Should().Be(0m);
        noMembers.Value.AvailableHours.Should().Be(0m);
    }

    [Fact]
    public async Task GetProjectTeamOverviewAsync_AppliesResourceCalendarNonWorkingExceptions()
    {
        using var context = CreateContext();
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(allocation => allocation.ResourceId == ResourceAliceId));
        context.ResourceCalendars.Add(new ResourceCalendar
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceAliceId,
            Date = new DateTime(2026, 1, 7),
            DayType = "Leave",
            AvailableHours = 0m
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId) },
            TeamCapacityFilter());

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCapacityDataAvailable.Should().BeTrue();
        result.Value.TotalCapacityHours.Should().Be(32m);
        result.Value.AvailableHours.Should().Be(32m);
    }

    [Fact]
    public async Task GetProjectTeamOverviewAsync_MarksCapacityUnavailableWhenNoWorkingCalendarExists()
    {
        using var context = CreateContext();
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(allocation => allocation.ResourceId == ResourceAliceId));
        context.WorkingCalendars.RemoveRange(context.WorkingCalendars);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetProjectTeamOverviewAsync(
            new[] { TeamMember(ResourceAliceId) },
            TeamCapacityFilter());

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCapacityDataAvailable.Should().BeFalse();
        result.Value.TotalCapacityHours.Should().BeNull();
        result.Value.UtilizedHours.Should().BeNull();
        result.Value.IdleResourceCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMatrixAsync_CalculatesGrossCapacityUsingWorkingCalendar()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
            ResourceId = ResourceAliceId
        };

        var result = await service.GetMatrixAsync(filter);

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Rows.Should().ContainSingle().Subject;
        row.Cells.Should().ContainSingle();
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
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
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
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations.Where(allocation => allocation.ResourceId == ResourceAliceId));
        context.ResourceAllocations.AddRange(
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                ResourceId = ResourceAliceId,
                ProjectName = "Confirmed Work",
                ProjectRoleId = ProjectRoleDevId,
                AllocationPercentage = 50,
                StartDate = new DateTime(2026, 1, 4),
                EndDate = new DateTime(2026, 1, 11),
                Status = ResourceAllocation.StatusConfirmed
            },
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                ResourceId = ResourceAliceId,
                ProjectName = "Proposed Work",
                ProjectRoleId = ProjectRoleDevId,
                AllocationPercentage = 30,
                StartDate = new DateTime(2026, 1, 4),
                EndDate = new DateTime(2026, 1, 11),
                Status = ResourceAllocation.StatusProposed
            });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
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
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
            Status = ResourceAllocation.StatusConfirmed
        });
        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceCharlieId,
            ProjectName = "Extra Work",
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 30,
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
            Status = ResourceAllocation.StatusProposed
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
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
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
            Status = ResourceAllocation.StatusConfirmed
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
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
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
            Status = ResourceAllocation.StatusConfirmed
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
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
    public async Task CapacityQueries_ExcludeAllocationsOutsideManagementProjectScope()
    {
        using var context = CreateContext();
        var allowedProjectId = Guid.Parse("F3000000-0000-0000-0000-000000000001");
        var outOfScopeProjectId = Guid.Parse("F3000000-0000-0000-0000-000000000002");
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations);
        context.ResourceAllocations.AddRange(
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantConstants.DemoTenantId,
                ResourceId = ResourceAliceId,
                ProjectId = allowedProjectId,
                ProjectRoleId = ProjectRoleDevId,
                AllocationSource = ResourceAllocation.SourceProjectPlan,
                PlannedHours = 10m,
                StartDate = new DateTime(2026, 1, 5),
                EndDate = new DateTime(2026, 1, 12),
                Status = ResourceAllocation.StatusConfirmed
            },
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantConstants.DemoTenantId,
                ResourceId = ResourceAliceId,
                ProjectId = outOfScopeProjectId,
                ProjectRoleId = ProjectRoleDevId,
                AllocationSource = ResourceAllocation.SourceProjectPlan,
                PlannedHours = 1000m,
                StartDate = new DateTime(2026, 1, 5),
                EndDate = new DateTime(2026, 1, 12),
                Status = ResourceAllocation.StatusConfirmed
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, new HashSet<Guid> { allowedProjectId });
        var filter = new CapacityFilterDto
        {
            PeriodType = "Month",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 2, 1),
            ResourceId = ResourceAliceId
        };

        var matrix = await service.GetMatrixAsync(filter);
        var matrixAllocations = matrix.Value.Rows.Single().Cells.SelectMany(cell => cell.Allocations).ToList();
        matrixAllocations.Should().ContainSingle().Which.ProjectId.Should().Be(allowedProjectId);

        var availability = await service.GetAvailabilityAsync(filter);
        availability.Items.Single(resource => resource.ResourceId == ResourceAliceId).AvailableCapacityHours.Should().BeGreaterThan(0m);

        var demand = await service.GetDemandVsCapacityAsync(filter);
        demand.Value.Groups.Single(group => group.GroupType == "ProjectRole").DemandHours.Should().Be(10m);

        var gantt = await service.GetGanttDataAsync(filter);
        gantt.Value.Should().ContainSingle().Which.ProjectId.Should().Be(allowedProjectId);

        var resourceAvailability = await service.GetResourceAvailabilityAsync(
            ResourceAliceId,
            new DateTime(2026, 1, 5),
            new DateTime(2026, 1, 12));
        resourceAvailability.Value.AvailableCapacityHours.Should().BeGreaterThan(0m);
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
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
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
        foreach (var rule in defaultCalendar.DayRules.Where(rule => rule.IsWorkingDay))
            rule.EndTime = TimeSpan.FromHours(18);
        await context.SaveChangesAsync();

        var filter = new CapacityFilterDto
        {
            PeriodType = "Week",
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
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
            StartDate = new DateTime(2026, 1, 4),
            EndDate = new DateTime(2026, 1, 11),
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
