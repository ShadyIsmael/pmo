using System.Text.Json;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Contracts;

public sealed record WorkflowExecutionContext(
    Guid TenantId,
    string UserId,
    string? UserName = null,
    bool ReadOnly = false,
    string? CorrelationId = null,
    string? Comment = null,
    IReadOnlyDictionary<string, string?>? Metadata = null);

public sealed record WorkflowEntitySnapshot(
    string EntityType,
    Guid EntityId,
    IReadOnlyDictionary<string, object?> Facts);

public sealed record WorkflowTransitionContext(
    WorkflowExecutionContext Execution,
    Guid EntityId,
    WorkflowEntitySnapshot Entity,
    WorkflowState FromState,
    WorkflowState ToState,
    WorkflowTransition Transition,
    IWorkflowEntityAdapter EntityAdapter);

public interface IWorkflowEntityAdapter
{
    string EntityType { get; }
    Task<Result<WorkflowEntitySnapshot>> GetSnapshotAsync(Guid entityId, WorkflowExecutionContext context, CancellationToken cancellationToken = default);
    Task<Result> AuthorizeAsync(WorkflowExecutionContext context, Guid entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Write-path authorization for a specific transition. Read access to an entity does not
    /// imply the right to move it through its workflow, so adapters must check the permission
    /// that actually governs the target state here.
    /// </summary>
    Task<Result> AuthorizeTransitionAsync(WorkflowTransitionContext context, CancellationToken cancellationToken = default);

    Task<Result> ValidateTransitionAsync(WorkflowTransitionContext context, CancellationToken cancellationToken = default);
    Task<Result> ApplyTransitionAsync(WorkflowTransitionContext context, CancellationToken cancellationToken = default);
}

public interface IWorkflowEntityAdapterRegistry
{
    IWorkflowEntityAdapter? Resolve(string entityType);
    bool IsSupported(string entityType);
}

public interface IWorkflowGuardHandler
{
    string GuardType { get; }
    Task<Result> ValidateConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default);
    Task<Result> EvaluateAsync(WorkflowTransitionContext context, JsonElement configuration, CancellationToken cancellationToken = default);
}

public interface IWorkflowActionHandler
{
    string ActionType { get; }
    Task<Result> ValidateConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default);
    Task<Result> ExecuteAsync(WorkflowTransitionContext context, JsonElement configuration, CancellationToken cancellationToken = default);
}

public interface IWorkflowGuardHandlerRegistry
{
    bool IsSupported(string guardType);
    IWorkflowGuardHandler? GetGuardHandler(string guardType);
}

public interface IWorkflowActionHandlerRegistry
{
    bool IsSupported(string actionType);
    IWorkflowActionHandler? GetActionHandler(string actionType);
}
