using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Persistence;

namespace NEXUS.Api.Finance;

/// <summary>
/// Loads existing, authoritative financial records in batches and maps them into
/// the pure Project Cash Flow calculator. This reader does not authorize callers;
/// API controllers must establish tenant, permission, and resource scope first.
/// </summary>
public static class ProjectCashFlowReader
{
    private const int QueryBatchSize = 800;
    private const string PaymentRequestType = "ProjectPaymentRequest";
    private const string PaymentRequestEntityType = "Project";
    private const string ReversalRequestType = "ProjectPaymentRequestReversal";
    private const string ReversalEntityType = "Project";

    public static async Task<ProjectCashFlowDto> ReadProjectAsync(
        AppDbContext db,
        Guid tenantId,
        Guid projectId,
        string? projectCurrency,
        DateOnly from,
        DateOnly to,
        CashFlowGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        var reports = await ReadManyAsync(
            db,
            tenantId,
            new Dictionary<Guid, string?> { [projectId] = projectCurrency },
            from,
            to,
            granularity,
            cancellationToken);

        return reports[projectId];
    }

    public static async Task<IReadOnlyDictionary<Guid, ProjectCashFlowDto>> ReadManyAsync(
        AppDbContext db,
        Guid tenantId,
        IReadOnlyDictionary<Guid, string?> projectCurrenciesById,
        DateOnly from,
        DateOnly to,
        CashFlowGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(projectCurrenciesById);
        if (from > to) throw new ArgumentOutOfRangeException(nameof(from), "The cash-flow range must start on or before it ends.");

        var projectCurrencies = projectCurrenciesById
            .Where(item => item.Key != Guid.Empty)
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.Last().Value);
        if (projectCurrencies.Count == 0)
            return new Dictionary<Guid, ProjectCashFlowDto>();

        var requestedCurrencyCodes = projectCurrencies.Values
            .Select(NormalizeCurrency)
            .Where(IsCurrencyCode)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var validCurrencyCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var currencyBatch in requestedCurrencyCodes.Chunk(QueryBatchSize))
        {
            var currencies = await db.Set<Currency>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => !item.IsDeleted && currencyBatch.Contains(item.Code))
                .Select(item => item.Code)
                .ToListAsync(cancellationToken);
            foreach (var currency in currencies)
                validCurrencyCodes.Add(NormalizeCurrency(currency));
        }

        foreach (var projectId in projectCurrencies.Keys.ToArray())
        {
            var normalized = NormalizeCurrency(projectCurrencies[projectId]);
            if (!validCurrencyCodes.Contains(normalized))
                projectCurrencies[projectId] = null;
        }

        var projectIds = projectCurrencies.Keys.Order().ToArray();
        var installmentsByProject = projectIds.ToDictionary(id => id, _ => new List<CashFlowInstallmentFact>());
        foreach (var projectBatch in projectIds.Chunk(QueryBatchSize))
        {
            var rows = await (
                from plan in db.PlannedPayments.IgnoreQueryFilters().AsNoTracking()
                join installment in db.PlannedPaymentInstallments.IgnoreQueryFilters().AsNoTracking()
                    on new { plan.TenantId, PlanId = plan.Id }
                    equals new { installment.TenantId, PlanId = installment.PlannedPaymentId }
                where plan.TenantId == tenantId
                    && projectBatch.Contains(plan.ProjectId)
                    && !plan.IsDeleted
                    && !installment.IsDeleted
                    && installment.ProjectId == plan.ProjectId
                    && (plan.State == PlannedPaymentStates.Registered || plan.State == PlannedPaymentStates.Locked)
                select new
                {
                    plan.ProjectId,
                    PlannedPaymentId = plan.Id,
                    plan.Currency,
                    plan.State,
                    InstallmentId = installment.Id,
                    installment.Amount,
                    installment.DueDateUtc,
                    InstallmentDescription = installment.Description,
                    PlanDescription = plan.Description
                }).ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                installmentsByProject[row.ProjectId].Add(new CashFlowInstallmentFact(
                    row.InstallmentId,
                    row.PlannedPaymentId,
                    row.InstallmentDescription ?? row.PlanDescription,
                    row.Amount,
                    row.Currency,
                    ToDateOnly(row.DueDateUtc),
                    row.State,
                    IsActive: true));
            }
        }

        var activeInstallmentsById = installmentsByProject.Values
            .SelectMany(items => items)
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var paymentRequestRows = new List<PaymentRequestRow>();
        foreach (var projectBatch in projectIds.Chunk(QueryBatchSize))
        {
            paymentRequestRows.AddRange(await (
                from detail in db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
                join request in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                    on new { detail.TenantId, RequestId = detail.ApprovalRequestId }
                    equals new { request.TenantId, RequestId = request.Id }
                where detail.TenantId == tenantId
                    && projectBatch.Contains(detail.ProjectId)
                    && !request.IsDeleted
                    && detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                    && request.Type == PaymentRequestType
                    && request.EntityType == PaymentRequestEntityType
                    && request.EntityId == detail.ProjectId
                select new PaymentRequestRow(detail, request)).ToListAsync(cancellationToken));
        }

        var reversalRows = new List<ApprovedReversalRow>();
        foreach (var projectBatch in projectIds.Chunk(QueryBatchSize))
        {
            var requestIds = paymentRequestRows
                .Where(item => projectBatch.Contains(item.Detail.ProjectId))
                .Select(item => item.Request.Id)
                .Distinct()
                .ToArray();
            foreach (var requestIdBatch in requestIds.Chunk(QueryBatchSize))
            {
                if (requestIdBatch.Length == 0) continue;
                reversalRows.AddRange(await (
                    from detail in db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
                    join reversal in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                        on new { detail.TenantId, RequestId = detail.ApprovalRequestId }
                        equals new { reversal.TenantId, RequestId = reversal.Id }
                    where detail.TenantId == tenantId
                        && projectBatch.Contains(detail.ProjectId)
                        && requestIdBatch.Contains(detail.ReversesApprovalRequestId ?? Guid.Empty)
                        && !reversal.IsDeleted
                        && detail.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                        && reversal.Type == ReversalRequestType
                        && reversal.Status == PaymentRequestStatuses.Approved
                    select new ApprovedReversalRow(detail, reversal)).ToListAsync(cancellationToken));
            }
        }

        var transferIds = reversalRows
            .Where(item => item.Detail.FundingTransferId.HasValue)
            .Select(item => item.Detail.FundingTransferId!.Value)
            .Distinct()
            .ToArray();
        var transfersById = new Dictionary<Guid, ProjectPaymentRequestFundingTransfer>();
        foreach (var transferBatch in transferIds.Chunk(QueryBatchSize))
        {
            if (transferBatch.Length == 0) continue;
            var rows = await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && transferBatch.Contains(item.Id)
                    && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var row in rows) transfersById[row.Id] = row;
        }

        var requestRowsById = paymentRequestRows.ToDictionary(item => item.Request.Id);
        var releasesByRequest = new Dictionary<Guid, decimal>();
        var invalidReleaseRequests = new HashSet<Guid>();
        var projectsWithUnmatchedReversal = new HashSet<Guid>();

        foreach (var reversal in reversalRows)
        {
            var originalRequestId = reversal.Detail.ReversesApprovalRequestId;
            if (!originalRequestId.HasValue || !requestRowsById.TryGetValue(originalRequestId.Value, out var original))
            {
                projectsWithUnmatchedReversal.Add(reversal.Detail.ProjectId);
                continue;
            }

            var currency = NormalizeCurrency(original.Request.Currency);
            var transferIsValid = !reversal.Detail.FundingTransferId.HasValue
                || (transfersById.TryGetValue(reversal.Detail.FundingTransferId.Value, out var transfer)
                    && transfer.ProjectId == original.Detail.ProjectId
                    && transfer.PaymentRequestId == original.Request.Id
                    && string.Equals(NormalizeCurrency(transfer.Currency), currency, StringComparison.Ordinal));
            var reversalIsValid = reversal.Request.Amount is > 0m
                && decimal.Round(reversal.Request.Amount.Value, 4) == reversal.Request.Amount.Value
                && string.Equals(NormalizeCurrency(reversal.Request.Currency), currency, StringComparison.Ordinal)
                && reversal.Request.EntityType == ReversalEntityType
                && reversal.Request.EntityId == original.Detail.ProjectId
                && reversal.Detail.ProjectId == original.Detail.ProjectId
                && reversal.Detail.ProjectAnnualBudgetId == original.Detail.ProjectAnnualBudgetId
                && reversal.Detail.AnnualBudgetFiscalYear == original.Detail.AnnualBudgetFiscalYear
                && string.Equals(reversal.Detail.AnnualBudgetReference, original.Detail.AnnualBudgetReference, StringComparison.Ordinal)
                && transferIsValid;

            if (!reversalIsValid)
            {
                invalidReleaseRequests.Add(original.Request.Id);
                continue;
            }

            releasesByRequest[original.Request.Id] = releasesByRequest.GetValueOrDefault(original.Request.Id)
                + reversal.Request.Amount!.Value;
        }

        var requestsByProject = projectIds.ToDictionary(id => id, _ => new List<CashFlowPaymentRequestFact>());
        foreach (var row in paymentRequestRows)
        {
            var expectedDate = ToDateOnly(row.Request.RequiredBy);
            if (!expectedDate.HasValue
                && row.Detail.PlannedPaymentInstallmentId.HasValue
                && activeInstallmentsById.TryGetValue(row.Detail.PlannedPaymentInstallmentId.Value, out var linkedInstallment))
            {
                expectedDate = linkedInstallment.DueDate;
            }

            var releaseIsUnavailable = invalidReleaseRequests.Contains(row.Request.Id)
                || projectsWithUnmatchedReversal.Contains(row.Detail.ProjectId);
            requestsByProject[row.Detail.ProjectId].Add(new CashFlowPaymentRequestFact(
                row.Request.Id,
                row.Detail.RequestKind,
                row.Detail.PlannedPaymentInstallmentId,
                row.Request.Amount,
                row.Request.Currency,
                row.Request.Status,
                expectedDate,
                releaseIsUnavailable ? null : releasesByRequest.GetValueOrDefault(row.Request.Id),
                row.Request.Description ?? row.Request.Title));
        }

        var settlementsByProject = projectIds.ToDictionary(id => id, _ => new List<CashFlowSettlementFact>());
        var settlementFacts = await ActualPaidSettlementCalculator.LoadForProjectsAsync(
            db,
            tenantId,
            projectIds,
            periodFrom: null,
            periodTo: null,
            ignoreQueryFilters: true,
            cancellationToken: cancellationToken);
        var requestsById = requestsByProject.Values
            .SelectMany(items => items)
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var settlement in settlementFacts)
        {
            if (!settlementsByProject.TryGetValue(settlement.ProjectId, out var projectSettlements)) continue;
            CashFlowPaymentRequestFact? request = null;
            if (settlement.ProjectPaymentRequestId.HasValue)
                requestsById.TryGetValue(settlement.ProjectPaymentRequestId.Value, out request);

            var installmentId = settlement.PlannedPaymentInstallmentId ?? request?.InstallmentId;
            var expectedDate = request?.ExpectedDate;
            if (!expectedDate.HasValue && installmentId.HasValue
                && activeInstallmentsById.TryGetValue(installmentId.Value, out var linkedInstallment))
            {
                expectedDate = linkedInstallment.DueDate;
            }

            var description = request?.Description;
            if (string.IsNullOrWhiteSpace(description)
                && installmentId.HasValue
                && activeInstallmentsById.TryGetValue(installmentId.Value, out linkedInstallment))
            {
                description = linkedInstallment.Description;
            }

            projectSettlements.Add(new CashFlowSettlementFact(
                settlement.Id,
                settlement.ProjectPaymentRequestId,
                installmentId,
                settlement.PaymentObligationId,
                settlement.Amount,
                settlement.Currency,
                ToDateOnly(settlement.SettlementDate),
                expectedDate,
                description,
                settlement.PlannedPaymentInstallmentId));
        }

        var adjustmentsByProject = projectIds.ToDictionary(id => id, _ => new List<CashFlowAdjustmentFact>());
        foreach (var projectBatch in projectIds.Chunk(QueryBatchSize))
        {
            var adjustments = await db.PaymentSettlementAdjustments.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && projectBatch.Contains(item.ProjectId)
                    && !item.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var item in adjustments)
            {
                adjustmentsByProject[item.ProjectId].Add(new CashFlowAdjustmentFact(
                    item.Id,
                    item.OriginalSettlementId,
                    item.ProjectPaymentRequestId,
                    item.PlannedPaymentInstallmentId,
                    item.PaymentObligationId,
                    item.Amount,
                    item.Currency,
                    item.AdjustmentType,
                    item.Status,
                    ToDateOnly(item.ExecutionDate),
                    ToDateOnly(item.EffectiveDate),
                    item.Reason));
            }
        }

        var reportsByProject = new Dictionary<Guid, ProjectCashFlowDto>(projectCurrencies.Count);
        foreach (var projectId in projectIds)
        {
            reportsByProject[projectId] = ProjectCashFlowCalculator.Calculate(new ProjectCashFlowCalculationInput(
                projectId,
                projectCurrencies[projectId],
                from,
                to,
                granularity,
                installmentsByProject[projectId],
                requestsByProject[projectId],
                settlementsByProject[projectId],
                adjustmentsByProject[projectId]));
        }

        return reportsByProject;
    }

    private static DateOnly? ToDateOnly(DateTime? value) =>
        value.HasValue ? DateOnly.FromDateTime(value.Value) : null;

    private static string NormalizeCurrency(string? currency) =>
        currency?.Trim().ToUpperInvariant() ?? string.Empty;

    private static bool IsCurrencyCode(string currency) =>
        currency.Length == 3 && currency.All(character => character is >= 'A' and <= 'Z');

    private sealed record PaymentRequestRow(ProjectPaymentRequestDetail Detail, ApprovalRequest Request);
    private sealed record ApprovedReversalRow(ProjectPaymentRequestDetail Detail, ApprovalRequest Request);
}
