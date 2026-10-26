namespace NEXUS.Modules.CostManagement.Application.DTOs;

public sealed class CurrencyFinancialSummary
{
    public string Currency { get; init; } = string.Empty;
    public decimal OriginalApprovedBudget { get; init; }
    public decimal ApprovedBudget { get; init; }
    public decimal AllocatedBudget { get; init; }
    public decimal UnallocatedBudget { get; init; }
    public decimal AvailableBudget { get; init; }
    public decimal ActualCost { get; init; }
    public decimal CommittedCost { get; init; }
    public decimal ForecastAtCompletion { get; init; }
    public decimal ForecastVariance { get; init; }
    public decimal? BudgetUtilizationPercentage { get; init; }
    public int ProjectCount { get; init; }
}

public sealed record ProjectFinancialBreakdown
{
    public Guid ProjectId { get; init; }
    public Guid ProjectPublicId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal OriginalApprovedBudget { get; init; }
    public decimal ApprovedBudget { get; init; }
    public decimal AllocatedBudget { get; init; }
    public decimal UnallocatedBudget { get; init; }
    public decimal AvailableBudget { get; init; }
    public decimal ActualCost { get; init; }
    public decimal CommittedCost { get; init; }
    public decimal ForecastAtCompletion { get; init; }
    public decimal ForecastVariance { get; init; }
    public decimal? BudgetUtilizationPercentage { get; init; }
}

public sealed class GlobalFinancialSummaryDto
{
    public IReadOnlyList<CurrencyFinancialSummary> ByCurrency { get; init; } = [];
    public IReadOnlyList<ProjectFinancialBreakdown> ByProject { get; init; } = [];
}
