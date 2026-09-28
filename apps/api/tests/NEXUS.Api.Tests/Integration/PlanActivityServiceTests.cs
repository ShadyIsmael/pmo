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
/// Focused service/integration tests for the Project Planning Activities foundation
/// (list CRUD inside the active Working version). Uses the same in-memory SQLite
/// setup and shared tenant resolution as the other API integration tests.
/// </summary>
public class PlanActivityServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<IHttpContextAccessor> _accessor;
    private readonly Mock<IAuditService> _auditService;
    private readonly PlanService _planService;
    private readonly PlanActivityService _activityService;

    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public PlanActivityServiceTests()
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

    private async Task<Guid> CreateWbsNodeAsync(Guid planId, string code = "1", string name = "Phase 1")
    {
        var version = _context.PlanVersions.Single(v => v.PlanId == planId);
        var node = new PlanWbsNode
        {
            TenantId = _tenantA,
            PlanVersionId = version.Id,
            Code = code,
            Name = name,
            CreatedBy = _userA.ToString()
        };
        _context.PlanWbsNodes.Add(node);
        await _context.SaveChangesAsync();
        return node.Id;
    }

    [Fact]
    public async Task Create_Task_Succeeds()
    {
        var planId = await CreatePlanAsync();

        var result = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "A1",
            Name = "Design",
            Duration = 5,
            EstimateHours = 40
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("A1", result.Value.Code);
        Assert.Equal(PlanActivityType.Task, result.Value.Type);
        Assert.Equal(0, result.Value.SortOrder);
        Assert.NotEmpty(result.Value.RowVersion);
        Assert.Single(_context.PlanActivities.IgnoreQueryFilters());
        _auditService.Verify(a => a.LogAsync(It.Is<AuditEntry>(e => e.EntityType == "PlanActivity" && e.Action == "Create"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_Milestone_OnlyZeroDuration()
    {
        var planId = await CreatePlanAsync();

        var result = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "M1",
            Name = "Milestone",
            Type = PlanActivityType.Milestone,
            Duration = 0
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanActivityType.Milestone, result.Value!.Type);
        Assert.Equal(0, result.Value.Duration);
    }

    [Fact]
    public async Task Create_MilestoneWithNonZeroDuration_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();

        var result = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "M1",
            Name = "Milestone",
            Type = PlanActivityType.Milestone,
            Duration = 5
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
        Assert.Empty(_context.PlanActivities.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Create_LinksWbsNodeFromSameVersion()
    {
        var planId = await CreatePlanAsync();
        var wbsNodeId = await CreateWbsNodeAsync(planId);

        var result = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "A1",
            Name = "Design",
            WbsId = wbsNodeId
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(wbsNodeId, result.Value!.WbsId);
    }

    [Fact]
    public async Task Create_WbsNodeFromDifferentVersion_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var foreignNodeId = await CreateBaselineActivityAsync(planId, workingVersion.Id);

        var result = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "A1",
            Name = "Design",
            WbsId = foreignNodeId
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_ActualFinishWithoutStart_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();

        var result = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "A1",
            Name = "Executed",
            ActualFinish = new DateTime(2026, 1, 5)
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_AutoAssignsNextSortOrder()
    {
        var planId = await CreatePlanAsync();
        await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "One" }, _userA.ToString());
        await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A2", Name = "Two" }, _userA.ToString());

        var third = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A3", Name = "Three" }, _userA.ToString())).Value!;

        Assert.Equal(2, third.SortOrder);
    }

    [Fact]
    public async Task Create_CustomSortOrder_IsRespected()
    {
        var planId = await CreatePlanAsync();

        var result = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "A1",
            Name = "Design",
            SortOrder = 7
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value!.SortOrder);
    }

    [Fact]
    public async Task Create_DuplicateCode_SameVersion_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "A" }, _userA.ToString());

        var result = await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "B" }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
        Assert.Single(_context.PlanActivities.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Create_MissingName_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();

        var result = await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "  " }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_MissingCode_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();

        var result = await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Name = "Design" }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Create_UnknownPlan_ReturnsNotFound()
    {
        SetTenant(_tenantA, _userA);

        var result = await _activityService.CreateAsync(Guid.NewGuid(), new CreatePlanActivityDto { Code = "A1", Name = "A" }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task GetByPlan_OrdersBySortOrderThenCode()
    {
        var planId = await CreatePlanAsync();
        await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "B", Name = "B", SortOrder = 2 }, _userA.ToString());
        await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "C", Name = "C", SortOrder = 2 }, _userA.ToString());
        await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A", Name = "A", SortOrder = 1 }, _userA.ToString());

        var result = await _activityService.GetByPlanAsync(planId);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "A", "B", "C" }, result.Value!.Select(a => a.Code).ToArray());
    }

    [Fact]
    public async Task GetByPlan_UnknownPlan_ReturnsNotFound()
    {
        SetTenant(_tenantA, _userA);

        var result = await _activityService.GetByPlanAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_ValidChanges_SucceedsAndPersists()
    {
        var planId = await CreatePlanAsync();
        var activity = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "Before" }, _userA.ToString())).Value!;

        var result = await _activityService.UpdateAsync(planId, activity.Id, new UpdatePlanActivityDto
        {
            Code = "A1",
            Name = "After",
            Progress = 0.5m,
            SortOrder = 3,
            RowVersion = activity.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal("After", result.Value!.Name);
        Assert.Equal(0.5m, result.Value.Progress);
        Assert.Equal(3, result.Value.SortOrder);
    }

    [Fact]
    public async Task Update_FinishDateOnly_UsesFinishAnchorWhenStoredTimesDiffer()
    {
        var planId = await CreatePlanAsync();
        var activity = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "SCHEDULE-FINISH",
            Name = "Finish date",
            StartDate = new DateTime(2026, 1, 5, 8, 0, 0),
            Duration = 3
        }, _userA.ToString())).Value!;

        var result = await _activityService.UpdateAsync(planId, activity.Id, new UpdatePlanActivityDto
        {
            Code = activity.Code,
            Name = activity.Name,
            Type = activity.Type,
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 12),
            Duration = activity.Duration,
            SchedulingMode = activity.SchedulingMode,
            ConstraintType = activity.ConstraintType,
            SortOrder = activity.SortOrder,
            RowVersion = activity.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new DateTime(2026, 1, 12).Date, result.Value!.EndDate?.Date);
    }

    [Fact]
    public async Task Update_ActualDates_PersistWithoutChangingPlannedSchedule()
    {
        var planId = await CreatePlanAsync();
        var activity = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "A1",
            Name = "Executed",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 5),
            Duration = 5
        }, _userA.ToString())).Value!;

        var result = await _activityService.UpdateAsync(planId, activity.Id, new UpdatePlanActivityDto
        {
            Code = "A1",
            Name = "Executed",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 5),
            Duration = 5,
            ActualStart = new DateTime(2026, 1, 2),
            ActualFinish = new DateTime(2026, 1, 6),
            RowVersion = activity.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new DateTime(2026, 1, 2), result.Value!.ActualStart);
        Assert.Equal(new DateTime(2026, 1, 6), result.Value.ActualFinish);
        var stored = await _context.PlanActivities.SingleAsync(value => value.Id == activity.Id);
        Assert.Equal(new DateTime(2026, 1, 1), stored.StartDate);
        Assert.Equal(new DateTime(2026, 1, 5), stored.EndDate);
    }

    [Fact]
    public async Task Update_DuplicateCode_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        var a = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "A" }, _userA.ToString())).Value!;
        var b = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A2", Name = "B" }, _userA.ToString())).Value!;

        var result = await _activityService.UpdateAsync(planId, b.Id, new UpdatePlanActivityDto
        {
            Code = "A1",
            Name = "B",
            RowVersion = b.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_StaleRowVersion_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        var activity = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "A" }, _userA.ToString())).Value!;

        var result = await _activityService.UpdateAsync(planId, activity.Id, new UpdatePlanActivityDto
        {
            Code = "A1",
            Name = "Edited",
            RowVersion = new byte[8]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_BaselineActivity_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var baselineActivityId = await CreateBaselineActivityAsync(planId, workingVersion.Id);

        var result = await _activityService.UpdateAsync(planId, baselineActivityId, new UpdatePlanActivityDto
        {
            Code = "9",
            Name = "Mutated",
            RowVersion = new byte[8]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_WbsNodeFromDifferentVersion_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var activity = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "A" }, _userA.ToString())).Value!;
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var foreignNodeId = await CreateBaselineActivityAsync(planId, workingVersion.Id);

        var result = await _activityService.UpdateAsync(planId, activity.Id, new UpdatePlanActivityDto
        {
            Code = "A1",
            Name = "A",
            WbsId = foreignNodeId,
            RowVersion = activity.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_MilestoneWithNonZeroDuration_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var activity = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "M1", Name = "Milestone", Type = PlanActivityType.Milestone }, _userA.ToString())).Value!;

        var result = await _activityService.UpdateAsync(planId, activity.Id, new UpdatePlanActivityDto
        {
            Code = "M1",
            Name = "Milestone",
            Type = PlanActivityType.Milestone,
            Duration = 5,
            RowVersion = activity.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_UnknownActivity_ReturnsNotFound()
    {
        var planId = await CreatePlanAsync();

        var result = await _activityService.UpdateAsync(planId, Guid.NewGuid(), new UpdatePlanActivityDto
        {
            Code = "A1",
            Name = "A",
            RowVersion = new byte[8]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Delete_SoftDeletesActivity()
    {
        var planId = await CreatePlanAsync();
        var activity = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "Leaf" }, _userA.ToString())).Value!;

        var result = await _activityService.DeleteAsync(planId, activity.Id, activity.RowVersion, _userA.ToString());

        Assert.True(result.IsSuccess);
        var stored = await _context.PlanActivities.IgnoreQueryFilters().SingleAsync(a => a.Id == activity.Id);
        Assert.True(stored.IsDeleted);
        var list = await _activityService.GetByPlanAsync(planId);
        Assert.True(list.IsSuccess);
        Assert.Empty(list.Value!);
    }

    [Fact]
    public async Task Delete_BaselineActivity_ReturnsValidationError()
    {
        var planId = await CreatePlanAsync();
        var workingVersion = _context.PlanVersions.Single(v => v.PlanId == planId);
        var baselineActivityId = await CreateBaselineActivityAsync(planId, workingVersion.Id);

        var result = await _activityService.DeleteAsync(planId, baselineActivityId, new byte[8], _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Delete_StaleRowVersion_ReturnsConflict()
    {
        var planId = await CreatePlanAsync();
        var activity = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "Leaf" }, _userA.ToString())).Value!;

        var result = await _activityService.DeleteAsync(planId, activity.Id, new byte[8], _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task TenantB_CannotModifyTenantAClaimedActivity()
    {
        var planId = await CreatePlanAsync();
        var activity = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "Secret" }, _userA.ToString())).Value!;

        SetTenant(_tenantB, Guid.NewGuid());

        var result = await _activityService.UpdateAsync(planId, activity.Id, new UpdatePlanActivityDto
        {
            Code = "A1",
            Name = "Hacked",
            RowVersion = activity.RowVersion
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
