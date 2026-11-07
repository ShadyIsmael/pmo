using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class TenantContextFailClosedTests : IDisposable
{
    private static readonly Guid LegacyDemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HttpContextAccessor _httpContextAccessor = new();
    private readonly AppDbContext _db;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _demoProjectId = Guid.NewGuid();
    private readonly Guid _tenantAProjectId = Guid.NewGuid();
    private readonly Guid _emptyTenantProjectId = Guid.NewGuid();

    public TenantContextFailClosedTests()
    {
        _connection.Open();
        AppDbContext.ConfigureTenantResolution(_httpContextAccessor);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new SqliteTestAppDbContext(options);
        _db.Database.EnsureCreated();

        using (TrustedTenantExecutionContext.Enter(LegacyDemoTenantId))
        {
            _db.Projects.Add(NewProject(_demoProjectId, LegacyDemoTenantId, "LEGACY-DEMO"));
            _db.SaveChanges();
        }

        using (TrustedTenantExecutionContext.Enter(_tenantA))
        {
            _db.Projects.AddRange(
                NewProject(_tenantAProjectId, _tenantA, "TENANT-A"),
                NewProject(_emptyTenantProjectId, _tenantA, "INVALID-EMPTY-TENANT"));
            _db.SaveChanges();
        }

        // Seed an intentionally invalid legacy row directly in the isolated SQLite
        // fixture so the query filter remains tested after the production write guard
        // rejects new Guid.Empty tenant entities.
        _db.Database.ExecuteSqlInterpolated(
            $"UPDATE \"Projects\" SET \"TenantId\" = {Guid.Empty} WHERE \"Id\" = {_emptyTenantProjectId}");
        _db.ChangeTracker.Clear();

    }

    [Fact]
    public async Task Missing_http_context_does_not_fall_back_to_demo_tenant()
    {
        _httpContextAccessor.HttpContext = null;

        var visibleProjects = await _db.Projects.ToListAsync();

        Assert.Empty(visibleProjects);
    }

    [Fact]
    public async Task Guid_empty_tenant_rows_are_invisible_when_http_tenant_context_is_missing_or_empty()
    {
        _httpContextAccessor.HttpContext = null;
        Assert.Empty(await _db.Projects.Where(project => project.Id == _emptyTenantProjectId).ToListAsync());

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", Guid.Empty.ToString())],
                authenticationType: "test"))
        };

        Assert.Empty(await _db.Projects.Where(project => project.Id == _emptyTenantProjectId).ToListAsync());
    }

    [Fact]
    public async Task Authenticated_request_without_tenant_claim_fails_closed()
    {
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                authenticationType: "test"))
        };

        var visibleProjects = await _db.Projects.ToListAsync();

        Assert.Empty(visibleProjects);
    }

    [Fact]
    public async Task Invalid_http_tenant_claim_ignores_an_explicit_trusted_background_scope()
    {
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                authenticationType: "test"))
        };

        using var trustedScope = TrustedTenantExecutionContext.Enter(LegacyDemoTenantId);
        var visibleProjects = await _db.Projects.ToListAsync();

        Assert.Empty(visibleProjects);
    }

    [Fact]
    public async Task Explicit_trusted_background_scope_reads_only_its_tenant_and_disposal_restores_empty_scope()
    {
        _httpContextAccessor.HttpContext = null;

        using (TrustedTenantExecutionContext.Enter(_tenantA))
        {
            var visibleProjects = await _db.Projects.ToListAsync();
            var project = Assert.Single(visibleProjects);
            Assert.Equal(_tenantAProjectId, project.Id);
            Assert.DoesNotContain(visibleProjects, item => item.Id == _demoProjectId);
        }

        Assert.Empty(await _db.Projects.ToListAsync());
    }

    [Fact]
    public async Task Trusted_tenant_claim_still_reads_its_own_record()
    {
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", _tenantA.ToString())],
                authenticationType: "test"))
        };

        var visibleProjects = await _db.Projects.ToListAsync();

        var visibleProject = Assert.Single(visibleProjects);
        Assert.Equal(_tenantAProjectId, visibleProject.Id);
        Assert.DoesNotContain(visibleProjects, project => project.Id == _demoProjectId);
    }

    private static Project NewProject(Guid id, Guid tenantId, string code) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = code,
        Code = code,
        Description = "Tenant boundary regression fixture",
        Status = "Active",
        Currency = "USD",
        CreatedBy = "tenant-boundary-test",
        RowVersion = new byte[8]
    };

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
