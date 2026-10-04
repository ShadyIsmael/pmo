using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
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
    public async Task SeedAsync_Rerun_DoesNotDeleteUserCreatedRisks()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        var projectId = Guid.Parse("e0000001-0000-0000-0000-000000000001");
        var risk = new Risk
        {
            TenantId = SeedDataProvider.TenantId,
            ProjectId = projectId,
            Title = "User-created risk",
            Description = "Must survive a development restart",
            RiskLevel = RiskLevel.Medium,
            Status = RiskStatus.Identified,
            CreatedBy = "user"
        };
        context.Risks.Add(risk);
        await context.SaveChangesAsync();

        await SeedDataProvider.SeedAsync(context);

        var persisted = await context.Risks.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.Id == risk.Id);
        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be("User-created risk");
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldKeepOnlyTheAdministratorAccount()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        await SeedDataProvider.SeedAsync(context);

        var users = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == SeedDataProvider.TenantId)
            .OrderBy(u => u.Email)
            .ToListAsync();

        // Demo accounts (the five named PMs, the per-role accounts, the 80 generated
        // accounts and the legacy pmNN@nexus.com shape) are purged, so a fresh tenant
        // starts with the administrator only.
        users.Should().HaveCount(1);
        users[0].Email.Should().Be("admin@nexus.com");
        users.Select(u => u.Email).Should().NotContain(e => e.StartsWith("pm") && e.EndsWith("@nexus.com"));
        users.Select(u => u.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldNotSeedVendorsOrOutsourcedResources()
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
        vendors.Should().BeEmpty("the CloudNova and DataWorks demo vendors are purged");

        var resources = await context.Resources
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        resources.Should().BeEmpty("the demo employee and outsourced resources are purged");
        resources.Should().NotContain(r => r.Type == "Outsourced");
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldPreserveUserCreatedVendorsAndResources()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);

        var vendor = new Vendor
        {
            TenantId = SeedDataProvider.TenantId,
            Name = "Acme Consulting",
            Status = "Active",
            IsActive = true,
            CreatedBy = "user"
        };
        var resource = new Resource
        {
            TenantId = SeedDataProvider.TenantId,
            Name = "Contract Engineer",
            Type = "Outsourced",
            Status = "Active",
            IsActive = true,
            VendorId = vendor.Id,
            CreatedBy = "user"
        };
        context.Vendors.Add(vendor);
        context.Resources.Add(resource);
        await context.SaveChangesAsync();

        // Re-seeding must not touch rows the purge cannot recognise as demo data.
        await SeedDataProvider.SeedAsync(context);

        var persistedVendor = await context.Vendors.IgnoreQueryFilters().SingleOrDefaultAsync(v => v.Id == vendor.Id);
        persistedVendor.Should().NotBeNull();
        persistedVendor!.Name.Should().Be("Acme Consulting");

        var persistedResource = await context.Resources.IgnoreQueryFilters().SingleOrDefaultAsync(r => r.Id == resource.Id);
        persistedResource.Should().NotBeNull();
        persistedResource!.VendorId.Should().Be(vendor.Id);
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldSeedVendorCategoriesAsReferenceData()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        await SeedDataProvider.SeedAsync(context);

        var categories = await context.Lookups
            .IgnoreQueryFilters()
            .Where(l => l.TenantId == SeedDataProvider.TenantId && l.Category == NEXUS.SharedKernel.ReferenceDataCategories.VendorCategory && !l.IsDeleted)
            .ToListAsync();

        // Categories are reference data and are deliberately kept, unlike the demo
        // vendors that used to point at them.
        categories.Should().HaveCount(5);
        categories.Select(c => c.Code).Should().OnlyHaveUniqueItems();
        categories.Should().OnlyContain(c => c.IsActive);

        var vendors = await context.Vendors
            .IgnoreQueryFilters()
            .Where(v => v.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        vendors.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldSeedSkillCatalogWithoutContractsOrResourceLinks()
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
        skills.Should().HaveCount(10, "the skill catalog is reference data and is deliberately kept");

        var contracts = await context.Contracts
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        contracts.Should().BeEmpty("demo contracts belonged to the purged demo resources");

        var resourceSkills = await context.ResourceSkills
            .IgnoreQueryFilters()
            .Where(rs => rs.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        resourceSkills.Should().BeEmpty("resource-skill links belonged to the purged demo resources");
    }
}
