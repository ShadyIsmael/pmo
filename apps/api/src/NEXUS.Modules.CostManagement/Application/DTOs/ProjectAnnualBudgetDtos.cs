namespace NEXUS.Modules.CostManagement.Application.DTOs;

public sealed record CreateProjectAnnualBudgetDto(
    int FiscalYear,
    decimal NewApprovedFunding,
    decimal ApprovedAdjustments);

public sealed record UpdateProjectAnnualBudgetDto(
    decimal NewApprovedFunding,
    decimal ApprovedAdjustments,
    byte[] RowVersion);

public sealed record ApproveProjectAnnualBudgetDto(byte[] RowVersion);

public sealed record DeleteProjectAnnualBudgetDto(byte[] RowVersion);

public sealed record WithdrawProjectAnnualBudgetSubmissionDto(byte[] RowVersion, byte[] ApprovalRowVersion);

public sealed record SubmitProjectAnnualBudgetReviewDto(
    string? ApprovalMode,
    IReadOnlyList<PaymentRequestApproverSelectionDto>? Approvers,
    byte[] RowVersion,
    bool CustomizeForThisSubmission = false,
    string? OverrideReason = null,
    byte[]? ApprovalConfigurationRowVersion = null,
    byte[]? ProjectSettingsRowVersion = null);

public sealed record DecideProjectAnnualBudgetReviewDto(
    string Decision,
    string? Reason,
    byte[] BudgetRowVersion,
    byte[] ApprovalRowVersion);

public sealed record ReviseProjectAnnualBudgetDto(
    decimal NewApprovedFunding,
    decimal ApprovedAdjustments,
    string Reason,
    string? ApprovalMode,
    IReadOnlyList<PaymentRequestApproverSelectionDto>? Approvers,
    byte[] RowVersion,
    bool CustomizeForThisSubmission = false,
    string? OverrideReason = null,
    byte[]? ApprovalConfigurationRowVersion = null,
    byte[]? ProjectSettingsRowVersion = null);

public sealed record ProjectAnnualBudgetReviewEventDto(
    string Event,
    string? ActorName,
    DateTime TimestampUtc,
    string? Reason,
    int? ApprovalLevel);

public sealed record ProjectAnnualBudgetEligibleApproverDto(Guid UserId, string DisplayName)
{
    public IReadOnlyList<string> EligibleRoleCategories { get; init; } = [];
}

public sealed record ProjectAnnualBudgetConfiguredApproverDto(
    Guid UserId,
    string? DisplayName,
    int? Position,
    bool IsEligible,
    string? IssueCode);

public sealed record ProjectAnnualBudgetApprovalConfigurationDto(
    Guid ProjectId,
    string ProjectName,
    string ConfigurationSource,
    string? FinanceApprovalMode,
    string? ApprovalMode,
    IReadOnlyList<string> AllowedModes,
    IReadOnlyList<ProjectAnnualBudgetConfiguredApproverDto> ConfiguredApprovers,
    IReadOnlyList<ProjectAnnualBudgetEligibleApproverDto> EligibleApprovers,
    byte[]? RowVersion,
    byte[] ProjectSettingsRowVersion,
    bool IsConfigured,
    bool IsValid,
    bool CanSubmitWithDefaults,
    IReadOnlyList<string> ValidationIssues,
    bool CanOverride);

public sealed record SaveProjectAnnualBudgetApprovalConfigurationDto(
    string ApprovalMode,
    IReadOnlyList<Guid> ApproverUserIds,
    byte[]? RowVersion,
    byte[] ProjectSettingsRowVersion);

public sealed record ProjectAnnualBudgetItemDto(
    Guid Id,
    Guid ProjectId,
    int FiscalYear,
    string FiscalYearLabel,
    DateOnly FiscalYearStartDate,
    DateOnly FiscalYearEndDate,
    string FinancialReference,
    string Currency,
    decimal NewApprovedFunding,
    decimal ApprovedCarryForward,
    decimal ApprovedReservedCarryForward,
    decimal ApprovedFreeCarryForward,
    decimal ApprovedAdjustments,
    decimal TotalAnnualBudget,
    string Status,
    Guid? ApprovedBy,
    DateTime? ApprovedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    byte[] RowVersion,
    Guid? ClosedByClosingId,
    DateTime? ClosedAtUtc,
    Guid? ClosedBy,
    bool IsClosed,
    bool CanEdit,
    bool CanApprove,
    string ReviewStatus,
    string? ApprovalMode,
    Guid? ReviewRequestId,
    byte[]? ReviewRowVersion,
    Guid? SubmittedBy,
    DateTime? SubmittedAtUtc,
    bool CanSubmit,
    bool CanReview,
    bool CanRevise,
    IReadOnlyList<ProjectAnnualBudgetReviewEventDto> ReviewHistory)
{
    public bool CanDeleteDraft { get; init; }
    public bool CanWithdrawSubmission { get; init; }
    public string? DeleteDraftBlockReason { get; init; }
}

public sealed record ProjectAnnualBudgetPageDto(
    int CurrentFiscalYear,
    int FiscalYearStartMonth,
    IReadOnlyList<ProjectAnnualBudgetItemDto> Items,
    decimal? TotalProjectFinancialCeiling,
    string ProjectCurrency,
    decimal TotalApprovedNewAllocations,
    decimal PendingSubmittedAllocations,
    decimal? RemainingUnallocatedCeiling,
    int? SuggestedFiscalYear = null,
    decimal? SuggestedNewFunding = null,
    string? SuggestedNewFundingSource = null,
    int? MissingClosingFiscalYear = null);

public sealed record PrepareProjectAnnualBudgetClosingDto(
    int FiscalYear,
    decimal ProposedFreeCarryForward,
    bool CarryOutstandingCommitments,
    decimal NextFiscalYearNewFunding,
    decimal NextFiscalYearAdjustments,
    Guid IdempotencyKey);

public sealed record SubmitProjectAnnualBudgetClosingDto(
    string ApprovalMode,
    IReadOnlyList<PaymentRequestApproverSelectionDto> Approvers,
    byte[] RowVersion);

public sealed record DecideProjectAnnualBudgetClosingDto(string Decision, string? Comment);
public sealed record CancelProjectAnnualBudgetClosingDto(byte[] RowVersion, byte[]? ApprovalRowVersion = null);

public sealed record ReassignProjectAnnualBudgetClosingApproverDto(
    Guid AssignmentId,
    Guid ReplacementApproverId,
    string Reason,
    byte[] RowVersion,
    byte[] ApprovalRowVersion);

public sealed record RecoverCancelProjectAnnualBudgetClosingDto(
    Guid AssignmentId,
    byte[] RowVersion,
    byte[] ApprovalRowVersion,
    string Reason);

public sealed record ProjectAnnualBudgetClosingDecisionDto(
    Guid UserId,
    string? UserName,
    int? Position,
    string Requirement,
    string State,
    string? Decision,
    DateTime? DecidedAtUtc,
    string? Comment)
{
    public Guid AssignmentId { get; init; }
    public bool CanReassignAssignment { get; init; }
    public bool CanRecoverCancelAssignment { get; init; }
}

public sealed record ProjectAnnualBudgetClosingDto(
    Guid Id,
    Guid ProjectId,
    Guid SourceAnnualBudgetId,
    int FiscalYear,
    string FiscalYearLabel,
    DateOnly FiscalYearStartDate,
    DateOnly FiscalYearEndDate,
    string Currency,
    string Status,
    string? ApprovalMode,
    Guid? ApprovalRequestId,
    Guid? DestinationAnnualBudgetId,
    int? DestinationFiscalYear,
    decimal ApprovedAnnualBudget,
    decimal NewFunding,
    decimal IncomingCarryForward,
    decimal ApprovedAdjustments,
    decimal ApprovedPaymentRequestAllocations,
    decimal ApprovedReversalReleases,
    decimal EffectiveActiveAllocations,
    decimal ActualSettlementsInFiscalYear,
    decimal OutstandingCommitments,
    decimal FreeAvailableBudget,
    decimal TotalUnspentBudget,
    decimal ProposedFreeCarryForward,
    bool CarryOutstandingCommitments,
    decimal ApprovedFreeCarryForward,
    decimal ApprovedReservedCarryForward,
    decimal NonCarriedFreeBalance,
    decimal NextFiscalYearNewFunding,
    decimal NextFiscalYearAdjustments,
    int PendingPaymentRequestCount,
    int PendingReversalRequestCount,
    int UnresolvedLegacyRequestCount,
    int IntegrityIssueCount,
    IReadOnlyList<string> IntegrityIssues,
    IReadOnlyList<ProjectAnnualBudgetClosingDecisionDto> ApproverDecisions,
    Guid? PreparedBy,
    DateTime? PreparedAtUtc,
    Guid? SubmittedBy,
    DateTime? SubmittedAtUtc,
    Guid? CompletedBy,
    DateTime? CompletedAtUtc,
    byte[] RowVersion,
    bool CanSubmit,
    bool CanApprove,
    bool CanCancel)
{
    public bool CanReassignApprover { get; init; }
    public bool CanRecoverCancel { get; init; }
    public byte[]? ApprovalRowVersion { get; init; }
}

public sealed record ProjectAnnualBudgetClosingPageDto(
    int CurrentFiscalYear,
    IReadOnlyList<ProjectAnnualBudgetClosingDto> Items);
