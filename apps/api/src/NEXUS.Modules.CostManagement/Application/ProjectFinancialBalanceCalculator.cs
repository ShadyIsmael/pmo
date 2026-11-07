using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>Shared balance functions for project finance, including approved request allocations.</summary>
public static class ProjectFinancialBalanceCalculator
{
    public static decimal? CalculateAvailableBudget(decimal? approvedProjectBudget, decimal activeApprovedRequestAllocations) =>
        approvedProjectBudget.HasValue
            ? approvedProjectBudget.Value - activeApprovedRequestAllocations
            : null;

    public static decimal CalculateActualPaid(IEnumerable<PaymentObligation> obligations) =>
        obligations.Sum(obligation => obligation.Settlements
            .Where(settlement => !settlement.IsDeleted)
            .Sum(settlement => settlement.Amount));

    public static decimal CalculateOutstandingPaymentBalance(IEnumerable<PaymentObligation> obligations) =>
        obligations.Sum(obligation => Math.Max(0m, obligation.Amount - obligation.Settlements
            .Where(settlement => !settlement.IsDeleted)
            .Sum(settlement => settlement.Amount)));
}
