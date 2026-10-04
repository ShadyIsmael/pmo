using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class ProjectServiceTests
{
    [Fact]
    public async Task GetPagedAsync_ReturnsLightweightSummariesWithoutLoadingProjectCollections()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.Projects.Add(new Project
            {
                Id = Guid.NewGuid(),
                Name = "List Project",
                Code = "PRJ-LIST",
                TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Members = new List<ProjectMember>
                {
                    new() { Id = Guid.NewGuid(), ResourceId = Guid.NewGuid(), Role = "TeamMember" }
                }
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var service = new ProjectService(
            context,
            new Mock<IAuditService>().Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new ProjectManagerMembershipSynchronizer(context));

        var result = await service.GetPagedAsync(1, 20);

        result.Items.Should().ContainSingle().Which.Name.Should().Be("List Project");
        result.Items.Single().Should().BeOfType<ProjectSummaryDto>();
    }

    [Fact]
    public async Task GetShellAsync_DoesNotLoadProjectCollections()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var projectId = Guid.NewGuid();
        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Shell Project",
                Code = "PRJ-SHELL",
                TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Members = new List<ProjectMember>
                {
                    new() { Id = Guid.NewGuid(), ResourceId = Guid.NewGuid(), Role = "TeamMember" }
                }
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var service = new ProjectService(
            context,
            new Mock<IAuditService>().Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new ProjectManagerMembershipSynchronizer(context));

        var result = await service.GetShellAsync(projectId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Shell Project");
        result.Value.Members.Should().BeEmpty();
        context.ChangeTracker.Entries<ProjectMember>().Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_GeneratesUniqueProjectCode_WhenCodeIsNotProvided()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        context.Currencies.Add(new Currency { Code = "GBP", Name = "British Pound", Symbol = "£", IsBase = true, TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111") });
        context.Projects.Add(new Project { Id = Guid.NewGuid(), Name = "Existing Project", Code = "PRJ-1001", TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111") });
        await context.SaveChangesAsync();

        var auditService = new Mock<IAuditService>();
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("tenantId", "11111111-1111-1111-1111-111111111111")
                }, "Test"))
            }
        };

        var service = new ProjectService(context, auditService.Object, httpContextAccessor, new ProjectManagerMembershipSynchronizer(context));

        var result = await service.CreateAsync(new CreateProjectDto
        {
            Name = "New Project",
            Currency = "GBP",
            ProjectManagerId = Guid.NewGuid()
        }, "user-1");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("PRJ-1002");
    }
}
