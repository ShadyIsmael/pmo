using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class SeedDataProviderTests
{
    [Fact]
    public async Task SeedAsync_Rerun_ShouldProduceExactlyTenDeterministicProjects()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        await SeedDataProvider.SeedAsync(context);

        var projects = await context.Projects
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();

        projects.Should().HaveCount(10);
        projects.Select(p => p.Code).Should().OnlyHaveUniqueItems();
        projects.Select(p => p.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldSeedExactlyFiveNamedProjectManagers()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        await SeedDataProvider.SeedAsync(context);

        var managerRole = await context.Roles
            .IgnoreQueryFilters()
            .FirstAsync(r => r.TenantId == SeedDataProvider.TenantId && r.Name == "project.manager");

        var managers = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == SeedDataProvider.TenantId &&
                u.UserRoles.Any(ur => ur.RoleId == managerRole.Id))
            .OrderBy(u => u.Email)
            .ToListAsync();

        // The five named accounts (alex.morgan, priya.rao, liam.chen, sophia.novak, omar.haddad)
        // plus the primary seeded project.manager@nexus.com account all share the role.
        managers.Should().HaveCount(6);
        managers.Select(m => m.Email).Should().Contain(new[]
        {
            "alex.morgan@nexus.com",
            "priya.rao@nexus.com",
            "liam.chen@nexus.com",
            "sophia.novak@nexus.com",
            "omar.haddad@nexus.com"
        });
        managers.Select(m => m.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldSeedTwoVendorsAndTenOutsourcedResources()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        await SeedDataProvider.SeedAsync(context);

        var vendors = await context.Vendors
            .IgnoreQueryFilters()
            .Where(v => v.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();

        vendors.Should().HaveCount(2);
        vendors.Select(v => v.Name).Should().Contain(new[] { "CloudNova Solutions", "DataWorks Consulting" });

        var outsourced = await context.Resources
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == SeedDataProvider.TenantId && r.Type == "Outsourced")
            .ToListAsync();

        outsourced.Should().HaveCount(10);
        outsourced.Select(r => r.Id).Should().OnlyHaveUniqueItems();
        outsourced.Select(r => r.VendorId).Should().NotContainNulls();

        var cloudNovaId = vendors.First(v => v.Name == "CloudNova Solutions").Id;
        var dataWorksId = vendors.First(v => v.Name == "DataWorks Consulting").Id;
        outsourced.Count(r => r.VendorId == cloudNovaId).Should().Be(5);
        outsourced.Count(r => r.VendorId == dataWorksId).Should().Be(5);
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldSeedSkillsAndContracts()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        await SeedDataProvider.SeedAsync(context);

        var skills = await context.Skills
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        skills.Should().HaveCount(10);

        var contracts = await context.Contracts
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        contracts.Should().HaveCount(10);
        contracts.Select(c => c.ResourceId).Should().OnlyHaveUniqueItems();

        var resourceSkills = await context.ResourceSkills
            .IgnoreQueryFilters()
            .Where(rs => rs.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        resourceSkills.Should().HaveCountGreaterThan(0);
    }
}
