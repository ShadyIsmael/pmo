import * as React from "react";
import { useOutletContext } from "react-router-dom";
import { AlertTriangle, Archive, Ban, Pencil, Plus, Trash2, TrendingDown } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Badge } from "@components/ui/Badge";
import { Pagination } from "@components/ui/Pagination";
import { useI18n } from "@i18n/messages";
import { Progress } from "@components/ui/Progress";
import { Textarea } from "@components/ui/Textarea";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { useAuth } from "@auth/AuthProvider";
import { hasPermission, Permissions } from "@lib/permissions";
import { useManagement } from "@api/management";
import { useProjectOverview } from "./api/projects";
import { BudgetRevisionSection } from "./components/BudgetRevisionSection";
import {
  useProjectBudgets,
  useProjectCosts,
  useProjectCommitments,
  useBudgets,
  useCosts,
  useCommitments,
  useCostManagementMutations,
  type BudgetDto,
  type CostEntryDto,
  type PurchaseCommitmentDto,
} from "@features/finance/api/finance";

type ProjectContext = { projectId: string; project: { id: string; currency?: string } };
type BudgetForm = { name: string; description: string; category: string; plannedAmount: string; approvedAmount: string; forecastAmount: string; fiscalYear: string; fiscalQuarter: string; status: string; startDate?: string; endDate?: string };
type CostForm = { budgetId?: string; subCategory?: string; category: string; description: string; amount: string; taxAmount: string; incurredDate: string; type: string; reference: string; vendorName: string; status: string };
type CommitmentForm = { budgetId?: string; purchaseOrderNumber: string; description: string; vendorName: string; amount: string; commitmentDate: string; expectedDeliveryDate: string };

const today = () => new Date().toISOString().slice(0, 10);
const emptyBudget = (): BudgetForm => ({ name: "", description: "", category: "", plannedAmount: "", approvedAmount: "", forecastAmount: "", fiscalYear: String(new Date().getFullYear()), fiscalQuarter: "", status: "Draft" });
const emptyCost = (): CostForm => ({ category: "", description: "", amount: "", taxAmount: "", incurredDate: today(), type: "Actual", reference: "", vendorName: "", status: "Posted" });
const emptyCommitment = (): CommitmentForm => ({ purchaseOrderNumber: "", description: "", vendorName: "", amount: "", commitmentDate: today(), expectedDeliveryDate: "" });

function money(value: number | null | undefined, currency: string, formatCurrency: (value: number, currency?: string) => string) {
  return typeof value === "number" && Number.isFinite(value) ? formatCurrency(value, currency) : "—";
}

function statusLabel(t: (key: string) => string, namespace: "budgetStatuses" | "costStatuses" | "commitmentStatuses", value?: string | null) {
  const key = (value || "").trim().toLowerCase();
  if (!key) return t("projects.commitmentStatusUnknown");
  const label = t(`projects.${namespace}.${key}`);
  return label === `projects.${namespace}.${key}` ? value! : label;
}

function fiscalPeriod(t: (key: string) => string, year?: number | null, quarter?: number | null) {
  if (year == null) return "—";
  return quarter ? `${t("projects.fiscalYearShort")} ${year} · ${t("projects.quarterShort")}${quarter}` : `${t("projects.fiscalYearShort")} ${year}`;
}

function budgetVariance(item: BudgetDto) { return finite(item.approvedAmount) && finite(item.forecastAmount) ? item.approvedAmount - item.forecastAmount : null; }
function budgetVarianceLabel(t: (key: string) => string, value: number) { return t(value > 0 ? "projects.underBudget" : value < 0 ? "projects.overBudget" : "projects.onBudget"); }

function dateOnly(value?: string) { return value ? value.slice(0, 10) : "—"; }
function finite(value: unknown): value is number { return typeof value === "number" && Number.isFinite(value); }
function percentage(value?: number | null) { return finite(value) ? `${value.toFixed(1)}%` : "—"; }

function SummaryCard({ label, value, hint, tone, metricKey }: { label: string; value: string; hint?: string; tone?: string; metricKey?: string }) {
  return <Card><CardContent className="p-4"><p className="text-xs font-medium text-muted-foreground">{label}</p><p data-testid={metricKey ? `finance-metric-${metricKey}` : undefined} className={`mt-1 text-xl font-semibold tabular-nums ${tone ?? ""}`}>{value}</p>{hint ? <p className="mt-1 text-xs text-muted-foreground">{hint}</p> : null}</CardContent></Card>;
}

function FinancialTableState({ loading, error, colSpan, onRetry, t }: { loading: boolean; error: boolean; colSpan: number; onRetry: () => void; t: (key: string) => string }) {
  if (loading) return <TableRow><TableCell colSpan={colSpan} className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</TableCell></TableRow>;
  if (error) return <TableRow><TableCell colSpan={colSpan} className="p-6 text-center"><div className="flex flex-col items-center gap-2"><p role="alert" className="text-sm text-destructive">{t("common.errorLoading")}</p><Button size="sm" variant="outline" onClick={onRetry}>{t("common.retry")}</Button></div></TableCell></TableRow>;
  return null;
}

export function ProjectFinanceTab() {
  const { projectId, project } = useOutletContext<ProjectContext>();
  const { t, formatCurrency } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Finances.Read));
  const canManage = Boolean(canView && user && hasPermission(user.permissions, Permissions.Finances.Write) && canMutate && !effectiveReadOnly);
  const queriesEnabled = canView && Boolean(projectId);
  const overview = useProjectOverview(projectId, queriesEnabled);
  const budgets = useProjectBudgets(projectId, queriesEnabled, { page: 1, pageSize: 5 });
  const costs = useProjectCosts(projectId, queriesEnabled, { page: 1, pageSize: 5 });
  const commitments = useProjectCommitments(projectId, queriesEnabled, { page: 1, pageSize: 5 });
  const mutations = useCostManagementMutations(projectId);
  const currency = overview.data?.currency || project.currency || "GBP";

  const [budgetOpen, setBudgetOpen] = React.useState(false);
  const [costOpen, setCostOpen] = React.useState(false);
  const [commitmentOpen, setCommitmentOpen] = React.useState(false);
  const [budgetListOpen, setBudgetListOpen] = React.useState(false);
  const [costListOpen, setCostListOpen] = React.useState(false);
  const [commitmentListOpen, setCommitmentListOpen] = React.useState(false);
  const [budgetPage, setBudgetPage] = React.useState(1);
  const [costPage, setCostPage] = React.useState(1);
  const [commitmentPage, setCommitmentPage] = React.useState(1);
  const [editingBudget, setEditingBudget] = React.useState<BudgetDto | null>(null);
  const [editingCost, setEditingCost] = React.useState<CostEntryDto | null>(null);
  const [editingCommitment, setEditingCommitment] = React.useState<PurchaseCommitmentDto | null>(null);
  const [budgetForm, setBudgetForm] = React.useState<BudgetForm>(emptyBudget);
  const [costForm, setCostForm] = React.useState<CostForm>(emptyCost);
  const [commitmentForm, setCommitmentForm] = React.useState<CommitmentForm>(emptyCommitment);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [budgetFormError, setBudgetFormError] = React.useState<string | null>(null);
  const [costFormError, setCostFormError] = React.useState<string | null>(null);
  const [commitmentFormError, setCommitmentFormError] = React.useState<string | null>(null);
  const budgetList = useBudgets({ projectId, page: budgetPage, pageSize: 20 }, budgetListOpen && queriesEnabled);
  const costList = useCosts({ projectId, page: costPage, pageSize: 20 }, costListOpen && queriesEnabled);
  const commitmentList = useCommitments({ projectId, page: commitmentPage, pageSize: 20 }, commitmentListOpen && queriesEnabled);

  const openBudget = (item?: BudgetDto) => {
    setEditingBudget(item ?? null);
    setBudgetForm(item ? {
      name: item.name, description: item.description ?? "", category: item.category,
      plannedAmount: String(item.plannedAmount), approvedAmount: String(item.approvedAmount), forecastAmount: String(item.forecastAmount),
      fiscalYear: String(item.fiscalYear), fiscalQuarter: item.fiscalQuarter == null ? "" : String(item.fiscalQuarter), status: item.status,
      startDate: item.startDate?.slice(0, 10), endDate: item.endDate?.slice(0, 10),
    } : emptyBudget());
    setActionError(null); setBudgetFormError(null); setBudgetOpen(true);
  };

  const openCost = (item?: CostEntryDto) => {
    setEditingCost(item ?? null);
    setCostForm(item ? {
      budgetId: item.budgetId, subCategory: item.subCategory, category: item.category, description: item.description ?? "", amount: String(item.amount), taxAmount: item.taxAmount == null ? "" : String(item.taxAmount),
      incurredDate: dateOnly(item.incurredDate), type: item.type, reference: item.reference ?? "", vendorName: item.vendorName ?? "", status: item.status,
    } : emptyCost());
    setActionError(null); setCostFormError(null); setCostOpen(true);
  };

  const openCommitment = (item?: PurchaseCommitmentDto) => {
    setEditingCommitment(item ?? null);
    setCommitmentForm(item ? {
      budgetId: item.budgetId, purchaseOrderNumber: item.purchaseOrderNumber, description: item.description ?? "", vendorName: item.vendorName ?? "", amount: String(item.amount),
      commitmentDate: dateOnly(item.commitmentDate), expectedDeliveryDate: item.expectedDeliveryDate ? dateOnly(item.expectedDeliveryDate) : "",
    } : emptyCommitment());
    setActionError(null); setCommitmentFormError(null); setCommitmentOpen(true);
  };

  const submitBudget = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setBudgetFormError(null);
    const amount = Number(budgetForm.plannedAmount);
    const amounts = [budgetForm.plannedAmount, budgetForm.approvedAmount, budgetForm.forecastAmount].map(Number);
    const fiscalYear = Number(budgetForm.fiscalYear);
    const fiscalQuarter = budgetForm.fiscalQuarter ? Number(budgetForm.fiscalQuarter) : undefined;
    if (!budgetForm.name.trim()) { setBudgetFormError(t("projects.budgetLineNameRequired")); return; }
    if (!budgetForm.category.trim()) { setBudgetFormError(t("projects.budgetLineCategoryRequired")); return; }
    if (!Number.isFinite(amount) || amounts.some((value) => !Number.isFinite(value) || value < 0)) { setBudgetFormError(t("projects.budgetLineAmountsInvalid")); return; }
    if (!Number.isInteger(fiscalYear) || fiscalYear < 1900 || fiscalYear > 2200) { setBudgetFormError(t("projects.budgetLineFiscalYearInvalid")); return; }
    if (fiscalQuarter !== undefined && (!Number.isInteger(fiscalQuarter) || fiscalQuarter < 1 || fiscalQuarter > 4)) { setBudgetFormError(t("projects.budgetLineFiscalQuarterInvalid")); return; }
    const body = {
      projectId: project.id, name: budgetForm.name.trim(), description: budgetForm.description.trim() || undefined, category: budgetForm.category.trim(),
      plannedAmount: amount, approvedAmount: Number(budgetForm.approvedAmount), forecastAmount: Number(budgetForm.forecastAmount),
      currency, fiscalYear, fiscalQuarter, status: budgetForm.status, startDate: budgetForm.startDate || undefined, endDate: budgetForm.endDate || undefined,
    };
    try {
      if (editingBudget) await mutations.updateBudget.mutateAsync({ id: editingBudget.id, body: { ...body, rowVersion: editingBudget.rowVersion } });
      else await mutations.createBudget.mutateAsync(body);
      setBudgetOpen(false);
    } catch (error) { setBudgetFormError(error instanceof Error ? error.message : t("projects.budgetLineSaveFailed")); }
  };

  const submitCost = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setCostFormError(null);
    const amount = Number(costForm.amount);
    const taxAmount = costForm.taxAmount === "" ? undefined : Number(costForm.taxAmount);
    if (!costForm.category.trim() || !Number.isFinite(amount) || amount < 0 || (taxAmount !== undefined && (!Number.isFinite(taxAmount) || taxAmount < 0)) || !costForm.incurredDate) { setCostFormError(t("projects.budgetLineAmountsInvalid")); return; }
    const body = {
      projectId: project.id, budgetId: costForm.budgetId, subCategory: costForm.subCategory, category: costForm.category.trim(), description: costForm.description.trim() || undefined, amount, currency,
      taxAmount, incurredDate: costForm.incurredDate, type: costForm.type.trim() || "Actual",
      reference: costForm.reference.trim() || undefined, vendorName: costForm.vendorName.trim() || undefined, status: costForm.status,
    };
    try {
      if (editingCost) await mutations.updateCost.mutateAsync({ id: editingCost.id, body: { ...body, rowVersion: editingCost.rowVersion } });
      else await mutations.createCost.mutateAsync(body);
      setCostOpen(false);
    } catch (error) { setCostFormError(error instanceof Error ? error.message : t("projects.costEntrySaveFailed")); }
  };

  const submitCommitment = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setCommitmentFormError(null);
    const amount = Number(commitmentForm.amount);
    if (!commitmentForm.purchaseOrderNumber.trim() || !Number.isFinite(amount) || amount <= 0 || !commitmentForm.commitmentDate) { setCommitmentFormError(t("projects.commitmentReferenceRequired")); return; }
    const base = {
      budgetId: commitmentForm.budgetId, purchaseOrderNumber: commitmentForm.purchaseOrderNumber.trim(), description: commitmentForm.description.trim() || undefined, vendorName: commitmentForm.vendorName.trim() || undefined,
      amount, commitmentDate: commitmentForm.commitmentDate, expectedDeliveryDate: commitmentForm.expectedDeliveryDate || undefined,
    };
    try {
      if (editingCommitment) await mutations.updateCommitment.mutateAsync({ id: editingCommitment.id, body: { ...base, rowVersion: editingCommitment.rowVersion } });
      else await mutations.createCommitment.mutateAsync({ ...base, projectId: project.id });
      setCommitmentOpen(false);
    } catch (error) { setCommitmentFormError(error instanceof Error ? error.message : t("projects.commitmentSaveFailed")); }
  };

  const deleteBudget = async (item: BudgetDto) => {
    if (!await confirm({ description: t("projects.deleteBudgetLineConfirm", { name: item.name }), destructive: true })) return;
    try { await mutations.deleteBudget.mutateAsync(item.id); } catch (error) { setActionError(error instanceof Error ? error.message : t("projects.budgetLineDeleteFailed")); }
  };
  const deleteCost = async (item: CostEntryDto) => {
    if (!await confirm({ description: t("projects.deleteCostEntryConfirm", { amount: money(item.amount, item.currency, formatCurrency) }), destructive: true })) return;
    try { await mutations.deleteCost.mutateAsync(item.id); } catch (error) { setActionError(error instanceof Error ? error.message : t("projects.costEntryDeleteFailed")); }
  };
  const changeCommitmentStatus = async (item: PurchaseCommitmentDto, action: "close" | "cancel") => {
    const message = action === "close" ? t("projects.closeCommitmentConfirm", { reference: item.purchaseOrderNumber }) : t("projects.cancelCommitmentConfirm", { reference: item.purchaseOrderNumber });
    if (!await confirm({ description: message, destructive: action === "cancel" })) return;
    try {
      if (action === "close") await mutations.closeCommitment.mutateAsync({ id: item.id, rowVersion: item.rowVersion });
      else await mutations.cancelCommitment.mutateAsync({ id: item.id, rowVersion: item.rowVersion });
    } catch (error) { setActionError(error instanceof Error ? error.message : action === "close" ? t("projects.commitmentCloseFailed") : t("projects.commitmentCancelFailed")); }
  };

  if (!canView) return <Card><CardContent className="p-6 text-sm text-muted-foreground">{t("projects.financialsPermissionRequired")}</CardContent></Card>;
  if (overview.isLoading) return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
  if (overview.isError) return <Card><CardContent className="p-6 text-center text-sm text-destructive">{t("projects.financialsLoadFailed")}</CardContent></Card>;

  const summary = overview.data;
  const ceiling = finite(summary?.approvedBudget) ? summary.approvedBudget : null;
  const allocated = finite(summary?.allocatedBudget) ? summary.allocatedBudget : null;
  const actual = finite(summary?.actualCost) ? summary.actualCost : null;
  const committed = finite(summary?.committedCost) ? summary.committedCost : null;
  const forecast = finite(summary?.forecastAtCompletion) ? summary.forecastAtCompletion : null;
  const unallocated = finite(summary?.unallocatedBudget) ? summary.unallocatedBudget : finite(ceiling) && finite(allocated) ? ceiling - allocated : null;
  const available = finite(ceiling) && finite(actual) && finite(committed) ? ceiling - actual - committed : null;
  const forecastVariance = finite(summary?.forecastVariance) ? summary.forecastVariance : null;
  const utilization = summary?.budgetUtilizationPercentage;
  const allocationPct = finite(ceiling) && ceiling > 0 && finite(allocated) ? (allocated / ceiling) * 100 : 0;
  const forecastWarning = finite(forecast) && finite(ceiling)
    ? forecast > ceiling ? t("projects.forecastExceedsCeiling")
      : finite(available) && forecast > available ? t("projects.insufficientAvailable")
        : null
    : null;
  const spendingChartData = [{
    name: t("projects.spendingPosition"),
    actual: finite(actual) ? actual : 0,
    committed: finite(committed) ? committed : 0,
    available: finite(available) ? Math.max(available, 0) : 0,
  }];
  const busy = mutations.createBudget.isPending || mutations.updateBudget.isPending || mutations.createCost.isPending || mutations.updateCost.isPending || mutations.createCommitment.isPending || mutations.updateCommitment.isPending;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="text-base font-semibold">{t("projects.financialSummary")}</h2><p className="mt-1 text-sm text-muted-foreground">{t("projects.financialSummaryDescription")}</p></div>{canManage ? <div className="flex flex-wrap gap-2"><Button size="sm" variant="outline" onClick={() => openBudget()}><Plus className="me-1.5 h-4 w-4" />{t("projects.addBudgetLine")}</Button><Button size="sm" variant="outline" onClick={() => openCost()}><Plus className="me-1.5 h-4 w-4" />{t("projects.addCostEntry")}</Button><Button size="sm" onClick={() => openCommitment()}><Plus className="me-1.5 h-4 w-4" />{t("projects.addCommitment")}</Button></div> : null}</div>
      {actionError ? <p className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{actionError}</p> : null}
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3"><SummaryCard metricKey="budget-ceiling" label={t("projects.budgetCeiling")} value={money(ceiling, currency, formatCurrency)} hint={t("projects.budgetCeilingHint")} /><SummaryCard metricKey="allocated" label={t("projects.allocatedBudget")} value={money(allocated, currency, formatCurrency)} hint={t("projects.allocatedBudgetHint")} /><SummaryCard metricKey="unallocated" label={t("projects.unallocatedBudget")} value={money(unallocated, currency, formatCurrency)} tone={finite(unallocated) && unallocated <= 0 ? "text-warning" : undefined} /></div>
      <div className="space-y-2"><div className="flex items-center justify-between gap-3 text-xs text-muted-foreground"><span>{t("projects.allocationProgress")}</span><span className="tabular-nums">{percentage(allocationPct)} · {money(allocated, currency, formatCurrency)} / {money(ceiling, currency, formatCurrency)}</span></div><Progress value={Math.min(allocationPct, 100)} className="h-2" /></div>
      <div className="grid gap-3 lg:grid-cols-2">
        <Card><CardHeader className="pb-3"><CardTitle className="text-xs uppercase tracking-wide text-muted-foreground">{t("projects.spendingPosition")}</CardTitle><p className="text-xs text-muted-foreground">{t("projects.spendingPositionDescription")}</p></CardHeader><CardContent className="space-y-3">
          <div className="h-36 min-w-0" role="img" aria-label={t("projects.spendingPosition")}><ResponsiveContainer width="100%" height="100%"><BarChart data={spendingChartData} layout="vertical" margin={{ top: 0, right: 8, left: 0, bottom: 0 }}><CartesianGrid strokeDasharray="3 3" horizontal={false} /><XAxis type="number" /><YAxis type="category" dataKey="name" hide /><Tooltip formatter={(value) => money(Number(value), currency, formatCurrency)} /><Bar dataKey="actual" name={t("projects.actualSpend")} stackId="spend" fill="hsl(var(--fin-blue))" /><Bar dataKey="committed" name={t("projects.committedSpend")} stackId="spend" fill="hsl(var(--fin-amber))" /><Bar dataKey="available" name={t("projects.availableBudget")} stackId="spend" fill="hsl(var(--fin-green))" /></BarChart></ResponsiveContainer></div>
          <div className="space-y-1.5 text-xs">{[{ key: "actualSpend", value: actual, color: "bg-fin-blue" }, { key: "committedSpend", value: committed, color: "bg-fin-amber" }, { key: "availableBudget", value: available, color: "bg-fin-green" }].map(({ key, value, color }) => <div key={key} className="flex items-center justify-between gap-3"><span className="flex items-center gap-2 text-muted-foreground"><span aria-hidden="true" className={`h-2 w-2 rounded-full ${color}`} />{t(`projects.${key}`)}</span><span data-testid={`finance-spending-${key}`} className="font-medium tabular-nums">{money(value, currency, formatCurrency)}</span></div>)}</div>
        </CardContent></Card>
        <Card><CardHeader className="pb-3"><CardTitle className="text-xs uppercase tracking-wide text-muted-foreground">{t("projects.forecastSection")}</CardTitle><p className="text-xs text-muted-foreground">{t("projects.forecastSectionDescription")}</p></CardHeader><CardContent className="space-y-3">
          <div className="grid grid-cols-2 gap-3"><SummaryCard metricKey="forecast" label={t("projects.estimateAtCompletion")} value={money(forecast, currency, formatCurrency)} /><SummaryCard label={t("projects.forecastVarianceLabel")} value={money(forecastVariance, currency, formatCurrency)} tone={finite(forecastVariance) ? forecastVariance < 0 ? "text-destructive" : "text-success" : undefined} /></div>
          <div className="grid grid-cols-2 gap-3"><SummaryCard label={t("projects.budgetUtilization")} value={percentage(utilization)} /><SummaryCard label={t("projects.originalApprovedBudget")} value={money(summary?.originalApprovedBudget, currency, formatCurrency)} /></div>
          {forecastWarning ? <p role="status" className="flex items-start gap-2 text-xs text-warning"><AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" /><span>{forecastWarning}</span></p> : finite(forecast) && finite(ceiling) && forecast <= ceiling ? <p className="flex items-start gap-2 text-xs text-success"><TrendingDown className="mt-0.5 h-3.5 w-3.5 shrink-0" /><span>{t("projects.forecastWithinBudget")}</span></p> : null}
        </CardContent></Card>
      </div>

      <Card><CardHeader className="flex flex-row items-center justify-between gap-2"><CardTitle>{t("projects.budgetLines")}</CardTitle>{(budgets.data?.totalCount ?? 0) > 5 ? <Button size="sm" variant="outline" onClick={() => { setBudgetPage(1); setBudgetListOpen(true); }}>{t("projects.viewAllCount", { count: budgets.data?.totalCount ?? 0 })}</Button> : null}</CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("projects.budgetLine")}</TableHead><TableHead>{t("projects.category")}</TableHead><TableHead>{t("projects.fiscalPeriod")}</TableHead><TableHead>{t("projects.plannedAmount")}</TableHead><TableHead>{t("projects.approvedAmount")}</TableHead><TableHead>{t("projects.forecastAmount")}</TableHead><TableHead>{t("common.status")}</TableHead>{canManage ? <TableHead>{t("projects.actions")}</TableHead> : null}</TableRow></TableHeader><TableBody>{budgets.isLoading || budgets.isError ? <FinancialTableState loading={budgets.isLoading} error={budgets.isError} colSpan={canManage ? 8 : 7} onRetry={() => void budgets.refetch()} t={t} /> : (budgets.data?.items ?? []).length === 0 ? <TableRow><TableCell colSpan={canManage ? 8 : 7} className="p-6 text-center text-sm text-muted-foreground">{t("projects.noBudgetLines")}</TableCell></TableRow> : (budgets.data?.items ?? []).map((item) => { const variance = budgetVariance(item); return <TableRow key={item.id}><TableCell className="font-medium">{item.name}</TableCell><TableCell>{item.category}</TableCell><TableCell className="whitespace-nowrap text-xs text-muted-foreground">{fiscalPeriod(t, item.fiscalYear, item.fiscalQuarter)}</TableCell><TableCell className="tabular-nums">{money(item.plannedAmount, item.currency, formatCurrency)}</TableCell><TableCell className="tabular-nums">{money(item.approvedAmount, item.currency, formatCurrency)}</TableCell><TableCell className="tabular-nums">{money(item.forecastAmount, item.currency, formatCurrency)}{variance !== null ? <span className="mt-0.5 block text-[10px] text-muted-foreground">{t("projects.approvedMinusForecast")}: {money(variance, item.currency, formatCurrency)} · {budgetVarianceLabel(t, variance)}</span> : null}</TableCell><TableCell><Badge status={item.status}>{statusLabel(t, "budgetStatuses", item.status)}</Badge></TableCell>{canManage ? <TableCell><div className="flex gap-1"><Button size="sm" variant="ghost" aria-label={t("projects.editBudgetLine")} onClick={() => openBudget(item)}><Pencil className="h-4 w-4" /></Button><Button size="sm" variant="ghost" aria-label={t("projects.deleteBudgetLine")} onClick={() => void deleteBudget(item)}><Trash2 className="h-4 w-4 text-destructive" /></Button></div></TableCell> : null}</TableRow>; })}</TableBody></Table></CardContent></Card>
      <BudgetRevisionSection projectId={projectId} currency={currency} originalApprovedBudget={summary?.originalApprovedBudget} currentApprovedBudget={ceiling} enabled={Boolean(projectId)} canManage={canManage} /><Card><CardHeader className="flex flex-row items-center justify-between gap-2"><CardTitle>{t("projects.actualCosts")}</CardTitle>{(costs.data?.totalCount ?? 0) > 5 ? <Button size="sm" variant="outline" onClick={() => { setCostPage(1); setCostListOpen(true); }}>{t("projects.viewAllCount", { count: costs.data?.totalCount ?? 0 })}</Button> : null}</CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("projects.description")}</TableHead><TableHead>{t("projects.category")}</TableHead><TableHead>{t("projects.amount")}</TableHead><TableHead>{t("projects.incurredDate")}</TableHead><TableHead>{t("projects.vendorName")}</TableHead><TableHead>{t("common.status")}</TableHead>{canManage ? <TableHead>{t("projects.actions")}</TableHead> : null}</TableRow></TableHeader><TableBody>{costs.isLoading || costs.isError ? <FinancialTableState loading={costs.isLoading} error={costs.isError} colSpan={canManage ? 7 : 6} onRetry={() => void costs.refetch()} t={t} /> : (costs.data?.items ?? []).length === 0 ? <TableRow><TableCell colSpan={canManage ? 7 : 6} className="p-6 text-center text-sm text-muted-foreground">{t("projects.noCostEntries")}</TableCell></TableRow> : (costs.data?.items ?? []).map((item) => <TableRow key={item.id}><TableCell className="font-medium">{item.description || "—"}</TableCell><TableCell>{item.category}</TableCell><TableCell className="tabular-nums">{money(item.amount, item.currency, formatCurrency)}</TableCell><TableCell className="whitespace-nowrap">{dateOnly(item.incurredDate)}</TableCell><TableCell>{item.vendorName || "—"}</TableCell><TableCell><Badge status={item.status}>{statusLabel(t, "costStatuses", item.status)}</Badge></TableCell>{canManage ? <TableCell><div className="flex gap-1"><Button size="sm" variant="ghost" aria-label={t("projects.editCostEntry")} onClick={() => openCost(item)}><Pencil className="h-4 w-4" /></Button><Button size="sm" variant="ghost" aria-label={t("projects.deleteCostEntryConfirm")} onClick={() => void deleteCost(item)}><Trash2 className="h-4 w-4 text-destructive" /></Button></div></TableCell> : null}</TableRow>)}</TableBody></Table></CardContent></Card>
      <Card><CardHeader className="flex flex-row items-center justify-between gap-2"><CardTitle>{t("projects.commitments")}</CardTitle>{(commitments.data?.totalCount ?? 0) > 5 ? <Button size="sm" variant="outline" onClick={() => { setCommitmentPage(1); setCommitmentListOpen(true); }}>{t("projects.viewAllCount", { count: commitments.data?.totalCount ?? 0 })}</Button> : null}</CardHeader><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("projects.purchaseOrderNumber")}</TableHead><TableHead>{t("projects.vendorName")}</TableHead><TableHead>{t("projects.description")}</TableHead><TableHead>{t("projects.amount")}</TableHead><TableHead>{t("projects.status")}</TableHead><TableHead>{t("projects.commitmentDate")}</TableHead>{canManage ? <TableHead>{t("projects.actions")}</TableHead> : null}</TableRow></TableHeader><TableBody>{commitments.isLoading || commitments.isError ? <FinancialTableState loading={commitments.isLoading} error={commitments.isError} colSpan={canManage ? 7 : 6} onRetry={() => void commitments.refetch()} t={t} /> : (commitments.data?.items ?? []).length === 0 ? <TableRow><TableCell colSpan={canManage ? 7 : 6} className="p-6 text-center text-sm text-muted-foreground">{t("projects.noCommitments")}</TableCell></TableRow> : (commitments.data?.items ?? []).map((item) => <TableRow key={item.id}><TableCell className="font-medium">{item.purchaseOrderNumber}</TableCell><TableCell>{item.vendorName || "—"}</TableCell><TableCell className="max-w-[220px] truncate">{item.description || "—"}</TableCell><TableCell className="tabular-nums">{money(item.amount, item.currency, formatCurrency)}</TableCell><TableCell><Badge status={item.status}>{statusLabel(t, "commitmentStatuses", item.status)}</Badge></TableCell><TableCell className="whitespace-nowrap">{dateOnly(item.commitmentDate)}{item.expectedDeliveryDate ? <span className="mt-0.5 block text-xs text-muted-foreground">{t("projects.expectedDeliveryDate")}: {dateOnly(item.expectedDeliveryDate)}</span> : null}</TableCell>{canManage ? <TableCell><div className="flex flex-wrap gap-1"><Button size="sm" variant="ghost" aria-label={t("projects.editCommitment")} onClick={() => openCommitment(item)}><Pencil className="h-4 w-4" /></Button>{["open", "active"].includes(item.status.toLowerCase()) ? <><Button size="sm" variant="ghost" aria-label={t("projects.closeCommitment")} onClick={() => void changeCommitmentStatus(item, "close")}><Archive className="h-4 w-4" /></Button><Button size="sm" variant="ghost" aria-label={t("projects.cancelCommitment")} onClick={() => void changeCommitmentStatus(item, "cancel")}><Ban className="h-4 w-4 text-destructive" /></Button></> : null}</div></TableCell> : null}</TableRow>)}</TableBody></Table></CardContent></Card>

      <SideSheet open={budgetOpen} onOpenChange={(open) => { setBudgetOpen(open); if (!open) setBudgetFormError(null); }} title={editingBudget ? t("projects.editBudgetLine") : t("projects.addBudgetLine")} description={t("projects.budgetLineFormDescription")}>
        <form onSubmit={(event) => void submitBudget(event)} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-5">
          <section className="space-y-3"><h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.budgetLineDetails")}</h3><div><Label>{t("projects.name")} *</Label><Input value={budgetForm.name} onChange={(event) => setBudgetForm({ ...budgetForm, name: event.target.value })} required /></div><div><Label>{t("projects.category")} *</Label><Input value={budgetForm.category} onChange={(event) => setBudgetForm({ ...budgetForm, category: event.target.value })} required /></div><div><Label>{t("projects.description")}</Label><Textarea value={budgetForm.description} onChange={(event) => setBudgetForm({ ...budgetForm, description: event.target.value })} rows={3} /></div></section>
          <section className="space-y-3"><h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.budgetLineAmounts")}</h3><div className="grid gap-3 sm:grid-cols-3"><div><Label>{t("projects.plannedAmount")} *</Label><Input type="number" min="0" step="0.01" value={budgetForm.plannedAmount} onChange={(event) => setBudgetForm({ ...budgetForm, plannedAmount: event.target.value })} required /></div><div><Label>{t("projects.approvedAmount")} *</Label><Input type="number" min="0" step="0.01" value={budgetForm.approvedAmount} onChange={(event) => setBudgetForm({ ...budgetForm, approvedAmount: event.target.value })} required /></div><div><Label>{t("projects.forecastAmount")} *</Label><Input type="number" min="0" step="0.01" value={budgetForm.forecastAmount} onChange={(event) => setBudgetForm({ ...budgetForm, forecastAmount: event.target.value })} required /></div></div></section>
          <section className="space-y-3"><h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.budgetLineFinancialPeriod")}</h3><div className="grid gap-3 sm:grid-cols-2"><div><Label>{t("projects.fiscalYear")} *</Label><Input type="number" min="1900" max="2200" value={budgetForm.fiscalYear} onChange={(event) => setBudgetForm({ ...budgetForm, fiscalYear: event.target.value })} required /></div><div><Label>{t("projects.fiscalQuarter")}</Label><Input type="number" min="1" max="4" step="1" value={budgetForm.fiscalQuarter} onChange={(event) => setBudgetForm({ ...budgetForm, fiscalQuarter: event.target.value })} /></div></div></section>
          <section className="space-y-2"><h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.budgetLineCurrency")}</h3><p className="rounded-md border px-3 py-2 text-sm font-medium">{currency}</p></section>
          {budgetFormError ? <p role="alert" className="text-sm text-destructive">{budgetFormError}</p> : null}
        </SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setBudgetOpen(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={busy}>{busy ? t("common.saving") : editingBudget ? t("projects.saveChanges") : t("projects.saveBudgetLine")}</Button></SideSheetFooter></form>
      </SideSheet>
      <SideSheet open={costOpen} onOpenChange={(open) => { setCostOpen(open); if (!open) setCostFormError(null); }} title={editingCost ? t("projects.editCostEntry") : t("projects.addCostEntry")} description={t("projects.financialFormDescription")}>
        <form onSubmit={(event) => void submitCost(event)} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-5">
          <section className="space-y-3"><h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.costDetailsSection")}</h3><div><Label>{t("projects.description")}</Label><Input value={costForm.description} onChange={(event) => setCostForm({ ...costForm, description: event.target.value })} /></div><div><Label>{t("projects.category")} *</Label><Input value={costForm.category} onChange={(event) => setCostForm({ ...costForm, category: event.target.value })} required /></div><div><Label>{t("projects.vendorName")}</Label><Input value={costForm.vendorName} onChange={(event) => setCostForm({ ...costForm, vendorName: event.target.value })} /></div><div><Label>{t("projects.costType")}</Label><Input value={costForm.type} onChange={(event) => setCostForm({ ...costForm, type: event.target.value })} /></div></section>
          <section className="space-y-3"><h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.costFinancialSection")}</h3><div className="grid gap-3 sm:grid-cols-2"><div><Label>{t("projects.amount")} *</Label><Input type="number" min="0" step="0.01" value={costForm.amount} onChange={(event) => setCostForm({ ...costForm, amount: event.target.value })} required /></div><div><Label>{t("projects.taxAmount")}</Label><Input type="number" min="0" step="0.01" value={costForm.taxAmount} onChange={(event) => setCostForm({ ...costForm, taxAmount: event.target.value })} /></div></div><div><Label>{t("projects.currencyLabel")}</Label><p className="rounded-md border px-3 py-2 text-sm font-medium">{currency}</p></div></section>
          <section className="space-y-3"><h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.costTimingSection")}</h3><div className="grid gap-3 sm:grid-cols-2"><div><Label>{t("projects.incurredDate")} *</Label><Input type="date" value={costForm.incurredDate} onChange={(event) => setCostForm({ ...costForm, incurredDate: event.target.value })} required /></div><div><Label>{t("projects.status")}</Label><Input value={costForm.status} onChange={(event) => setCostForm({ ...costForm, status: event.target.value })} /></div></div><div><Label>{t("projects.reference")}</Label><Input value={costForm.reference} onChange={(event) => setCostForm({ ...costForm, reference: event.target.value })} /></div></section>
          {costFormError ? <p role="alert" className="text-sm text-destructive">{costFormError}</p> : null}
        </SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setCostOpen(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={busy}>{busy ? t("common.saving") : t("common.save")}</Button></SideSheetFooter></form>
      </SideSheet>
      <SideSheet open={commitmentOpen} onOpenChange={(open) => { setCommitmentOpen(open); if (!open) setCommitmentFormError(null); }} title={editingCommitment ? t("projects.editCommitment") : t("projects.addCommitment")} description={t("projects.commitmentFormDescription")}>
        <form onSubmit={(event) => void submitCommitment(event)} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-5">
          <section className="space-y-3"><h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.commitmentDetailsSection")}</h3><div><Label>{t("projects.purchaseOrderNumber")} *</Label><Input value={commitmentForm.purchaseOrderNumber} onChange={(event) => setCommitmentForm({ ...commitmentForm, purchaseOrderNumber: event.target.value })} required /></div><div><Label>{t("projects.description")}</Label><Textarea value={commitmentForm.description} onChange={(event) => setCommitmentForm({ ...commitmentForm, description: event.target.value })} rows={3} /></div><div><Label>{t("projects.vendorName")}</Label><Input value={commitmentForm.vendorName} onChange={(event) => setCommitmentForm({ ...commitmentForm, vendorName: event.target.value })} /></div></section>
          <section className="space-y-3"><h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.commitmentFinancialSection")}</h3><div><Label>{t("projects.amount")} *</Label><Input type="number" min="0.01" step="0.01" value={commitmentForm.amount} onChange={(event) => setCommitmentForm({ ...commitmentForm, amount: event.target.value })} required /></div><div><Label>{t("projects.currencyLabel")}</Label><p className="rounded-md border px-3 py-2 text-sm font-medium">{currency}</p></div></section>
          <section className="space-y-3"><h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.commitmentTimingSection")}</h3><div className="grid gap-3 sm:grid-cols-2"><div><Label>{t("projects.status")}</Label><p className="rounded-md border px-3 py-2 text-sm">{statusLabel(t, "commitmentStatuses", editingCommitment?.status ?? "Open")}</p></div><div><Label>{t("projects.commitmentDate")} *</Label><Input type="date" value={commitmentForm.commitmentDate} onChange={(event) => setCommitmentForm({ ...commitmentForm, commitmentDate: event.target.value })} required /></div></div><div><Label>{t("projects.expectedDeliveryDate")}</Label><Input type="date" value={commitmentForm.expectedDeliveryDate} onChange={(event) => setCommitmentForm({ ...commitmentForm, expectedDeliveryDate: event.target.value })} /></div></section>
          {commitmentFormError ? <p role="alert" className="text-sm text-destructive">{commitmentFormError}</p> : null}
        </SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setCommitmentOpen(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={busy}>{busy ? t("common.saving") : editingCommitment ? t("projects.saveChanges") : t("projects.saveCommitment")}</Button></SideSheetFooter></form>
      </SideSheet>

      {budgetListOpen ? <FinancialListSheet open={budgetListOpen} onOpenChange={setBudgetListOpen} title={t("projects.budgetLines")} description={t("projects.budgetLinesDescription")} total={budgetList.data?.totalCount ?? 0} page={budgetPage} totalPages={budgetList.data?.totalPages ?? Math.ceil((budgetList.data?.totalCount ?? 0) / 20)} loading={budgetList.isLoading} error={budgetList.isError} onRetry={() => void budgetList.refetch()} onPageChange={setBudgetPage} actionError={actionError} t={t}>
        <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("projects.budgetLine")}</TableHead><TableHead>{t("projects.category")}</TableHead><TableHead>{t("projects.fiscalPeriod")}</TableHead><TableHead>{t("projects.plannedAmount")}</TableHead><TableHead>{t("projects.approvedAmount")}</TableHead><TableHead>{t("projects.forecastAmount")}</TableHead><TableHead>{t("common.status")}</TableHead>{canManage ? <TableHead>{t("projects.actions")}</TableHead> : null}</TableRow></TableHeader><TableBody>{(budgetList.data?.items ?? []).map((item) => <TableRow key={item.id}><TableCell className="font-medium">{item.name}</TableCell><TableCell>{item.category}</TableCell><TableCell>{fiscalPeriod(t, item.fiscalYear, item.fiscalQuarter)}</TableCell><TableCell className="tabular-nums">{money(item.plannedAmount, item.currency, formatCurrency)}</TableCell><TableCell className="tabular-nums">{money(item.approvedAmount, item.currency, formatCurrency)}</TableCell><TableCell className="tabular-nums">{money(item.forecastAmount, item.currency, formatCurrency)}</TableCell><TableCell><Badge status={item.status}>{statusLabel(t, "budgetStatuses", item.status)}</Badge></TableCell>{canManage ? <TableCell><div className="flex gap-1"><Button size="sm" variant="ghost" aria-label={t("projects.editBudgetLine")} disabled={mutations.updateBudget.isPending} onClick={() => { setBudgetListOpen(false); openBudget(item); }}><Pencil className="h-4 w-4" /></Button><Button size="sm" variant="ghost" aria-label={t("projects.deleteBudgetLine")} onClick={() => void deleteBudget(item)}><Trash2 className="h-4 w-4 text-destructive" /></Button></div></TableCell> : null}</TableRow>)}</TableBody></Table></div>
      </FinancialListSheet> : null}
      {costListOpen ? <FinancialListSheet open={costListOpen} onOpenChange={setCostListOpen} title={t("projects.actualCosts")} description={t("projects.actualCostsDescription")} total={costList.data?.totalCount ?? 0} page={costPage} totalPages={costList.data?.totalPages ?? Math.ceil((costList.data?.totalCount ?? 0) / 20)} loading={costList.isLoading} error={costList.isError} onRetry={() => void costList.refetch()} onPageChange={setCostPage} actionError={actionError} t={t}>
        <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("projects.description")}</TableHead><TableHead>{t("projects.category")}</TableHead><TableHead>{t("projects.vendorName")}</TableHead><TableHead>{t("projects.costType")}</TableHead><TableHead>{t("projects.incurredDate")}</TableHead><TableHead>{t("projects.amount")}</TableHead><TableHead>{t("projects.taxAmount")}</TableHead><TableHead>{t("common.status")}</TableHead>{canManage ? <TableHead>{t("projects.actions")}</TableHead> : null}</TableRow></TableHeader><TableBody>{(costList.data?.items ?? []).map((item) => <TableRow key={item.id}><TableCell className="max-w-[220px] truncate font-medium">{item.description || "—"}</TableCell><TableCell>{item.category}</TableCell><TableCell>{item.vendorName || "—"}</TableCell><TableCell>{item.type}</TableCell><TableCell className="whitespace-nowrap">{dateOnly(item.incurredDate)}</TableCell><TableCell className="tabular-nums">{money(item.amount, item.currency, formatCurrency)}</TableCell><TableCell className="tabular-nums">{item.taxAmount == null ? "—" : money(item.taxAmount, item.currency, formatCurrency)}</TableCell><TableCell><Badge status={item.status}>{statusLabel(t, "costStatuses", item.status)}</Badge></TableCell>{canManage ? <TableCell><div className="flex gap-1"><Button size="sm" variant="ghost" aria-label={t("projects.editCostEntry")} disabled={mutations.updateCost.isPending} onClick={() => { setCostListOpen(false); openCost(item); }}><Pencil className="h-4 w-4" /></Button><Button size="sm" variant="ghost" aria-label={t("projects.deleteCostEntryConfirm")} onClick={() => void deleteCost(item)}><Trash2 className="h-4 w-4 text-destructive" /></Button></div></TableCell> : null}</TableRow>)}</TableBody></Table></div>
      </FinancialListSheet> : null}
      {commitmentListOpen ? <FinancialListSheet open={commitmentListOpen} onOpenChange={setCommitmentListOpen} title={t("projects.commitments")} description={t("projects.commitmentsDescription")} total={commitmentList.data?.totalCount ?? 0} page={commitmentPage} totalPages={commitmentList.data?.totalPages ?? Math.ceil((commitmentList.data?.totalCount ?? 0) / 20)} loading={commitmentList.isLoading} error={commitmentList.isError} onRetry={() => void commitmentList.refetch()} onPageChange={setCommitmentPage} actionError={actionError} t={t}>
        <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("projects.purchaseOrderNumber")}</TableHead><TableHead>{t("projects.vendorName")}</TableHead><TableHead>{t("projects.description")}</TableHead><TableHead>{t("projects.amount")}</TableHead><TableHead>{t("projects.status")}</TableHead><TableHead>{t("projects.commitmentDate")}</TableHead>{canManage ? <TableHead>{t("projects.actions")}</TableHead> : null}</TableRow></TableHeader><TableBody>{(commitmentList.data?.items ?? []).map((item) => <TableRow key={item.id}><TableCell className="font-medium">{item.purchaseOrderNumber}</TableCell><TableCell>{item.vendorName || "—"}</TableCell><TableCell className="max-w-[220px] truncate">{item.description || "—"}</TableCell><TableCell className="tabular-nums">{money(item.amount, item.currency, formatCurrency)}</TableCell><TableCell><Badge status={item.status}>{statusLabel(t, "commitmentStatuses", item.status)}</Badge></TableCell><TableCell className="whitespace-nowrap">{dateOnly(item.commitmentDate)}{item.expectedDeliveryDate ? <span className="mt-0.5 block text-xs text-muted-foreground">{t("projects.expectedDeliveryDate")}: {dateOnly(item.expectedDeliveryDate)}</span> : null}</TableCell>{canManage ? <TableCell><div className="flex gap-1"><Button size="sm" variant="ghost" aria-label={t("projects.editCommitment")} disabled={mutations.updateCommitment.isPending} onClick={() => { setCommitmentListOpen(false); openCommitment(item); }}><Pencil className="h-4 w-4" /></Button>{["open", "active"].includes(item.status.toLowerCase()) ? <><Button size="sm" variant="ghost" aria-label={t("projects.closeCommitment")} disabled={mutations.closeCommitment.isPending} onClick={() => void changeCommitmentStatus(item, "close")}><Archive className="h-4 w-4" /></Button><Button size="sm" variant="ghost" aria-label={t("projects.cancelCommitment")} disabled={mutations.cancelCommitment.isPending} onClick={() => void changeCommitmentStatus(item, "cancel")}><Ban className="h-4 w-4 text-destructive" /></Button></> : null}</div></TableCell> : null}</TableRow>)}</TableBody></Table></div>
      </FinancialListSheet> : null}
    </div>
  );
}

function FinancialListSheet({ open, onOpenChange, title, description, total, page, totalPages, loading, error, onRetry, onPageChange, actionError, t, children }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  total: number;
  page: number;
  totalPages: number;
  loading: boolean;
  error: boolean;
  onRetry: () => void;
  onPageChange: (page: number) => void;
  actionError: string | null;
  t: (key: string, params?: Record<string, string | number>) => string;
  children: React.ReactNode;
}) {
  return <SideSheet open={open} onOpenChange={onOpenChange} title={title} description={description} size="xl">
    <SideSheetBody className="space-y-4">
      <p className="text-xs text-muted-foreground">{t("projects.totalRecords", { count: total })}</p>
      {actionError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{actionError}</p> : null}
      {loading ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : error ? <div className="flex min-h-40 flex-col items-center justify-center gap-3 text-center"><p role="alert" className="text-sm text-destructive">{t("common.errorLoading")}</p><Button variant="outline" size="sm" onClick={onRetry}>{t("common.retry")}</Button></div> : total === 0 ? <p className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">{t("projects.noRecords")}</p> : children}
    </SideSheetBody>
    {!loading && !error && totalPages > 1 ? <SideSheetFooter><Pagination page={page} totalPages={totalPages} total={total} pageSize={20} onChange={onPageChange} /></SideSheetFooter> : null}
  </SideSheet>;
}
