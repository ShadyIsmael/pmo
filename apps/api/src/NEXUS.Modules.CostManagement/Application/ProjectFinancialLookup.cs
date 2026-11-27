using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>
/// Aggregates authoritative financial data from Cost Management for a given project.
/// Only sums records whose currency matches the project currency (case-insensitive).
/// No FX conversion is performed.
/// </summary>
public sealed class ProjectFinancialLookup : IProjectFinancialLookup
{
    private readonly DbContext _context;
    private readonly IProjectReferenceLookup _projects;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public ProjectFinancialLookup(DbContext context, IProjectReferenceLookup projects, IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _projects = projects;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ProjectFinancialSummary?> GetAsync(
        Guid projectId,
        string projectCurrency,
        CancellationToken cancellationToken = default)
    {
        // Resolve the authoritative project currency from the project reference.
        // A missing or mismatched currency must never silently fall back to GBP.
        var project = await _projects.FindAsync(projectId, cancellationToken);
        if (project is null) return null;

        var currency = NormalizeCurrency(project.Currency);
        if (string.IsNullOrEmpty(currency))
            currency = NormalizeCurrency(projectCurrency);
        if (string.IsNullOrEmpty(currency))
            return new ProjectFinancialSummary { Currency = string.Empty, HasFinancialData = false };

        var budgets = await _context.Set<Budget>()
            .AsNoTracking()
            .Where(b => b.ProjectId == projectId && !b.IsDeleted)
            .ToListAsync(cancellationToken);

        var costEntries = await _context.Set<CostEntry>()
            .AsNoTracking()
            .Where(c => c.ProjectId == projectId && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var matchingBudgets = budgets.Where(b => NormalizeCurrency(b.Currency) == currency).ToList();
        var matchingCostEntries = costEntries.Where(c => NormalizeCurrency(c.Currency) == currency).ToList();

        var hasMatchingCurrencyData = matchingBudgets.Count > 0 || matchingCostEntries.Count > 0;

        var matchingBudgetIds = matchingBudgets.Select(b => b.Id).Distinct().ToList();

        // Find the first approved revision per budget using deterministic ordering:
        // ReviewedAt (authoritative approval timestamp), then CreatedAt as stable fallback.
        var allApprovedRevisions = matchingBudgetIds.Count > 0
            ? await _context.Set<BudgetRevision>()
                .AsNoTracking()
                .Where(r => matchingBudgetIds.Contains(r.BudgetId)
                    && r.Status == BudgetRevision.Statuses.Approved)
                .ToListAsync(cancellationToken)
            : [];

        var originalAmounts = allApprovedRevisions
            .GroupBy(r => r.BudgetId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.ReviewedAt ?? r.CreatedAt)
                      .ThenBy(r => r.CreatedAt)
                      .ThenBy(r => r.Id)
                      .First()
                      .PreviousApprovedAmount);

        var allocatedBudget = matchingBudgets.Sum(b => b.ApprovedAmount);
        var approvedBudget = project.CurrentApprovedProjectBudget;
        // Revision history remains allocation history, never project ceiling approval history.
        var originalApprovedBudget = matchingBudgets.Sum(b => originalAmounts.TryGetValue(b.Id, out var amount) ? amount : b.ApprovedAmount);
        var revisedBudget = allApprovedRevisions.Count > 0 ? allocatedBudget : (decimal?)null;

        // HasPendingRevision: true when a Draft or Submitted revision exists for any of this project's budgets.
        var hasPendingRevision = matchingBudgetIds.Count > 0
            && await _context.Set<BudgetRevision>()
                .AsNoTracking()
                .AnyAsync(r => matchingBudgetIds.Contains(r.BudgetId)
                    && (r.Status == BudgetRevision.Statuses.Draft || r.Status == BudgetRevision.Statuses.Submitted),
                    cancellationToken);

        var actualCost = matchingCostEntries.Sum(c => c.Amount);

        // Actual Paid is cash: confirmed PaymentSettlement facts across both ownership rails.
        // It is never substituted with planned, approved, or incurred-cost amounts. A settlement
        // whose currency cannot be resolved makes the total incomplete, so it is reported as null.
        var tenantId = FinancePermissionGuard.TryGetTenantId(_httpContextAccessor?.HttpContext?.User, out var resolvedTenant)
            ? resolvedTenant
            : project.TenantId;
        var settlements = await ActualPaidSettlementCalculator.LoadForProjectAsync(
            _context, tenantId, projectId, cancellationToken: cancellationToken);
        var hasUnresolvedSettlementCurrency = ActualPaidSettlementCalculator.HasUnresolvedCurrency(settlements);
        var actualPaid = hasUnresolvedSettlementCurrency
            ? (decimal?)null
            : ActualPaidSettlementCalculator.SumActualPaid(settlements, currency);
        var netAdjustmentAmount = await ActualPaidSettlementCalculator.SumNetAdjustmentsAsync(
            _context, tenantId, projectId, currency, cancellationToken: cancellationToken);
        decimal? netActualPaid = actualPaid.HasValue ? actualPaid.Value + netAdjustmentAmount : null;

        var forecastAtCompletion = matchingBudgets.Any(b => b.ForecastAmount > 0m)
            ? matchingBudgets.Sum(b => b.ForecastAmount)
            : (decimal?)null;

        var remainingBudget = approvedBudget - actualCost;
        var budgetUtilization = approvedBudget.HasValue && approvedBudget.Value != 0m
            ? actualCost / approvedBudget * 100m : (decimal?)null;
        var forecastVariance = approvedBudget.HasValue && forecastAtCompletion.HasValue
            ? approvedBudget - forecastAtCompletion
            : (decimal?)null;

        return new ProjectFinancialSummary
        {
            Currency = currency,
            OriginalApprovedBudget = originalApprovedBudget,
            ApprovedBudget = approvedBudget,
            CurrentApprovedProjectBudget = approvedBudget,
            AllocatedBudget = allocatedBudget,
            UnallocatedBudget = approvedBudget - allocatedBudget,
            RevisedBudget = revisedBudget,
            ActualCost = actualCost,
            ActualPaid = actualPaid,
            GrossActualPaid = actualPaid,
            NetActualPaid = netActualPaid,
            ForecastAtCompletion = forecastAtCompletion,
            RemainingBudget = remainingBudget,
            BudgetUtilizationPercentage = budgetUtilization,
            ForecastVariance = forecastVariance,
            HasBudgetData = approvedBudget.HasValue,
            HasCostEntryData = costEntries.Count > 0,
            HasFinancialData = hasMatchingCurrencyData || approvedBudget.HasValue,
            HasPendingRevision = hasPendingRevision
        };
    }

    private static string NormalizeCurrency(string? currency)
    {
        return (currency ?? string.Empty).Trim().ToUpperInvariant();
    }
}
