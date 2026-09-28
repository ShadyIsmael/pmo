using NEXUS.Modules.WorkflowEngine.Domain;

namespace NEXUS.Modules.WorkflowEngine.Application.Contracts;

public sealed record WorkflowDefinitionListItemDto(
    Guid Id,
    string Name,
    string Code,
    string EntityType,
    bool IsActive,
    Guid? ActivePublishedVersionId,
    int? ActivePublishedVersionNumber,
    string? ActivePublishedLifecycleStatus,
    int VersionCount,
    DateTime? UpdatedAt,
    byte[]? RowVersion);

public sealed record WorkflowDefinitionDetailsDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string EntityType,
    bool IsActive,
    Guid? ActivePublishedVersionId,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    byte[]? RowVersion,
    IReadOnlyList<WorkflowVersionListItemDto> Versions);

public sealed record WorkflowVersionListItemDto(
    Guid Id,
    int VersionNumber,
    WorkflowDefinitionVersionLifecycleStatus LifecycleStatus,
    string? ChangeLog,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? PublishedAt,
    string? PublishedBy,
    int StateCount,
    int TransitionCount,
    int GuardCount,
    int ActionCount,
    byte[]? RowVersion);

public sealed record WorkflowVersionDetailsDto(
    Guid Id,
    Guid WorkflowDefinitionId,
    int VersionNumber,
    WorkflowDefinitionVersionLifecycleStatus LifecycleStatus,
    string? ChangeLog,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? PublishedAt,
    string? PublishedBy,
    byte[]? RowVersion,
    IReadOnlyList<WorkflowStateDto> States,
    IReadOnlyList<WorkflowTransitionDto> Transitions);

public sealed record WorkflowStateDto(
    Guid Id,
    string Code,
    string NameEn,
    string NameAr,
    string? Description,
    int DisplayOrder,
    bool IsInitial,
    bool IsFinal,
    bool IsActive,
    byte[]? RowVersion);

public sealed record WorkflowTransitionDto(
    Guid Id,
    string Code,
    string NameEn,
    string NameAr,
    Guid FromStateId,
    Guid ToStateId,
    string FromStateCode,
    string ToStateCode,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<WorkflowGuardDto> Guards,
    IReadOnlyList<WorkflowActionDto> Actions,
    byte[]? RowVersion);

public sealed record WorkflowGuardDto(
    Guid Id,
    string GuardType,
    string ConfigurationJson,
    int ExecutionOrder,
    bool IsActive,
    byte[]? RowVersion);

public sealed record WorkflowActionDto(
    Guid Id,
    string ActionType,
    string ConfigurationJson,
    int ExecutionOrder,
    bool IsActive,
    byte[]? RowVersion);

public sealed class CreateWorkflowDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
}

public sealed class UpdateWorkflowDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}

public sealed class CreateDraftRequest
{
    public string? ChangeLog { get; set; }
    public byte[]? RowVersion { get; set; }
}

public sealed class SaveWorkflowStateRequest
{
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}

public sealed class SaveWorkflowTransitionRequest
{
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public Guid FromStateId { get; set; }
    public Guid ToStateId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}

public sealed class SaveWorkflowGuardRequest
{
    public string GuardType { get; set; } = string.Empty;
    public Dictionary<string, object?> Configuration { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int ExecutionOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}

public sealed class SaveWorkflowActionRequest
{
    public string ActionType { get; set; } = string.Empty;
    public Dictionary<string, object?> Configuration { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int ExecutionOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}

public sealed class ValidateWorkflowRequest
{
    public byte[]? RowVersion { get; set; }
}

public sealed class PublishWorkflowRequest
{
    public byte[]? RowVersion { get; set; }
}

public sealed record WorkflowPreviewStateDto(
    string Code,
    string NameEn,
    string NameAr,
    bool IsInitial,
    bool IsFinal,
    IReadOnlyList<WorkflowPreviewTransitionDto> Transitions);

public sealed record WorkflowPreviewTransitionDto(
    string Code,
    string NameEn,
    string NameAr,
    string ToStateCode,
    string ToStateNameEn,
    string ToStateNameAr);

public sealed record WorkflowManagementMetadataDto(
    IReadOnlyList<WorkflowEntityTypeMetadata> EntityTypes,
    IReadOnlyList<string> GuardTypes,
    IReadOnlyList<string> ActionTypes);
