using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.WorkflowEngine.Application;
using NEXUS.Modules.WorkflowEngine.Application.Actions;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Guards;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using Xunit;
using WorkflowExecutionService = NEXUS.Modules.WorkflowEngine.Application.Services.WorkflowExecutionService;

namespace NEXUS.Api.Tests.Unit;

public sealed class WorkflowExecutionPhase2Tests : IDisposable
{
    private readonly WorkflowTestDbContext _db;
    private readonly TestEntityAdapter _adapter;
    private readonly WorkflowExecutionService _executionService;
    private readonly WorkflowInstanceService _instanceService;
    private readonly WorkflowTransitionQueryService _transitionQueryService;

    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly WorkflowExecutionContext DefaultCtx = new(
        TenantId: TenantId,
        UserId: "user-001",
        UserName: "Test User");

    public WorkflowExecutionPhase2Tests()
    {
        var options = new DbContextOptionsBuilder<WorkflowTestDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new WorkflowTestDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _adapter = new TestEntityAdapter();
        var adapterRegistry = new SingleAdapterRegistry(_adapter);

        var guards = new List<IWorkflowGuardHandler>
        {
            new RequirePermissionGuardHandler(),
            new RequireRoleGuardHandler(),
            new RequireCommentGuardHandler(),
            new RequireFactGuardHandler()
        };
        var actions = new List<IWorkflowActionHandler>
        {
            new DomainProjectionActionHandler(),
            new AuditProjectionActionHandler()
        };

        var configRegistry = new WorkflowConfigurationRegistry(guards, actions);
        var auditService = new AuditService(_db);

        _executionService = new WorkflowExecutionService(_db, adapterRegistry, configRegistry, configRegistry, auditService);
        _instanceService = new WorkflowInstanceService(_db, adapterRegistry);
        _transitionQueryService = new WorkflowTransitionQueryService(_db, adapterRegistry, configRegistry);
    }

    #region Helpers

    private WorkflowDefinition SeedWorkflow(
        string code = "TEST-WF",
        string entityType = "TestEntity",
        WorkflowDefinitionVersionLifecycleStatus status = WorkflowDefinitionVersionLifecycleStatus.Published)
    {
        var def = new WorkflowDefinition
        {
            Code = code,
            Name = $"Test Workflow {code}",
            EntityType = entityType,
            IsActive = true
        };
        _db.Set<WorkflowDefinition>().Add(def);
        _db.SaveChanges();

        var version = new WorkflowDefinitionVersion
        {
            WorkflowDefinitionId = def.Id,
            VersionNumber = 1,
            LifecycleStatus = status,
            PublishedAt = status == WorkflowDefinitionVersionLifecycleStatus.Published ? DateTime.UtcNow : null,
            PublishedBy = status == WorkflowDefinitionVersionLifecycleStatus.Published ? "admin" : null
        };
        _db.Set<WorkflowDefinitionVersion>().Add(version);
        _db.SaveChanges();

        var openState = new WorkflowState
        {
            WorkflowDefinitionVersionId = version.Id,
            Code = "Open",
            NameEn = "Open",
            NameAr = "Open",
            DisplayOrder = 1,
            IsInitial = true,
            IsFinal = false,
            IsActive = true
        };
        var closedState = new WorkflowState
        {
            WorkflowDefinitionVersionId = version.Id,
            Code = "Closed",
            NameEn = "Closed",
            NameAr = "Closed",
            DisplayOrder = 2,
            IsInitial = false,
            IsFinal = true,
            IsActive = true
        };
        _db.Set<WorkflowState>().Add(openState);
        _db.Set<WorkflowState>().Add(closedState);
        _db.SaveChanges();

        var transition = new WorkflowTransition
        {
            WorkflowDefinitionVersionId = version.Id,
            Code = "Close",
            NameEn = "Close",
            NameAr = "Close",
            FromStateId = openState.Id,
            ToStateId = closedState.Id,
            DisplayOrder = 1,
            IsActive = true
        };
        _db.Set<WorkflowTransition>().Add(transition);
        _db.SaveChanges();

        def.ActivePublishedVersionId = version.Id;
        _db.SaveChanges();

        return def;
    }

    private async Task<WorkflowInstance> ProvisionAndGetInstance(
        string entityType = "TestEntity",
        Guid? entityId = null)
    {
        var eid = entityId ?? Guid.NewGuid();
        _adapter.AddEntity(eid, "Open", "Open", "Open");
        var result = await _instanceService.ProvisionAsync(entityType, eid, "TEST-WF", DefaultCtx);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return _db.Set<WorkflowInstance>().Include(i => i.CurrentState).First(i => i.EntityId == eid);
    }

    #endregion

    #region 1. Provision_FromPublishedVersion_Succeeds

    [Fact]
    public async Task Provision_FromPublishedVersion_Succeeds()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        Assert.NotNull(instance);
        Assert.True(instance.IsActive);
    }

    #endregion

    #region 2. Provision_InitialState_IsOpen

    [Fact]
    public async Task Provision_InitialState_IsOpen()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        Assert.Equal("Open", instance.CurrentState.Code);
    }

    #endregion

    #region 3. Provision_DuplicateInstance_Rejected

    [Fact]
    public async Task Provision_DuplicateInstance_Rejected()
    {
        SeedWorkflow();
        var eid = Guid.NewGuid();
        _adapter.AddEntity(eid, "Open", "Open", "Open");
        var r1 = await _instanceService.ProvisionAsync("TestEntity", eid, "TEST-WF", DefaultCtx);
        Assert.True(r1.IsSuccess);
        var r2 = await _instanceService.ProvisionAsync("TestEntity", eid, "TEST-WF", DefaultCtx);
        Assert.True(r2.IsFailure);
        Assert.Contains("already exists", r2.Error!.Message);
    }

    #endregion

    #region 4. Provision_DraftVersion_Rejected

    [Fact]
    public async Task Provision_DraftVersion_Rejected()
    {
        SeedWorkflow(status: WorkflowDefinitionVersionLifecycleStatus.Draft);
        var eid = Guid.NewGuid();
        _adapter.AddEntity(eid, "Open", "Open", "Open");
        var result = await _instanceService.ProvisionAsync("TestEntity", eid, "TEST-WF", DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("draft", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 5. Provision_RetiredVersion_Rejected

    [Fact]
    public async Task Provision_RetiredVersion_Rejected()
    {
        SeedWorkflow(status: WorkflowDefinitionVersionLifecycleStatus.Retired);
        var eid = Guid.NewGuid();
        _adapter.AddEntity(eid, "Open", "Open", "Open");
        var result = await _instanceService.ProvisionAsync("TestEntity", eid, "TEST-WF", DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("retired", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 6. AvailableTransitions_OnlyFromCurrentState

    [Fact]
    public async Task AvailableTransitions_OnlyFromCurrentState()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        var result = await _transitionQueryService.GetAvailableTransitionsAsync("TestEntity", instance.EntityId, DefaultCtx);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(result.Value!.Transitions);
        Assert.Equal("Close", result.Value.Transitions[0].Code);
    }

    #endregion

    #region 7. ExecuteTransition_Successful

    [Fact]
    public async Task ExecuteTransition_Successful()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Closed", result.Value!.ToStateCode);
    }

    #endregion

    #region 8. ExecuteTransition_HistoryWrittenOnce

    [Fact]
    public async Task ExecuteTransition_HistoryWrittenOnce()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        var historyCount = await _db.Set<WorkflowTransitionHistory>().CountAsync(h => h.WorkflowInstanceId == instance.Id);
        Assert.Equal(1, historyCount);
    }

    #endregion

    #region 9. ExecuteTransition_AuditAppendedOnce

    [Fact]
    public async Task ExecuteTransition_AuditAppendedOnce()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        var auditCount = await _db.Set<AuditEntry>().CountAsync(a => a.Action == "Transition");
        Assert.Equal(1, auditCount);
    }

    #endregion

    #region 10. Guard_RejectsTransition_WhenCommentRequired

    [Fact]
    public async Task Guard_RejectsTransition_WhenCommentRequired()
    {
        var def = SeedWorkflow();
        var version = _db.Set<WorkflowDefinitionVersion>().First(v => v.WorkflowDefinitionId == def.Id);
        var transition = _db.Set<WorkflowTransition>().First(t => t.WorkflowDefinitionVersionId == version.Id);

        transition.Guards.Add(new WorkflowTransitionGuard
        {
            WorkflowTransitionId = transition.Id,
            GuardType = "RequireComment",
            ConfigurationJson = "{\"minLength\":1}",
            ExecutionOrder = 1,
            IsActive = true
        });
        _db.SaveChanges();

        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("Comment", result.Error!.Message);
    }

    #endregion

    #region 11. Guard_PassesRequireComment_WhenCommentProvided

    [Fact]
    public async Task Guard_PassesRequireComment_WhenCommentProvided()
    {
        var def = SeedWorkflow();
        var version = _db.Set<WorkflowDefinitionVersion>().First(v => v.WorkflowDefinitionId == def.Id);
        var transition = _db.Set<WorkflowTransition>().First(t => t.WorkflowDefinitionVersionId == version.Id);

        transition.Guards.Add(new WorkflowTransitionGuard
        {
            WorkflowTransitionId = transition.Id,
            GuardType = "RequireComment",
            ConfigurationJson = "{\"minLength\":1}",
            ExecutionOrder = 1,
            IsActive = true
        });
        _db.SaveChanges();

        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close", Comment: "Approved");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    #endregion

    #region 12. Guard_RejectsRequirePermission_WhenNotGranted

    [Fact]
    public async Task Guard_RejectsRequirePermission_WhenNotGranted()
    {
        var def = SeedWorkflow();
        var version = _db.Set<WorkflowDefinitionVersion>().First(v => v.WorkflowDefinitionId == def.Id);
        var transition = _db.Set<WorkflowTransition>().First(t => t.WorkflowDefinitionVersionId == version.Id);

        transition.Guards.Add(new WorkflowTransitionGuard
        {
            WorkflowTransitionId = transition.Id,
            GuardType = "RequirePermission",
            ConfigurationJson = "{\"permission\":\"workflow.close\"}",
            ExecutionOrder = 1,
            IsActive = true
        });
        _db.SaveChanges();

        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 13. Guard_RejectsRequireRole_WhenNotGranted

    [Fact]
    public async Task Guard_RejectsRequireRole_WhenNotGranted()
    {
        var def = SeedWorkflow();
        var version = _db.Set<WorkflowDefinitionVersion>().First(v => v.WorkflowDefinitionId == def.Id);
        var transition = _db.Set<WorkflowTransition>().First(t => t.WorkflowDefinitionVersionId == version.Id);

        transition.Guards.Add(new WorkflowTransitionGuard
        {
            WorkflowTransitionId = transition.Id,
            GuardType = "RequireRole",
            ConfigurationJson = "{\"role\":\"Admin\"}",
            ExecutionOrder = 1,
            IsActive = true
        });
        _db.SaveChanges();

        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("role", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 14. Guard_RejectsRequireFact_WhenFactMissing

    [Fact]
    public async Task Guard_RejectsRequireFact_WhenFactMissing()
    {
        var def = SeedWorkflow();
        var version = _db.Set<WorkflowDefinitionVersion>().First(v => v.WorkflowDefinitionId == def.Id);
        var transition = _db.Set<WorkflowTransition>().First(t => t.WorkflowDefinitionVersionId == version.Id);

        transition.Guards.Add(new WorkflowTransitionGuard
        {
            WorkflowTransitionId = transition.Id,
            GuardType = "RequireFact",
            ConfigurationJson = "{\"fact\":\"HasResolution\",\"operator\":\"truthy\"}",
            ExecutionOrder = 1,
            IsActive = true
        });
        _db.SaveChanges();

        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("HasResolution", result.Error!.Message);
    }

    #endregion

    #region 15. Guard_UnknownGuardType_Rejected

    [Fact]
    public async Task Guard_UnknownGuardType_Rejected()
    {
        var def = SeedWorkflow();
        var version = _db.Set<WorkflowDefinitionVersion>().First(v => v.WorkflowDefinitionId == def.Id);
        var transition = _db.Set<WorkflowTransition>().First(t => t.WorkflowDefinitionVersionId == version.Id);

        transition.Guards.Add(new WorkflowTransitionGuard
        {
            WorkflowTransitionId = transition.Id,
            GuardType = "UnknownGuard",
            ConfigurationJson = "{}",
            ExecutionOrder = 1,
            IsActive = true
        });
        _db.SaveChanges();

        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("Unknown guard type", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 16. Action_UnknownActionType_Rejected

    [Fact]
    public async Task Action_UnknownActionType_Rejected()
    {
        var def = SeedWorkflow();
        var version = _db.Set<WorkflowDefinitionVersion>().First(v => v.WorkflowDefinitionId == def.Id);
        var transition = _db.Set<WorkflowTransition>().First(t => t.WorkflowDefinitionVersionId == version.Id);

        transition.Actions.Add(new WorkflowTransitionAction
        {
            WorkflowTransitionId = transition.Id,
            ActionType = "UnknownAction",
            ConfigurationJson = "{}",
            ExecutionOrder = 1,
            IsActive = true
        });
        _db.SaveChanges();

        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("Unknown action type", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 17. Adapter_AuthorizationFailure_PreventsTransition

    [Fact]
    public async Task Adapter_AuthorizationFailure_PreventsTransition()
    {
        SeedWorkflow();
        var eid = Guid.NewGuid();
        _adapter.AddEntity(eid, "Open", "Open", "Open");
        await _instanceService.ProvisionAsync("TestEntity", eid, "TEST-WF", DefaultCtx);

        var wrongTenantCtx = new WorkflowExecutionContext(Guid.NewGuid(), "user-001", "Test User");
        var request = new WorkflowTransitionRequest("TestEntity", eid, "Close");
        var result = await _executionService.ExecuteTransitionAsync(request, wrongTenantCtx);
        Assert.True(result.IsFailure);
    }

    #endregion

    #region 18. ReadOnly_Rejected

    [Fact]
    public async Task ReadOnly_Rejected()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        var ctx = new WorkflowExecutionContext(TenantId, "user-001", "Test User", ReadOnly: true);
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        var result = await _executionService.ExecuteTransitionAsync(request, ctx);
        Assert.True(result.IsFailure);
        Assert.Contains("read-only", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 19. UnknownEntityType_Rejected

    [Fact]
    public async Task UnknownEntityType_Rejected()
    {
        var result = await _executionService.ExecuteTransitionAsync(
            new WorkflowTransitionRequest("UnknownType", Guid.NewGuid(), "Close"), DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("Unknown entity type", result.Error!.Message);
    }

    #endregion

    #region 20. InvalidTransitionCode_Rejected

    [Fact]
    public async Task InvalidTransitionCode_Rejected()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "NonExistent");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 21. ExpectedStateMismatch_Rejected

    [Fact]
    public async Task ExpectedStateMismatch_Rejected()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close", ExpectedStateCode: "Closed");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("Expected state", result.Error!.Message);
    }

    #endregion

    #region 22. RowVersion_ConcurrencyConflict

    [Fact]
    public async Task RowVersion_ConcurrencyConflict()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        var fakeVersion = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close", ExpectedRowVersion: fakeVersion);
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("changed", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 23. TransitionToFinal_CompletesInstance

    [Fact]
    public async Task TransitionToFinal_CompletesInstance()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsSuccess);

        var updated = _db.Set<WorkflowInstance>().First(i => i.Id == instance.Id);
        Assert.NotNull(updated.CompletedAt);
        Assert.False(updated.IsActive);
    }

    #endregion

    #region 24. FinalState_CannotTransition

    [Fact]
    public async Task FinalState_CannotTransition()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        await _executionService.ExecuteTransitionAsync(new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close"), DefaultCtx);

        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        var result = await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        Assert.True(result.IsFailure);
        Assert.Contains("final", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 25. FailedTransition_CreatesNoHistory

    [Fact]
    public async Task FailedTransition_CreatesNoHistory()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        var request = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "NonExistent");
        await _executionService.ExecuteTransitionAsync(request, DefaultCtx);
        var historyCount = await _db.Set<WorkflowTransitionHistory>().CountAsync(h => h.WorkflowInstanceId == instance.Id);
        Assert.Equal(0, historyCount);
    }

    #endregion

    #region 26. VersionRemainsFixed_DuringInstanceLifetime

    [Fact]
    public async Task VersionRemainsFixed_DuringInstanceLifetime()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        var originalVersionId = instance.WorkflowDefinitionVersionId;
        await _executionService.ExecuteTransitionAsync(new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close"), DefaultCtx);

        var updated = _db.Set<WorkflowInstance>().First(i => i.Id == instance.Id);
        Assert.Equal(originalVersionId, updated.WorkflowDefinitionVersionId);
    }

    #endregion

    #region 27. Guard_ExecutionOrderRespected

    [Fact]
    public async Task Guard_ExecutionOrderRespected()
    {
        var def = SeedWorkflow();
        var version = _db.Set<WorkflowDefinitionVersion>().First(v => v.WorkflowDefinitionId == def.Id);
        var transition = _db.Set<WorkflowTransition>().First(t => t.WorkflowDefinitionVersionId == version.Id);

        transition.Guards.Add(new WorkflowTransitionGuard
        {
            WorkflowTransitionId = transition.Id,
            GuardType = "RequireComment",
            ConfigurationJson = "{\"minLength\":1}",
            ExecutionOrder = 1,
            IsActive = true
        });
        transition.Guards.Add(new WorkflowTransitionGuard
        {
            WorkflowTransitionId = transition.Id,
            GuardType = "RequireComment",
            ConfigurationJson = "{\"minLength\":1}",
            ExecutionOrder = 2,
            IsActive = true
        });
        _db.SaveChanges();

        var instance = await ProvisionAndGetInstance();
        var requestWithoutComment = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close");
        var result1 = await _executionService.ExecuteTransitionAsync(requestWithoutComment, DefaultCtx);
        Assert.True(result1.IsFailure);

        var requestWithComment = new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close", Comment: "Approved");
        var result2 = await _executionService.ExecuteTransitionAsync(requestWithComment, DefaultCtx);
        Assert.True(result2.IsSuccess, result2.Error?.Message);
    }

    #endregion

    #region 28. AvailableTransitions_FinalState_Empty

    [Fact]
    public async Task AvailableTransitions_FinalState_Empty()
    {
        SeedWorkflow();
        var instance = await ProvisionAndGetInstance();
        await _executionService.ExecuteTransitionAsync(new WorkflowTransitionRequest("TestEntity", instance.EntityId, "Close"), DefaultCtx);

        var result = await _transitionQueryService.GetAvailableTransitionsAsync("TestEntity", instance.EntityId, DefaultCtx);
        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsFinalState);
        Assert.Empty(result.Value.Transitions);
    }

    #endregion

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    #region Inner Types

    private sealed class WorkflowTestDbContext : DbContext
    {
        public WorkflowTestDbContext(DbContextOptions options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WorkflowDefinition>(e =>
            {
                e.ToTable("WorkflowDefinitions");
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).HasMaxLength(100).IsRequired();
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
                e.Ignore(x => x.ActivePublishedVersion);
            });
            modelBuilder.Entity<WorkflowDefinitionVersion>(e =>
            {
                e.ToTable("WorkflowDefinitionVersions");
                e.HasKey(x => x.Id);
                e.Property(x => x.LifecycleStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
                e.HasOne(x => x.WorkflowDefinition).WithMany(x => x.Versions).HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<WorkflowState>(e =>
            {
                e.ToTable("WorkflowStates");
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).HasMaxLength(100).IsRequired();
                e.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
                e.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
                e.HasAlternateKey(x => new { x.WorkflowDefinitionVersionId, x.Id });
                e.HasOne(x => x.WorkflowDefinitionVersion).WithMany(x => x.States).HasForeignKey(x => x.WorkflowDefinitionVersionId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<WorkflowTransition>(e =>
            {
                e.ToTable("WorkflowTransitions");
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).HasMaxLength(100).IsRequired();
                e.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
                e.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
                e.HasOne(x => x.WorkflowDefinitionVersion).WithMany(x => x.Transitions).HasForeignKey(x => x.WorkflowDefinitionVersionId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.FromState).WithMany(x => x.OutgoingTransitions).HasForeignKey(x => new { x.WorkflowDefinitionVersionId, x.FromStateId }).HasPrincipalKey(x => new { x.WorkflowDefinitionVersionId, x.Id }).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.ToState).WithMany(x => x.IncomingTransitions).HasForeignKey(x => new { x.WorkflowDefinitionVersionId, x.ToStateId }).HasPrincipalKey(x => new { x.WorkflowDefinitionVersionId, x.Id }).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<WorkflowTransitionGuard>(e =>
            {
                e.ToTable("WorkflowTransitionGuards");
                e.HasKey(x => x.Id);
                e.Property(x => x.GuardType).HasMaxLength(100).IsRequired();
                e.Property(x => x.ConfigurationJson).HasMaxLength(4000).IsRequired();
                e.HasOne(x => x.WorkflowTransition).WithMany(x => x.Guards).HasForeignKey(x => x.WorkflowTransitionId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<WorkflowTransitionAction>(e =>
            {
                e.ToTable("WorkflowTransitionActions");
                e.HasKey(x => x.Id);
                e.Property(x => x.ActionType).HasMaxLength(100).IsRequired();
                e.Property(x => x.ConfigurationJson).HasMaxLength(4000).IsRequired();
                e.HasOne(x => x.WorkflowTransition).WithMany(x => x.Actions).HasForeignKey(x => x.WorkflowTransitionId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<WorkflowInstance>(e =>
            {
                e.ToTable("WorkflowInstances");
                e.HasKey(x => x.Id);
                e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
                e.Property(x => x.StartedBy).HasMaxLength(200).IsRequired();
                e.HasOne(x => x.WorkflowDefinition).WithMany(x => x.Instances).HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.WorkflowDefinitionVersion).WithMany(x => x.Instances).HasForeignKey(x => x.WorkflowDefinitionVersionId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.CurrentState).WithMany().HasForeignKey(x => new { x.WorkflowDefinitionVersionId, x.CurrentStateId }).HasPrincipalKey(x => new { x.WorkflowDefinitionVersionId, x.Id }).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<WorkflowTransitionHistory>(e =>
            {
                e.ToTable("WorkflowTransitionHistory");
                e.HasKey(x => x.Id);
                e.Property(x => x.ExecutedBy).HasMaxLength(200).IsRequired();
                e.Property(x => x.ExecutedByNameSnapshot).HasMaxLength(200);
                e.Property(x => x.Comment).HasMaxLength(4000);
                e.Property(x => x.MetadataJson).HasMaxLength(4000);
                e.Property(x => x.CorrelationId).HasMaxLength(100);
                e.HasOne(x => x.WorkflowInstance).WithMany(x => x.History).HasForeignKey(x => x.WorkflowInstanceId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.WorkflowTransition).WithMany().HasForeignKey(x => x.WorkflowTransitionId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.FromState).WithMany().HasForeignKey(x => x.FromStateId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.ToState).WithMany().HasForeignKey(x => x.ToStateId).OnDelete(DeleteBehavior.NoAction);
            });
            modelBuilder.Entity<AuditEntry>(e => { e.ToTable("AuditEntries"); e.HasKey(x => x.Id); });
        }
    }

    public sealed class TestEntityAdapter : IWorkflowEntityAdapter
    {
        private readonly Dictionary<Guid, TestEntityState> _entities = new();

        public string EntityType => "TestEntity";

        public void AddEntity(Guid id, string stateCode, string stateNameEn, string stateNameAr)
        {
            _entities[id] = new TestEntityState
            {
                Id = id,
                StateCode = stateCode,
                StateNameEn = stateNameEn,
                StateNameAr = stateNameAr,
                HasAssignee = false,
                Type = "Default",
                HasResolution = false
            };
        }

        public Task<Result<WorkflowEntitySnapshot>> GetSnapshotAsync(
            Guid entityId,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (!_entities.TryGetValue(entityId, out var state))
                return Task.FromResult(Result<WorkflowEntitySnapshot>.Failure(
                    WorkflowErrors.EntityNotAccessible("TestEntity", entityId)));

            var facts = new Dictionary<string, object?>
            {
                ["StateCode"] = state.StateCode,
                ["StateNameEn"] = state.StateNameEn,
                ["StateNameAr"] = state.StateNameAr,
                ["HasAssignee"] = state.HasAssignee,
                ["Type"] = state.Type,
                ["HasResolution"] = state.HasResolution
            };

            return Task.FromResult(Result<WorkflowEntitySnapshot>.Success(
                new WorkflowEntitySnapshot("TestEntity", entityId, facts)));
        }

        public Task<Result> AuthorizeAsync(
            WorkflowExecutionContext context,
            Guid entityId,
            CancellationToken cancellationToken = default)
        {
            if (!_entities.ContainsKey(entityId))
                return Task.FromResult(Result.Failure(
                    WorkflowErrors.EntityNotAccessible("TestEntity", entityId)));

            if (context.TenantId != TenantId && context.TenantId != Guid.Empty)
                return Task.FromResult(Result.Failure(WorkflowErrors.CrossTenantAccess));

            return Task.FromResult(Result.Success());
        }

        public Task<Result> ValidateTransitionAsync(
            WorkflowTransitionContext context,
            CancellationToken cancellationToken = default)
        {
            if (!_entities.ContainsKey(context.EntityId))
                return Task.FromResult(Result.Failure(
                    WorkflowErrors.DomainValidationFailed("Entity not found.")));

            return Task.FromResult(Result.Success());
        }

        public Task<Result> ApplyTransitionAsync(
            WorkflowTransitionContext context,
            CancellationToken cancellationToken = default)
        {
            if (_entities.TryGetValue(context.EntityId, out var state))
            {
                state.StateCode = context.ToState.Code;
                state.StateNameEn = context.ToState.NameEn;
                state.StateNameAr = context.ToState.NameAr;
            }

            return Task.FromResult(Result.Success());
        }

        public sealed class TestEntityState
        {
            public Guid Id { get; set; }
            public string StateCode { get; set; } = string.Empty;
            public string StateNameEn { get; set; } = string.Empty;
            public string StateNameAr { get; set; } = string.Empty;
            public bool HasAssignee { get; set; }
            public string Type { get; set; } = string.Empty;
            public bool HasResolution { get; set; }
        }
    }

    public sealed class SingleAdapterRegistry : IWorkflowEntityAdapterRegistry
    {
        private readonly TestEntityAdapter _adapter;

        public SingleAdapterRegistry(TestEntityAdapter adapter) => _adapter = adapter;

        public IWorkflowEntityAdapter? Resolve(string entityType) =>
            entityType == _adapter.EntityType ? _adapter : null;

        public bool IsSupported(string entityType) => entityType == _adapter.EntityType;
    }

    #endregion
}
