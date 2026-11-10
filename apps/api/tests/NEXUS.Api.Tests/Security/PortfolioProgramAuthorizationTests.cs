using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Identity.Application;
using NEXUS.Api.Management;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.SharedKernel.Management;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class PortfolioProgramAuthorizationTests
{
    [Fact]
    public async Task Create_rejects_initial_program_manager_assignment_without_handover_permission()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new ProgramService(
            db,
            CreateHttpContextAccessor(tenantId, userId),
            CreateManagementContextAccessor(tenantId, userId));

        var result = await service.CreateAsync(new SaveProgramDto
        {
            Name = "Program", PortfolioId = Guid.NewGuid(), ProgramManagerId = Guid.NewGuid(), Status = "Planning"
        }, "actor");

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Contains("Handover.AdminInitiate", result.Error?.Message);
        Assert.Empty(await db.Programs.ToListAsync());
    }

    [Fact]
    public async Task Update_rejects_first_program_manager_assignment_without_handover_permission()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        db.Programs.Add(new ProgramEntity
        {
            Id = programId,
            TenantId = tenantId,
            PortfolioId = portfolioId,
            Name = "Program",
            Status = "Planning",
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        var service = new ProgramService(
            db,
            CreateHttpContextAccessor(tenantId, userId),
            CreateManagementContextAccessor(tenantId, userId, programId));

        var result = await service.UpdateAsync(programId, new SaveProgramDto
        {
            Name = "Program", PortfolioId = portfolioId, ProgramManagerId = Guid.NewGuid(), Status = "Planning"
        }, "actor");

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Contains("Handover.AdminInitiate", result.Error?.Message);
        Assert.Null((await db.Programs.SingleAsync(item => item.Id == programId)).ProgramManagerId);
    }

    [Fact]
    public async Task Eligible_portfolio_lookups_exclude_records_with_missing_status()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activePortfolioId = Guid.NewGuid();
        var missingStatusPortfolioId = Guid.NewGuid();
        db.Portfolios.AddRange(
            new PortfolioEntity
            {
                Id = activePortfolioId,
                TenantId = tenantId,
                Name = "Active Portfolio",
                Status = "Active",
                Currency = "USD"
            },
            new PortfolioEntity
            {
                Id = missingStatusPortfolioId,
                TenantId = tenantId,
                Name = "Portfolio with no status",
                Status = null,
                Currency = "USD"
            });
        await db.SaveChangesAsync();

        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.GetPerspectiveSnapshotAsync(
                userId,
                tenantId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(activePortfolioId, missingStatusPortfolioId));
        var permissions = new FixedEffectivePermissionService(
            new HashSet<string>(StringComparer.Ordinal) { "Program.Create", "Program.Edit" });
        var service = new ProgramService(
            db,
            CreateHttpContextAccessor(tenantId, userId),
            CreateManagementContextAccessor(tenantId, userId),
            resolver.Object,
            permissions);

        var creationResult = await service.GetEligiblePortfoliosForCreationAsync();
        var editingResult = await service.GetEligiblePortfoliosForEditingAsync();

        Assert.True(creationResult.IsSuccess);
        Assert.True(editingResult.IsSuccess);
        Assert.Equal(new[] { activePortfolioId }, creationResult.GetValueOrThrow().Select(item => item.Id));
        Assert.Equal(new[] { activePortfolioId }, editingResult.GetValueOrThrow().Select(item => item.Id));
        Assert.Equal("Active", creationResult.GetValueOrThrow().Single().Status);
        Assert.Equal("Active", editingResult.GetValueOrThrow().Single().Status);
    }

    [Fact]
    public async Task Create_with_cross_portfolio_permission_fails_closed_when_assignment_scope_is_missing()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Portfolios.Add(new PortfolioEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Active Portfolio",
            Status = "Active",
            OrganizationId = Guid.NewGuid(),
            Currency = "USD"
        });
        await db.SaveChangesAsync();
        var service = new ProgramService(
            db,
            CreateHttpContextAccessor(tenantId, userId),
            CreateManagementContextAccessor(tenantId, userId),
            managementScopeResolver: null,
            effectivePermissions: new FixedEffectivePermissionService(new HashSet<string>(StringComparer.Ordinal)
            {
                "Program.Create", "Program.CreateAcrossPortfolios", "Portfolio.View"
            }));

        var result = await service.CreateAsync(new SaveProgramDto
        {
            Name = "Program",
            PortfolioId = db.Portfolios.Local.Single().Id,
            Status = "Planning"
        }, "actor");

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error?.StatusCode);
        Assert.Empty(await db.Programs.ToListAsync());
    }

    [Fact]
    public async Task Create_across_portfolios_rejects_portfolio_without_organization_assignment()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        db.Portfolios.Add(new PortfolioEntity
        {
            Id = portfolioId,
            TenantId = tenantId,
            Name = "Unscoped Portfolio",
            Status = "Active",
            OrganizationId = null,
            Currency = "USD"
        });
        await db.SaveChangesAsync();

        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.GetPerspectiveSnapshotAsync(
                userId,
                tenantId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot());
        var service = new ProgramService(
            db,
            CreateHttpContextAccessor(tenantId, userId),
            CreateManagementContextAccessor(tenantId, userId),
            resolver.Object,
            new FixedEffectivePermissionService(new HashSet<string>(StringComparer.Ordinal)
            {
                "Program.Create", "Program.CreateAcrossPortfolios", "Portfolio.View"
            }));

        var result = await service.CreateAsync(new SaveProgramDto
        {
            Name = "Program",
            PortfolioId = portfolioId,
            Status = "Planning"
        }, "actor");

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error?.StatusCode);
        Assert.Empty(await db.Programs.ToListAsync());
    }

    private static ProgramAuthorizationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ProgramAuthorizationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IHttpContextAccessor CreateHttpContextAccessor(Guid tenantId, Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("permission", "Program.Create"),
            new Claim("permission", "Program.Edit"),
            new Claim("permission", "Portfolio.View")
        ], "test"));
        return new HttpContextAccessor { HttpContext = httpContext };
    }

    private static ManagementContextAccessor CreateManagementContextAccessor(
        Guid tenantId,
        Guid userId,
        Guid? scopedProgramId = null)
    {
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(
            userId,
            tenantId,
            ManagementPerspective.ProgramManager,
            ManagementViewMode.Native,
            null,
            scopedProgramId,
            null,
            new ManagementScope(
                new HashSet<Guid>(),
                scopedProgramId.HasValue ? new HashSet<Guid> { scopedProgramId.Value } : new HashSet<Guid>(),
                new HashSet<Guid>())));
        return accessor;
    }

    private static ManagementPerspectiveSnapshot CreateSnapshot(params Guid[] nativePortfolioIds) => new(
        Array.Empty<ManagementPerspective>(),
        [new ManagementPerspectiveAvailability(
            ManagementPerspective.PortfolioManager,
            true,
            false,
            nativePortfolioIds.Select(id => new ManagementEntityReference(id, "Portfolio")).ToArray())],
        ManagementPerspective.PortfolioManager,
        false,
        ManagementScope.Empty,
        null);

    private sealed class FixedEffectivePermissionService(IReadOnlySet<string> permissions) : IUserEffectivePermissionService
    {
        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(permissions);

        public Task<IReadOnlySet<string>> GetEffectiveRoleNamesAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
    }

    private sealed class ProgramAuthorizationDbContext(DbContextOptions<ProgramAuthorizationDbContext> options) : DbContext(options)
    {
        public DbSet<ProgramEntity> Programs => Set<ProgramEntity>();
        public DbSet<PortfolioEntity> Portfolios => Set<PortfolioEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProgramEntity>().Ignore(program => program.Portfolio);
            modelBuilder.Entity<PortfolioEntity>()
                .Ignore(portfolio => portfolio.Programs)
                .Ignore(portfolio => portfolio.HealthHistory)
                .Ignore(portfolio => portfolio.Budgets);
        }
    }
}
