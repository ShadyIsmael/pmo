using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

public sealed class GlobalFinancialSummaryService : IGlobalFinancialSummaryService
{
    private readonly DbContext _context;
    private readonly IProjectReferenceLookup _projectLookup;

    public GlobalFinancialSummaryService(DbContext context, IProjectReferenceLookup projectLookup)
    {
        _context = context;
        _projectLookup = projectLookup;
    }

    public async Task<GlobalFinancialSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var budgets = await _context.Set<Budget>()
            .AsNoTracking()
            .Where(b => !b.IsDeleted)
            .ToListAsync(cancellationToken);

        var costEntries = await _context.Set<CostEntry>()
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var commitments = await _context.Set<PurchaseCommitment>()
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .ToListAsync(cancellationToken);

        var budgetIds = budgets.Select(b => b.Id).Distinct().ToList();
        var allApprovedRevisions = budgetIds.Count > 0
            ? await _context.Set<BudgetRevision>()
                .AsNoTracking()
                .Where(r => budgetIds.Contains(r.BudgetId)
                    && r.Status == BudgetRevision.Statuses.Approved)
                .ToListAsync(cancellationToken)
            : [];

        // OriginalApprovedBudget per budget: PreviousApprovedAmount from the FIRST approved revision
        // chronologically (ReviewedAt first, then CreatedAt/Id as stable fallback).
        var originalAmounts = allApprovedRevisions
            .GroupBy(r => r.BudgetId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.ReviewedAt ?? r.CreatedAt)
                      .ThenBy(r => r.CreatedAt)
                      .ThenBy(r => r.Id)
                      .First()
                      .PreviousApprovedAmount);

        // Track which budgets have at least one approved revision (for RevisedBudget computation).
        var budgetsWithApprovedRevision = allApprovedRevisions
            .Select(r => r.BudgetId)
            .Distinct()
            .ToHashSet();

        var projects = await _projectLookup.GetFinancialReferencesAsync(cancellationToken);
        var budgetGroups = budgets.Where(b => b.ProjectId.HasValue).ToLookup(b => b.ProjectId!.Value);
        var costGroups = costEntries.Where(c => c.ProjectId.HasValue).ToLookup(c => c.ProjectId!.Value);
        var commitmentGroups = commitments.Where(c => c.ProjectId.HasValue).ToLookup(c => c.ProjectId!.Value);
        var byProject = projects.Select(project =>
        {
            var currency = NormalizeCurrency(project.Currency);
            var lines = budgetGroups[project.Id].Where(b => NormalizeCurrency(b.Currency) == currency).ToList();
            var allocated = lines.Sum(b => b.ApprovedAmount);
            var ceiling = project.CurrentApprovedProjectBudget ?? 0;
            var actual = costGroups[project.Id].Where(c => NormalizeCurrency(c.Currency) == currency).Sum(c => c.Amount);
            var committed = commitmentGroups[project.Id].Where(c => NormalizeCurrency(c.Currency) == currency && c.Status != "Cancelled" && c.Status != "Closed").Sum(c => c.Amount);
            var forecast = lines.Sum(b => b.ForecastAmount);
            return new ProjectFinancialBreakdown
            {
                ProjectId = project.Id, ProjectPublicId = project.PublicId, ProjectName = project.Name, Currency = currency,
                OriginalApprovedBudget = lines.Sum(b => originalAmounts.TryGetValue(b.Id, out var amount) ? amount : b.ApprovedAmount),
                ApprovedBudget = ceiling, AllocatedBudget = allocated, UnallocatedBudget = ceiling - allocated,
                ActualCost = actual, CommittedCost = committed, AvailableBudget = ceiling - actual - committed,
                ForecastAtCompletion = forecast, ForecastVariance = ceiling - forecast,
                BudgetUtilizationPercentage = ceiling > 0 ? actual / ceiling * 100m : null
            };
        }).OrderBy(p => p.Currency).ThenBy(p => p.ProjectName).ToList();

        var byCurrency = byProject
            .GroupBy(p => p.Currency)
            .Select(g => new CurrencyFinancialSummary
            {
                Currency = g.Key,
                OriginalApprovedBudget = g.Sum(p => p.OriginalApprovedBudget),
                ApprovedBudget = g.Sum(p => p.ApprovedBudget),
                AllocatedBudget = g.Sum(p => p.AllocatedBudget),
                UnallocatedBudget = g.Sum(p => p.UnallocatedBudget),
                AvailableBudget = g.Sum(p => p.AvailableBudget),
                ActualCost = g.Sum(p => p.ActualCost),
                CommittedCost = g.Sum(p => p.CommittedCost),
                ForecastAtCompletion = g.Sum(p => p.ForecastAtCompletion),
                ForecastVariance = g.Sum(p => p.ForecastVariance),
                BudgetUtilizationPercentage = g.Sum(p => p.ApprovedBudget) > 0
                    ? g.Sum(p => p.ActualCost) / g.Sum(p => p.ApprovedBudget) * 100m
                    : (decimal?)null,
                ProjectCount = g.Count()
            })
            .OrderBy(c => c.Currency)
            .ToList();

        return new GlobalFinancialSummaryDto
        {
            ByCurrency = byCurrency,
            ByProject = byProject
        };
    }

    private static string NormalizeCurrency(string? currency)
    {
        return (currency ?? "GBP").Trim().ToUpperInvariant();
    }
}
