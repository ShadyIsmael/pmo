namespace NEXUS.Modules.CostManagement.Application.DTOs;

public sealed record CreatePlannedPaymentInstallmentDto(
    decimal Amount,
    DateTime? DueDateUtc,
    Guid? PlanActivityId,
    string? Description);

public sealed record CreatePlannedPaymentDto(
    string Description,
    IReadOnlyList<CreatePlannedPaymentInstallmentDto> Installments,
    Guid IdempotencyKey);

public sealed record UpdatePlannedPaymentInstallmentDto(
    Guid? Id,
    decimal Amount,
    DateTime? DueDateUtc,
    Guid? PlanActivityId,
    string? Description);

public sealed record UpdatePlannedPaymentDto(
    string Description,
    IReadOnlyList<UpdatePlannedPaymentInstallmentDto> Installments,
    Guid IdempotencyKey,
    byte[] RowVersion);

public sealed record PlannedPaymentInstallmentDto(
    Guid Id,
    int Sequence,
    decimal Amount,
    DateTime? DueDateUtc,
    Guid? PlanActivityId,
    string? PlanActivityName,
    decimal RequestedAmount,
    decimal SettledAmount,
    decimal ActualPaid,
    decimal OutstandingBalance,
    decimal RemainingRequestableAmount,
    byte[] RowVersion);

public sealed record PlannedPaymentDto(
    Guid Id,
    Guid ProjectId,
    string Reference,
    string Description,
    string Currency,
    decimal TotalAmount,
    string State,
    bool IsLocked,
    string PaymentStatus,
    decimal ReservedAmount,
    decimal ReleasedAmount,
    decimal OutstandingReservedAmount,
    decimal SettledAmount,
    DateTime? RegisteredAtUtc,
    DateTime? FirstRequestAtUtc,
    Guid? ReplacesPlannedPaymentId,
    Guid? ReplacedByPlannedPaymentId,
    IReadOnlyList<PlannedPaymentInstallmentDto> Installments,
    byte[] RowVersion,
    bool CanEdit);

public sealed record PlannedPaymentBudgetDto(
    Guid ProjectId,
    string Currency,
    decimal? LifetimeCeiling,
    decimal LifetimeConsumed,
    decimal LifetimeReserved,
    decimal AvailableLifetimeBudget,
    bool CanRegister);

public sealed record PlannedPaymentPageDto(
    IReadOnlyList<PlannedPaymentDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ReplacePlannedPaymentInstallmentDto(
    decimal Amount,
    DateTime? DueDateUtc,
    Guid? PlanActivityId,
    string? Description);

public sealed record ReplaceRemainingCommitmentDto(
    decimal NewRemainingCommitment,
    IReadOnlyList<ReplacePlannedPaymentInstallmentDto> Installments,
    string Reason,
    Guid IdempotencyKey);

public sealed record ReplacementPaymentRequestBlockerDto(
    Guid PaymentRequestId,
    string PaymentRequestReference,
    string Status,
    decimal OutstandingAmount,
    decimal ApprovedReversalAmount,
    decimal PendingReversalAmount);

public sealed record PlannedPaymentReplacementPreviewDto(
    Guid PlannedPaymentId,
    string Currency,
    decimal CurrentCommitment,
    decimal HistoricalPaid,
    decimal CurrentOutstandingCommitment,
    decimal NewRemainingCommitment,
    decimal NetLifetimeBudgetChange,
    decimal AvailableLifetimeBudgetBefore,
    decimal AvailableLifetimeBudgetAfter,
    bool HasSufficientLifetimeBudget,
    IReadOnlyList<ReplacementPaymentRequestBlockerDto> Blockers);
