using NEXUS.Modules.CostManagement.Application.DTOs;

namespace NEXUS.Modules.CostManagement.Application;

public sealed record AnnualBudgetRequestReservation(Guid? AnnualBudgetId, string Status, decimal? Amount);

/// <summary>Calculates source allocations that reserve an approved Annual Budget.</summary>
public static class AnnualBudgetRequestReservationCalculator
{
    public static decimal SumForBudget(IEnumerable<AnnualBudgetRequestReservation> requests, Guid? annualBudgetId)
    {
        if (!annualBudgetId.HasValue) return 0m;

        return requests
            .Where(item => item.AnnualBudgetId == annualBudgetId.Value
                && item.Status is PaymentRequestStatuses.Pending or PaymentRequestStatuses.Approved)
            .Sum(item => item.Amount ?? 0m);
    }
}
