using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Portfolio.Api;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.SharedKernel.Management;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class ProgramCreationServiceTests
{
    [Fact]
    public async Task Create_without_manager_persists_and_reloads_program()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProgramCreationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ProgramCreationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE Currencies (TenantId TEXT NOT NULL, Code TEXT NOT NULL, IsDeleted INTEGER NOT NULL)");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Currencies (TenantId, Code, IsDeleted) VALUES ({0}, {1}, {2})",
            tenantId, "USD", false);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE Projects (Id TEXT NOT NULL, Name TEXT NOT NULL, Code TEXT NOT NULL, Status TEXT NOT NULL, Currency TEXT NOT NULL, TenantId TEXT NOT NULL, ProgramId TEXT NULL, IsDeleted INTEGER NOT NULL)");
        db.Portfolios.Add(new PortfolioEntity
        {
            Id = portfolioId,
            TenantId = tenantId,
            Name = "Assigned Portfolio",
            Code = "PORT-01",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var permissions = new TestEffectivePermissions("Program.Create");
        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.GetPerspectiveSnapshotAsync(
                userId,
                tenantId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(portfolioId));
        var managementContext = CreateManagementContextAccessor(tenantId, userId);
        var httpContext = CreateHttpContext(tenantId, userId);
        var service = new ProgramService(
            db,
            new HttpContextAccessor { HttpContext = httpContext },
            managementContext,
            resolver.Object,
            permissions);
        var controller = new ProgramController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var createdResult = await controller.Create(new SaveProgramDto
        {
            Name = "Program without a manager",
            PortfolioId = portfolioId,
            Status = "Planning",
            Currency = "USD"
        }, default);

        if (createdResult is not CreatedAtActionResult)
        {
            var failure = Assert.IsType<ObjectResult>(createdResult);
            Assert.Fail($"Program creation failed with HTTP {failure.StatusCode}: {JsonSerializer.Serialize(failure.Value)}");
        }
        var created = (CreatedAtActionResult)createdResult;
        Assert.Equal(nameof(ProgramController.GetById), created.ActionName);
        var createdProgram = Assert.IsType<ProgramDto>(created.Value);
        Assert.Null(createdProgram.ProgramManagerId);
        Assert.Equal("PGM-0001", createdProgram.Code);
        Assert.Equal(0, createdProgram.ProjectCount);
        Assert.Empty(createdProgram.Projects);
        Assert.Single(await db.Programs.ToListAsync());

        db.ChangeTracker.Clear();
        managementContext.Set(CreateManagementContext(tenantId, userId, createdProgram.Id));
        var reloadedResult = await controller.GetById(createdProgram.Id, default);

        var reloaded = Assert.IsType<OkObjectResult>(reloadedResult);
        var reloadedProgram = Assert.IsType<ProgramDto>(reloaded.Value);
        Assert.Equal(createdProgram.Id, reloadedProgram.Id);
        Assert.Equal(portfolioId, reloadedProgram.PortfolioId);
        Assert.Null(reloadedProgram.ProgramManagerId);
    }

    [Fact]
    public async Task Create_rejects_invalid_name_as_validation_without_writing()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProgramCreationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ProgramCreationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Portfolios.Add(new PortfolioEntity
        {
            Id = portfolioId,
            TenantId = tenantId,
            Name = "Assigned Portfolio",
            Code = "PORT-01",
            Status = "Active",
            Currency = "USD",
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.GetPerspectiveSnapshotAsync(
                userId,
                tenantId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(portfolioId));
        var service = new ProgramService(
            db,
            CreateHttpContextAccessor(tenantId, userId),
            CreateManagementContextAccessor(tenantId, userId),
            resolver.Object,
            new TestEffectivePermissions("Program.Create"));

        var result = await service.CreateAsync(new SaveProgramDto
        {
            Name = " ",
            PortfolioId = portfolioId,
            Status = "Planning",
            Currency = "USD"
        }, userId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error?.StatusCode);
        Assert.Contains("Name is required", result.Error?.Message);
        Assert.Empty(await db.Programs.ToListAsync());
    }

    [Fact]
    public async Task Create_cannot_use_a_portfolio_from_another_tenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var assignedPortfolioId = Guid.NewGuid();
        var otherTenantPortfolioId = Guid.NewGuid();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProgramCreationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ProgramCreationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Portfolios.AddRange(
            new PortfolioEntity
            {
                Id = assignedPortfolioId,
                TenantId = tenantId,
                Name = "Actor Portfolio",
                Code = "PORT-A",
                Status = "Active",
                Currency = "USD",
                CreatedBy = "test"
            },
            new PortfolioEntity
            {
                Id = otherTenantPortfolioId,
                TenantId = otherTenantId,
                Name = "Other Tenant Portfolio",
                Code = "PORT-B",
                Status = "Active",
                Currency = "USD",
                CreatedBy = "test"
            });
        await db.SaveChangesAsync();

        var resolver = new Mock<IManagementScopeResolver>();
        resolver.Setup(item => item.GetPerspectiveSnapshotAsync(
                userId,
                tenantId,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(assignedPortfolioId));
        var service = new ProgramService(
            db,
            CreateHttpContextAccessor(tenantId, userId),
            CreateManagementContextAccessor(tenantId, userId),
            resolver.Object,
            new TestEffectivePermissions("Program.Create"));

        var result = await service.CreateAsync(new SaveProgramDto
        {
            Name = "Cross tenant Program",
            PortfolioId = otherTenantPortfolioId,
            Status = "Planning",
            Currency = "USD"
        }, userId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error?.StatusCode);
        Assert.Empty(await db.Programs.ToListAsync());
    }

    private static IHttpContextAccessor CreateHttpContextAccessor(Guid tenantId, Guid userId)
    {
        return new HttpContextAccessor { HttpContext = CreateHttpContext(tenantId, userId) };
    }

    private static DefaultHttpContext CreateHttpContext(Guid tenantId, Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("permission", "Program.Create")
        ], "test"));
        return httpContext;
    }

    private static ManagementContextAccessor CreateManagementContextAccessor(Guid tenantId, Guid userId)
    {
        var accessor = new ManagementContextAccessor();
        accessor.Set(CreateManagementContext(tenantId, userId));
        return accessor;
    }

    private static ManagementContext CreateManagementContext(Guid tenantId, Guid userId, Guid? programId = null) => new(
        userId,
        tenantId,
        ManagementPerspective.ProgramManager,
        ManagementViewMode.Native,
        null,
        programId,
        null,
        new ManagementScope(
            new HashSet<Guid>(),
            programId.HasValue ? new HashSet<Guid> { programId.Value } : new HashSet<Guid>(),
            new HashSet<Guid>()));

    private static ManagementPerspectiveSnapshot CreateSnapshot(Guid portfolioId) => new(
        Array.Empty<ManagementPerspective>(),
        [new ManagementPerspectiveAvailability(
            ManagementPerspective.PortfolioManager,
            true,
            false,
            [new ManagementEntityReference(portfolioId, "Portfolio")])],
        ManagementPerspective.PortfolioManager,
        false,
        ManagementScope.Empty,
        null);

    private sealed class TestEffectivePermissions(params string[] permissions) : IUserEffectivePermissionService
    {
        private readonly IReadOnlySet<string> _permissions = new HashSet<string>(permissions, StringComparer.Ordinal);

        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken = default) => Task.FromResult(_permissions);

        public Task<IReadOnlySet<string>> GetEffectivePermissionsForOrganizationAsync(
            Guid userId,
            Guid tenantId,
            Guid organizationId,
            CancellationToken cancellationToken = default) => Task.FromResult(_permissions);

        public Task<IReadOnlySet<string>> GetEffectiveRoleNamesAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
    }

    private sealed class ProgramCreationDbContext(DbContextOptions<ProgramCreationDbContext> options) : DbContext(options)
    {
        public DbSet<ProgramEntity> Programs => Set<ProgramEntity>();
        public DbSet<PortfolioEntity> Portfolios => Set<PortfolioEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProgramEntity>().ToTable("Programs").Ignore(program => program.Portfolio);
            modelBuilder.Entity<PortfolioEntity>()
                .ToTable("Portfolios")
                .Ignore(portfolio => portfolio.Programs)
                .Ignore(portfolio => portfolio.HealthHistory)
                .Ignore(portfolio => portfolio.Budgets);
        }
    }
}
