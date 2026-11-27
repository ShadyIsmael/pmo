using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Finance;

public sealed partial class ProjectPaymentRequestWorkflow
{
    private static Error? ValidateSelectedAnnualBudgetAssociation(Project project, ProjectAnnualBudget budget)
    {
        if (budget.TenantId != project.TenantId || budget.ProjectId != project.Id
            || budget.FiscalYear is < 1900 or > 2200
            || !string.Equals(NormalizeCurrency(budget.Currency), NormalizeCurrency(project.Currency), StringComparison.Ordinal))
            return Error.Conflict("The Annual Budget does not match this Tenant, Project, fiscal year, or Project currency.");
        if (budget.Status != ProjectAnnualBudgetStatuses.Approved)
            return Error.Conflict($"The associated FY {budget.FiscalYear} Annual Budget must remain approved.");
        if (budget.ClosedAtUtc.HasValue)
            return Error.Conflict($"The associated FY {budget.FiscalYear} Annual Budget is closed.");
        return null;
    }

    private async Task<(decimal Amount, bool CurrencyMismatch)> GetActiveBudgetLineAllocationAsync(
        Guid tenantId, Guid projectId, Guid annualBudgetId, Guid budgetLineId, Guid? excludeRequestId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from detail in db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
            join request in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                on detail.ApprovalRequestId equals request.Id
            where detail.TenantId == tenantId && detail.ProjectId == projectId
                && detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                && detail.ProjectAnnualBudgetId == annualBudgetId
                && detail.ProjectAnnualBudgetLineId == budgetLineId
                && (!excludeRequestId.HasValue || detail.ApprovalRequestId != excludeRequestId.Value)
                && (request.Status == PaymentRequestStatuses.Pending || request.Status == PaymentRequestStatuses.Approved)
                && !request.IsDeleted
            select new { RequestId = request.Id, request.Amount, request.Currency, request.Status }
        ).ToListAsync(cancellationToken);

        if (rows.Count == 0) return (0m, false);
        var budgetCurrency = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == annualBudgetId && item.TenantId == tenantId
                && item.ProjectId == projectId && !item.IsDeleted)
            .Select(item => item.Currency).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(budgetCurrency)) return (0m, true);
        var requestIds = rows.Select(item => item.RequestId).ToArray();
        var reversalRows = await (
            from detail in db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
            join reversal in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                on detail.ApprovalRequestId equals reversal.Id
            where detail.TenantId == tenantId && detail.ProjectId == projectId
                && detail.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                && detail.ReversesApprovalRequestId.HasValue
                && requestIds.Contains(detail.ReversesApprovalRequestId.Value)
                && reversal.Status == PaymentRequestStatuses.Approved && !reversal.IsDeleted
            select new { RequestId = detail.ReversesApprovalRequestId!.Value, reversal.Amount, reversal.Currency }
        ).ToListAsync(cancellationToken);
        var reversals = reversalRows.GroupBy(item => item.RequestId)
            .ToDictionary(group => group.Key, group => new
            {
                Amount = group.Sum(item => item.Amount ?? 0m),
                CurrencyMismatch = group.Any(item => !string.Equals(NormalizeCurrency(item.Currency),
                    NormalizeCurrency(rows[0].Currency), StringComparison.Ordinal))
            });

        var expectedCurrency = NormalizeCurrency(budgetCurrency);
        var mismatch = rows.Any(row => !string.Equals(NormalizeCurrency(row.Currency), expectedCurrency, StringComparison.Ordinal));
        var total = 0m;
        foreach (var row in rows)
        {
            var amount = row.Amount ?? 0m;
            var reversal = reversals.GetValueOrDefault(row.RequestId);
            var released = reversal?.Amount ?? 0m;
            if (amount <= 0m || decimal.Round(amount, 4) != amount || released < 0m || released > amount
                || reversal?.CurrencyMismatch == true)
            {
                mismatch = true;
                continue;
            }
            total += amount - released;
        }
        return (total, mismatch);
    }
}
