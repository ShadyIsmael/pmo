namespace NEXUS.Modules.CostManagement.Application.DTOs;

/// <summary>
/// Cost summary for a single currency across every in-scope Project. Every amount is
/// resolved from an authoritative source for that currency; no value is substituted
/// from a different measure and no cross-currency aggregation is performed.
/// </summary>
public sealed class CurrencyFinancialSummaryDto
{
    public string Currency { get; init; } = string.Empty;
    public decimal OriginalApprovedBudget { get; init; }
    public decimal ApprovedBudget { get; init; }
    public decimal AllocatedBudget { get; init; }
    public decimal UnallocatedBudget { get; init; }
    public decimal? AvailableBudget { get; init; }
    public decimal? ActualCost { get; init; }
    public decimal? ActualPaid { get; init; }
    public decimal? GrossActualPaid { get; init; }
    public decimal? NetActualPaid { get; init; }
    public decimal? ForecastAtCompletion { get; init; }
    public decimal? ForecastVariance { get; init; }
    public decimal? BudgetUtilizationPercentage { get; init; }
    public int ProjectCount { get; init; }
    public int ProjectsWithMissingFinancialData { get; init; }
}

/// <summary>
/// Cost summary for a single Project. Amounts are authoritative for the Project currency;
/// a null amount means the measure is unavailable and must not be shown as zero.
/// </summary>
public sealed class ProjectFinancialBreakdownDto
{
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal? OriginalApprovedBudget { get; init; }
    public decimal? ApprovedBudget { get; init; }
    public decimal AllocatedBudget { get; init; }
    public decimal? UnallocatedBudget { get; init; }
    public decimal? AvailableBudget { get; init; }
    public decimal? ActualCost { get; init; }
    public decimal? ActualPaid { get; init; }
    public decimal? GrossActualPaid { get; init; }
    public decimal? NetActualPaid { get; init; }
    public decimal? ForecastAtCompletion { get; init; }
    public decimal? ForecastVariance { get; init; }
    public decimal? BudgetUtilizationPercentage { get; init; }
    public bool HasFinancialData { get; init; }
}

/// <summary>
/// Tenant-wide cost summary grouped by exact currency code. Projects are never summed
/// across currencies and unresolved-currency rows are excluded and reported separately.
/// </summary>
public sealed class GlobalFinancialSummaryDto
{
    public IReadOnlyList<CurrencyFinancialSummaryDto> ByCurrency { get; init; } = [];
    public IReadOnlyList<ProjectFinancialBreakdownDto> ByProject { get; init; } = [];
}
