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
    private readonly PlanGridCommandHistoryService _history;

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
        var scheduling = new PlanSchedulingService(_context, new NEXUS.Modules.ProjectPlanning.Domain.Scheduling.AutoSchedulingEngine());
        var rollup = new SummaryTaskRollupService(_context);
        _history = new PlanGridCommandHistoryService(_context, _accessor.Object, scheduling, rollup);
        _activityService = new PlanActivityService(_context, _auditService.Object, _accessor.Object, scheduling, summaryRollup: rollup, commandHistory: _history);
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

    private PlanWbsService CreateWbsService() => new(
        _context,
        _auditService.Object,
        _accessor.Object,
        new SummaryTaskRollupService(_context),
        _history);

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
    public async Task GridDurationEdit_UndoAndRedoSurviveReload()
    {
        var planId = await CreatePlanAsync();
        var created = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "HISTORY-1",
            Name = "History task",
            SchedulingMode = PlanSchedulingMode.Manual,
            StartDate = new DateTime(2026, 3, 2, 8, 0, 0),
            EndDate = new DateTime(2026, 3, 2, 17, 0, 0),
            Duration = 1
        }, _userA.ToString());
        Assert.True(created.IsSuccess, created.Error?.Message);

        var update = await _activityService.UpdateAsync(planId, created.Value!.Id, new UpdatePlanActivityDto
        {
            Code = created.Value.Code,
            Name = created.Value.Name,
            Type = created.Value.Type,
            SchedulingMode = created.Value.SchedulingMode,
            ConstraintType = created.Value.ConstraintType,
            StartDate = created.Value.StartDate,
            EndDate = created.Value.EndDate,
            Duration = 4,
            SortOrder = created.Value.SortOrder,
            RowVersion = created.Value.RowVersion
        }, _userA.ToString());
        Assert.True(update.IsSuccess, update.Error?.Message);
        Assert.Equal(4, update.Value!.Duration);

        _context.ChangeTracker.Clear();
        var persisted = (await _activityService.GetByPlanAsync(planId)).Value!.Single(item => item.Id == created.Value.Id);
        Assert.Equal(4, persisted.Duration);

        var working = _context.PlanVersions.Single(version => version.PlanId == planId && version.Type == PlanVersionType.Working);
        var state = await _history.GetStateAsync(_projectId, planId, _userA.ToString());
        Assert.True(state.IsSuccess);
        Assert.True(state.Value!.CanUndo);
        var commandId = state.Value.UndoCommandId!.Value;

        var undone = await _history.UndoAsync(_projectId, planId, commandId, _userA.ToString());
        Assert.True(undone.IsSuccess, undone.Error?.Message);
        _context.ChangeTracker.Clear();
        var previous = (await _activityService.GetByPlanAsync(planId)).Value!.Single(item => item.Id == created.Value.Id);
        Assert.Equal(1, previous.Duration);
        Assert.Equal(working.Id, previous.PlanVersionId);

        var afterUndo = await _history.GetStateAsync(_projectId, planId, _userA.ToString());
        Assert.True(afterUndo.Value!.CanRedo);
        var redone = await _history.RedoAsync(_projectId, planId, commandId, _userA.ToString());
        Assert.True(redone.IsSuccess, redone.Error?.Message);
        _context.ChangeTracker.Clear();
        var latest = (await _activityService.GetByPlanAsync(planId)).Value!.Single(item => item.Id == created.Value.Id);
        Assert.Equal(4, latest.Duration);
    }

    [Fact]
    public async Task RecalculateSchedule_ReappliesCanonicalDatesAndUndoRedoSurviveReload()
    {
        var planId = await CreatePlanAsync();
        var predecessor = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "RECALC-A",
            Name = "Predecessor",
            StartDate = new DateTime(2026, 1, 5, 8, 0, 0),
            EndDate = new DateTime(2026, 1, 5, 17, 0, 0),
            Duration = 1
        }, _userA.ToString());
        var successor = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "RECALC-B",
            Name = "Successor",
            StartDate = new DateTime(2026, 1, 5, 8, 0, 0),
            EndDate = new DateTime(2026, 1, 5, 17, 0, 0),
            Duration = 1
        }, _userA.ToString());
        Assert.True(predecessor.IsSuccess, predecessor.Error?.Message);
        Assert.True(successor.IsSuccess, successor.Error?.Message);

        var versionId = _context.PlanVersions.Single(version => version.PlanId == planId && version.Type == PlanVersionType.Working).Id;
        _context.PlanDependencies.Add(new PlanDependency
        {
            TenantId = _tenantA,
            PlanVersionId = versionId,
            ActivityId = successor.Value!.Id,
            PredecessorActivityId = predecessor.Value!.Id,
            Type = PlanDependencyType.FinishToStart,
            CreatedBy = _userA.ToString()
        });
        await _context.SaveChangesAsync();

        var staleSuccessor = await _context.PlanActivities.SingleAsync(activity => activity.Id == successor.Value.Id);
        staleSuccessor.StartDate = new DateTime(2026, 1, 1, 8, 0, 0);
        staleSuccessor.EndDate = new DateTime(2026, 1, 1, 17, 0, 0);
        await _context.SaveChangesAsync();

        var recalculated = await _activityService.RecalculateScheduleAsync(planId, _userA.ToString());
        Assert.True(recalculated.IsSuccess, recalculated.Error?.Message);
        Assert.Contains(recalculated.Value!.ChangedActivities, item => item.ActivityId == successor.Value.Id && item.StartDate == new DateTime(2026, 1, 6, 8, 0, 0));

        _context.ChangeTracker.Clear();
        var persisted = await _context.PlanActivities.SingleAsync(activity => activity.Id == successor.Value.Id);
        Assert.Equal(new DateTime(2026, 1, 6, 8, 0, 0), persisted.StartDate);
        var history = await _history.GetStateAsync(_projectId, planId, _userA.ToString());
        Assert.True(history.IsSuccess);
        Assert.True(history.Value!.CanUndo);

        var undone = await _history.UndoAsync(_projectId, planId, history.Value.UndoCommandId!.Value, _userA.ToString());
        Assert.True(undone.IsSuccess, undone.Error?.Message);
        _context.ChangeTracker.Clear();
        persisted = await _context.PlanActivities.SingleAsync(activity => activity.Id == successor.Value.Id);
        Assert.Equal(new DateTime(2026, 1, 1, 8, 0, 0), persisted.StartDate);

        var redoState = await _history.GetStateAsync(_projectId, planId, _userA.ToString());
        Assert.True(redoState.IsSuccess);
        Assert.True(redoState.Value!.CanRedo);
        var redone = await _history.RedoAsync(_projectId, planId, redoState.Value.RedoCommandId!.Value, _userA.ToString());
        Assert.True(redone.IsSuccess, redone.Error?.Message);
        _context.ChangeTracker.Clear();
        persisted = await _context.PlanActivities.SingleAsync(activity => activity.Id == successor.Value.Id);
        Assert.Equal(new DateTime(2026, 1, 6, 8, 0, 0), persisted.StartDate);
    }

    [Fact]
    public async Task BulkGridEdit_UndoAndRedoRestoreAllPersistedRows()
    {
        var planId = await CreatePlanAsync();
        var first = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "BULK-HISTORY-1", Name = "First", SchedulingMode = PlanSchedulingMode.Manual, Duration = 1
        }, _userA.ToString());
        var second = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "BULK-HISTORY-2", Name = "Second", SchedulingMode = PlanSchedulingMode.Manual, Duration = 1
        }, _userA.ToString());
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        var firstActivity = first.Value!;
        var secondActivity = second.Value!;

        static UpdatePlanActivityDto Update(PlanActivityDto activity) => new()
        {
            Code = activity.Code,
            Name = activity.Name,
            Type = activity.Type,
            SchedulingMode = activity.SchedulingMode,
            ConstraintType = activity.ConstraintType,
            StartDate = activity.StartDate,
            EndDate = activity.EndDate,
            Duration = 3,
            SortOrder = activity.SortOrder,
            RowVersion = activity.RowVersion
        };

        var edited = await _activityService.BulkUpdateAsync(planId, new BulkUpdatePlanActivitiesDto
        {
            Items =
            [
                new BulkUpdatePlanActivityItemDto { ActivityId = firstActivity.Id, Update = Update(firstActivity) },
                new BulkUpdatePlanActivityItemDto { ActivityId = secondActivity.Id, Update = Update(secondActivity) }
            ]
        }, _userA.ToString());
        Assert.True(edited.IsSuccess, edited.Error?.Message);
        Assert.All(edited.Value!, item => Assert.Equal(3, item.Duration));

        _context.ChangeTracker.Clear();
        var persisted = (await _activityService.GetByPlanAsync(planId)).Value!;
        Assert.All(persisted, item => Assert.Equal(3, item.Duration));
        var command = await _history.GetStateAsync(_projectId, planId, _userA.ToString());
        Assert.True(command.Value!.CanUndo);
        var commandId = command.Value.UndoCommandId!.Value;
        var undo = await _history.UndoAsync(_projectId, planId, commandId, _userA.ToString());
        Assert.True(undo.IsSuccess, undo.Error?.Message);

        _context.ChangeTracker.Clear();
        var afterUndo = (await _activityService.GetByPlanAsync(planId)).Value!;
        Assert.All(afterUndo, item => Assert.Equal(1, item.Duration));
        var redo = await _history.RedoAsync(_projectId, planId, commandId, _userA.ToString());
        Assert.True(redo.IsSuccess, redo.Error?.Message);
        _context.ChangeTracker.Clear();
        var afterRedo = (await _activityService.GetByPlanAsync(planId)).Value!;
        Assert.All(afterRedo, item => Assert.Equal(3, item.Duration));
    }

    [Fact]
    public async Task RootActivityReorder_UndoAndRedoRestorePersistedOrder()
    {
        var planId = await CreatePlanAsync();
        var first = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "MOVE-1", Name = "First", SchedulingMode = PlanSchedulingMode.Manual, Duration = 1
        }, _userA.ToString());
        var second = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "MOVE-2", Name = "Second", SchedulingMode = PlanSchedulingMode.Manual, Duration = 1
        }, _userA.ToString());
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        var firstActivity = first.Value!;
        var secondActivity = second.Value!;

        var moved = await _activityService.MoveAsync(planId, firstActivity.Id, new MovePlanActivityDto
        {
            TargetActivityId = secondActivity.Id,
            Position = PlanActivityMovePosition.After,
            RowVersion = firstActivity.RowVersion
        }, _userA.ToString());
        Assert.True(moved.IsSuccess, moved.Error?.Message);
        var movedActivity = moved.Value!;
        _context.ChangeTracker.Clear();
        var movedOrder = await _context.PlanActivities.Where(item => item.PlanVersionId == movedActivity.PlanVersionId && !item.IsDeleted)
            .OrderBy(item => item.SortOrder).Select(item => item.Id).ToListAsync();
        Assert.Equal(new[] { secondActivity.Id, firstActivity.Id }, movedOrder);

        var state = await _history.GetStateAsync(_projectId, planId, _userA.ToString());
        Assert.True(state.Value!.CanUndo);
        var commandId = state.Value.UndoCommandId!.Value;
        var undone = await _history.UndoAsync(_projectId, planId, commandId, _userA.ToString());
        Assert.True(undone.IsSuccess, undone.Error?.Message);
        _context.ChangeTracker.Clear();
        var restoredOrder = await _context.PlanActivities.Where(item => item.PlanVersionId == movedActivity.PlanVersionId && !item.IsDeleted)
            .OrderBy(item => item.SortOrder).Select(item => item.Id).ToListAsync();
        Assert.Equal(new[] { firstActivity.Id, secondActivity.Id }, restoredOrder);

        var redone = await _history.RedoAsync(_projectId, planId, commandId, _userA.ToString());
        Assert.True(redone.IsSuccess, redone.Error?.Message);
        _context.ChangeTracker.Clear();
        var finalOrder = await _context.PlanActivities.Where(item => item.PlanVersionId == movedActivity.PlanVersionId && !item.IsDeleted)
            .OrderBy(item => item.SortOrder).Select(item => item.Id).ToListAsync();
        Assert.Equal(new[] { secondActivity.Id, firstActivity.Id }, finalOrder);
    }

    [Fact]
    public async Task WbsNodeReorder_UndoAndRedoRestorePersistedOrder()
    {
        var planId = await CreatePlanAsync();
        var firstWbsId = await CreateWbsNodeAsync(planId, "W1", "First WBS");
        var secondWbsId = await CreateWbsNodeAsync(planId, "W2", "Second WBS");
        var first = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "WBS-MOVE-1", Name = "First task", WbsId = firstWbsId, SchedulingMode = PlanSchedulingMode.Manual, Duration = 1
        }, _userA.ToString());
        var second = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "WBS-MOVE-2", Name = "Second task", WbsId = secondWbsId, SchedulingMode = PlanSchedulingMode.Manual, Duration = 1
        }, _userA.ToString());
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        var firstActivity = first.Value!;
        var secondActivity = second.Value!;

        var sourceWbs = await _context.PlanWbsNodes.SingleAsync(node => node.Id == firstWbsId);
        var targetWbs = await _context.PlanWbsNodes.SingleAsync(node => node.Id == secondWbsId);
        var moved = await _activityService.MoveAsync(planId, firstActivity.Id, new MovePlanActivityDto
        {
            TargetActivityId = secondActivity.Id,
            Position = PlanActivityMovePosition.After,
            RowVersion = firstActivity.RowVersion,
            WbsRowVersion = sourceWbs.RowVersion,
            TargetWbsRowVersion = targetWbs.RowVersion
        }, _userA.ToString());
        Assert.True(moved.IsSuccess, moved.Error?.Message);
        var movedActivity = moved.Value!;

        var state = await _history.GetStateAsync(_projectId, planId, _userA.ToString());
        Assert.True(state.Value!.CanUndo);
        var commandId = state.Value.UndoCommandId!.Value;
        var undone = await _history.UndoAsync(_projectId, planId, commandId, _userA.ToString());
        Assert.True(undone.IsSuccess, undone.Error?.Message);
        _context.ChangeTracker.Clear();
        var restoredOrder = await _context.PlanWbsNodes.Where(node => node.PlanVersionId == movedActivity.PlanVersionId && !node.IsDeleted)
            .OrderBy(node => node.SortOrder).Select(node => node.Id).ToListAsync();
        Assert.Equal(new[] { firstWbsId, secondWbsId }, restoredOrder);

        var redone = await _history.RedoAsync(_projectId, planId, commandId, _userA.ToString());
        Assert.True(redone.IsSuccess, redone.Error?.Message);
        _context.ChangeTracker.Clear();
        var finalOrder = await _context.PlanWbsNodes.Where(node => node.PlanVersionId == movedActivity.PlanVersionId && !node.IsDeleted)
            .OrderBy(node => node.SortOrder).Select(node => node.Id).ToListAsync();
        Assert.Equal(new[] { secondWbsId, firstWbsId }, finalOrder);
    }

    [Fact]
    public async Task GridCommandHistory_IsActorScopedAndRejectsStaleCommand()
    {
        var planId = await CreatePlanAsync();
        var created = await _activityService.CreateAsync(planId, new CreatePlanActivityDto
        {
            Code = "HISTORY-2", Name = "History task", SchedulingMode = PlanSchedulingMode.Manual, Duration = 1
        }, _userA.ToString());
        Assert.True(created.IsSuccess);
        var first = await _activityService.UpdateAsync(planId, created.Value!.Id, new UpdatePlanActivityDto
        {
            Code = created.Value.Code, Name = created.Value.Name, Type = created.Value.Type,
            SchedulingMode = created.Value.SchedulingMode, ConstraintType = created.Value.ConstraintType,
            Duration = 2, SortOrder = created.Value.SortOrder, RowVersion = created.Value.RowVersion
        }, _userA.ToString());
        Assert.True(first.IsSuccess);
        var state = await _history.GetStateAsync(_projectId, planId, _userA.ToString());
        var staleCommandId = state.Value!.UndoCommandId!.Value;

        var next = await _activityService.UpdateAsync(planId, created.Value.Id, new UpdatePlanActivityDto
        {
            Code = first.Value!.Code, Name = first.Value.Name, Type = first.Value.Type,
            SchedulingMode = first.Value.SchedulingMode, ConstraintType = first.Value.ConstraintType,
            Duration = 3, SortOrder = first.Value.SortOrder, RowVersion = first.Value.RowVersion
        }, _userA.ToString());
        Assert.True(next.IsSuccess);

        SetTenant(_tenantA, Guid.NewGuid());
        var otherUser = await _history.UndoAsync(_projectId, planId, staleCommandId, Guid.NewGuid().ToString());
        Assert.True(otherUser.IsFailure);
        Assert.Equal(409, otherUser.Error!.StatusCode);

        SetTenant(_tenantA, _userA);
        var stale = await _history.UndoAsync(_projectId, planId, staleCommandId, _userA.ToString());
        Assert.True(stale.IsFailure);
        Assert.Equal(409, stale.Error!.StatusCode);
    }

    [Fact]
    public async Task BulkWbsPlacement_UndoAndRedoRestorePersistedHierarchy()
    {
        var planId = await CreatePlanAsync();
        var parentId = await CreateWbsNodeAsync(planId, "P", "Parent");
        var firstId = await CreateWbsNodeAsync(planId, "A", "First");
        var secondId = await CreateWbsNodeAsync(planId, "B", "Second");
        var wbsService = new PlanWbsService(
            _context,
            _auditService.Object,
            _accessor.Object,
            new SummaryTaskRollupService(_context),
            _history);
        var first = await _context.PlanWbsNodes.SingleAsync(node => node.Id == firstId);
        var second = await _context.PlanWbsNodes.SingleAsync(node => node.Id == secondId);

        var moved = await wbsService.UpdatePlacementsAsync(planId, new BulkUpdatePlanWbsPlacementsDto
        {
            Items =
            [
                new PlanWbsPlacementItemDto { NodeId = firstId, ParentWbsId = parentId, SortOrder = 0, RowVersion = first.RowVersion },
                new PlanWbsPlacementItemDto { NodeId = secondId, ParentWbsId = parentId, SortOrder = 1, RowVersion = second.RowVersion }
            ]
        }, _userA.ToString());
        Assert.True(moved.IsSuccess, moved.Error?.Message);
        Assert.All(moved.Value!, node => Assert.Equal(parentId, node.ParentWbsId));

        _context.ChangeTracker.Clear();
        Assert.Equal(parentId, (await _context.PlanWbsNodes.SingleAsync(node => node.Id == firstId)).ParentWbsId);
        var state = await _history.GetStateAsync(_projectId, planId, _userA.ToString());
        Assert.True(state.Value!.CanUndo);
        var commandId = state.Value.UndoCommandId!.Value;

        var undone = await _history.UndoAsync(_projectId, planId, commandId, _userA.ToString());
        Assert.True(undone.IsSuccess, undone.Error?.Message);
        _context.ChangeTracker.Clear();
        Assert.Null((await _context.PlanWbsNodes.SingleAsync(node => node.Id == firstId)).ParentWbsId);
        Assert.Null((await _context.PlanWbsNodes.SingleAsync(node => node.Id == secondId)).ParentWbsId);

        var redone = await _history.RedoAsync(_projectId, planId, commandId, _userA.ToString());
        Assert.True(redone.IsSuccess, redone.Error?.Message);
        _context.ChangeTracker.Clear();
        Assert.Equal(parentId, (await _context.PlanWbsNodes.SingleAsync(node => node.Id == firstId)).ParentWbsId);
        Assert.Equal(parentId, (await _context.PlanWbsNodes.SingleAsync(node => node.Id == secondId)).ParentWbsId);
    }

    [Fact]
    public async Task BulkWbsPlacement_CycleRejectsTheWholeRequest()
    {
        var planId = await CreatePlanAsync();
        var parentId = await CreateWbsNodeAsync(planId, "P", "Parent");
        var childId = await CreateWbsNodeAsync(planId, "C", "Child");
        var parent = await _context.PlanWbsNodes.SingleAsync(node => node.Id == parentId);
        var child = await _context.PlanWbsNodes.SingleAsync(node => node.Id == childId);

        var result = await CreateWbsService().UpdatePlacementsAsync(planId, new BulkUpdatePlanWbsPlacementsDto
        {
            Items =
            [
                new PlanWbsPlacementItemDto { NodeId = parentId, ParentWbsId = childId, SortOrder = 0, RowVersion = parent.RowVersion },
                new PlanWbsPlacementItemDto { NodeId = childId, ParentWbsId = parentId, SortOrder = 0, RowVersion = child.RowVersion }
            ]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
        _context.ChangeTracker.Clear();
        Assert.Null((await _context.PlanWbsNodes.SingleAsync(node => node.Id == parentId)).ParentWbsId);
        Assert.Null((await _context.PlanWbsNodes.SingleAsync(node => node.Id == childId)).ParentWbsId);
    }

    [Fact]
    public async Task BulkWbsPlacement_RejectsParentFromDifferentVersion()
    {
        var planId = await CreatePlanAsync();
        var workingVersion = await _context.PlanVersions.SingleAsync(version => version.PlanId == planId);
        var childId = await CreateWbsNodeAsync(planId, "C", "Working child");
        var baseline = await CreateBaselineVersionAsync(planId, workingVersion.Id);
        var baselineParent = new PlanWbsNode
        {
            TenantId = _tenantA,
            PlanVersionId = baseline.Id,
            Code = "B1",
            Name = "Baseline parent",
            CreatedBy = _userA.ToString()
        };
        _context.PlanWbsNodes.Add(baselineParent);
        await _context.SaveChangesAsync();
        var child = await _context.PlanWbsNodes.SingleAsync(node => node.Id == childId);

        var result = await CreateWbsService().UpdatePlacementsAsync(planId, new BulkUpdatePlanWbsPlacementsDto
        {
            Items = [new PlanWbsPlacementItemDto { NodeId = childId, ParentWbsId = baselineParent.Id, SortOrder = 0, RowVersion = child.RowVersion }]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
        _context.ChangeTracker.Clear();
        Assert.Null((await _context.PlanWbsNodes.SingleAsync(node => node.Id == childId)).ParentWbsId);
    }

    [Fact]
    public async Task BulkWbsPlacement_DoesNotModifyAnotherTenantsPlan()
    {
        var planId = await CreatePlanAsync();
        var nodeId = await CreateWbsNodeAsync(planId, "A", "Tenant A node");
        var node = await _context.PlanWbsNodes.SingleAsync(item => item.Id == nodeId);

        SetTenant(_tenantB, Guid.NewGuid());
        var result = await CreateWbsService().UpdatePlacementsAsync(planId, new BulkUpdatePlanWbsPlacementsDto
        {
            Items = [new PlanWbsPlacementItemDto { NodeId = nodeId, ParentWbsId = null, SortOrder = 1, RowVersion = node.RowVersion }]
        }, Guid.NewGuid().ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error!.StatusCode);
        SetTenant(_tenantA, _userA);
        _context.ChangeTracker.Clear();
        Assert.Equal(0, (await _context.PlanWbsNodes.SingleAsync(item => item.Id == nodeId)).SortOrder);
    }

    [Fact]
    public async Task BulkWbsPlacement_RejectsMissingRowVersion()
    {
        var planId = await CreatePlanAsync();
        var nodeId = await CreateWbsNodeAsync(planId);

        var result = await CreateWbsService().UpdatePlacementsAsync(planId, new BulkUpdatePlanWbsPlacementsDto
        {
            Items = [new PlanWbsPlacementItemDto { NodeId = nodeId, ParentWbsId = null, SortOrder = 1, RowVersion = null! }]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION_ERROR", result.Error!.Code, ignoreCase: true);
        _context.ChangeTracker.Clear();
        Assert.Equal(0, (await _context.PlanWbsNodes.SingleAsync(item => item.Id == nodeId)).SortOrder);
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
        _context.ChangeTracker.Clear();
        var planned = await _context.PlanActivities.AsNoTracking().SingleAsync(value => value.Id == activity.Id);

        var result = await _activityService.UpdateAsync(planId, activity.Id, new UpdatePlanActivityDto
        {
            Code = "A1",
            Name = "Executed",
            StartDate = planned.StartDate,
            EndDate = planned.EndDate,
            Duration = planned.Duration,
            SchedulingMode = planned.SchedulingMode,
            ActualStart = new DateTime(2026, 1, 2),
            ActualFinish = new DateTime(2026, 1, 6),
            RowVersion = activity.RowVersion
        }, _userA.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new DateTime(2026, 1, 2), result.Value!.ActualStart);
        Assert.Equal(new DateTime(2026, 1, 6), result.Value.ActualFinish);
        var stored = await _context.PlanActivities.SingleAsync(value => value.Id == activity.Id);
        Assert.Equal(planned.StartDate, stored.StartDate);
        Assert.Equal(planned.EndDate, stored.EndDate);
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
    public async Task Delete_SoftDeletesActivityRelationshipsInBothDependencyDirections()
    {
        var planId = await CreatePlanAsync();
        var dependent = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "Dependent" }, _userA.ToString())).Value!;
        var activity = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A2", Name = "To delete" }, _userA.ToString())).Value!;
        var predecessor = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A3", Name = "Predecessor" }, _userA.ToString())).Value!;
        var versionId = _context.PlanVersions.Single(version => version.PlanId == planId).Id;

        _context.PlanDependencies.AddRange(
            new PlanDependency
            {
                TenantId = _tenantA,
                PlanVersionId = versionId,
                ActivityId = dependent.Id,
                PredecessorActivityId = activity.Id,
                CreatedBy = _userA.ToString()
            },
            new PlanDependency
            {
                TenantId = _tenantA,
                PlanVersionId = versionId,
                ActivityId = activity.Id,
                PredecessorActivityId = predecessor.Id,
                CreatedBy = _userA.ToString()
            });
        _context.PlanAssignments.Add(new PlanAssignment
        {
            TenantId = _tenantA,
            PlanVersionId = versionId,
            ActivityId = activity.Id,
            ResourceId = Guid.NewGuid(),
            CreatedBy = _userA.ToString()
        });
        await _context.SaveChangesAsync();

        var currentRowVersion = await _context.PlanActivities
            .Where(item => item.Id == activity.Id)
            .Select(item => item.RowVersion)
            .SingleAsync();
        var result = await _activityService.DeleteAsync(planId, activity.Id, currentRowVersion, _userA.ToString());

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True((await _context.PlanActivities.IgnoreQueryFilters().SingleAsync(item => item.Id == activity.Id)).IsDeleted);
        var storedDependencies = await _context.PlanDependencies.IgnoreQueryFilters()
            .Where(dependency => dependency.PlanVersionId == versionId)
            .ToListAsync();
        Assert.Equal(2, storedDependencies.Count);
        Assert.All(storedDependencies, dependency => Assert.True(dependency.IsDeleted));
        var storedAssignment = await _context.PlanAssignments.IgnoreQueryFilters()
            .SingleAsync(assignment => assignment.PlanVersionId == versionId && assignment.ActivityId == activity.Id);
        Assert.True(storedAssignment.IsDeleted);
        var remainingActivities = await _context.PlanActivities.Where(item => item.PlanVersionId == versionId).ToListAsync();
        Assert.Contains(remainingActivities, item => item.Id == dependent.Id);
        Assert.Contains(remainingActivities, item => item.Id == predecessor.Id);
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
    public async Task BulkUpdate_InvalidMemberRollsBackEarlierActivityUpdates()
    {
        var planId = await CreatePlanAsync();
        var first = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "First" }, _userA.ToString())).Value!;
        var second = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A2", Name = "Second" }, _userA.ToString())).Value!;
        UpdatePlanActivityDto Update(PlanActivityDto activity, string name, int duration) => new()
        {
            Code = activity.Code, Name = name, Type = activity.Type, SchedulingMode = activity.SchedulingMode,
            ConstraintType = activity.ConstraintType, StartDate = activity.StartDate, EndDate = activity.EndDate,
            ActualStart = activity.ActualStart, ActualFinish = activity.ActualFinish, CalendarId = activity.CalendarId,
            Duration = duration, EstimateHours = activity.EstimateHours, Progress = activity.Progress, WbsId = activity.WbsId,
            SortOrder = activity.SortOrder, RowVersion = activity.RowVersion
        };

        var result = await _activityService.BulkUpdateAsync(planId, new BulkUpdatePlanActivitiesDto
        {
            Items =
            [
                new BulkUpdatePlanActivityItemDto { ActivityId = first.Id, Update = Update(first, "Changed", first.Duration) },
                new BulkUpdatePlanActivityItemDto { ActivityId = second.Id, Update = Update(second, "Second", -1) }
            ]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        var storedFirst = await _context.PlanActivities.AsNoTracking().SingleAsync(item => item.Id == first.Id);
        Assert.Equal("First", storedFirst.Name);
    }

    [Fact]
    public async Task BulkUpdate_StaleMemberRollsBackEarlierActivityUpdates()
    {
        var planId = await CreatePlanAsync();
        var first = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A1", Name = "First" }, _userA.ToString())).Value!;
        var second = (await _activityService.CreateAsync(planId, new CreatePlanActivityDto { Code = "A2", Name = "Second" }, _userA.ToString())).Value!;
        UpdatePlanActivityDto Update(PlanActivityDto activity, string name, byte[]? rowVersion = null) => new()
        {
            Code = activity.Code, Name = name, Type = activity.Type, SchedulingMode = activity.SchedulingMode,
            ConstraintType = activity.ConstraintType, StartDate = activity.StartDate, EndDate = activity.EndDate,
            ActualStart = activity.ActualStart, ActualFinish = activity.ActualFinish, CalendarId = activity.CalendarId,
            Duration = activity.Duration, EstimateHours = activity.EstimateHours, Progress = activity.Progress, WbsId = activity.WbsId,
            SortOrder = activity.SortOrder, RowVersion = rowVersion ?? activity.RowVersion
        };

        var result = await _activityService.BulkUpdateAsync(planId, new BulkUpdatePlanActivitiesDto
        {
            Items =
            [
                new BulkUpdatePlanActivityItemDto { ActivityId = first.Id, Update = Update(first, "Changed") },
                new BulkUpdatePlanActivityItemDto { ActivityId = second.Id, Update = Update(second, "Second", new byte[8]) }
            ]
        }, _userA.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal("CONFLICT", result.Error!.Code, ignoreCase: true);
        var storedFirst = await _context.PlanActivities.AsNoTracking().SingleAsync(item => item.Id == first.Id);
        Assert.Equal("First", storedFirst.Name);
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
