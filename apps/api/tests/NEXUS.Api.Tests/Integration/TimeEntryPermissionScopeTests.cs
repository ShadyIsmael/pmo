using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.Api.Tests.Testing;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

[Collection("Static tenant resolution")]
public sealed class TimeEntryPermissionScopeTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _otherProjectId = Guid.NewGuid();
    private readonly Guid _ownResourceId = Guid.NewGuid();
    private readonly Guid _teamResourceId = Guid.NewGuid();
    private readonly Guid _otherTeamResourceId = Guid.NewGuid();
    private readonly Guid _ownEntryId = Guid.NewGuid();
    private readonly Guid _teamEntryId = Guid.NewGuid();
    private readonly Guid _otherTeamEntryId = Guid.NewGuid();

    public TimeEntryPermissionScopeTests()
    {
        _connection.Open();
        _db = new TimeEntrySqliteTestAppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        SetPermissions(TimeTrackingPermissions.ViewTeam);
    }

    [Fact]
    public async Task Team_view_does_not_reveal_own_entries_when_own_view_is_denied()
    {
        _db.Users.Add(new User
        {
            Id = _actorId,
            TenantId = _tenantId,
            Email = $"time-scope-{_actorId:N}@example.test",
            PasswordHash = "test-hash",
            FirstName = "Time",
            LastName = "Scope Actor",
            CreatedBy = "test"
        });
        _db.Projects.Add(new Project
        {
            Id = _projectId,
            TenantId = _tenantId,
            CreatedBy = _actorId.ToString(),
            Name = "Time scope test project",
            Code = "TIME-SCOPE-TEST",
            Status = "Active",
            Priority = "Medium"
        });
        _db.Projects.Add(new Project
        {
            Id = _otherProjectId,
            TenantId = _tenantId,
            CreatedBy = _actorId.ToString(),
            Name = "Other Organization project",
            Code = "TIME-SCOPE-OTHER",
            Status = "Active",
            Priority = "Medium"
        });
        _db.Resources.AddRange(
            new Resource
            {
                Id = _ownResourceId,
                TenantId = _tenantId,
                CreatedBy = _actorId.ToString(),
                UserId = _actorId,
                Name = "Actor resource",
                Type = "Internal",
                Status = "Active"
            },
            new Resource
            {
                Id = _teamResourceId,
                TenantId = _tenantId,
                CreatedBy = _actorId.ToString(),
                Name = "Team resource",
                Type = "Internal",
                Status = "Active"
            },
            new Resource
            {
                Id = _otherTeamResourceId,
                TenantId = _tenantId,
                CreatedBy = "test",
                Name = "Other Organization team resource",
                Type = "Internal",
                Status = "Active"
            });
        _db.TimeEntries.AddRange(
            NewEntry(_ownEntryId, _ownResourceId, "Own entry"),
            NewEntry(_teamEntryId, _teamResourceId, "Team entry"),
            NewEntry(_otherTeamEntryId, _otherTeamResourceId, "Other Organization team entry", _otherProjectId));
        await _db.SaveChangesAsync();

        var scopeResolver = new Mock<IManagementScopeResolver>();
        var projectScope = new ManagementScope(
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            new HashSet<Guid> { _projectId, _otherProjectId });
        scopeResolver.Setup(item => item.GetPerspectiveSnapshotAsync(
                _actorId, _tenantId, It.IsAny<ManagementContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ManagementPerspectiveSnapshot(
                Array.Empty<ManagementPerspective>(),
                Array.Empty<ManagementPerspectiveAvailability>(),
                null,
                false,
                projectScope,
                null));
        scopeResolver.Setup(item => item.CanAccessProjectAsync(
                _actorId, _tenantId, _projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var requestScope = new ManagementScope(
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            new HashSet<Guid> { _projectId });
        var managementContext = new Mock<IManagementContextAccessor>();
        managementContext.Setup(accessor => accessor.Current).Returns(new ManagementContext(
            _actorId, _tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null, requestScope));

        var service = new TimeEntryService(
            _db,
            Mock.Of<IResourceReferenceLookup>(),
            Mock.Of<IProjectResourcePlanningContextLookup>(),
            Mock.Of<IWorkItemReferenceLookup>(),
            Mock.Of<IWorkItemAssigneeLookup>(),
            scopeResolver.Object,
            managementContext.Object,
            Mock.Of<IAuditService>(),
            _http.Object);

        var ownEntry = await service.GetByIdAsync(_ownEntryId, _actorId, _tenantId);
        Assert.False(ownEntry.IsSuccess);
        Assert.Equal(403, ownEntry.Error?.StatusCode);

        var visibleTeamEntry = await service.GetByIdAsync(_teamEntryId, _actorId, _tenantId);
        Assert.True(visibleTeamEntry.IsSuccess, visibleTeamEntry.Error?.Message);

        var list = await service.ListAsync(new TimeEntryFilterDto(), _actorId, _tenantId);
        Assert.True(list.IsSuccess, list.Error?.Message);
        Assert.Single(list.Value!.Items);
        Assert.Equal(_teamEntryId, list.Value.Items[0].Id);
        Assert.DoesNotContain(list.Value.Items, item => item.Id == _otherTeamEntryId);

        var ownResourceList = await service.ListAsync(new TimeEntryFilterDto { ResourceId = _ownResourceId }, _actorId, _tenantId);
        Assert.False(ownResourceList.IsSuccess);
        Assert.Equal(403, ownResourceList.Error?.StatusCode);

        var outsideProjectList = await service.ListAsync(new TimeEntryFilterDto { ProjectId = Guid.NewGuid() }, _actorId, _tenantId);
        Assert.False(outsideProjectList.IsSuccess);
        Assert.Equal(403, outsideProjectList.Error?.StatusCode);

        var ownSummary = await service.GetSummaryAsync(new TimeEntryFilterDto { ResourceId = _ownResourceId }, _actorId, _tenantId);
        Assert.False(ownSummary.IsSuccess);
        Assert.Equal(403, ownSummary.Error?.StatusCode);
    }

    private TimeEntry NewEntry(Guid id, Guid resourceId, string description, Guid? projectId = null) => new()
    {
        Id = id,
        TenantId = _tenantId,
        CreatedBy = _actorId.ToString(),
        ResourceId = resourceId,
        ProjectId = projectId ?? _projectId,
        WorkDate = DateTime.UtcNow.Date,
        StartTime = TimeSpan.FromHours(9),
        EndTime = TimeSpan.FromHours(10),
        ElapsedSeconds = 3600,
        ActualSeconds = 3600,
        Description = description
    };

    private void SetPermissions(params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("tenantId", _tenantId.ToString()),
            new(ClaimTypes.NameIdentifier, _actorId.ToString())
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Time tracking scope test"))
        };
        _http.Setup(item => item.HttpContext).Returns(context);
        AppDbContext.ConfigureTenantResolution(_http.Object);
    }

    public void Dispose()
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class TimeEntrySqliteTestAppDbContext(DbContextOptions<AppDbContext> options)
        : SqliteTestAppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TimeEntry>(entity =>
            {
                entity.Property(item => item.StartTime)
                    .HasConversion(new TimeSpanToTicksConverter());
                entity.Property(item => item.EndTime)
                    .HasConversion(new TimeSpanToTicksConverter());
            });
        }
    }
}
