using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Api.Tests.Testing;
using NEXUS.Persistence;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

/// <summary>
/// Focused EF/persistence tests for the Project Planning module skeleton
/// (Phase 1). Uses the provider-adapted in-memory SQLite setup and shared tenant
/// resolution used by the tenant-isolation integration tests.
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
        _context = new SqliteTestAppDbContext(options);
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
    public async Task SchedulingAnalysis_UsesPersistedWorkingVersionAndReturnsDrivingDependencies()
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();
        var version = NewWorkingVersion(plan);
        _context.PlanVersions.Add(version);
        await _context.SaveChangesAsync();

        var monday = new DateTime(2026, 1, 5, 8, 0, 0);
        var tuesday = new DateTime(2026, 1, 6, 8, 0, 0);
        var wednesday = new DateTime(2026, 1, 7, 8, 0, 0);
        var first = NewActivity(version, "A", "First task");
        first.Duration = 1;
        first.StartDate = monday;
        first.EndDate = monday.AddHours(9);
        var second = NewActivity(version, "B", "Second task");
        second.Duration = 1;
        second.StartDate = tuesday;
        second.EndDate = tuesday.AddHours(9);
        var milestone = NewActivity(version, "M", "Go live");
        milestone.Type = PlanActivityType.Milestone;
        milestone.Duration = 0;
        milestone.StartDate = wednesday;
        milestone.EndDate = wednesday;
        _context.PlanActivities.AddRange(first, second, milestone);
        await _context.SaveChangesAsync();

        var firstToSecond = new PlanDependency
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanVersionId = version.Id,
            PredecessorActivityId = first.Id,
            ActivityId = second.Id,
            Type = PlanDependencyType.FinishToStart
        };
        var secondToMilestone = new PlanDependency
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanVersionId = version.Id,
            PredecessorActivityId = second.Id,
            ActivityId = milestone.Id,
            Type = PlanDependencyType.FinishToStart
        };
        var nonDrivingShortcut = new PlanDependency
        {
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            PlanVersionId = version.Id,
            PredecessorActivityId = first.Id,
            ActivityId = milestone.Id,
            Type = PlanDependencyType.FinishToStart
        };
        _context.PlanDependencies.AddRange(firstToSecond, secondToMilestone, nonDrivingShortcut);
        await _context.SaveChangesAsync();

        var service = new PlanSchedulingAnalysisService(_context, new CriticalPathAnalyzer());
        var activeVersion = await service.GetActiveWorkingVersionIdAsync(_projectId, plan.Id);
        var analysis = await service.AnalyzeAsync(activeVersion.Value);
        var mismatchedProject = await service.GetActiveWorkingVersionIdAsync(Guid.NewGuid(), plan.Id);

        Assert.True(activeVersion.IsSuccess);
        Assert.True(mismatchedProject.IsFailure);
        Assert.Equal(404, mismatchedProject.Error?.StatusCode);
        Assert.True(analysis.IsSuccess);
        Assert.False(analysis.Value!.HasCycle);
        Assert.Equal(new[] { first.Id, second.Id, milestone.Id }, analysis.Value.CriticalPathActivityIds);
        Assert.Equal(new[] { firstToSecond.Id, secondToMilestone.Id }.Order(), analysis.Value.CriticalPathDependencyIds.Order());
        Assert.DoesNotContain(nonDrivingShortcut.Id, analysis.Value.CriticalPathDependencyIds);
        Assert.Equal(monday, analysis.Value.ActivityMetrics[first.Id].EarlyStart);
        Assert.Equal(monday.AddHours(9), analysis.Value.ActivityMetrics[first.Id].LateFinish);
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

        var progress = await new ProjectPlanProgressLookup(_context).GetManyAsync([_projectId]);
        Assert.Null(progress[_projectId]);
    }

    [Fact]
    public async Task ProjectRegistrySummary_UsesActiveWorkingPlanProgressSeparatelyFromProjectRecordProgress()
    {
        SetTenant(_tenantA, _userA);
        _context.Projects.Add(new Project
        {
            Id = _projectId,
            TenantId = _tenantA,
            CreatedBy = _userA.ToString(),
            Name = "Registry progress project",
            Code = "PRJ-PROGRESS",
            Progress = 78m
        });
        var plan = NewPlan();
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();

        var workingVersion = NewWorkingVersion(plan);
        _context.PlanVersions.Add(workingVersion);
        var firstActivity = NewActivity(workingVersion, "A-001", "First task");
        firstActivity.Progress = 30m;
        var secondActivity = NewActivity(workingVersion, "A-002", "Second task");
        secondActivity.Progress = 70m;
        _context.PlanActivities.AddRange(
            firstActivity,
            secondActivity);
        await _context.SaveChangesAsync();

        var service = new ProjectService(
            _context,
            new Mock<IAuditService>().Object,
            _accessor.Object,
            new ProjectManagerMembershipSynchronizer(_context),
            planProgressLookup: new ProjectPlanProgressLookup(_context));

        var result = await service.GetPagedAsync(1, 10);
        var summary = Assert.Single(result.Items);

        Assert.Equal(78m, summary.Progress);
        Assert.Equal(50m, summary.PlanProgress!.Value);
    }

    [Fact]
    public async Task ProjectAllocationConflictCheck_DoesNotAddNonOverlappingMembershipsAcrossTheWholeRange()
    {
        SetTenant(_tenantA, _userA);
        var resourceId = Guid.NewGuid();
        var firstProject = new Project { Id = Guid.NewGuid(), TenantId = _tenantA, CreatedBy = _userA.ToString(), Name = "First project", Code = "PRJ-FIRST" };
        var secondProject = new Project { Id = Guid.NewGuid(), TenantId = _tenantA, CreatedBy = _userA.ToString(), Name = "Second project", Code = "PRJ-SECOND" };
        _context.Projects.AddRange(firstProject, secondProject);
        _context.Resources.Add(new Resource { Id = resourceId, TenantId = _tenantA, CreatedBy = _userA.ToString(), Name = "Shared resource", Type = "Internal", Status = "Active" });
        _context.ProjectMembers.AddRange(
            new ProjectMember
            {
                TenantId = _tenantA, CreatedBy = _userA.ToString(), ProjectId = firstProject.Id, ResourceId = resourceId,
                Role = "Team Member", AllocationPercentage = 40m, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 7), Status = "Active"
            },
            new ProjectMember
            {
                TenantId = _tenantA, CreatedBy = _userA.ToString(), ProjectId = secondProject.Id, ResourceId = resourceId,
                Role = "Team Member", AllocationPercentage = 40m, StartDate = new DateTime(2026, 1, 8), EndDate = new DateTime(2026, 1, 14), Status = "Active"
            });
        await _context.SaveChangesAsync();

        var service = new ProjectService(
            _context, new Mock<IAuditService>().Object, _accessor.Object, new ProjectManagerMembershipSynchronizer(_context));
        var result = await service.CheckResourceConflictsAsync(
            resourceId, new DateTime(2026, 1, 1), new DateTime(2026, 1, 14), 30m, null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ProjectAllocationConflictCheck_TreatsMembershipEndDateAsInclusive()
    {
        SetTenant(_tenantA, _userA);
        var resourceId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), TenantId = _tenantA, CreatedBy = _userA.ToString(), Name = "Existing project", Code = "PRJ-EXISTING" };
        _context.Projects.Add(project);
        _context.Resources.Add(new Resource { Id = resourceId, TenantId = _tenantA, CreatedBy = _userA.ToString(), Name = "Shared resource", Type = "Internal", Status = "Active" });
        var member = new ProjectMember
        {
            TenantId = _tenantA, CreatedBy = _userA.ToString(), ProjectId = project.Id, ResourceId = resourceId,
            Role = "Team Member", AllocationPercentage = 60m, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 7), Status = "Active"
        };
        _context.ProjectMembers.Add(member);
        await _context.SaveChangesAsync();

        var service = new ProjectService(
            _context, new Mock<IAuditService>().Object, _accessor.Object, new ProjectManagerMembershipSynchronizer(_context));
        var result = await service.CheckResourceConflictsAsync(
            resourceId, new DateTime(2026, 1, 7), new DateTime(2026, 1, 14), 50m, null);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(member.Id, result.Value![0].AllocationId);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Summary_IndependentOverlappingChildren_PersistWorkingSpan(bool nested)
    {
        SetTenant(_tenantA, _userA);
        var plan = NewPlan();
        var version = NewWorkingVersion(plan);
        _context.Plans.Add(plan);
        _context.PlanVersions.Add(version);
        var root = new PlanWbsNode { TenantId = _tenantA, PlanVersionId = version.Id, Code = "1", Name = "Analysis", CreatedBy = "test" };
        var summary = NewActivity(version, "S", "Analysis");
        summary.WbsId = root.Id;
        summary.SchedulingMode = PlanSchedulingMode.Manual;
        summary.StartDate = new DateTime(2026, 11, 4);
        _context.PlanWbsNodes.Add(root);
        _context.PlanActivities.Add(summary);
        var parent = root;
        PlanActivity? nestedSummary = null;
        if (nested)
        {
            parent = new PlanWbsNode { TenantId = _tenantA, PlanVersionId = version.Id, ParentWbsId = root.Id, Code = "1.1", Name = "Subphase", CreatedBy = "test" };
            nestedSummary = NewActivity(version, "SS", "Subphase");
            nestedSummary.WbsId = parent.Id;
            _context.PlanWbsNodes.Add(parent);
            _context.PlanActivities.Add(nestedSummary);
        }
        foreach (var (code, start, finish) in new[] { ("A", 2, 6), ("B", 5, 11), ("C", 9, 13) })
        {
            var node = new PlanWbsNode { TenantId = _tenantA, PlanVersionId = version.Id, ParentWbsId = parent.Id, Code = code, Name = code, CreatedBy = "test" };
            var child = NewActivity(version, code, code);
            child.WbsId = node.Id;
            child.StartDate = new DateTime(2026, 11, start, 8, 0, 0);
            child.EndDate = new DateTime(2026, 11, finish, 17, 0, 0);
            _context.PlanWbsNodes.Add(node);
            _context.PlanActivities.Add(child);
        }
        await _context.SaveChangesAsync();
        var ids = await new SummaryTaskRollupService(_context).RecalculateAsync(version.Id, "test");
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var stored = await _context.PlanActivities.SingleAsync(a => a.Id == summary.Id);
        Assert.Contains(summary.Id, ids);
        Assert.Equal(new DateTime(2026, 11, 2, 8, 0, 0), stored.StartDate);
        Assert.Equal(new DateTime(2026, 11, 13, 17, 0, 0), stored.EndDate);
        Assert.Equal(10, stored.Duration);
        Assert.Equal(PlanSchedulingMode.Manual, stored.SchedulingMode);
        Assert.Empty(await _context.PlanDependencies.ToListAsync());
        if (nestedSummary is not null) Assert.Contains(nestedSummary.Id, ids);
    }
    /// <summary>
    /// D-01 regression: a Project Team membership created through the API path must
    /// carry the owning project's tenant, otherwise the global tenant filter hides it
    /// from team reads, scope resolution, and allocation-conflict checks.
    /// </summary>
    [Fact]
    public async Task AddTeamMemberAsync_PersistsProjectTenantIdAndIsVisibleToTeamReads()
    {
        SetTenant(_tenantA, _userA);
        var project = new Project { Id = Guid.NewGuid(), TenantId = _tenantA, CreatedBy = _userA.ToString(), Name = "Team project", Code = "PRJ-TEAM" };
        var resource = new Resource
        {
            Id = Guid.NewGuid(), TenantId = _tenantA, CreatedBy = _userA.ToString(),
            Name = "New team member", Type = "Internal", Status = "Active", IsActive = true
        };
        _context.Projects.Add(project);
        _context.Resources.Add(resource);
        await _context.SaveChangesAsync();

        var service = new ProjectService(
            _context, new Mock<IAuditService>().Object, _accessor.Object, new ProjectManagerMembershipSynchronizer(_context));
        var result = await service.AddTeamMemberAsync(project.Id, new NEXUS.Modules.ProjectManagement.Application.DTOs.CreateProjectMemberDto
        {
            ProjectId = project.Id,
            ResourceId = resource.Id,
            Role = "Team Member",
            AllocationPercentage = 50m,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            Status = "Active"
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        var stored = await _context.Set<ProjectMember>().AsNoTracking().SingleAsync(m => m.Id == result.Value!.Id);
        Assert.Equal(_tenantA, stored.TenantId);

        var team = await service.GetTeamAsync(project.Id);
        Assert.True(team.IsSuccess);
        Assert.Single(team.Value!);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }
}
