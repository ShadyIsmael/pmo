using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>
/// Pure project Cash Flow projection over normalized source facts. It performs
/// no database access and never combines amounts across currency groups.
/// </summary>
public static class ProjectCashFlowCalculator
{
    public static ProjectCashFlowDto Calculate(ProjectCashFlowCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Installments);
        ArgumentNullException.ThrowIfNull(input.PaymentRequests);
        ArgumentNullException.ThrowIfNull(input.Settlements);
        ArgumentNullException.ThrowIfNull(input.Adjustments);
        if (input.PeriodFrom.HasValue && input.PeriodTo.HasValue
            && input.PeriodFrom.Value > input.PeriodTo.Value)
            throw new ArgumentException("The Cash Flow start date must not be after the end date.", nameof(input));
        if (!Enum.IsDefined(input.Granularity))
            throw new ArgumentOutOfRangeException(nameof(input), "Cash Flow granularity must be Day or Month.");

        var issues = new IssueCollector();
        var groups = new Dictionary<string, CurrencyAccumulator>(StringComparer.Ordinal);
        var projectCurrency = NormalizeCurrency(input.ProjectCurrency);
        var projectCurrencyIsValid = IsCurrencyCode(projectCurrency);

        if (!projectCurrencyIsValid)
        {
            issues.Add(
                CashFlowQualityCodes.ProjectCurrencyUnavailable,
                CashFlowSourceTypes.PlannedInstallment,
                null,
                null,
                null,
                null,
                null);
            groups[string.Empty] = new CurrencyAccumulator(null);
            groups[string.Empty].QualityCodes.Add(CashFlowQualityCodes.ProjectCurrencyUnavailable);
        }
        else
        {
            groups[projectCurrency] = new CurrencyAccumulator(projectCurrency);
        }

        var installments = NormalizeInstallments(input.Installments, issues);
        var paymentRequests = NormalizePaymentRequests(input.PaymentRequests, issues);
        var settlements = NormalizeSettlements(input.Settlements, issues);
        var adjustments = NormalizeAdjustments(input.Adjustments, issues);
        var installmentById = installments.ToDictionary(item => item.Id);
        var settlementById = settlements.ToDictionary(item => item.Id);
        var paymentRequestById = paymentRequests
            .Where(item => item.RequestKind == ProjectPaymentRequestKinds.PaymentRequest)
            .ToDictionary(item => item.Id);

        CurrencyAccumulator GetGroup(
            string? rawCurrency,
            string sourceType,
            Guid? sourceId,
            Guid? installmentId = null,
            Guid? requestId = null,
            Guid? obligationId = null,
            HashSet<string>? lineCodes = null)
        {
            var currency = NormalizeCurrency(rawCurrency);
            if (!groups.TryGetValue(currency, out var group))
            {
                group = new CurrencyAccumulator(currency.Length == 0 ? null : currency);
                groups.Add(currency, group);
            }

            if (currency.Length == 0)
            {
                AddIssue(
                    issues, group, CashFlowQualityCodes.UnresolvedSourceCurrency,
                    sourceType, sourceId, installmentId, requestId, obligationId, null, lineCodes);
            }
            else if (projectCurrencyIsValid && currency != projectCurrency)
            {
                AddIssue(
                    issues, group, CashFlowQualityCodes.ForeignCurrencySource,
                    sourceType, sourceId, installmentId, requestId, obligationId, currency, lineCodes);
            }

            return group;
        }

        void MarkAdjustmentUnavailable(
            CurrencyAccumulator adjustmentGroup,
            CashFlowAdjustmentFact adjustment,
            string code,
            ISet<string> lineCodes,
            CashFlowSettlementFact? sourceSettlement = null)
        {
            adjustmentGroup.AdjustmentsUnavailable = true;
            AddIssue(
                issues, adjustmentGroup, code,
                CashFlowSourceTypes.PaymentSettlementAdjustment, adjustment.Id,
                adjustment.InstallmentId, adjustment.PaymentRequestId,
                adjustment.PaymentObligationId, adjustment.Currency, lineCodes);

            if (sourceSettlement is null) return;

            var sourceCurrencyKey = NormalizeCurrency(sourceSettlement.Currency);
            if (!groups.TryGetValue(sourceCurrencyKey, out var sourceGroup)) return;
            sourceGroup.AdjustmentsUnavailable = true;
            AddIssue(
                issues, sourceGroup, code,
                CashFlowSourceTypes.PaymentSettlementAdjustment, adjustment.Id,
                adjustment.InstallmentId, adjustment.PaymentRequestId,
                adjustment.PaymentObligationId, sourceSettlement.Currency, lineCodes);
        }

        var requestSettlementTotals = new Dictionary<Guid, decimal>();
        var requestSettlementCurrencyMismatch = new HashSet<Guid>();
        var requestSettlementMissingAmount = new HashSet<Guid>();
        var installmentSettlementTotals = new Dictionary<Guid, decimal>();
        var installmentSettlementCurrencyMismatch = new HashSet<Guid>();
        var installmentSettlementMissingAmount = new HashSet<Guid>();
        var linkedInstallmentBySettlement = new Dictionary<Guid, Guid>();

        foreach (var settlement in settlements.OrderBy(item => item.Id))
        {
            var lineCodes = new HashSet<string>(StringComparer.Ordinal);
            var group = GetGroup(
                settlement.Currency,
                CashFlowSourceTypes.PaymentSettlement,
                settlement.Id,
                settlement.InstallmentId,
                settlement.PaymentRequestId,
                settlement.PaymentObligationId,
                lineCodes);
            var settlementCurrency = NormalizeCurrency(settlement.Currency);
            if (settlement.Amount is null)
            {
                group.ActualUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.MissingSettlementAmount,
                    CashFlowSourceTypes.PaymentSettlement, settlement.Id,
                    settlement.InstallmentId, settlement.PaymentRequestId,
                    settlement.PaymentObligationId, settlement.Currency, lineCodes);
            }
            else if (settlement.Amount <= 0m)
            {
                group.ActualUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.InvalidSettlementAmount,
                    CashFlowSourceTypes.PaymentSettlement, settlement.Id,
                    settlement.InstallmentId, settlement.PaymentRequestId,
                    settlement.PaymentObligationId, settlement.Currency, lineCodes);
            }

            if (!settlement.SettlementDate.HasValue)
            {
                AddIssue(
                    issues, group, CashFlowQualityCodes.UnknownActualSettlementDate,
                    CashFlowSourceTypes.PaymentSettlement, settlement.Id,
                    settlement.InstallmentId, settlement.PaymentRequestId,
                    settlement.PaymentObligationId, settlement.Currency, lineCodes);
            }

            var request = settlement.PaymentRequestId.HasValue
                ? paymentRequestById.GetValueOrDefault(settlement.PaymentRequestId.Value)
                : null;
            if (settlement.PaymentRequestId.HasValue && request is null)
            {
                group.CommittedUnavailable = true;
                group.PlannedUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.MissingSettlementRequest,
                    CashFlowSourceTypes.PaymentSettlement, settlement.Id,
                    settlement.InstallmentId, settlement.PaymentRequestId,
                    settlement.PaymentObligationId, settlement.Currency, lineCodes);
            }

            var linkedInstallmentId = settlement.InstallmentId;
            if (request is not null)
            {
                if (linkedInstallmentId.HasValue && request.InstallmentId.HasValue
                    && linkedInstallmentId.Value != request.InstallmentId.Value)
                {
                    group.PlannedUnavailable = true;
                    AddIssue(
                        issues, group, CashFlowQualityCodes.SettlementInstallmentConflict,
                        CashFlowSourceTypes.PaymentSettlement, settlement.Id,
                        linkedInstallmentId, settlement.PaymentRequestId,
                        settlement.PaymentObligationId, settlement.Currency, lineCodes);
                }
                linkedInstallmentId ??= request.InstallmentId;

                var requestGroup = GetGroup(
                    request.Currency,
                    CashFlowSourceTypes.ApprovedPaymentRequest,
                    request.Id,
                    request.InstallmentId,
                    request.Id,
                    lineCodes: lineCodes);
                if (settlementCurrency.Length == 0
                    || NormalizeCurrency(request.Currency) != settlementCurrency)
                {
                    requestSettlementCurrencyMismatch.Add(request.Id);
                    requestGroup.CommittedUnavailable = true;
                    requestGroup.PlannedUnavailable = true;
                    AddIssue(
                        issues, requestGroup, CashFlowQualityCodes.CurrencyMismatch,
                        CashFlowSourceTypes.PaymentSettlement, settlement.Id,
                        linkedInstallmentId, request.Id, settlement.PaymentObligationId,
                        settlement.Currency, lineCodes);
                }
                else if (settlement.Amount is null || settlement.Amount <= 0m)
                {
                    requestSettlementMissingAmount.Add(request.Id);
                    requestGroup.CommittedUnavailable = true;
                    requestGroup.PlannedUnavailable = true;
                }
                else
                {
                    requestSettlementTotals[request.Id] =
                        requestSettlementTotals.GetValueOrDefault(request.Id) + settlement.Amount.Value;
                }
            }

            if (linkedInstallmentId.HasValue)
            {
                linkedInstallmentBySettlement[settlement.Id] = linkedInstallmentId.Value;
                if (!installmentById.TryGetValue(linkedInstallmentId.Value, out var installment))
                {
                    group.PlannedUnavailable = true;
                    AddIssue(
                        issues, group, CashFlowQualityCodes.MissingSettlementReference,
                        CashFlowSourceTypes.PaymentSettlement, settlement.Id,
                        linkedInstallmentId, settlement.PaymentRequestId,
                        settlement.PaymentObligationId, settlement.Currency, lineCodes);
                }
                else
                {
                    var installmentGroup = GetGroup(
                        installment.Currency,
                        CashFlowSourceTypes.PlannedInstallment,
                        installment.Id,
                        installment.Id,
                        settlement.PaymentRequestId,
                        settlement.PaymentObligationId,
                        lineCodes);
                    if (settlementCurrency.Length == 0
                        || NormalizeCurrency(installment.Currency) != settlementCurrency)
                    {
                        installmentSettlementCurrencyMismatch.Add(installment.Id);
                        installmentGroup.PlannedUnavailable = true;
                        AddIssue(
                            issues, installmentGroup, CashFlowQualityCodes.CurrencyMismatch,
                            CashFlowSourceTypes.PaymentSettlement, settlement.Id,
                            installment.Id, settlement.PaymentRequestId,
                            settlement.PaymentObligationId, settlement.Currency, lineCodes);
                    }
                    else if (settlement.Amount is null || settlement.Amount <= 0m)
                    {
                        installmentSettlementMissingAmount.Add(installment.Id);
                        installmentGroup.PlannedUnavailable = true;
                    }
                    else
                    {
                        installmentSettlementTotals[installment.Id] =
                            installmentSettlementTotals.GetValueOrDefault(installment.Id) + settlement.Amount.Value;
                    }
                }
            }
            else
            {
                group.PlannedUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.MissingSettlementReference,
                    CashFlowSourceTypes.PaymentSettlement, settlement.Id,
                    null, settlement.PaymentRequestId,
                    settlement.PaymentObligationId, settlement.Currency, lineCodes);
            }

            var expectedDate = ResolveSettlementExpectedDate(
                settlement, request, linkedInstallmentId, installmentById);
            if (expectedDate is null && (settlement.PaymentRequestId.HasValue || settlement.InstallmentId.HasValue))
            {
                AddIssue(
                    issues, group, CashFlowQualityCodes.UnknownActualExpectedDate,
                    CashFlowSourceTypes.PaymentSettlement, settlement.Id,
                    linkedInstallmentId, settlement.PaymentRequestId,
                    settlement.PaymentObligationId, settlement.Currency, lineCodes);
            }

            var timingVariance = settlement.SettlementDate.HasValue && expectedDate.HasValue
                ? settlement.SettlementDate.Value.DayNumber - expectedDate.Value.DayNumber
                : (int?)null;
            AddLine(group, new CashFlowLineDto(
                    CashFlowMeasure.ActualGross,
                    CashFlowSourceTypes.PaymentSettlement,
                    settlement.Id,
                    linkedInstallmentId,
                    settlement.PaymentRequestId,
                    settlement.PaymentObligationId,
                    null,
                    settlement.Description,
                    null,
                    settlement.SettlementDate,
                    expectedDate,
                    timingVariance,
                    settlement.Amount,
                    group.Currency,
                    lineCodes.OrderBy(code => code, StringComparer.Ordinal).ToArray()),
                input.Granularity, input.PeriodFrom, input.PeriodTo);
        }

        var requestBalances = new Dictionary<Guid, RequestBalance>();
        foreach (var request in paymentRequests
                     .Where(item => item.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                         && IsStatus(item.Status, PaymentRequestStatuses.Approved))
                     .OrderBy(item => item.Id))
        {
            var lineCodes = new HashSet<string>(StringComparer.Ordinal);
            var group = GetGroup(
                request.Currency,
                CashFlowSourceTypes.ApprovedPaymentRequest,
                request.Id,
                request.InstallmentId,
                request.Id,
                lineCodes: lineCodes);
            var requestCurrency = NormalizeCurrency(request.Currency);
            var settled = requestSettlementTotals.GetValueOrDefault(request.Id);
            decimal? remaining = null;

            if (request.Amount is null)
            {
                group.CommittedUnavailable = true;
                group.PlannedUnavailable = request.InstallmentId.HasValue || group.Currency is null;
                AddIssue(
                    issues, group, CashFlowQualityCodes.MissingRequestAmount,
                    CashFlowSourceTypes.ApprovedPaymentRequest, request.Id,
                    request.InstallmentId, request.Id, null, request.Currency, lineCodes);
            }
            else if (request.Amount <= 0m)
            {
                group.CommittedUnavailable = true;
                group.PlannedUnavailable = request.InstallmentId.HasValue || group.Currency is null;
                AddIssue(
                    issues, group, CashFlowQualityCodes.InvalidRequestAmount,
                    CashFlowSourceTypes.ApprovedPaymentRequest, request.Id,
                    request.InstallmentId, request.Id, null, request.Currency, lineCodes);
            }
            else if (request.ApprovedUnpaidReleaseAmount is null)
            {
                group.CommittedUnavailable = true;
                group.PlannedUnavailable = request.InstallmentId.HasValue || group.Currency is null;
                AddIssue(
                    issues, group, CashFlowQualityCodes.MissingRequestReleaseAmount,
                    CashFlowSourceTypes.ApprovedPaymentRequest, request.Id,
                    request.InstallmentId, request.Id, null, request.Currency, lineCodes);
            }
            else if (request.ApprovedUnpaidReleaseAmount < 0m)
            {
                group.CommittedUnavailable = true;
                group.PlannedUnavailable = request.InstallmentId.HasValue || group.Currency is null;
                AddIssue(
                    issues, group, CashFlowQualityCodes.InvalidRequestReleaseAmount,
                    CashFlowSourceTypes.ApprovedPaymentRequest, request.Id,
                    request.InstallmentId, request.Id, null, request.Currency, lineCodes);
            }
            else if (requestSettlementCurrencyMismatch.Contains(request.Id)
                || requestSettlementMissingAmount.Contains(request.Id))
            {
                group.CommittedUnavailable = true;
                group.PlannedUnavailable = request.InstallmentId.HasValue || group.Currency is null;
            }
            else if (settled + request.ApprovedUnpaidReleaseAmount.Value > request.Amount.Value)
            {
                group.CommittedUnavailable = true;
                group.PlannedUnavailable = request.InstallmentId.HasValue || group.Currency is null;
                AddIssue(
                    issues,
                    group,
                    request.ApprovedUnpaidReleaseAmount.Value > 0m
                        ? CashFlowQualityCodes.RequestReleaseExceedsAmount
                        : CashFlowQualityCodes.RequestSettlementExceedsAmount,
                    CashFlowSourceTypes.ApprovedPaymentRequest,
                    request.Id,
                    request.InstallmentId,
                    request.Id,
                    null,
                    request.Currency,
                    lineCodes);
            }
            else if (requestCurrency.Length == 0)
            {
                group.CommittedUnavailable = true;
                group.PlannedUnavailable = true;
            }
            else
            {
                remaining = Math.Max(0m, request.Amount.Value - settled - request.ApprovedUnpaidReleaseAmount.Value);
            }

            var linkedInstallment = request.InstallmentId.HasValue
                && installmentById.TryGetValue(request.InstallmentId.Value, out var foundInstallment)
                ? foundInstallment
                : null;
            var expectedDate = request.ExpectedDate ?? linkedInstallment?.DueDate;
            if (!request.InstallmentId.HasValue || linkedInstallment is null)
            {
                group.PlannedUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.MissingRequestInstallmentLink,
                    CashFlowSourceTypes.ApprovedPaymentRequest, request.Id,
                    request.InstallmentId, request.Id, null, request.Currency, lineCodes);
            }
            if (!expectedDate.HasValue)
            {
                AddIssue(
                    issues, group, CashFlowQualityCodes.UnknownCommittedExpectedDate,
                    CashFlowSourceTypes.ApprovedPaymentRequest, request.Id,
                    request.InstallmentId, request.Id, null, request.Currency, lineCodes);
            }

            requestBalances[request.Id] = new RequestBalance(request, remaining, expectedDate);
            if (remaining is > 0m)
            {
                AddLine(group, new CashFlowLineDto(
                        CashFlowMeasure.CommittedUnpaid,
                        CashFlowSourceTypes.ApprovedPaymentRequest,
                        request.Id,
                        request.InstallmentId,
                        request.Id,
                        null,
                        null,
                        request.Description,
                        request.Status,
                        expectedDate,
                        expectedDate,
                        null,
                        remaining,
                        group.Currency,
                        lineCodes.OrderBy(code => code, StringComparer.Ordinal).ToArray()),
                    input.Granularity, input.PeriodFrom, input.PeriodTo);
            }
        }

        foreach (var installment in installments
                     .Where(IsEligibleInstallment)
                     .OrderBy(item => item.Id))
        {
            var lineCodes = new HashSet<string>(StringComparer.Ordinal);
            var group = GetGroup(
                installment.Currency,
                CashFlowSourceTypes.PlannedInstallment,
                installment.Id,
                installment.Id,
                lineCodes: lineCodes);
            if (installment.Amount is null)
            {
                group.PlannedUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.MissingInstallmentAmount,
                    CashFlowSourceTypes.PlannedInstallment, installment.Id,
                    installment.Id, null, null, installment.Currency, lineCodes);
                continue;
            }
            if (installment.Amount <= 0m)
            {
                group.PlannedUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.InvalidInstallmentAmount,
                    CashFlowSourceTypes.PlannedInstallment, installment.Id,
                    installment.Id, null, null, installment.Currency, lineCodes);
                continue;
            }

            if (!installment.DueDate.HasValue)
            {
                AddIssue(
                    issues, group, CashFlowQualityCodes.UnknownPlannedDueDate,
                    CashFlowSourceTypes.PlannedInstallment, installment.Id,
                    installment.Id, null, null, installment.Currency, lineCodes);
            }

            var linkedRequests = requestBalances.Values
                .Where(balance => balance.Request.InstallmentId == installment.Id)
                .ToArray();
            if (installmentSettlementCurrencyMismatch.Contains(installment.Id)
                || installmentSettlementMissingAmount.Contains(installment.Id)
                || linkedRequests.Any(balance => !balance.Remaining.HasValue))
            {
                group.PlannedUnavailable = true;
                continue;
            }

            var committed = linkedRequests.Sum(balance => balance.Remaining!.Value);
            var settled = installmentSettlementTotals.GetValueOrDefault(installment.Id);
            var allocated = settled + committed;
            if (allocated > installment.Amount.Value)
            {
                group.PlannedUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.InstallmentOverallocated,
                    CashFlowSourceTypes.PlannedInstallment, installment.Id,
                    installment.Id, null, null, installment.Currency, lineCodes);
                continue;
            }

            var residual = Math.Max(0m, installment.Amount.Value - allocated);
            if (residual <= 0m) continue;
            AddLine(group, new CashFlowLineDto(
                    CashFlowMeasure.PlannedResidual,
                    CashFlowSourceTypes.PlannedInstallment,
                    installment.Id,
                    installment.Id,
                    null,
                    null,
                    null,
                    installment.Description,
                    installment.State,
                    installment.DueDate,
                    installment.DueDate,
                    null,
                    residual,
                    group.Currency,
                    lineCodes.OrderBy(code => code, StringComparer.Ordinal).ToArray()),
                input.Granularity, input.PeriodFrom, input.PeriodTo);
        }

        foreach (var adjustment in adjustments
                     .Where(item => IsStatus(item.Status, PaymentSettlementAdjustment.Executed))
                     .OrderBy(item => item.Id))
        {
            var lineCodes = new HashSet<string>(StringComparer.Ordinal);
            var group = GetGroup(
                adjustment.Currency,
                CashFlowSourceTypes.PaymentSettlementAdjustment,
                adjustment.Id,
                adjustment.InstallmentId,
                adjustment.PaymentRequestId,
                adjustment.PaymentObligationId,
                lineCodes);
            var eventDate = adjustment.ExecutionDate ?? adjustment.EffectiveDate;
            if (!adjustment.OriginalSettlementId.HasValue)
            {
                MarkAdjustmentUnavailable(
                    group, adjustment, CashFlowQualityCodes.MissingAdjustmentSettlementReference, lineCodes);
            }
            else if (!settlementById.TryGetValue(adjustment.OriginalSettlementId.Value, out var sourceSettlement))
            {
                MarkAdjustmentUnavailable(
                    group, adjustment, CashFlowQualityCodes.MissingAdjustmentSettlementReference, lineCodes);
            }
            else
            {
                var adjustmentCurrency = NormalizeCurrency(adjustment.Currency);
                var sourceCurrency = NormalizeCurrency(sourceSettlement.Currency);
                if (adjustmentCurrency.Length == 0
                    || sourceCurrency.Length == 0
                    || adjustmentCurrency != sourceCurrency)
                {
                    MarkAdjustmentUnavailable(
                        group, adjustment, CashFlowQualityCodes.CurrencyMismatch, lineCodes, sourceSettlement);
                }

                if (adjustment.PaymentRequestId != sourceSettlement.PaymentRequestId
                    || adjustment.PaymentObligationId != sourceSettlement.PaymentObligationId)
                {
                    MarkAdjustmentUnavailable(
                        group, adjustment, CashFlowQualityCodes.ConflictingSourceRows, lineCodes, sourceSettlement);
                }

                // The workflow copies the settlement's direct installment link onto
                // the adjustment. Keep this raw source field separate from the
                // request-derived effective link used for cash-flow allocation.
                var installmentLinkMatches = adjustment.InstallmentId == sourceSettlement.DirectInstallmentId;
                if (!installmentLinkMatches)
                {
                    MarkAdjustmentUnavailable(
                        group, adjustment, CashFlowQualityCodes.SettlementInstallmentConflict, lineCodes, sourceSettlement);
                }
            }
            if (!eventDate.HasValue)
            {
                group.AdjustmentsUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.UnknownAdjustmentDate,
                    CashFlowSourceTypes.PaymentSettlementAdjustment, adjustment.Id,
                    adjustment.InstallmentId, adjustment.PaymentRequestId,
                    adjustment.PaymentObligationId, adjustment.Currency, lineCodes);
            }

            decimal? signedAmount = null;
            if (adjustment.Amount is null)
            {
                group.AdjustmentsUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.MissingAdjustmentAmount,
                    CashFlowSourceTypes.PaymentSettlementAdjustment, adjustment.Id,
                    adjustment.InstallmentId, adjustment.PaymentRequestId,
                    adjustment.PaymentObligationId, adjustment.Currency, lineCodes);
            }
            else if (adjustment.Amount <= 0m)
            {
                group.AdjustmentsUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.InvalidAdjustmentAmount,
                    CashFlowSourceTypes.PaymentSettlementAdjustment, adjustment.Id,
                    adjustment.InstallmentId, adjustment.PaymentRequestId,
                    adjustment.PaymentObligationId, adjustment.Currency, lineCodes);
            }
            else if (IsStatus(adjustment.Type, PaymentSettlementAdjustment.Refund))
            {
                signedAmount = -adjustment.Amount.Value;
            }
            else if (IsStatus(adjustment.Type, PaymentSettlementAdjustment.Correction))
            {
                signedAmount = adjustment.Amount.Value;
            }
            else
            {
                group.AdjustmentsUnavailable = true;
                AddIssue(
                    issues, group, CashFlowQualityCodes.UnknownAdjustmentType,
                    CashFlowSourceTypes.PaymentSettlementAdjustment, adjustment.Id,
                    adjustment.InstallmentId, adjustment.PaymentRequestId,
                    adjustment.PaymentObligationId, adjustment.Currency, lineCodes);
            }

            AddLine(group, new CashFlowLineDto(
                    CashFlowMeasure.ExecutedAdjustment,
                    CashFlowSourceTypes.PaymentSettlementAdjustment,
                    adjustment.Id,
                    adjustment.InstallmentId,
                    adjustment.PaymentRequestId,
                    adjustment.PaymentObligationId,
                    adjustment.OriginalSettlementId,
                    adjustment.Description,
                    adjustment.Status,
                    eventDate,
                    null,
                    null,
                    signedAmount,
                    group.Currency,
                    lineCodes.OrderBy(code => code, StringComparer.Ordinal).ToArray()),
                input.Granularity, input.PeriodFrom, input.PeriodTo);
        }

        var currencyGroups = groups.Values
            .OrderBy(group => group.Currency is null ? 1 : 0)
            .ThenBy(group => group.Currency, StringComparer.Ordinal)
            .Select(group => ToDto(group))
            .ToArray();
        var overallStatus = currencyGroups.Length == 0
            || currencyGroups.All(group => group.DataQualityStatus == CashFlowDataQualityStatus.Unavailable)
                ? CashFlowDataQualityStatus.Unavailable
                : issues.Items.Count > 0
                    ? CashFlowDataQualityStatus.Partial
                    : CashFlowDataQualityStatus.Available;

        return new ProjectCashFlowDto(
            input.ProjectId,
            input.PeriodFrom,
            input.PeriodTo,
            input.Granularity,
            overallStatus,
            issues.Items,
            currencyGroups);
    }

    private static IReadOnlyList<CashFlowInstallmentFact> NormalizeInstallments(
        IReadOnlyList<CashFlowInstallmentFact> rows,
        IssueCollector issues)
    {
        var result = new List<CashFlowInstallmentFact>();
        foreach (var set in rows.GroupBy(item => item.Id).OrderBy(set => set.Key))
        {
            var unique = set.Distinct().ToArray();
            var item = unique[0];
            if (unique.Length > 1)
            {
                issues.Add(CashFlowQualityCodes.ConflictingSourceRows, CashFlowSourceTypes.PlannedInstallment,
                    item.Id, item.Id, null, null, item.Currency);
                item = item with { Amount = null, Currency = null, DueDate = null };
            }
            result.Add(item);
        }
        return result;
    }

    private static IReadOnlyList<CashFlowPaymentRequestFact> NormalizePaymentRequests(
        IReadOnlyList<CashFlowPaymentRequestFact> rows,
        IssueCollector issues)
    {
        var result = new List<CashFlowPaymentRequestFact>();
        foreach (var set in rows.GroupBy(item => item.Id).OrderBy(set => set.Key))
        {
            var unique = set.Distinct().ToArray();
            var item = unique[0];
            if (unique.Length > 1)
            {
                issues.Add(CashFlowQualityCodes.ConflictingSourceRows, CashFlowSourceTypes.ApprovedPaymentRequest,
                    item.Id, item.InstallmentId, item.Id, null, item.Currency);
                item = item with
                {
                    Amount = null,
                    Currency = null,
                    ExpectedDate = null,
                    ApprovedUnpaidReleaseAmount = null
                };
            }
            result.Add(item);
        }
        return result;
    }

    private static IReadOnlyList<CashFlowSettlementFact> NormalizeSettlements(
        IReadOnlyList<CashFlowSettlementFact> rows,
        IssueCollector issues)
    {
        var result = new List<CashFlowSettlementFact>();
        foreach (var set in rows.GroupBy(item => item.Id).OrderBy(set => set.Key))
        {
            var unique = set.Distinct().ToArray();
            var item = unique[0];
            if (set.Count() > 1)
                issues.Add(CashFlowQualityCodes.DuplicateSettlement, CashFlowSourceTypes.PaymentSettlement,
                    item.Id, item.InstallmentId, item.PaymentRequestId, item.PaymentObligationId, item.Currency);
            if (unique.Length > 1)
            {
                issues.Add(CashFlowQualityCodes.ConflictingSourceRows, CashFlowSourceTypes.PaymentSettlement,
                    item.Id, item.InstallmentId, item.PaymentRequestId, item.PaymentObligationId, item.Currency);
                item = item with
                {
                    PaymentRequestId = null,
                    InstallmentId = null,
                    PaymentObligationId = null,
                    Amount = null,
                    Currency = null,
                    SettlementDate = null,
                    ExpectedDate = null
                };
            }
            result.Add(item);
        }
        return result;
    }

    private static IReadOnlyList<CashFlowAdjustmentFact> NormalizeAdjustments(
        IReadOnlyList<CashFlowAdjustmentFact> rows,
        IssueCollector issues)
    {
        var result = new List<CashFlowAdjustmentFact>();
        foreach (var set in rows.GroupBy(item => item.Id).OrderBy(set => set.Key))
        {
            var unique = set.Distinct().ToArray();
            var item = unique[0];
            if (set.Count() > 1 && unique.Length > 1)
            {
                issues.Add(CashFlowQualityCodes.ConflictingSourceRows,
                    CashFlowSourceTypes.PaymentSettlementAdjustment, item.Id,
                    item.InstallmentId, item.PaymentRequestId, item.PaymentObligationId, item.Currency);
                item = item with { Amount = null, Currency = null, ExecutionDate = null, EffectiveDate = null };
            }
            result.Add(item);
        }
        return result;
    }

    private static bool IsEligibleInstallment(CashFlowInstallmentFact item) =>
        item.IsActive
        && (IsStatus(item.State, PlannedPaymentStates.Registered)
            || IsStatus(item.State, PlannedPaymentStates.Locked));

    private static DateOnly? ResolveSettlementExpectedDate(
        CashFlowSettlementFact settlement,
        CashFlowPaymentRequestFact? request,
        Guid? linkedInstallmentId,
        IReadOnlyDictionary<Guid, CashFlowInstallmentFact> installmentById)
    {
        if (settlement.ExpectedDate.HasValue) return settlement.ExpectedDate;
        if (request?.ExpectedDate is { } requestExpectedDate) return requestExpectedDate;
        if (linkedInstallmentId.HasValue
            && installmentById.TryGetValue(linkedInstallmentId.Value, out var installment))
            return installment.DueDate;
        return null;
    }

    private static void AddLine(
        CurrencyAccumulator group,
        CashFlowLineDto line,
        CashFlowGranularity granularity,
        DateOnly? periodFrom,
        DateOnly? periodTo)
    {
        if (line.EventDate.HasValue
            && ((periodFrom.HasValue && line.EventDate.Value < periodFrom.Value)
                || (periodTo.HasValue && line.EventDate.Value > periodTo.Value)))
            return;

        var bucket = line.EventDate.HasValue
            ? group.GetBucket(GetPeriodStart(line.EventDate.Value, granularity))
            : group.Undated;
        bucket.Items.Add(line);
        if (!line.Amount.HasValue) return;

        switch (line.Measure)
        {
            case CashFlowMeasure.PlannedResidual:
                bucket.PlannedResidual += line.Amount.Value;
                break;
            case CashFlowMeasure.CommittedUnpaid:
                bucket.CommittedUnpaid += line.Amount.Value;
                break;
            case CashFlowMeasure.ActualGross:
                bucket.ActualGross += line.Amount.Value;
                break;
            case CashFlowMeasure.ExecutedAdjustment:
                bucket.ExecutedAdjustments += line.Amount.Value;
                break;
        }
    }

    private static ProjectCashFlowCurrencyGroupDto ToDto(CurrencyAccumulator group)
    {
        var status = group.Currency is null
            ? CashFlowDataQualityStatus.Unavailable
            : group.QualityCodes.Count > 0
                ? CashFlowDataQualityStatus.Partial
                : CashFlowDataQualityStatus.Available;
        return new ProjectCashFlowCurrencyGroupDto(
            group.Currency,
            status,
            group.Buckets.Select(pair => ToDto(pair.Key, pair.Value, group)).ToArray(),
            ToUndatedDto(group.Undated, group));
    }

    private static CashFlowBucketDto ToDto(
        DateOnly periodStart,
        BucketAccumulator bucket,
        CurrencyAccumulator group) =>
        new(
            periodStart,
            group.Currency is null || group.PlannedUnavailable ? null : bucket.PlannedResidual,
            group.Currency is null || group.CommittedUnavailable ? null : bucket.CommittedUnpaid,
            group.Currency is null || group.ActualUnavailable ? null : bucket.ActualGross,
            group.Currency is null || group.AdjustmentsUnavailable ? null : bucket.ExecutedAdjustments,
            group.Currency is null || group.ActualUnavailable || group.AdjustmentsUnavailable
                ? null
                : bucket.ActualGross + bucket.ExecutedAdjustments,
            SortLines(bucket.Items));

    private static CashFlowUndatedTotalsDto ToUndatedDto(
        BucketAccumulator bucket,
        CurrencyAccumulator group) =>
        new(
            group.Currency is null || group.PlannedUnavailable ? null : bucket.PlannedResidual,
            group.Currency is null || group.CommittedUnavailable ? null : bucket.CommittedUnpaid,
            group.Currency is null || group.ActualUnavailable ? null : bucket.ActualGross,
            group.Currency is null || group.AdjustmentsUnavailable ? null : bucket.ExecutedAdjustments,
            group.Currency is null || group.ActualUnavailable || group.AdjustmentsUnavailable
                ? null
                : bucket.ActualGross + bucket.ExecutedAdjustments,
            SortLines(bucket.Items));

    private static IReadOnlyList<CashFlowLineDto> SortLines(IEnumerable<CashFlowLineDto> items) =>
        items.OrderBy(item => item.Measure)
            .ThenBy(item => item.EventDate)
            .ThenBy(item => item.SourceType, StringComparer.Ordinal)
            .ThenBy(item => item.SourceId)
            .ToArray();

    private static void AddIssue(
        IssueCollector issues,
        CurrencyAccumulator? group,
        string code,
        string? sourceType,
        Guid? sourceId,
        Guid? installmentId,
        Guid? requestId,
        Guid? obligationId,
        string? currency,
        ISet<string>? lineCodes = null)
    {
        issues.Add(code, sourceType, sourceId, installmentId, requestId, obligationId, currency);
        group?.QualityCodes.Add(code);
        lineCodes?.Add(code);
    }

    private static DateOnly GetPeriodStart(DateOnly date, CashFlowGranularity granularity) =>
        granularity == CashFlowGranularity.Month
            ? new DateOnly(date.Year, date.Month, 1)
            : date;

    private static string NormalizeCurrency(string? currency) =>
        currency?.Trim().ToUpperInvariant() ?? string.Empty;

    private static bool IsCurrencyCode(string currency) =>
        currency.Length == 3 && currency.All(character => character is >= 'A' and <= 'Z');

    private static bool IsStatus(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static CashFlowDataQualityStatus GetIssueSeverity(string code) =>
        code is CashFlowQualityCodes.ProjectCurrencyUnavailable
            or CashFlowQualityCodes.ForeignCurrencySource
            or CashFlowQualityCodes.UnknownPlannedDueDate
            or CashFlowQualityCodes.UnknownCommittedExpectedDate
            or CashFlowQualityCodes.UnknownActualSettlementDate
            or CashFlowQualityCodes.UnknownActualExpectedDate
            or CashFlowQualityCodes.UnknownAdjustmentDate
            or CashFlowQualityCodes.DuplicateSettlement
            ? CashFlowDataQualityStatus.Partial
            : CashFlowDataQualityStatus.Unavailable;

    private sealed class IssueCollector
    {
        private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
        private readonly List<CashFlowQualityIssueDto> _items = [];

        public IReadOnlyList<CashFlowQualityIssueDto> Items => _items
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Currency, StringComparer.Ordinal)
            .ThenBy(item => item.SourceType, StringComparer.Ordinal)
            .ThenBy(item => item.SourceId)
            .ToArray();

        public void Add(
            string code,
            string? sourceType,
            Guid? sourceId,
            Guid? installmentId,
            Guid? requestId,
            Guid? obligationId,
            string? currency)
        {
            var key = string.Join("|", code, sourceType, sourceId, installmentId, requestId, obligationId, currency);
            if (!_keys.Add(key)) return;
            _items.Add(new CashFlowQualityIssueDto(
                code,
                GetIssueSeverity(code),
                sourceType,
                sourceId,
                installmentId,
                requestId,
                obligationId,
                NormalizeCurrency(currency) is { Length: > 0 } normalized ? normalized : null));
        }
    }

    private sealed class CurrencyAccumulator(string? currency)
    {
        public string? Currency { get; } = currency;
        public HashSet<string> QualityCodes { get; } = new(StringComparer.Ordinal);
        public bool PlannedUnavailable { get; set; }
        public bool CommittedUnavailable { get; set; }
        public bool ActualUnavailable { get; set; }
        public bool AdjustmentsUnavailable { get; set; }
        public SortedDictionary<DateOnly, BucketAccumulator> Buckets { get; } = [];
        public BucketAccumulator Undated { get; } = new();

        public BucketAccumulator GetBucket(DateOnly periodStart)
        {
            if (!Buckets.TryGetValue(periodStart, out var bucket))
            {
                bucket = new BucketAccumulator();
                Buckets.Add(periodStart, bucket);
            }
            return bucket;
        }
    }

    private sealed class BucketAccumulator
    {
        public decimal PlannedResidual { get; set; }
        public decimal CommittedUnpaid { get; set; }
        public decimal ActualGross { get; set; }
        public decimal ExecutedAdjustments { get; set; }
        public List<CashFlowLineDto> Items { get; } = [];
    }

    private sealed record RequestBalance(
        CashFlowPaymentRequestFact Request,
        decimal? Remaining,
        DateOnly? ExpectedDate);
}
