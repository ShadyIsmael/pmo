using NEXUS.Modules.WorkflowEngine.Domain;

namespace NEXUS.Modules.WorkflowEngine.Application.Contracts;

public sealed record ProjectWorkflowConfigurationDto(
    Guid Id,
    Guid ProjectId,
    string Domain,
    string Mode,
    Guid? WorkflowDefinitionId,
    string? WorkflowDefinitionName,
    Guid? EffectiveWorkflowVersionId,
    int? EffectiveWorkflowVersionNumber,
    bool IsPublished,
    string? EffectiveBehavior,
    DateTime ConfiguredAt,
    string ConfiguredBy,
    byte[]? RowVersion,
    bool SponsorApprovalRequired = true,
    bool AllowAnyOneOptionalApprover = false,
    bool AllowSequentialApprovers = true);

public sealed record ProjectWorkflowSettingsDto(
    Guid ProjectId,
    IReadOnlyList<ProjectWorkflowConfigurationDto> Domains);

public sealed class SaveProjectWorkflowConfigurationRequest
{
    public string Mode { get; set; } = ProjectWorkflowModes.Standard;
    public Guid? WorkflowDefinitionId { get; set; }
    public byte[]? RowVersion { get; set; }
    public bool? SponsorApprovalRequired { get; set; }
    public bool? AllowAnyOneOptionalApprover { get; set; }
    public bool? AllowSequentialApprovers { get; set; }
}

public sealed record ProjectWorkflowSelection(
    string Domain,
    string EntityType,
    bool IsCustom,
    Guid? WorkflowDefinitionId,
    Guid? WorkflowVersionId,
    string? WorkflowDefinitionCode,
    string? InitialStateCode);
