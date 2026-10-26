using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public static class ProjectWorkflowModes
{
    public const string Standard = "Standard";
    public const string Custom = "Custom";

    public static bool IsSupported(string? value) => string.Equals(value, Standard, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Custom, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Project adoption is independent per workflow domain. A row is created only
/// when a project explicitly changes the default Standard behavior.
/// </summary>
public sealed class ProjectWorkflowConfiguration : BaseEntity
{
    public Guid ProjectId { get; set; }
    public string Domain { get; set; } = WorkflowDomains.Task;
    public string Mode { get; set; } = ProjectWorkflowModes.Standard;
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? EffectiveWorkflowVersionId { get; set; }
    public string ConfiguredBy { get; set; } = string.Empty;
    public DateTime ConfiguredAt { get; set; } = DateTime.UtcNow;
    public bool SponsorApprovalRequired { get; set; } = true;
    public bool AllowAnyOneOptionalApprover { get; set; }
    public bool AllowSequentialApprovers { get; set; } = true;
    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public WorkflowDefinitionVersion? EffectiveWorkflowVersion { get; set; }
}
