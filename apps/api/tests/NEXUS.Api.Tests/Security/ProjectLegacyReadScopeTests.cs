using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class ProjectLegacyReadScopeTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly AppDbContext _context;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _visibleProjectId = Guid.NewGuid();
    private readonly Guid _hiddenProjectId = Guid.NewGuid();
    private readonly Guid _visibleBaselineId = Guid.NewGuid();
    private readonly Guid _hiddenBaselineId = Guid.NewGuid();
    private readonly Guid _visibleMilestoneId = Guid.NewGuid();
    private readonly Guid _hiddenMilestoneId = Guid.NewGuid();
    private readonly Guid _visibleSnapshotId = Guid.NewGuid();
    private readonly Guid _hiddenSnapshotId = Guid.NewGuid();

    public ProjectLegacyReadScopeTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        SetActor();
        Seed();
    }

    [Fact]
    public async Task LegacyBaselineAndMilestoneReadsRespectManagementProjectScope()
    {
        var management = new Mock<IManagementContextAccessor>();
        management.SetupGet(accessor => accessor.Current).Returns(new ManagementContext(
            _actorId,
            _tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { _visibleProjectId })));
        var baselines = new BaselineService(_context, management.Object);
        var milestones = new MilestoneService(_context, management.Object);

        var visibleBaseline = await baselines.GetByIdAsync(_visibleBaselineId);
        var hiddenBaseline = await baselines.GetByIdAsync(_hiddenBaselineId);
        var visibleBaselines = await baselines.GetByProjectAsync(_visibleProjectId, 1, 20);
        var hiddenBaselines = await baselines.GetByProjectAsync(_hiddenProjectId, 1, 20);
        var visibleMilestone = await milestones.GetByIdAsync(_visibleMilestoneId);
        var hiddenMilestone = await milestones.GetByIdAsync(_hiddenMilestoneId);
        var visibleMilestones = await milestones.GetByProjectAsync(_visibleProjectId, 1, 20);
        var hiddenMilestones = await milestones.GetByProjectAsync(_hiddenProjectId, 1, 20);
        var snapshots = new ProjectSnapshotService(_context, Mock.Of<IAuditService>(), _http.Object,
            new ProjectManagerMembershipSynchronizer(_context), managementContext: management.Object);
        var visibleSnapshots = await snapshots.GetSnapshotsAsync(_visibleProjectId);
        var hiddenSnapshots = await snapshots.GetSnapshotsAsync(_hiddenProjectId);
        var visibleSnapshot = await snapshots.GetSnapshotAsync(_visibleProjectId, _visibleSnapshotId);
        var hiddenSnapshot = await snapshots.GetSnapshotAsync(_hiddenProjectId, _hiddenSnapshotId);

        Assert.True(visibleBaseline.IsSuccess);
        Assert.Equal(404, hiddenBaseline.Error?.StatusCode);
        Assert.Single(visibleBaselines.Items);
        Assert.Empty(hiddenBaselines.Items);
        Assert.True(visibleMilestone.IsSuccess);
        Assert.Equal(404, hiddenMilestone.Error?.StatusCode);
        Assert.Single(visibleMilestones.Items);
        Assert.Empty(hiddenMilestones.Items);
        Assert.True(visibleSnapshots.IsSuccess);
        Assert.Single(visibleSnapshots.Value!);
        Assert.Equal(404, hiddenSnapshots.Error?.StatusCode);
        Assert.True(visibleSnapshot.IsSuccess);
        Assert.Equal(_visibleSnapshotId, visibleSnapshot.Value!.Id);
        Assert.Equal(404, hiddenSnapshot.Error?.StatusCode);
    }

    private void Seed()
    {
        _context.Projects.AddRange(
            new Project { Id = _visibleProjectId, TenantId = _tenantId, Name = "Visible", Code = "VISIBLE", Currency = "USD", CreatedBy = _actorId.ToString(), RowVersion = new byte[8] },
            new Project { Id = _hiddenProjectId, TenantId = _tenantId, Name = "Hidden", Code = "HIDDEN", Currency = "USD", CreatedBy = _actorId.ToString(), RowVersion = new byte[8] });
        _context.Set<Baseline>().AddRange(
            new Baseline { Id = _visibleBaselineId, TenantId = _tenantId, ProjectId = _visibleProjectId, Name = "Visible baseline", Version = 1, RowVersion = new byte[8] },
            new Baseline { Id = _hiddenBaselineId, TenantId = _tenantId, ProjectId = _hiddenProjectId, Name = "Hidden baseline", Version = 1, RowVersion = new byte[8] });
        _context.Set<Milestone>().AddRange(
            new Milestone { Id = _visibleMilestoneId, TenantId = _tenantId, ProjectId = _visibleProjectId, Name = "Visible milestone", RowVersion = new byte[8] },
            new Milestone { Id = _hiddenMilestoneId, TenantId = _tenantId, ProjectId = _hiddenProjectId, Name = "Hidden milestone", RowVersion = new byte[8] });
        _context.Set<ProjectSnapshot>().AddRange(
            new ProjectSnapshot { Id = _visibleSnapshotId, TenantId = _tenantId, ProjectId = _visibleProjectId, SnapshotNumber = 1, SnapshotName = "Visible snapshot", SnapshotDate = DateTime.UtcNow, EffectiveEndDate = DateTime.UtcNow, CreatedBy = _actorId.ToString() },
            new ProjectSnapshot { Id = _hiddenSnapshotId, TenantId = _tenantId, ProjectId = _hiddenProjectId, SnapshotNumber = 1, SnapshotName = "Hidden snapshot", SnapshotDate = DateTime.UtcNow, EffectiveEndDate = DateTime.UtcNow, CreatedBy = _actorId.ToString() });
        _context.SaveChanges();
    }

    private void SetActor()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenantId", _tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, _actorId.ToString())
        }, "scope-test"));
        _http.Setup(accessor => accessor.HttpContext).Returns(httpContext);
        AppDbContext.ConfigureTenantResolution(_http.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
