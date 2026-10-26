import * as React from "react";
import { Link } from "react-router-dom";
import { AlertTriangle, ArrowUpRight, Banknote, DollarSign, RefreshCw, TrendingDown, TrendingUp, type LucideIcon } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Tabs, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useGlobalFinancialSummary, type CurrencyFinancialSummary, type ProjectFinancialBreakdown } from "./api/finance";

type FinancialBadgeVariant = "destructive" | "warning" | "success" | "secondary";
type FinancialState = { label: string; variant: FinancialBadgeVariant };
const EMPTY_CURRENCY_SUMMARIES: CurrencyFinancialSummary[] = [];

function finite(value: number | null | undefined): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function percent(value: number | null | undefined, base: number | null | undefined): number | null {
  const amount = finite(value);
  const denominator = finite(base);
  if (amount === null || denominator === null || denominator <= 0) return null;
  return (amount / denominator) * 100;
}

function formattedMoney(value: number | null | undefined, currency: string, formatCurrency: (amount: number, code?: string) => string): string {
  const amount = finite(value);
  const code = currency.trim();
  if (amount === null || !code) return "—";
  try {
    return formatCurrency(amount, code);
  } catch {
    return "—";
  }
}

function forecastState(variance: number | null | undefined, t: (key: string) => string): FinancialState {
  const value = finite(variance);
  if (value === null) return { label: t("costs.financialStatus.unavailable"), variant: "secondary" };
  if (value < 0) return { label: t("costs.forecastStatus.overBudget"), variant: "destructive" };
  if (value > 0) return { label: t("costs.forecastStatus.underBudget"), variant: "success" };
  return { label: t("costs.forecastStatus.onBudget"), variant: "secondary" };
}

function attentionState(project: ProjectFinancialBreakdown, t: (key: string) => string): FinancialState {
  const variance = finite(project.forecastVariance);
  const available = finite(project.availableBudget);
  if (variance === null && available === null) return { label: t("costs.financialStatus.unavailable"), variant: "secondary" };
  if (variance !== null && variance < 0) return { label: t("costs.financialStatus.overBudget"), variant: "destructive" };
  if (available !== null && available < 0) return { label: t("costs.financialStatus.overcommitted"), variant: "warning" };
  return { label: t("costs.financialStatus.withinBudget"), variant: "success" };
}

export function CostsOverviewPage() {
  const { t, formatCurrency } = useI18n();
  const query = useGlobalFinancialSummary();
  const [selectedCurrency, setSelectedCurrency] = React.useState("");
  const currencies = query.data?.byCurrency ?? EMPTY_CURRENCY_SUMMARIES;

  React.useEffect(() => {
    if (!currencies.some((summary) => summary.currency === selectedCurrency)) {
      setSelectedCurrency(currencies[0]?.currency ?? "");
    }
  }, [currencies, selectedCurrency]);

  const activeCurrency = currencies.find((summary) => summary.currency === selectedCurrency)?.currency ?? currencies[0]?.currency ?? "";
  const summary = currencies.find((item) => item.currency === activeCurrency);
  const projects = query.data?.byProject.filter((project) => project.currency === activeCurrency) ?? [];

  if (query.isLoading) {
    return <div className="space-y-6" aria-label={t("costs.loading")}>
      <PageHeader title={t("costs.title")} description={t("costs.description")} />
      <div className="grid gap-4 xl:grid-cols-2">{[0, 1].map((item) => <Card key={item}><CardHeader><div className="h-4 w-36 animate-pulse rounded bg-muted" /></CardHeader><CardContent><div className="h-14 w-48 animate-pulse rounded bg-muted" /></CardContent></Card>)}</div>
      <div className="grid gap-4 md:grid-cols-3">{[0, 1, 2].map((item) => <Card key={item}><CardContent className="p-6"><div className="h-20 animate-pulse rounded bg-muted" /></CardContent></Card>)}</div>
      <Card><CardContent className="p-6"><div className="h-64 animate-pulse rounded bg-muted" /></CardContent></Card>
    </div>;
  }

  if (query.isError) {
    return <div className="space-y-6">
      <PageHeader title={t("costs.title")} description={t("costs.description")} />
      <Card><CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
        <AlertTriangle className="h-10 w-10 text-destructive" />
        <h2 className="font-medium">{t("costs.loadFailed")}</h2>
        <Button variant="outline" onClick={() => void query.refetch()}><RefreshCw className="me-2 h-4 w-4" />{t("costs.retry")}</Button>
      </CardContent></Card>
    </div>;
  }

  if (!query.data || currencies.length === 0) {
    return <div className="space-y-6">
      <PageHeader title={t("costs.title")} description={t("costs.description")} />
      <Card><CardContent className="flex flex-col items-center justify-center gap-2 py-16 text-center">
        <Banknote className="h-10 w-10 text-muted-foreground" />
        <h2 className="font-medium">{t("costs.noData")}</h2>
      </CardContent></Card>
    </div>;
  }

  return <div className="space-y-6">
    <PageHeader title={t("costs.title")} description={t("costs.description")} />
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-muted/20 px-4 py-3">
      <div>
        <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{t("costs.currencyContext")}</p>
        <p className="mt-1 text-xs text-muted-foreground">{t("costs.currencyContextDescription")}</p>
      </div>
      {currencies.length > 1 ? <Tabs value={activeCurrency} onValueChange={setSelectedCurrency}>
        <TabsList>{currencies.map((item) => <TabsTrigger key={item.currency} value={item.currency}>{item.currency}</TabsTrigger>)}</TabsList>
      </Tabs> : <Badge variant="outline" className="px-3 py-1 text-xs">{activeCurrency || t("costs.financialStatus.unavailable")}</Badge>}
    </div>
    {!summary || projects.length === 0 ? <Card><CardContent className="flex flex-col items-center justify-center gap-2 py-16 text-center">
      <Banknote className="h-10 w-10 text-muted-foreground" /><h2 className="font-medium">{t("costs.noData")}</h2>
    </CardContent></Card> : <CurrencyDashboard summary={summary} projects={projects} t={t} formatCurrency={formatCurrency} />}
  </div>;
}

function CurrencyDashboard({ summary, projects, t, formatCurrency }: {
  summary: CurrencyFinancialSummary;
  projects: ProjectFinancialBreakdown[];
  t: (key: string, params?: Record<string, string | number>) => string;
  formatCurrency: (amount: number, currency?: string) => string;
}) {
  return <div className="space-y-6">
    <div className="grid gap-4 xl:grid-cols-2">
      <BudgetAuthorityCard summary={summary} t={t} formatCurrency={formatCurrency} />
      <ForecastPositionCard summary={summary} t={t} formatCurrency={formatCurrency} />
    </div>
    <div className="grid gap-4 md:grid-cols-3">
      <MetricCard title={t("costs.actualCost")} value={formattedMoney(summary.actualCost, summary.currency, formatCurrency)} icon={DollarSign} semantic="blue" />
      <MetricCard title={t("costs.committedCost")} value={formattedMoney(summary.committedCost, summary.currency, formatCurrency)} icon={Banknote} semantic="amber" />
      <MetricCard title={t("costs.availableBudget")} value={formattedMoney(summary.availableBudget, summary.currency, formatCurrency)} icon={summary.availableBudget < 0 ? TrendingDown : TrendingUp} valueClassName={summary.availableBudget < 0 ? "text-destructive" : undefined} semantic="green" />
    </div>
    <SpendingPositionCard summary={summary} t={t} formatCurrency={formatCurrency} />
    <ProjectFinancialTable projects={projects} t={t} formatCurrency={formatCurrency} />
  </div>;
}

function MetricCard({ title, value, icon: Icon, valueClassName, semantic }: {
  title: string; value: string; icon: LucideIcon; valueClassName?: string; semantic: "blue" | "amber" | "green";
}) {
  const semanticClasses = {
    blue: "fin-tint-blue text-fin-blue",
    amber: "fin-tint-amber text-fin-amber",
    green: "fin-tint-green text-fin-green",
  }[semantic];
  return <Card><CardHeader className="flex flex-row items-center justify-between pb-2">
    <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
    <span className={`inline-flex h-7 w-7 items-center justify-center rounded-lg ${semanticClasses.split(" ")[0]}`}><Icon className={`h-4 w-4 ${semanticClasses.split(" ")[1]}`} /></span>
  </CardHeader><CardContent><div className={`text-2xl font-bold tabular-nums ${valueClassName ?? ""}`}>{value}</div></CardContent></Card>;
}

function BudgetAuthorityCard({ summary, t, formatCurrency }: {
  summary: CurrencyFinancialSummary; t: (key: string, params?: Record<string, string | number>) => string; formatCurrency: (amount: number, currency?: string) => string;
}) {
  const allocatedPct = percent(summary.allocatedBudget, summary.approvedBudget);
  const unallocatedPct = percent(summary.unallocatedBudget, summary.approvedBudget);
  return <Card><CardHeader><div className="flex items-center gap-2">
    <span className="inline-flex h-7 w-7 items-center justify-center rounded-lg fin-tint-navy"><Banknote className="h-4 w-4 text-fin-navy" /></span>
    <div><CardTitle className="text-base">{t("costs.budgetAuthority")}</CardTitle><p className="text-xs text-muted-foreground">{t("costs.budgetAuthorityDescription")}</p></div>
  </div></CardHeader><CardContent className="space-y-4">
    <div className="flex items-end justify-between gap-3"><div><p className="text-xs text-muted-foreground">{t("costs.budgetCeiling")}</p><div className="mt-1 text-3xl font-bold tabular-nums">{formattedMoney(summary.approvedBudget, summary.currency, formatCurrency)}</div></div><span className="text-xs font-medium text-muted-foreground">{t("costs.projects", { count: summary.projectCount })}</span></div>
    <div className="space-y-2"><div className="flex h-3 overflow-hidden rounded-full bg-muted/60" aria-label={t("costs.allocationBarLabel")}>
      {allocatedPct !== null && allocatedPct > 0 ? <div className="h-3 shrink-0 bg-fin-indigo" style={{ width: `${Math.min(allocatedPct, 100)}%` }} /> : null}
      {unallocatedPct !== null && unallocatedPct > 0 ? <div className="h-3 shrink-0 bg-fin-slate/40" style={{ width: `${Math.min(unallocatedPct, 100)}%` }} /> : null}
    </div><div className="grid grid-cols-2 gap-3 text-xs">
      <Legend label={t("costs.allocatedBudget")} value={formattedMoney(summary.allocatedBudget, summary.currency, formatCurrency)} color="bg-fin-indigo" />
      <Legend label={t("costs.unallocatedBudget")} value={formattedMoney(summary.unallocatedBudget, summary.currency, formatCurrency)} color="bg-fin-slate/40" />
    </div></div>
  </CardContent></Card>;
}

function SpendingPositionCard({ summary, t, formatCurrency }: {
  summary: CurrencyFinancialSummary; t: (key: string) => string; formatCurrency: (amount: number, currency?: string) => string;
}) {
  const actualPct = percent(summary.actualCost, summary.approvedBudget);
  const committedPct = percent(summary.committedCost, summary.approvedBudget);
  const availablePct = percent(Math.max(0, summary.availableBudget), summary.approvedBudget);
  const overBudget = summary.actualCost + summary.committedCost > summary.approvedBudget;
  return <Card><CardHeader><div className="flex items-center gap-2">
    <span className="inline-flex h-7 w-7 items-center justify-center rounded-lg fin-tint-blue"><DollarSign className="h-4 w-4 text-fin-blue" /></span>
    <div><CardTitle className="text-base">{t("costs.spendingPosition")}</CardTitle><p className="text-xs text-muted-foreground">{t("costs.spendingPositionDescription")}</p></div>
  </div></CardHeader><CardContent className="space-y-4">
    <div className="flex h-4 overflow-hidden rounded-full bg-muted/60" aria-label={t("costs.spendingBarLabel")}>
      {actualPct !== null && actualPct > 0 ? <div className="h-4 shrink-0 bg-fin-blue" style={{ width: `${Math.min(actualPct, 100)}%` }} /> : null}
      {committedPct !== null && committedPct > 0 ? <div className="h-4 shrink-0 bg-fin-amber" style={{ width: `${Math.min(committedPct, 100)}%` }} /> : null}
      {availablePct !== null && availablePct > 0 ? <div className="h-4 shrink-0 bg-fin-green" style={{ width: `${Math.min(availablePct, 100)}%` }} /> : null}
    </div><div className="grid gap-3 sm:grid-cols-3">
      <Legend label={t("costs.actualCost")} value={formattedMoney(summary.actualCost, summary.currency, formatCurrency)} color="bg-fin-blue" />
      <Legend label={t("costs.committedCost")} value={formattedMoney(summary.committedCost, summary.currency, formatCurrency)} color="bg-fin-amber" />
      <Legend label={t("costs.availableBudget")} value={formattedMoney(summary.availableBudget, summary.currency, formatCurrency)} color="bg-fin-green" />
    </div>{overBudget ? <div className="flex items-center gap-2 rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-xs font-medium text-destructive"><AlertTriangle className="h-4 w-4 shrink-0" />{t("costs.spendingOverBudget")}</div> : null}
  </CardContent></Card>;
}

function ForecastPositionCard({ summary, t, formatCurrency }: {
  summary: CurrencyFinancialSummary; t: (key: string, params?: Record<string, string | number>) => string; formatCurrency: (amount: number, currency?: string) => string;
}) {
  const state = forecastState(summary.forecastVariance, t);
  const forecastPct = percent(summary.forecastAtCompletion, summary.approvedBudget);
  return <Card><CardHeader><div className="flex items-center gap-2">
    <span className="inline-flex h-7 w-7 items-center justify-center rounded-lg fin-tint-violet"><TrendingUp className="h-4 w-4 text-fin-violet" /></span>
    <div><CardTitle className="text-base">{t("costs.forecastPosition")}</CardTitle><p className="text-xs text-muted-foreground">{t("costs.forecastPositionDescription")}</p></div>
  </div></CardHeader><CardContent>
    <div className="flex items-end justify-between gap-3"><div><p className="text-xs text-muted-foreground">{t("costs.forecastAtCompletion")}</p><div className="mt-1 text-3xl font-bold tabular-nums">{formattedMoney(summary.forecastAtCompletion, summary.currency, formatCurrency)}</div></div><Badge variant={state.variant}>{state.label}</Badge></div>
    <div className="relative mt-5 h-3 overflow-hidden rounded-full bg-muted/60" aria-label={t("costs.forecastBarLabel")}>
      {forecastPct !== null ? <div className={`absolute inset-y-0 start-0 h-3 rounded-full ${summary.forecastVariance < 0 ? "bg-fin-red" : "bg-fin-violet"}`} style={{ width: `${Math.min(Math.max(0, forecastPct), 100)}%` }} /> : null}
      <div className="absolute inset-y-[-3px] start-full -translate-x-1/2 border-s-2 border-dashed border-muted-foreground/70" />
    </div>
    <div className="mt-4 flex items-center justify-between gap-3 rounded-lg bg-muted/30 p-3"><span className="text-xs text-muted-foreground">{t("costs.forecastVariance")}</span>
      <span className={`text-sm font-semibold tabular-nums ${summary.forecastVariance < 0 ? "text-fin-red" : summary.forecastVariance > 0 ? "text-fin-green" : ""}`}>{formattedMoney(Math.abs(summary.forecastVariance), summary.currency, formatCurrency)} · {state.label}</span>
    </div>
  </CardContent></Card>;
}

function Legend({ label, value, color }: { label: string; value: string; color: string }) {
  return <div className="flex items-start gap-2 text-xs"><span className={`mt-0.5 h-2.5 w-2.5 shrink-0 rounded-full ${color}`} /><div><p className="text-muted-foreground">{label}</p><p className="mt-1 font-semibold tabular-nums">{value}</p></div></div>;
}

function ProjectFinancialTable({ projects, t, formatCurrency }: {
  projects: ProjectFinancialBreakdown[]; t: (key: string) => string; formatCurrency: (amount: number, currency?: string) => string;
}) {
  const sorted = [...projects].sort((left, right) => right.approvedBudget - left.approvedBudget);
  return <Card><CardHeader><CardTitle className="text-base">{t("costs.projectFinancials")}</CardTitle><p className="text-xs text-muted-foreground">{t("costs.projectFinancialsDescription")}</p></CardHeader>
    <CardContent className="p-0"><Table className="min-w-[1100px]"><TableHeader><TableRow>
      <TableHead>{t("costs.project")}</TableHead><TableHead className="text-end">{t("costs.budgetCeiling")}</TableHead><TableHead className="text-end">{t("costs.allocatedBudget")}</TableHead><TableHead className="text-end">{t("costs.actualCost")}</TableHead><TableHead className="text-end">{t("costs.committedCost")}</TableHead><TableHead className="text-end">{t("costs.availableBudget")}</TableHead><TableHead className="text-end">{t("costs.forecastAtCompletion")}</TableHead><TableHead className="text-end">{t("costs.forecastVariance")}</TableHead><TableHead>{t("costs.financialStatusLabel")}</TableHead>
    </TableRow></TableHeader><TableBody>
      {sorted.map((project) => {
        const attention = attentionState(project, t);
        const forecast = forecastState(project.forecastVariance, t);
        const projectName = project.projectName?.trim() || project.projectId;
        return <TableRow key={project.projectId}>
          <TableCell className="min-w-48">{project.projectPublicId ? <Link to={`/projects/${encodeURIComponent(project.projectPublicId)}?tab=financials`} className="group inline-flex max-w-full items-center gap-1 font-medium hover:text-primary"><span className="truncate">{projectName}</span><ArrowUpRight className="h-3.5 w-3.5 shrink-0 opacity-60 transition-opacity group-hover:opacity-100" /></Link> : <span className="font-medium">{projectName}</span>}</TableCell>
          <TableCell className="text-end font-medium tabular-nums">{formattedMoney(project.approvedBudget, project.currency, formatCurrency)}</TableCell>
          <TableCell className="text-end tabular-nums text-fin-indigo">{formattedMoney(project.allocatedBudget, project.currency, formatCurrency)}</TableCell>
          <TableCell className="text-end tabular-nums text-fin-blue">{formattedMoney(project.actualCost, project.currency, formatCurrency)}</TableCell>
          <TableCell className="text-end tabular-nums text-fin-amber">{formattedMoney(project.committedCost, project.currency, formatCurrency)}</TableCell>
          <TableCell className={`text-end tabular-nums ${project.availableBudget < 0 ? "font-semibold text-fin-red" : "text-fin-green"}`}>{formattedMoney(project.availableBudget, project.currency, formatCurrency)}</TableCell>
          <TableCell className="text-end tabular-nums text-fin-violet">{formattedMoney(project.forecastAtCompletion, project.currency, formatCurrency)}</TableCell>
          <TableCell className={`text-end tabular-nums ${project.forecastVariance < 0 ? "font-semibold text-fin-red" : "font-semibold text-fin-green"}`}><span>{formattedMoney(Math.abs(project.forecastVariance), project.currency, formatCurrency)}</span><span className="mt-1 block text-[10px] text-muted-foreground">{forecast.label}</span></TableCell>
          <TableCell><Badge variant={attention.variant}>{attention.label}</Badge></TableCell>
        </TableRow>;
      })}
    </TableBody></Table></CardContent>
  </Card>;
}
