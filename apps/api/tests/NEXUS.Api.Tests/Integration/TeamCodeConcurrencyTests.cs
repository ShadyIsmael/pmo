using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class TeamCodeConcurrencyTests : IDisposable
{
    private const string ConnectionString = "Data Source=file:nexus-team-code-concurrency;Mode=Memory;Cache=Shared";

    private readonly SqliteConnection _keeperConnection = new(ConnectionString);
    private readonly List<(AppDbContext Context, SqliteConnection Connection)> _contexts = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public TeamCodeConcurrencyTests()
    {
        _keeperConnection.Open();
        _audit.Setup(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        SetTenant(_tenantA);
        using var schemaContext = CreateContext();
        CreateSqliteTestSchema(schemaContext);
    }

    [Fact]
    public async Task First_team_uses_canonical_code()
    {
        var result = await CreateService(CreateContext()).CreateAsync(NewTeam("First"), "test");

        Assert.True(result.IsSuccess);
        Assert.Equal("TEAM-000001", result.Value!.Code);
    }

    [Fact]
    public async Task Sequential_creates_increment_the_tenant_counter()
    {
        var service = CreateService(CreateContext());

        var first = await service.CreateAsync(NewTeam("First"), "test");
        var second = await service.CreateAsync(NewTeam("Second"), "test");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("TEAM-000001", first.Value!.Code);
        Assert.Equal("TEAM-000002", second.Value!.Code);
    }

    [Fact]
    public async Task Existing_team_codes_bootstrap_a_missing_sequence_row()
    {
        var context = CreateContext();
        context.Teams.Add(new Team
        {
            TenantId = _tenantA,
            Code = "TEAM-000041",
            Name = "Existing",
            CreatedBy = "migration-test"
        });
        await context.SaveChangesAsync();

        var result = await CreateService(CreateContext()).CreateAsync(NewTeam("After migration"), "test");

        Assert.True(result.IsSuccess);
        Assert.Equal("TEAM-000042", result.Value!.Code);
    }

    [Fact]
    public async Task Tenants_have_independent_team_code_sequences()
    {
        var tenantAResult = await CreateService(CreateContext()).CreateAsync(NewTeam("Tenant A"), "test");

        SetTenant(_tenantB);
        var tenantBResult = await CreateService(CreateContext()).CreateAsync(NewTeam("Tenant B"), "test");

        Assert.True(tenantAResult.IsSuccess);
        Assert.True(tenantBResult.IsSuccess);
        Assert.Equal("TEAM-000001", tenantAResult.Value!.Code);
        Assert.Equal("TEAM-000001", tenantBResult.Value!.Code);

        var context = CreateContext();
        var rows = await context.Teams.IgnoreQueryFilters().ToListAsync();
        Assert.Contains(rows, team => team.TenantId == _tenantA && team.Code == "TEAM-000001");
        Assert.Contains(rows, team => team.TenantId == _tenantB && team.Code == "TEAM-000001");
    }

    [Fact]
    public async Task Concurrent_creates_all_succeed_with_unique_codes()
    {
        const int batchSize = 24;

        var results = await Task.WhenAll(Enumerable.Range(1, batchSize).Select(index =>
            CreateTeamOnIndependentContextAsync($"Concurrent {index}")));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        var codes = results.Select(result => result.Value!.Code).ToList();
        Assert.Equal(batchSize, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            Enumerable.Range(1, batchSize),
            codes.Select(ParseCodeValue).OrderBy(value => value));
    }

    [Fact]
    public async Task High_concurrency_batch_does_not_return_code_collision_conflicts()
    {
        // Exercise the already-initialized counter path, which is the normal
        // production path after the migration has created the tenant row.
        var initial = await CreateService(CreateContext()).CreateAsync(NewTeam("Initial"), "test");
        Assert.True(initial.IsSuccess);

        const int batchSize = 48;
        var results = await Task.WhenAll(Enumerable.Range(1, batchSize).Select(index =>
            CreateTeamOnIndependentContextAsync($"High concurrency {index}")));

        Assert.All(results, result =>
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.NotEqual(409, result.Error?.StatusCode);
        });

        var codes = results.Select(result => result.Value!.Code).ToList();
        Assert.Equal(batchSize, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain("Please retry", string.Join(" ", results.Select(result => result.Error?.Message)));
    }

    [Fact]
    public async Task Failed_code_allocation_does_not_advance_the_sequence()
    {
        var context = CreateContext();
        context.Teams.Add(new Team
        {
            TenantId = _tenantA,
            Code = "TEAM-000001",
            Name = "Existing",
            CreatedBy = "test"
        });
        context.EntityCodeSequences.Add(new EntityCodeSequence
        {
            TenantId = _tenantA,
            EntityType = "Team",
            NextValue = 1
        });
        await context.SaveChangesAsync();

        var result = await CreateService(CreateContext()).CreateAsync(NewTeam("Conflicting allocation"), "test");

        Assert.True(result.IsFailure);
        Assert.Equal(503, result.Error!.StatusCode);

        var verification = CreateContext();
        var sequence = await verification.EntityCodeSequences.IgnoreQueryFilters()
            .SingleAsync(item => item.TenantId == _tenantA && item.EntityType == "Team");
        Assert.Equal(1, sequence.NextValue);
        Assert.Equal(1, await verification.Teams.IgnoreQueryFilters().CountAsync(team => team.TenantId == _tenantA));
    }

    [Fact]
    public async Task Tenant_and_code_unique_constraint_rejects_duplicate_persisted_codes()
    {
        var context = CreateContext();
        context.Teams.AddRange(
            new Team { TenantId = _tenantA, Code = "TEAM-999999", Name = "One", CreatedBy = "test" },
            new Team { TenantId = _tenantA, Code = "TEAM-999999", Name = "Two", CreatedBy = "test" });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private async Task<Result<TeamDto>> CreateTeamOnIndependentContextAsync(string name)
    {
        var context = CreateContext();
        return await CreateService(context).CreateAsync(NewTeam(name), "test");
    }

    private AppDbContext CreateContext()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options);
        _contexts.Add((context, connection));
        return context;
    }

    private static void CreateSqliteTestSchema(AppDbContext context)
    {
        // The production model contains SQL Server-specific column types on a few
        // unrelated tables. Create only the Team aggregate schema here so SQLite can
        // exercise the real service transaction and unique indexes without pretending
        // to be a full production database migration runner.
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Teams (
                Id TEXT NOT NULL PRIMARY KEY,
                TenantId TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NULL,
                CreatedBy TEXT NOT NULL,
                UpdatedBy TEXT NULL,
                IsDeleted INTEGER NOT NULL,
                RowVersion BLOB NOT NULL,
                DepartmentId TEXT NULL,
                Name TEXT NOT NULL,
                Code TEXT NULL,
                Description TEXT NULL,
                TeamLeadId TEXT NULL,
                IsActive INTEGER NOT NULL,
                ExternalDirectoryId TEXT NULL,
                EmailAlias TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Teams_TenantId_Code
                ON Teams (TenantId, Code) WHERE Code IS NOT NULL;
            CREATE TABLE IF NOT EXISTS TeamMembers (
                Id TEXT NOT NULL PRIMARY KEY,
                TenantId TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NULL,
                CreatedBy TEXT NOT NULL,
                UpdatedBy TEXT NULL,
                IsDeleted INTEGER NOT NULL,
                RowVersion BLOB NOT NULL,
                TeamId TEXT NOT NULL,
                ResourceId TEXT NOT NULL,
                EffectiveFrom TEXT NOT NULL,
                EffectiveTo TEXT NULL,
                IsActive INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS EntityCodeSequences (
                Id TEXT NOT NULL PRIMARY KEY,
                TenantId TEXT NOT NULL,
                EntityType TEXT NOT NULL,
                NextValue INTEGER NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_EntityCodeSequences_TenantId_EntityType
                ON EntityCodeSequences (TenantId, EntityType);
            """);
    }

    private TeamService CreateService(AppDbContext context) => new(context, _audit.Object, _http.Object);

    private static CreateTeamDto NewTeam(string name) => new() { Name = name };

    private void SetTenant(Guid tenantId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("tenantId", tenantId.ToString()), new Claim(ClaimTypes.NameIdentifier, "test") },
                "test"))
        };
        _http.Setup(accessor => accessor.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_http.Object);
    }

    private static int ParseCodeValue(string? code)
    {
        Assert.NotNull(code);
        return int.Parse(code!["TEAM-".Length..]);
    }

    public void Dispose()
    {
        foreach (var (context, connection) in _contexts)
        {
            context.Dispose();
            connection.Dispose();
        }

        _keeperConnection.Dispose();
    }
}
