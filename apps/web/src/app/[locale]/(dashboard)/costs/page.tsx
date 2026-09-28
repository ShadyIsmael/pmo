"use client";

import React from "react";
import Link from "next/link";
import { useLocale, useTranslations } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { AlertTriangle, ArrowUpRight, Banknote, DollarSign, RefreshCw, TrendingDown, TrendingUp, type LucideIcon } from "lucide-react";
import { useGlobalFinancialSummary, type CurrencyFinancialSummary, type ProjectFinancialBreakdown } from "@/lib/api/costs";
import { formatCurrency } from "@/lib/utils";

type Translator = ReturnType<typeof useTranslations>;
type StatusVariant = "status-red" | "status-amber" | "status-green" | "status-neutral";
const EMPTY_CURRENCY_SUMMARIES: CurrencyFinancialSummary[] = [];

function finiteValue(value: number | null | undefined): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function formatMoney(value: number | null | undefined, currency: string, locale: string): string {
  const amount = finiteValue(value);
  const code = currency?.trim();
  if (amount == null || !code) return "—";
  try {
    return formatCurrency(amount, code, locale);
  } catch {
    return "—";
  }
}

function ratioPercent(value: number | null | undefined, base: number | null | undefined): number | null {
  const amount = finiteValue(value);
  const denominator = finiteValue(base);
  if (amount == null || denominator == null || denominator <= 0) return null;
  return (amount / denominator) * 100;
}

function forecastState(summary: Pick<CurrencyFinancialSummary, "forecastVariance"> | Pick<ProjectFinancialBreakdown, "forecastVariance">, t: Translator): { label: string; variant: StatusVariant } {
  const variance = finiteValue(summary.forecastVariance);
  if (variance == null) return { label: t("financialStatus.unavailable"), variant: "status-neutral" };
  if (variance < 0) return { label: t("forecastStatus.overBudget"), variant: "status-red" };
  if (variance > 0) return { label: t("forecastStatus.underBudget"), variant: "status-green" };
  return { label: t("forecastStatus.onBudget"), variant: "status-neutral" };
}

function financialAttention(project: ProjectFinancialBreakdown, t: Translator): { label: string; variant: StatusVariant } {
  const variance = finiteValue(project.forecastVariance);
  const available = finiteValue(project.availableBudget);
  if (variance == null && available == null) return { label: t("financialStatus.unavailable"), variant: "status-neutral" };
  if (variance != null && variance < 0) return { label: t("financialStatus.overBudget"), variant: "status-red" };
  if (available != null && available < 0) return { label: t("financialStatus.overcommitted"), variant: "status-amber" };
  return { label: t("financialStatus.withinBudget"), variant: "status-green" };
}

export default function CostsPage() {
  const t = useTranslations("costs");
  const locale = useLocale();
  const formatterLocale = locale === "ar" ? "ar-EG" : "en-GB";
  const summaryQuery = useGlobalFinancialSummary();
  const data = summaryQuery.data;
  const [selectedCurrency, setSelectedCurrency] = React.useState("");

  const currencies = data?.byCurrency ?? EMPTY_CURRENCY_SUMMARIES;

  React.useEffect(() => {
    const selectedStillExists = currencies.some((summary) => summary.currency === selectedCurrency);
    if (!selectedStillExists) setSelectedCurrency(currencies[0]?.currency ?? "");
  }, [currencies, selectedCurrency]);

  const activeCurrency = currencies.find((summary) => summary.currency === selectedCurrency)?.currency ?? currencies[0]?.currency ?? "";
  const currencySummary = currencies.find((summary) => summary.currency === activeCurrency);
  const projectsForCurrency = data?.byProject.filter((project) => project.currency === activeCurrency) ?? [];

  if (summaryQuery.isLoading) {
    return (
      <div className="space-y-6">
        <PageHeader title={t("title")} description={t("description")} />
        <div className="grid gap-4 xl:grid-cols-2">
          {Array.from({ length: 2 }).map((_, i) => (
            <Card key={i}>
              <CardHeader className="pb-2"><Skeleton className="h-4 w-32" /></CardHeader>
              <CardContent><Skeleton className="h-16 w-40" /></CardContent>
            </Card>
          ))}
        </div>
        <div className="grid gap-4 md:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-32 w-full" />)}
        </div>
        <Skeleton className="h-72 w-full" />
      </div>
    );
  }

  if (summaryQuery.isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t("title")} description={t("description")} />
        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
            <AlertTriangle className="h-10 w-10 text-destructive" />
            <h2 className="font-medium">{t("loadFailed")}</h2>
            <button onClick={() => void summaryQuery.refetch()} className="inline-flex items-center gap-2 text-sm text-primary hover:underline">
              <RefreshCw className="h-4 w-4" />{t("retry")}
            </button>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (!data || currencies.length === 0) {
    return (
      <div className="space-y-6">
        <PageHeader title={t("title")} description={t("description")} />
        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-2 py-16 text-center">
            <Banknote className="h-10 w-10 text-muted-foreground" />
            <h2 className="font-medium">{t("noData")}</h2>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t("title")} description={t("description")} />

      <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-muted/20 px-4 py-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{t("currencyContext")}</p>
          <p className="mt-1 text-xs text-muted-foreground">{t("currencyContextDescription")}</p>
        </div>
        {currencies.length > 1 ? (
          <Tabs value={activeCurrency} onValueChange={setSelectedCurrency}>
            <TabsList>
              {currencies.map((summary) => <TabsTrigger key={summary.currency} value={summary.currency}>{summary.currency}</TabsTrigger>)}
            </TabsList>
          </Tabs>
        ) : (
          <Badge variant="outline" className="px-3 py-1 text-xs">{activeCurrency || t("financialStatus.unavailable")}</Badge>
        )}
      </div>

      {!currencySummary || projectsForCurrency.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center gap-2 py-16 text-center">
            <Banknote className="h-10 w-10 text-muted-foreground" />
            <h2 className="font-medium">{t("noData")}</h2>
          </CardContent>
        </Card>
      ) : (
        <CurrencyDashboard summary={currencySummary} projects={projectsForCurrency} t={t} locale={formatterLocale} activeLocale={locale} />
      )}
    </div>
  );
}

function CurrencyDashboard({ summary, projects, t, locale, activeLocale }: { summary: CurrencyFinancialSummary; projects: ProjectFinancialBreakdown[]; t: Translator; locale: string; activeLocale: string }) {
  return (
    <div className="space-y-6">
      <div className="grid gap-4 xl:grid-cols-2">
        <BudgetAuthorityCard summary={summary} t={t} locale={locale} />
        <ForecastPositionCard summary={summary} t={t} locale={locale} />
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <MetricCard title={t("actualCost")} value={formatMoney(summary.actualCost, summary.currency, locale)} icon={DollarSign} semantic="blue" />
        <MetricCard title={t("committedCost")} value={formatMoney(summary.committedCost, summary.currency, locale)} icon={Banknote} semantic="amber" />
        <MetricCard
          title={t("availableBudget")}
          value={formatMoney(summary.availableBudget, summary.currency, locale)}
          icon={summary.availableBudget < 0 ? TrendingDown : TrendingUp}
          valueClassName={summary.availableBudget < 0 ? "text-destructive" : undefined}
          semantic="green"
        />
      </div>

      <SpendingPositionCard summary={summary} t={t} locale={locale} />
      <ProjectFinancialTable projects={projects} t={t} locale={locale} activeLocale={activeLocale} />
    </div>
  );
}

function MetricCard({ title, value, icon: Icon, valueClassName, semantic }: { title: string; value: string; icon: LucideIcon; valueClassName?: string; semantic?: "navy" | "blue" | "amber" | "green" | "violet" | "indigo" }) {
  const iconBg = semantic ? `fin-tint-${semantic}` : "";
  const iconText = semantic ? `text-fin-${semantic}` : "";
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
        <span className={`inline-flex h-7 w-7 items-center justify-center rounded-lg ${iconBg || "bg-muted/50"}`}>
          <Icon className={`h-4 w-4 ${iconText || "text-muted-foreground"}`} />
        </span>
      </CardHeader>
      <CardContent><div className={`text-2xl font-bold ${valueClassName ?? ""}`}>{value}</div></CardContent>
    </Card>
  );
}

function BudgetAuthorityCard({ summary, t, locale }: { summary: CurrencyFinancialSummary; t: Translator; locale: string }) {
  const ceiling = finiteValue(summary.approvedBudget);
  const allocated = finiteValue(summary.allocatedBudget);
  const unallocated = finiteValue(summary.unallocatedBudget);
  const allocatedPct = ratioPercent(allocated, ceiling);
  const unallocatedPct = ratioPercent(unallocated, ceiling);

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <span className="inline-flex h-7 w-7 items-center justify-center rounded-lg fin-tint-navy">
            <Banknote className="h-4 w-4 text-fin-navy" />
          </span>
          <div>
            <CardTitle>{t("budgetAuthority")}</CardTitle>
            <p className="text-xs text-muted-foreground">{t("budgetAuthorityDescription")}</p>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex items-end justify-between gap-3">
          <div>
            <p className="text-xs text-muted-foreground">{t("budgetCeiling")}</p>
            <div className="mt-1 text-3xl font-bold">{formatMoney(ceiling, summary.currency, locale)}</div>
          </div>
            <span className="text-xs font-medium text-muted-foreground">{t("projects", { count: summary.projectCount })}</span>
        </div>
        <div className="space-y-2">
          <div className="flex h-3 overflow-visible rounded-full bg-muted/60" aria-label={t("allocationBarLabel")}>
            {allocatedPct != null && allocatedPct > 0 && <div className="h-3 shrink-0 rounded-l-full bg-fin-indigo" style={{ width: `${Math.max(0, allocatedPct)}%` }} />}
            {unallocatedPct != null && unallocatedPct > 0 && <div className="h-3 shrink-0 rounded-r-full bg-fin-slate/40" style={{ width: `${Math.max(0, unallocatedPct)}%` }} />}
          </div>
          <div className="grid grid-cols-2 gap-3 text-xs">
            <div className="flex items-center gap-1.5"><span className="inline-block h-2 w-2 rounded-full bg-fin-indigo" /><div><p className="text-muted-foreground">{t("allocatedBudget")}</p><p className="mt-1 font-semibold">{formatMoney(allocated, summary.currency, locale)}</p></div></div>
            <div className="flex items-center gap-1.5"><span className="inline-block h-2 w-2 rounded-full bg-fin-slate/40" /><div><p className="text-muted-foreground">{t("unallocatedBudget")}</p><p className="mt-1 font-semibold">{formatMoney(unallocated, summary.currency, locale)}</p></div></div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function SpendingPositionCard({ summary, t, locale }: { summary: CurrencyFinancialSummary; t: Translator; locale: string }) {
  const ceiling = finiteValue(summary.approvedBudget);
  const actual = finiteValue(summary.actualCost);
  const committed = finiteValue(summary.committedCost);
  const available = finiteValue(summary.availableBudget);
  const actualPct = ratioPercent(actual, ceiling);
  const committedPct = ratioPercent(committed, ceiling);
  const availablePct = available == null ? null : ratioPercent(Math.max(0, available), ceiling);
  const overBudget = actual != null && committed != null && ceiling != null && actual + committed > ceiling;

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <span className="inline-flex h-7 w-7 items-center justify-center rounded-lg fin-tint-blue">
            <DollarSign className="h-4 w-4 text-fin-blue" />
          </span>
          <div>
            <CardTitle>{t("spendingPosition")}</CardTitle>
            <p className="text-xs text-muted-foreground">{t("spendingPositionDescription")}</p>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex h-4 overflow-visible rounded-full bg-muted/60" aria-label={t("spendingBarLabel")}>
          {actualPct != null && actualPct > 0 && <div className="h-4 shrink-0 rounded-l-full bg-fin-blue" style={{ width: `${Math.max(0, actualPct)}%` }} />}
          {committedPct != null && committedPct > 0 && <div className="h-4 shrink-0 bg-fin-amber" style={{ width: `${Math.max(0, committedPct)}%` }} />}
          {availablePct != null && availablePct > 0 && <div className="h-4 shrink-0 rounded-r-full bg-fin-green" style={{ width: `${Math.max(0, availablePct)}%` }} />}
        </div>
        <div className="grid gap-3 sm:grid-cols-3">
          <SpendingLegend label={t("actualCost")} value={formatMoney(actual, summary.currency, locale)} color="bg-fin-blue" />
          <SpendingLegend label={t("committedCost")} value={formatMoney(committed, summary.currency, locale)} color="bg-fin-amber" />
          <SpendingLegend label={t("availableBudget")} value={formatMoney(available, summary.currency, locale)} color="bg-fin-green" />
        </div>
        {overBudget && <div className="flex items-center gap-2 rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-xs font-medium text-destructive"><AlertTriangle className="h-4 w-4 shrink-0" />{t("spendingOverBudget")}</div>}
      </CardContent>
    </Card>
  );
}

function SpendingLegend({ label, value, color }: { label: string; value: string; color: string }) {
  return <div className="flex items-start gap-2 text-xs"><span className={`mt-0.5 h-2.5 w-2.5 shrink-0 rounded-full ${color}`} /><div><p className="text-muted-foreground">{label}</p><p className="mt-1 font-semibold tabular-nums">{value}</p></div></div>;
}

function ForecastPositionCard({ summary, t, locale }: { summary: CurrencyFinancialSummary; t: Translator; locale: string }) {
  const forecast = finiteValue(summary.forecastAtCompletion);
  const variance = finiteValue(summary.forecastVariance);
  const forecastPct = ratioPercent(forecast, summary.approvedBudget);
  const state = forecastState(summary, t);
  const forecastWidth = forecastPct == null ? 0 : Math.max(0, forecastPct);

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <span className="inline-flex h-7 w-7 items-center justify-center rounded-lg fin-tint-violet">
            <TrendingUp className="h-4 w-4 text-fin-violet" />
          </span>
          <div>
            <CardTitle>{t("forecastPosition")}</CardTitle>
            <p className="text-xs text-muted-foreground">{t("forecastPositionDescription")}</p>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <div className="flex items-end justify-between gap-3">
          <div>
            <p className="text-xs text-muted-foreground">{t("forecastAtCompletion")}</p>
            <div className="mt-1 text-3xl font-bold">{formatMoney(forecast, summary.currency, locale)}</div>
          </div>
          <Badge variant={state.variant}>{state.label}</Badge>
        </div>
        <div className="relative mt-5 h-3 overflow-visible rounded-full bg-muted/60" aria-label={t("forecastBarLabel")}>
          {forecastPct != null && <div className={`absolute inset-y-0 left-0 h-3 rounded-full ${variance != null && variance < 0 ? "bg-fin-red" : "bg-fin-violet"}`} style={{ width: `${forecastWidth}%` }} />}
          <div className="absolute inset-y-[-3px] left-full -translate-x-1/2 border-l-2 border-dashed border-muted-foreground/70" />
        </div>
        <div className="mt-4 flex items-center justify-between gap-3 rounded-lg bg-muted/30 p-3">
          <span className="text-xs text-muted-foreground">{t("forecastVariance")}</span>
          <span className={`text-sm font-semibold tabular-nums ${variance != null && variance < 0 ? "text-fin-red" : variance != null && variance > 0 ? "text-fin-green" : ""}`}>{formatMoney(variance == null ? null : Math.abs(variance), summary.currency, locale)} · {state.label}</span>
        </div>
      </CardContent>
    </Card>
  );
}

function ProjectFinancialTable({ projects, t, locale, activeLocale }: { projects: ProjectFinancialBreakdown[]; t: Translator; locale: string; activeLocale: string }) {
  const sorted = [...projects].sort((a, b) => (b.approvedBudget ?? 0) - (a.approvedBudget ?? 0));

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("projectFinancials")}</CardTitle>
        <p className="text-xs text-muted-foreground">{t("projectFinancialsDescription")}</p>
      </CardHeader>
      <CardContent>
        {sorted.length === 0 ? (
          <p className="py-8 text-center text-sm text-muted-foreground">{t("noData")}</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("project")}</TableHead>
                <TableHead className="text-end">{t("budgetCeiling")}</TableHead>
                <TableHead className="text-end">{t("allocatedBudget")}</TableHead>
                <TableHead className="text-end">{t("actualCost")}</TableHead>
                <TableHead className="text-end">{t("committedCost")}</TableHead>
                <TableHead className="text-end">{t("availableBudget")}</TableHead>
                <TableHead className="text-end">{t("forecastAtCompletion")}</TableHead>
                <TableHead className="text-end">{t("forecastVariance")}</TableHead>
                <TableHead>{t("financialStatusLabel")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {sorted.map((project) => {
                const attention = financialAttention(project, t);
                const forecast = forecastState(project, t);
                const projectName = project.projectName?.trim() || project.projectId;
                return (
                  <TableRow key={project.projectId}>
                    <TableCell className="min-w-48">
                      <Link href={`/${activeLocale}/projects/${encodeURIComponent(project.projectId)}?tab=financials`} className="group inline-flex max-w-full items-center gap-1 font-medium hover:text-primary">
                        <span className="truncate">{projectName}</span><ArrowUpRight className="h-3.5 w-3.5 shrink-0 opacity-60 transition-opacity group-hover:opacity-100" />
                      </Link>
                      <span className="mt-1 block text-[10px] text-muted-foreground">{project.projectId}</span>
                    </TableCell>
                    <TableCell className="text-end font-medium tabular-nums">{formatMoney(project.approvedBudget, project.currency, locale)}</TableCell>
                    <TableCell className="text-end tabular-nums text-fin-indigo">{formatMoney(project.allocatedBudget, project.currency, locale)}</TableCell>
                    <TableCell className="text-end tabular-nums text-fin-blue">{formatMoney(project.actualCost, project.currency, locale)}</TableCell>
                    <TableCell className="text-end tabular-nums text-fin-amber">{formatMoney(project.committedCost, project.currency, locale)}</TableCell>
                    <TableCell className={`text-end tabular-nums ${project.availableBudget < 0 ? "font-semibold text-fin-red" : "text-fin-green"}`}>{formatMoney(project.availableBudget, project.currency, locale)}</TableCell>
                    <TableCell className="text-end tabular-nums text-fin-violet">{formatMoney(project.forecastAtCompletion, project.currency, locale)}</TableCell>
                    <TableCell className={`text-end tabular-nums ${project.forecastVariance < 0 ? "font-semibold text-fin-red" : "font-semibold text-fin-green"}`}>
                      <span>{formatMoney(project.forecastVariance == null ? null : Math.abs(project.forecastVariance), project.currency, locale)}</span>
                      <span className="mt-1 block text-[10px] text-muted-foreground">{forecast.label}</span>
                    </TableCell>
                    <TableCell><Badge variant={attention.variant}>{attention.label}</Badge></TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
