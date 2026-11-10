namespace NEXUS.Modules.CostManagement.Application.DTOs;

public static class ProjectAnnualBudgetDraftApprovalConfigurationSources
{
    public const string ProjectSettings = "ProjectSettings";
    public const string DraftOverride = "DraftOverride";
}

/// <summary>
/// Approval plan shown while editing an Annual Budget Draft. The project-settings
/// row versions allow the caller to detect when the displayed defaults changed.
/// </summary>
public sealed record ProjectAnnualBudgetDraftApprovalConfigurationDto(
    Guid ProjectId,
    Guid BudgetId,
    string ProjectName,
    string ConfigurationSource,
    string? ProjectApprovalMode,
    string? ApprovalMode,
    IReadOnlyList<string> AllowedModes,
    IReadOnlyList<ProjectAnnualBudgetConfiguredApproverDto> ConfiguredApprovers,
    IReadOnlyList<ProjectAnnualBudgetDraftApproverCandidateDto> EligibleApprovers,
    byte[] BudgetRowVersion,
    byte[]? ProjectSettingsConfigurationRowVersion,
    byte[] ProjectSettingsRowVersion,
    bool IsValid,
    bool CanSave,
    IReadOnlyList<string> ValidationIssues,
    string? OverrideReason,
    DateTime? UpdatedAtUtc);

public sealed record ProjectAnnualBudgetDraftApproverCandidateDto(
    Guid UserId,
    string DisplayName,
    IReadOnlyList<string> EligibleRoleCategories,
    string? SelectionBlockReason);

public sealed record SaveProjectAnnualBudgetDraftApprovalConfigurationDto(
    string ApprovalMode,
    IReadOnlyList<Guid> ApproverUserIds,
    byte[] BudgetRowVersion,
    byte[] ProjectSettingsRowVersion,
    byte[]? ProjectSettingsConfigurationRowVersion,
    string OverrideReason);

/// <summary>
/// Persisted with the Draft until submission. The review workflow copies this plan
/// into the immutable Approval Request assignments and audit snapshot.
/// </summary>
public sealed record ProjectAnnualBudgetDraftApprovalConfigurationSnapshot(
    int Version,
    string ApprovalMode,
    IReadOnlyList<PaymentRequestApproverSelectionDto> Approvers,
    string OverrideReason,
    byte[] ProjectSettingsRowVersion,
    byte[]? ProjectSettingsConfigurationRowVersion,
    Guid UpdatedBy,
    DateTime UpdatedAtUtc)
{
    public const int CurrentVersion = 1;

    public static bool TryRead(string? json, out ProjectAnnualBudgetDraftApprovalConfigurationSnapshot? snapshot)
    {
        snapshot = null;
        if (json is null) return true;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            snapshot = System.Text.Json.JsonSerializer.Deserialize<ProjectAnnualBudgetDraftApprovalConfigurationSnapshot>(json);
            return snapshot is { Version: CurrentVersion }
                && !string.IsNullOrWhiteSpace(snapshot.ApprovalMode)
                && snapshot.Approvers is { Count: > 0 }
                && snapshot.ProjectSettingsRowVersion is { Length: > 0 }
                && snapshot.UpdatedBy != Guid.Empty
                && !string.IsNullOrWhiteSpace(snapshot.OverrideReason)
                && snapshot.OverrideReason.Trim().Length <= 1000;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
