import * as React from "react";
import { AlertTriangle, ChevronLeft, ChevronRight, ExternalLink } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { useI18n } from "@i18n/messages";
import { CashFlowExportButtons } from "../finance/CashFlowExportButtons";
import type { CashFlowExportInput } from "../finance/cash-flow-export";
import type { ProjectCashFlow as ExportProjectCashFlow } from "../delivery/api/program-portfolio-cash-flow";
import {
  cashFlowDateKey,
  getCashFlowMonthGridDays,
  getCashFlowMonthRange,
  parseCashFlowDateKey,
  shiftCashFlowMonth,
} from "./cash-flow-calendar";
import {
  useProjectCashFlow,
  type CashFlowBucketTotals,
  type CashFlowCurrencyGroup,
  type CashFlowDateBucket,
  type CashFlowDataQualityIssue,
  type CashFlowLineItem,
} from "./api/project-cash-flow";

export type CashFlowFiscalYearOption = {
  value: string;
  label: string;
  from: string;
  to: string;
};

export type CashFlowCalendarProps = {
  projectId: string;
  projectName?: string;
  enabled?: boolean;
  canExport?: boolean;
  fiscalYearOptions?: readonly CashFlowFiscalYearOption[];
  onOpenItem?: (item: CashFlowLineItem) => void;
};

const UNKNOWN_CURRENCY = "__unresolved_currency__";

function normalizeCurrency(currency: string | null | undefined): string | null {
  const value = currency?.trim().toUpperCase();
  return value && /^[A-Z]{3}$/.test(value) ? value : null;
}

function getCurrencyKey(currency: string | null | undefined): string {
  return normalizeCurrency(currency) ?? UNKNOWN_CURRENCY;
}

function formatDate(value: string, locale: string, options: Intl.DateTimeFormatOptions): string {
  const dateKey = value.slice(0, 10);
  const parsed = parseCashFlowDateKey(dateKey);
  return parsed ? new Intl.DateTimeFormat(locale, options).format(parsed) : value;
}

function translatedCode(t: (key: string) => string, prefix: string, value: string): string {
  const key = `${prefix}.${value}`;
  const translated = t(key);
  return translated === key ? `${t("projects.cashFlow.unrecognizedValue")} (${value})` : translated;
}

function itemIsOverdue(item: CashFlowLineItem, today: string): boolean {
  if (item.measure !== "PlannedResidual" && item.measure !== "CommittedUnpaid") return false;
  const dueDate = item.expectedDate ?? item.eventDate;
  return Boolean(dueDate && dueDate.slice(0, 10) < today);
}

function amountForCurrency(
  amount: number | null | undefined,
  currency: string | null | undefined,
  formatCurrency: (value: number, currency?: string) => string,
  t: (key: string) => string,
): string {
  const normalized = normalizeCurrency(currency);
  if (typeof amount !== "number" || !Number.isFinite(amount) || !normalized) {
    return t("projects.cashFlow.amountUnavailable");
  }
  try {
    return formatCurrency(amount, normalized);
  } catch {
    return t("projects.cashFlow.amountUnavailable");
  }
}

function itemCurrency(item: CashFlowLineItem, group: CashFlowCurrencyGroup): string | null {
  return item.currency ?? group.currency;
}

function qualityIssueText(issue: CashFlowDataQualityIssue, t: (key: string) => string): string {
  return translatedCode(t, "projects.cashFlow.quality", issue.code);
}

function toExportReport(report: NonNullable<ReturnType<typeof useProjectCashFlow>["data"]>): ExportProjectCashFlow {
  return {
    projectId: report.projectId,
    periodFrom: report.periodFrom,
    periodTo: report.periodTo,
    granularity: report.granularity,
    dataQualityStatus: report.dataQualityStatus,
    dataQualityIssues: report.dataQualityIssues.map(issue => ({
      code: issue.code,
      severity: issue.severity === "Available" || issue.severity === "Unavailable" ? issue.severity : "Partial",
      sourceType: issue.sourceType ?? null,
      sourceId: issue.sourceId ?? null,
      installmentId: null,
      paymentRequestId: null,
      paymentObligationId: null,
      currency: issue.currency ?? null,
    })),
    currencyGroups: report.currencyGroups.map(group => {
      const normalizeLine = (item: CashFlowLineItem) => ({
        measure: item.measure,
        sourceType: item.sourceType,
        sourceId: item.sourceId,
        installmentId: item.installmentId ?? null,
        paymentRequestId: item.paymentRequestId ?? null,
        paymentObligationId: item.paymentObligationId ?? null,
        originalSettlementId: item.originalSettlementId ?? null,
        description: item.description ?? null,
        status: item.status ?? null,
        eventDate: item.eventDate ?? null,
        expectedDate: item.expectedDate ?? null,
        timingVarianceDays: item.timingVarianceDays ?? null,
        amount: item.amount ?? null,
        currency: item.currency ?? null,
        qualityCodes: item.qualityCodes,
      });
      const normalizeTotals = (totals: CashFlowBucketTotals) => ({
        plannedResidual: totals.plannedResidual,
        committedUnpaid: totals.committedUnpaid,
        actualGross: totals.actualGross,
        executedAdjustments: totals.executedAdjustments,
        actualNet: totals.actualNet,
        items: totals.items.map(normalizeLine),
      });
      return {
        currency: group.currency ?? null,
        dataQualityStatus: group.dataQualityStatus,
        buckets: group.buckets.map(bucket => ({ ...normalizeTotals(bucket), periodStart: bucket.periodStart })),
        undated: normalizeTotals(group.undated),
      };
    }),
  };
}

function CurrencyTotals({
  totals,
  currency,
  formatCurrency,
  t,
}: {
  totals: CashFlowBucketTotals;
  currency: string | null;
  formatCurrency: (value: number, currency?: string) => string;
  t: (key: string) => string;
}) {
  const categories = [
    ["plannedResidual", totals.plannedResidual],
    ["committedUnpaid", totals.committedUnpaid],
    ["actualGross", totals.actualGross],
    ["executedAdjustments", totals.executedAdjustments],
    ["actualNet", totals.actualNet],
  ] as const;

  return (
    <dl className="grid gap-2 sm:grid-cols-2 xl:grid-cols-5">
      {categories.map(([name, value]) => (
        <div key={name} className="rounded-md border p-3">
          <dt className="text-xs text-muted-foreground">{t(`projects.cashFlow.category.${name}`)}</dt>
          <dd className="mt-1 font-medium tabular-nums">
            {amountForCurrency(value, currency, formatCurrency, t)}
          </dd>
        </div>
      ))}
    </dl>
  );
}

function ItemDetails({
  item,
  group,
  onOpenItem,
  locale,
  formatCurrency,
  formatNumber,
  t,
}: {
  item: CashFlowLineItem;
  group: CashFlowCurrencyGroup;
  onOpenItem?: (item: CashFlowLineItem) => void;
  locale: string;
  formatCurrency: (value: number, currency?: string) => string;
  formatNumber: (value: number, options?: Intl.NumberFormatOptions) => string;
  t: (key: string, params?: Record<string, string | number>) => string;
}) {
  const isOverdue = itemIsOverdue(item, cashFlowDateKey(new Date()));
  const label = item.description?.trim() || translatedCode(t, "projects.cashFlow.category", item.measure);
  const status = item.status ? translatedCode(t, "projects.cashFlow.status", item.status) : null;
  const date = item.expectedDate ?? item.eventDate;

  return (
    <li className="rounded-md border p-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="font-medium">{label}</p>
          <p className="mt-1 text-xs text-muted-foreground">
            {translatedCode(t, "projects.cashFlow.category", item.measure)}
            {status ? ` · ${status}` : ""}
          </p>
        </div>
        {isOverdue ? (
          <span className="rounded-full bg-destructive/10 px-2 py-1 text-xs font-medium text-destructive">
            {t("projects.cashFlow.overdue")}
          </span>
        ) : null}
      </div>
      <dl className="mt-3 grid gap-x-4 gap-y-2 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-xs text-muted-foreground">{t("projects.cashFlow.amount")}</dt>
          <dd className="tabular-nums">{amountForCurrency(item.amount, itemCurrency(item, group), formatCurrency, t)}</dd>
        </div>
        {date ? (
          <div>
            <dt className="text-xs text-muted-foreground">{t("projects.cashFlow.expectedDate")}</dt>
            <dd>{formatDate(date, locale, { dateStyle: "medium" })}</dd>
          </div>
        ) : null}
        {item.installmentId ? (
          <div>
            <dt className="text-xs text-muted-foreground">{t("projects.cashFlow.installment")}</dt>
            <dd className="break-all">{item.installmentId}</dd>
          </div>
        ) : null}
        {item.paymentRequestId ? (
          <div>
            <dt className="text-xs text-muted-foreground">{t("projects.cashFlow.paymentRequest")}</dt>
            <dd className="break-all">{item.paymentRequestId}</dd>
          </div>
        ) : null}
        {item.paymentObligationId ? (
          <div>
            <dt className="text-xs text-muted-foreground">{t("projects.cashFlow.paymentObligation")}</dt>
            <dd className="break-all">{item.paymentObligationId}</dd>
          </div>
        ) : null}
        {item.timingVarianceDays != null ? (
          <div>
            <dt className="text-xs text-muted-foreground">{t("projects.cashFlow.timingVariance")}</dt>
            <dd>{t("projects.cashFlow.timingVarianceDays", { days: formatNumber(item.timingVarianceDays, { signDisplay: "always" }) })}</dd>
          </div>
        ) : null}
      </dl>
      {item.qualityCodes.length ? (
        <ul className="mt-3 space-y-1 text-xs text-warning" aria-label={t("projects.cashFlow.itemQuality") }>
          {item.qualityCodes.map(code => <li key={code}>{translatedCode(t, "projects.cashFlow.quality", code)}</li>)}
        </ul>
      ) : null}
      {onOpenItem && item.paymentRequestId ? (
        <Button type="button" size="sm" variant="outline" className="mt-3" onClick={() => onOpenItem(item)}>
          <ExternalLink className="me-2 size-4" aria-hidden="true" />
          {t("projects.cashFlow.openSourceRecord")}
        </Button>
      ) : null}
    </li>
  );
}

export function CashFlowCalendar({
  projectId,
  projectName,
  enabled = true,
  canExport = false,
  fiscalYearOptions = [],
  onOpenItem,
}: CashFlowCalendarProps) {
  const { t, locale, formatCurrency, formatNumber } = useI18n();
  const [monthAnchor, setMonthAnchor] = React.useState(() => new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  const [selectedFiscalYear, setSelectedFiscalYear] = React.useState("all");
  const [selectedCurrency, setSelectedCurrency] = React.useState("all");
  const [selectedDate, setSelectedDate] = React.useState<string | null>(null);
  const selectedYearOption = fiscalYearOptions.find(option => option.value === selectedFiscalYear);
  const monthRange = React.useMemo(() => getCashFlowMonthRange(monthAnchor), [monthAnchor]);
  const requestFrom = selectedYearOption && selectedYearOption.from > monthRange.from ? selectedYearOption.from : monthRange.from;
  const requestTo = selectedYearOption && selectedYearOption.to < monthRange.to ? selectedYearOption.to : monthRange.to;
  const validRange = requestFrom <= requestTo;
  const cashFlow = useProjectCashFlow(projectId, { from: requestFrom, to: requestTo, granularity: "Day" }, enabled && validRange);
  const data = cashFlow.data;
  const currencyGroups = data?.currencyGroups ?? [];
  const currencyKeys = React.useMemo(() => currencyGroups.map(group => getCurrencyKey(group.currency)), [currencyGroups]);
  const visibleGroups = React.useMemo(() => selectedCurrency === "all"
    ? currencyGroups
    : currencyGroups.filter(group => getCurrencyKey(group.currency) === selectedCurrency), [currencyGroups, selectedCurrency]);
  const firstDayOfWeek = locale.toLowerCase().startsWith("ar") ? 6 : 0;
  const gridDays = React.useMemo(() => getCashFlowMonthGridDays(monthAnchor, firstDayOfWeek), [monthAnchor, firstDayOfWeek]);
  const weekdays = React.useMemo(() => Array.from({ length: 7 }, (_, index) => {
    const date = new Date(2023, 0, 1 + ((firstDayOfWeek + index) % 7));
    return new Intl.DateTimeFormat(locale, { weekday: "short" }).format(date);
  }), [firstDayOfWeek, locale]);
  const dateBucketsByGroup = React.useMemo(() => visibleGroups.map(group => ({
    group,
    buckets: new Map(group.buckets.map(bucket => [bucket.periodStart.slice(0, 10), bucket])),
  })), [visibleGroups]);

  React.useEffect(() => {
    if (selectedCurrency !== "all" && !currencyKeys.includes(selectedCurrency)) setSelectedCurrency("all");
  }, [currencyKeys, selectedCurrency]);

  const currencyLabel = (currency: string | null) => normalizeCurrency(currency)
    ?? t("projects.cashFlow.currencyUnavailable");
  const formatAmount = (value: number | null, currency: string | null) =>
    amountForCurrency(value, currency, formatCurrency, t);
  const monthTitle = new Intl.DateTimeFormat(locale, { month: "long", year: "numeric" }).format(monthAnchor);
  const firstAllowed = selectedYearOption?.from;
  const lastAllowed = selectedYearOption?.to;
  const previousDisabled = Boolean(firstAllowed && monthRange.from <= firstAllowed);
  const nextDisabled = Boolean(lastAllowed && monthRange.to >= lastAllowed);
  const today = cashFlowDateKey(new Date());
  const visibleFiscalYearOptions = fiscalYearOptions.filter(option =>
    parseCashFlowDateKey(option.from) && parseCashFlowDateKey(option.to) && option.from <= option.to);
  const exportReport = React.useMemo(() => data ? toExportReport(data) : null, [data]);
  const makeExportInput = React.useCallback((): CashFlowExportInput | null => exportReport ? ({
    level: "Project",
    projectId,
    entityName: projectName?.trim() || projectId,
    report: exportReport,
    locale: locale.toLowerCase().startsWith("ar") ? "ar" : "en",
    generatedAt: new Date().toISOString(),
    translate: t,
  }) : null, [exportReport, locale, projectId, projectName, t]);

  const openDate = (date: Date) => {
    const key = cashFlowDateKey(date);
    if (selectedYearOption && (key < selectedYearOption.from || key > selectedYearOption.to)) return;
    setSelectedDate(key);
  };

  const selectedGroups = selectedDate ? dateBucketsByGroup.flatMap(({ group, buckets }) => {
    const bucket = buckets.get(selectedDate);
    return bucket ? [{ group, bucket }] : [];
  }) : [];
  const selectedItems = selectedGroups.flatMap(({ group, bucket }) => bucket.items.map(item => ({ group, item })));
  const showLoading = enabled && cashFlow.isLoading;

  return (
    <section dir={locale.toLowerCase().startsWith("ar") ? "rtl" : "ltr"} aria-labelledby="cash-flow-calendar-title">
      <Card>
        <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle id="cash-flow-calendar-title">{t("projects.cashFlow.title")}</CardTitle>
            <p className="mt-1 text-sm text-muted-foreground">{t("projects.cashFlow.calendarDescription")}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <CashFlowExportButtons ready={Boolean(data)} canExport={canExport} makeInput={makeExportInput} />
            <label className="sr-only" htmlFor="cash-flow-fiscal-year">{t("projects.cashFlow.fiscalYear")}</label>
            <select
              id="cash-flow-fiscal-year"
              className="h-9 rounded-md border bg-background px-2 text-sm"
              value={selectedFiscalYear}
              onChange={event => {
                const value = event.target.value;
                setSelectedFiscalYear(value);
                setSelectedDate(null);
                const option = fiscalYearOptions.find(item => item.value === value);
                const start = option ? parseCashFlowDateKey(option.from) : null;
                if (start && option && (monthRange.to < option.from || monthRange.from > option.to)) {
                  setMonthAnchor(new Date(start.getFullYear(), start.getMonth(), 1));
                }
              }}
              disabled={visibleFiscalYearOptions.length === 0}
            >
              <option value="all">{t("projects.cashFlow.allFiscalYears")}</option>
              {visibleFiscalYearOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
              {visibleFiscalYearOptions.length === 0 ? <option value="unavailable">{t("projects.cashFlow.fiscalYearUnavailable")}</option> : null}
            </select>
            <label className="sr-only" htmlFor="cash-flow-currency">{t("projects.cashFlow.currency")}</label>
            <select
              id="cash-flow-currency"
              className="h-9 rounded-md border bg-background px-2 text-sm"
              value={selectedCurrency}
              onChange={event => setSelectedCurrency(event.target.value)}
              disabled={currencyGroups.length === 0}
            >
              <option value="all">{t("projects.cashFlow.allCurrencies")}</option>
              {currencyGroups.map((group, index) => {
                const key = getCurrencyKey(group.currency);
                return <option key={`${key}-${index}`} value={key}>{currencyLabel(group.currency)}</option>;
              })}
            </select>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center justify-between gap-2">
            <Button type="button" variant="outline" size="sm" aria-label={t("projects.cashFlow.previousMonth")} disabled={previousDisabled} onClick={() => { setSelectedDate(null); setMonthAnchor(current => shiftCashFlowMonth(current, -1)); }}>
              <ChevronLeft className="size-4" aria-hidden="true" />
            </Button>
            <h2 className="text-base font-semibold capitalize" aria-live="polite">{monthTitle}</h2>
            <Button type="button" variant="outline" size="sm" aria-label={t("projects.cashFlow.nextMonth")} disabled={nextDisabled} onClick={() => { setSelectedDate(null); setMonthAnchor(current => shiftCashFlowMonth(current, 1)); }}>
              <ChevronRight className="size-4" aria-hidden="true" />
            </Button>
          </div>

          {!enabled ? (
            <p role="status" className="rounded-md border p-4 text-sm text-muted-foreground">{t("projects.financialsPermissionRequired")}</p>
          ) : null}
          {showLoading ? <p role="status" className="py-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : null}
          {enabled && cashFlow.isError ? (
            <div role="alert" className="rounded-md border border-destructive/30 p-4 text-sm text-destructive">
              <p>{t("projects.cashFlow.loadFailed")}</p>
              <Button type="button" variant="outline" size="sm" className="mt-3" onClick={() => void cashFlow.refetch()}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {enabled && !cashFlow.isLoading && !cashFlow.isError && data?.dataQualityStatus === "Unavailable" ? (
            <p role="status" className="rounded-md border p-4 text-sm text-muted-foreground">{t("projects.cashFlow.dataUnavailable")}</p>
          ) : null}
          {enabled && !cashFlow.isLoading && !cashFlow.isError && data && data.dataQualityStatus !== "Unavailable" ? (
            <>
              {data.dataQualityIssues.length ? (
                <div role="status" className="rounded-md border border-warning/30 bg-warning/5 p-3">
                  <p className="flex items-center gap-2 text-sm font-medium">
                    <AlertTriangle className="size-4 text-warning" aria-hidden="true" />
                    {t("projects.cashFlow.dataQualityNotice")}
                  </p>
                  <ul className="mt-2 list-inside list-disc space-y-1 text-sm text-muted-foreground">
                    {data.dataQualityIssues.map((issue, index) => <li key={`${issue.code}-${issue.sourceId ?? ""}-${index}`}>{qualityIssueText(issue, t)}</li>)}
                  </ul>
                </div>
              ) : null}
              {visibleGroups.length === 0 ? (
                <p role="status" className="rounded-md border p-4 text-sm text-muted-foreground">{t("projects.cashFlow.empty")}</p>
              ) : visibleGroups.map((group, groupIndex) => {
                const buckets = dateBucketsByGroup.find(entry => entry.group === group)?.buckets ?? new Map<string, CashFlowDateBucket>();
                const currency = normalizeCurrency(group.currency);
                return (
                  <div key={`${getCurrencyKey(group.currency)}-${groupIndex}`} className="space-y-3">
                    <h3 className="text-sm font-semibold">{currencyLabel(group.currency)}</h3>
                    <div className="grid grid-cols-7 gap-1" aria-label={t("projects.cashFlow.monthCalendar") }>
                      {weekdays.map((weekday, index) => <div key={`${weekday}-${index}`} className="p-2 text-center text-xs font-medium text-muted-foreground">{weekday}</div>)}
                      {gridDays.map(day => {
                        const dayKey = cashFlowDateKey(day);
                        const inMonth = day.getMonth() === monthAnchor.getMonth();
                        const inFiscalYear = !selectedYearOption || (dayKey >= selectedYearOption.from && dayKey <= selectedYearOption.to);
                        const bucket = buckets.get(dayKey);
                        const overdue = bucket?.items.some(item => itemIsOverdue(item, today)) ?? false;
                        const planned = bucket?.plannedResidual ?? null;
                        const committed = bucket?.committedUnpaid ?? null;
                        const actual = bucket?.actualNet ?? null;
                        const ariaLabel = [
                          formatDate(dayKey, locale, { dateStyle: "full" }),
                          `${t("projects.cashFlow.category.plannedResidual")}: ${formatAmount(planned, currency)}`,
                          `${t("projects.cashFlow.category.committedUnpaid")}: ${formatAmount(committed, currency)}`,
                          `${t("projects.cashFlow.category.actualNet")}: ${formatAmount(actual, currency)}`,
                          overdue ? t("projects.cashFlow.overdue") : "",
                        ].filter(Boolean).join(". ");
                        return (
                          <button
                            key={dayKey}
                            type="button"
                            className={`min-h-28 rounded-md border p-2 text-start align-top transition-colors hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${inMonth ? "bg-background" : "bg-muted/20 text-muted-foreground"} ${selectedDate === dayKey ? "border-primary ring-1 ring-primary" : ""}`}
                            aria-label={ariaLabel}
                            aria-pressed={selectedDate === dayKey}
                            disabled={!inMonth || !inFiscalYear}
                            onClick={() => openDate(day)}
                          >
                            <span className="flex items-center justify-between text-sm font-medium">
                              {day.getDate()}
                              {overdue ? <span className="rounded-full bg-destructive/10 px-1.5 py-0.5 text-[10px] text-destructive">{t("projects.cashFlow.overdue")}</span> : null}
                            </span>
                            {bucket ? (
                              <dl className="mt-2 space-y-1 text-[11px] leading-tight">
                                <div><dt className="inline text-muted-foreground">{t("projects.cashFlow.short.planned")}: </dt><dd className="inline tabular-nums">{formatAmount(planned, currency)}</dd></div>
                                <div><dt className="inline text-muted-foreground">{t("projects.cashFlow.short.committed")}: </dt><dd className="inline tabular-nums">{formatAmount(committed, currency)}</dd></div>
                                <div><dt className="inline text-muted-foreground">{t("projects.cashFlow.short.actual")}: </dt><dd className="inline tabular-nums">{formatAmount(actual, currency)}</dd></div>
                              </dl>
                            ) : inMonth ? <span className="mt-3 block text-[11px] text-muted-foreground">{t("projects.cashFlow.noFlows")}</span> : null}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                );
              })}
            </>
          ) : null}
          {enabled && !cashFlow.isLoading && !cashFlow.isError && !data ? (
            <p role="status" className="rounded-md border p-4 text-sm text-muted-foreground">{t("projects.cashFlow.dataUnavailable")}</p>
          ) : null}
        </CardContent>
      </Card>

      {selectedDate ? (
        <Card className="mt-4" aria-labelledby="cash-flow-day-details-title">
          <CardHeader className="flex flex-row items-start justify-between gap-3">
            <div>
              <CardTitle id="cash-flow-day-details-title">{formatDate(selectedDate, locale, { dateStyle: "full" })}</CardTitle>
              <p className="mt-1 text-sm text-muted-foreground">{t("projects.cashFlow.dayDetails")}</p>
            </div>
            <Button type="button" variant="outline" size="sm" onClick={() => setSelectedDate(null)}>{t("common.close")}</Button>
          </CardHeader>
          <CardContent className="space-y-5">
            {selectedGroups.length === 0 ? <p role="status" className="text-sm text-muted-foreground">{t("projects.cashFlow.noItemsForDay")}</p> : null}
            {selectedGroups.map(({ group, bucket }, index) => (
              <section key={`${getCurrencyKey(group.currency)}-${index}`} className="space-y-3">
                <h3 className="text-sm font-semibold">{currencyLabel(group.currency)}</h3>
                <CurrencyTotals totals={bucket} currency={normalizeCurrency(group.currency)} formatCurrency={formatCurrency} t={t} />
                {bucket.items.length ? (
                  <ul className="space-y-2">
                    {bucket.items.map((item, itemIndex) => <ItemDetails
                      key={`${item.sourceType}-${item.sourceId}-${item.measure}-${itemIndex}`}
                      item={item}
                      group={group}
                      onOpenItem={onOpenItem}
                      locale={locale}
                      formatCurrency={formatCurrency}
                      formatNumber={formatNumber}
                      t={t}
                    />)}
                  </ul>
                ) : <p className="text-sm text-muted-foreground">{t("projects.cashFlow.noItemsForDay")}</p>}
              </section>
            ))}
          </CardContent>
        </Card>
      ) : null}

      {data?.currencyGroups.some(group => group.undated.items.length > 0) ? (
        <Card className="mt-4" aria-labelledby="cash-flow-undated-title">
          <CardHeader><CardTitle id="cash-flow-undated-title">{t("projects.cashFlow.undatedTitle")}</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">{t("projects.cashFlow.undatedDescription")}</p>
            {data.currencyGroups.filter(group => group.undated.items.length > 0).map((group, index) => (
              <section key={`${getCurrencyKey(group.currency)}-${index}`} className="space-y-3">
                <h3 className="text-sm font-semibold">{currencyLabel(group.currency)}</h3>
                <CurrencyTotals totals={group.undated} currency={normalizeCurrency(group.currency)} formatCurrency={formatCurrency} t={t} />
                <ul className="space-y-2">
                  {group.undated.items.map((item, itemIndex) => <ItemDetails
                    key={`${item.sourceType}-${item.sourceId}-${item.measure}-${itemIndex}`}
                    item={item}
                    group={group}
                    onOpenItem={onOpenItem}
                    locale={locale}
                    formatCurrency={formatCurrency}
                    formatNumber={formatNumber}
                    t={t}
                  />)}
                </ul>
              </section>
            ))}
          </CardContent>
        </Card>
      ) : null}
    </section>
  );
}
