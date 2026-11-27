using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Modules.CostManagement.Application.DTOs;

/// <summary>
/// API contracts for the project-scoped Payment Request workflow.
/// </summary>
public sealed record CreatePaymentRequestDto(
    string Title,
    decimal Amount,
    string Reason,
    Guid? ProjectPlanMilestoneId,
    Guid? IdempotencyKey = null,
    Guid? PlannedPaymentInstallmentId = null,
    Guid? ProjectAnnualBudgetId = null,
    Guid? ProjectAnnualBudgetLineId = null);

/// <summary>
/// Creates a one-installment Planned Payment and linked Payment Request as one
/// coordinated command. Annual funding begins only when SubmitForApproval is true.
/// </summary>
public sealed record CreateQuickOneOffPaymentRequestDto(
    string Description,
    decimal Amount,
    string Purpose,
    DateTime DueDateUtc,
    Guid? ProjectPlanMilestoneId,
    Guid? ProjectAnnualBudgetId,
    Guid? ProjectAnnualBudgetLineId,
    Guid IdempotencyKey,
    bool SubmitForApproval = false,
    string? ApprovalMode = null,
    IReadOnlyList<PaymentRequestApproverSelectionDto>? Approvers = null);

public sealed record QuickOneOffPaymentRequestResultDto(
    PlannedPaymentDto PlannedPayment,
    PlannedPaymentInstallmentDto Installment,
    ProjectPaymentRequestDetailDto PaymentRequest);

public sealed record UpdatePaymentRequestDto(
    string Title,
    decimal Amount,
    string Reason,
    Guid? ProjectPlanMilestoneId,
    byte[] RowVersion,
    Guid? ProjectAnnualBudgetId = null,
    Guid? ProjectAnnualBudgetLineId = null);

public sealed record PaymentRequestApproverSelectionDto(
    Guid UserId,
    int? Position,
    string Requirement);

public sealed record SubmitPaymentRequestDto(
    string ApprovalMode,
    IReadOnlyList<PaymentRequestApproverSelectionDto> Approvers,
    byte[] RowVersion,
    Guid? IdempotencyKey = null);

public sealed record DecidePaymentRequestApprovalDto(
    string Decision,
    string? Comment,
    byte[] RowVersion,
    decimal? ApprovedAmount = null);

public sealed record CancelPaymentRequestDto(byte[] RowVersion);

public sealed record CreatePaymentReversalDto(decimal Amount, string Reason, Guid IdempotencyKey);

public sealed record UpdatePaymentReversalDto(decimal Amount, string Reason, byte[] RowVersion);

public sealed record SubmitPaymentReversalDto(
    string ApprovalMode,
    IReadOnlyList<PaymentRequestApproverSelectionDto> Approvers,
    byte[] RowVersion);

public sealed record ProjectPaymentReversalDto(
    Guid Id,
    string Reference,
    Guid OriginalPaymentRequestId,
    string OriginalPaymentRequestReference,
    Guid? AnnualBudgetId,
    int? AnnualBudgetFiscalYear,
    string? AnnualBudgetReference,
    decimal OriginalApprovedAmount,
    decimal TotalSettled,
    decimal PreviouslyReleasedAmount,
    decimal MaximumReversibleAmount,
    decimal Amount,
    string Currency,
    string Reason,
    string Status,
    Guid RequesterId,
    string RequesterName,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? CompletedAtUtc,
    string? ApprovalMode,
    IReadOnlyList<PaymentRequestApproverDto> Approvers,
    byte[] RowVersion,
    bool CanEdit,
    bool CanSubmit,
    bool CanDecide,
    bool CanCancel);

public sealed record ProjectPaymentReversalPageDto(
    Guid OriginalPaymentRequestId,
    string OriginalPaymentRequestReference,
    decimal OriginalApprovedAmount,
    decimal TotalSettled,
    decimal TotalReleased,
    decimal RemainingActiveAllocation,
    decimal RemainingPayable,
    decimal MaximumReversibleAmount,
    string Currency,
    Guid? AnnualBudgetId,
    int? AnnualBudgetFiscalYear,
    string? AnnualBudgetReference,
    bool HasPendingReversal,
    bool CanCreate,
    IReadOnlyList<ProjectPaymentReversalDto> Items);

/// <summary>Actual payment fact applied to an already approved Payment Request.</summary>
public sealed record RecordProjectPaymentSettlementDto(
    Guid IdempotencyKey,
    decimal Amount,
    DateOnly PaymentDate,
    string PaymentReference,
    string? Description);

public sealed record RecordSettlementAdjustmentDto(
    Guid IdempotencyKey,
    Guid OriginalSettlementId,
    decimal Amount,
    DateOnly EffectiveDate,
    string Reason,
    Guid AssignedApproverId = default);

public sealed record DecideSettlementAdjustmentDto(
    string Decision,
    string? Comment,
    byte[] RowVersion);

public sealed record ConfirmSettlementAdjustmentExecutionDto(
    Guid IdempotencyKey,
    DateOnly ExecutionDate);

public sealed record SettlementAdjustmentDto(
    Guid Id,
    Guid OriginalSettlementId,
    string AdjustmentType,
    decimal Amount,
    string Currency,
    string Reason,
    DateTime EffectiveDate,
    DateTime RecordedAt,
    string Status = PaymentSettlementAdjustment.Executed,
    Guid? ApprovalRequestId = null,
    Guid? AssignedApproverId = null,
    Guid? ApprovedBy = null,
    DateTime? ApprovedAtUtc = null,
    Guid? ExecutionConfirmedBy = null,
    DateTime? ExecutionConfirmedAtUtc = null,
    DateTime? ExecutionDate = null,
    byte[]? RowVersion = null,
    bool CanDecide = false,
    bool CanConfirmExecution = false);

public sealed record ProjectPaymentRequestSettlementDto(
    Guid Id,
    Guid PaymentRequestId,
    Guid ProjectId,
    string PaymentRequestReference,
    string SettlementReference,
    decimal Amount,
    string Currency,
    DateOnly PaymentDate,
    int ReportingFiscalYear,
    string? Description,
    DateTime CreatedAtUtc,
    Guid? CreatedBy);

public sealed record ProjectPaymentRequestSettlementSummaryDto(
    Guid PaymentRequestId,
    string PaymentRequestReference,
    string RequestStatus,
    decimal ApprovedAmount,
    decimal TotalSettled,
    decimal TotalReleased,
    decimal EffectiveActiveAllocation,
    decimal OutstandingAmount,
    string SettlementStatus,
    string Currency,
    Guid? AnnualBudgetId,
    int? AnnualBudgetFiscalYear,
    string? AnnualBudgetReference);

public sealed record ProjectPaymentRequestSettlementPageDto(
    ProjectPaymentRequestSettlementSummaryDto Summary,
    IReadOnlyList<ProjectPaymentRequestSettlementDto> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record PaymentRequestApproverDto(
    Guid UserId,
    string DisplayName,
    int? Position,
    string Requirement,
    string DecisionStatus,
    DateTime? DecidedAt,
    string? Comment,
    bool IsActive,
    bool CanDecide);

public sealed record PaymentRequestEligibleApproverDto(Guid UserId, string DisplayName);

public sealed record PaymentRequestMilestoneOptionDto(Guid Id, string Name, DateTime? EndDate);

public sealed record ProjectPaymentRequestListItemDto(
    Guid Id,
    string Reference,
    string Title,
    decimal Amount,
    string Currency,
    string Status,
    Guid RequesterId,
    string RequesterName,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? CompletedAtUtc,
    string? ApprovalMode,
    bool ReservesBudget,
    byte[] RowVersion,
    bool CanEdit,
    bool CanSubmit,
    bool CanDecide,
    bool CanCancel,
    Guid? AnnualBudgetId,
    int? AnnualBudgetFiscalYear,
    string? AnnualBudgetReference,
    Guid? PlannedPaymentInstallmentId = null,
    Guid? AnnualBudgetLineId = null,
    string? AnnualBudgetLineName = null,
    IReadOnlyList<ProjectPaymentRequestApprovalCycleDto>? ApprovalCycles = null);

public sealed record ProjectPaymentRequestApprovalCycleDto(
    Guid Id,
    int CycleNumber,
    DateTime SubmittedAtUtc,
    DateTime? CompletedAtUtc,
    string ApprovalMode,
    string Status,
    decimal Amount,
    string Currency,
    Guid AnnualBudgetId,
    string AnnualBudgetReference,
    int FiscalYear,
    Guid AnnualBudgetLineId,
    string AnnualBudgetLineName,
    string RequesterName,
    string? DecisionComment,
    IReadOnlyList<PaymentRequestApproverDto> Approvers);

public sealed record ProjectPaymentRequestBudgetLineOptionDto(
    Guid Id,
    string Reference,
    string Description,
    decimal AuthorizedLimit,
    decimal ReservedAmount,
    decimal AvailableAmount);

public sealed record ProjectPaymentRequestAnnualBudgetOptionDto(
    Guid Id,
    string Reference,
    int FiscalYear,
    string Currency,
    string Status,
    decimal AnnualBudgetAmount,
    decimal ReservedAmount,
    decimal AvailableAmount,
    IReadOnlyList<ProjectPaymentRequestBudgetLineOptionDto> Lines);

public sealed record ProjectPaymentRequestFundingOptionsDto(
    string ProjectCurrency,
    IReadOnlyList<ProjectPaymentRequestAnnualBudgetOptionDto> Budgets);

public sealed record PendingPaymentRequestApprovalDto(
    Guid ProjectId,
    string ProjectName,
    Guid RequestId,
    string Reference,
    string Title,
    decimal Amount,
    string Currency,
    string Status,
    string RequesterName,
    DateTime SubmittedAtUtc,
    string? ApprovalMode,
    byte[] RowVersion,
    bool CanDecide);

public sealed record ProjectPaymentRequestBudgetAvailabilityDto(
    Guid? AnnualBudgetId,
    int FiscalYear,
    string FiscalYearLabel,
    DateOnly FiscalYearStartDate,
    DateOnly FiscalYearEndDate,
    string? AnnualBudgetReference,
    string Currency,
    string? AnnualBudgetStatus,
    decimal? ApprovedAnnualBudget,
    decimal ApprovedReservations,
    decimal? AvailableAmount,
    int LegacyUnassociatedApprovedRequests,
    string AvailabilityStatus,
    bool CanCreate,
    bool CanSubmit);

public sealed record ProjectPaymentRequestPageDto(
    IReadOnlyList<ProjectPaymentRequestListItemDto> Items,
    int Page,
    int PageSize,
    int Total,
    ProjectPaymentRequestBudgetAvailabilityDto BudgetAvailability);

public sealed record ProjectPaymentRequestDetailDto(
    Guid Id,
    Guid ProjectId,
    string Reference,
    string Title,
    decimal Amount,
    string Currency,
    string Reason,
    Guid? ProjectPlanMilestoneId,
    string? ProjectPlanMilestoneName,
    string Status,
    Guid RequesterId,
    string RequesterName,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? CompletedAtUtc,
    string? ApprovalMode,
    IReadOnlyList<PaymentRequestApproverDto> Approvers,
    byte[] RowVersion,
    bool CanEdit,
    bool CanSubmit,
    bool CanDecide,
    bool CanCancel,
    Guid? AnnualBudgetId,
    int? AnnualBudgetFiscalYear,
    string? AnnualBudgetReference,
    Guid? PlannedPaymentInstallmentId = null,
    Guid? AnnualBudgetLineId = null,
    string? AnnualBudgetLineName = null,
    IReadOnlyList<ProjectPaymentRequestApprovalCycleDto>? ApprovalCycles = null);

public static class PaymentRequestApprovalModes
{
    public const string Sequential = "Sequential";
    public const string Parallel = "Parallel";

    public static bool IsValid(string? value) =>
        value is Sequential or Parallel;
}

public static class PaymentRequestApproverRequirements
{
    public const string Mandatory = "Mandatory";
    public const string Optional = "Optional";

    public static bool IsValid(string? value) =>
        value is Mandatory or Optional;
}

public static class PaymentRequestDecisions
{
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string ReturnedForRevision = "ReturnedForRevision";

    public static bool IsValid(string? value) =>
        value is Approved or Rejected or ReturnedForRevision;
}

public static class PaymentRequestStatuses
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string ReturnedForRevision = "ReturnedForRevision";
    public const string Cancelled = "Cancelled";
    public const string Reversed = "Reversed";
}

public sealed record PaymentRequestApproverState(
    Guid UserId,
    int? Position,
    string Requirement,
    string? Decision);
