using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Services;

public sealed class WorkflowTransitionQueryService : IWorkflowTransitionQueryService
{
    private readonly DbContext _context;
    private readonly IWorkflowEntityAdapterRegistry _adapterRegistry;
    private readonly IWorkflowGuardHandlerRegistry _guardRegistry;

    public WorkflowTransitionQueryService(
        DbContext context,
        IWorkflowEntityAdapterRegistry adapterRegistry,
        IWorkflowGuardHandlerRegistry guardRegistry)
    {
        _context = context;
        _adapterRegistry = adapterRegistry;
        _guardRegistry = guardRegistry;
    }

    public async Task<Result<WorkflowTransitionsResponse>> GetAvailableTransitionsAsync(
        string entityType,
        Guid entityId,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var adapter = _adapterRegistry.Resolve(entityType);
        if (adapter is null)
            return Result<WorkflowTransitionsResponse>.Failure(WorkflowErrors.UnknownEntityType(entityType));

        var authorizeResult = await adapter.AuthorizeAsync(context, entityId, cancellationToken);
        if (authorizeResult.IsFailure)
            return Result<WorkflowTransitionsResponse>.Failure(authorizeResult.Error!);

        var instance = await _context.Set<WorkflowInstance>()
            .Include(i => i.CurrentState)
            .Include(i => i.WorkflowDefinitionVersion)
                .ThenInclude(v => v.Transitions)
                    .ThenInclude(t => t.FromState)
            .Include(i => i.WorkflowDefinitionVersion)
                .ThenInclude(v => v.Transitions)
                    .ThenInclude(t => t.ToState)
            .Include(i => i.WorkflowDefinitionVersion)
                .ThenInclude(v => v.Transitions)
                    .ThenInclude(t => t.Guards)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TenantId == context.TenantId && i.EntityType == entityType
                && i.EntityId == entityId && !i.IsDeleted, cancellationToken);

        if (instance is null)
            return Result<WorkflowTransitionsResponse>.Failure(WorkflowErrors.WorkflowInstanceNotFound(entityType, entityId));

        var currentState = instance.CurrentState;
        var transitions = instance.WorkflowDefinitionVersion.Transitions
            .Where(t => t.IsActive && t.FromStateId == currentState.Id && t.FromState.IsActive && t.ToState.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ToList();

        if (currentState.IsFinal)
        {
            // A final state offers only its reopen transitions (guards are
            // still evaluated below); the response keeps IsFinalState = true
            // for the current state.
            transitions = transitions
                .Where(t => t.Code.StartsWith(WorkflowTransitionCodes.ReopenPrefix, StringComparison.Ordinal))
                .ToList();
        }

        var snapshotResult = await adapter.GetSnapshotAsync(entityId, context, cancellationToken);
        if (snapshotResult.IsFailure)
            return Result<WorkflowTransitionsResponse>.Failure(snapshotResult.Error!);

        var entitySnapshot = snapshotResult.Value!;

        var options = new List<AvailableTransitionOption>();

        foreach (var transition in transitions)
        {
            var canExecute = true;
            string? denialReason = null;

            foreach (var guard in transition.Guards.Where(g => g.IsActive).OrderBy(g => g.ExecutionOrder))
            {
                var handler = _guardRegistry.GetGuardHandler(guard.GuardType);
                if (handler is null)
                {
                    canExecute = false;
                    denialReason = $"Unknown guard type '{guard.GuardType}'.";
                    break;
                }

                var guardContext = new WorkflowTransitionContext(
                    context,
                    entityId,
                    entitySnapshot,
                    currentState,
                    transition.ToState,
                    transition,
                    adapter);

                var guardResult = await handler.EvaluateAsync(
                    guardContext,
                    JsonSerializer.Deserialize<JsonElement>(guard.ConfigurationJson),
                    cancellationToken);

                if (guardResult.IsFailure)
                {
                    canExecute = false;
                    denialReason = guardResult.Error?.Message ?? $"Guard '{guard.GuardType}' rejected.";
                    break;
                }
            }

            options.Add(new AvailableTransitionOption(
                transition.Code,
                transition.NameEn,
                transition.NameAr,
                transition.ToState.Code,
                transition.ToState.NameEn,
                transition.ToState.NameAr,
                transition.DisplayOrder,
                canExecute,
                denialReason));
        }

        return Result<WorkflowTransitionsResponse>.Success(new WorkflowTransitionsResponse(
            currentState.Code,
            currentState.NameEn,
            currentState.NameAr,
            currentState.IsFinal,
            options,
            instance.RowVersion));
    }
}
