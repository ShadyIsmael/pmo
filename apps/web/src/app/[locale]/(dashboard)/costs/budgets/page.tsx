"use client";

import React from "react";
import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { AlertTriangle, ArrowUpRight, Banknote, Info, RefreshCw } from "lucide-react";
import { useBudgets, type BudgetDto } from "@/lib/api/costs";
import { formatCurrency, isFiniteNumber } from "@/lib/utils";
import { TablePagination } from "@/components/ui/table-pagination";
import { getLookupTextClass } from "@/lib/status-presentation";

const budgetStatusKeys: Record<string, string> = {
  active: "budgetStatuses.active",
  approved: "budgetStatuses.approved",
  cancelled: "budgetStatuses.cancelled",
  closed: "budgetStatuses.closed",
  draft: "budgetStatuses.draft",
  open: "budgetStatuses.open",
  pending: "budgetStatuses.pending",
  planned: "budgetStatuses.planned",
};

type Translator = ReturnType<typeof useTranslations>;

function displayText(value: string | null | undefined, unavailable: string): string {
  const trimmed = value?.trim();
  return trimmed || unavailable;
}

function normalizedCurrency(value: string | null | undefined): string {
  return value?.trim().toUpperCase() ?? "";
}

function formatMoney(value: number | null | undefined, currency: string, locale: string, unavailable: string): string {
  if (!isFiniteNumber(value) || !currency) return unavailable;
  try {
    return formatCurrency(value, currency, locale);
  } catch {
    return unavailable;
  }
}

function lineVariance(item: BudgetDto): number | null {
  if (!isFiniteNumber(item.approvedAmount) || !isFiniteNumber(item.forecastAmount)) return null;
  return item.approvedAmount - item.forecastAmount;
}

function varianceLabel(t: Translator, value: number): string {
  if (value > 0) return t("underAllocation");
  if (value < 0) return t("overAllocation");
  return t("onAllocation");
}

function varianceValue(value: number, currency: string, locale: string, unavailable: string): string {
  const amount = formatMoney(Math.abs(value), currency, locale, unavailable);
  if (amount === unavailable || value === 0) return amount;
  return value > 0 ? `+${amount}` : `-${amount}`;
}

function fiscalPeriodLabel(t: Translator, item: BudgetDto, unavailable: string): string {
  if (!isFiniteNumber(item.fiscalYear) || item.fiscalYear <= 0) return unavailable;
  const year = `${t("fiscalYearShort")} ${item.fiscalYear}`;
  if (!isFiniteNumber(item.fiscalQuarter) || item.fiscalQuarter < 1 || item.fiscalQuarter > 4) return year;
  return `${year} · ${t("quarterShort")}${item.fiscalQuarter}`;
}

function budgetStatusLabel(t: Translator, status: string | null | undefined, unavailable: string): string {
  const trimmed = status?.trim();
  if (!trimmed) return unavailable;
  const key = budgetStatusKeys[trimmed.toLowerCase()];
  return key ? t(key as never) : trimmed;
}

function sumAmount(items: readonly BudgetDto[], selector: (_item: BudgetDto) => unknown): number | null {
  const values = items.map(selector);
  if (values.some((value) => !isFiniteNumber(value))) return null;
  return values.reduce<number>((total, value) => total + (value as number), 0);
}

type CurrencyPageSummary = {
  currency: string;
  planned: number | null;
  allocated: number | null;
  forecast: number | null;
  count: number;
};

function groupPageSummaries(items: readonly BudgetDto[]): CurrencyPageSummary[] {
  const groups = new Map<string, BudgetDto[]>();
  for (const item of items) {
    const currency = normalizedCurrency(item.currency);
    const key = currency || "__unknown__";
    groups.set(key, [...(groups.get(key) ?? []), item]);
  }

  return Array.from(groups.entries()).map(([key, group]) => ({
    currency: key === "__unknown__" ? "" : key,
    planned: sumAmount(group, (item) => item.plannedAmount),
    allocated: sumAmount(group, (item) => item.approvedAmount),
    forecast: sumAmount(group, (item) => item.forecastAmount),
    count: group.length,
  }));
}

export default function BudgetsPage() {
  const t = useTranslations("costs");
  const tBudgets = useTranslations("costs.budgets");
  const locale = useLocale();
  const formatterLocale = locale === "ar" ? "ar-EG" : "en-GB";
  const unavailable = tBudgets("unavailable");
  const [page, setPage] = React.useState(1);
  const pageSize = 20;

  const budgetsQuery = useBudgets({ page, pageSize });
  const budgets = budgetsQuery.data?.items ?? [];
  const totalPages = budgetsQuery.data?.totalPages ?? 0;
  const totalCount = budgetsQuery.data?.totalCount ?? 0;
  const pageSummaries = groupPageSummaries(budgets);

  return (
    <div className="space-y-6">
      <PageHeader title={tBudgets("title")} description={tBudgets("description")}>
        <Badge variant="secondary">{tBudgets("entity")}</Badge>
      </PageHeader>

      <Card className="border-fin-indigo/20 bg-fin-indigo/[0.04] shadow-none">
        <CardContent className="flex items-start gap-3 p-4 text-sm">
          <Info className="mt-0.5 h-4 w-4 shrink-0 text-fin-indigo" aria-hidden="true" />
          <div>
            <p className="font-medium">{tBudgets("identityNoteTitle")}</p>
            <p className="mt-1 text-xs leading-5 text-muted-foreground">{tBudgets("identityNote")}</p>
          </div>
        </CardContent>
      </Card>

      {budgetsQuery.isLoading ? (
        <LoadingState />
      ) : budgetsQuery.isError ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
            <AlertTriangle className="h-10 w-10 text-destructive" />
            <h2 className="font-medium">{tBudgets("loadFailed")}</h2>
            <Button variant="outline" size="sm" onClick={() => void budgetsQuery.refetch()}>
              <RefreshCw className="me-2 h-4 w-4" />{tBudgets("retry")}
            </Button>
          </CardContent>
        </Card>
      ) : budgets.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-2 py-16 text-center">
            <Banknote className="h-10 w-10 text-muted-foreground" />
            <h2 className="font-medium">{tBudgets("emptyTitle")}</h2>
            <p className="max-w-md text-xs text-muted-foreground">{tBudgets("emptyDescription")}</p>
          </CardContent>
        </Card>
      ) : (
        <>
          <PageTotals summaries={pageSummaries} t={tBudgets} locale={formatterLocale} unavailable={unavailable} />

          <Card className="shadow-none">
            <CardHeader className="gap-1">
              <CardTitle className="text-base">{tBudgets("tableTitle")}</CardTitle>
              <p className="text-xs text-muted-foreground">{tBudgets("tableDescription")}</p>
            </CardHeader>
            <CardContent className="p-0">
              <Table className="min-w-[1180px]">
                <TableHeader>
                  <TableRow>
                    <TableHead>{tBudgets("budgetLine")}</TableHead>
                    <TableHead>{tBudgets("project")}</TableHead>
                    <TableHead>{tBudgets("category")}</TableHead>
                    <TableHead className="text-end">{tBudgets("plannedAmount")}</TableHead>
                    <TableHead className="text-end">{tBudgets("currentAllocation")}</TableHead>
                    <TableHead className="text-end">{tBudgets("forecastAmount")}</TableHead>
                    <TableHead className="text-end">{tBudgets("forecastVariance")}</TableHead>
                    <TableHead>{tBudgets("fiscalPeriod")}</TableHead>
                    <TableHead>{tBudgets("status")}</TableHead>
                    <TableHead>{tBudgets("currency")}</TableHead>
                    <TableHead className="text-end">{tBudgets("actions")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {budgets.map((budget) => (
                    <BudgetRow
                      key={budget.id}
                      budget={budget}
                      locale={locale}
                      formatterLocale={formatterLocale}
                      unavailable={unavailable}
                      t={tBudgets}
                    />
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>

          <TablePagination
            page={page}
            totalPages={totalPages}
            summary={t("pageSummary", { page, totalPages, total: totalCount })}
            previousLabel={t("previous")}
            nextLabel={t("next")}
            onPageChange={setPage}
            footerNote={tBudgets("pageSizeNote", { pageSize: 20 })}
          />
        </>
      )}
    </div>
  );
}

function LoadingState() {
  return (
    <div className="space-y-4">
      <div className="grid gap-4 md:grid-cols-3">
        {[1, 2, 3].map((value) => (
          <Card key={value}>
            <CardHeader className="pb-2"><Skeleton className="h-4 w-28" /></CardHeader>
            <CardContent><Skeleton className="h-8 w-28" /></CardContent>
          </Card>
        ))}
      </div>
      <Card>
        <CardContent className="space-y-3 p-4">
          {[1, 2, 3, 4, 5].map((value) => <Skeleton key={value} className="h-11 w-full" />)}
        </CardContent>
      </Card>
    </div>
  );
}

function PageTotals({
  summaries,
  t,
  locale,
  unavailable,
}: {
  summaries: readonly CurrencyPageSummary[];
  t: Translator;
  locale: string;
  unavailable: string;
}) {
  return (
    <div className="space-y-3">
      <div>
        <h2 className="text-sm font-semibold">{t("pageTotals")}</h2>
        <p className="mt-1 text-xs text-muted-foreground">{t("pageTotalsDescription")}</p>
      </div>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {summaries.map((summary) => (
          <Card key={summary.currency || "unknown"} className="shadow-none">
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">{summary.currency || unavailable}</CardTitle>
              <span className="text-[11px] text-muted-foreground">{t("recordsOnPage", { count: summary.count })}</span>
            </CardHeader>
            <CardContent className="grid grid-cols-3 gap-3">
              <AmountSummary label={t("pagePlanned")} value={formatMoney(summary.planned, summary.currency, locale, unavailable)} semantic="navy" />
              <AmountSummary label={t("pageAllocated")} value={formatMoney(summary.allocated, summary.currency, locale, unavailable)} semantic="indigo" />
              <AmountSummary label={t("pageForecast")} value={formatMoney(summary.forecast, summary.currency, locale, unavailable)} semantic="violet" />
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}

function AmountSummary({ label, value, semantic }: { label: string; value: string; semantic?: "navy" | "indigo" | "violet" | "blue" | "amber" }) {
  const valueClass = semantic ? `text-fin-${semantic}` : "";
  return (
    <div className="min-w-0">
      <p className="truncate text-[10px] uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className={`mt-1 truncate text-sm font-semibold tabular-nums ${valueClass}`}>{value}</p>
    </div>
  );
}

function BudgetRow({
  budget,
  locale,
  formatterLocale,
  unavailable,
  t,
}: {
  budget: BudgetDto;
  locale: string;
  formatterLocale: string;
  unavailable: string;
  t: Translator;
}) {
  const currency = normalizedCurrency(budget.currency);
  const variance = lineVariance(budget);
  const projectId = budget.projectId?.trim() ?? "";
  const projectHref = projectId ? `/${locale}/projects/${encodeURIComponent(projectId)}?tab=financials` : "";
  const status = budget.status?.trim() ?? "";

  return (
    <TableRow>
      <TableCell className="min-w-48">
        <p className="font-medium">{displayText(budget.name, unavailable)}</p>
        {budget.description?.trim() ? <p className="mt-1 line-clamp-2 text-[11px] text-muted-foreground">{budget.description.trim()}</p> : null}
      </TableCell>
      <TableCell className="min-w-56">
        {projectId ? (
          <Link href={projectHref} className="font-mono text-[11px] text-primary hover:underline" title={t("projectIdFallback")}>
            {projectId}
          </Link>
        ) : unavailable}
      </TableCell>
      <TableCell className={`min-w-32 ${getLookupTextClass(budget.category)}`}>{displayText(budget.category, unavailable)}</TableCell>
      <TableCell className="text-end tabular-nums">{formatMoney(budget.plannedAmount, currency, formatterLocale, unavailable)}</TableCell>
      <TableCell className="text-end font-semibold tabular-nums text-fin-indigo" title={t("currentAllocationHint")}>
        {formatMoney(budget.approvedAmount, currency, formatterLocale, unavailable)}
      </TableCell>
      <TableCell className="text-end tabular-nums text-fin-violet">{formatMoney(budget.forecastAmount, currency, formatterLocale, unavailable)}</TableCell>
      <TableCell className="text-end tabular-nums">
        {variance === null ? unavailable : (
          <>
            <span className={variance < 0 ? "font-semibold text-fin-red" : variance > 0 ? "font-semibold text-fin-green" : "font-medium"}>
              {varianceValue(variance, currency, formatterLocale, unavailable)}
            </span>
            <span className="mt-1 block text-[10px] text-muted-foreground">{varianceLabel(t, variance)}</span>
          </>
        )}
      </TableCell>
      <TableCell className="whitespace-nowrap">{fiscalPeriodLabel(t, budget, unavailable)}</TableCell>
      <TableCell>{status ? <Badge status={status}>{budgetStatusLabel(t, status, unavailable)}</Badge> : unavailable}</TableCell>
      <TableCell className={`font-medium ${getLookupTextClass(currency)}`}>{currency || unavailable}</TableCell>
      <TableCell className="text-end">
        {projectId ? (
          <Button asChild variant="ghost" size="sm" className="h-8 px-2">
            <Link href={projectHref} aria-label={`${t("viewFinancials")}: ${projectId}`}>
              <ArrowUpRight className="me-1.5 h-3.5 w-3.5" aria-hidden="true" />
              {t("viewFinancials")}
            </Link>
          </Button>
        ) : unavailable}
      </TableCell>
    </TableRow>
  );
}
