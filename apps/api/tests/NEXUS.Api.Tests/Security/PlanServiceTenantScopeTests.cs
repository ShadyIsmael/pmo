using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class PlanServiceTenantScopeTests : IDisposable
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectA = Guid.NewGuid();
    private readonly Guid _projectB = Guid.NewGuid();
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HttpContextAccessor _http = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IProjectPublicIdLookup> _projectLookup = new();
    private readonly AppDbContext _db;

    public PlanServiceTenantScopeTests()
    {
        _connection.Open();
        AppDbContext.ConfigureTenantResolution(_http);
        _db = new SqliteTestAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();
        _audit.Setup(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _projectLookup.Setup(lookup => lookup.FindPublicIdAsync(
                _projectA, _tenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        _projectLookup.Setup(lookup => lookup.FindPublicIdAsync(
                _projectB, _tenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        SeedProject(_tenantA, _projectA, "TENANT-A");
        SeedProject(_tenantB, _projectB, "TENANT-B");
        _http.HttpContext = RequestContext(_tenantA, _userId);
    }

    [Fact]
    public async Task Create_allows_same_tenant_project_inside_management_scope()
    {
        var service = CreateService(_projectA);

        var result = await service.CreateAsync(NewPlanRequest(_projectA), _userId.ToString("D"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        var persistedPlan = await _db.Plans.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        var persistedVersion = await _db.PlanVersions.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(_tenantA, persistedPlan.TenantId);
        Assert.Equal(_projectA, persistedPlan.ProjectId);
        Assert.Equal(persistedPlan.Id, persistedVersion.PlanId);
        _audit.Verify(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_rejects_same_tenant_project_outside_management_scope_before_write_or_audit()
    {
        var service = CreateService();

        var result = await service.CreateAsync(NewPlanRequest(_projectA), _userId.ToString("D"));

        Assert.True(result.IsFailure);
        await AssertNoPlanWriteOrAuditAsync();
    }

    [Fact]
    public async Task Create_rejects_foreign_tenant_project_even_if_management_scope_contains_its_id()
    {
        var service = CreateService(_projectB);

        var result = await service.CreateAsync(NewPlanRequest(_projectB), _userId.ToString("D"));

        Assert.True(result.IsFailure);
        await AssertNoPlanWriteOrAuditAsync();
    }

    [Fact]
    public async Task Create_rejects_when_management_context_is_missing_before_write_or_audit()
    {
        var service = CreateServiceWithoutManagementContext();

        var result = await service.CreateAsync(NewPlanRequest(_projectA), _userId.ToString("D"));

        Assert.True(result.IsFailure);
        await AssertNoPlanWriteOrAuditAsync();
    }

    private PlanService CreateService(params Guid[] projectScope)
    {
        var accessor = new ManagementContextAccessor();
        accessor.Set(new ManagementContext(
            _userId,
            _tenantA,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), projectScope.ToHashSet())));

        return new PlanService(
            _db,
            _audit.Object,
            _http,
            managementContextAccessor: accessor,
            projectPublicIdLookup: _projectLookup.Object);
    }

    private PlanService CreateServiceWithoutManagementContext() =>
        new(
            _db,
            _audit.Object,
            _http,
            managementContextAccessor: new ManagementContextAccessor(),
            projectPublicIdLookup: _projectLookup.Object);

    private void SeedProject(Guid tenantId, Guid projectId, string code)
    {
        _http.HttpContext = null;
        using (TrustedTenantExecutionContext.Enter(tenantId))
        {
            _db.Projects.Add(new Project
            {
                Id = projectId,
                TenantId = tenantId,
                Name = code,
                Code = code,
                Description = "Plan service tenant-scope test",
                Status = "Active",
                Currency = "USD",
                CreatedBy = "plan-service-tenant-scope-test",
                RowVersion = new byte[8]
            });
            _db.SaveChanges();
        }
    }

    private static CreatePlanDto NewPlanRequest(Guid projectId) => new()
    {
        ProjectId = projectId,
        PlanType = PlanType.Execution,
        Name = "Execution Plan"
    };

    private static DefaultHttpContext RequestContext(Guid tenantId, Guid userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", tenantId.ToString("D")),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString("D"))
        ], "test"))
    };

    private async Task AssertNoPlanWriteOrAuditAsync()
    {
        Assert.Empty(await _db.Plans.IgnoreQueryFilters().AsNoTracking().ToListAsync());
        Assert.Empty(await _db.PlanVersions.IgnoreQueryFilters().AsNoTracking().ToListAsync());
        _audit.Verify(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose()
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        _db.Dispose();
        _connection.Dispose();
    }
}
