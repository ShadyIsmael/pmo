import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ApiError } from "@api/client";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { useI18n } from "@i18n/messages";
import { useProjectAnnualBudgets } from "./api/project-annual-budgets";
import { useProjectAnnualBudgetClosings, useProjectFinancialFoundation, useProjectPaymentRequests } from "./api/project-financials";

const APPROVAL_COUNT_PAGE_SIZE = 100;

export function ProjectFinanceOverview({ projectId, selectedFiscalYear, active, canViewFoundation, canViewAnnualBudgets, canViewPaymentRequests, canViewAnnualBudgetClosing, canManage, onOpenAnnualBudgets }: {
  projectId: string;
  selectedFiscalYear: number | null;
  active: boolean;
  canViewFoundation: boolean;
  canViewAnnualBudgets: boolean;
  canViewPaymentRequests: boolean;
  canViewAnnualBudgetClosing: boolean;
  canManage: boolean;
  onOpenAnnualBudgets: () => void;
}) {
  const { t, formatCurrency, formatNumber } = useI18n();
  const budgets = useProjectAnnualBudgets(projectId, active && canViewAnnualBudgets);
  const foundation = useProjectFinancialFoundation(projectId, active && canViewFoundation);
  const availabilityRequest = useProjectPaymentRequests(projectId, active && canViewPaymentRequests);
  const pendingRequests = useProjectPaymentRequests(projectId, active && canViewPaymentRequests, "Pending", APPROVAL_COUNT_PAGE_SIZE);
  const closings = useProjectAnnualBudgetClosings(projectId, active && canViewAnnualBudgetClosing);

  // Disabled queries can retain cached results after permissions change; never keep rendering those values.
  const budgetPage = canViewAnnualBudgets ? budgets.data : undefined;
  const foundationData = canViewFoundation ? foundation.data : undefined;
  const paymentRequestData = canViewPaymentRequests ? availabilityRequest.data : undefined;
  const closingPage = canViewAnnualBudgetClosing ? closings.data : undefined;
  const pendingRequestPage = canViewPaymentRequests ? pendingRequests.data : undefined;
  const budget = budgetPage?.items.find(item => item.fiscalYear === selectedFiscalYear);
  const closedPeriod = closingPage?.items.find(item =>
    item.fiscalYear === selectedFiscalYear && item.status === "Closed");
  const availability = paymentRequestData?.budgetAvailability;
  const isCurrentFiscalYear = availability?.fiscalYear === selectedFiscalYear;
  const currency = budget?.currency;
  const approvedBudget = budget?.status === "Approved" ? budget.totalAnnualBudget : null;
  const actualSettlements = closedPeriod?.actualSettlementsInFiscalYear ?? null;
  const hasNoAuthorizedAnnualBudget = availability?.availabilityStatus === "AnnualBudgetMissing"
    || availability?.availabilityStatus === "AnnualBudgetDraft";
  const availableBudgetIsAuthoritative = hasNoAuthorizedAnnualBudget
    || availability?.availabilityStatus === "Ready"
    || availability?.availabilityStatus === "NoAvailableBalance";
  const availableBudget = isCurrentFiscalYear && availableBudgetIsAuthoritative
    ? availability?.availableAmount ?? null
    : null;
  const carryForward = budget?.status === "Approved" ? budget.approvedCarryForward : null;
  const pendingCount = pendingRequestPage?.total ?? null;
  const pendingApprovals = pendingRequestPage
    && pendingRequestPage.total <= pendingRequestPage.items.length
    ? pendingRequestPage.items.filter(item => item.canDecide).length
    : null;
  const accessDenied = [
    canViewAnnualBudgets ? budgets.error : null,
    canViewFoundation ? foundation.error : null,
    canViewPaymentRequests ? availabilityRequest.error : null,
    canViewPaymentRequests ? pendingRequests.error : null,
    canViewAnnualBudgetClosing ? closings.error : null,
  ]
    .some(error => error instanceof ApiError && error.status === 403);

  const metricValue = (value: number | null | undefined, moneyCurrency?: string | null) =>
    value === null || value === undefined || !moneyCurrency
      ? t("projects.financeKpiUnavailable")
      : formatCurrency(value, moneyCurrency);

  const performanceAvailable = Boolean(
    budget?.status === "Approved"
    && budget.isClosed
    && closedPeriod
    && currency
    && closedPeriod.currency === currency,
  );
  const chartData = performanceAvailable && budget && closedPeriod
    ? [{ label: t("projects.financeWorkspaceOverview"), budget: budget.totalAnnualBudget, actual: closedPeriod.actualSettlementsInFiscalYear }]
    : [];

  const metrics = [
    {
      key: "project-lifetime-ceiling",
      label: t("projects.financeKpiProjectLifetimeCeiling"),
      value: metricValue(foundationData?.approvedBudget, foundationData?.projectCurrency),
      hint: t("projects.budgetCeilingHint"),
    },
    {
      key: "approved-new-allocations",
      label: t("projects.financeKpiApprovedNewAllocations"),
      value: metricValue(budgetPage?.totalApprovedNewAllocations, budgetPage?.projectCurrency),
      hint: budgetPage?.pendingSubmittedAllocations ? t("projects.annualBudgetAllocationCeilingHint") : undefined,
    },
    {
      key: "remaining-unallocated-ceiling",
      label: t("projects.financeKpiRemainingUnallocatedCeiling"),
      value: metricValue(budgetPage?.remainingUnallocatedCeiling, budgetPage?.projectCurrency),
    },
    {
      key: "approved-annual-budget",
      label: t("projects.financeKpiApprovedAnnualBudget"),
      value: metricValue(approvedBudget, currency),
      hint: budget?.status === "Draft" ? t("projects.financeKpiBudgetDraft") : undefined,
    },
    {
      key: "actual-paid",
      label: t("projects.financeKpiActualPaid"),
      value: metricValue(foundationData?.actualPaid, foundationData?.projectCurrency),
    },
    {
      key: "outstanding-obligations",
      label: t("projects.financeKpiOutstandingObligations"),
      value: metricValue(foundationData?.outstandingPaymentBalance, foundationData?.projectCurrency),
      hint: t("projects.financeKpiProjectWideObligations"),
    },
    {
      key: "actual-settlements",
      label: t("projects.financeKpiActualSettlements"),
      value: metricValue(actualSettlements, closedPeriod?.currency),
      hint: closedPeriod ? t("projects.financeKpiActualSettlementsFinalized") : t("projects.financeKpiActualSettlementsUnavailable"),
    },
    {
      key: "available-budget",
      label: t("projects.financeKpiAvailableBudget"),
      value: metricValue(availableBudget, isCurrentFiscalYear ? availability?.currency : null),
      hint: !isCurrentFiscalYear ? t("projects.financeKpiAvailableCurrentYearOnly")
        : hasNoAuthorizedAnnualBudget ? t("projects.financeKpiNoAuthorizedAnnualBudget") : undefined,
    },
    {
      key: "pending-requests",
      label: t("projects.financeKpiPendingRequests"),
      value: pendingCount === null ? t("projects.financeKpiUnavailable") : formatNumber(pendingCount, { maximumFractionDigits: 0 }),
      hint: t("projects.financeKpiRequestsAcrossYears"),
    },
    {
      key: "pending-approvals",
      label: t("projects.financeKpiPendingApprovals"),
      value: pendingApprovals === null ? t("projects.financeKpiUnavailable") : formatNumber(pendingApprovals, { maximumFractionDigits: 0 }),
      hint: pendingApprovals === null && pendingRequestPage
        ? t("projects.financeKpiApprovalCountUnavailable")
        : t("projects.financeKpiApprovalsAssignedToYou"),
    },
    {
      key: "carry-forward",
      label: t("projects.financeKpiCarryForward"),
      value: metricValue(carryForward, currency),
      hint: budget?.status === "Approved" ? t("projects.annualBudgetOpeningCarryForwardHint") : undefined,
    },
  ];
  const visibleMetrics = metrics.filter(({ key }) => {
    switch (key) {
      case "project-lifetime-ceiling":
      case "actual-paid":
      case "outstanding-obligations":
        return canViewFoundation;
      case "approved-new-allocations":
      case "remaining-unallocated-ceiling":
      case "approved-annual-budget":
      case "carry-forward":
        return canViewAnnualBudgets;
      case "actual-settlements":
        return canViewAnnualBudgetClosing;
      case "available-budget":
      case "pending-requests":
      case "pending-approvals":
        return canViewPaymentRequests;
      default:
        return false;
    }
  });
  const canViewPerformance = canViewAnnualBudgets && canViewAnnualBudgetClosing;

  return <div className="space-y-5">
    {accessDenied ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{t("projects.financeWorkspaceAccessDenied")}</p> : null}
    {canViewAnnualBudgets && !budgets.isLoading && !budgets.isError && selectedFiscalYear !== null && !budget ? <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-dashed p-4">
      <div><p className="text-sm font-medium">{t("projects.annualBudgetFirstUse")}</p><p className="mt-1 text-xs text-muted-foreground">{t("projects.annualBudgetNoRecord")}</p></div>
      {canManage ? <Button size="sm" onClick={onOpenAnnualBudgets}>{t("projects.annualBudgetCreate")}</Button> : null}
    </div> : null}
    {budget?.status === "Approved" && budget.totalAnnualBudget === 0 ? <p role="status" className="rounded-md border px-3 py-2 text-sm text-muted-foreground">{t("projects.annualBudgetApprovedZero")}</p> : null}
    <section aria-label={t("projects.financeKpiSection")}>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {visibleMetrics.map(metric => <Card key={metric.key}>
          <CardContent className="p-4">
            <p className="text-xs font-medium text-muted-foreground">{metric.label}</p>
            <p className="mt-1 text-xl font-semibold tabular-nums">{metric.value}</p>
            {metric.hint ? <p className="mt-1 text-xs text-muted-foreground">{metric.hint}</p> : null}
            {metric.key === "outstanding-obligations" && canViewFoundation && foundation.isError ? <p role="alert" className="mt-1 text-xs text-destructive">{foundation.error instanceof ApiError && foundation.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.financialFoundationLoadFailed")}</p> : null}
            {metric.key === "approved-annual-budget" && canViewAnnualBudgets && budgets.isError ? <p role="alert" className="mt-1 text-xs text-destructive">{t("projects.annualBudgetHistoryLoadFailed")}</p> : null}
            {metric.key === "available-budget" && canViewPaymentRequests && availabilityRequest.isError ? <p role="alert" className="mt-1 text-xs text-destructive">{availabilityRequest.error instanceof ApiError && availabilityRequest.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.paymentRequestLoadFailed")}</p> : null}
            {metric.key === "pending-requests" && canViewPaymentRequests && pendingRequests.isError ? <p role="alert" className="mt-1 text-xs text-destructive">{t("projects.paymentRequestLoadFailed")}</p> : null}
            {metric.key === "actual-settlements" && canViewAnnualBudgetClosing && closings.isError ? <p role="alert" className="mt-1 text-xs text-destructive">{closings.error instanceof ApiError && closings.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.yearEndClosingLoadFailed")}</p> : null}
          </CardContent>
        </Card>)}
      </div>
      {(canViewAnnualBudgets && budgets.isLoading) || (canViewFoundation && foundation.isLoading) || (canViewPaymentRequests && (availabilityRequest.isLoading || pendingRequests.isLoading)) || (canViewAnnualBudgetClosing && closings.isLoading) ? <p role="status" className="mt-2 text-xs text-muted-foreground">{t("common.loading")}</p> : null}
    </section>

    <div className="grid gap-4 xl:grid-cols-[minmax(0,1.3fr)_minmax(18rem,1fr)]">
      {canViewPerformance ? <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">{t("projects.financePerformanceTitle")}</CardTitle>
          <p className="text-sm text-muted-foreground">{t("projects.financePerformanceDescription")}</p>
        </CardHeader>
        <CardContent>
          {chartData.length > 0 && currency ? <div className="h-56 min-w-0" role="img" aria-label={t("projects.financePerformanceTitle")}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={chartData} margin={{ top: 8, right: 8, left: 4, bottom: 8 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="label" />
                <YAxis width={76} />
                <Tooltip formatter={(value) => formatCurrency(Number(value), currency)} />
                <Bar dataKey="budget" name={t("projects.financeKpiApprovedAnnualBudget")} fill="hsl(var(--fin-blue))" radius={[4, 4, 0, 0]} />
                <Bar dataKey="actual" name={t("projects.financeKpiActualSettlements")} fill="hsl(var(--fin-amber))" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div> : <p role="status" className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">{t("projects.financePerformanceUnavailable")}</p>}
        </CardContent>
      </Card> : null}

      <Card>
        <CardHeader className="pb-3"><CardTitle className="text-base">{t("projects.financeTermsTitle")}</CardTitle></CardHeader>
        <CardContent>
          <dl className="divide-y">
            {(["Budget", "Committed", "Approved", "Paid", "Available"] as const).map(term => <div key={term} className="py-2.5 first:pt-0 last:pb-0">
              <dt className="text-sm font-medium">{t(`projects.financeTerm${term}Label`)}</dt>
              <dd className="mt-0.5 text-xs leading-5 text-muted-foreground">{t(`projects.financeTerm${term}Description`)}</dd>
            </div>)}
          </dl>
        </CardContent>
      </Card>
    </div>
  </div>;
}
