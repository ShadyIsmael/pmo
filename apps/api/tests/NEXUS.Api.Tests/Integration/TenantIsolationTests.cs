using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Regression tests for the tenant isolation design: one cached EF model whose
/// global query filters read the current tenant per-execution (parameterized),
/// so tenants never see each other's rows and no per-tenant model is created.
/// Each test uses fresh random tenant/user/project ids, so results must be
/// independent of test execution order.
/// </summary>
[Collection("Static tenant resolution")]
public class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor;

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _projectId_A = Guid.NewGuid();
    private readonly Guid _projectId_B = Guid.NewGuid();

    public TenantIsolationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new SqliteTestAppDbContext(options);

        _context.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        Seed();
        _accessor = new Mock<IHttpContextAccessor>();
    }

    private void Seed()
    {
        _context.Users.AddRange(
            new User { Id = _userA, TenantId = _tenantA, Email = "a@example.com", PasswordHash = "hash", FirstName = "A", LastName = "TenantA", RowVersion = new byte[8] },
            new User { Id = _userB, TenantId = _tenantB, Email = "b@example.com", PasswordHash = "hash", FirstName = "B", LastName = "TenantB", RowVersion = new byte[8] });
        _context.Projects.AddRange(
            new Project { Id = _projectId_A, TenantId = _tenantA, Name = "Project A", Code = "PA", Description = "A", Status = "Active", Currency = "GBP", CreatedBy = _userA.ToString(), RowVersion = new byte[8] },
            new Project { Id = _projectId_B, TenantId = _tenantB, Name = "Project B", Code = "PB", Description = "B", Status = "Active", Currency = "GBP", CreatedBy = _userB.ToString(), RowVersion = new byte[8] });
        _context.SaveChangesAsync().GetAwaiter().GetResult();
    }

    private void SetTenant(Guid tenantId, Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "mock"));
        _accessor.Setup(x => x.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_accessor.Object);
    }

    [Fact]
    public async Task TenantA_NeverReadsTenantB_Data()
    {
        SetTenant(_tenantA, _userA);
        var asTenantA = await _context.Projects.ToListAsync();
        Assert.Single(asTenantA);
        Assert.Equal(_projectId_A, asTenantA[0].Id);

        // B's project is invisible to A, even by id.
        Assert.Empty(await _context.Projects.Where(p => p.Id == _projectId_B).ToListAsync());
    }

    [Fact]
    public async Task TenantB_NeverReadsTenantA_Data()
    {
        SetTenant(_tenantB, _userB);
        var asTenantB = await _context.Projects.ToListAsync();
        Assert.Single(asTenantB);
        Assert.Equal(_projectId_B, asTenantB[0].Id);

        // A's project is invisible to B, even by id.
        Assert.Empty(await _context.Projects.Where(p => p.Id == _projectId_A).ToListAsync());
    }

    [Fact]
    public async Task Isolation_Holds_RegardlessOfQueryOrder()
    {
        // Read A's view first, then B's view.
        SetTenant(_tenantA, _userA);
        Assert.Single(await _context.Projects.ToListAsync());
        Assert.Empty(await _context.Projects.Where(p => p.Id == _projectId_B).ToListAsync());

        SetTenant(_tenantB, _userB);
        Assert.Single(await _context.Projects.ToListAsync());
        Assert.Empty(await _context.Projects.Where(p => p.Id == _projectId_A).ToListAsync());

        // Read B's view first, then A's view (reversed order on the same context).
        Assert.Empty(await _context.Projects.Where(p => p.Id == _projectId_A).ToListAsync());

        SetTenant(_tenantA, _userA);
        Assert.Empty(await _context.Projects.Where(p => p.Id == _projectId_B).ToListAsync());
    }

    [Fact]
    public async Task Repeated_RandomTenantIds_DoNotCreateTenantSpecificModels()
    {
        // Different tenants on the same connection still share a single cached model.
        var extraContexts = new List<AppDbContext>();
        try
        {
            var seenModel = _context.Model;
            for (var i = 0; i < 5; i++)
            {
                var ctx = new SqliteTestAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(_connection)
                    .Options);
                extraContexts.Add(ctx);
                Assert.Same(seenModel, ctx.Model);
            }

            // A random, never-seeded tenant sees nothing, in any context, in any order.
            var randomTenant = Guid.NewGuid();
            SetTenant(randomTenant, Guid.NewGuid());
            Assert.Empty(await extraContexts[0].Projects.ToListAsync());
            Assert.Empty(await extraContexts[4].Projects.ToListAsync());
            Assert.Empty(await extraContexts[2].Projects.ToListAsync());
        }
        finally
        {
            foreach (var ctx in extraContexts)
            {
                ctx.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }
}
