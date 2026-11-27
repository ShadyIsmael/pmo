using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>
/// Consolidated financial reporting calculator using existing authoritative sources.
/// Reuses <see cref="ActualPaidSettlementCalculator"/>, <see cref="PlannedPaymentReportingCalculator"/>,
/// and <see cref="ProjectFinancialBalanceCalculator"/> without introducing competing formulas.
/// </summary>
public static class FinancialReportingCalculator
{
    /// <summary>
    /// Calculates the complete project financial reporting foundation.
    /// </summary>
    public static async Task<ProjectFinancialReportingDto> CalculateAsync(
        DbContext context,
        IProjectFinancialData project,
        DateTime? reportingPeriodFrom = null,
        DateTime? reportingPeriodTo = null,
        CancellationToken cancellationToken = default)
    {
        if (project is null)
        {
            return new ProjectFinancialReportingDto
            {
                ProjectId = Guid.Empty,
                Currency = string.Empty,
                DataQuality = new FinancialReportingDataQualityDto
                {
                    ProjectCurrencyUnavailable = true,
                    Details = ["Project not found"]
                }
            };
        }

        var currency = NormalizeCurrency(project.Currency);
        var currencyIsValid = IsValidCurrency(currency);
        var projectCurrencyUnavailable = !currencyIsValid;
        var tenantId = project.TenantId;
        var projectId = project.Id;

        // Load all required data in parallel where possible
        var budgetsTask = LoadAnnualBudgetsAsync(context, tenantId, projectId, currency, cancellationToken);
        var obligationsTask = LoadPaymentObligationsAsync(context, tenantId, projectId, currency, cancellationToken);
        var settlementFactsTask = ActualPaidSettlementCalculator.LoadForProjectAsync(
            context, tenantId, projectId, ignoreQueryFilters: true, cancellationToken: cancellationToken);
        var plannedPaymentsTask = LoadPlannedPaymentsAsync(context, tenantId, projectId, cancellationToken);
        var budgetLinesTask = LoadBudgetLinesAsync(context, tenantId, projectId, currency, cancellationToken);
        var netAdjustmentsTask = ActualPaidSettlementCalculator.SumNetAdjustmentsAsync(
            context, tenantId, projectId, currency, cancellationToken: cancellationToken);

        var budgets = await budgetsTask;
        var obligations = await obligationsTask;
        var settlementFacts = await settlementFactsTask;
        var plannedPayments = await plannedPaymentsTask;
        var budgetLines = await budgetLinesTask;
        var netAdjustments = await netAdjustmentsTask;

        var hasUnresolvedSettlementCurrency = ActualPaidSettlementCalculator.HasUnresolvedCurrency(settlementFacts);
        var hasForeignCurrencySettlement = ActualPaidSettlementCalculator.HasForeignCurrency(settlementFacts, currency);
        var hasObligationCurrencyMismatch = obligations.Any(o => NormalizeCurrency(o.Currency) != currency);
        var hasPlannedPaymentCurrencyMismatch = plannedPayments.Any(p => NormalizeCurrency(p.Currency) != currency);
        var hasBudgetLineCurrencyMismatch = budgetLines.Any(b => NormalizeCurrency(b.Currency) != currency);
        var hasMissingAnnualBudgets = budgets.Count == 0;

        var dataQuality = new FinancialReportingDataQualityDto
        {
            HasUnresolvedCurrency = hasUnresolvedSettlementCurrency,
            HasCurrencyMismatch = hasForeignCurrencySettlement || hasObligationCurrencyMismatch || hasPlannedPaymentCurrencyMismatch || hasBudgetLineCurrencyMismatch,
            ProjectCurrencyUnavailable = projectCurrencyUnavailable,
            HasIncompleteSettlementHistory = hasUnresolvedSettlementCurrency || hasForeignCurrencySettlement,
            HasMissingAnnualBudgetAllocations = hasMissingAnnualBudgets,
            Details = BuildDataQualityDetails(
                hasUnresolvedSettlementCurrency, hasForeignCurrencySettlement,
                hasObligationCurrencyMismatch, hasPlannedPaymentCurrencyMismatch,
                hasBudgetLineCurrencyMismatch, projectCurrencyUnavailable, hasMissingAnnualBudgets)
        };

        // Lifetime Ceiling (Project.Budget)
        var lifetimeCeiling = project.Budget > 0 ? project.Budget : (decimal?)null;

        // Annual Approved Budget (sum of approved annual budgets)
        var annualApprovedBudget = budgets.Where(b => b.Status == ProjectAnnualBudgetStatuses.Approved)
            .Sum(b => b.TotalAnnualBudget);
        var hasApprovedAnnualBudgets = budgets.Any(b => b.Status == ProjectAnnualBudgetStatuses.Approved);

        // Gross Actual Paid (confirmed settlements in project currency)
        var knownCurrencySettlements = settlementFacts
            .Where(s => NormalizeCurrency(s.Currency) == currency)
            .ToArray();
        var grossActualPaid = (projectCurrencyUnavailable || hasUnresolvedSettlementCurrency)
            ? (decimal?)null
            : knownCurrencySettlements.Sum(s => s.Amount);

        // Confirmed Refunds and Corrections (executed adjustments only)
        var confirmedRefundsAndCorrections = projectCurrencyUnavailable ? (decimal?)null : netAdjustments;

        // Net Actual Paid
        var netActualPaid = grossActualPaid.HasValue && confirmedRefundsAndCorrections.HasValue
            ? grossActualPaid.Value + confirmedRefundsAndCorrections.Value
            : (decimal?)null;

        // Lifetime Reserved (active Planned Payment reservations minus released and settled)
        var lifetimeReserved = CalculateLifetimeReserved(plannedPayments, settlementFacts, currency);

        // Outstanding Commitments (obligations + planned payments)
        var outstandingObligations = obligations
            .Where(o => NormalizeCurrency(o.Currency) == currency)
            .Sum(o => Math.Max(0m, o.Amount - o.Settlements
                .Where(s => !s.IsDeleted && NormalizeCurrency(s.Currency) == currency)
                .Sum(s => s.Amount)));

        var plannedPaymentOutstanding = plannedPayments
            .Where(p => p.State is PlannedPaymentStates.Registered or PlannedPaymentStates.Locked)
            .SelectMany(p => p.Installments, (p, i) => new { PlanCurrency = p.Currency, Installment = i })
            .Where(x => NormalizeCurrency(x.PlanCurrency) == currency)
            .Sum(x => x.Installment.OutstandingBalance);

        var outstandingCommitments = outstandingObligations + plannedPaymentOutstanding;

        // Available Lifetime Budget
        var lifetimeConsumed = grossActualPaid ?? 0m;
        var availableLifetimeBudget = lifetimeCeiling.HasValue
            ? Math.Max(0m, lifetimeCeiling.Value - lifetimeConsumed - lifetimeReserved)
            : (decimal?)null;

        // Annual Budget Utilization
        // Note: Effective utilization requires Annual Budget Closing data (ApprovedPaymentRequestAllocations, ApprovedReversalReleases)
        // which is not available on the ProjectAnnualBudget entity itself. Returning null until closing data is integrated.
        var annualBudgetUtilization = (decimal?)null;

        // Budget Line Utilizations
        var budgetLineUtilizations = budgetLines
            .Where(b => b.AuthorizedLimit > 0)
            .Select(b => new BudgetLineUtilizationDto
            {
                BudgetLineId = b.Id,
                Reference = b.Reference,
                Description = b.Description,
                AuthorizedLimit = b.AuthorizedLimit,
                ReservedAmount = b.ReservedAmount ?? 0m,
                UtilizationPercentage = b.AuthorizedLimit > 0 ? (b.ReservedAmount ?? 0m) / b.AuthorizedLimit * 100m : 0m,
                HasCurrencyMismatch = NormalizeCurrency(b.Currency) != currency
            })
            .ToList();

        return new ProjectFinancialReportingDto
        {
            ProjectId = projectId,
            ProjectCode = project.Code,
            ProjectName = project.Name,
            Currency = currency,
            LifetimeCeiling = lifetimeCeiling,
            AnnualApprovedBudget = hasApprovedAnnualBudgets ? annualApprovedBudget : (decimal?)null,
            LifetimeReserved = lifetimeReserved,
            OutstandingCommitments = outstandingCommitments,
            GrossActualPaid = grossActualPaid,
            ConfirmedRefundsAndCorrections = confirmedRefundsAndCorrections,
            NetActualPaid = netActualPaid,
            AvailableLifetimeBudget = availableLifetimeBudget,
            AnnualBudgetUtilizationPercentage = annualBudgetUtilization,
            BudgetLineUtilizations = budgetLineUtilizations,
            DataQuality = dataQuality
        };
    }

    private static async Task<List<ProjectAnnualBudget>> LoadAnnualBudgetsAsync(
        DbContext context, Guid tenantId, Guid projectId, string currency, CancellationToken ct)
    {
        return await context.Set<ProjectAnnualBudget>().AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.ProjectId == projectId && !b.IsDeleted)
            .ToListAsync(ct);
    }

    private static async Task<List<PaymentObligation>> LoadPaymentObligationsAsync(
        DbContext context, Guid tenantId, Guid projectId, string currency, CancellationToken ct)
    {
        return await context.Set<PaymentObligation>().AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.ProjectId == projectId && !o.IsDeleted)
            .Include(o => o.Settlements.Where(s => s.TenantId == tenantId && !s.IsDeleted))
            .ToListAsync(ct);
    }

    private static async Task<List<PlannedPayment>> LoadPlannedPaymentsAsync(
        DbContext context, Guid tenantId, Guid projectId, CancellationToken ct)
    {
        return await context.Set<PlannedPayment>().AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.ProjectId == projectId && !p.IsDeleted)
            .Include(p => p.Installments)
            .Include(p => p.Reservation)
            .ToListAsync(ct);
    }

    private static async Task<List<BudgetLineView>> LoadBudgetLinesAsync(
        DbContext context, Guid tenantId, Guid projectId, string currency, CancellationToken ct)
    {
        var lines = await context.Set<ProjectAnnualBudgetLine>().AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.ProjectId == projectId && !l.IsDeleted)
            .ToListAsync(ct);

        var lineIds = lines.Select(l => l.Id).ToList();
        var reservations = new Dictionary<Guid, decimal>();

        if (lineIds.Count > 0)
        {
            var requestDetails = await context.Set<ProjectPaymentRequestDetail>().AsNoTracking()
                .Where(d => d.TenantId == tenantId && d.ProjectId == projectId
                    && d.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                    && d.ProjectAnnualBudgetLineId.HasValue
                    && lineIds.Contains(d.ProjectAnnualBudgetLineId.Value))
                .ToListAsync(ct);

            var requestIds = requestDetails.Select(d => d.ApprovalRequestId).ToList();
            var approvedRequests = await context.Set<ApprovalRequest>().AsNoTracking()
                .Where(r => r.TenantId == tenantId && requestIds.Contains(r.Id)
                    && r.Status == ApprovalRequest.StatusApproved && !r.IsDeleted)
                .ToListAsync(ct);

            var approvedRequestIds = approvedRequests.Select(r => r.Id).ToHashSet();
            var approvedDetails = requestDetails.Where(d => approvedRequestIds.Contains(d.ApprovalRequestId)).ToList();

            foreach (var detail in approvedDetails)
            {
                if (detail.ProjectAnnualBudgetLineId.HasValue)
                {
                    var request = approvedRequests.FirstOrDefault(r => r.Id == detail.ApprovalRequestId);
                    if (request != null && request.Amount.HasValue)
                    {
                        var lineId = detail.ProjectAnnualBudgetLineId.Value;
                        reservations[lineId] = reservations.GetValueOrDefault(lineId) + request.Amount.Value;
                    }
                }
            }
        }

        return lines.Select(l => new BudgetLineView
        {
            Id = l.Id,
            Reference = l.Reference,
            Description = l.Description,
            AuthorizedLimit = l.AuthorizedLimit,
            Currency = string.Empty,
            ReservedAmount = reservations.GetValueOrDefault(l.Id)
        }).ToList();
    }

    private static decimal CalculateLifetimeReserved(
        List<PlannedPayment> plannedPayments,
        IReadOnlyList<ActualPaidSettlementCalculator.SettlementFact> settlementFacts,
        string currency)
    {
        var settlementByPlan = new Dictionary<Guid, decimal>();
        var requestIds = settlementFacts
            .Where(s => s.ProjectPaymentRequestId.HasValue)
            .Select(s => s.ProjectPaymentRequestId!.Value)
            .Distinct()
            .ToList();

        if (requestIds.Count > 0)
        {
            // This would need ProjectPaymentRequestDetail to map to installments to plans
            // For now, calculate from reservations directly
        }

        var reserved = plannedPayments
            .Where(p => p.State is PlannedPaymentStates.Registered or PlannedPaymentStates.Locked)
            .Sum(p =>
            {
                if (p.Reservation is null) return 0m;
                var settled = p.Installments
                    .Where(i => NormalizeCurrency(p.Currency) == currency)
                    .Sum(i => i.ActualPaid);
                return Math.Max(0m, p.Reservation.ReservedAmount - p.Reservation.ReleasedAmount - settled);
            });

        return reserved;
    }

    private static IReadOnlyList<string> BuildDataQualityDetails(
        bool hasUnresolvedSettlementCurrency,
        bool hasForeignCurrencySettlement,
        bool hasObligationCurrencyMismatch,
        bool hasPlannedPaymentCurrencyMismatch,
        bool hasBudgetLineCurrencyMismatch,
        bool projectCurrencyUnavailable,
        bool hasMissingAnnualBudgets)
    {
        var details = new List<string>();
        if (projectCurrencyUnavailable) details.Add("Project currency is not configured");
        if (hasUnresolvedSettlementCurrency) details.Add("One or more settlements have unresolved currency");
        if (hasForeignCurrencySettlement) details.Add("One or more settlements are in a different currency than the project");
        if (hasObligationCurrencyMismatch) details.Add("One or more payment obligations have a currency mismatch");
        if (hasPlannedPaymentCurrencyMismatch) details.Add("One or more planned payments have a currency mismatch");
        if (hasBudgetLineCurrencyMismatch) details.Add("One or more budget lines are associated with a different currency");
        if (hasMissingAnnualBudgets) details.Add("No approved annual budgets found for the project");
        return details;
    }

    private static string NormalizeCurrency(string? currency) =>
        (currency ?? string.Empty).Trim().ToUpperInvariant();

    private static bool IsValidCurrency(string currency) =>
        currency.Length == 3 && currency.All(c => c is >= 'A' and <= 'Z');

    private sealed class BudgetLineView
    {
        public Guid Id { get; init; }
        public string Reference { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public decimal AuthorizedLimit { get; init; }
        public string Currency { get; init; } = string.Empty;
        public decimal? ReservedAmount { get; init; }
    }
}