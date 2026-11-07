using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Services;

public sealed class WorkflowExecutionService : IWorkflowExecutionService
{
    private readonly DbContext _context;
    private readonly IWorkflowEntityAdapterRegistry _adapterRegistry;
    private readonly IWorkflowGuardHandlerRegistry _guardRegistry;
    private readonly IWorkflowActionHandlerRegistry _actionRegistry;
    private readonly IAuditService _auditService;

    public WorkflowExecutionService(
        DbContext context,
        IWorkflowEntityAdapterRegistry adapterRegistry,
        IWorkflowGuardHandlerRegistry guardRegistry,
        IWorkflowActionHandlerRegistry actionRegistry,
        IAuditService auditService)
    {
        _context = context;
        _adapterRegistry = adapterRegistry;
        _guardRegistry = guardRegistry;
        _actionRegistry = actionRegistry;
        _auditService = auditService;
    }

    public async Task<Result<WorkflowTransitionResult>> ExecuteTransitionAsync(
        WorkflowTransitionRequest request,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.ReadOnly)
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.ReadOnlyViewAs);

        if (request.Metadata?.Count > 50)
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.OversizedMetadata);

        var adapter = _adapterRegistry.Resolve(request.EntityType);
        if (adapter is null)
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.UnknownEntityType(request.EntityType));

        var authorizeResult = await adapter.AuthorizeAsync(context, request.EntityId, cancellationToken);
        if (authorizeResult.IsFailure)
            return Result<WorkflowTransitionResult>.Failure(authorizeResult.Error!);

        var instances = _context.Set<WorkflowInstance>();
        var transitions = _context.Set<WorkflowTransition>();

        var instance = await instances
            .Include(i => i.CurrentState)
            .Include(i => i.WorkflowDefinition)
            .Include(i => i.WorkflowDefinitionVersion)
                .ThenInclude(v => v.Transitions)
                    .ThenInclude(t => t.FromState)
            .Include(i => i.WorkflowDefinitionVersion)
                .ThenInclude(v => v.Transitions)
                    .ThenInclude(t => t.ToState)
            .Include(i => i.WorkflowDefinitionVersion)
                .ThenInclude(v => v.Transitions)
                    .ThenInclude(t => t.Guards)
            .Include(i => i.WorkflowDefinitionVersion)
                .ThenInclude(v => v.Transitions)
                    .ThenInclude(t => t.Actions)
            .FirstOrDefaultAsync(i => i.TenantId == context.TenantId && i.EntityType == request.EntityType
                && i.EntityId == request.EntityId && !i.IsDeleted, cancellationToken);

        if (instance is null)
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.WorkflowInstanceNotFound(request.EntityType, request.EntityId));

        if (instance.WorkflowDefinition.TenantId != context.TenantId
            || instance.WorkflowDefinitionVersion.TenantId != context.TenantId)
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.WorkflowInstanceNotFound(request.EntityType, request.EntityId));

        if (instance.WorkflowDefinitionVersion.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Draft)
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.DraftCannotExecute);

        // A final state rejects every transition except explicitly coded
        // reopens (REOPEN_ prefix). Reopen attempts continue through all the
        // existing checks below — expected state/row version, transition
        // lookup, FromState match, snapshot domain state, adapter
        // authorization and guards — unchanged.
        if (instance.CurrentState.IsFinal
            && request.TransitionCode?.StartsWith(WorkflowTransitionCodes.ReopenPrefix, StringComparison.Ordinal) != true)
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.FinalStateCannotTransition);

        if (request.ExpectedStateCode is not null &&
            !string.Equals(instance.CurrentState.Code, request.ExpectedStateCode, StringComparison.OrdinalIgnoreCase))
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.ExpectedStateMismatch);

        if (request.ExpectedRowVersion is not null &&
            !request.ExpectedRowVersion.SequenceEqual(instance.RowVersion))
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.ConcurrencyConflict);

        var transition = instance.WorkflowDefinitionVersion.Transitions
            .FirstOrDefault(t => t.IsActive && t.Code == request.TransitionCode);

        if (transition is null)
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.TransitionNotFound(request.TransitionCode));

        if (transition.FromStateId != instance.CurrentStateId)
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.TransitionNotAvailable(request.TransitionCode, instance.CurrentState.Code));

        var snapshotResult = await adapter.GetSnapshotAsync(request.EntityId, context, cancellationToken);
        if (snapshotResult.IsFailure)
            return Result<WorkflowTransitionResult>.Failure(snapshotResult.Error!);

        var entitySnapshot = snapshotResult.Value!;

        var domainStateCode = entitySnapshot.Facts.TryGetValue("StateCode", out var stateCodeObj) ? stateCodeObj?.ToString() : null;
        if (domainStateCode is not null && !string.Equals(domainStateCode, instance.CurrentState.Code, StringComparison.OrdinalIgnoreCase))
        {
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.WorkflowDomainStateMismatch(instance.CurrentState.Code, domainStateCode));
        }

        var transitionContext = new WorkflowTransitionContext(
            context with
            {
                Comment = request.Comment ?? context.Comment,
                Metadata = MergeTransitionMetadata(context.Metadata, request.Metadata),
                CorrelationId = request.CorrelationId ?? context.CorrelationId
            },
            request.EntityId,
            entitySnapshot,
            instance.CurrentState,
            transition.ToState,
            transition,
            adapter);

        var transitionAuthorization = await adapter.AuthorizeTransitionAsync(transitionContext, cancellationToken);
        if (transitionAuthorization.IsFailure)
            return Result<WorkflowTransitionResult>.Failure(transitionAuthorization.Error!);

        foreach (var guard in transition.Guards.Where(g => g.IsActive).OrderBy(g => g.ExecutionOrder))
        {
            var handler = _guardRegistry.GetGuardHandler(guard.GuardType);
            if (handler is null)
                return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.UnknownGuardType(guard.GuardType));

            var config = JsonSerializer.Deserialize<JsonElement>(guard.ConfigurationJson);
            var guardResult = await handler.EvaluateAsync(transitionContext, config, cancellationToken);
            if (guardResult.IsFailure)
                return Result<WorkflowTransitionResult>.Failure(guardResult.Error!);
        }

        var validationResult = await adapter.ValidateTransitionAsync(transitionContext, cancellationToken);
        if (validationResult.IsFailure)
            return Result<WorkflowTransitionResult>.Failure(validationResult.Error!);

        var fromState = instance.CurrentState;
        var toState = transition.ToState;

        instance.CurrentStateId = toState.Id;
        instance.UpdatedAt = DateTime.UtcNow;
        instance.UpdatedBy = context.UserId;

        if (toState.IsFinal)
        {
            instance.CompletedAt = DateTime.UtcNow;
            instance.IsActive = false;
        }
        else if (fromState.IsFinal)
        {
            // Reopen: moving out of a final state re-activates the instance.
            instance.IsActive = true;
            instance.CompletedAt = null;
        }

        var activeActions = transition.Actions.Where(a => a.IsActive).OrderBy(a => a.ExecutionOrder).ToList();
        if (!activeActions.Any(action => string.Equals(action.ActionType, "DomainProjection", StringComparison.OrdinalIgnoreCase)))
        {
            var projectionResult = await adapter.ApplyTransitionAsync(transitionContext, cancellationToken);
            if (projectionResult.IsFailure)
                return Result<WorkflowTransitionResult>.Failure(projectionResult.Error!);
        }

        foreach (var action in activeActions)
        {
            var handler = _actionRegistry.GetActionHandler(action.ActionType);
            if (handler is null)
                return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.UnknownActionType(action.ActionType));

            var config = JsonSerializer.Deserialize<JsonElement>(action.ConfigurationJson);
            var actionResult = await handler.ExecuteAsync(transitionContext, config, cancellationToken);
            if (actionResult.IsFailure)
                return Result<WorkflowTransitionResult>.Failure(actionResult.Error!);
        }

        var safeRequestMetadata = GetSafeRequestMetadata(request.Metadata);
        var history = new WorkflowTransitionHistory
        {
            TenantId = context.TenantId,
            WorkflowInstanceId = instance.Id,
            WorkflowTransitionId = transition.Id,
            FromStateId = fromState.Id,
            ToStateId = toState.Id,
            ExecutedBy = context.UserId,
            ExecutedByNameSnapshot = context.UserName,
            ExecutedAt = DateTime.UtcNow,
            CreatedBy = context.UserId,
            Comment = request.Comment,
            MetadataJson = safeRequestMetadata.Count > 0
                ? JsonSerializer.Serialize(safeRequestMetadata)
                : null,
            CorrelationId = request.CorrelationId ?? context.CorrelationId
        };

        _context.Set<WorkflowTransitionHistory>().Add(history);

        var auditEntry = new AuditEntry
        {
            TenantId = context.TenantId,
            EntityType = "WorkflowTransition",
            EntityId = instance.Id,
            Action = "Transition",
            UserId = Guid.TryParse(context.UserId, out var uid) ? uid : null,
            UserName = context.UserName,
            Timestamp = DateTime.UtcNow,
            Changes = JsonSerializer.Serialize(new
            {
                WorkflowDefinition = instance.WorkflowDefinition.Code,
                Version = instance.WorkflowDefinitionVersion.VersionNumber,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                TransitionCode = request.TransitionCode,
                FromState = fromState.Code,
                ToState = toState.Code,
                Executor = context.UserId,
                CorrelationId = request.CorrelationId ?? context.CorrelationId
            })
        };

        var auditAppendResult = await _auditService.AppendAsync(auditEntry, cancellationToken);
        if (auditAppendResult.IsFailure)
            return Result<WorkflowTransitionResult>.Failure(auditAppendResult.Error!);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<WorkflowTransitionResult>.Failure(WorkflowErrors.ConcurrencyConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<WorkflowTransitionResult>.Success(new WorkflowTransitionResult(
            true,
            instance.Id,
            fromState.Code,
            fromState.NameEn,
            fromState.NameAr,
            toState.Code,
            toState.NameEn,
            toState.NameAr,
            null,
            null,
            instance.RowVersion));
    }

    private static IReadOnlyDictionary<string, string?> MergeTransitionMetadata(
        IReadOnlyDictionary<string, string?>? trustedMetadata,
        IReadOnlyDictionary<string, string?>? requestMetadata)
    {
        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (trustedMetadata is not null)
        {
            foreach (var (key, value) in trustedMetadata)
            {
                if (!IsAuthorizationMetadataKey(key))
                    merged[key] = value;
            }
        }

        if (requestMetadata is not null)
        {
            foreach (var (key, value) in requestMetadata)
            {
                if (!IsAuthorizationMetadataKey(key))
                    merged[key] = value;
            }
        }

        SetTrustedAuthorizationMetadata("permissions");
        SetTrustedAuthorizationMetadata("roles");

        return merged;

        void SetTrustedAuthorizationMetadata(string key)
        {
            if (trustedMetadata is null)
                return;

            foreach (var (trustedKey, value) in trustedMetadata)
            {
                if (!string.Equals(trustedKey, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                merged[key] = value;
                return;
            }
        }
    }

    private static IReadOnlyDictionary<string, string?> GetSafeRequestMetadata(
        IReadOnlyDictionary<string, string?>? requestMetadata)
    {
        var safeMetadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (requestMetadata is null)
            return safeMetadata;

        foreach (var (key, value) in requestMetadata)
        {
            if (!IsAuthorizationMetadataKey(key))
                safeMetadata[key] = value;
        }

        return safeMetadata;
    }

    private static bool IsAuthorizationMetadataKey(string key) =>
        string.Equals(key, "permissions", StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, "roles", StringComparison.OrdinalIgnoreCase);
}
