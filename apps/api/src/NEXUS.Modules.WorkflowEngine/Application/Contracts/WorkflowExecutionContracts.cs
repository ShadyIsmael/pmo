using System.Text.Json;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Contracts;

public sealed record WorkflowTransitionRequest(
    string EntityType,
    Guid EntityId,
    string TransitionCode,
    string? Comment = null,
    IReadOnlyDictionary<string, string?>? Metadata = null,
    string? ExpectedStateCode = null,
    byte[]? ExpectedRowVersion = null,
    string? CorrelationId = null);

public sealed record WorkflowTransitionResult(
    bool Success,
    Guid? InstanceId,
    string? FromStateCode,
    string? FromStateNameEn,
    string? FromStateNameAr,
    string? ToStateCode,
    string? ToStateNameEn,
    string? ToStateNameAr,
    string? DenialCode,
    string? DenialMessage,
    byte[]? NewRowVersion);

public sealed record WorkflowInstanceSummary(
    Guid InstanceId,
    Guid DefinitionId,
    Guid VersionId,
    string DefinitionCode,
    int VersionNumber,
    string EntityType,
    Guid EntityId,
    string CurrentStateCode,
    string CurrentStateNameEn,
    string CurrentStateNameAr,
    bool IsFinalState,
    DateTime StartedAt,
    string StartedBy,
    DateTime? CompletedAt,
    bool IsActive,
    byte[]? RowVersion = null);

public sealed record AvailableTransitionOption(
    string Code,
    string NameEn,
    string NameAr,
    string TargetStateCode,
    string TargetStateNameEn,
    string TargetStateNameAr,
    int DisplayOrder,
    bool CanExecute,
    string? DenialReason);

public sealed record WorkflowTransitionsResponse(
    string CurrentStateCode,
    string CurrentStateNameEn,
    string CurrentStateNameAr,
    bool IsFinalState,
    IReadOnlyList<AvailableTransitionOption> Transitions,
    byte[]? CurrentRowVersion = null);

public sealed record WorkflowHistoryEntry(
    Guid Id,
    string TransitionCode,
    string TransitionNameEn,
    string TransitionNameAr,
    string FromStateCode,
    string FromStateNameEn,
    string FromStateNameAr,
    string ToStateCode,
    string ToStateNameEn,
    string ToStateNameAr,
    string ExecutedBy,
    string? ExecutedByNameSnapshot,
    DateTime ExecutedAt,
    string? Comment,
    string? MetadataJson,
    string? CorrelationId);

public sealed record WorkflowHistoryResponse(
    IReadOnlyList<WorkflowHistoryEntry> Items,
    int Page,
    int PageSize,
    int TotalCount);

public interface IWorkflowExecutionService
{
    Task<Result<WorkflowTransitionResult>> ExecuteTransitionAsync(
        WorkflowTransitionRequest request,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowInstanceService
{
    Task<Result<WorkflowInstanceSummary>> ProvisionAsync(
        string entityType,
        Guid entityId,
        string workflowDefinitionCode,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);

    Task<Result<WorkflowInstanceSummary>> ProvisionAsync(
        string entityType,
        Guid entityId,
        Guid workflowDefinitionId,
        Guid workflowVersionId,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);

    Task<Result<WorkflowInstanceSummary?>> GetInstanceAsync(
        string entityType,
        Guid entityId,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowTransitionQueryService
{
    Task<Result<WorkflowTransitionsResponse>> GetAvailableTransitionsAsync(
        string entityType,
        Guid entityId,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowGuardResult(
    bool Success,
    string? GuardType,
    string? Code,
    string? Message,
    string? Requirement);

public sealed record WorkflowDomainStateMismatch(
    string WorkflowStateCode,
    string WorkflowStateNameEn,
    string WorkflowStateNameAr,
    string DomainStateCode,
    string DomainStateNameEn,
    string DomainStateNameAr);

public static class WorkflowErrors
{
    private static Error Business(string code, string message, int statusCode = 400) =>
        new(code, message, statusCode);

    public static Error WorkflowNotConfigured(string entityType) =>
        Business("WORKFLOW_NOT_CONFIGURED", $"No workflow definition configured for entity type '{entityType}'.");

    public static Error WorkflowInstanceNotFound(string entityType, Guid entityId) =>
        Business("WORKFLOW_INSTANCE_NOT_FOUND", $"No workflow instance found for {entityType} '{entityId}'.", 404);

    public static Error UnknownEntityType(string entityType) =>
        Business("UNKNOWN_ENTITY_TYPE", $"Unknown entity type '{entityType}'.");

    public static Error EntityNotAccessible(string entityType, Guid entityId) =>
        Business("ENTITY_NOT_ACCESSIBLE", $"Access denied to {entityType} '{entityId}'.", 403);

    public static Error ReadOnlyViewAs =>
        Business("READ_ONLY_VIEW_AS", "Cannot execute this operation in read-only mode.", 403);

    public static Error TransitionNotFound(string transitionCode) =>
        Business("TRANSITION_NOT_FOUND", $"Transition '{transitionCode}' not found.", 404);

    public static Error TransitionNotAvailable(string transitionCode, string currentStateCode) =>
        Business("TRANSITION_NOT_AVAILABLE", $"Transition '{transitionCode}' is not available from current state '{currentStateCode}'.");

    public static Error GuardRejected(string guardType, string message) =>
        Business("GUARD_REJECTED", $"Guard '{guardType}' rejected: {message}");

    public static Error DomainValidationFailed(string message) =>
        Business("DOMAIN_VALIDATION_FAILED", $"Domain validation failed: {message}");

    public static Error WorkflowDomainStateMismatch(string workflowState, string domainState) =>
        Business("WORKFLOW_DOMAIN_STATE_MISMATCH", $"Workflow state '{workflowState}' does not match domain state '{domainState}'.", 409);

    public static Error ConcurrencyConflict =>
        Business("WORKFLOW_CONCURRENCY_CONFLICT", "Workflow state changed. Refresh and retry.", 409);

    public static Error WorkflowConfigurationInvalid(string message) =>
        Error.Validation($"Workflow configuration invalid: {message}");

    public static Error DefinitionValidationFailed(string message) =>
        Business("WORKFLOW_DEFINITION_VALIDATION_FAILED", $"Workflow definition validation failed: {message}");

    public static Error PublishedVersionImmutable =>
        Business("PUBLISHED_VERSION_IMMUTABLE", "Published and retired workflow versions are read-only.", 409);

    public static Error DraftAlreadyExists(int versionNumber) =>
        Business("DRAFT_ALREADY_EXISTS", $"A draft already exists for this workflow. Open version {versionNumber}.", 409);

    public static Error ConsumerValidationFailed(string message) =>
        Business("CONSUMER_VALIDATION_FAILED", $"Workflow consumer validation failed: {message}");

    public static Error DraftCannotExecute =>
        Error.Validation("Cannot execute transitions on a draft workflow version.");

    public static Error RetiredCannotStart =>
        Error.Validation("Cannot start new instance on a retired workflow version.");

    public static Error DuplicateInstance =>
        Error.Conflict("An active workflow instance already exists for this entity.");

    public static Error FinalStateCannotTransition =>
        Error.Validation("Cannot transition from a final state.");

    public static Error InvalidTransitionCode =>
        Error.Validation("Invalid transition code.");

    public static Error ExpectedStateMismatch =>
        Error.Validation("Expected state does not match current state.");

    public static Error NoActivePublishedVersion =>
        Error.Validation("No active published version available for this workflow definition.");

    public static Error NoInitialState =>
        Error.Validation("Workflow definition has no active initial state.");

    public static Error UnknownGuardType(string guardType) =>
        Error.Validation($"Unknown guard type '{guardType}'.");

    public static Error UnknownActionType(string actionType) =>
        Error.Validation($"Unknown action type '{actionType}'.");

    public static Error InvalidGuardConfiguration(string guardType, string message) =>
        Error.Validation($"Invalid configuration for guard '{guardType}': {message}");

    public static Error InvalidActionConfiguration(string actionType, string message) =>
        Error.Validation($"Invalid configuration for action '{actionType}': {message}");

    public static Error OversizedMetadata =>
        Error.Validation("Metadata exceeds maximum allowed size.");

    public static Error CrossTenantAccess =>
        Error.Forbidden("Cross-tenant access denied.");
}
