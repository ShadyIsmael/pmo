using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using NEXUS.SharedKernel.Helpers;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class AllocationServiceTests : TestBase
{
    private static AllocationService CreateService(TestDbContext context, IReadOnlySet<Guid>? allowedProjectIds = null)
    {
        var auditMock = new Mock<IAuditService>();
        auditMock.Setup(a => a.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
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
        return new AllocationService(context, auditMock.Object, management.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsSuccess()
    {
        using var context = CreateContext();
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
        var service = CreateService(context, new HashSet<Guid> { dto.ProjectId!.Value });

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
    public async Task CreateAsync_WithHiddenProjectConflictReturnsGenericErrorWithoutAllocationDetails()
    {
        using var context = CreateContext();
        var visibleProjectId = Guid.Parse("F3000000-0000-0000-0000-000000000011");
        var hiddenProjectId = Guid.Parse("F3000000-0000-0000-0000-000000000012");
        context.ResourceAllocations.RemoveRange(context.ResourceAllocations);
        context.ResourceAllocations.Add(new ResourceAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = TenantConstants.DemoTenantId,
            ResourceId = ResourceCharlieId,
            ProjectId = hiddenProjectId,
            ProjectName = "Confidential Project",
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 80m,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 31),
            Status = ResourceAllocation.StatusConfirmed
        });
        await context.SaveChangesAsync();
        var service = CreateService(context, new HashSet<Guid> { visibleProjectId });

        var result = await service.CreateAsync(new CreateAllocationDto
        {
            ResourceId = ResourceCharlieId,
            ProjectId = visibleProjectId,
            ProjectName = "Visible Project",
            ProjectRoleId = ProjectRoleDevId,
            AllocationPercentage = 30m,
            StartDate = new DateTime(2026, 3, 10),
            EndDate = new DateTime(2026, 3, 20),
            Status = ResourceAllocation.StatusConfirmed
        }, "11111111-1111-1111-1111-111111111111");

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("outside the current project scope");
        result.Error.Message.Should().NotContain("Confidential Project");
        result.Error.Message.Should().NotContain("80%");
        result.Error.Message.Should().NotContain("110%");
        (await context.ResourceAllocations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_WithOutOfScopeProjectRejectsBeforeResourceAndAllocationValidation()
    {
        using var context = CreateContext();
        var service = CreateService(context, new HashSet<Guid>());
        var beforeCount = await context.ResourceAllocations.CountAsync();

        var result = await service.CreateAsync(new CreateAllocationDto
        {
            ResourceId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            AllocationPercentage = 150m,
            StartDate = new DateTime(2026, 3, 20),
            EndDate = new DateTime(2026, 3, 10),
            Status = ResourceAllocation.StatusConfirmed
        }, "11111111-1111-1111-1111-111111111111");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NOT_FOUND");
        (await context.ResourceAllocations.CountAsync()).Should().Be(beforeCount);
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
    public async Task GetConflictsAsync_ExcludesOverlapsOutsideManagementProjectScope()
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
                StartDate = new DateTime(2026, 1, 5),
                EndDate = new DateTime(2026, 1, 15),
                Status = ResourceAllocation.StatusConfirmed
            },
            new ResourceAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantConstants.DemoTenantId,
                ResourceId = ResourceAliceId,
                ProjectId = outOfScopeProjectId,
                StartDate = new DateTime(2026, 1, 5),
                EndDate = new DateTime(2026, 1, 15),
                Status = ResourceAllocation.StatusConfirmed
            });
        await context.SaveChangesAsync();
        var service = CreateService(context, new HashSet<Guid> { allowedProjectId });

        var result = await service.GetConflictsAsync(
            ResourceAliceId,
            new DateTime(2026, 1, 8),
            new DateTime(2026, 1, 12),
            null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.ProjectId.Should().Be(allowedProjectId);
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
