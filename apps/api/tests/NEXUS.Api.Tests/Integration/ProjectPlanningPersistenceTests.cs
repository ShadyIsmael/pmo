using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Persistence;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Focused EF/persistence tests for the Project Planning module skeleton
/// (Phase 1). Uses the same in-memory SQLite setup and shared tenant resolution
/// as the other API integration tests.
/// </summary>
public class ProjectPlanningPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor;

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public ProjectPlanningPersistenceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        _accessor = new Mock<IHttpContextAccessor>();
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

    private Plan NewPlan()
    {
        return new Plan
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            ProjectId = _projectId,
            Name = "Project Plan",
            Status = PlanStatus.Draft
        };
    }

    private PlanVersion NewWorkingVersion(Plan plan, int versionNumber = 1)
    {
        return new PlanVersion
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanId = plan.Id,
            VersionNumber = versionNumber,
            Type = PlanVersionType.Working,
            Status = PlanVersionStatus.Active,
            Name = $"Working v{versionNumber}"
        };
    }

    private PlanActivity NewActivity(PlanVersion version, string code, string name)
    {
        return new PlanActivity
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanVersionId = version.Id,
            Code = code,
            Name = name,
            Type = PlanActivityType.Task,
            Duration = 5,
            EstimateHours = 40m,
            Progress = 0m
        };
    }

    [Fact]
    public async Task Save_RoundTrips_AllMappedProperties()
    {
        SetTenant(_tenantA, _userA);

        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        var version = NewWorkingVersion(plan);
        _context.PlanVersions.Add(version);
        await _context.SaveChangesAsync();

        var activity = NewActivity(version, "A-001", "Kick off");
        _context.PlanActivities.Add(activity);
        await _context.SaveChangesAsync();

        var loaded = await _context.PlanActivities
            .IgnoreQueryFilters()
            .SingleAsync(a => a.Id == activity.Id);

        Assert.Equal(activity.PlanVersionId, loaded.PlanVersionId);
        Assert.Equal("A-001", loaded.Code);
        Assert.Equal(PlanActivityType.Task, loaded.Type);
        Assert.Equal(5, loaded.Duration);
        Assert.Equal(40m, loaded.EstimateHours);
        Assert.Equal(0m, loaded.Progress);
        Assert.NotEmpty(loaded.RowVersion);
        Assert.Equal(_tenantA, loaded.TenantId);
        Assert.Equal(_userA.ToString(), loaded.CreatedBy);
        Assert.True(loaded.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task EveryPlanningRow_MustBelongToAPlanVersion()
    {
        SetTenant(_tenantA, _userA);

        // A plan-version-less activity must be rejected: PlanVersionId is required.
        var orphan = new PlanActivity
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            Code = "X-001",
            Name = "Orphan"
        };
        _context.PlanActivities.Add(orphan);

        var exception = await Record.ExceptionAsync(() => _context.SaveChangesAsync());

        Assert.NotNull(exception);
        Assert.IsType<DbUpdateException>(exception);
    }

    [Fact]
    public async Task Milestone_IsPersistedAsZeroDurationActivity()
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        var version = NewWorkingVersion(plan);
        _context.PlanVersions.Add(version);
        await _context.SaveChangesAsync();

        _context.PlanActivities.Add(new PlanActivity
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanVersionId = version.Id,
            Code = "M-001",
            Name = "Launch",
            Type = PlanActivityType.Milestone,
            Duration = 0
        });
        await _context.SaveChangesAsync();

        var milestone = await _context.PlanActivities.SingleAsync(a => a.Code == "M-001");
        Assert.Equal(PlanActivityType.Milestone, milestone.Type);
        Assert.Equal(0, milestone.Duration);
    }

    [Fact]
    public async Task Wbs_AdjacencyList_WithParent_RoundTrips()
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        var version = NewWorkingVersion(plan);
        _context.PlanVersions.Add(version);
        await _context.SaveChangesAsync();

        var root = new PlanWbsNode
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanVersionId = version.Id,
            Code = "1",
            Name = "Phase 1",
            SortOrder = 1
        };
        var child = new PlanWbsNode
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanVersionId = version.Id,
            ParentWbsId = root.Id,
            Code = "1.1",
            Name = "Work Package",
            SortOrder = 1
        };
        _context.PlanWbsNodes.AddRange(root, child);
        await _context.SaveChangesAsync();

        var loaded = await _context.PlanWbsNodes
            .IgnoreQueryFilters()
            .SingleAsync(n => n.Code == "1.1");

        Assert.Equal(root.Id, loaded.ParentWbsId);
        Assert.Equal(version.Id, loaded.PlanVersionId);
    }

    [Fact]
    public async Task Dependency_LinksActivitiesWithinSameVersion()
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        var version = NewWorkingVersion(plan);
        _context.PlanVersions.Add(version);
        await _context.SaveChangesAsync();

        var a = NewActivity(version, "A-001", "Task A");
        var b = NewActivity(version, "A-002", "Task B");
        _context.PlanActivities.AddRange(a, b);
        await _context.SaveChangesAsync();

        _context.PlanDependencies.Add(new PlanDependency
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanVersionId = version.Id,
            ActivityId = b.Id,
            PredecessorActivityId = a.Id,
            Type = PlanDependencyType.FinishToStart,
            Lag = 2
        });
        await _context.SaveChangesAsync();

        var dep = await _context.PlanDependencies.SingleAsync();
        Assert.Equal(a.Id, dep.PredecessorActivityId);
        Assert.Equal(b.Id, dep.ActivityId);
        Assert.Equal(PlanDependencyType.FinishToStart, dep.Type);
        Assert.Equal(2, dep.Lag);
    }

    [Fact]
    public async Task Deliverable_OptionalLinksToWbsAndActivity_RoundTrip()
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        var version = NewWorkingVersion(plan);
        _context.PlanVersions.Add(version);
        await _context.SaveChangesAsync();

        var activity = NewActivity(version, "A-001", "Build");
        var wbs = new PlanWbsNode
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanVersionId = version.Id,
            Code = "1",
            Name = "Build",
            SortOrder = 1
        };
        _context.PlanActivities.Add(activity);
        _context.PlanWbsNodes.Add(wbs);
        await _context.SaveChangesAsync();

        _context.PlanDeliverables.Add(new PlanDeliverable
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanVersionId = version.Id,
            Name = "Spec",
            AcceptanceCriteria = "Reviewed",
            WbsId = wbs.Id,
            ActivityId = activity.Id
        });
        await _context.SaveChangesAsync();

        var deliverable = await _context.PlanDeliverables.SingleAsync();
        Assert.Equal(wbs.Id, deliverable.WbsId);
        Assert.Equal(activity.Id, deliverable.ActivityId);
    }

    [Fact]
    public async Task Assignment_StoresPlanningAttributesOnly_WithResourceId()
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        var version = NewWorkingVersion(plan);
        _context.PlanVersions.Add(version);
        await _context.SaveChangesAsync();

        var activity = NewActivity(version, "A-001", "Build");
        _context.PlanActivities.Add(activity);
        await _context.SaveChangesAsync();

        var resourceId = Guid.NewGuid();
        _context.PlanAssignments.Add(new PlanAssignment
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanVersionId = version.Id,
            ActivityId = activity.Id,
            ResourceId = resourceId,
            AllocationPct = 50m,
            HoursPerWeek = 20m,
            StartDate = new DateTime(2026, 8, 10),
            EndDate = new DateTime(2026, 9, 10)
        });
        await _context.SaveChangesAsync();

        var assignment = await _context.PlanAssignments.SingleAsync();
        Assert.Equal(resourceId, assignment.ResourceId);
        Assert.Equal(50m, assignment.AllocationPct);
        Assert.Equal(20m, assignment.HoursPerWeek);
        Assert.NotNull(assignment.StartDate);
        Assert.NotNull(assignment.EndDate);
    }

    [Fact]
    public async Task TwoActiveWorkingVersions_SamePlan_FailUniqueConstraint()
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        _context.PlanVersions.Add(NewWorkingVersion(plan, versionNumber: 1));
        await _context.SaveChangesAsync();

        _context.PlanVersions.Add(NewWorkingVersion(plan, versionNumber: 2));
        var exception = await Record.ExceptionAsync(() => _context.SaveChangesAsync());

        Assert.NotNull(exception);
        Assert.IsType<DbUpdateException>(exception);
    }

    [Fact]
    public async Task InactiveWorking_SupersededBaseline_CanCoexist()
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        _context.PlanVersions.Add(new PlanVersion
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanId = plan.Id,
            VersionNumber = 1,
            Type = PlanVersionType.Working,
            Status = PlanVersionStatus.Active,
            Name = "Working v1"
        });
        _context.PlanVersions.Add(new PlanVersion
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanId = plan.Id,
            VersionNumber = 2,
            Type = PlanVersionType.Baseline,
            Status = PlanVersionStatus.Superseded,
            Name = "Baseline v1"
        });
        await _context.SaveChangesAsync();

        Assert.Equal(2, _context.PlanVersions.IgnoreQueryFilters().Count());
    }

    [Fact]
    public async Task SecondApprovedBaseline_SamePlan_RemainsImmutableHistory()
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        _context.PlanVersions.Add(new PlanVersion
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanId = plan.Id,
            VersionNumber = 1,
            Type = PlanVersionType.Baseline,
            Status = PlanVersionStatus.Approved,
            Name = "Baseline v1"
        });
        await _context.SaveChangesAsync();

        _context.PlanVersions.Add(new PlanVersion
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanId = plan.Id,
            VersionNumber = 2,
            Type = PlanVersionType.Baseline,
            Status = PlanVersionStatus.Approved,
            Name = "Baseline v2"
        });
        var exception = await Record.ExceptionAsync(() => _context.SaveChangesAsync());

        Assert.Null(exception);
        Assert.Equal(2, await _context.PlanVersions.CountAsync(value => value.PlanId == plan.Id && value.Type == PlanVersionType.Baseline && value.Status == PlanVersionStatus.Approved));
    }

    [Fact]
    public async Task TenantB_NeverReadsTenantA_PlanningRows()
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        var version = NewWorkingVersion(plan);
        _context.PlanVersions.Add(version);
        _context.PlanActivities.Add(NewActivity(version, "A-001", "Task A"));
        await _context.SaveChangesAsync();

        SetTenant(_tenantB, Guid.NewGuid());

        Assert.Empty(await _context.Plans.ToListAsync());
        Assert.Empty(await _context.PlanVersions.ToListAsync());
        Assert.Empty(await _context.PlanActivities.ToListAsync());
    }

    [Fact]
    public async Task SoftDeleteFlag_IsPersisted()
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        var loaded = await _context.Plans.SingleAsync();
        loaded.IsDeleted = true;
        await _context.SaveChangesAsync();

        var stored = await _context.Plans.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }
}
