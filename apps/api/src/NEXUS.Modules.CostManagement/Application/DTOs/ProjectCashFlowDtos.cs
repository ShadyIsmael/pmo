using System.Text.Json.Serialization;

namespace NEXUS.Modules.CostManagement.Application.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CashFlowGranularity
{
    Day,
    Month
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CashFlowDataQualityStatus
{
    Available,
    Partial,
    Unavailable
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CashFlowMeasure
{
    PlannedResidual,
    CommittedUnpaid,
    ActualGross,
    ExecutedAdjustment
}

public static class CashFlowQualityCodes
{
    public const string ProjectCurrencyUnavailable = "PROJECT_CURRENCY_UNAVAILABLE";
    public const string UnresolvedSourceCurrency = "UNRESOLVED_SOURCE_CURRENCY";
    public const string ForeignCurrencySource = "FOREIGN_CURRENCY_SOURCE";
    public const string UnknownPlannedDueDate = "UNKNOWN_PLANNED_DUE_DATE";
    public const string UnknownCommittedExpectedDate = "UNKNOWN_COMMITTED_EXPECTED_DATE";
    public const string UnknownActualSettlementDate = "UNKNOWN_ACTUAL_SETTLEMENT_DATE";
    public const string UnknownActualExpectedDate = "UNKNOWN_ACTUAL_EXPECTED_DATE";
    public const string UnknownAdjustmentDate = "UNKNOWN_ADJUSTMENT_DATE";
    public const string MissingRequestInstallmentLink = "MISSING_REQUEST_INSTALLMENT_LINK";
    public const string MissingSettlementReference = "MISSING_SETTLEMENT_REFERENCE";
    public const string MissingSettlementRequest = "MISSING_SETTLEMENT_REQUEST";
    public const string MissingSettlementAmount = "MISSING_SETTLEMENT_AMOUNT";
    public const string MissingRequestAmount = "MISSING_REQUEST_AMOUNT";
    public const string MissingRequestReleaseAmount = "MISSING_REQUEST_RELEASE_AMOUNT";
    public const string InvalidRequestReleaseAmount = "INVALID_REQUEST_RELEASE_AMOUNT";
    public const string MissingInstallmentAmount = "MISSING_INSTALLMENT_AMOUNT";
    public const string MissingAdjustmentAmount = "MISSING_ADJUSTMENT_AMOUNT";
    public const string MissingAdjustmentSettlementReference = "MISSING_ADJUSTMENT_SETTLEMENT_REFERENCE";
    public const string CurrencyMismatch = "CURRENCY_MISMATCH";
    public const string InvalidInstallmentAmount = "INVALID_INSTALLMENT_AMOUNT";
    public const string InvalidRequestAmount = "INVALID_REQUEST_AMOUNT";
    public const string InvalidSettlementAmount = "INVALID_SETTLEMENT_AMOUNT";
    public const string InvalidAdjustmentAmount = "INVALID_ADJUSTMENT_AMOUNT";
    public const string DuplicateSettlement = "DUPLICATE_SETTLEMENT";
    public const string ConflictingSourceRows = "CONFLICTING_SOURCE_ROWS";
    public const string InstallmentOverallocated = "INSTALLMENT_OVERALLOCATED";
    public const string RequestSettlementExceedsAmount = "REQUEST_SETTLEMENT_EXCEEDS_AMOUNT";
    public const string RequestReleaseExceedsAmount = "REQUEST_RELEASE_EXCEEDS_AMOUNT";
    public const string SettlementInstallmentConflict = "SETTLEMENT_INSTALLMENT_CONFLICT";
    public const string UnknownAdjustmentType = "UNKNOWN_ADJUSTMENT_TYPE";
}

public static class CashFlowSourceTypes
{
    public const string PlannedInstallment = "PlannedInstallment";
    public const string ApprovedPaymentRequest = "ApprovedPaymentRequest";
    public const string PaymentSettlement = "PaymentSettlement";
    public const string PaymentSettlementAdjustment = "PaymentSettlementAdjustment";
}

/// <summary>
/// Normalized, project-scoped facts for deterministic Cash Flow calculation.
/// Source loading and authorization remain the responsibility of the API layer.
/// </summary>
public sealed record ProjectCashFlowCalculationInput(
    Guid ProjectId,
    string? ProjectCurrency,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    CashFlowGranularity Granularity,
    IReadOnlyList<CashFlowInstallmentFact> Installments,
    IReadOnlyList<CashFlowPaymentRequestFact> PaymentRequests,
    IReadOnlyList<CashFlowSettlementFact> Settlements,
    IReadOnlyList<CashFlowAdjustmentFact> Adjustments);

public sealed record CashFlowInstallmentFact(
    Guid Id,
    Guid PlannedPaymentId,
    string? Description,
    decimal? Amount,
    string? Currency,
    DateOnly? DueDate,
    string? State,
    bool IsActive);

public sealed record CashFlowPaymentRequestFact(
    Guid Id,
    string? RequestKind,
    Guid? InstallmentId,
    decimal? Amount,
    string? Currency,
    string? Status,
    DateOnly? ExpectedDate,
    decimal? ApprovedUnpaidReleaseAmount,
    string? Description);

public sealed record CashFlowSettlementFact(
    Guid Id,
    Guid? PaymentRequestId,
    Guid? InstallmentId,
    Guid? PaymentObligationId,
    decimal? Amount,
    string? Currency,
    DateOnly? SettlementDate,
    DateOnly? ExpectedDate,
    string? Description,
    Guid? DirectInstallmentId = null);

public sealed record CashFlowAdjustmentFact(
    Guid Id,
    Guid? OriginalSettlementId,
    Guid? PaymentRequestId,
    Guid? InstallmentId,
    Guid? PaymentObligationId,
    decimal? Amount,
    string? Currency,
    string? Type,
    string? Status,
    DateOnly? ExecutionDate,
    DateOnly? EffectiveDate,
    string? Description);

public sealed record ProjectCashFlowDto(
    Guid ProjectId,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    CashFlowGranularity Granularity,
    CashFlowDataQualityStatus DataQualityStatus,
    IReadOnlyList<CashFlowQualityIssueDto> DataQualityIssues,
    IReadOnlyList<ProjectCashFlowCurrencyGroupDto> CurrencyGroups);

public sealed record ProjectCashFlowCurrencyGroupDto(
    string? Currency,
    CashFlowDataQualityStatus DataQualityStatus,
    IReadOnlyList<CashFlowBucketDto> Buckets,
    CashFlowUndatedTotalsDto Undated);

public sealed record CashFlowBucketDto(
    DateOnly PeriodStart,
    decimal? PlannedResidual,
    decimal? CommittedUnpaid,
    decimal? ActualGross,
    decimal? ExecutedAdjustments,
    decimal? ActualNet,
    IReadOnlyList<CashFlowLineDto> Items);

public sealed record CashFlowUndatedTotalsDto(
    decimal? PlannedResidual,
    decimal? CommittedUnpaid,
    decimal? ActualGross,
    decimal? ExecutedAdjustments,
    decimal? ActualNet,
    IReadOnlyList<CashFlowLineDto> Items);

public sealed record CashFlowLineDto(
    CashFlowMeasure Measure,
    string SourceType,
    Guid SourceId,
    Guid? InstallmentId,
    Guid? PaymentRequestId,
    Guid? PaymentObligationId,
    Guid? OriginalSettlementId,
    string? Description,
    string? Status,
    DateOnly? EventDate,
    DateOnly? ExpectedDate,
    int? TimingVarianceDays,
    decimal? Amount,
    string? Currency,
    IReadOnlyList<string> QualityCodes);

public sealed record CashFlowQualityIssueDto(
    string Code,
    CashFlowDataQualityStatus Severity,
    string? SourceType,
    Guid? SourceId,
    Guid? InstallmentId,
    Guid? PaymentRequestId,
    Guid? PaymentObligationId,
    string? Currency);
