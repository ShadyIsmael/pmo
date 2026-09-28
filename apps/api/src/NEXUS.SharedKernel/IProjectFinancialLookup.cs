namespace NEXUS.SharedKernel;

/// <summary>
/// Authoritative financial summary from Cost Management for a given project.
/// Only aggregated from active/non-deleted records matching the project currency.
/// </summary>
public sealed class ProjectFinancialSummary
{
    public string Currency { get; init; } = string.Empty;
    public decimal? OriginalApprovedBudget { get; init; }
    public decimal? CurrentApprovedProjectBudget { get; init; }
    public decimal AllocatedBudget { get; init; }
    public decimal? UnallocatedBudget { get; init; }
    public decimal? ApprovedBudget { get; init; }
    public decimal? RevisedBudget { get; init; }
    public decimal? ActualCost { get; init; }
    public decimal? CommittedCost { get; init; }
    public decimal? ForecastAtCompletion { get; init; }
    public decimal? RemainingBudget { get; init; }
    public decimal? BudgetUtilizationPercentage { get; init; }
    public decimal? ForecastVariance { get; init; }
    public bool HasBudgetData { get; init; }
    public bool HasCostEntryData { get; init; }
    public bool HasCommitmentData { get; init; }
    public bool HasFinancialData { get; init; }
    public bool HasPendingRevision { get; init; }
}

/// <summary>
/// Provides the authoritative financial summary from Cost Management for a project.
/// Implemented in the CostManagement module; consumed by ProjectManagement Overview.
/// </summary>
public interface IProjectFinancialLookup
{
    Task<ProjectFinancialSummary?> GetAsync(
        Guid projectId,
        string projectCurrency,
        CancellationToken cancellationToken = default);
}
