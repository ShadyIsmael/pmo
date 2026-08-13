using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.ProjectManagement.Domain.Entities;
using NEXUS.Persistence;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Regression tests proving the modernized ProjectBaselineVersion participates in the
/// global query filters: it is tenant-isolated and soft-delete aware, with the two
/// predicates combined into a single filter (a second HasQueryFilter call would have
/// replaced the first).
/// </summary>
public class ProjectBaselineVersionQueryFilterTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor;

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId_A = Guid.NewGuid();
    private readonly Guid _projectId_B = Guid.NewGuid();
    private readonly Guid _baselineId_A = Guid.NewGuid();
    private readonly Guid _baselineId_A2 = Guid.NewGuid();
    private readonly Guid _baselineId_B = Guid.NewGuid();

    public ProjectBaselineVersionQueryFilterTests()
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
        _context.ProjectBaselineVersions.AddRange(
            new ProjectBaselineVersion { Id = _baselineId_A, TenantId = _tenantA, ProjectId = new ProjectId(_projectId_A.ToString()), VersionNumber = new VersionNumber(1), Name = "Baseline A1", Status = Status.Approved, IsCurrent = true, CreatedBy = _userA.ToString() },
            new ProjectBaselineVersion { Id = _baselineId_A2, TenantId = _tenantA, ProjectId = new ProjectId(_projectId_A.ToString()), VersionNumber = new VersionNumber(2), Name = "Baseline A2", Status = Status.Superseded, IsCurrent = false, CreatedBy = _userA.ToString() },
            new ProjectBaselineVersion { Id = _baselineId_B, TenantId = _tenantB, ProjectId = new ProjectId(_projectId_B.ToString()), VersionNumber = new VersionNumber(1), Name = "Baseline B1", Status = Status.Approved, IsCurrent = true, CreatedBy = _userA.ToString() });
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
    public async Task ActiveBaseline_AnotherTenant_IsHidden()
    {
        SetTenant(_tenantA, _userA);

        var rows = await _context.ProjectBaselineVersions.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, b => b.Id == _baselineId_A);
        Assert.Contains(rows, b => b.Id == _baselineId_A2);
        Assert.DoesNotContain(rows, b => b.Id == _baselineId_B);
        Assert.Empty(await _context.ProjectBaselineVersions.Where(b => b.Id == _baselineId_B).ToListAsync());
    }

    [Fact]
    public async Task DeletedBaseline_SameTenant_IsHidden()
    {
        SetTenant(_tenantA, _userA);
        var baseline = _context.ProjectBaselineVersions.IgnoreQueryFilters().Single(b => b.Id == _baselineId_A2);
        baseline.IsDeleted = true;
        await _context.SaveChangesAsync();

        var rows = await _context.ProjectBaselineVersions.ToListAsync();
        Assert.Single(rows);
        Assert.Equal(_baselineId_A, rows[0].Id);
        Assert.Empty(await _context.ProjectBaselineVersions.Where(b => b.Id == _baselineId_A2).ToListAsync());
    }

    [Fact]
    public async Task DeletedBaseline_AnotherTenant_IsHidden()
    {
        // Delete B's baseline while acting as A; then B must not see it either.
        SetTenant(_tenantA, _userA);
        var baselineB = _context.ProjectBaselineVersions.IgnoreQueryFilters().Single(b => b.Id == _baselineId_B);
        baselineB.IsDeleted = true;
        await _context.SaveChangesAsync();

        SetTenant(_tenantB, _userA);
        Assert.Empty(await _context.ProjectBaselineVersions.ToListAsync());
    }

    [Fact]
    public async Task IgnoreQueryFilters_SeesDeletedAndCrossTenantBaselines()
    {
        SetTenant(_tenantA, _userA);
        var baseline = _context.ProjectBaselineVersions.IgnoreQueryFilters().Single(b => b.Id == _baselineId_A2);
        baseline.IsDeleted = true;
        await _context.SaveChangesAsync();

        var all = await _context.ProjectBaselineVersions.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(3, all.Count);
        Assert.Contains(all, b => b.Id == _baselineId_A);
        Assert.Contains(all, b => b.Id == _baselineId_A2);
        Assert.Contains(all, b => b.Id == _baselineId_B);
    }

    [Fact]
    public async Task ActiveBaseline_SameTenant_RemainsVisible()
    {
        SetTenant(_tenantA, _userA);

        var rows = await _context.ProjectBaselineVersions.ToListAsync();
        Assert.Equal(2, rows.Count);

        SetTenant(_tenantB, _userA);
        var rowsB = await _context.ProjectBaselineVersions.ToListAsync();
        Assert.Single(rowsB);
        Assert.Equal(_baselineId_B, rowsB[0].Id);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }
}
