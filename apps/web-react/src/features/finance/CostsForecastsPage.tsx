import * as React from "react";
import { AlertTriangle, ArrowUpRight, Banknote, RefreshCw } from "lucide-react";
import { Link } from "react-router-dom";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Pagination } from "@components/ui/Pagination";
import { Skeleton } from "@components/ui/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { getLookupTextClass } from "@lib/utils";
import { useBudgets, type BudgetDto } from "./api/finance";
import { forecastVariance, forecastVariancePercent, normalizedCurrency, summarizeForecastPage } from "./forecast-calculations";

const PAGE_SIZE = 20;

const budgetStatusKeys: Record<string, string> = {
  active: "active",
  approved: "approved",
  cancelled: "cancelled",
  closed: "closed",
  draft: "draft",
  open: "open",
  pending: "pending",
  planned: "planned",
};

export function CostsForecastsPage() {
  const { t, formatCurrency } = useI18n();
  const [page, setPage] = React.useState(1);
  const budgetsQuery = useBudgets({ page, pageSize: PAGE_SIZE, hasForecast: true });
  const budgets = budgetsQuery.data?.items ?? [];
  const totalCount = budgetsQuery.data?.totalCount ?? 0;
  const totalPages = budgetsQuery.data?.totalPages ?? Math.ceil(totalCount / PAGE_SIZE);
  const summaries = summarizeForecastPage(budgets);
  const unavailable = t("costs.forecasts.unavailable");

  return (
    <div className="space-y-6">
      <PageHeader title={t("costs.forecasts.title")} description={t("costs.forecasts.description")} />

      {budgetsQuery.isLoading ? <ForecastLoadingState /> : budgetsQuery.isError ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
            <AlertTriangle className="h-10 w-10 text-destructive" />
            <h2 className="font-medium">{t("costs.forecasts.loadFailed")}</h2>
            <Button variant="outline" size="sm" onClick={() => void budgetsQuery.refetch()}>
              <RefreshCw className="me-2 h-4 w-4" />{t("costs.forecasts.retry")}
            </Button>
          </CardContent>
        </Card>
      ) : budgets.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-2 py-16 text-center">
            <Banknote className="h-10 w-10 text-muted-foreground" />
            <h2 className="font-medium">{t("costs.forecasts.emptyTitle")}</h2>
            <p className="max-w-md text-xs text-muted-foreground">{t("costs.forecasts.emptyDescription")}</p>
          </CardContent>
        </Card>
      ) : (
        <>
          <PageTotals summaries={summaries} unavailable={unavailable} formatCurrency={formatCurrency} t={t} />
          <Card className="shadow-none">
            <CardHeader className="gap-1">
              <CardTitle className="text-base">{t("costs.forecasts.title")}</CardTitle>
              <p className="text-xs text-muted-foreground">{t("costs.forecasts.description")}</p>
            </CardHeader>
            <CardContent className="p-0">
              <Table className="min-w-[1100px]">
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("costs.forecasts.budgetLine")}</TableHead>
                    <TableHead>{t("costs.forecasts.project")}</TableHead>
                    <TableHead>{t("costs.forecasts.category")}</TableHead>
                    <TableHead className="text-end">{t("costs.forecasts.currentAllocation")}</TableHead>
                    <TableHead className="text-end">{t("costs.forecasts.forecast")}</TableHead>
                    <TableHead className="text-end">{t("costs.forecasts.forecastVariance")}</TableHead>
                    <TableHead className="text-end">{t("costs.forecasts.variancePercent")}</TableHead>
                    <TableHead>{t("costs.forecasts.fiscalPeriod")}</TableHead>
                    <TableHead>{t("costs.forecasts.status")}</TableHead>
                    <TableHead>{t("costs.forecasts.currency")}</TableHead>
                    <TableHead className="text-end">{t("costs.forecasts.viewFinancials")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {budgets.map((budget) => <ForecastRow key={budget.id} budget={budget} unavailable={unavailable} formatCurrency={formatCurrency} t={t} />)}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-xs text-muted-foreground">{t("costs.forecasts.pageSizeNote", { pageSize: PAGE_SIZE })}</p>
            {totalPages > 1 ? <Pagination page={page} totalPages={totalPages} total={totalCount} pageSize={PAGE_SIZE} onChange={setPage} /> : null}
          </div>
        </>
      )}
    </div>
  );
}

function ForecastLoadingState() {
  return (
    <div className="space-y-4">
      <div className="grid gap-4 md:grid-cols-3">{[1, 2, 3].map((value) => <Card key={value}><CardHeader className="pb-2"><Skeleton className="h-4 w-28" /></CardHeader><CardContent><Skeleton className="h-8 w-28" /></CardContent></Card>)}</div>
      <Card><CardContent className="space-y-3 p-4">{[1, 2, 3, 4, 5].map((value) => <Skeleton key={value} className="h-11 w-full" />)}</CardContent></Card>
    </div>
  );
}

function PageTotals({ summaries, unavailable, formatCurrency, t }: {
  summaries: ReturnType<typeof summarizeForecastPage>;
  unavailable: string;
  formatCurrency: (value: number, currency?: string) => string;
  t: (key: string, params?: Record<string, string | number>) => string;
}) {
  return (
    <section className="space-y-3" aria-label={t("costs.forecasts.pageTotals")}>
      <div><h2 className="text-sm font-semibold">{t("costs.forecasts.pageTotals")}</h2><p className="mt-1 text-xs text-muted-foreground">{t("costs.forecasts.pageTotalsDescription")}</p></div>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {summaries.map((summary) => (
          <Card key={summary.currency || "unknown"} className="shadow-none">
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">{summary.currency || unavailable}</CardTitle>
              <span className="text-[11px] text-muted-foreground">{t("costs.forecasts.recordsOnPage", { count: summary.count })}</span>
            </CardHeader>
            <CardContent className="grid grid-cols-3 gap-3">
              <AmountSummary label={t("costs.forecasts.pageAllocation")} value={money(summary.allocation, summary.currency, formatCurrency, unavailable)} semantic="indigo" />
              <AmountSummary label={t("costs.forecasts.pageForecast")} value={money(summary.forecast, summary.currency, formatCurrency, unavailable)} semantic="violet" />
              <AmountSummary label={t("costs.forecasts.pageVariance")} value={summary.variance === null ? unavailable : varianceValue(summary.variance, summary.currency, formatCurrency, unavailable)} semantic={summary.variance !== null ? (summary.variance < 0 ? "red" : summary.variance > 0 ? "green" : undefined) : undefined} />
            </CardContent>
          </Card>
        ))}
      </div>
    </section>
  );
}

function AmountSummary({ label, value, semantic }: { label: string; value: string; semantic?: "indigo" | "violet" | "green" | "red" }) {
  const valueClass = semantic ? `text-fin-${semantic}` : "";
  return <div className="min-w-0"><p className="truncate text-[10px] uppercase tracking-wide text-muted-foreground">{label}</p><p className={`mt-1 truncate text-sm font-semibold tabular-nums ${valueClass}`}>{value}</p></div>;
}

function ForecastRow({ budget, unavailable, formatCurrency, t }: {
  budget: BudgetDto;
  unavailable: string;
  formatCurrency: (value: number, currency?: string) => string;
  t: (key: string, params?: Record<string, string | number>) => string;
}) {
  const currency = normalizedCurrency(budget.currency);
  const variance = forecastVariance(budget);
  const pct = forecastVariancePercent(budget);
  const projectId = budget.projectId?.trim() ?? "";
  const status = budget.status?.trim() ?? "";
  const statusKey = budgetStatusKeys[status.toLowerCase()];
  const statusLabel = statusKey ? t(`costs.forecasts.budgetStatuses.${statusKey}`) : status || unavailable;
  const projectHref = `/projects/${encodeURIComponent(projectId)}?tab=financials`;

  return (
    <TableRow>
      <TableCell className="min-w-44"><p className="font-medium">{displayText(budget.name, unavailable)}</p>{budget.description?.trim() ? <p className="mt-1 line-clamp-2 text-[11px] text-muted-foreground">{budget.description.trim()}</p> : null}</TableCell>
      <TableCell className="min-w-36">{projectId ? <Link to={projectHref} className="font-mono text-[11px] text-primary hover:underline" title={t("costs.forecasts.projectIdFallback")}>{projectId}</Link> : unavailable}</TableCell>
      <TableCell className={`min-w-28 ${getLookupTextClass(budget.category)}`}>{displayText(budget.category, unavailable)}</TableCell>
      <TableCell className="text-end font-semibold tabular-nums text-fin-indigo">{money(budget.approvedAmount, currency, formatCurrency, unavailable)}</TableCell>
      <TableCell className="text-end tabular-nums text-fin-violet">{money(budget.forecastAmount, currency, formatCurrency, unavailable)}</TableCell>
      <TableCell className="text-end tabular-nums">{variance === null ? unavailable : <><span className={variance < 0 ? "font-semibold text-fin-red" : variance > 0 ? "font-semibold text-fin-green" : "font-medium"}>{varianceValue(variance, currency, formatCurrency, unavailable)}</span><span className="mt-1 block text-[10px] text-muted-foreground">{variance > 0 ? t("costs.forecasts.underAllocation") : variance < 0 ? t("costs.forecasts.overAllocation") : t("costs.forecasts.onAllocation")}</span></>}</TableCell>
      <TableCell className="text-end tabular-nums">{pct === null ? <span className="text-muted-foreground">{t("costs.forecasts.noAllocation")}</span> : <span className={pct > 0 ? "font-semibold text-fin-red" : pct < 0 ? "font-semibold text-fin-green" : "font-medium"}>{`${Math.round(pct)}%`}</span>}</TableCell>
      <TableCell className="whitespace-nowrap">{fiscalPeriodLabel(budget, t, unavailable)}</TableCell>
      <TableCell>{status ? <Badge status={status}>{statusLabel}</Badge> : unavailable}</TableCell>
      <TableCell className={`font-medium ${getLookupTextClass(currency)}`}>{currency || unavailable}</TableCell>
      <TableCell className="text-end">{projectId ? <Link to={projectHref} aria-label={`${t("costs.forecasts.viewFinancials")}: ${projectId}`} className="inline-flex h-8 items-center rounded-md px-2 text-sm hover:bg-muted"><ArrowUpRight className="me-1.5 h-3.5 w-3.5" aria-hidden="true" />{t("costs.forecasts.viewFinancials")}</Link> : unavailable}</TableCell>
    </TableRow>
  );
}

function money(value: number | null | undefined, currency: string, formatCurrency: (value: number, currency?: string) => string, unavailable: string): string {
  if (typeof value !== "number" || !Number.isFinite(value) || !currency) return unavailable;
  try { return formatCurrency(value, currency); } catch { return unavailable; }
}

function varianceValue(value: number, currency: string, formatCurrency: (value: number, currency?: string) => string, unavailable: string): string {
  const amount = money(Math.abs(value), currency, formatCurrency, unavailable);
  if (amount === unavailable || value === 0) return amount;
  return value > 0 ? `+${amount}` : `-${amount}`;
}

function fiscalPeriodLabel(budget: BudgetDto, t: (key: string, params?: Record<string, string | number>) => string, unavailable: string): string {
  if (!Number.isFinite(budget.fiscalYear) || budget.fiscalYear <= 0) return unavailable;
  const year = `${t("costs.forecasts.fiscalYearShort")} ${budget.fiscalYear}`;
  if (!Number.isFinite(budget.fiscalQuarter) || !budget.fiscalQuarter || budget.fiscalQuarter < 1 || budget.fiscalQuarter > 4) return year;
  return `${year} · ${t("costs.forecasts.quarterShort")}${budget.fiscalQuarter}`;
}

function displayText(value: string | null | undefined, unavailable: string): string {
  return value?.trim() || unavailable;
}
