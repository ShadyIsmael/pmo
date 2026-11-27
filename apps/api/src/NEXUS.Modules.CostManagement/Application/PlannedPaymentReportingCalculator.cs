using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Modules.CostManagement.Application;

public sealed record PlannedPaymentReportingSummary(
    decimal Outstanding,
    decimal DueWithinPeriod,
    decimal RemainingUnpaidAtPeriodEnd,
    bool HasRecordedPlans,
    bool HasMatchingCurrencyPlans,
    bool ProjectCurrencyUnavailable,
    bool HasCurrencyMismatch,
    bool HasIntegrityIssue)
{
    public bool HasDataQualityGap => ProjectCurrencyUnavailable || HasCurrencyMismatch || HasIntegrityIssue;
}

/// <summary>
/// Computes cash-facing outstanding measures from active Planned Payment installments.
/// Payment Requests fund these installments and are not added again as separate commitments.
/// </summary>
public static class PlannedPaymentReportingCalculator
{
    public static async Task<PlannedPaymentReportingSummary> CalculateAsync(
        DbContext context,
        Guid tenantId,
        Guid projectId,
        string? projectCurrency,
        DateTime periodFrom,
        DateTime periodTo,
        CancellationToken cancellationToken = default)
    {
        var plans = await context.Set<PlannedPayment>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted)
            .Include(item => item.Installments)
            .Include(item => item.Reservation)
            .ToListAsync(cancellationToken);

        var activePlans = plans.Where(item => item.State is PlannedPaymentStates.Registered or PlannedPaymentStates.Locked).ToArray();
        var projectCurrencyCode = ActualPaidSettlementCalculator.NormalizeCurrency(projectCurrency);
        var projectCurrencyUnavailable = !IsValidCurrency(projectCurrencyCode);
        var hasCurrencyMismatch = activePlans.Any(item =>
        {
            var currency = ActualPaidSettlementCalculator.NormalizeCurrency(item.Currency);
            return !IsValidCurrency(currency) || currency != projectCurrencyCode;
        });
        var hasIntegrityIssue = plans.Any(item => item.State is not PlannedPaymentStates.Registered
                and not PlannedPaymentStates.Locked
                and not PlannedPaymentStates.Replaced
                and not PlannedPaymentStates.Cancelled)
            || activePlans.Any(item => item.TotalAmount <= 0m
                || item.Reservation is null
                || item.Reservation.ReservedAmount != item.TotalAmount
                || item.Reservation.ReleasedAmount != 0m
                || item.Reservation.ProjectId != item.ProjectId
                || item.Reservation.PlannedPaymentId != item.Id
                || item.Reservation.TenantId != item.TenantId
                || item.Installments.Count == 0
                || item.Installments.Sum(installment => installment.Amount) != item.TotalAmount
                || item.Installments.Any(installment => installment.Amount <= 0m
                    || installment.ProjectId != item.ProjectId
                    || installment.PlannedPaymentId != item.Id
                    || installment.TenantId != item.TenantId));

        var matchingPlans = activePlans.Where(item =>
            ActualPaidSettlementCalculator.NormalizeCurrency(item.Currency) == projectCurrencyCode
            && projectCurrencyCode.Length > 0).ToArray();
        var installments = matchingPlans.SelectMany(item => item.Installments).ToArray();
        var paidByInstallment = new Dictionary<Guid, decimal>();
        if (installments.Length > 0 && !projectCurrencyUnavailable)
        {
            var settlementFacts = await ActualPaidSettlementCalculator.LoadForProjectAsync(
                context, tenantId, projectId, periodTo: periodTo, cancellationToken: cancellationToken);
            var requestedInstallments = installments.Select(item => item.Id).ToHashSet();
            var requestIds = settlementFacts.Where(item => item.ProjectPaymentRequestId.HasValue)
                .Select(item => item.ProjectPaymentRequestId!.Value).Distinct().ToArray();
            var detailByRequest = new Dictionary<Guid, ProjectPaymentRequestDetail>();
            var statusByRequest = new Dictionary<Guid, string>();
            if (requestIds.Length > 0)
            {
                var requestDetails = await context.Set<ProjectPaymentRequestDetail>().AsNoTracking()
                    .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                        && item.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                        && requestIds.Contains(item.ApprovalRequestId))
                    .ToListAsync(cancellationToken);
                detailByRequest = requestDetails.ToDictionary(item => item.ApprovalRequestId);
                var approvals = await context.Set<ApprovalRequest>().AsNoTracking()
                    .Where(item => item.TenantId == tenantId && requestIds.Contains(item.Id) && !item.IsDeleted)
                    .Select(item => new { item.Id, item.Status })
                    .ToListAsync(cancellationToken);
                statusByRequest = approvals.ToDictionary(item => item.Id, item => item.Status);
            }

            foreach (var settlement in settlementFacts.Where(item => item.ProjectPaymentRequestId.HasValue))
            {
                var requestId = settlement.ProjectPaymentRequestId!.Value;
                var hasDetail = detailByRequest.TryGetValue(requestId, out var detail);
                var installmentId = settlement.PlannedPaymentInstallmentId
                    ?? (hasDetail ? detail!.PlannedPaymentInstallmentId : null);
                if (!installmentId.HasValue || !requestedInstallments.Contains(installmentId.Value)) continue;
                if (!hasDetail || !statusByRequest.TryGetValue(requestId, out var status)
                    || status is not (PaymentRequestStatuses.Approved or PaymentRequestStatuses.Reversed))
                {
                    hasIntegrityIssue = true;
                    continue;
                }
                if (settlement.PlannedPaymentInstallmentId.HasValue && detail!.PlannedPaymentInstallmentId.HasValue
                    && settlement.PlannedPaymentInstallmentId != detail.PlannedPaymentInstallmentId)
                {
                    hasIntegrityIssue = true;
                    continue;
                }
                if (settlement.Amount <= 0m) { hasIntegrityIssue = true; continue; }
                if (ActualPaidSettlementCalculator.NormalizeCurrency(settlement.Currency) != projectCurrencyCode) continue;
                paidByInstallment[installmentId.Value] = paidByInstallment.GetValueOrDefault(installmentId.Value) + settlement.Amount;
            }
        }

        decimal Outstanding(PlannedPaymentInstallment item) =>
            Math.Max(0m, item.Amount - paidByInstallment.GetValueOrDefault(item.Id));

        if (installments.Any(item => paidByInstallment.GetValueOrDefault(item.Id) > item.Amount))
            hasIntegrityIssue = true;

        return new PlannedPaymentReportingSummary(
            installments.Sum(Outstanding),
            installments.Where(item => item.DueDateUtc.HasValue
                    && item.DueDateUtc.Value.Date >= periodFrom.Date
                    && item.DueDateUtc.Value.Date <= periodTo.Date)
                .Sum(Outstanding),
            installments.Where(item => item.DueDateUtc.HasValue && item.DueDateUtc.Value.Date <= periodTo.Date)
                .Sum(Outstanding),
            plans.Count > 0,
            matchingPlans.Length > 0,
            projectCurrencyUnavailable,
            hasCurrencyMismatch,
            hasIntegrityIssue);
    }

    private static bool IsValidCurrency(string currency) => currency.Length == 3
        && currency.All(character => character is >= 'A' and <= 'Z');
}
