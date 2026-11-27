using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>
/// Unified Actual Paid source. Confirmed <see cref="PaymentSettlement"/> rows are
/// the only cash-payment facts; each settlement has exactly one owner
/// (PaymentObligation or ProjectPaymentRequest) enforced by the database. The
/// owning project and currency are resolved through the owner record so
/// obligation settlements that do not carry their own currency still report
/// correctly, and request settlements are never silently dropped.
/// </summary>
public static class ActualPaidSettlementCalculator
{
    private const int ProjectBatchSize = 800;

    /// <summary>A confirmed settlement fact with its owner-resolved project and currency.</summary>
    public sealed record SettlementFact(
        Guid Id,
        Guid ProjectId,
        Guid? PaymentObligationId,
        Guid? ProjectPaymentRequestId,
        Guid? PlannedPaymentInstallmentId,
        decimal Amount,
        DateTime SettlementDate,
        string? Currency);

    /// <summary>
    /// Loads every confirmed settlement owned by the project through either
    /// payment rail. The project is resolved from the owning obligation or
    /// payment request, never from the settlement row alone.
    /// </summary>
    public static async Task<IReadOnlyList<SettlementFact>> LoadForProjectAsync(
        DbContext context,
        Guid tenantId,
        Guid projectId,
        DateTime? periodFrom = null,
        DateTime? periodTo = null,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        var facts = new List<SettlementFact>();

        var obligationSettlements = context.Set<PaymentSettlement>().AsNoTracking();
        var obligations = context.Set<PaymentObligation>().AsNoTracking();
        if (ignoreQueryFilters)
        {
            obligationSettlements = obligationSettlements.IgnoreQueryFilters();
            obligations = obligations.IgnoreQueryFilters();
        }

        var obligationRows = await (
            from settlement in obligationSettlements
            join obligation in obligations
                on new { settlement.TenantId, ObligationId = settlement.PaymentObligationId }
                equals new { obligation.TenantId, ObligationId = (Guid?)obligation.Id }
            where settlement.TenantId == tenantId
                && obligation.ProjectId == projectId
                && !settlement.IsDeleted
                && !obligation.IsDeleted
                && settlement.PaymentObligationId.HasValue
                && !settlement.ProjectPaymentRequestId.HasValue
            select new
            {
                settlement.Id,
                settlement.PaymentObligationId,
                settlement.ProjectPaymentRequestId,
                settlement.Amount,
                settlement.SettlementDate,
                settlement.Currency,
                ObligationCurrency = obligation.Currency
            }).ToListAsync(cancellationToken);

        facts.AddRange(obligationRows.Select(item => new SettlementFact(
            item.Id,
            projectId,
            item.PaymentObligationId,
            item.ProjectPaymentRequestId,
            null,
            item.Amount,
            item.SettlementDate,
            ResolveCurrency(item.Currency, item.ObligationCurrency))));

        var requestSettlements = context.Set<PaymentSettlement>().AsNoTracking();
        var requestDetails = context.Set<ProjectPaymentRequestDetail>().AsNoTracking();
        if (ignoreQueryFilters)
        {
            requestSettlements = requestSettlements.IgnoreQueryFilters();
            requestDetails = requestDetails.IgnoreQueryFilters();
        }

        var requestRows = await (
            from settlement in requestSettlements
            join detail in requestDetails
                on new { settlement.TenantId, RequestId = settlement.ProjectPaymentRequestId }
                equals new { detail.TenantId, RequestId = (Guid?)detail.ApprovalRequestId }
            where settlement.TenantId == tenantId
                && detail.ProjectId == projectId
                && !settlement.IsDeleted
                && settlement.ProjectPaymentRequestId.HasValue
                && !settlement.PaymentObligationId.HasValue
            select new
            {
                settlement.Id,
                settlement.PaymentObligationId,
                settlement.ProjectPaymentRequestId,
                settlement.PlannedPaymentInstallmentId,
                settlement.Amount,
                settlement.SettlementDate,
                settlement.Currency
            }).ToListAsync(cancellationToken);

        facts.AddRange(requestRows.Select(item => new SettlementFact(
            item.Id,
            projectId,
            item.PaymentObligationId,
            item.ProjectPaymentRequestId,
            item.PlannedPaymentInstallmentId,
            item.Amount,
            item.SettlementDate,
            ResolveCurrency(item.Currency, null))));

        if (periodFrom.HasValue || periodTo.HasValue)
        {
            facts = facts.Where(item =>
                    (!periodFrom.HasValue || item.SettlementDate.Date >= periodFrom.Value.Date)
                    && (!periodTo.HasValue || item.SettlementDate.Date <= periodTo.Value.Date))
                .ToList();
        }

        return facts;
    }

    /// <summary>
    /// Loads the canonical settlement facts for several projects without issuing
    /// one set of queries per project. Each chunk reuses the same disjoint
    /// PaymentObligation and PaymentRequest owner joins as <see cref="LoadForProjectAsync"/>.
    /// </summary>
    public static async Task<IReadOnlyList<SettlementFact>> LoadForProjectsAsync(
        DbContext context,
        Guid tenantId,
        IReadOnlyCollection<Guid> projectIds,
        DateTime? periodFrom = null,
        DateTime? periodTo = null,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(projectIds);
        if (tenantId == Guid.Empty) return [];

        var uniqueProjectIds = projectIds
            .Where(projectId => projectId != Guid.Empty)
            .Distinct()
            .OrderBy(projectId => projectId)
            .ToArray();
        if (uniqueProjectIds.Length == 0) return [];

        var facts = new List<SettlementFact>();
        foreach (var projectIdBatch in uniqueProjectIds.Chunk(ProjectBatchSize))
        {
            var obligationSettlements = context.Set<PaymentSettlement>().AsNoTracking();
            var obligations = context.Set<PaymentObligation>().AsNoTracking();
            if (ignoreQueryFilters)
            {
                obligationSettlements = obligationSettlements.IgnoreQueryFilters();
                obligations = obligations.IgnoreQueryFilters();
            }

            var obligationRows = await (
                from settlement in obligationSettlements
                join obligation in obligations
                    on new { settlement.TenantId, ObligationId = settlement.PaymentObligationId }
                    equals new { obligation.TenantId, ObligationId = (Guid?)obligation.Id }
                where settlement.TenantId == tenantId
                    && projectIdBatch.Contains(obligation.ProjectId)
                    && !settlement.IsDeleted
                    && !obligation.IsDeleted
                    && settlement.PaymentObligationId.HasValue
                    && !settlement.ProjectPaymentRequestId.HasValue
                select new
                {
                    obligation.ProjectId,
                    settlement.Id,
                    settlement.PaymentObligationId,
                    settlement.ProjectPaymentRequestId,
                    settlement.Amount,
                    settlement.SettlementDate,
                    settlement.Currency,
                    ObligationCurrency = obligation.Currency
                }).ToListAsync(cancellationToken);

            facts.AddRange(obligationRows.Select(item => new SettlementFact(
                item.Id,
                item.ProjectId,
                item.PaymentObligationId,
                item.ProjectPaymentRequestId,
                null,
                item.Amount,
                item.SettlementDate,
                ResolveCurrency(item.Currency, item.ObligationCurrency))));

            var requestSettlements = context.Set<PaymentSettlement>().AsNoTracking();
            var requestDetails = context.Set<ProjectPaymentRequestDetail>().AsNoTracking();
            if (ignoreQueryFilters)
            {
                requestSettlements = requestSettlements.IgnoreQueryFilters();
                requestDetails = requestDetails.IgnoreQueryFilters();
            }

            var requestRows = await (
                from settlement in requestSettlements
                join detail in requestDetails
                    on new { settlement.TenantId, RequestId = settlement.ProjectPaymentRequestId }
                    equals new { detail.TenantId, RequestId = (Guid?)detail.ApprovalRequestId }
                where settlement.TenantId == tenantId
                    && projectIdBatch.Contains(detail.ProjectId)
                    && !settlement.IsDeleted
                    && settlement.ProjectPaymentRequestId.HasValue
                    && !settlement.PaymentObligationId.HasValue
                select new
                {
                    detail.ProjectId,
                    settlement.Id,
                    settlement.PaymentObligationId,
                    settlement.ProjectPaymentRequestId,
                    settlement.PlannedPaymentInstallmentId,
                    settlement.Amount,
                    settlement.SettlementDate,
                    settlement.Currency
                }).ToListAsync(cancellationToken);

            facts.AddRange(requestRows.Select(item => new SettlementFact(
                item.Id,
                item.ProjectId,
                item.PaymentObligationId,
                item.ProjectPaymentRequestId,
                item.PlannedPaymentInstallmentId,
                item.Amount,
                item.SettlementDate,
                ResolveCurrency(item.Currency, null))));
        }

        if (periodFrom.HasValue || periodTo.HasValue)
        {
            facts = facts.Where(item =>
                    (!periodFrom.HasValue || item.SettlementDate.Date >= periodFrom.Value.Date)
                    && (!periodTo.HasValue || item.SettlementDate.Date <= periodTo.Value.Date))
                .ToList();
        }

        return facts
            .OrderBy(item => item.ProjectId)
            .ThenBy(item => item.SettlementDate)
            .ThenBy(item => item.Id)
            .ToArray();
    }

    /// <summary>
    /// Sums confirmed settlements in the normalized currency within the optional
    /// reporting period. Settlements in any other currency are excluded, never
    /// converted or silently merged.
    /// </summary>
    public static decimal SumActualPaid(
        IEnumerable<SettlementFact> settlements,
        string normalizedCurrency,
        DateTime? periodFrom = null,
        DateTime? periodTo = null) =>
        settlements
            .Where(item => NormalizeCurrency(item.Currency) == normalizedCurrency
                && (!periodFrom.HasValue || item.SettlementDate.Date >= periodFrom.Value.Date)
                && (!periodTo.HasValue || item.SettlementDate.Date <= periodTo.Value.Date))
            .Sum(item => item.Amount);

    /// <summary>True when any loaded settlement lacks a resolvable currency.</summary>
    public static bool HasUnresolvedCurrency(IEnumerable<SettlementFact> settlements) =>
        settlements.Any(item => NormalizeCurrency(item.Currency).Length == 0);

    /// <summary>True when any loaded settlement resolves to a currency other than the target.</summary>
    public static bool HasForeignCurrency(IEnumerable<SettlementFact> settlements, string normalizedCurrency) =>
        settlements.Any(item =>
        {
            var currency = NormalizeCurrency(item.Currency);
            return currency.Length > 0 && currency != normalizedCurrency;
        });

    /// <summary>
    /// Resolves confirmed Payment Request settlements to Planned Payment installments.
    /// New rows use the direct installment FK; legacy rows are resolved through their
    /// original Payment Request detail. The direct FK wins so a row is counted once.
    /// Persisted installment ActualPaid values are never used as the source of truth.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, decimal>> LoadForInstallmentsAsync(
        DbContext context,
        Guid tenantId,
        Guid projectId,
        IReadOnlyCollection<Guid> installmentIds,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default,
        string? normalizedCurrency = null,
        DateTime? periodTo = null)
    {
        if (installmentIds.Count == 0) return new Dictionary<Guid, decimal>();

        var facts = await LoadForProjectAsync(
            context, tenantId, projectId, periodTo: periodTo, ignoreQueryFilters: ignoreQueryFilters,
            cancellationToken: cancellationToken);
        var requestIds = facts.Where(item => item.ProjectPaymentRequestId.HasValue)
            .Select(item => item.ProjectPaymentRequestId!.Value).Distinct().ToArray();
        var detailsQuery = context.Set<ProjectPaymentRequestDetail>().AsNoTracking();
        if (ignoreQueryFilters) detailsQuery = detailsQuery.IgnoreQueryFilters();
        var details = requestIds.Length == 0
            ? []
            : await detailsQuery
                .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                    && item.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                    && requestIds.Contains(item.ApprovalRequestId)
                    && item.PlannedPaymentInstallmentId.HasValue)
                .Select(item => new { item.ApprovalRequestId, InstallmentId = item.PlannedPaymentInstallmentId!.Value })
                .ToListAsync(cancellationToken);
        var installmentByRequest = details.ToDictionary(item => item.ApprovalRequestId, item => item.InstallmentId);
        var requestedInstallments = installmentIds.ToHashSet();
        var result = new Dictionary<Guid, decimal>();
        foreach (var fact in facts)
        {
            if (!fact.ProjectPaymentRequestId.HasValue) continue;
            if (!string.IsNullOrWhiteSpace(normalizedCurrency)
                && NormalizeCurrency(fact.Currency) != NormalizeCurrency(normalizedCurrency)) continue;
            Guid installmentId;
            if (fact.PlannedPaymentInstallmentId.HasValue)
                installmentId = fact.PlannedPaymentInstallmentId.Value;
            else if (!installmentByRequest.TryGetValue(fact.ProjectPaymentRequestId.Value, out installmentId))
                continue;
            if (!requestedInstallments.Contains(installmentId)) continue;
            result[installmentId] = result.GetValueOrDefault(installmentId) + fact.Amount;
        }
        return result;
    }

    public static async Task<decimal> SumNetAdjustmentsAsync(
        DbContext context, Guid tenantId, Guid projectId, string normalizedCurrency,
        DateTime? periodFrom = null,
        DateTime? periodTo = null,
        CancellationToken cancellationToken = default)
    {
        var adjustments = context.Set<PaymentSettlementAdjustment>().AsNoTracking();
        var query = adjustments.Where(item => item.TenantId == tenantId && item.ProjectId == projectId
            && item.Status == PaymentSettlementAdjustment.Executed && !item.IsDeleted);
        if (periodFrom.HasValue)
            query = query.Where(item => (item.ExecutionDate ?? item.EffectiveDate) >= periodFrom.Value.Date);
        if (periodTo.HasValue)
            query = query.Where(item => (item.ExecutionDate ?? item.EffectiveDate) < periodTo.Value.Date.AddDays(1));
        var rows = await query
            .ToListAsync(cancellationToken);
        return rows
            .Where(item => NormalizeCurrency(item.Currency) == normalizedCurrency)
            .Sum(item => item.AdjustmentType == PaymentSettlementAdjustment.Refund ? -item.Amount : item.Amount);
    }

    public static string NormalizeCurrency(string? currency) =>
        currency?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string? ResolveCurrency(string? settlementCurrency, string? ownerCurrency)
    {
        if (!string.IsNullOrWhiteSpace(settlementCurrency)) return settlementCurrency;
        return string.IsNullOrWhiteSpace(ownerCurrency) ? null : ownerCurrency;
    }
}
