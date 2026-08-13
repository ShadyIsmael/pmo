using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain.Entities;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Focused EF/persistence tests for the initial (first) approved baseline workflow.
/// Uses the same in-memory SQLite setup as the other API integration tests, with the
/// shared tenant resolution so the global tenant filter stays active.
/// </summary>
public class ProjectInitialBaselineTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor;
    private readonly Mock<IAuditService> _auditService;
    private readonly ProjectBaselineService _service;

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public ProjectInitialBaselineTests()
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
        _auditService
            .Setup(a => a.LogAsync(It.IsAny<NEXUS.Modules.Audit.Domain.AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var snapshotService = new ProjectSnapshotService(_context, _auditService.Object, _accessor.Object);
        _service = new ProjectBaselineService(_context, _auditService.Object, _accessor.Object, snapshotService);
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

    private void SeedProject(Guid tenantId)
    {
        _context.Projects.Add(new Project
        {
            Id = _projectId,
            TenantId = tenantId,
            Name = "Project A",
            Code = "PA",
            Status = "Active",
            Currency = "GBP",
            CreatedBy = _userA.ToString(),
            RowVersion = new byte[8]
        });
        _context.SaveChanges();
    }

    private void SeedApprovedCurrentBaseline()
    {
        _context.ProjectBaselineVersions.Add(new ProjectBaselineVersion
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantA,
            ProjectId = new ProjectId(_projectId.ToString()),
            VersionNumber = new VersionNumber(1),
            Name = "Baseline v1",
            Status = Status.Approved,
            CreatedBy = _userA.ToString(),
            IsCurrent = true
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateInitialBaseline_CreatesApprovedCurrentVersionOne_AndCapturesSnapshot()
    {
        SetTenant(_tenantA, _userA);
        SeedProject(_tenantA);

        var result = await _service.CreateInitialBaselineAsync(_projectId, _userA.ToString());

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(1, dto.VersionNumber);
        Assert.Equal("Approved", dto.Status);
        Assert.True(dto.IsCurrent);
        Assert.Equal(_projectId, dto.ProjectId);
        Assert.Equal(_userA.ToString(), dto.CreatedBy);
        Assert.Equal(_userA.ToString(), dto.ApprovedBy);
        Assert.NotNull(dto.ApprovedAt);
        Assert.True(dto.CreatedAt > DateTime.MinValue);
        Assert.NotNull(dto.BaselineSnapshotId);

        var baseline = await _context.ProjectBaselineVersions.IgnoreQueryFilters()
            .SingleAsync(b => b.ProjectId == new ProjectId(_projectId.ToString()));
        Assert.Equal(Status.Approved, baseline.Status);
        Assert.True(baseline.IsCurrent);
        Assert.Equal(1, baseline.VersionNumber.Value);
        Assert.Equal(_tenantA, baseline.TenantId);

        var snapshot = await _context.ProjectSnapshots.IgnoreQueryFilters()
            .SingleAsync(s => s.Id == dto.BaselineSnapshotId);
        Assert.Equal("InitialBaseline", snapshot.SnapshotType);
        Assert.Equal("Initial Baseline v1", snapshot.SnapshotName);
        Assert.Equal(_projectId, snapshot.ProjectId);
        Assert.NotEmpty(snapshot.Data!);
    }

    [Fact]
    public async Task CreateInitialBaseline_RejectsDuplicate_WhenApprovedCurrentExists()
    {
        SetTenant(_tenantA, _userA);
        SeedProject(_tenantA);
        SeedApprovedCurrentBaseline();

        var result = await _service.CreateInitialBaselineAsync(_projectId, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
        Assert.Empty(_context.ProjectSnapshots.IgnoreQueryFilters());
        Assert.Equal(1, _context.ProjectBaselineVersions.IgnoreQueryFilters().Count());
    }

    [Fact]
    public async Task CreateInitialBaseline_UnknownProject_ReturnsNotFound()
    {
        SetTenant(_tenantA, _userA);

        var result = await _service.CreateInitialBaselineAsync(_projectId, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task CreateInitialBaseline_OtherTenant_CannotSeeProject()
    {
        SetTenant(_tenantA, _userA);
        SeedProject(_tenantA);

        SetTenant(_tenantB, _userA);

        var result = await _service.CreateInitialBaselineAsync(_projectId, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
        Assert.Empty(_context.ProjectBaselineVersions.IgnoreQueryFilters());
        Assert.Empty(_context.ProjectSnapshots.IgnoreQueryFilters());
    }

    [Fact]
    public async Task CreateInitialBaseline_MissingTenantClaim_ReturnsUnauthorized()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _userA.ToString())
            }, "mock"))
        };
        _accessor.Setup(x => x.HttpContext).Returns(httpContext);

        var result = await _service.CreateInitialBaselineAsync(_projectId, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("UNAUTHORIZED", result.Error!.Code, ignoreCase: true);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }
}
