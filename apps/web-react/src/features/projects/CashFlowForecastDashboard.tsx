import * as React from "react";
import { AlertTriangle, ExternalLink } from "lucide-react";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { cashFlowDateKey, parseCashFlowDateKey } from "./cash-flow-calendar";
import type { CashFlowFiscalYearOption } from "./CashFlowCalendar";
import {
  useProjectCashFlow,
  type CashFlowCurrencyGroup,
  type CashFlowDataQualityIssue,
  type CashFlowLineItem,
} from "./api/project-cash-flow";

export type CashFlowForecastDashboardProps = {
  projectId: string;
  enabled?: boolean;
  fiscalYearOptions?: readonly CashFlowFiscalYearOption[];
  onOpenItem?: (item: CashFlowLineItem) => void;
};

const UNKNOWN_CURRENCY = "__unresolved_currency__";

function normalizeCurrency(currency: string | null | undefined): string | null {
  const value = currency?.trim().toUpperCase();
  return value && /^[A-Z]{3}$/.test(value) ? value : null;
}

function currencyKey(currency: string | null | undefined): string {
  return normalizeCurrency(currency) ?? UNKNOWN_CURRENCY;
}

function formatAmount(
  amount: number | null | undefined,
  currency: string | null | undefined,
  formatCurrency: (value: number, currency?: string) => string,
  t: (key: string) => string,
): string {
  const normalized = normalizeCurrency(currency);
  if (typeof amount !== "number" || !Number.isFinite(amount) || !normalized) return t("projects.cashFlow.amountUnavailable");
  try {
    return formatCurrency(amount, normalized);
  } catch {
    return t("projects.cashFlow.amountUnavailable");
  }
}

function formatDate(value: string | null | undefined, locale: string, options: Intl.DateTimeFormatOptions): string {
  if (!value) return "";
  const date = parseCashFlowDateKey(value.slice(0, 10));
  return date ? new Intl.DateTimeFormat(locale, options).format(date) : value;
}

function translateCode(t: (key: string) => string, prefix: string, code: string): string {
  const key = `${prefix}.${code}`;
  const value = t(key);
  return value === key ? `${t("projects.cashFlowForecast.unrecognizedValue")} (${code})` : value;
}

function knownSum(values: readonly (number | null | undefined)[]): number | null {
  if (values.length === 0 || values.some(value => typeof value !== "number" || !Number.isFinite(value))) return null;
  return values.reduce<number>((sum, value) => sum + (value as number), 0);
}

function dueDateFor(item: CashFlowLineItem): string | null {
  return item.expectedDate?.slice(0, 10) ?? item.eventDate?.slice(0, 10) ?? null;
}

function isOverdue(item: CashFlowLineItem, today: string): boolean {
  if (item.measure !== "PlannedResidual" && item.measure !== "CommittedUnpaid") return false;
  const dueDate = dueDateFor(item);
  return Boolean(dueDate && dueDate < today);
}

function issueLabel(issue: CashFlowDataQualityIssue, t: (key: string) => string): string {
  return translateCode(t, "projects.cashFlow.quality", issue.code);
}

function CurrencyForecast({
  group,
  locale,
  today,
  formatCurrency,
  formatNumber,
  t,
  onOpenItem,
}: {
  group: CashFlowCurrencyGroup;
  locale: string;
  today: string;
  formatCurrency: (value: number, currency?: string) => string;
  formatNumber: (value: number, options?: Intl.NumberFormatOptions) => string;
  t: (key: string, params?: Record<string, string | number>) => string;
  onOpenItem?: (item: CashFlowLineItem) => void;
}) {
  const currency = normalizeCurrency(group.currency);
  const currencyLabel = currency ?? t("projects.cashFlow.currencyUnavailable");
  const nextMonth = cashFlowDateKey(new Date(new Date().getFullYear(), new Date().getMonth() + 1, 1));
  const futureBuckets = group.buckets.filter(bucket => bucket.periodStart.slice(0, 10) >= nextMonth);
  const futurePlanned = knownSum(futureBuckets.map(bucket => bucket.plannedResidual));
  const futureCommitted = knownSum(futureBuckets.map(bucket => bucket.committedUnpaid));
  const monthlyItems = group.buckets.flatMap(bucket => bucket.items);
  const overdueItems = monthlyItems.filter(item => isOverdue(item, today));
  const timingItems = monthlyItems.filter(item => item.measure === "ActualGross" && item.timingVarianceDays != null);
  const monthLabel = (value: string) => formatDate(value, locale, { month: "short", year: "numeric" });
  const qualityStatus = translateCode(t, "projects.cashFlowForecast.status", group.dataQualityStatus);

  const itemButton = (item: CashFlowLineItem) => onOpenItem ? (
    <Button type="button" size="sm" variant="outline" onClick={() => onOpenItem(item)}>
      <ExternalLink className="me-2 size-4" aria-hidden="true" />{t("projects.cashFlow.openSourceRecord")}
    </Button>
  ) : null;

  return (
    <section className="space-y-4" aria-labelledby={`cash-flow-forecast-${currency ?? "unknown"}`}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 id={`cash-flow-forecast-${currency ?? "unknown"}`} className="text-base font-semibold">{currencyLabel}</h3>
        <span className={`rounded-full px-2 py-1 text-xs ${group.dataQualityStatus === "Available" ? "bg-muted text-muted-foreground" : "bg-warning/10 text-warning"}`}>
          {qualityStatus}
        </span>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("projects.cashFlowForecast.expectedOutflowChart")}</CardTitle>
          <p className="text-sm text-muted-foreground">{t("projects.cashFlowForecast.separateSeriesNotice")}</p>
        </CardHeader>
        <CardContent>
          {group.buckets.length === 0 ? (
            <p role="status" className="rounded-md border p-4 text-sm text-muted-foreground">{t("projects.cashFlowForecast.noMonthlyData")}</p>
          ) : (
            <div role="img" aria-label={`${t("projects.cashFlowForecast.expectedOutflowChart")}: ${currencyLabel}`} className="h-72 w-full">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={group.buckets} margin={{ top: 8, right: 12, bottom: 8, left: 8 }}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="periodStart" tickFormatter={monthLabel} />
                  <YAxis tickFormatter={value => formatNumber(Number(value), { notation: "compact" })} />
                  <Tooltip
                    labelFormatter={label => monthLabel(String(label))}
                    formatter={(value, name) => [formatAmount(Number(value), currency, formatCurrency, t), String(name)]}
                  />
                  <Legend />
                  <Bar dataKey="plannedResidual" name={t("projects.cashFlow.category.plannedResidual")} fill="hsl(var(--fin-navy))" />
                  <Bar dataKey="committedUnpaid" name={t("projects.cashFlow.category.committedUnpaid")} fill="hsl(var(--warning))" />
                  <Bar dataKey="actualGross" name={t("projects.cashFlow.category.actualGross")} fill="hsl(var(--fin-violet))" />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("projects.cashFlowForecast.futureRequirements")}</CardTitle>
          <p className="text-sm text-muted-foreground">{t("projects.cashFlowForecast.futureRequirementsCurrentState")}</p>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-2">
          <div className="rounded-md border p-3">
            <p className="text-xs text-muted-foreground">{t("projects.cashFlow.category.plannedResidual")}</p>
            <p className="mt-1 font-semibold tabular-nums">{formatAmount(futurePlanned, currency, formatCurrency, t)}</p>
          </div>
          <div className="rounded-md border p-3">
            <p className="text-xs text-muted-foreground">{t("projects.cashFlow.category.committedUnpaid")}</p>
            <p className="mt-1 font-semibold tabular-nums">{formatAmount(futureCommitted, currency, formatCurrency, t)}</p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t("projects.cashFlowForecast.monthlyComparison")}</CardTitle>
          <p className="text-sm text-muted-foreground">{t("projects.cashFlowForecast.currentStateComparisonNotice")}</p>
        </CardHeader>
        <CardContent className="overflow-x-auto">
          <Table>
            <TableHeader><TableRow>
              <TableHead>{t("projects.cashFlowForecast.period")}</TableHead>
              <TableHead className="text-end">{t("projects.cashFlow.category.plannedResidual")}</TableHead>
              <TableHead className="text-end">{t("projects.cashFlow.category.committedUnpaid")}</TableHead>
              <TableHead className="text-end">{t("projects.cashFlow.category.actualGross")}</TableHead>
              <TableHead className="text-end">{t("projects.cashFlow.category.executedAdjustments")}</TableHead>
              <TableHead className="text-end">{t("projects.cashFlow.category.actualNet")}</TableHead>
            </TableRow></TableHeader>
            <TableBody>
              {group.buckets.map(bucket => (
                <TableRow key={bucket.periodStart}>
                  <TableCell>{monthLabel(bucket.periodStart)}</TableCell>
                  <TableCell className="text-end tabular-nums">{formatAmount(bucket.plannedResidual, currency, formatCurrency, t)}</TableCell>
                  <TableCell className="text-end tabular-nums">{formatAmount(bucket.committedUnpaid, currency, formatCurrency, t)}</TableCell>
                  <TableCell className="text-end tabular-nums">{formatAmount(bucket.actualGross, currency, formatCurrency, t)}</TableCell>
                  <TableCell className="text-end tabular-nums">{formatAmount(bucket.executedAdjustments, currency, formatCurrency, t)}</TableCell>
                  <TableCell className="text-end tabular-nums">{formatAmount(bucket.actualNet, currency, formatCurrency, t)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t("projects.cashFlowForecast.overduePayments")}</CardTitle>
            <p className="text-sm text-muted-foreground">{t("projects.cashFlowForecast.overdueUnpaidNotice")}</p>
          </CardHeader>
          <CardContent>
            {overdueItems.length === 0 ? (
              <p role="status" className="text-sm text-muted-foreground">{t("projects.cashFlowForecast.noOverduePayments")}</p>
            ) : (
              <ul className="space-y-2">
                {overdueItems.map((item, index) => (
                  <li key={`${item.sourceType}-${item.sourceId}-${index}`} className="flex flex-wrap items-start justify-between gap-3 rounded-md border p-3">
                    <div className="min-w-0">
                      <p className="font-medium">{item.description?.trim() || translateCode(t, "projects.cashFlow.category", item.measure)}</p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {translateCode(t, "projects.cashFlow.category", item.measure)}
                        {dueDateFor(item) ? ` · ${t("projects.cashFlow.expectedDate")}: ${formatDate(dueDateFor(item), locale, { dateStyle: "medium" })}` : ""}
                      </p>
                    </div>
                    <div className="text-end">
                      <p className="font-medium tabular-nums">{formatAmount(item.amount, item.currency ?? currency, formatCurrency, t)}</p>
                      {itemButton(item)}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t("projects.cashFlowForecast.paymentTiming")}</CardTitle>
            <p className="text-sm text-muted-foreground">{t("projects.cashFlowForecast.timingIsNotAmountVariance")}</p>
          </CardHeader>
          <CardContent>
            {timingItems.length === 0 ? (
              <p role="status" className="text-sm text-muted-foreground">{t("projects.cashFlowForecast.noTimingHistory")}</p>
            ) : (
              <ul className="space-y-2">
                {timingItems.map((item, index) => {
                  const days = item.timingVarianceDays ?? 0;
                  const timingLabel = days > 0 ? t("projects.cashFlowForecast.late")
                    : days < 0 ? t("projects.cashFlowForecast.early") : t("projects.cashFlowForecast.onTime");
                  return (
                    <li key={`${item.sourceType}-${item.sourceId}-${index}`} className="flex flex-wrap items-start justify-between gap-3 rounded-md border p-3">
                      <div className="min-w-0">
                        <p className="font-medium">{item.description?.trim() || translateCode(t, "projects.cashFlow.category", item.measure)}</p>
                        <p className="mt-1 text-xs text-muted-foreground">
                          {item.expectedDate ? `${t("projects.cashFlow.expectedDate")}: ${formatDate(item.expectedDate, locale, { dateStyle: "medium" })}` : ""}
                          {item.eventDate ? ` · ${t("projects.cashFlowForecast.actualDate")}: ${formatDate(item.eventDate, locale, { dateStyle: "medium" })}` : ""}
                        </p>
                      </div>
                      <div className="text-end">
                        <p className="font-medium">{t("projects.cashFlowForecast.timingVarianceDays", { days: formatNumber(days, { signDisplay: "always" }) })}</p>
                        <p className="text-xs text-muted-foreground">{timingLabel}</p>
                        {itemButton(item)}
                      </div>
                    </li>
                  );
                })}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
    </section>
  );
}

export function CashFlowForecastDashboard({
  projectId,
  enabled = true,
  fiscalYearOptions = [],
  onOpenItem,
}: CashFlowForecastDashboardProps) {
  const { t, locale, formatCurrency, formatNumber } = useI18n();
  const validOptions = React.useMemo(() => fiscalYearOptions.filter(option => {
    const from = parseCashFlowDateKey(option.from);
    const to = parseCashFlowDateKey(option.to);
    return from && to && option.from <= option.to;
  }), [fiscalYearOptions]);
  const today = cashFlowDateKey(new Date());
  const [selectedFiscalYear, setSelectedFiscalYear] = React.useState("");
  const [selectedCurrency, setSelectedCurrency] = React.useState("all");

  React.useEffect(() => {
    if (validOptions.some(option => option.value === selectedFiscalYear)) return;
    const current = validOptions.find(option => option.from <= today && today <= option.to);
    setSelectedFiscalYear(current?.value ?? validOptions[0]?.value ?? "");
  }, [selectedFiscalYear, today, validOptions]);

  const selectedYearOption = validOptions.find(option => option.value === selectedFiscalYear);
  const request = {
    from: selectedYearOption?.from ?? "",
    to: selectedYearOption?.to ?? "",
    granularity: "Month" as const,
  };
  const queryEnabled = enabled && Boolean(selectedYearOption);
  const cashFlow = useProjectCashFlow(projectId, request, queryEnabled);
  const data = cashFlow.data;
  const groups = data?.currencyGroups ?? [];
  const keys = React.useMemo(() => groups.map(group => currencyKey(group.currency)), [groups]);
  const visibleGroups = selectedCurrency === "all" ? groups : groups.filter(group => currencyKey(group.currency) === selectedCurrency);

  React.useEffect(() => {
    if (selectedCurrency !== "all" && !keys.includes(selectedCurrency)) setSelectedCurrency("all");
  }, [keys, selectedCurrency]);

  return (
    <section dir={locale.toLowerCase().startsWith("ar") ? "rtl" : "ltr"} aria-labelledby="cash-flow-forecast-title">
      <Card>
        <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3">
          <div>
            <CardTitle id="cash-flow-forecast-title">{t("projects.cashFlowForecast.title")}</CardTitle>
            <p className="mt-1 text-sm text-muted-foreground">{t("projects.cashFlowForecast.description")}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <label htmlFor="cash-flow-forecast-fiscal-year" className="sr-only">{t("projects.cashFlow.fiscalYear")}</label>
            <select
              id="cash-flow-forecast-fiscal-year"
              className="h-9 rounded-md border bg-background px-2 text-sm"
              value={selectedFiscalYear}
              onChange={event => setSelectedFiscalYear(event.target.value)}
              disabled={!enabled || validOptions.length === 0}
            >
              {validOptions.length === 0 ? <option value="">{t("projects.cashFlow.fiscalYearUnavailable")}</option> : null}
              {validOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
            <label htmlFor="cash-flow-forecast-currency" className="sr-only">{t("projects.cashFlow.currency")}</label>
            <select
              id="cash-flow-forecast-currency"
              className="h-9 rounded-md border bg-background px-2 text-sm"
              value={selectedCurrency}
              onChange={event => setSelectedCurrency(event.target.value)}
              disabled={groups.length === 0}
            >
              <option value="all">{t("projects.cashFlow.allCurrencies")}</option>
              {groups.map((group, index) => <option key={`${currencyKey(group.currency)}-${index}`} value={currencyKey(group.currency)}>
                {normalizeCurrency(group.currency) ?? t("projects.cashFlow.currencyUnavailable")}
              </option>)}
            </select>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div role="status" className="rounded-md border border-warning/30 bg-warning/5 p-3 text-sm">
            <p className="flex items-start gap-2 font-medium">
              <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden="true" />
              {t("projects.cashFlowForecast.historicalSnapshotUnavailable")}
            </p>
            <p className="mt-1 text-muted-foreground">{t("projects.cashFlowForecast.currentStateNotice")}</p>
          </div>

          {!enabled ? <p role="status" className="rounded-md border p-4 text-sm text-muted-foreground">{t("projects.financialsPermissionRequired")}</p> : null}
          {enabled && validOptions.length === 0 ? <p role="status" className="rounded-md border p-4 text-sm text-muted-foreground">{t("projects.cashFlow.fiscalYearUnavailable")}</p> : null}
          {queryEnabled && cashFlow.isLoading ? <p role="status" className="py-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : null}
          {queryEnabled && cashFlow.isError ? (
            <div role="alert" className="rounded-md border border-destructive/30 p-4 text-sm text-destructive">
              <p>{t("projects.cashFlowForecast.loadFailed")}</p>
              <Button type="button" variant="outline" size="sm" className="mt-3" onClick={() => void cashFlow.refetch()}>{t("common.retry")}</Button>
            </div>
          ) : null}
          {data?.dataQualityIssues.length ? (
            <div role="status" className="rounded-md border border-warning/30 bg-warning/5 p-3">
              <p className="text-sm font-medium">{t("projects.cashFlow.dataQualityNotice")}</p>
              <ul className="mt-2 list-inside list-disc space-y-1 text-sm text-muted-foreground">
                {data.dataQualityIssues.map((issue, index) => <li key={`${issue.code}-${issue.sourceId ?? ""}-${index}`}>
                  {issueLabel(issue, t)}
                </li>)}
              </ul>
            </div>
          ) : null}
          {queryEnabled && !cashFlow.isLoading && !cashFlow.isError && data?.dataQualityStatus === "Unavailable" ? (
            <p role="status" className="rounded-md border p-4 text-sm text-muted-foreground">{t("projects.cashFlowForecast.dataUnavailable")}</p>
          ) : null}
          {queryEnabled && !cashFlow.isLoading && !cashFlow.isError && data && data.dataQualityStatus !== "Unavailable" ? (
            visibleGroups.length === 0 ? (
              <p role="status" className="rounded-md border p-4 text-sm text-muted-foreground">{t("projects.cashFlowForecast.noMonthlyData")}</p>
            ) : visibleGroups.map((group, index) => <CurrencyForecast
              key={`${currencyKey(group.currency)}-${index}`}
              group={group}
              locale={locale}
              today={today}
              formatCurrency={formatCurrency}
              formatNumber={formatNumber}
              t={t}
              onOpenItem={onOpenItem}
            />)
          ) : null}
        </CardContent>
      </Card>
    </section>
  );
}
