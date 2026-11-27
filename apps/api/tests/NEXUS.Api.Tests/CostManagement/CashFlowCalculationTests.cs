using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using Xunit;

namespace NEXUS.Api.Tests.CostManagement;

public sealed class CashFlowCalculationTests
{
    private static readonly Guid ProjectId = Guid.Parse("d7fdd2b3-28f5-4c68-88d8-fad1aab1dc7c");
    private static readonly DateOnly PeriodFrom = new(2026, 1, 1);
    private static readonly DateOnly PeriodTo = new(2026, 3, 31);

    [Fact]
    public void Linked_installment_request_and_partial_settlement_reconcile_without_double_counting()
    {
        var installmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();

        var result = Calculate(
            installments:
            [
                Installment(installmentId, 1_000m, new DateOnly(2026, 1, 10))
            ],
            requests:
            [
                Request(
                    requestId,
                    installmentId,
                    700m,
                    PaymentRequestStatuses.Approved,
                    expectedDate: new DateOnly(2026, 1, 10),
                    approvedUnpaidReleaseAmount: 100m)
            ],
            settlements:
            [
                Settlement(
                    settlementId,
                    200m,
                    settlementDate: new DateOnly(2026, 1, 20),
                    paymentRequestId: requestId,
                    installmentId: installmentId,
                    expectedDate: new DateOnly(2026, 1, 10))
            ]);

        var plannedAndCommitted = Bucket(result, new DateOnly(2026, 1, 10));
        var actual = Bucket(result, new DateOnly(2026, 1, 20));

        Assert.Equal(400m, plannedAndCommitted.PlannedResidual);
        Assert.Equal(400m, plannedAndCommitted.CommittedUnpaid);
        Assert.Equal(200m, actual.ActualGross);
        Assert.Equal(200m, actual.ActualNet);

        // The released unpaid amount returns to the planned residual. Across these
        // separate lifecycle views, the scheduled amount is represented exactly once.
        Assert.Equal(1_000m,
            (plannedAndCommitted.PlannedResidual ?? 0m)
            + (plannedAndCommitted.CommittedUnpaid ?? 0m)
            + (actual.ActualGross ?? 0m));

        var lines = AllLines(result).ToArray();
        Assert.Equal(3, lines.Length);
        Assert.Single(lines.Where(line => line.Measure == CashFlowMeasure.PlannedResidual));
        Assert.Single(lines.Where(line => line.Measure == CashFlowMeasure.CommittedUnpaid));
        var actualLine = Assert.Single(lines.Where(line => line.Measure == CashFlowMeasure.ActualGross));
        Assert.Equal(settlementId, actualLine.SourceId);
        Assert.Equal(10, actualLine.TimingVarianceDays);
        Assert.Equal(CashFlowDataQualityStatus.Available, result.DataQualityStatus);
    }

    [Fact]
    public void Pending_requests_and_approved_non_payment_request_facts_do_not_become_commitments()
    {
        var installmentId = Guid.NewGuid();
        var paymentRequestId = Guid.NewGuid();
        var reversalRequestId = Guid.NewGuid();

        var result = Calculate(
            installments:
            [
                Installment(installmentId, 1_000m, new DateOnly(2026, 2, 5))
            ],
            requests:
            [
                Request(
                    paymentRequestId,
                    installmentId,
                    500m,
                    PaymentRequestStatuses.Pending,
                    expectedDate: new DateOnly(2026, 2, 5)),
                Request(
                    reversalRequestId,
                    installmentId,
                    300m,
                    PaymentRequestStatuses.Approved,
                    expectedDate: new DateOnly(2026, 2, 5),
                    requestKind: ProjectPaymentRequestKinds.ReversalRequest),
                Request(
                    Guid.NewGuid(),
                    installmentId,
                    250m,
                    PaymentRequestStatuses.Approved,
                    expectedDate: new DateOnly(2026, 2, 5))
            ]);

        var bucket = Bucket(result, new DateOnly(2026, 2, 5));

        Assert.Equal(750m, bucket.PlannedResidual);
        Assert.Equal(250m, bucket.CommittedUnpaid);
        Assert.DoesNotContain(AllLines(result), line => line.SourceId == paymentRequestId);
        Assert.DoesNotContain(AllLines(result), line => line.SourceId == reversalRequestId);
        Assert.Single(AllLines(result).Where(line => line.Measure == CashFlowMeasure.CommittedUnpaid));
    }

    [Fact]
    public void Only_executed_refunds_and_corrections_change_net_actual_using_execution_then_effective_date()
    {
        var settlementId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();

        var result = Calculate(
            installments:
            [
                Installment(500m, new DateOnly(2026, 1, 10), installmentId)
            ],
            requests:
            [
                Request(requestId, installmentId, 300m, PaymentRequestStatuses.Approved,
                    expectedDate: new DateOnly(2026, 1, 10))
            ],
            settlements:
            [
                Settlement(settlementId, 300m, new DateOnly(2026, 1, 15), requestId, installmentId,
                    expectedDate: new DateOnly(2026, 1, 10))
            ],
            adjustments:
            [
                Adjustment(Guid.NewGuid(), settlementId, 50m, PaymentSettlementAdjustment.Refund,
                    PaymentSettlementAdjustment.Executed,
                    executionDate: new DateOnly(2026, 1, 20), effectiveDate: new DateOnly(2026, 1, 18),
                    paymentRequestId: requestId, installmentId: installmentId),
                Adjustment(Guid.NewGuid(), settlementId, 25m, PaymentSettlementAdjustment.Correction,
                    PaymentSettlementAdjustment.Executed,
                    executionDate: null, effectiveDate: new DateOnly(2026, 1, 21),
                    paymentRequestId: requestId, installmentId: installmentId),
                Adjustment(Guid.NewGuid(), settlementId, 900m, PaymentSettlementAdjustment.Refund,
                    PaymentSettlementAdjustment.Approved,
                    executionDate: new DateOnly(2026, 1, 22), effectiveDate: new DateOnly(2026, 1, 19),
                    paymentRequestId: requestId, installmentId: installmentId)
            ]);

        var actual = Bucket(result, new DateOnly(2026, 1, 15));
        var refund = Bucket(result, new DateOnly(2026, 1, 20));
        var correction = Bucket(result, new DateOnly(2026, 1, 21));

        Assert.Equal(300m, actual.ActualGross);
        Assert.Equal(300m, actual.ActualNet);
        Assert.Equal(-50m, refund.ExecutedAdjustments);
        Assert.Equal(-50m, refund.ActualNet);
        Assert.Equal(25m, correction.ExecutedAdjustments);
        Assert.Equal(25m, correction.ActualNet);
        Assert.Equal(300m, Sum(result, line => line.Measure == CashFlowMeasure.ActualGross));
        Assert.Equal(-25m, Sum(result, line => line.Measure == CashFlowMeasure.ExecutedAdjustment));
        Assert.Equal(275m, result.CurrencyGroups.Single(group => group.Currency == "USD")
            .Buckets.Sum(bucket => bucket.ActualNet ?? 0m));
        Assert.DoesNotContain(AllLines(result), line => line.Amount == -900m || line.Amount == 900m);
        Assert.Equal(5, Assert.Single(AllLines(result).Where(line => line.SourceId == settlementId)).TimingVarianceDays);
    }

    [Fact]
    public void Adjustment_without_a_resolvable_source_settlement_keeps_flagged_detail_and_nulls_adjustment_totals()
    {
        var installmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();
        var orphanAdjustmentId = Guid.NewGuid();
        var unresolvedAdjustmentId = Guid.NewGuid();

        var result = Calculate(
            installments:
            [
                Installment(100m, new DateOnly(2026, 2, 1), installmentId)
            ],
            requests:
            [
                Request(requestId, installmentId, 100m, PaymentRequestStatuses.Approved,
                    expectedDate: new DateOnly(2026, 2, 1))
            ],
            settlements:
            [
                Settlement(settlementId, 100m, new DateOnly(2026, 2, 2), requestId, installmentId,
                    expectedDate: new DateOnly(2026, 2, 1))
            ],
            adjustments:
            [
                Adjustment(orphanAdjustmentId, originalSettlementId: null, 20m,
                    PaymentSettlementAdjustment.Refund, PaymentSettlementAdjustment.Executed,
                    executionDate: new DateOnly(2026, 2, 3), effectiveDate: new DateOnly(2026, 2, 2),
                    paymentRequestId: requestId, installmentId: installmentId),
                Adjustment(unresolvedAdjustmentId, originalSettlementId: Guid.NewGuid(), 15m,
                    PaymentSettlementAdjustment.Correction, PaymentSettlementAdjustment.Executed,
                    executionDate: new DateOnly(2026, 2, 4), effectiveDate: new DateOnly(2026, 2, 3),
                    paymentRequestId: requestId, installmentId: installmentId)
            ]);

        var orphanLine = Assert.Single(AllLines(result).Where(line => line.SourceId == orphanAdjustmentId));
        var unresolvedLine = Assert.Single(AllLines(result).Where(line => line.SourceId == unresolvedAdjustmentId));
        Assert.Equal(-20m, orphanLine.Amount);
        Assert.Equal(15m, unresolvedLine.Amount);
        Assert.Contains(CashFlowQualityCodes.MissingAdjustmentSettlementReference, orphanLine.QualityCodes);
        Assert.Contains(CashFlowQualityCodes.MissingAdjustmentSettlementReference, unresolvedLine.QualityCodes);
        Assert.Equal(100m, Bucket(result, new DateOnly(2026, 2, 2)).ActualGross);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 2)).ActualNet);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 3)).ExecutedAdjustments);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 3)).ActualNet);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 4)).ExecutedAdjustments);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 4)).ActualNet);
    }

    [Fact]
    public void Adjustment_currency_mismatch_flags_detail_and_nulls_both_currency_groups_net_totals()
    {
        var installmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();
        var adjustmentId = Guid.NewGuid();

        var result = Calculate(
            installments:
            [
                Installment(100m, new DateOnly(2026, 2, 1), installmentId)
            ],
            requests:
            [
                Request(requestId, installmentId, 100m, PaymentRequestStatuses.Approved,
                    expectedDate: new DateOnly(2026, 2, 1))
            ],
            settlements:
            [
                Settlement(settlementId, 100m, new DateOnly(2026, 2, 2), requestId, installmentId,
                    expectedDate: new DateOnly(2026, 2, 1))
            ],
            adjustments:
            [
                Adjustment(adjustmentId, settlementId, 10m, PaymentSettlementAdjustment.Refund,
                    PaymentSettlementAdjustment.Executed,
                    executionDate: new DateOnly(2026, 2, 3), effectiveDate: new DateOnly(2026, 2, 2),
                    paymentRequestId: requestId, installmentId: installmentId, currency: "EUR")
            ]);

        var detail = Assert.Single(AllLines(result).Where(line => line.SourceId == adjustmentId));
        Assert.Equal(-10m, detail.Amount);
        Assert.Contains(CashFlowQualityCodes.CurrencyMismatch, detail.QualityCodes);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 2)).ActualNet);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 3), "EUR").ExecutedAdjustments);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 3), "EUR").ActualNet);
    }

    [Fact]
    public void Adjustment_owner_link_mismatch_flags_detail_and_nulls_affected_net_totals()
    {
        var installmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();
        var adjustmentId = Guid.NewGuid();

        var result = Calculate(
            installments:
            [
                Installment(100m, new DateOnly(2026, 2, 1), installmentId)
            ],
            requests:
            [
                Request(requestId, installmentId, 100m, PaymentRequestStatuses.Approved,
                    expectedDate: new DateOnly(2026, 2, 1))
            ],
            settlements:
            [
                Settlement(settlementId, 100m, new DateOnly(2026, 2, 2), requestId, installmentId,
                    expectedDate: new DateOnly(2026, 2, 1))
            ],
            adjustments:
            [
                Adjustment(adjustmentId, settlementId, 10m, PaymentSettlementAdjustment.Correction,
                    PaymentSettlementAdjustment.Executed,
                    executionDate: new DateOnly(2026, 2, 3), effectiveDate: new DateOnly(2026, 2, 2),
                    paymentRequestId: Guid.NewGuid(), installmentId: installmentId)
            ]);

        var detail = Assert.Single(AllLines(result).Where(line => line.SourceId == adjustmentId));
        Assert.Equal(10m, detail.Amount);
        Assert.Contains(CashFlowQualityCodes.ConflictingSourceRows, detail.QualityCodes);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 2)).ActualNet);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 3)).ExecutedAdjustments);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 3)).ActualNet);
    }

    [Fact]
    public void Adjustment_installment_link_mismatch_flags_detail_and_nulls_affected_net_totals()
    {
        var installmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();
        var adjustmentId = Guid.NewGuid();

        var result = Calculate(
            installments:
            [
                Installment(100m, new DateOnly(2026, 2, 1), installmentId)
            ],
            requests:
            [
                Request(requestId, installmentId, 100m, PaymentRequestStatuses.Approved,
                    expectedDate: new DateOnly(2026, 2, 1))
            ],
            settlements:
            [
                Settlement(settlementId, 100m, new DateOnly(2026, 2, 2), requestId, installmentId,
                    expectedDate: new DateOnly(2026, 2, 1))
            ],
            adjustments:
            [
                Adjustment(adjustmentId, settlementId, 10m, PaymentSettlementAdjustment.Refund,
                    PaymentSettlementAdjustment.Executed,
                    executionDate: new DateOnly(2026, 2, 3), effectiveDate: new DateOnly(2026, 2, 2),
                    paymentRequestId: requestId, installmentId: Guid.NewGuid())
            ]);

        var detail = Assert.Single(AllLines(result).Where(line => line.SourceId == adjustmentId));
        Assert.Equal(-10m, detail.Amount);
        Assert.Contains(CashFlowQualityCodes.SettlementInstallmentConflict, detail.QualityCodes);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 2)).ActualNet);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 3)).ExecutedAdjustments);
        Assert.Null(Bucket(result, new DateOnly(2026, 2, 3)).ActualNet);
    }

    [Fact]
    public void Request_derived_installment_link_with_null_raw_and_copied_links_is_valid()
    {
        var installmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();
        var adjustmentId = Guid.NewGuid();

        var result = Calculate(
            installments:
            [
                Installment(100m, new DateOnly(2026, 2, 1), installmentId)
            ],
            requests:
            [
                Request(requestId, installmentId, 100m, PaymentRequestStatuses.Approved,
                    expectedDate: new DateOnly(2026, 2, 1))
            ],
            settlements:
            [
                Settlement(settlementId, 100m, new DateOnly(2026, 2, 2), requestId, installmentId: null,
                    expectedDate: new DateOnly(2026, 2, 1))
            ],
            adjustments:
            [
                Adjustment(adjustmentId, settlementId, 10m, PaymentSettlementAdjustment.Refund,
                    PaymentSettlementAdjustment.Executed,
                    executionDate: new DateOnly(2026, 2, 3), effectiveDate: new DateOnly(2026, 2, 2),
                    paymentRequestId: requestId, installmentId: null)
            ]);

        var settlementLine = Assert.Single(AllLines(result).Where(line => line.SourceId == settlementId));
        var adjustmentLine = Assert.Single(AllLines(result).Where(line => line.SourceId == adjustmentId));
        Assert.Equal(installmentId, settlementLine.InstallmentId);
        Assert.Null(adjustmentLine.InstallmentId);
        Assert.Empty(adjustmentLine.QualityCodes);
        Assert.Equal(-10m, adjustmentLine.Amount);
        Assert.Equal(CashFlowDataQualityStatus.Available, result.DataQualityStatus);
        Assert.Equal(100m, Bucket(result, new DateOnly(2026, 2, 2)).ActualGross);
        Assert.Equal(100m, Bucket(result, new DateOnly(2026, 2, 2)).ActualNet);
        Assert.Equal(-10m, Bucket(result, new DateOnly(2026, 2, 3)).ExecutedAdjustments);
        Assert.Equal(-10m, Bucket(result, new DateOnly(2026, 2, 3)).ActualNet);
    }

    [Fact]
    public void Daily_and_monthly_granularity_bucket_the_same_authoritative_schedule()
    {
        var installments = new[]
        {
            Installment(100m, new DateOnly(2026, 2, 3)),
            Installment(200m, new DateOnly(2026, 2, 21))
        };

        var daily = Calculate(installments: installments);
        var monthly = Calculate(installments: installments, granularity: CashFlowGranularity.Month);

        Assert.Equal(100m, Bucket(daily, new DateOnly(2026, 2, 3)).PlannedResidual);
        Assert.Equal(200m, Bucket(daily, new DateOnly(2026, 2, 21)).PlannedResidual);
        Assert.Equal(300m, Bucket(monthly, new DateOnly(2026, 2, 1)).PlannedResidual);
        Assert.Equal(CashFlowGranularity.Day, daily.Granularity);
        Assert.Equal(CashFlowGranularity.Month, monthly.Granularity);
    }

    [Fact]
    public void Unknown_dates_are_preserved_in_undated_totals_and_flagged_as_partial()
    {
        var result = Calculate(installments: [Installment(75m, dueDate: null)]);
        var group = Group(result);

        Assert.DoesNotContain(group.Buckets, bucket => bucket.Items.Count > 0);
        Assert.Equal(75m, group.Undated.PlannedResidual);
        Assert.Single(group.Undated.Items);
        Assert.Equal(CashFlowQualityCodes.UnknownPlannedDueDate,
            Assert.Single(result.DataQualityIssues).Code);
        Assert.Equal(CashFlowDataQualityStatus.Partial, result.DataQualityStatus);
    }

    [Fact]
    public void Different_currencies_remain_in_separate_groups_without_conversion_or_mixed_totals()
    {
        var result = Calculate(
            installments:
            [
                Installment(100m, new DateOnly(2026, 3, 1), currency: "USD"),
                Installment(200m, new DateOnly(2026, 3, 1), currency: "EUR")
            ]);

        Assert.Equal(2, result.CurrencyGroups.Count);
        Assert.Equal(100m, Bucket(result, new DateOnly(2026, 3, 1), "USD").PlannedResidual);
        Assert.Equal(200m, Bucket(result, new DateOnly(2026, 3, 1), "EUR").PlannedResidual);
        Assert.DoesNotContain(result.CurrencyGroups, group => group.Currency == "USD" &&
            group.Buckets.Any(bucket => bucket.PlannedResidual == 300m));
        Assert.Contains(result.DataQualityIssues, issue => issue.Code == CashFlowQualityCodes.ForeignCurrencySource);
    }

    [Fact]
    public void Missing_links_are_reported_without_guessing_a_relationship()
    {
        var installmentId = Guid.NewGuid();
        var orphanRequestId = Guid.NewGuid();
        var orphanSettlementId = Guid.NewGuid();
        var result = Calculate(
            installments:
            [
                Installment(installmentId, 1_000m, new DateOnly(2026, 3, 5))
            ],
            requests:
            [
                Request(orphanRequestId, installmentId: null, 125m, PaymentRequestStatuses.Approved,
                    expectedDate: new DateOnly(2026, 3, 5))
            ],
            settlements:
            [
                Settlement(orphanSettlementId, 40m, new DateOnly(2026, 3, 6), paymentRequestId: null,
                    installmentId: null, paymentObligationId: null)
            ]);

        Assert.NotEqual(CashFlowDataQualityStatus.Available, result.DataQualityStatus);
        Assert.Contains(result.DataQualityIssues, issue => issue.Code == CashFlowQualityCodes.MissingRequestInstallmentLink);
        Assert.Contains(result.DataQualityIssues, issue => issue.Code == CashFlowQualityCodes.MissingSettlementReference);
        Assert.Equal(40m, Bucket(result, new DateOnly(2026, 3, 6)).ActualGross);
        Assert.Contains(AllLines(result), line => line.SourceId == orphanRequestId
            && line.Measure == CashFlowMeasure.CommittedUnpaid
            && line.InstallmentId is null);
        Assert.DoesNotContain(AllLines(result), line => line.SourceId == orphanRequestId
            && line.InstallmentId == installmentId);
    }

    [Fact]
    public void Empty_input_does_not_invent_cash_flow_amounts_or_items()
    {
        var result = Calculate();

        Assert.Empty(AllLines(result));
        Assert.All(result.CurrencyGroups, group =>
        {
            Assert.DoesNotContain(group.Buckets, bucket => bucket.Items.Count > 0);
            Assert.Empty(group.Undated.Items);
            Assert.Equal(0m, group.Undated.PlannedResidual);
            Assert.Equal(0m, group.Undated.CommittedUnpaid);
            Assert.Equal(0m, group.Undated.ActualGross);
            Assert.Equal(0m, group.Undated.ExecutedAdjustments);
        });
    }

    private static ProjectCashFlowDto Calculate(
        IReadOnlyList<CashFlowInstallmentFact>? installments = null,
        IReadOnlyList<CashFlowPaymentRequestFact>? requests = null,
        IReadOnlyList<CashFlowSettlementFact>? settlements = null,
        IReadOnlyList<CashFlowAdjustmentFact>? adjustments = null,
        CashFlowGranularity granularity = CashFlowGranularity.Day) =>
        ProjectCashFlowCalculator.Calculate(new ProjectCashFlowCalculationInput(
            ProjectId,
            "USD",
            PeriodFrom,
            PeriodTo,
            granularity,
            installments ?? [],
            requests ?? [],
            settlements ?? [],
            adjustments ?? []));

    private static CashFlowInstallmentFact Installment(
        decimal amount,
        DateOnly? dueDate,
        Guid? id = null,
        string currency = "USD") =>
        Installment(id ?? Guid.NewGuid(), amount, dueDate, currency);

    private static CashFlowInstallmentFact Installment(
        Guid id,
        decimal amount,
        DateOnly? dueDate,
        string currency = "USD") =>
        new(id, Guid.NewGuid(), "Scheduled installment", amount, currency, dueDate, "Registered", true);

    private static CashFlowPaymentRequestFact Request(
        Guid id,
        Guid? installmentId,
        decimal? amount,
        string status,
        DateOnly? expectedDate,
        decimal? approvedUnpaidReleaseAmount = 0m,
        string? requestKind = null) =>
        new(id, requestKind ?? ProjectPaymentRequestKinds.PaymentRequest, installmentId, amount,
            "USD", status, expectedDate, approvedUnpaidReleaseAmount, "Payment request");

    private static CashFlowSettlementFact Settlement(
        Guid id,
        decimal? amount,
        DateOnly? settlementDate,
        Guid? paymentRequestId,
        Guid? installmentId,
        Guid? paymentObligationId = null,
        DateOnly? expectedDate = null) =>
        new(id, paymentRequestId, installmentId, paymentObligationId, amount, "USD", settlementDate,
            expectedDate, "Confirmed settlement", installmentId);

    private static CashFlowAdjustmentFact Adjustment(
        Guid id,
        Guid? originalSettlementId,
        decimal? amount,
        string type,
        string status,
        DateOnly? executionDate,
        DateOnly? effectiveDate,
        Guid? paymentRequestId,
        Guid? installmentId,
        string currency = "USD",
        Guid? paymentObligationId = null) =>
        new(id, originalSettlementId, paymentRequestId, installmentId, paymentObligationId, amount, currency, type, status,
            executionDate, effectiveDate, "Settlement adjustment");

    private static ProjectCashFlowCurrencyGroupDto Group(ProjectCashFlowDto result, string currency = "USD") =>
        result.CurrencyGroups.Single(group => group.Currency == currency);

    private static CashFlowBucketDto Bucket(ProjectCashFlowDto result, DateOnly periodStart, string currency = "USD") =>
        Group(result, currency).Buckets.Single(bucket => bucket.PeriodStart == periodStart);

    private static IEnumerable<CashFlowLineDto> AllLines(ProjectCashFlowDto result) =>
        result.CurrencyGroups.SelectMany(group =>
            group.Buckets.SelectMany(bucket => bucket.Items).Concat(group.Undated.Items));

    private static decimal Sum(ProjectCashFlowDto result, Func<CashFlowLineDto, bool> predicate) =>
        AllLines(result).Where(predicate).Sum(line => line.Amount ?? 0m);
}
