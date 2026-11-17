using NEXUS.Api.Management;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Application;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class PortfolioOrganizationScopeTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly HttpContextAccessor _http;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _otherOrganizationId = Guid.NewGuid();
    private readonly Guid _portfolioId = Guid.NewGuid();

    public PortfolioOrganizationScopeTests()
    {
        _connection.Open();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenantId", _tenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, _actorId.ToString())
            ], "portfolio-organization-tests"))
        };
        _http = new HttpContextAccessor { HttpContext = httpContext };
        AppDbContext.ConfigureTenantResolution(_http);
        _db = new SqliteTestAppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.Organizations.AddRange(
            new Organization { Id = _organizationId, TenantId = _tenantId, Name = "In-scope Organization", IsActive = true, CreatedBy = "tests" },
            new Organization { Id = _otherOrganizationId, TenantId = _tenantId, Name = "Out-of-scope Organization", IsActive = true, CreatedBy = "tests" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Create_persists_only_an_active_organization_in_the_management_scope()
    {
        var result = await CreateService(OrganizationScope(_organizationId)).CreateAsync(
            new SavePortfolioDto { Name = "Scoped portfolio", OrganizationId = _organizationId, Status = "Active", Currency = "EGP" },
            _actorId.ToString("D"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(_organizationId, result.Value!.OrganizationId);
        var saved = await _db.Portfolios.IgnoreQueryFilters().SingleAsync(item => item.Id == result.Value.Id);
        Assert.Equal(_tenantId, saved.TenantId);
        Assert.Equal(_organizationId, saved.OrganizationId);
    }

    [Fact]
    public async Task Create_rejects_an_active_organization_outside_the_management_scope()
    {
        var result = await CreateService(OrganizationScope(_organizationId)).CreateAsync(
            new SavePortfolioDto { Name = "Out-of-scope portfolio", OrganizationId = _otherOrganizationId, Status = "Active", Currency = "EGP" },
            _actorId.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error?.StatusCode);
        Assert.Empty(await _db.Portfolios.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Update_cannot_change_portfolio_organization_without_governed_transfer()
    {
        _db.Portfolios.Add(new Portfolio
        {
            Id = _portfolioId,
            TenantId = _tenantId,
            OrganizationId = _organizationId,
            Name = "Existing portfolio",
            Code = "PF-0001",
            Status = "Active",
            Currency = "EGP",
            CreatedBy = "tests"
        });
        await _db.SaveChangesAsync();

        var scope = new ManagementScope(
            new HashSet<Guid> { _portfolioId }, new HashSet<Guid>(), new HashSet<Guid>())
        {
            OrganizationIds = new HashSet<Guid> { _organizationId }
        };
        var result = await CreateService(scope).UpdateAsync(
            _portfolioId,
            new SavePortfolioDto { Name = "Attempted transfer", OrganizationId = _otherOrganizationId, Status = "Active", Currency = "EGP" },
            _actorId.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error?.StatusCode);
        var unchanged = await _db.Portfolios.IgnoreQueryFilters().SingleAsync(item => item.Id == _portfolioId);
        Assert.Equal(_organizationId, unchanged.OrganizationId);
        Assert.Equal("Existing portfolio", unchanged.Name);
    }

    [Fact]
    public async Task Create_requires_an_organization()
    {
        var result = await CreateService(OrganizationScope(_organizationId)).CreateAsync(
            new SavePortfolioDto { Name = "Missing organization", Status = "Active", Currency = "EGP" },
            _actorId.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.Equal("organizationId", result.Error?.FieldName);
        Assert.Empty(await _db.Portfolios.IgnoreQueryFilters().ToListAsync());
    }

    private PortfolioService CreateService(ManagementScope scope)
    {
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            _actorId,
            _tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            scope));
        return new PortfolioService(_db, _http, management);
    }

    private ManagementScope OrganizationScope(Guid organizationId) => new(
        new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>())
    {
        OrganizationIds = new HashSet<Guid> { organizationId }
    };

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
