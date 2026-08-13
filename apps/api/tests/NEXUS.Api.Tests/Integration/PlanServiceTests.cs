using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Persistence;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Focused service/integration tests for the Project Planning Plan Management
/// foundation (create/list/get/get-active-working-version). Uses the same
/// in-memory SQLite setup and shared tenant resolution as the other API
/// integration tests, so the global tenant filter stays active.
/// </summary>
public class PlanServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor;
    private readonly Mock<IAuditService> _auditService;
    private readonly PlanService _service;

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public PlanServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        _accessor = new Mock<IHttpContextAccessor>();
        _auditService = new Mock<IAuditService>();
        _service = new PlanService(_context, _auditService.Object, _accessor.Object);
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

    private CreatePlanDto NewDto(PlanType type, string? name = null)
    {
        return new CreatePlanDto
        {
            ProjectId = _projectId,
            PlanType = type,
            Name = name ?? "Project Plan"
        };
    }

    [Fact]
    public async Task Create_WithValidData_CreatesPlanAndInitialWorkingVersion()
    {
        SetTenant(_tenantA, _userA);

        var result = await _service.CreateAsync(NewDto(PlanType.Execution), _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PlanType.Execution, result.Value.PlanType);
        Assert.Equal(_tenantA, result.Value.TenantId);
        Assert.Single(result.Value.Versions);
        var version = result.Value.Versions[0];
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(PlanVersionType.Working, version.Type);
        Assert.Equal(PlanVersionStatus.Active, version.Status);

        Assert.Single(_context.PlanVersions.IgnoreQueryFilters());
        _auditService.Verify(a => a.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WithoutPlanType_ReturnsValidationError()
    {
        SetTenant(_tenantA, _userA);

        var result = await _service.CreateAsync(new CreatePlanDto
        {
            ProjectId = _projectId,
            Name = "No type"
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
        Assert.Empty(_context.Plans.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Create_WithoutName_ReturnsValidationError()
    {
        SetTenant(_tenantA, _userA);

        var result = await _service.CreateAsync(NewDto(PlanType.Execution, "   "), _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_DuplicatePlanType_SameProject_ReturnsConflict()
    {
        SetTenant(_tenantA, _userA);
        await _service.CreateAsync(NewDto(PlanType.Execution), _userA.ToString());

        var result = await _service.CreateAsync(NewDto(PlanType.Execution), _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
        Assert.Single(_context.Plans.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Create_DifferentPlanTypes_SameProject_Succeeds()
    {
        SetTenant(_tenantA, _userA);
        await _service.CreateAsync(NewDto(PlanType.Execution), _userA.ToString());

        var result = await _service.CreateAsync(NewDto(PlanType.Recovery), _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanType.Recovery, result.Value!.PlanType);
        Assert.Equal(2, _context.Plans.IgnoreQueryFilters().Count());
    }

    [Fact]
    public async Task GetByProject_ReturnsOnlyTenantVisiblePlans()
    {
        SetTenant(_tenantA, _userA);
        await _service.CreateAsync(NewDto(PlanType.Execution), _userA.ToString());
        await _service.CreateAsync(NewDto(PlanType.Tender, "Tender Plan"), _userA.ToString());

        SetTenant(_tenantB, Guid.NewGuid());

        var result = await _service.GetByProjectAsync(_projectId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetByProject_ReturnsAllPlanTypes_WithVersionCount()
    {
        SetTenant(_tenantA, _userA);
        await _service.CreateAsync(NewDto(PlanType.Execution), _userA.ToString());
        await _service.CreateAsync(NewDto(PlanType.Construction, "Construction Plan"), _userA.ToString());

        var result = await _service.GetByProjectAsync(_projectId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value, p => Assert.Equal(1, p.VersionCount));
        Assert.Contains(result.Value, p => p.PlanType == PlanType.Execution);
        Assert.Contains(result.Value, p => p.PlanType == PlanType.Construction);
    }

    [Fact]
    public async Task GetById_ReturnsPlanWithVersions()
    {
        SetTenant(_tenantA, _userA);
        var created = await _service.CreateAsync(NewDto(PlanType.Execution), _userA.ToString());

        var result = await _service.GetByIdAsync(created.Value!.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(created.Value.Id, result.Value!.Id);
        Assert.Single(result.Value.Versions);
    }

    [Fact]
    public async Task GetById_UnknownPlan_ReturnsNotFound()
    {
        SetTenant(_tenantA, _userA);

        var result = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task GetActiveWorkingVersion_ReturnsWorkingVersion()
    {
        SetTenant(_tenantA, _userA);
        var created = await _service.CreateAsync(NewDto(PlanType.Execution), _userA.ToString());

        var result = await _service.GetActiveWorkingVersionAsync(created.Value!.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanVersionType.Working, result.Value!.Type);
        Assert.Equal(PlanVersionStatus.Active, result.Value.Status);
    }

    [Fact]
    public async Task GetActiveWorkingVersion_TenantB_CannotSeeTenantAPlan()
    {
        SetTenant(_tenantA, _userA);
        var created = await _service.CreateAsync(NewDto(PlanType.Execution), _userA.ToString());

        SetTenant(_tenantB, Guid.NewGuid());

        var result = await _service.GetActiveWorkingVersionAsync(created.Value!.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task GetActiveWorkingVersion_UnknownPlan_ReturnsNotFound()
    {
        SetTenant(_tenantA, _userA);

        var result = await _service.GetActiveWorkingVersionAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }
}
