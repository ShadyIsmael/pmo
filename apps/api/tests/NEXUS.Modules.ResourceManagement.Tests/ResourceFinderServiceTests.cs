using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class ResourceFinderServiceTests : TestBase
{
    private static ResourceFinderService CreateService(TestDbContext context)
    {
        var capacityMock = new Mock<ICapacityService>();
        return new ResourceFinderService(context, capacityMock.Object);
    }

    [Fact]
    public async Task FindAsync_FiltersByRole()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var query = new ResourceFinderQueryDto
        {
            Role = "Developer",
            Page = 1,
            PageSize = 20
        };

        var result = await service.FindAsync(query);

        result.Items.Should().Contain(r => r.ResourceName == "Alice Johnson");
        result.Items.Should().NotContain(r => r.ResourceName == "Bob Smith");
        result.Items.Should().Contain(r => r.ResourceName == "Charlie Brown");
    }

    [Fact]
    public async Task FindAsync_FiltersBySeniority()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var query = new ResourceFinderQueryDto
        {
            Seniority = "Senior",
            Page = 1,
            PageSize = 20
        };

        var result = await service.FindAsync(query);

        result.Items.Should().ContainSingle();
        result.Items[0].Seniority.Should().Be("Senior");
        result.Items[0].ResourceName.Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task FindAsync_FiltersByDepartment()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var query = new ResourceFinderQueryDto
        {
            DepartmentId = DepartmentEngineeringId,
            Page = 1,
            PageSize = 20
        };

        var result = await service.FindAsync(query);

        result.Items.Should().HaveCount(2);
        result.Items.All(r => r.ResourceName is "Alice Johnson" or "Charlie Brown");
    }

    [Fact]
    public async Task FindAsync_FiltersBySkill()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var query = new ResourceFinderQueryDto
        {
            SkillIds = new List<Guid> { SkillCSharpId },
            Page = 1,
            PageSize = 20
        };

        var result = await service.FindAsync(query);

        result.Items.Should().ContainSingle();
        result.Items[0].ResourceName.Should().Be("Alice Johnson");
    }

    [Fact]
    public async Task FindAsync_MatchScore_ReflectsSkillMatch()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var query = new ResourceFinderQueryDto
        {
            SkillIds = new List<Guid> { SkillCSharpId },
            Page = 1,
            PageSize = 20
        };

        var result = await service.FindAsync(query);

        var alice = result.Items.Should().ContainSingle().Subject;
        alice.ResourceName.Should().Be("Alice Johnson");
        alice.MatchScore.Should().Be(70);
    }

    [Fact]
    public async Task FindAsync_MatchScore_ReflectsRoleMatch()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var query = new ResourceFinderQueryDto
        {
            Role = "Developer",
            Page = 1,
            PageSize = 20
        };

        var result = await service.FindAsync(query);

        result.Items.Should().HaveCount(2);
        result.Items.All(r => r.MatchScore == 65).Should().BeTrue();
    }

    [Fact]
    public async Task FindAsync_ReflectsExistingAllocationsInAvailableCapacity()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var query = new ResourceFinderQueryDto
        {
            ResourceType = "Contractor",
            Page = 1,
            PageSize = 20
        };

        var result = await service.FindAsync(query);

        var charlie = result.Items.Single();
        charlie.AvailableCapacityHours.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FindAsync_ReturnsEmpty_WhenNoMatch()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var query = new ResourceFinderQueryDto
        {
            Role = "Data Scientist",
            Page = 1,
            PageSize = 20
        };

        var result = await service.FindAsync(query);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task FindAsync_SortsByMatchScoreDescending()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var query = new ResourceFinderQueryDto
        {
            Role = "Developer",
            Seniority = "Senior",
            SkillIds = new List<Guid> { SkillCSharpId },
            Page = 1,
            PageSize = 20
        };

        var result = await service.FindAsync(query);

        result.Items.Should().BeInDescendingOrder(r => r.MatchScore);
    }

    [Fact]
    public async Task FindAsync_WithAllFilters_ReturnsScoredResults()
    {
        using var context = CreateContext();
        var service = CreateService(context);
        var query = new ResourceFinderQueryDto
        {
            Role = "Developer",
            Seniority = "Senior",
            DepartmentId = DepartmentEngineeringId,
            SkillIds = new List<Guid> { SkillCSharpId, SkillJavaScriptId },
            ResourceType = "Employee",
            Page = 1,
            PageSize = 20
        };

        var result = await service.FindAsync(query);

        result.Items.Should().ContainSingle();
        result.Items[0].ResourceName.Should().Be("Alice Johnson");
        result.Items[0].MatchScore.Should().BeGreaterThan(80);
    }
}
