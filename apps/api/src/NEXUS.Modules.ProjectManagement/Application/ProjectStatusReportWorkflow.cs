using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

public static class ProjectStatusReportWorkflow
{
    public const string EntityType = "ProjectStatusReport";
    public const string DefinitionCode = "PROJECT_STATUS_REPORT_WEEKLY";
    public const string Generate = "GENERATE";
    public const string Submit = "SUBMIT";
    public const string Approve = "APPROVE";
    public const string Return = "RETURN";
    public const string Reopen = "REOPEN";
    public const string Publish = "PUBLISH";
    private static readonly (string Code, string From, string To)[] RequiredTransitions =
    [
        (Publish, "Draft", "Published")
    ];
    private static readonly (string Code, string From, string To)[] LegacyTransitions =
    [
        (Generate, "Draft", "Generated"),
        (Submit, "Generated", "Submitted"),
        (Approve, "Submitted", "Approved"),
        (Return, "Submitted", "Returned"),
        (Reopen, "Returned", "Draft"),
        (Publish, "Approved", "Published")
    ];

    public static bool IsExpectedTransition(string code, string from, string to) => RequiredTransitions.Any(item =>
        item.Code.Equals(code, StringComparison.OrdinalIgnoreCase)
        && item.From.Equals(from, StringComparison.OrdinalIgnoreCase)
        && item.To.Equals(to, StringComparison.OrdinalIgnoreCase));

    public static bool HasConfiguredSections(string? performanceSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(performanceSnapshotJson)) return false;
        try
        {
            using var document = JsonDocument.Parse(performanceSnapshotJson);
            var root = document.RootElement;
            if (!root.TryGetProperty(nameof(ProjectPerformanceDto.IncludedReportSections), out var sections)
                && !root.TryGetProperty("includedReportSections", out sections))
                return true; // A legacy snapshot predates section configuration and remains publishable.
            if (sections.ValueKind != JsonValueKind.Array) return false;
            var included = sections.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return included.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static Result ValidateWorkflowVersion(WorkflowDefinitionVersion version)
    {
        var requiredStates = new[] { "Draft", "Published" };
        var activeStates = version.States.Where(item => item.IsActive && !item.IsDeleted).ToList();
        if (activeStates.Any(item => !requiredStates.Contains(item.Code, StringComparer.OrdinalIgnoreCase))
            || requiredStates.Any(expected => activeStates.Count(item => item.Code.Equals(expected, StringComparison.OrdinalIgnoreCase)) != 1))
            return Result.Failure(Error.Validation("The status-report workflow must contain each supported state exactly once."));
        if (activeStates.Count(item => item.IsInitial) != 1
            || activeStates.Count(item => item.IsInitial && item.Code.Equals("Draft", StringComparison.OrdinalIgnoreCase)) != 1)
            return Result.Failure(Error.Validation("The status-report workflow must have exactly one initial Draft state."));
        if (activeStates.Count(item => item.IsFinal) != 1
            || activeStates.Count(item => item.IsFinal && item.Code.Equals("Published", StringComparison.OrdinalIgnoreCase)) != 1)
            return Result.Failure(Error.Validation("Published must be the only final status-report workflow state."));

        var activeTransitions = version.Transitions.Where(item => item.IsActive && !item.IsDeleted).ToList();
        if (activeTransitions.Count != RequiredTransitions.Length || RequiredTransitions.Any(expected =>
            activeTransitions.Count(item => item.Code.Equals(expected.Code, StringComparison.OrdinalIgnoreCase)
                && item.FromState.Code.Equals(expected.From, StringComparison.OrdinalIgnoreCase)
                && item.ToState.Code.Equals(expected.To, StringComparison.OrdinalIgnoreCase)) != 1))
            return Result.Failure(Error.Validation("The status-report workflow must contain only the Draft-to-Published transition."));
        return Result.Success();
    }

    public static bool IsLegacyWorkflowVersion(WorkflowDefinitionVersion version)
    {
        var legacyStates = new[] { "Draft", "Generated", "Submitted", "Approved", "Returned", "Published" };
        var states = version.States.Where(item => item.IsActive && !item.IsDeleted).ToList();
        if (states.Count != legacyStates.Length || legacyStates.Any(code => states.Count(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) != 1))
            return false;
        if (states.Count(item => item.IsInitial) != 1 || !states.Any(item => item.IsInitial && item.Code.Equals("Draft", StringComparison.OrdinalIgnoreCase)))
            return false;
        if (states.Count(item => item.IsFinal) != 1 || !states.Any(item => item.IsFinal && item.Code.Equals("Published", StringComparison.OrdinalIgnoreCase)))
            return false;
        var transitions = version.Transitions.Where(item => item.IsActive && !item.IsDeleted).ToList();
        return transitions.Count == LegacyTransitions.Length && LegacyTransitions.All(expected => transitions.Count(item =>
            item.Code.Equals(expected.Code, StringComparison.OrdinalIgnoreCase)
            && item.FromState.Code.Equals(expected.From, StringComparison.OrdinalIgnoreCase)
            && item.ToState.Code.Equals(expected.To, StringComparison.OrdinalIgnoreCase)) == 1);
    }
}

public sealed record ProjectStatusReportWorkflowDefinition(Guid DefinitionId, Guid VersionId);

public interface IProjectStatusReportWorkflowProvisioningService
{
    Task<Result<ProjectStatusReportWorkflowDefinition>> EnsureAsync(Guid tenantId, string actor, CancellationToken cancellationToken = default);
}

public sealed class ProjectStatusReportWorkflowProvisioningService(
    DbContext context,
    ILogger<ProjectStatusReportWorkflowProvisioningService> logger) : IProjectStatusReportWorkflowProvisioningService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TenantProvisioningGates = new();
    private static readonly (string Code, string NameEn, string NameAr, bool IsFinal)[] States =
    [
        ("Draft", "Draft", "مسودة", false),
        ("Published", "Published", "منشور", true)
    ];

    private static readonly (string Code, string From, string To, string NameEn, string NameAr)[] Transitions =
    [
        (ProjectStatusReportWorkflow.Publish, "Draft", "Published", "Publish report", "نشر التقرير")
    ];

    private const int MaxProvisioningRecoveryAttempts = 4;

    public async Task<Result<ProjectStatusReportWorkflowDefinition>> EnsureAsync(Guid tenantId, string actor, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return await EnsureAsyncCore(tenantId, actor, cancellationToken, recoveryAttempt: 0);

        // Serialize first-time/legacy workflow upgrades for this tenant across API requests
        // in this process. Database concurrency recovery below covers multiple API instances.
        var gate = TenantProvisioningGates.GetOrAdd(tenantId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await EnsureAsyncCore(tenantId, actor, cancellationToken, recoveryAttempt: 0);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<Result<ProjectStatusReportWorkflowDefinition>> EnsureAsyncCore(Guid tenantId, string actor, CancellationToken cancellationToken, int recoveryAttempt)
    {
        if (tenantId == Guid.Empty) return Result<ProjectStatusReportWorkflowDefinition>.Failure(Error.Validation("A valid tenant is required to provision the status-report workflow."));
        // Existing published configurations are common. Avoid taking a write lock for
        // that path; if provisioning is required, every provisioning read is repeated
        // after the cross-process lock has been acquired below.
        var currentDefinition = await GetCurrentValidDefinitionAsync(tenantId, cancellationToken);
        if (currentDefinition is not null)
            return Result<ProjectStatusReportWorkflowDefinition>.Success(currentDefinition);

        IDbContextTransaction? ownedTransaction = null;
        WorkflowDefinition? definition = null;
        WorkflowDefinitionVersion? version = null;
        var createdDefinition = false;
        var attemptEntities = new List<object>();
        try
        {
            if (context.Database.CurrentTransaction is null)
                ownedTransaction = await context.Database.BeginTransactionAsync(cancellationToken);

            // The transaction-owned SQL application lock must precede the reads that
            // choose the definition row and next version number. Otherwise another API
            // instance can commit while this request waits, leaving it with stale data.
            var databaseLock = await AcquireDatabaseProvisioningLockAsync(tenantId, cancellationToken);
            if (databaseLock < 0)
            {
                return Result<ProjectStatusReportWorkflowDefinition>.Failure(
                    Error.Conflict("The status-report workflow is busy in another server process. Wait a moment and retry publishing."));
            }

            // Recheck after waiting for the lock. A competing API instance may have
            // completed provisioning while this request was queued.
            currentDefinition = await GetCurrentValidDefinitionAsync(tenantId, cancellationToken);
            if (currentDefinition is not null)
            {
                if (ownedTransaction is not null)
                    await ownedTransaction.CommitAsync(cancellationToken);
                return Result<ProjectStatusReportWorkflowDefinition>.Success(currentDefinition);
            }

            var definitionSnapshot = await context.Set<WorkflowDefinition>().AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.EntityType == ProjectStatusReportWorkflow.EntityType
                    && item.Code == ProjectStatusReportWorkflow.DefinitionCode && !item.IsDeleted, cancellationToken);
            if (definitionSnapshot is { IsActive: false })
                return Result<ProjectStatusReportWorkflowDefinition>.Failure(Error.Conflict("The tenant's project status-report workflow is disabled."));

            if (definitionSnapshot?.ActivePublishedVersionId is Guid activeVersionId)
            {
                var activeVersion = await context.Set<WorkflowDefinitionVersion>().AsNoTracking()
                    .Include(item => item.States).Include(item => item.Transitions).ThenInclude(item => item.FromState)
                    .Include(item => item.Transitions).ThenInclude(item => item.ToState)
                    .FirstOrDefaultAsync(item => item.Id == activeVersionId && item.WorkflowDefinitionId == definitionSnapshot.Id
                        && item.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Published && !item.IsDeleted, cancellationToken);
                if (activeVersion is not null)
                {
                    var validation = ValidateVersion(activeVersion);
                    if (!validation.IsSuccess && !ProjectStatusReportWorkflow.IsLegacyWorkflowVersion(activeVersion))
                        return Result<ProjectStatusReportWorkflowDefinition>.Failure(validation.Error!);
                }
            }

            // The version number is also selected while holding the tenant lock, so
            // concurrent instances cannot both construct the same next version.
            var existingVersionNumbers = definitionSnapshot is null ? new List<int>() : await context.Set<WorkflowDefinitionVersion>()
                .Where(item => item.WorkflowDefinitionId == definitionSnapshot.Id && !item.IsDeleted)
                .Select(item => item.VersionNumber).ToListAsync(cancellationToken);
            version = new WorkflowDefinitionVersion
            {
                TenantId = tenantId,
                VersionNumber = existingVersionNumbers.DefaultIfEmpty(0).Max() + 1,
                LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Published,
                ChangeLog = "Provisioned the simplified Draft-to-Published status-report workflow.",
                PublishedAt = DateTime.UtcNow,
                PublishedBy = actor,
                CreatedBy = actor
            };
            attemptEntities.Add(version);

            var stateByCode = new Dictionary<string, WorkflowState>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < States.Length; index++)
            {
                var item = States[index];
                var state = new WorkflowState
                {
                    TenantId = tenantId, WorkflowDefinitionVersionId = version.Id, Code = item.Code,
                    NameEn = item.NameEn, NameAr = item.NameAr, DisplayOrder = index + 1,
                    IsInitial = item.Code == "Draft", IsFinal = item.IsFinal, IsActive = true, CreatedBy = actor
                };
                version.States.Add(state);
                attemptEntities.Add(state);
                stateByCode.Add(state.Code, state);
            }
            foreach (var item in Transitions)
            {
                var transition = new WorkflowTransition
                {
                    TenantId = tenantId, WorkflowDefinitionVersionId = version.Id, Code = item.Code,
                    NameEn = item.NameEn, NameAr = item.NameAr,
                    FromStateId = stateByCode[item.From].Id, ToStateId = stateByCode[item.To].Id,
                    DisplayOrder = Array.IndexOf(Transitions, item) + 1, IsActive = true, CreatedBy = actor
                };
                var action = new WorkflowTransitionAction
                {
                    TenantId = tenantId, WorkflowTransitionId = transition.Id, ActionType = "DomainProjection",
                    ConfigurationJson = "{}", ExecutionOrder = 10, IsActive = true, CreatedBy = actor
                };
                transition.Actions.Add(action);
                version.Transitions.Add(transition);
                attemptEntities.Add(transition);
                attemptEntities.Add(action);
            }

            if (definitionSnapshot is null)
            {
                definition = new WorkflowDefinition
                {
                    TenantId = tenantId, Code = ProjectStatusReportWorkflow.DefinitionCode,
                    Name = "Weekly Project Status Report", Description = "Simplified Draft and Published lifecycle for status reports.",
                    Domain = WorkflowDomains.Reporting, EntityType = ProjectStatusReportWorkflow.EntityType,
                    IsActive = true, CreatedBy = actor
                };
                createdDefinition = true;
                attemptEntities.Add(definition);
            }
            else
            {
                definition = context.Set<WorkflowDefinition>().Local.FirstOrDefault(item => item.Id == definitionSnapshot.Id);
                if (definition is null)
                {
                    definition = definitionSnapshot;
                    context.Attach(definition);
                }
                else
                {
                    // A scoped DbContext can retain a definition read before this lock.
                    // Refresh it so its row version and active-version pointer are current.
                    await context.Entry(definition).ReloadAsync(cancellationToken);
                }
            }

            definition.Versions.Add(version);

            if (createdDefinition)
            {
                // Track the complete new workflow graph. Adding only children to an
                // untracked definition leaves SaveChanges with nothing to persist.
                definition.ActivePublishedVersionId = null;
                definition.ActivePublishedVersion = null;
                context.Set<WorkflowDefinition>().Add(definition);
            }
            else
            {
                // BaseEntity assigns GUID keys before tracking. Discovering this new
                // graph through an existing definition can mark it Modified and issue
                // UPDATEs for rows that do not exist. Explicit Add tracks the entire
                // version/state/transition/action graph as new while retaining the
                // existing definition's optimistic concurrency token.
                version.WorkflowDefinitionId = definition.Id;
                context.Set<WorkflowDefinitionVersion>().Add(version);
                definition.ActivePublishedVersionId = version.Id;
                definition.ActivePublishedVersion = version;
                definition.UpdatedAt = DateTime.UtcNow;
                definition.UpdatedBy = actor;
            }

            await context.SaveChangesAsync(cancellationToken);
            if (createdDefinition)
            {
                // A new definition and its version reference each other. Persist the
                // definition/version graph first, then set the optional active pointer.
                definition.ActivePublishedVersionId = version.Id;
                definition.ActivePublishedVersion = version;
                definition.UpdatedAt = DateTime.UtcNow;
                definition.UpdatedBy = actor;
                await context.SaveChangesAsync(cancellationToken);
            }
            if (ownedTransaction is not null)
                await ownedTransaction.CommitAsync(cancellationToken);

            return Result<ProjectStatusReportWorkflowDefinition>.Success(new(definition.Id, version.Id));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception,
                "Status-report workflow provisioning conflict for tenant {TenantId}, attempt {Attempt}/{Attempts}. Conflicting entities: {Entities}",
                tenantId, recoveryAttempt + 1, MaxProvisioningRecoveryAttempts + 1,
                string.Join(", ", exception.Entries.Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.State}")));
            if (ownedTransaction is null)
                return Result<ProjectStatusReportWorkflowDefinition>.Failure(
                    Error.Conflict("The status-report workflow changed while publishing. Refresh and retry."));

            await ownedTransaction.RollbackAsync(CancellationToken.None);
            await CleanupFailedProvisioningAttemptAsync(definition, createdDefinition, attemptEntities);
            await ownedTransaction.DisposeAsync();
            ownedTransaction = null;

            var concurrentDefinition = await GetCurrentValidDefinitionAsync(tenantId, cancellationToken);
            if (concurrentDefinition is not null)
                return Result<ProjectStatusReportWorkflowDefinition>.Success(concurrentDefinition);

            if (recoveryAttempt < MaxProvisioningRecoveryAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(50 * (1 << recoveryAttempt));
                await Task.Delay(delay, cancellationToken);
                return await EnsureAsyncCore(tenantId, actor, cancellationToken, recoveryAttempt + 1);
            }

            return Result<ProjectStatusReportWorkflowDefinition>.Failure(
                Error.Conflict("The status-report workflow is still being updated. Wait a moment, refresh, and retry publishing."));
        }
        catch (DbUpdateException)
        {
            if (ownedTransaction is null)
                throw;

            // The application lock normally prevents duplicate inserts. Keep bounded
            // recovery for a competing writer that does not participate in that lock,
            // but preserve unrelated persistence failures as server errors.
            await ownedTransaction.RollbackAsync(CancellationToken.None);
            await CleanupFailedProvisioningAttemptAsync(definition, createdDefinition, attemptEntities);
            await ownedTransaction.DisposeAsync();
            ownedTransaction = null;

            var concurrentDefinition = await GetCurrentValidDefinitionAsync(tenantId, cancellationToken);
            if (concurrentDefinition is not null)
                return Result<ProjectStatusReportWorkflowDefinition>.Success(concurrentDefinition);

            if (recoveryAttempt < MaxProvisioningRecoveryAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(50 * (1 << recoveryAttempt));
                await Task.Delay(delay, cancellationToken);
                return await EnsureAsyncCore(tenantId, actor, cancellationToken, recoveryAttempt + 1);
            }

            throw;
        }
        catch
        {
            if (ownedTransaction is not null)
                await ownedTransaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
                await ownedTransaction.DisposeAsync();
        }
    }

    private async Task CleanupFailedProvisioningAttemptAsync(
        WorkflowDefinition? definition,
        bool createdDefinition,
        IReadOnlyCollection<object> attemptEntities)
    {
        // Detach dependents before their principals. Removing a WorkflowDefinitionVersion
        // from the required Versions collection first creates an EF conceptual null and
        // can mask the concurrency error that this cleanup is recovering from.
        var entitiesToDetach = attemptEntities.OfType<WorkflowTransitionAction>().Cast<object>()
            .Concat(attemptEntities.OfType<WorkflowTransition>().Cast<object>())
            .Concat(attemptEntities.OfType<WorkflowState>().Cast<object>())
            .Concat(attemptEntities.OfType<WorkflowDefinitionVersion>().Cast<object>())
            .ToList();
        var detectChangesEnabled = context.ChangeTracker.AutoDetectChangesEnabled;
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var entity in entitiesToDetach)
            {
                var entry = context.Entry(entity);
                if (entry.State != EntityState.Detached)
                    entry.State = EntityState.Detached;
            }

            if (createdDefinition && definition is not null)
            {
                var entry = context.Entry(definition);
                if (entry.State != EntityState.Detached)
                    entry.State = EntityState.Detached;
            }
        }
        finally
        {
            context.ChangeTracker.AutoDetectChangesEnabled = detectChangesEnabled;
        }

        if (!createdDefinition && definition is not null)
        {
            var entry = context.Entry(definition);
            if (entry.State != EntityState.Detached)
                await entry.ReloadAsync(CancellationToken.None);
        }
    }

    private async Task<int> AcquireDatabaseProvisioningLockAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!string.Equals(context.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            return 0;

        var transaction = context.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("A transaction is required to lock status-report workflow provisioning.");
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 20;
        command.CommandText = """
            DECLARE @lockResult int;
            EXEC @lockResult = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 15000;
            SELECT @lockResult;
            """;
        var resource = command.CreateParameter();
        resource.ParameterName = "@resource";
        resource.Value = $"NEXUS:ProjectStatusReportWorkflow:{tenantId:D}";
        command.Parameters.Add(resource);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<ProjectStatusReportWorkflowDefinition?> GetCurrentValidDefinitionAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var definition = await context.Set<WorkflowDefinition>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.EntityType == ProjectStatusReportWorkflow.EntityType
                && item.Code == ProjectStatusReportWorkflow.DefinitionCode && item.IsActive && !item.IsDeleted)
            .Select(item => new { item.Id, item.ActivePublishedVersionId })
            .FirstOrDefaultAsync(cancellationToken);
        if (definition?.ActivePublishedVersionId is not Guid activeVersionId) return null;

        var activeVersion = await context.Set<WorkflowDefinitionVersion>().AsNoTracking()
            .Include(item => item.States)
            .Include(item => item.Transitions).ThenInclude(item => item.FromState)
            .Include(item => item.Transitions).ThenInclude(item => item.ToState)
            .FirstOrDefaultAsync(item => item.Id == activeVersionId && item.WorkflowDefinitionId == definition.Id
                && item.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Published && !item.IsDeleted, cancellationToken);
        if (activeVersion is null || !ValidateVersion(activeVersion).IsSuccess) return null;

        return new ProjectStatusReportWorkflowDefinition(definition.Id, activeVersion.Id);
    }

    private static Result ValidateVersion(WorkflowDefinitionVersion version)
        => ProjectStatusReportWorkflow.ValidateWorkflowVersion(version);
}

public sealed class ProjectStatusReportWorkflowAdapter(
    DbContext context,
    IHttpContextAccessor httpContextAccessor,
    IManagementContextAccessor managementContext) : IWorkflowEntityAdapter
{
    public string EntityType => ProjectStatusReportWorkflow.EntityType;

    public async Task<Result<WorkflowEntitySnapshot>> GetSnapshotAsync(Guid entityId, WorkflowExecutionContext execution, CancellationToken cancellationToken = default)
    {
        var report = await FindAccessibleAsync(entityId, execution, cancellationToken);
        if (report is null) return Result<WorkflowEntitySnapshot>.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId));
        return Result<WorkflowEntitySnapshot>.Success(new WorkflowEntitySnapshot(EntityType, entityId, new Dictionary<string, object?>
        {
            ["StateCode"] = report.ReportState.ToString(),
            ["StateNameEn"] = report.ReportState.ToString(),
            ["StateNameAr"] = report.ReportState.ToString(),
            ["ProjectId"] = report.ProjectId,
            ["PreparedByUserId"] = report.PreparedByUserId,
            ["HasConfiguredSections"] = ProjectStatusReportWorkflow.HasConfiguredSections(report.PerformanceSnapshotJson),
            ["StatusDate"] = report.StatusDate
        }));
    }

    public async Task<Result> AuthorizeAsync(WorkflowExecutionContext execution, Guid entityId, CancellationToken cancellationToken = default)
    {
        if (!HasPermission("StatusReport.View")) return Result.Failure(Error.Forbidden("Status report access is required."));
        return await FindAccessibleAsync(entityId, execution, cancellationToken) is null
            ? Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId))
            : Result.Success();
    }

    /// <summary>
    /// Publication is gated by the dedicated publish permission; any other draft-lifecycle
    /// transition is a write and requires edit rights. The exact lifecycle is still enforced
    /// by <see cref="ValidateTransitionAsync"/>.
    /// </summary>
    public async Task<Result> AuthorizeTransitionAsync(WorkflowTransitionContext transition, CancellationToken cancellationToken = default)
    {
        var permission = transition.ToState.Code.Equals("Published", StringComparison.OrdinalIgnoreCase)
            ? "StatusReport.Publish"
            : "StatusReport.Edit";
        if (!HasPermission(permission))
            return Result.Failure(Error.Forbidden("You do not have permission for this status-report workflow transition."));
        return await FindAccessibleAsync(transition.EntityId, transition.Execution, cancellationToken) is null
            ? Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, transition.EntityId))
            : Result.Success();
    }

    public Task<Result> ValidateTransitionAsync(WorkflowTransitionContext transition, CancellationToken cancellationToken = default)
    {
        var target = transition.ToState.Code;
        if (!ProjectStatusReportWorkflow.IsExpectedTransition(transition.Transition.Code, transition.FromState.Code, target))
            return Task.FromResult(Result.Failure(Error.Forbidden("This transition is not part of the approved status-report lifecycle.")));
        var permission = target.Equals("Published", StringComparison.OrdinalIgnoreCase) ? "StatusReport.Publish" : null;
        if (permission is null || !HasPermission(permission))
            return Task.FromResult(Result.Failure(Error.Forbidden("You do not have permission for this status-report workflow transition.")));
        if (target.Equals("Published", StringComparison.OrdinalIgnoreCase)
            && (!transition.Entity.Facts.TryGetValue("HasConfiguredSections", out var hasConfiguredSections) || hasConfiguredSections is not true))
            return Task.FromResult(Result.Failure(Error.Conflict("At least one configured report section is required before publication.")));
        return Task.FromResult(Result.Success());
    }

    public async Task<Result> ApplyTransitionAsync(WorkflowTransitionContext transition, CancellationToken cancellationToken = default)
    {
        var report = await FindAccessibleAsync(transition.EntityId, transition.Execution, cancellationToken, tracked: true);
        if (report is null) return Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, transition.EntityId));
        if (!string.Equals(report.ReportState.ToString(), transition.FromState.Code, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(WorkflowErrors.WorkflowDomainStateMismatch(transition.FromState.Code, report.ReportState.ToString()));
        if (!Enum.TryParse<ProjectStatusReportState>(transition.ToState.Code, true, out var newState))
            return Result.Failure(Error.Validation("The target state is not supported by the status-report domain."));

        if (newState == ProjectStatusReportState.Published && string.IsNullOrWhiteSpace(report.PerformanceSnapshotJson))
            return Result.Failure(Error.Conflict("Capture the report snapshot as part of publication."));
        if (newState == ProjectStatusReportState.Published && !ProjectStatusReportWorkflow.HasConfiguredSections(report.PerformanceSnapshotJson))
            return Result.Failure(Error.Conflict("At least one configured report section is required before publication."));
        report.ReportState = newState;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedBy = transition.Execution.UserId;
        if (newState == ProjectStatusReportState.Published)
        {
            report.PublishedAt = report.SourceDataCapturedAt ?? DateTime.UtcNow;
            report.PublishedByUserId = Guid.TryParse(transition.Execution.UserId, out var publisherId) ? publisherId : null;
            report.PublishedByNameSnapshot = transition.Execution.UserName;
        }
        return Result.Success();
    }

    private async Task<ProjectStatusReport?> FindAccessibleAsync(Guid id, WorkflowExecutionContext execution, CancellationToken cancellationToken, bool tracked = false)
    {
        var query = context.Set<ProjectStatusReport>().AsQueryable();
        if (!tracked) query = query.AsNoTracking();
        var report = await query.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted
            && item.Project.TenantId == execution.TenantId && !item.Project.IsDeleted, cancellationToken);
        if (report is null || execution.TenantId == Guid.Empty || report.TenantId != execution.TenantId) return null;
        var management = managementContext.Current;
        if (management is null || management.TenantId != report.TenantId) return null;
        return management.Scope.ProjectIds.Contains(report.ProjectId) ? report : null;
    }

    private bool HasPermission(string permission)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true
            && principal.HasClaim("permission", permission);
    }
}

public sealed class ProjectStatusReportWorkflowMetadataProvider : IWorkflowEntityMetadataProvider
{
    public string EntityType => ProjectStatusReportWorkflow.EntityType;
    public WorkflowEntityTypeMetadata GetMetadata() => new(WorkflowDomains.Reporting, EntityType, "Project Status Reports", "تقارير حالة المشروع", new[]
    {
        new WorkflowFactMetadata("ProjectId", "Project", "المشروع", "string"),
        new WorkflowFactMetadata("PreparedByUserId", "Prepared by", "أعده", "string"),
        new WorkflowFactMetadata("HasConfiguredSections", "Has configured report sections", "توجد أقسام تقرير مهيأة", "boolean"),
        new WorkflowFactMetadata("StatusDate", "Status date", "تاريخ الحالة", "date")
    });
}

public sealed class ProjectStatusReportWorkflowConsumerValidator : IWorkflowDefinitionConsumerValidator
{
    public string EntityType => ProjectStatusReportWorkflow.EntityType;
    public Task<Result> ValidateAsync(WorkflowDefinitionVersion version, CancellationToken cancellationToken = default)
        => Task.FromResult(ProjectStatusReportWorkflow.ValidateWorkflowVersion(version));
}
