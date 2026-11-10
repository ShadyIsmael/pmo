using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Application.Import;
using NEXUS.Modules.ProjectPlanning.Application.Mapping;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using System.Security.Claims;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class PlanningImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Mock<IHttpContextAccessor> _accessor = new();
    private readonly DefaultHttpContext _httpContext = new();
    private readonly Mock<IResourceService> _resources = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly PlanningWorkflowTestDbContext _context;

    public PlanningImportServiceTests()
    {
        _connection.Open();
        SetPermissions("Planning.View", "Planning.Edit", "ProjectPlan.Create");
        _accessor.Setup(value => value.HttpContext).Returns(_httpContext);
        _context = new PlanningWorkflowTestDbContext(
            new DbContextOptionsBuilder<PlanningWorkflowTestDbContext>().UseSqlite(_connection).Options,
            _accessor.Object);
        _context.Database.EnsureCreated();
        _audit.Setup(value => value.LogAsync(It.IsAny<Modules.Audit.Domain.AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    [Fact]
    public async Task ImportNewPlan_MapsHierarchyDependencyResourceAndCalendarToWorkingVersion()
    {
        var resourceId = Guid.NewGuid();
        SetResources(new ResourceDto { Id = resourceId, Name = "Developer", Email = "dev@example.com" });
        var service = CreateService(FixtureDocument());

        var result = await service.ImportAsync(new MemoryStream([1]), "fixture.mpp", new CommitPlanImportDto
        {
            ProjectId = _projectId,
            NewPlanName = "Imported execution",
            NewPlanType = PlanType.Execution
        }, _userId.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        _context.ChangeTracker.Clear();
        var version = await _context.PlanVersions
            .Include(value => value.WbsNodes)
            .Include(value => value.Activities)
            .Include(value => value.Dependencies)
            .Include(value => value.Assignments)
            .Include(value => value.Calendars).ThenInclude(value => value.DayRules)
            .SingleAsync();
        Assert.Equal(PlanVersionType.Working, version.Type);
        Assert.Equal(PlanVersionStatus.Active, version.Status);
        Assert.Equal(2, version.Activities.Count);
        Assert.Equal(new DateTime(2026, 1, 6), version.Activities.Single(value => value.Code == "1").ActualStart);
        Assert.Equal(new DateTime(2026, 1, 9), version.Activities.Single(value => value.Code == "1").ActualFinish);
        var root = version.WbsNodes.Single(value => value.Code == "1");
        Assert.Equal(root.Id, version.WbsNodes.Single(value => value.Code == "1.1").ParentWbsId);
        Assert.All(version.WbsNodes, node =>
        {
            Assert.NotNull(node.TextFormat);
            Assert.False(node.TextFormat!.IsSpecified);
            Assert.Null(node.ToDto().TextFormat);
        });
        Assert.All(version.Activities, activity =>
        {
            Assert.NotNull(activity.TextFormat);
            Assert.False(activity.TextFormat!.IsSpecified);
            Assert.Null(activity.ToDto().TextFormat);
        });
        var dependency = Assert.Single(version.Dependencies);
        Assert.Equal(PlanDependencyType.StartToFinish, dependency.Type);
        Assert.Equal(-2, dependency.Lag);
        Assert.Equal(resourceId, Assert.Single(version.Assignments).ResourceId);
        Assert.Equal(2, Assert.Single(version.Calendars).DayRules.Count);
    }

    [Fact]
    public async Task ImportExistingPlan_ReplacesWorkingAndPreservesApprovedBaseline()
    {
        SetPermissions("Planning.View", "Planning.Edit");
        SetResources();
        var plan = new Plan { TenantId = _tenantId, ProjectId = _projectId, PlanType = PlanType.Execution, Name = "Execution", CreatedBy = _userId.ToString() };
        var baseline = new PlanVersion { TenantId = _tenantId, PlanId = plan.Id, VersionNumber = 1, Type = PlanVersionType.Baseline, Status = PlanVersionStatus.Approved, Name = "Approved", CreatedBy = _userId.ToString() };
        var working = new PlanVersion { TenantId = _tenantId, PlanId = plan.Id, VersionNumber = 1, Type = PlanVersionType.Working, Status = PlanVersionStatus.Active, Name = "Old working", CreatedBy = _userId.ToString() };
        _context.AddRange(plan, baseline, working);
        await _context.SaveChangesAsync();

        var result = await CreateService(FixtureDocument()).ImportAsync(new MemoryStream([1]), "fixture.mpp", new CommitPlanImportDto
        {
            ProjectId = _projectId,
            TargetPlanId = plan.Id
        }, _userId.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(await _context.PlanVersions.IgnoreQueryFilters().Where(value => value.Id == baseline.Id).Select(value => value.IsDeleted).SingleAsync());
        Assert.True(await _context.PlanVersions.IgnoreQueryFilters().Where(value => value.Id == working.Id).Select(value => value.IsDeleted).SingleAsync());
        var replacement = await _context.PlanVersions.SingleAsync(value => value.Type == PlanVersionType.Working && value.Status == PlanVersionStatus.Active);
        Assert.Equal(2, replacement.VersionNumber);
        Assert.Equal(baseline.Id, replacement.SourceBaselineId);
    }

    [Theory]
    [InlineData("Planning.View", "Planning.Edit")]
    [InlineData("Planning.View", "ProjectPlan.Create")]
    public async Task ImportNewPlan_RequiresBothCreateAndEditPermissions(string permissionA, string permissionB)
    {
        SetPermissions(permissionA, permissionB);
        SetResources();

        var result = await CreateService(FixtureDocument()).ImportAsync(new MemoryStream([1]), "fixture.mpp", new CommitPlanImportDto
        {
            ProjectId = _projectId,
            NewPlanName = "Must not be created",
            NewPlanType = PlanType.Execution
        }, _userId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
        Assert.Empty(await _context.Plans.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task CreateSession_RequiresPlanningEditBeforeParsingOrPersisting()
    {
        SetPermissions("Planning.View");
        var service = CreateService(FixtureDocument());

        var result = await service.CreateSessionAsync(new MemoryStream([1]), "fixture.mpp", _projectId, null, _userId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
        Assert.Empty(await _context.Set<PlanImportSession>().IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task CommitSession_RechecksResourceCreateAfterResourceResolution()
    {
        SetPermissions("Planning.View", "Planning.Edit", "Resource.Create");
        SetResources();
        var service = CreateService(FixtureDocument());
        var session = await service.CreateSessionAsync(new MemoryStream([1]), "fixture.mpp", _projectId, null, _userId.ToString());
        Assert.True(session.IsSuccess, session.Error?.Message);

        var resolution = await service.ResolveResourceAsync(_projectId, session.Value!.Id, "r1", new PlanImportResourceResolutionDto
        {
            Resolution = "CreateNew",
            CreateResource = new CreateResourceDto { Name = "Imported Developer", Type = ResourceType.Staff }
        }, _userId.ToString());
        Assert.True(resolution.IsSuccess, resolution.Error?.Message);

        SetPermissions("Planning.View", "Planning.Edit");
        var result = await service.CommitSessionAsync(_projectId, session.Value.Id, new CommitPlanImportDto
        {
            ProjectId = _projectId,
            TargetPlanId = Guid.NewGuid()
        }, _userId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error!.StatusCode);
        _resources.Verify(resource => resource.CreateAsync(It.IsAny<CreateResourceDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(await _context.Plans.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Import_WhenAuditPersistenceFails_RollsBackEntirePlan()
    {
        SetResources();
        _audit.Setup(value => value.LogAsync(It.IsAny<Modules.Audit.Domain.AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new Error("AUDIT_FAILURE", "audit unavailable", 500)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(FixtureDocument()).ImportAsync(
            new MemoryStream([1]),
            "fixture.mpp",
            new CommitPlanImportDto { ProjectId = _projectId, NewPlanName = "Atomic", NewPlanType = PlanType.Execution },
            _userId.ToString()));

        _context.ChangeTracker.Clear();
        Assert.Empty(await _context.Plans.ToListAsync());
        Assert.Empty(await _context.PlanVersions.ToListAsync());
        Assert.Empty(await _context.PlanActivities.ToListAsync());
    }

    [Fact]
    public async Task ResolveResource_RejectsUndefinedResourceTypeEnum()
    {
        SetResources();
        var service = CreateService(FixtureDocument());
        var session = await service.CreateSessionAsync(new MemoryStream([1]), "fixture.mpp", _projectId, null, _userId.ToString());
        Assert.True(session.IsSuccess, session.Error?.Message);

        var result = await service.ResolveResourceAsync(_projectId, session.Value!.Id, "r1", new PlanImportResourceResolutionDto
        {
            Resolution = "CreateNew",
            CreateResource = new CreateResourceDto { Name = "Invalid type", Type = (ResourceType)99 }
        }, _userId.ToString());

        Assert.True(result.IsFailure);
        Assert.Contains(string.Join(", ", Enum.GetNames<ResourceType>()), result.Error!.Message);
    }

    [Fact]
    public async Task ResolveResource_VendorEnumRequiresVendorId()
    {
        SetResources();
        var service = CreateService(FixtureDocument());
        var session = await service.CreateSessionAsync(new MemoryStream([1]), "fixture.mpp", _projectId, null, _userId.ToString());
        Assert.True(session.IsSuccess, session.Error?.Message);

        var result = await service.ResolveResourceAsync(_projectId, session.Value!.Id, "r1", new PlanImportResourceResolutionDto
        {
            Resolution = "CreateNew",
            CreateResource = new CreateResourceDto { Name = "Vendor resource", Type = ResourceType.Vendor }
        }, _userId.ToString());

        Assert.True(result.IsFailure);
        Assert.Contains("Vendor", result.Error!.Message);
    }

    [Fact]
    public async Task RealMppUatFixture_WhenConfigured_PreservesVerifiedSourceShape()
    {
        var fixturePath = Environment.GetEnvironmentVariable("NEXUS_MPP_UAT_FIXTURE");
        if (string.IsNullOrWhiteSpace(fixturePath)) return;

        SetResources(
            new ResourceDto { Id = Guid.NewGuid(), Name = "BDC" },
            new ResourceDto { Id = Guid.NewGuid(), Name = "KN" },
            new ResourceDto { Id = Guid.NewGuid(), Name = "Developer" });
        var service = new PlanningImportService(
            [new MppPlanningImportProvider()], _context, _resources.Object, _audit.Object, _accessor.Object);

        await using var stream = File.OpenRead(fixturePath);
        var result = await service.ImportAsync(stream, Path.GetFileName(fixturePath), new CommitPlanImportDto
        {
            ProjectId = _projectId,
            NewPlanName = "BDC Custody UAT",
            NewPlanType = PlanType.Custom
        }, _userId.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        _context.ChangeTracker.Clear();
        var version = await _context.PlanVersions
            .Include(value => value.WbsNodes)
            .Include(value => value.Activities)
            .Include(value => value.Dependencies)
            .Include(value => value.Assignments)
            .Include(value => value.Calendars)
            .SingleAsync();
        Assert.All(version.WbsNodes, node =>
        {
            Assert.NotNull(node.TextFormat);
            Assert.False(node.TextFormat!.IsSpecified);
            Assert.Null(node.ToDto().TextFormat);
        });
        Assert.All(version.Activities, activity =>
        {
            Assert.NotNull(activity.TextFormat);
            Assert.False(activity.TextFormat!.IsSpecified);
            Assert.Null(activity.ToDto().TextFormat);
        });
        Assert.Equal(36, version.Activities.Count);
        Assert.Equal(5, version.Activities.Count(value => value.Type == PlanActivityType.Milestone));
        Assert.Equal(24, version.Dependencies.Count);
        Assert.Equal(33, version.Assignments.Count);
        Assert.Equal(3, version.Assignments.Select(value => value.ResourceId).Distinct().Count());
        Assert.Equal(3, version.Calendars.Count);
        Assert.Equal(new DateTime(2024, 8, 1, 8, 0, 0), version.Activities.Min(value => value.StartDate));
        Assert.Equal(new DateTime(2024, 10, 9, 17, 0, 0), version.Activities.Max(value => value.EndDate));
        Assert.Equal(5, MaximumDepth(version.WbsNodes));
        Assert.Equal(new[] { "1", "1.1", "1.1.1", "1.1.2", "1.2" },
            version.Activities.OrderBy(value => value.SortOrder).Take(5).Select(value => value.Code));
        Assert.Equal(21, version.Dependencies.Count(value => value.Type == PlanDependencyType.FinishToStart));
        Assert.Equal(2, version.Dependencies.Count(value => value.Type == PlanDependencyType.StartToStart));
        Assert.Equal(1, version.Dependencies.Count(value => value.Type == PlanDependencyType.FinishToFinish));
    }

    private PlanningImportService CreateService(PlanningImportDocument document) => new(
        [new FakeImportProvider(document)], _context, _resources.Object, _audit.Object, _accessor.Object);

    private void SetPermissions(params string[] permissionKeys)
    {
        var claims = new List<Claim>
        {
            new("tenantId", _tenantId.ToString()),
            new(ClaimTypes.NameIdentifier, _userId.ToString())
        };
        claims.AddRange(permissionKeys.Select(key => new Claim("permission", key)));
        _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private void SetResources(params ResourceDto[] resources) =>
        _resources.Setup(value => value.GetPagedAsync(It.IsAny<ResourceFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ResourceDto>(resources, 1, 5000, resources.Length));

    private static PlanningImportDocument FixtureDocument() => new()
    {
        Format = "mpp",
        ProjectName = "Fixture",
        Tasks =
        {
            new PlanningImportTask { SourceId = "1", Code = "1", Name = "Summary", OutlineLevel = 1, SortOrder = 0, DurationDays = 5, Start = new DateTime(2026, 1, 5), Finish = new DateTime(2026, 1, 9), ActualStart = new DateTime(2026, 1, 6), ActualFinish = new DateTime(2026, 1, 9) },
            new PlanningImportTask { SourceId = "2", ParentSourceId = "1", Code = "1.1", Name = "Milestone", OutlineLevel = 2, SortOrder = 1, IsMilestone = true, Start = new DateTime(2026, 1, 9), Finish = new DateTime(2026, 1, 9), PercentComplete = 50 }
        },
        Dependencies =
        {
            new PlanningImportDependency { PredecessorSourceId = "1", SuccessorSourceId = "2", Type = PlanDependencyType.StartToFinish, LagDays = -2 }
        },
        Resources =
        {
            new PlanningImportResource { SourceId = "r1", Name = "Developer", Email = "dev@example.com" }
        },
        Assignments =
        {
            new PlanningImportAssignment { TaskSourceId = "2", ResourceSourceId = "r1", AllocationPercent = 50 }
        },
        Calendars =
        {
            new PlanningImportCalendar
            {
                Name = "Standard",
                IsDefault = true,
                Days =
                {
                    new PlanningImportCalendarDay { DayOfWeek = DayOfWeek.Monday, IsWorkingDay = true, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0) },
                    new PlanningImportCalendarDay { DayOfWeek = DayOfWeek.Sunday, IsWorkingDay = false }
                }
            }
        }
    };

    private static int MaximumDepth(IEnumerable<PlanWbsNode> nodes)
    {
        var byId = nodes.ToDictionary(value => value.Id);
        int Depth(PlanWbsNode node) => node.ParentWbsId.HasValue && byId.TryGetValue(node.ParentWbsId.Value, out var parent)
            ? 1 + Depth(parent)
            : 1;
        return byId.Values.Max(Depth);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeImportProvider(PlanningImportDocument document) : IPlanningImportProvider
    {
        public string Format => "mpp";
        public IReadOnlyCollection<string> SupportedExtensions => [".mpp"];
        public bool CanRead(string fileName) => Path.GetExtension(fileName).Equals(".mpp", StringComparison.OrdinalIgnoreCase);
        public Task<PlanningImportDocument> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken = default) => Task.FromResult(document);
    }
}
