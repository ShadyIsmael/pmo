using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Regression tests for the AppDbContext global query filters: tenant isolation and
/// soft-delete must be combined into a single filter expression per entity, so both
/// predicates are always enforced together (a second HasQueryFilter call would replace
/// the first). Verify deleted rows are hidden, cross-tenant rows are hidden, and
/// behavior is stable across query order and multiple contexts.
/// </summary>
public class GlobalQueryFilterTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor;

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId_A = Guid.NewGuid();
    private readonly Guid _projectId_B = Guid.NewGuid();

    public GlobalQueryFilterTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        Seed();
        _accessor = new Mock<IHttpContextAccessor>();
    }

    private void Seed()
    {
        _context.Projects.AddRange(
            new Project { Id = _projectId_A, TenantId = _tenantA, Name = "Project A", Code = "PA", Description = "A", Status = "Active", Currency = "GBP", CreatedBy = _userA.ToString(), RowVersion = new byte[8] },
            new Project { Id = _projectId_B, TenantId = _tenantB, Name = "Project B", Code = "PB", Description = "B", Status = "Active", Currency = "GBP", CreatedBy = _userA.ToString(), RowVersion = new byte[8] });
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

    private void SoftDeleteTenantAProject()
    {
        var projectA = _context.Projects.IgnoreQueryFilters().Single(p => p.Id == _projectId_A);
        projectA.IsDeleted = true;
        _context.SaveChangesAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task DeletedRow_SameTenant_IsHidden()
    {
        SetTenant(_tenantA, _userA);
        SoftDeleteTenantAProject();

        Assert.Empty(await _context.Projects.ToListAsync());
        // Even by explicit id the deleted row is invisible.
        Assert.Empty(await _context.Projects.Where(p => p.Id == _projectId_A).ToListAsync());
    }

    [Fact]
    public async Task ActiveRow_AnotherTenant_IsHidden()
    {
        SetTenant(_tenantA, _userA);

        Assert.Single(await _context.Projects.ToListAsync());
        Assert.Empty(await _context.Projects.Where(p => p.Id == _projectId_B).ToListAsync());
    }

    [Fact]
    public async Task DeletedRow_AnotherTenant_IsHidden()
    {
        // Delete A's row while acting as B, then read as A.
        SetTenant(_tenantB, _userA);
        var projectB = _context.Projects.IgnoreQueryFilters().Single(p => p.Id == _projectId_B);
        projectB.IsDeleted = true;
        _context.SaveChangesAsync().GetAwaiter().GetResult();

        SetTenant(_tenantA, _userA);
        // A still sees only its own active row.
        Assert.Single(await _context.Projects.ToListAsync());
        Assert.Empty(await _context.Projects.Where(p => p.Id == _projectId_B).ToListAsync());
    }

    [Fact]
    public async Task ActiveRow_SameTenant_RemainsVisible()
    {
        SetTenant(_tenantA, _userA);
        SoftDeleteTenantAProject();

        // After A's project is deleted, A's view is empty; but an active row stays visible.
        Assert.Empty(await _context.Projects.ToListAsync());

        // B's active row must remain visible to B.
        SetTenant(_tenantB, _userA);
        Assert.Single(await _context.Projects.ToListAsync());
        Assert.Equal(_projectId_B, (await _context.Projects.ToListAsync())[0].Id);
    }

    [Fact]
    public async Task IgnoreQueryFilters_SeesDeletedAndCrossTenantRows()
    {
        SetTenant(_tenantA, _userA);
        SoftDeleteTenantAProject();

        // Admin/recovery path sees everything regardless of the active tenant.
        var all = await _context.Projects.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, p => p.Id == _projectId_A);
        Assert.Contains(all, p => p.Id == _projectId_B);
    }

    [Fact]
    public async Task FilterBehavior_IsStable_AcrossQueryOrder()
    {
        SetTenant(_tenantA, _userA);
        SoftDeleteTenantAProject();

        // A's view first.
        Assert.Empty(await _context.Projects.ToListAsync());
        SetTenant(_tenantB, _userA);
        Assert.Single(await _context.Projects.ToListAsync());

        // Reversed: B's view first, then A's view, on the same context.
        Assert.Single(await _context.Projects.ToListAsync());
        SetTenant(_tenantA, _userA);
        Assert.Empty(await _context.Projects.ToListAsync());
    }

    [Fact]
    public async Task FilterBehavior_IsStable_AcrossMultipleContexts()
    {
        SetTenant(_tenantA, _userA);
        SoftDeleteTenantAProject();

        var extraContexts = new List<AppDbContext>();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(_connection)
                    .Options);
                extraContexts.Add(ctx);
                Assert.Same(_context.Model, ctx.Model);
            }

            Assert.Empty(await extraContexts[0].Projects.ToListAsync());
            Assert.Equal(2, (await extraContexts[1].Projects.IgnoreQueryFilters().ToListAsync()).Count);
            Assert.Empty(await extraContexts[2].Projects.ToListAsync());

            SetTenant(_tenantB, _userA);
            Assert.Single(await extraContexts[0].Projects.ToListAsync());
            Assert.Single(await extraContexts[2].Projects.ToListAsync());
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