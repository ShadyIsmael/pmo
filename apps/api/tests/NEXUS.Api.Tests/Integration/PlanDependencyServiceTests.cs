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
/// Focused service/integration tests for the Project Planning Dependencies
/// foundation (CRUD inside the active Working version). Uses the same in-memory
/// SQLite setup and shared tenant resolution as the other API integration tests.
/// </summary>
public class PlanDependencyServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor;
    private readonly Mock<IAuditService> _auditService;
    private readonly PlanService _planService;
    private readonly PlanActivityService _activityService;
    private readonly PlanDependencyService _dependencyService;

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public PlanDependencyServiceTests()
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
        _planService = new PlanService(_context, _auditService.Object, _accessor.Object);
        _activityService = new PlanActivityService(_context, _auditService.Object, _accessor.Object);
        _dependencyService = new PlanDependencyService(_context, _auditService.Object, _accessor.Object);
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

    private async Task<Guid> CreatePlanAsync(PlanType type = PlanType.Execution)
    {
        SetTenant(_tenantA, _userA);
        var result = await _planService.CreateAsync(new CreatePlanDto
        {
            ProjectId = _projectId,
            PlanType = type,
            Name = "Plan"
        }, _userA.ToString());
        Assert.True(result.IsSuccess);
        return result.Value!.Id;
    }

    private async Task<Guid> CreateActivityAsync(Guid planId, string code)
    {
        var result = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = code,
            Name = code,
            Duration = 1
        }, _userA.ToString());
        Assert.True(result.IsSuccess);
        return result.Value!.Id;
    }

    private async Task<Guid> CreateDependencyAsync(Guid planId, Guid successorId, Guid predecessorId, PlanDependencyType type = PlanDependencyType.FinishToStart, int? lag = null)
    {
        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = successorId,
            PredecessorActivityId = predecessorId,
            Type = type,
            Lag = lag
        }, _userA.ToString());
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!.Id;
    }

    private async Task<PlanVersion> CreateBaselineVersionAsync(Guid planId, Guid workingVersionId)
    {
        var baseline = new PlanVersion
        {
            TenantId = _tenantA,
            PlanId = planId,
            VersionNumber = 2,
            Type = PlanVersionType.Baseline,
            Status = PlanVersionStatus.Approved,
            Name = "Baseline v1",
            SourceVersionId = workingVersionId,
            CreatedBy = _userA.ToString()
        };
        _context.PlanVersions.Add(baseline);
        await _context.SaveChangesAsync();
        return baseline;
    }

    private async Task<Guid> CreateBaselineActivityAsync(Guid planId, Guid workingVersionId, string code = "9")
    {
        var baseline = await CreateBaselineVersionAsync(planId, workingVersionId);
        var activity = new PlanActivity
        {
            TenantId = _tenantA,
            PlanVersionId = baseline.Id,
            Code = code,
            Name = code,
            CreatedBy = _userA.ToString()
        };
        _context.PlanActivities.Add(activity);
        await _context.SaveChangesAsync();
        return activity.Id;
    }

    private async Task<Guid> CreateBaselineDependencyAsync(Guid planId, Guid workingVersionId)
    {
        var baseline = await CreateBaselineVersionAsync(planId, workingVersionId);
        var first = new PlanActivity
        {
            TenantId = _tenantA,
            PlanVersionId = baseline.Id,
            Code = "9.1",
            Name = "A",
            CreatedBy = _userA.ToString()
        };
        var second = new PlanActivity
        {
            TenantId = _tenantA,
            PlanVersionId = baseline.Id,
            Code = "9.2",
            Name = "B",
            CreatedBy = _userA.ToString()
        };
        _context.PlanActivities.AddRange(first, second);
        await _context.SaveChangesAsync();

        var dependency = new PlanDependency
        {
            TenantId = _tenantA,
            PlanVersionId = baseline.Id,
            ActivityId = second.Id,
            PredecessorActivityId = first.Id,
            Type = PlanDependencyType.FinishToStart,
            CreatedBy = _userA.ToString()
        };
        _context.PlanDependencies.Add(dependency);
        await _context.SaveChangesAsync();
        return dependency.Id;
    }

    [Fact]
    public async Task Create_Dependency_Succeeds()
    {
        var planId = await CreatePlanAsync();
        var first = await CreateActivityAsync(planId, "A1");
        var second = await CreateActivityAsync(planId, "A2");

        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = second,
            PredecessorActivityId = first,
            Type = PlanDependencyType.FinishToStart,
            Lag = 2
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(first, result.Value.PredecessorActivityId);
        Assert.Equal(second, result.Value.ActivityId);
        Assert.Equal(2, result.Value.Lag);
        Assert.NotEmpty(result.Value.RowVersion);
        Assert.Single(_context.PlanDependencies.IgnoreQueryFilters());
        _auditService.Verify(a => a.LogAsync(It.Is<AuditEntry>(e => e.EntityType == "PlanDependency" && e.Action == "Create"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_SupportsAllDependencyTypes()
    {
        var planId = await CreatePlanAsync();
        var first = await CreateActivityAsync(planId, "A1");
        var second = await CreateActivityAsync(planId, "A2");

        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = second,
            PredecessorActivityId = first,
            Type = PlanDependencyType.StartToStart
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanDependencyType.StartToStart, result.Value!.Type);
    }

    [Fact]
    public async Task Create_NullLag_Succeeds()
    {
        var planId = await CreatePlanAsync();
        var first = await CreateActivityAsync(planId, "A1");
        var second = await CreateActivityAsync(planId, "A2");

        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = second,
            PredecessorActivityId = first
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Lag);
    }

    [Fact]
    public async Task Create_SelfDependency_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var activity = await CreateActivityAsync(planId, "A1");

        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = activity,
            PredecessorActivityId = activity
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
        Assert.Empty(_context.PlanDependencies.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Create_DuplicateDependency_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        var first = await CreateActivityAsync(planId, "A1");
        var second = await CreateActivityAsync(planId, "A2");
        await CreateDependencyAsync(planId, second, first);

        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = second,
            PredecessorActivityId = first
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
        Assert.Single(_context.PlanDependencies.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Create_SuccessorFromDifferentVersion_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var foreignActivityId = await CreateBaselineActivityAsync(planId, workingVersion.Id);
        var local = await CreateActivityAsync(planId, "A1");

        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = foreignActivityId,
            PredecessorActivityId = local
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_PredecessorFromDifferentVersion_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var foreignActivityId = await CreateBaselineActivityAsync(planId, workingVersion.Id);
        var local = await CreateActivityAsync(planId, "A1");

        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = local,
            PredecessorActivityId = foreignActivityId
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_UnknownActivities_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();

        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = Guid.NewGuid(),
            PredecessorActivityId = Guid.NewGuid()
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_ChainDependencies_Succeeds()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        var c = await CreateActivityAsync(planId, "A3");

        await CreateDependencyAsync(planId, b, a);
        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = c,
            PredecessorActivityId = b
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_Cycle_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        var c = await CreateActivityAsync(planId, "A3");
        await CreateDependencyAsync(planId, b, a);
        await CreateDependencyAsync(planId, c, b);

        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = a,
            PredecessorActivityId = c
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_DirectTwoNodeCycle_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        await CreateDependencyAsync(planId, b, a);

        var result = await _dependencyService.CreateAsync(planId, new CreatePlanDependencyDto
        {
            ActivityId = a,
            PredecessorActivityId = b
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_UnknownPlan_ReturnsNotFound()
    {
        SetTenant(_tenantA, _userA);

        var result = await _dependencyService.CreateAsync(Guid.NewGuid(), new CreatePlanDependencyDto
        {
            ActivityId = Guid.NewGuid(),
            PredecessorActivityId = Guid.NewGuid()
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task GetByPlan_ReturnsAllDependencies()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        var c = await CreateActivityAsync(planId, "A3");
        await CreateDependencyAsync(planId, b, a);
        await CreateDependencyAsync(planId, c, b);

        var result = await _dependencyService.GetByPlanAsync(planId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public async Task GetByPlan_UnknownPlan_ReturnsNotFound()
    {
        SetTenant(_tenantA, _userA);

        var result = await _dependencyService.GetByPlanAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_ValidChanges_SucceedsAndPersists()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        var c = await CreateActivityAsync(planId, "A3");
        var dependencyId = await CreateDependencyAsync(planId, b, a);
        var dependency = _context.PlanDependencies.Single(d => d.Id == dependencyId);

        var result = await _dependencyService.UpdateAsync(planId, dependencyId, new UpdatePlanDependencyDto
        {
            ActivityId = c,
            PredecessorActivityId = b,
            Type = PlanDependencyType.FinishToFinish,
            Lag = 3,
            RowVersion = dependency.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(c, result.Value!.ActivityId);
        Assert.Equal(PlanDependencyType.FinishToFinish, result.Value.Type);
        Assert.Equal(3, result.Value.Lag);
    }

    [Fact]
    public async Task Update_SelfDependency_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        var dependencyId = await CreateDependencyAsync(planId, b, a);
        var dependency = _context.PlanDependencies.Single(d => d.Id == dependencyId);

        var result = await _dependencyService.UpdateAsync(planId, dependencyId, new UpdatePlanDependencyDto
        {
            ActivityId = a,
            PredecessorActivityId = a,
            RowVersion = dependency.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_DuplicateEdge_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        var c = await CreateActivityAsync(planId, "A3");
        var firstId = await CreateDependencyAsync(planId, b, a);
        var secondId = await CreateDependencyAsync(planId, c, b);
        var first = _context.PlanDependencies.Single(d => d.Id == firstId);

        var result = await _dependencyService.UpdateAsync(planId, secondId, new UpdatePlanDependencyDto
        {
            ActivityId = b,
            PredecessorActivityId = a,
            RowVersion = _context.PlanDependencies.Single(d => d.Id == secondId).RowVersion
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_StaleRowVersion_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        var dependencyId = await CreateDependencyAsync(planId, b, a);

        var result = await _dependencyService.UpdateAsync(planId, dependencyId, new UpdatePlanDependencyDto
        {
            ActivityId = b,
            PredecessorActivityId = a,
            RowVersion = new byte[8]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_CreatingCycle_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        var c = await CreateActivityAsync(planId, "A3");
        await CreateDependencyAsync(planId, b, a);
        var cToB = await CreateDependencyAsync(planId, b, c);
        var cToBDep = _context.PlanDependencies.Single(d => d.Id == cToB);

        // Rewrite the c->b edge so it becomes b->a, forming a -> b -> a.
        var result = await _dependencyService.UpdateAsync(planId, cToB, new UpdatePlanDependencyDto
        {
            ActivityId = a,
            PredecessorActivityId = b,
            RowVersion = cToBDep.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_BaselineDependency_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var baselineDependencyId = await CreateBaselineDependencyAsync(planId, workingVersion.Id);

        var result = await _dependencyService.UpdateAsync(planId, baselineDependencyId, new UpdatePlanDependencyDto
        {
            ActivityId = Guid.NewGuid(),
            PredecessorActivityId = Guid.NewGuid(),
            RowVersion = new byte[8]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_UnknownDependency_ReturnsNotFound()
    {
        var planId = await CreatePlanAsync();

        var result = await _dependencyService.UpdateAsync(planId, Guid.NewGuid(), new UpdatePlanDependencyDto
        {
            ActivityId = Guid.NewGuid(),
            PredecessorActivityId = Guid.NewGuid(),
            RowVersion = new byte[8]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Delete_SoftDeletesDependency()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        var dependencyId = await CreateDependencyAsync(planId, b, a);
        var dependency = _context.PlanDependencies.Single(d => d.Id == dependencyId);

        var result = await _dependencyService.DeleteAsync(planId, dependencyId, dependency.RowVersion, _userA.ToString());

        Assert.True(result.IsSuccess);
        var stored = await _context.PlanDependencies.IgnoreQueryFilters().SingleAsync(d => d.Id == dependencyId);
        Assert.True(stored.IsDeleted);
        var list = await _dependencyService.GetByPlanAsync(planId);
        Assert.True(list.IsSuccess);
        Assert.Empty(list.Value!);
    }

    [Fact]
    public async Task Delete_BaselineDependency_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var baselineDependencyId = await CreateBaselineDependencyAsync(planId, workingVersion.Id);

        var result = await _dependencyService.DeleteAsync(planId, baselineDependencyId, new byte[8], _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Delete_StaleRowVersion_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        var dependencyId = await CreateDependencyAsync(planId, b, a);

        var result = await _dependencyService.DeleteAsync(planId, dependencyId, new byte[8], _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task TenantB_CannotModifyTenantAClaimedDependency()
    {
        var planId = await CreatePlanAsync();
        var a = await CreateActivityAsync(planId, "A1");
        var b = await CreateActivityAsync(planId, "A2");
        var dependencyId = await CreateDependencyAsync(planId, b, a);
        var dependency = _context.PlanDependencies.Single(d => d.Id == dependencyId);

        SetTenant(_tenantB, Guid.NewGuid());

        var result = await _dependencyService.UpdateAsync(planId, dependencyId, new UpdatePlanDependencyDto
        {
            ActivityId = b,
            PredecessorActivityId = a,
            RowVersion = dependency.RowVersion
        }, "tenantB-user");

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }
}