using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>
/// Builds the tenant-wide cost summary that backs <c>GET /api/costs/summary</c>.
///
/// Authoritative-source rules enforced here:
/// - The lifetime ceiling is the Project's current approved budget, never a sum of annual budgets.
/// - Allocated budget is the sum of in-scope Budget Lines' <see cref="Domain.Budget.ApprovedAmount"/>.
/// - Actual Cost is the sum of incurred <see cref="Domain.CostEntry"/> amounts.
/// - Actual Paid is the cash total from confirmed settlements (shared calculator), never planned/approved.
/// - Forecast At Completion and its variance are derived only from real forecast values; a missing
///   forecast stays missing rather than being replaced by a planned amount.
/// - Amounts are grouped by exact currency code and are never added across currencies.
/// </summary>
public sealed class CostSummaryService : ICostSummaryService
{
    private readonly DbContext _context;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor? _managementContext;

    public CostSummaryService(
        DbContext context,
        IProjectReferenceLookup projectLookup,
        IHttpContextAccessor httpContextAccessor,
        IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _projectLookup = projectLookup;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
    }

    public async Task<GlobalFinancialSummaryDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = FinancePermissionGuard.TryGetTenantId(_httpContextAccessor.HttpContext?.User, out var resolvedTenant)
            ? resolvedTenant
            : Guid.Empty;

        var projects = await _projectLookup.GetFinancialReferencesAsync(cancellationToken);
        var projectIds = projects.Select(project => project.Id).ToArray();
        if (projectIds.Length == 0) return new GlobalFinancialSummaryDto();

        var budgets = await _context.Set<Domain.Budget>()
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && !b.IsDeleted && b.ProjectId != null && projectIds.Contains(b.ProjectId.Value))
            .Select(b => new
            {
                ProjectId = b.ProjectId,
                b.Currency,
                b.ApprovedAmount,
                b.ForecastAmount
            })
            .ToListAsync(cancellationToken);

        var costEntries = await _context.Set<Domain.CostEntry>()
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.ProjectId != null && projectIds.Contains(c.ProjectId.Value))
            .Select(c => new
            {
                ProjectId = c.ProjectId,
                c.Currency,
                c.Amount
            })
            .ToListAsync(cancellationToken);

        // Actual Paid across both settlement ownership rails, resolved per project.
        var settlementsByProject = new Dictionary<Guid, IReadOnlyList<ActualPaidSettlementCalculator.SettlementFact>>();
        foreach (var projectId in projectIds)
        {
            settlementsByProject[projectId] = await ActualPaidSettlementCalculator.LoadForProjectAsync(
                _context, tenantId, projectId, cancellationToken: cancellationToken);
        }
        var netAdjustmentsByProject = new Dictionary<Guid, decimal>();
        foreach (var project in projects)
        {
            netAdjustmentsByProject[project.Id] = await ActualPaidSettlementCalculator.SumNetAdjustmentsAsync(
                _context, tenantId, project.Id, NormalizeCurrency(project.Currency),
                cancellationToken: cancellationToken);
        }

        var breakdown = projects
            .Select(project => BuildBreakdown(
                project,
                budgets.Select(b => (b.ProjectId, b.Currency, b.ApprovedAmount, b.ForecastAmount)),
                costEntries.Select(c => (c.ProjectId, c.Currency, c.Amount)),
                settlementsByProject,
                netAdjustmentsByProject))
            .ToList();

        var byCurrency = breakdown
            .Where(item => !string.IsNullOrEmpty(item.Currency))
            .GroupBy(item => item.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildCurrencySummary(group.Key, group.ToList()))
            .OrderBy(summary => summary.Currency, StringComparer.Ordinal)
            .ToList();

        return new GlobalFinancialSummaryDto
        {
            ByCurrency = byCurrency,
            ByProject = breakdown
                .OrderByDescending(item => item.ApprovedBudget ?? 0)
                .ThenBy(item => item.ProjectName, StringComparer.Ordinal)
                .ToList()
        };
    }

    private static ProjectFinancialBreakdownDto BuildBreakdown(
        ProjectReference project,
        IEnumerable<(Guid? ProjectId, string Currency, decimal ApprovedAmount, decimal ForecastAmount)> budgets,
        IEnumerable<(Guid? ProjectId, string Currency, decimal Amount)> costEntries,
        IReadOnlyDictionary<Guid, IReadOnlyList<ActualPaidSettlementCalculator.SettlementFact>> settlementsByProject,
        IReadOnlyDictionary<Guid, decimal> netAdjustmentsByProject)
    {
        var currency = NormalizeCurrency(project.Currency);
        var projectBudgets = budgets
            .Where(b => b.ProjectId == project.Id && CurrencyEquals(b.Currency, currency))
            .ToList();
        var projectCosts = costEntries
            .Where(c => c.ProjectId == project.Id && CurrencyEquals(c.Currency, currency))
            .ToList();

        var allocatedBudget = projectBudgets.Sum(b => b.ApprovedAmount);
        var approvedBudget = project.CurrentApprovedProjectBudget;
        var unallocatedBudget = approvedBudget.HasValue ? approvedBudget - allocatedBudget : (decimal?)null;

        var actualCost = projectCosts.Count > 0 ? projectCosts.Sum(c => c.Amount) : (decimal?)null;

        var settlements = settlementsByProject.TryGetValue(project.Id, out var facts) ? facts : [];
        var hasUnresolvedSettlement = ActualPaidSettlementCalculator.HasUnresolvedCurrency(settlements);
        var actualPaid = hasUnresolvedSettlement
            ? (decimal?)null
            : ActualPaidSettlementCalculator.SumActualPaid(settlements, currency);
        decimal? netActualPaid = actualPaid.HasValue
            ? actualPaid.Value + netAdjustmentsByProject.GetValueOrDefault(project.Id)
            : null;

        // Forecast is only reported when at least one in-scope Budget Line carries a real forecast
        // value; a planned amount is never substituted for a missing forecast.
        var hasForecast = projectBudgets.Any(b => b.ForecastAmount > 0m);
        var forecastAtCompletion = hasForecast ? projectBudgets.Sum(b => b.ForecastAmount) : (decimal?)null;
        var forecastVariance = approvedBudget.HasValue && forecastAtCompletion.HasValue
            ? approvedBudget - forecastAtCompletion
            : (decimal?)null;

        var availableBudget = approvedBudget.HasValue && actualCost.HasValue
            ? approvedBudget - actualCost
            : null;

        var budgetUtilization = approvedBudget.HasValue && approvedBudget.Value != 0m && actualCost.HasValue
            ? actualCost.Value / approvedBudget.Value * 100m
            : (decimal?)null;

        return new ProjectFinancialBreakdownDto
        {
            ProjectId = project.Id,
            ProjectPublicId = project.PublicId,
            ProjectName = project.Name,
            Currency = currency,
            OriginalApprovedBudget = project.CurrentApprovedProjectBudget,
            ApprovedBudget = approvedBudget,
            AllocatedBudget = allocatedBudget,
            UnallocatedBudget = unallocatedBudget,
            AvailableBudget = availableBudget,
            ActualCost = actualCost,
            ActualPaid = actualPaid,
            GrossActualPaid = actualPaid,
            NetActualPaid = netActualPaid,
            ForecastAtCompletion = forecastAtCompletion,
            ForecastVariance = forecastVariance,
            BudgetUtilizationPercentage = budgetUtilization,
            HasFinancialData = approvedBudget.HasValue || projectBudgets.Count > 0 || projectCosts.Count > 0 || settlements.Count > 0
        };
    }

    private static CurrencyFinancialSummaryDto BuildCurrencySummary(string currency, IReadOnlyList<ProjectFinancialBreakdownDto> projects)
    {
        decimal Sum(Func<ProjectFinancialBreakdownDto, decimal?> selector) =>
            projects.Sum(project => selector(project) ?? 0m);

        decimal? SumNullable(Func<ProjectFinancialBreakdownDto, decimal?> selector)
        {
            var values = projects.Select(selector).ToList();
            return values.Any(value => value is null) ? null : values.Sum(value => value!.Value);
        }

        var allocatedBudget = Sum(project => project.AllocatedBudget);
        var approvedBudget = Sum(project => project.ApprovedBudget);
        var actualCost = SumNullable(project => project.ActualCost);
        var actualPaid = SumNullable(project => project.ActualPaid);
        var grossActualPaid = SumNullable(project => project.GrossActualPaid);
        var netActualPaid = SumNullable(project => project.NetActualPaid);
        var forecastAtCompletion = SumNullable(project => project.ForecastAtCompletion);
        var forecastVariance = SumNullable(project => project.ForecastVariance);
        var availableBudget = SumNullable(project => project.AvailableBudget);

        return new CurrencyFinancialSummaryDto
        {
            Currency = currency,
            OriginalApprovedBudget = Sum(project => project.OriginalApprovedBudget),
            ApprovedBudget = approvedBudget,
            AllocatedBudget = allocatedBudget,
            UnallocatedBudget = approvedBudget - allocatedBudget,
            AvailableBudget = availableBudget,
            ActualCost = actualCost,
            ActualPaid = actualPaid,
            GrossActualPaid = grossActualPaid,
            NetActualPaid = netActualPaid,
            ForecastAtCompletion = forecastAtCompletion,
            ForecastVariance = forecastVariance,
            BudgetUtilizationPercentage = approvedBudget != 0m && actualCost.HasValue ? actualCost.Value / approvedBudget * 100m : null,
            ProjectCount = projects.Count,
            ProjectsWithMissingFinancialData = projects.Count(project => !project.HasFinancialData)
        };
    }

    private static bool CurrencyEquals(string? left, string? right) =>
        string.Equals(NormalizeCurrency(left), NormalizeCurrency(right), StringComparison.Ordinal);

    private static string NormalizeCurrency(string? currency) =>
        (currency ?? string.Empty).Trim().ToUpperInvariant();
}
