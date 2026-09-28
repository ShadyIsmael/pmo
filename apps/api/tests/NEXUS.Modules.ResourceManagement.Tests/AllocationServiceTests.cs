using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class AllocationServiceTests : TestBase
{
    private static AllocationService CreateService(TestDbContext context)
    {
        var auditMock = new Mock<IAuditService>();
        auditMock.Setup(a => a.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return new AllocationService(context, auditMock.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsSuccess()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var dto = new CreateAllocationDto
        {
            ResourceId = ResourceAliceId,
            ProjectId = Guid.NewGuid(),
            ProjectName = "Test Project",
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 40,
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2026, 2, 15),
            Status = ResourceAllocation.StatusConfirmed,
            IsBillable = true
        };

        var result = await service.CreateAsync(dto, "11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ResourceId.Should().Be(ResourceAliceId);
        result.Value.AllocationPercentage.Should().Be(40);
        result.Value.ProjectName.Should().Be("Test Project");
        result.Value.ResourceName.Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentResource_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var dto = new CreateAllocationDto
        {
            ResourceId = Guid.NewGuid(),
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 40,
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2026, 2, 15)
        };

        var result = await service.CreateAsync(dto, "11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task CreateAsync_WithInactiveResource_ReturnsValidationError()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var dto = new CreateAllocationDto
        {
            ResourceId = ResourceDianaId,
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 40,
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2026, 2, 15)
        };

        var result = await service.CreateAsync(dto, "11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("VALIDATION_ERROR");
        result.Error.Message.Should().Contain("not active");
    }

    [Fact]
    public async Task CreateAsync_WithStartDateEqualToEndDate_ReturnsValidationError()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var dto = new CreateAllocationDto
        {
            ResourceId = ResourceAliceId,
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 40,
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2026, 2, 1)
        };

        var result = await service.CreateAsync(dto, "11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("VALIDATION_ERROR");
        result.Error.Message.Should().Contain("Start date must be before end date");
    }

    [Fact]
    public async Task CreateAsync_WithAllocationOver100Percent_ReturnsValidationError()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var dto = new CreateAllocationDto
        {
            ResourceId = ResourceAliceId,
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 150,
            StartDate = new DateTime(2026, 2, 1),
            EndDate = new DateTime(2026, 2, 15)
        };

        var result = await service.CreateAsync(dto, "11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("VALIDATION_ERROR");
        result.Error.Message.Should().Contain("between 1 and 100");
    }

    [Fact]
    public async Task CreateAsync_WithOverlappingAllocationsExceeding100Percent_ReturnsOverAllocationError()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var existing = new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceCharlieId,
            ProjectName = "Project X",
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 60,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 31),
            Status = ResourceAllocation.StatusConfirmed
        };
        context.ResourceAllocations.Add(existing);
        await context.SaveChangesAsync();

        var dto = new CreateAllocationDto
        {
            ResourceId = ResourceCharlieId,
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 50,
            StartDate = new DateTime(2026, 3, 10),
            EndDate = new DateTime(2026, 3, 20)
        };

        var result = await service.CreateAsync(dto, "11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("VALIDATION_ERROR");
        result.Error.Message.Should().Contain("Over-allocation");
    }

    [Fact]
    public async Task CreateAsync_WithOverlappingAllocationsWithin100Percent_ReturnsSuccess()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var existing = new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceCharlieId,
            ProjectName = "Project X",
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 40,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 31),
            Status = ResourceAllocation.StatusConfirmed
        };
        context.ResourceAllocations.Add(existing);
        await context.SaveChangesAsync();

        var dto = new CreateAllocationDto
        {
            ResourceId = ResourceCharlieId,
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 50,
            StartDate = new DateTime(2026, 3, 10),
            EndDate = new DateTime(2026, 3, 20)
        };

        var result = await service.CreateAsync(dto, "11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeTrue();
        result.Value.AllocationPercentage.Should().Be(50);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ReturnsUpdatedAllocation()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var dto = new UpdateAllocationDto
        {
            AllocationPercentage = 75,
            ProjectRoleId = ProjectRoleArchId
        };

        var result = await service.UpdateAsync(ExistingAllocationId, dto, "11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeTrue();
        result.Value.AllocationPercentage.Should().Be(75);
        result.Value.ProjectRoleId.Should().Be(ProjectRoleArchId);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentAllocation_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateAllocationDto(), "11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task DeleteAsync_WithExistingAllocation_SoftDeletesAndReturnsSuccess()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.DeleteAsync(ExistingAllocationId, "11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeTrue();

        var allocation = await context.ResourceAllocations.FirstAsync(a => a.Id == ExistingAllocationId);
        allocation.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentAllocation_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.DeleteAsync(Guid.NewGuid(), "11111111-1111-1111-1111-111111111111");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task GetByResourceAsync_ReturnsPagedResultsForGivenResource()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceAliceId,
            ProjectName = "Project B",
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 30,
            StartDate = new DateTime(2026, 4, 1),
            EndDate = new DateTime(2026, 4, 15),
            Status = ResourceAllocation.StatusConfirmed
        });
        await context.SaveChangesAsync();

        var result = await service.GetByResourceAsync(ResourceAliceId, 1, 10);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.All(i => i.ResourceId == ResourceAliceId).Should().BeTrue();
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsPagedResultsForGivenProject()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var projectId = Guid.Parse("F2000000-0000-0000-0000-000000000001");

        var result = await service.GetByProjectAsync(projectId, 1, 10);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items.All(i => i.ProjectId == projectId).Should().BeTrue();
    }

    [Fact]
    public async Task GetConflictsAsync_ReturnsOverlappingAllocations()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetConflictsAsync(
            ResourceAliceId,
            new DateTime(2026, 1, 8),
            new DateTime(2026, 1, 12),
            null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].ResourceId.Should().Be(ResourceAliceId);
    }

    [Fact]
    public async Task GetConflictsAsync_WithNoOverlap_ReturnsEmpty()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetConflictsAsync(
            ResourceAliceId,
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 15),
            null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingAllocation_ReturnsAllocation()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetByIdAsync(ExistingAllocationId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(ExistingAllocationId);
        result.Value.ResourceName.Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentAllocation_ReturnsNotFound()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task GetPagedAsync_WithPageParameters_ReturnsPagedResults()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetPagedAsync(1, 10);

        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedAsync_WithStatusFilter_ReturnsFilteredResults()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.GetPagedAsync(1, 10, status: "Confirmed");

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be("Confirmed");
    }
}
