"use client";

import { useMemo, useState } from "react";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { ChevronDown, ChevronUp, CircleCheck, CircleX, Loader2, Pencil, Plus, Trash2, WalletCards, AlertCircle, RotateCcw, AlertTriangle, TrendingDown } from "lucide-react";
import { useTranslations } from "next-intl";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { Tooltip as TooltipUI, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useBudgets, useCommitments, useCostEntries, useCostManagementMutations, useProjectBudgets, useProjectCommitments, useProjectCosts, type BudgetDto, type CreateBudgetDto, type CreateCommitmentDto, type CreateCostEntryDto, type CostEntryDto, type PurchaseCommitmentDto } from "@/lib/api/costs";
import type { ProjectDto } from "@/hooks/projects/useProjects";
import type { ProjectOverviewDto } from "@/lib/api/projects";
import { formatCurrency, formatDate, isFiniteNumber } from "@/lib/utils";
import { BudgetRevisionSection } from "@/components/projects/BudgetRevisionSection";
import { TablePagination } from "@/components/ui/table-pagination";
import { ChartContainer, ChartTooltipContent } from "@/components/ui/chart";

type Props = { project: ProjectDto; summary?: ProjectOverviewDto; enabled: boolean; canManage: boolean; loading?: boolean; error?: boolean };
type BudgetForm = Omit<CreateBudgetDto, "projectId" | "currency">;
type CostForm = Omit<CreateCostEntryDto, "projectId" | "currency">;
type CommitmentForm = Omit<CreateCommitmentDto, "projectId">;

const emptyBudget: BudgetForm = {
  name: "",
  description: "",
  category: "",
  plannedAmount: 0,
  approvedAmount: 0,
  forecastAmount: 0,
  fiscalYear: new Date().getFullYear(),
  fiscalQuarter: undefined,
  status: "Draft",
};
const emptyCost: CostForm = { category: "", description: "", amount: 0, incurredDate: new Date().toISOString().slice(0, 10), type: "Actual", status: "Posted" };
const emptyCommitment: CommitmentForm = { purchaseOrderNumber: "", description: "", vendorName: "", amount: 0, commitmentDate: new Date().toISOString().slice(0, 10) };

function money(value: number | null | undefined, currency: string) { return isFiniteNumber(value) ? formatCurrency(value, currency) : "—"; }
function version(value: number[]) { return Array.isArray(value) ? value : []; }
function normalizedCommitmentStatus(status: string | null | undefined) { return status?.trim().toLowerCase() ?? ""; }
function isActiveStatus(status: string | null | undefined) { const normalized = normalizedCommitmentStatus(status); return normalized === "open" || normalized === "active"; }
function isTerminalCommitmentStatus(status: string | null | undefined) { const normalized = normalizedCommitmentStatus(status); return normalized === "closed" || normalized === "cancelled" || normalized === "canceled"; }
function pct(value: number | null | undefined) { return isFiniteNumber(value) ? `${value.toFixed(1)}%` : "—"; }

const commitmentStatusKeys: Record<string, string> = { open: "commitmentStatuses.open", active: "commitmentStatuses.active", closed: "commitmentStatuses.closed", cancelled: "commitmentStatuses.cancelled", canceled: "commitmentStatuses.cancelled" };
function commitmentStatusLabel(t: ReturnType<typeof useTranslations>, status: string | null | undefined) {
  const trimmed = status?.trim();
  if (!trimmed) return "—";
  const key = commitmentStatusKeys[trimmed.toLowerCase()];
  return key ? t(key as never) : trimmed;
}

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

function budgetStatusLabel(t: ReturnType<typeof useTranslations>, status: string) {
  return t((budgetStatusKeys[status.trim().toLowerCase()] ?? "budgetStatusUnknown") as never);
}

const costStatusKeys: Record<string, string> = {
  draft: "costStatuses.draft",
  posted: "costStatuses.posted",
  verified: "costStatuses.verified",
  cancelled: "costStatuses.cancelled",
};

function costStatusLabel(t: ReturnType<typeof useTranslations>, status: string | null | undefined): string {
  const trimmed = status?.trim();
  if (!trimmed) return "—";
  const key = costStatusKeys[trimmed.toLowerCase()];
  return key ? t(key as never) : trimmed;
}

function budgetVariance(item: BudgetDto) {
  if (!isFiniteNumber(item.approvedAmount) || !isFiniteNumber(item.forecastAmount)) return null;
  return item.approvedAmount - item.forecastAmount;
}

function varianceLabel(t: ReturnType<typeof useTranslations>, value: number) {
  if (value > 0) return t("underBudget");
  if (value < 0) return t("overBudget");
  return t("onBudget");
}

function fiscalPeriodLabel(t: ReturnType<typeof useTranslations>, item: BudgetDto) {
  return item.fiscalQuarter == null ? `${t("fiscalYearShort")} ${item.fiscalYear}` : `${t("fiscalYearShort")} ${item.fiscalYear} / ${t("quarterShort")}${item.fiscalQuarter}`;
}

function forecastWarningKey(t: ReturnType<typeof useTranslations>, forecast: number | null | undefined, ceiling: number | null | undefined, available: number | null | undefined): string | null {
  if (!isFiniteNumber(forecast) || !isFiniteNumber(ceiling)) return null;
  if (forecast > ceiling) return t("forecastExceedsCeiling");
  if (isFiniteNumber(available) && forecast > available) return t("insufficientAvailable");
  return null;
}

export function ProjectFinancials({ project, summary, enabled, canManage, loading, error }: Props) {
  const t = useTranslations("projects");
  const common = useTranslations("common");
  const budgets = useProjectBudgets(project.id, enabled, { page: 1, pageSize: 5 });
  const costs = useProjectCosts(project.id, enabled, { page: 1, pageSize: 5 });
  const commitments = useProjectCommitments(project.id, enabled, { page: 1, pageSize: 5 });
  const mutations = useCostManagementMutations(project.id);
  const [budgetOpen, setBudgetOpen] = useState(false);
  const [costOpen, setCostOpen] = useState(false);
  const [commitmentOpen, setCommitmentOpen] = useState(false);
  const [budgetListOpen, setBudgetListOpen] = useState(false);
  const [costListOpen, setCostListOpen] = useState(false);
  const [commitmentListOpen, setCommitmentListOpen] = useState(false);
  const [budgetPage, setBudgetPage] = useState(1);
  const [costPage, setCostPage] = useState(1);
  const [commitmentPage, setCommitmentPage] = useState(1);
  const [editingBudget, setEditingBudget] = useState<BudgetDto | null>(null);
  const [editingCost, setEditingCost] = useState<CostEntryDto | null>(null);
  const [editingCommitment, setEditingCommitment] = useState<PurchaseCommitmentDto | null>(null);
  const [budgetForm, setBudgetForm] = useState<BudgetForm>({ ...emptyBudget });
  const [costForm, setCostForm] = useState<CostForm>({ ...emptyCost });
  const [commitmentForm, setCommitmentForm] = useState<CommitmentForm>({ ...emptyCommitment });
  const [budgetFormError, setBudgetFormError] = useState<string | null>(null);
  const [budgetActionError, setBudgetActionError] = useState<string | null>(null);
  const [costFormError, setCostFormError] = useState<string | null>(null);
  const [costActionError, setCostActionError] = useState<string | null>(null);
  const [commitmentFormError, setCommitmentFormError] = useState<string | null>(null);
  const [commitmentActionError, setCommitmentActionError] = useState<string | null>(null);
  const [expandedBudgetId, setExpandedBudgetId] = useState<string | null>(null);
  const currency = summary?.currency || project.currency || "GBP";
  const budgetItems = budgets.data?.items ?? [];
  const costItems = costs.data?.items ?? [];
  const commitmentItems = commitments.data?.items ?? [];
  const budgetList = useBudgets({ projectId: project.id, page: budgetPage, pageSize: 20 }, budgetListOpen && enabled);
  const costList = useCostEntries({ projectId: project.id, page: costPage, pageSize: 20 }, costListOpen && enabled);
  const commitmentList = useCommitments({ projectId: project.id, page: commitmentPage, pageSize: 20 }, commitmentListOpen && enabled);

  const budgetCeiling = summary?.approvedBudget ?? null;
  const allocatedBudget = summary?.allocatedBudget ?? null;
  const unallocatedBudget = summary?.unallocatedBudget ?? (isFiniteNumber(budgetCeiling) && isFiniteNumber(allocatedBudget) ? budgetCeiling - allocatedBudget : null);
  const actualCost = summary?.actualCost ?? null;
  const committedCost = summary?.committedCost ?? null;
  const forecastAtCompletion = summary?.forecastAtCompletion ?? null;
  const availableBudget = isFiniteNumber(budgetCeiling) && isFiniteNumber(actualCost) && isFiniteNumber(committedCost) ? budgetCeiling - actualCost - committedCost : null;
  const allocationPct = isFiniteNumber(budgetCeiling) && budgetCeiling > 0 && isFiniteNumber(allocatedBudget) ? (allocatedBudget / budgetCeiling) * 100 : 0;
  const warning = forecastWarningKey(t, forecastAtCompletion, budgetCeiling, availableBudget);

  const spendingChartData = useMemo(() => {
    const a = isFiniteNumber(actualCost) ? actualCost : 0;
    const c = isFiniteNumber(committedCost) ? committedCost : 0;
    const avail = isFiniteNumber(availableBudget) ? Math.max(availableBudget, 0) : 0;
    return [{ name: t("spendingPosition"), actual: a, committed: c, available: avail }];
  }, [actualCost, committedCost, availableBudget, t]);

  const openBudget = (item?: BudgetDto) => {
    setEditingBudget(item ?? null);
    setBudgetForm(item ? {
      name: item.name,
      description: item.description ?? "",
      category: item.category,
      plannedAmount: item.plannedAmount,
      approvedAmount: item.approvedAmount,
      forecastAmount: item.forecastAmount,
      fiscalYear: item.fiscalYear,
      fiscalQuarter: item.fiscalQuarter,
      status: item.status,
      startDate: item.startDate,
      endDate: item.endDate,
    } : { ...emptyBudget });
    setBudgetFormError(null);
    setBudgetActionError(null);
    setBudgetOpen(true);
  };

  const openCost = (item?: CostEntryDto) => {
    setEditingCost(item ?? null);
    setCostForm(item ? { budgetId: item.budgetId, category: item.category, subCategory: item.subCategory, description: item.description, amount: item.amount, taxAmount: item.taxAmount, incurredDate: item.incurredDate.slice(0, 10), type: item.type, reference: item.reference, vendorName: item.vendorName, status: item.status } : { ...emptyCost });
    setCostFormError(null);
    setCostActionError(null);
    setCostOpen(true);
  };

  const openCommitment = (item?: PurchaseCommitmentDto) => {
    setEditingCommitment(item ?? null);
    setCommitmentForm(item ? { purchaseOrderNumber: item.purchaseOrderNumber, description: item.description, vendorName: item.vendorName, amount: item.amount, commitmentDate: item.commitmentDate.slice(0, 10), expectedDeliveryDate: item.expectedDeliveryDate?.slice(0, 10), budgetId: item.budgetId } : { ...emptyCommitment });
    setCommitmentFormError(null);
    setCommitmentActionError(null);
    setCommitmentOpen(true);
  };

  const validateBudgetForm = () => {
    if (!budgetForm.name.trim()) return t("budgetLineNameRequired");
    if (!budgetForm.category.trim()) return t("budgetLineCategoryRequired");
    if ([budgetForm.plannedAmount, budgetForm.approvedAmount, budgetForm.forecastAmount].some((value) => !isFiniteNumber(value) || value < 0)) return t("budgetLineAmountsInvalid");
    if (!Number.isInteger(budgetForm.fiscalYear) || budgetForm.fiscalYear < 1900 || budgetForm.fiscalYear > 2200) return t("budgetLineFiscalYearInvalid");
    if (budgetForm.fiscalQuarter != null && (!Number.isInteger(budgetForm.fiscalQuarter) || budgetForm.fiscalQuarter < 1 || budgetForm.fiscalQuarter > 4)) return t("budgetLineFiscalQuarterInvalid");
    return null;
  };

  const submitBudget = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validationError = validateBudgetForm();
    if (validationError) { setBudgetFormError(validationError); return; }
    setBudgetFormError(null);
    try {
      const body = { ...budgetForm, name: budgetForm.name.trim(), category: budgetForm.category.trim(), projectId: project.id, currency };
      if (editingBudget) await mutations.updateBudget.mutateAsync({ id: editingBudget.id, body: { ...body, rowVersion: editingBudget.rowVersion } });
      else await mutations.createBudget.mutateAsync(body);
      setBudgetOpen(false);
    } catch (err) {
      setBudgetFormError(err instanceof Error ? err.message : t("budgetLineSaveFailed"));
    }
  };

  const deleteBudget = async (item: BudgetDto) => {
    if (!window.confirm(t("deleteBudgetLineConfirm", { name: item.name }))) return;
    setBudgetActionError(null);
    try {
      await mutations.deleteBudget.mutateAsync(item.id);
    } catch (err) {
      setBudgetActionError(err instanceof Error ? err.message : t("budgetLineDeleteFailed"));
    }
  };

  const submitCost = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!costForm.category.trim()) { setCostFormError(t("budgetLineCategoryRequired")); return; }
    if (!isFiniteNumber(costForm.amount) || costForm.amount < 0) { setCostFormError(t("budgetLineAmountsInvalid")); return; }
    setCostFormError(null);
    try {
      const body = { ...costForm, category: costForm.category.trim(), description: costForm.description?.trim() || undefined, vendorName: costForm.vendorName?.trim() || undefined, reference: costForm.reference?.trim() || undefined, projectId: project.id, currency };
      if (editingCost) await mutations.updateCost.mutateAsync({ id: editingCost.id, body: { ...body, rowVersion: version(editingCost.rowVersion) } });
      else await mutations.createCost.mutateAsync(body);
      setCostOpen(false);
    } catch (err) {
      setCostFormError(err instanceof Error ? err.message : t("costEntrySaveFailed"));
    }
  };

  const deleteCost = async (item: CostEntryDto) => {
    if (!window.confirm(t("deleteCostEntryConfirm", { amount: money(item.amount, item.currency) }))) return;
    setCostActionError(null);
    try {
      await mutations.deleteCost.mutateAsync(item.id);
    } catch (err) {
      setCostActionError(err instanceof Error ? err.message : t("costEntryDeleteFailed"));
    }
  };

  const submitCommitment = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!commitmentForm.purchaseOrderNumber.trim()) { setCommitmentFormError(t("commitmentReferenceRequired")); return; }
    if (!isFiniteNumber(commitmentForm.amount) || commitmentForm.amount <= 0) { setCommitmentFormError(t("commitmentAmountInvalid")); return; }
    if (!commitmentForm.commitmentDate) { setCommitmentFormError(t("commitmentDateRequired")); return; }
    setCommitmentFormError(null);
    try {
      const body = { ...commitmentForm, purchaseOrderNumber: commitmentForm.purchaseOrderNumber.trim(), description: commitmentForm.description?.trim() || undefined, vendorName: commitmentForm.vendorName?.trim() || undefined, expectedDeliveryDate: commitmentForm.expectedDeliveryDate || undefined };
      if (editingCommitment) await mutations.updateCommitment.mutateAsync({ id: editingCommitment.id, body: { ...body, rowVersion: editingCommitment.rowVersion } });
      else await mutations.createCommitment.mutateAsync({ ...body, projectId: project.id });
      setCommitmentOpen(false);
    } catch (err) {
      setCommitmentFormError(err instanceof Error ? err.message : t("commitmentSaveFailed"));
    }
  };

  const transitionCommitment = async (item: PurchaseCommitmentDto, target: "close" | "cancel") => {
    const message = target === "close" ? t("closeCommitmentConfirm", { reference: item.purchaseOrderNumber }) : t("cancelCommitmentConfirm", { reference: item.purchaseOrderNumber });
    if (!window.confirm(message)) return;
    setCommitmentActionError(null);
    try {
      if (target === "close") await mutations.closeCommitment.mutateAsync({ id: item.id, rowVersion: item.rowVersion });
      else await mutations.cancelCommitment.mutateAsync({ id: item.id, rowVersion: item.rowVersion });
    } catch (err) {
      setCommitmentActionError(err instanceof Error ? err.message : target === "close" ? t("commitmentCloseFailed") : t("commitmentCancelFailed"));
    }
  };

  if (!enabled) return <Card className="shadow-none"><CardContent className="p-6 text-sm text-muted-foreground">{t("financialsPermissionRequired")}</CardContent></Card>;
  if (loading) return <div className="space-y-4"><Card className="shadow-none"><CardContent className="space-y-3 p-6"><Skeleton className="h-5 w-48" /><Skeleton className="h-4 w-80" /><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3"><Skeleton className="h-20" /><Skeleton className="h-20" /><Skeleton className="h-20" /></div></CardContent></Card><DataCard title={t("budgetLines")}><BudgetLinesSkeleton /></DataCard></div>;
  if (error) return <Card className="shadow-none"><CardContent className="p-6 text-sm text-destructive">{t("financialsLoadFailed")}</CardContent></Card>;

  const budgetSaving = mutations.createBudget.isPending || mutations.updateBudget.isPending;

  return <div className="space-y-4">
    <Card className="shadow-none">
      <CardHeader className="flex flex-row items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold">{t("financialSummary")}</h2>
          <p className="mt-1 text-xs text-muted-foreground">{t("financialSummaryDescription")}</p>
        </div>
        <span className="text-xs font-medium text-muted-foreground">{currency}</span>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <KpiCard label={t("budgetCeiling")} hint={t("budgetCeilingHint")} value={money(budgetCeiling, currency)} semantic="navy" />
          <KpiCard label={t("allocatedBudget")} hint={t("allocatedBudgetHint")} value={money(allocatedBudget, currency)} semantic="indigo" />
          <KpiCard label={t("unallocatedBudget")} hint={t("unallocatedBudgetHint")} value={money(unallocatedBudget, currency)} accent={isFiniteNumber(unallocatedBudget) && unallocatedBudget <= 0 ? "warning" : undefined} />
        </div>

        <div className="space-y-2">
          <div className="flex items-center justify-between text-xs text-muted-foreground">
            <span>{pct(allocationPct)} {t("allocationProgress")}</span>
            <span>{money(allocatedBudget, currency)} / {money(budgetCeiling, currency)}</span>
          </div>
          <Progress value={Math.min(allocationPct, 100)} className="h-2" />
        </div>

        <div className="grid gap-3 sm:grid-cols-2">
          <Card className="shadow-none">
            <CardHeader className="pb-3">
              <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("spendingPosition")}</h3>
              <p className="text-[11px] text-muted-foreground">{t("spendingPositionDescription")}</p>
            </CardHeader>
            <CardContent className="space-y-3">
              <ChartContainer className="h-36">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={spendingChartData} layout="vertical" margin={{ top: 0, right: 8, left: 0, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" horizontal={false} />
                    <XAxis type="number" />
                    <YAxis type="category" dataKey="name" hide />
                    <Tooltip content={<ChartTooltipContent formatter={(value) => money(Number(value), currency)} />} />
                    <Bar dataKey="actual" name={t("actualSpend")} stackId="a" fill="hsl(var(--fin-blue))" />
                    <Bar dataKey="committed" name={t("committedSpend")} stackId="a" fill="hsl(var(--fin-amber))" />
                    <Bar dataKey="available" name={t("availableBudget")} stackId="a" fill="hsl(var(--fin-green))" />
                  </BarChart>
                </ResponsiveContainer>
              </ChartContainer>
              <div className="space-y-1.5 text-xs">
                <SpendingRow label={t("actualSpend")} value={actualCost} currency={currency} color="hsl(var(--fin-blue))" />
                <SpendingRow label={t("committedSpend")} value={committedCost} currency={currency} color="hsl(var(--fin-amber))" />
                <SpendingRow label={t("availableBudget")} value={availableBudget} currency={currency} color="hsl(var(--fin-green))" hint={t("availableBudgetHint")} />
              </div>
            </CardContent>
          </Card>

          <Card className="shadow-none">
            <CardHeader className="pb-3">
              <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("forecastSection")}</h3>
              <p className="text-[11px] text-muted-foreground">{t("forecastSectionDescription")}</p>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <KpiCard label={t("estimateAtCompletion")} value={money(forecastAtCompletion, currency)} semantic="violet" />
                <KpiCard label={t("forecastVarianceLabel")} value={isFiniteNumber(summary?.forecastVariance) ? money(summary.forecastVariance, currency) : "—"} accent={isFiniteNumber(summary?.forecastVariance) ? (summary.forecastVariance! < 0 ? "danger" : "success") : undefined} />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <KpiCard label={t("budgetUtilization")} value={pct(summary?.budgetUtilizationPercentage)} />
                <KpiCard label={t("originalApprovedBudget")} value={money(summary?.originalApprovedBudget, currency)} />
              </div>
              {warning ? (
                <div className="flex items-start gap-2 rounded-md border border-fin-amber/30 bg-fin-amber/5 px-3 py-2 text-xs text-fin-amber dark:border-fin-amber/40 dark:bg-fin-amber/10 dark:text-fin-amber">
                  <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  <span>{warning}</span>
                </div>
              ) : isFiniteNumber(forecastAtCompletion) && isFiniteNumber(budgetCeiling) && forecastAtCompletion <= budgetCeiling ? (
                <div className="flex items-start gap-2 rounded-md border border-fin-green/30 bg-fin-green/5 px-3 py-2 text-xs text-fin-green dark:border-fin-green/40 dark:bg-fin-green/10 dark:text-fin-green">
                  <TrendingDown className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  <span>{t("forecastWithinBudget")}</span>
                </div>
              ) : null}
            </CardContent>
          </Card>
        </div>
      </CardContent>
    </Card>

    <DataCard title={t("budgetLines")} description={t("budgetLinesDescription")} meta={<span className="text-xs font-medium text-muted-foreground">{t("allAmountsIn", { currency })}</span>} action={<div className="flex flex-wrap items-center justify-end gap-2">{(budgets.data?.totalCount ?? 0) > 5 ? <Button size="sm" variant="outline" onClick={() => { setBudgetPage(1); setBudgetListOpen(true); }}>{t("viewAllCount", { count: budgets.data?.totalCount ?? 0 })}</Button> : null}{canManage ? <Button size="sm" onClick={() => openBudget()}><Plus className="me-1.5 h-3.5 w-3.5" />{t("addBudgetLine")}</Button> : null}</div>}>
      {budgetActionError ? <p role="alert" className="mb-3 rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-xs text-destructive">{budgetActionError}</p> : null}
      {budgets.isLoading ? <BudgetLinesSkeleton /> : budgets.isError ? <BudgetLinesState message={t("budgetLinesLoadFailed")} action={<Button type="button" variant="outline" size="sm" onClick={() => void budgets.refetch()}>{common("retry")}</Button>} /> : budgetItems.length ? <BudgetLinesView items={budgetItems} currency={currency} canManage={canManage} expandedBudgetId={expandedBudgetId} onToggleExpanded={(id) => setExpandedBudgetId((current) => current === id ? null : id)} onEdit={openBudget} onDelete={(item) => void deleteBudget(item)} t={t} /> : <BudgetLinesState message={t("noBudgetLinesDescription")} action={canManage ? <Button type="button" size="sm" onClick={() => openBudget()}><Plus className="me-1.5 h-3.5 w-3.5" />{t("addBudgetLine")}</Button> : null} />}
    </DataCard>

    <BudgetRevisionSection projectId={project.id} currency={currency} originalApprovedBudget={summary?.originalApprovedBudget} currentApprovedBudget={summary?.approvedBudget} enabled={enabled} canManage={canManage} />
    <DataCard title={t("actualCosts")} description={t("actualCostsDescription")} meta={<span className="text-xs font-medium text-muted-foreground">{t("allAmountsIn", { currency })}</span>} action={<div className="flex flex-wrap items-center justify-end gap-2">{(costs.data?.totalCount ?? 0) > 5 ? <Button size="sm" variant="outline" onClick={() => { setCostPage(1); setCostListOpen(true); }}>{t("viewAllCount", { count: costs.data?.totalCount ?? 0 })}</Button> : null}{canManage ? <Button size="sm" onClick={() => openCost()}><Plus className="me-1.5 h-3.5 w-3.5" />{t("addCostEntry")}</Button> : null}</div>}>
      {costActionError ? <p role="alert" className="mb-3 rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-xs text-destructive">{costActionError}</p> : null}
      {costs.isLoading ? <BudgetLinesSkeleton /> : costs.isError ? <BudgetLinesState message={t("budgetLinesLoadFailed")} action={<Button type="button" variant="outline" size="sm" onClick={() => void costs.refetch()}>{common("retry")}</Button>} /> : costItems.length ? <>
        <div className="mb-3 flex items-baseline gap-2">
          <span className="text-xs text-muted-foreground">{t("totalActualCost")}</span>
          <span className="text-sm font-semibold tabular-nums">{money(actualCost, currency)}</span>
        </div>
        <div className="divide-y">{costItems.map(item => <CostRow key={item.id} item={item} canManage={canManage} onEdit={openCost} onDelete={deleteCost} t={t} common={common} />)}</div>
      </> : <Empty text={t("noCostEntries")} />}
    </DataCard>
    <DataCard title={t("commitments")} description={t("commitmentsDescription")} meta={<div className="space-y-1"><div className="flex flex-wrap items-baseline gap-x-2 gap-y-1 text-xs"><span className="text-muted-foreground">{t("totalOutstandingCommitments")}</span><span className="font-semibold tabular-nums">{money(committedCost, currency)}</span></div><p className="text-[11px] text-muted-foreground">{t("commitmentsBudgetRelationship")}</p></div>} action={<div className="flex flex-wrap items-center justify-end gap-2">{(commitments.data?.totalCount ?? 0) > 5 ? <Button size="sm" variant="outline" onClick={() => { setCommitmentPage(1); setCommitmentListOpen(true); }}>{t("viewAllCount", { count: commitments.data?.totalCount ?? 0 })}</Button> : null}{canManage ? <Button size="sm" onClick={() => openCommitment()}><Plus className="me-1.5 h-3.5 w-3.5" />{t("addCommitment")}</Button> : null}</div>}>
      {commitmentActionError ? <p role="alert" className="mb-3 rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-xs text-destructive">{commitmentActionError}</p> : null}
      {commitments.isLoading ? <BudgetLinesSkeleton /> : commitments.isError ? <BudgetLinesState message={t("commitmentsLoadFailed")} action={<Button type="button" variant="outline" size="sm" onClick={() => void commitments.refetch()}>{common("retry")}</Button>} /> : commitmentItems.length ? <div className="divide-y">{commitmentItems.map(item => <CommitmentRow key={item.id} item={item} canManage={canManage} onEdit={openCommitment} onClose={(value) => void transitionCommitment(value, "close")} onCancel={(value) => void transitionCommitment(value, "cancel")} t={t} common={common} />)}</div> : <Empty text={t("noCommitments")} />}
    </DataCard>

    <Sheet open={budgetOpen} onOpenChange={(open) => { setBudgetOpen(open); if (!open) setBudgetFormError(null); }}><SheetContent><SheetHeader><SheetTitle>{t(editingBudget ? "editBudgetLine" : "addBudgetLine")}</SheetTitle><SheetDescription>{t("budgetLineFormDescription")}</SheetDescription></SheetHeader><form onSubmit={submitBudget} className="mt-6 space-y-6">
      <BudgetFormSection title={t("budgetLineDetails")}><Field id="budget-line-name" label={t("name")} required><Input id="budget-line-name" required value={budgetForm.name} onChange={(event) => setBudgetForm({ ...budgetForm, name: event.target.value })} /></Field><Field id="budget-line-category" label={t("category")} required><Input id="budget-line-category" required value={budgetForm.category} onChange={(event) => setBudgetForm({ ...budgetForm, category: event.target.value })} /></Field><Field id="budget-line-description" label={t("description")}><Textarea id="budget-line-description" value={budgetForm.description ?? ""} onChange={(event) => setBudgetForm({ ...budgetForm, description: event.target.value })} rows={3} /></Field></BudgetFormSection>
      <BudgetFormSection title={t("budgetLineAmounts")}><div className="grid gap-3 sm:grid-cols-3"><Field id="budget-line-planned" label={t("plannedAmount")} required><Input id="budget-line-planned" required type="number" min="0" step="0.01" value={budgetForm.plannedAmount} onChange={(event) => setBudgetForm({ ...budgetForm, plannedAmount: Number(event.target.value) })} /></Field><Field id="budget-line-approved" label={t("approvedAmount")} required><Input id="budget-line-approved" required type="number" min="0" step="0.01" value={budgetForm.approvedAmount} onChange={(event) => setBudgetForm({ ...budgetForm, approvedAmount: Number(event.target.value) })} /></Field><Field id="budget-line-forecast" label={t("forecastAmount")} required><Input id="budget-line-forecast" required type="number" min="0" step="0.01" value={budgetForm.forecastAmount} onChange={(event) => setBudgetForm({ ...budgetForm, forecastAmount: Number(event.target.value) })} /></Field></div></BudgetFormSection>
      <BudgetFormSection title={t("budgetLineFinancialPeriod")}><div className="grid gap-3 sm:grid-cols-2"><Field id="budget-line-fiscal-year" label={t("fiscalYear")} required><Input id="budget-line-fiscal-year" required type="number" min="1900" max="2200" value={budgetForm.fiscalYear} onChange={(event) => setBudgetForm({ ...budgetForm, fiscalYear: Number(event.target.value) })} /></Field><Field id="budget-line-fiscal-quarter" label={t("fiscalQuarter")} hint={t("fiscalQuarterOptional")}><Input id="budget-line-fiscal-quarter" type="number" min="1" max="4" step="1" value={budgetForm.fiscalQuarter ?? ""} onChange={(event) => setBudgetForm({ ...budgetForm, fiscalQuarter: event.target.value === "" ? undefined : Number(event.target.value) })} /></Field></div></BudgetFormSection>
      <BudgetFormSection title={t("budgetLineCurrency")}><div className="rounded-md border bg-muted/20 px-3 py-2 text-sm font-medium">{currency}</div></BudgetFormSection>
      {budgetFormError ? <p role="alert" className="rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">{budgetFormError}</p> : null}
      <SheetFooter className="flex-row justify-end gap-2"><Button type="button" variant="outline" onClick={() => setBudgetOpen(false)}>{common("cancel")}</Button><Button type="submit" disabled={budgetSaving}>{budgetSaving ? <><Loader2 className="me-1.5 h-4 w-4 animate-spin" />{common("saving")}</> : editingBudget ? t("saveChanges") : t("saveBudgetLine")}</Button></SheetFooter>
    </form></SheetContent></Sheet>
    <Sheet open={costOpen} onOpenChange={(open) => { setCostOpen(open); if (!open) setCostFormError(null); }}><SheetContent><SheetHeader><SheetTitle>{t(editingCost ? "editCostEntry" : "addCostEntry")}</SheetTitle><SheetDescription>{t("financialFormDescription")}</SheetDescription></SheetHeader><form onSubmit={submitCost} className="mt-6 space-y-6">
      <BudgetFormSection title={t("costDetailsSection")}><Field id="cost-description" label={t("description")} hint={t("descriptionHint")}><Input id="cost-description" value={costForm.description ?? ""} onChange={event => setCostForm({ ...costForm, description: event.target.value })} /></Field><Field id="cost-category" label={t("category")} required><Input id="cost-category" required value={costForm.category} onChange={event => setCostForm({ ...costForm, category: event.target.value })} /></Field><Field id="cost-vendor" label={t("vendorName")} hint={t("vendorHint")}><Input id="cost-vendor" value={costForm.vendorName ?? ""} onChange={event => setCostForm({ ...costForm, vendorName: event.target.value })} /></Field><Field id="cost-type" label={t("costType")} hint={t("costTypeHint")}><Input id="cost-type" value={costForm.type} onChange={event => setCostForm({ ...costForm, type: event.target.value })} /></Field></BudgetFormSection>
      <BudgetFormSection title={t("costFinancialSection")}><div className="grid gap-3 sm:grid-cols-2"><Field id="cost-amount" label={t("amount")} required hint={t("amountHint")}><Input id="cost-amount" required type="number" min="0" step="0.01" value={costForm.amount} onChange={event => setCostForm({ ...costForm, amount: Number(event.target.value) })} /></Field><Field id="cost-tax" label={t("taxAmount")} hint={t("taxHint")}><Input id="cost-tax" type="number" min="0" step="0.01" value={costForm.taxAmount ?? ""} onChange={event => setCostForm({ ...costForm, taxAmount: event.target.value === "" ? undefined : Number(event.target.value) })} /></Field></div><Field id="cost-currency" label={t("currencyLabel")}><div className="rounded-md border bg-muted/20 px-3 py-2 text-sm font-medium">{currency}</div></Field></BudgetFormSection>
      <BudgetFormSection title={t("costTimingSection")}><div className="grid gap-3 sm:grid-cols-2"><Field id="cost-date" label={t("incurredDate")} required hint={t("incurredDateHint")}><Input id="cost-date" required type="date" value={costForm.incurredDate} onChange={event => setCostForm({ ...costForm, incurredDate: event.target.value })} /></Field><Field id="cost-status" label={t("status")} hint={t("statusHint")}><Input id="cost-status" value={costForm.status} onChange={event => setCostForm({ ...costForm, status: event.target.value })} /></Field></div><Field id="cost-reference" label={t("reference")}><Input id="cost-reference" value={costForm.reference ?? ""} onChange={event => setCostForm({ ...costForm, reference: event.target.value })} /></Field></BudgetFormSection>
      {costFormError ? <p role="alert" className="rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">{costFormError}</p> : null}
      <SheetFooter className="flex-row justify-end gap-2"><Button type="button" variant="outline" onClick={() => setCostOpen(false)}>{common("cancel")}</Button><Button type="submit" disabled={mutations.createCost.isPending || mutations.updateCost.isPending}>{(mutations.createCost.isPending || mutations.updateCost.isPending) ? <><Loader2 className="me-1.5 h-4 w-4 animate-spin" />{common("saving")}</> : common("save")}</Button></SheetFooter>
    </form></SheetContent></Sheet>
    <Sheet open={commitmentOpen} onOpenChange={(open) => { setCommitmentOpen(open); if (!open) setCommitmentFormError(null); }}><SheetContent><SheetHeader><SheetTitle>{t(editingCommitment ? "editCommitment" : "addCommitment")}</SheetTitle><SheetDescription>{t("commitmentFormDescription")}</SheetDescription></SheetHeader><form onSubmit={submitCommitment} className="mt-6 space-y-6">
      <BudgetFormSection title={t("commitmentDetailsSection")}><Field id="commitment-reference" label={t("purchaseOrderNumber")} required hint={t("commitmentReferenceHint")}><Input id="commitment-reference" required value={commitmentForm.purchaseOrderNumber} onChange={event => setCommitmentForm({ ...commitmentForm, purchaseOrderNumber: event.target.value })} /></Field><Field id="commitment-description" label={t("description")}><Textarea id="commitment-description" value={commitmentForm.description ?? ""} onChange={event => setCommitmentForm({ ...commitmentForm, description: event.target.value })} rows={3} /></Field><Field id="commitment-vendor" label={t("vendorName")} hint={t("commitmentVendorHint")}><Input id="commitment-vendor" value={commitmentForm.vendorName ?? ""} onChange={event => setCommitmentForm({ ...commitmentForm, vendorName: event.target.value })} /></Field></BudgetFormSection>
      <BudgetFormSection title={t("commitmentFinancialSection")}><Field id="commitment-amount" label={t("amount")} required hint={t("commitmentAmountHint")}><Input id="commitment-amount" required type="number" min="0.01" step="0.01" value={commitmentForm.amount} onChange={event => setCommitmentForm({ ...commitmentForm, amount: Number(event.target.value) })} /></Field><Field id="commitment-currency" label={t("currencyLabel")} hint={t("commitmentCurrencyHint")}><div id="commitment-currency" className="rounded-md border bg-muted/20 px-3 py-2 text-sm font-medium">{currency}</div></Field></BudgetFormSection>
      <BudgetFormSection title={t("commitmentTimingSection")}><div className="grid gap-3 sm:grid-cols-2"><Field id="commitment-status" label={t("status")} hint={t("commitmentStatusHint")}><div id="commitment-status" className="rounded-md border bg-muted/20 px-3 py-2 text-sm font-medium">{commitmentStatusLabel(t, editingCommitment?.status ?? "Open")}</div></Field><Field id="commitment-date" label={t("commitmentDate")} required><Input id="commitment-date" required type="date" value={commitmentForm.commitmentDate} onChange={event => setCommitmentForm({ ...commitmentForm, commitmentDate: event.target.value })} /></Field></div><Field id="commitment-delivery" label={t("expectedDeliveryDate")} hint={t("expectedDeliveryHint")}><Input id="commitment-delivery" type="date" value={commitmentForm.expectedDeliveryDate ?? ""} onChange={event => setCommitmentForm({ ...commitmentForm, expectedDeliveryDate: event.target.value || undefined })} /></Field></BudgetFormSection>
      {commitmentFormError ? <p role="alert" className="rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-sm text-destructive">{commitmentFormError}</p> : null}
      <SheetFooter className="flex-row justify-end gap-2"><Button type="button" variant="outline" onClick={() => setCommitmentOpen(false)}>{common("cancel")}</Button><Button type="submit" disabled={mutations.createCommitment.isPending || mutations.updateCommitment.isPending}>{(mutations.createCommitment.isPending || mutations.updateCommitment.isPending) ? <><Loader2 className="me-1.5 h-4 w-4 animate-spin" />{common("saving")}</> : editingCommitment ? t("saveChanges") : t("saveCommitment")}</Button></SheetFooter>
    </form></SheetContent></Sheet>

    {budgetListOpen ? <FinancialListSheet open={budgetListOpen} onOpenChange={setBudgetListOpen} title={t("budgetLines")} description={t("budgetLinesDescription")} total={budgets.data?.totalCount ?? 0} page={budgetPage} totalPages={budgetList.data?.totalPages ?? 0} loading={budgetList.isLoading} error={budgetList.isError} onRetry={() => void budgetList.refetch()} onPageChange={setBudgetPage} t={t} common={common}>
      {budgetList.data?.items.length ? <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("budgetLine")}</TableHead><TableHead className="text-end">{t("plannedAmount")}</TableHead><TableHead className="text-end">{t("approvedAmount")}</TableHead><TableHead className="text-end">{t("forecastAmount")}</TableHead><TableHead>{t("fiscalPeriod")}</TableHead><TableHead>{t("status")}</TableHead><TableHead className="text-end">{t("actions")}</TableHead></TableRow></TableHeader><TableBody>{budgetList.data.items.map(item => <BudgetLineTableRow key={item.id} item={item} currency={currency} canManage={canManage} onEdit={openBudget} onDelete={(value) => void deleteBudget(value)} t={t} />)}</TableBody></Table></div> : null}
    </FinancialListSheet> : null}
    {costListOpen ? <FinancialListSheet open={costListOpen} onOpenChange={setCostListOpen} title={t("actualCosts")} description={t("actualCostsDescription")} total={costs.data?.totalCount ?? 0} page={costPage} totalPages={costList.data?.totalPages ?? 0} loading={costList.isLoading} error={costList.isError} onRetry={() => void costList.refetch()} onPageChange={setCostPage} t={t} common={common}>
      {costList.data?.items.length ? <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("description")}</TableHead><TableHead>{t("category")}</TableHead><TableHead>{t("vendorName")}</TableHead><TableHead>{t("costType")}</TableHead><TableHead>{t("incurredDate")}</TableHead><TableHead className="text-end">{t("amount")}</TableHead><TableHead className="text-end">{t("taxAmount")}</TableHead><TableHead>{t("status")}</TableHead><TableHead className="text-end">{t("actions")}</TableHead></TableRow></TableHeader><TableBody>{costList.data.items.map(item => <TableRow key={item.id}><TableCell className="font-medium max-w-[200px] truncate">{item.description || "—"}</TableCell><TableCell className="max-w-[120px] truncate">{item.category}</TableCell><TableCell className="max-w-[120px] truncate">{item.vendorName || "—"}</TableCell><TableCell className="max-w-[120px] truncate">{item.type}</TableCell><TableCell className="whitespace-nowrap">{formatDate(item.incurredDate)}</TableCell><TableCell className="text-end font-semibold tabular-nums">{money(item.amount, item.currency)}</TableCell><TableCell className="text-end tabular-nums text-muted-">{item.taxAmount != null ? money(item.taxAmount, item.currency) : "—"}</TableCell><TableCell><Badge status={item.status}>{costStatusLabel(t, item.status)}</Badge></TableCell><TableCell><div className="nexus-table-actions flex justify-end gap-1">{canManage ? <><Button type="button" size="icon" variant="ghost" aria-label={common("edit")} onClick={() => openCost(item)}><Pencil className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="ghost" className="text-destructive hover:text-destructive" aria-label={common("delete")} onClick={() => void deleteCost(item)}><Trash2 className="h-3.5 w-3.5" /></Button></> : null}</div></TableCell></TableRow>)}</TableBody></Table></div> : null}
    </FinancialListSheet> : null}
    {commitmentListOpen ? <FinancialListSheet open={commitmentListOpen} onOpenChange={setCommitmentListOpen} title={t("commitments")} description={t("commitmentsDescription")} total={commitments.data?.totalCount ?? 0} page={commitmentPage} totalPages={commitmentList.data?.totalPages ?? 0} loading={commitmentList.isLoading} error={commitmentList.isError} onRetry={() => void commitmentList.refetch()} onPageChange={setCommitmentPage} t={t} common={common}>
      {commitmentActionError ? <p role="alert" className="mb-3 rounded-md border border-destructive/20 bg-destructive/5 px-3 py-2 text-xs text-destructive">{commitmentActionError}</p> : null}
      {commitmentList.data?.items.length ? <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("purchaseOrderNumber")}</TableHead><TableHead>{t("vendorName")}</TableHead><TableHead>{t("description")}</TableHead><TableHead className="text-end">{t("amount")}</TableHead><TableHead>{t("status")}</TableHead><TableHead>{t("commitmentDate")}</TableHead><TableHead className="text-end">{t("actions")}</TableHead></TableRow></TableHeader><TableBody>{commitmentList.data.items.map(item => <TableRow key={item.id} className={isTerminalCommitmentStatus(item.status) ? "text-muted-foreground" : undefined}><TableCell className="font-medium">{item.purchaseOrderNumber}</TableCell><TableCell>{item.vendorName || "—"}</TableCell><TableCell className="max-w-[220px] truncate">{item.description || "—"}</TableCell><TableCell className="text-end font-semibold tabular-nums">{money(item.amount, item.currency)}</TableCell><TableCell><Badge status={item.status}>{commitmentStatusLabel(t, item.status)}</Badge></TableCell><TableCell className="whitespace-nowrap"><span>{formatDate(item.commitmentDate)}</span>{item.expectedDeliveryDate ? <span className="mt-0.5 block text-[11px] text-muted-foreground">{t("expectedDeliveryDate")}: {formatDate(item.expectedDeliveryDate)}</span> : null}</TableCell><TableCell><div className="nexus-table-actions flex justify-end gap-1">{canManage && isActiveStatus(item.status) ? <><Button type="button" size="icon" variant="ghost" aria-label={`${common("edit")}: ${item.purchaseOrderNumber}`} title={common("edit")} disabled={mutations.updateCommitment.isPending} onClick={() => openCommitment(item)}><Pencil className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="ghost" aria-label={`${t("closeCommitment")}: ${item.purchaseOrderNumber}`} title={t("closeCommitment")} disabled={mutations.closeCommitment.isPending} onClick={() => void transitionCommitment(item, "close")}><CircleCheck className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="ghost" aria-label={`${t("cancelCommitment")}: ${item.purchaseOrderNumber}`} title={t("cancelCommitment")} disabled={mutations.cancelCommitment.isPending} onClick={() => void transitionCommitment(item, "cancel")}><CircleX className="h-3.5 w-3.5" /></Button></> : null}</div></TableCell></TableRow>)}</TableBody></Table></div> : null}
    </FinancialListSheet> : null}
  </div>;
}

function CommitmentRow({ item, canManage, onEdit, onClose, onCancel, t, common }: { item: PurchaseCommitmentDto; canManage: boolean; onEdit: (_item: PurchaseCommitmentDto) => void; onClose: (_item: PurchaseCommitmentDto) => void; onCancel: (_item: PurchaseCommitmentDto) => void; t: ReturnType<typeof useTranslations>; common: ReturnType<typeof useTranslations> }) {
  const terminal = isTerminalCommitmentStatus(item.status);
  return <div className={`flex items-center gap-3 py-3 ${terminal ? "opacity-70" : ""}`}>
    <div className="min-w-0 flex-1">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1"><p className="text-sm font-semibold">{item.purchaseOrderNumber}</p><Badge status={item.status}>{commitmentStatusLabel(t, item.status)}</Badge></div>
      <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground"><span>{item.vendorName || "—"}</span><span>{formatDate(item.commitmentDate)}</span>{item.expectedDeliveryDate ? <span>{t("expectedDeliveryDate")}: {formatDate(item.expectedDeliveryDate)}</span> : null}</div>
      {item.description ? <p className="mt-1 truncate text-xs text-muted-foreground">{item.description}</p> : null}
    </div>
    <span className={`text-sm font-semibold tabular-nums ${terminal ? "text-muted-foreground line-through" : ""}`}>{money(item.amount, item.currency)}</span>
    {canManage && isActiveStatus(item.status) ? <div className="flex shrink-0 items-center gap-1"><Button type="button" size="icon" variant="ghost" aria-label={`${common("edit")}: ${item.purchaseOrderNumber}`} title={common("edit")} onClick={() => onEdit(item)}><Pencil className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="ghost" aria-label={`${t("closeCommitment")}: ${item.purchaseOrderNumber}`} title={t("closeCommitment")} onClick={() => onClose(item)}><CircleCheck className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="ghost" aria-label={`${t("cancelCommitment")}: ${item.purchaseOrderNumber}`} title={t("cancelCommitment")} onClick={() => onCancel(item)}><CircleX className="h-3.5 w-3.5" /></Button></div> : null}
  </div>;
}

function CostRow({ item, canManage, onEdit, onDelete, t, common }: { item: CostEntryDto; canManage: boolean; onEdit: (_item: CostEntryDto) => void; onDelete: (_item: CostEntryDto) => void; t: ReturnType<typeof useTranslations>; common: ReturnType<typeof useTranslations> }) {
  return <div className="flex items-center gap-3 py-3">
    <div className="min-w-0 flex-1">
      <p className="text-sm font-medium">{item.description || item.category}</p>
      <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
        <span>{item.category}</span>
        {item.vendorName ? <span>{item.vendorName}</span> : null}
        <span>{formatDate(item.incurredDate)}</span>
        <Badge status={item.status}>{costStatusLabel(t, item.status)}</Badge>
      </div>
    </div>
    <span className="text-sm font-semibold tabular-nums">{money(item.amount, item.currency)}</span>
    {canManage ? <>
      <Button type="button" size="icon" variant="ghost" aria-label={common("edit")} onClick={() => onEdit(item)}><Pencil className="h-3.5 w-3.5" /></Button>
      <Button type="button" size="icon" variant="ghost" className="text-destructive hover:text-destructive" aria-label={common("delete")} onClick={() => onDelete(item)}><Trash2 className="h-3.5 w-3.5" /></Button>
    </> : null}
  </div>;
}

function KpiCard({ label, hint, value, accent, semantic }: { label: string; hint?: string; value: string; accent?: "warning" | "danger" | "success"; semantic?: "navy" | "indigo" | "blue" | "amber" | "violet" | "green" | "red" }) {
  const accentClass = accent === "warning" ? "border-amber-200 bg-amber-50 dark:border-amber-800 dark:bg-amber-950" : accent === "danger" ? "border-destructive/20 bg-destructive/5" : accent === "success" ? "border-emerald-200 bg-emerald-50 dark:border-emerald-800 dark:bg-emerald-950" : "";
  const valueClass = accent === "danger" ? "text-destructive" : accent === "success" ? "text-emerald-600 dark:text-emerald-400" : "";
  const semanticIconBg = semantic ? `fin-tint-${semantic}` : "";
  const semanticIconText = semantic ? `text-fin-${semantic}` : "";
  return (
    <TooltipProvider>
      <TooltipUI>
        <TooltipTrigger asChild>
          <div className={`rounded-lg border p-3 ${accentClass}`}>
            <div className="flex items-center gap-2">
              {semantic ? <span className={`inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-md ${semanticIconBg} ${semanticIconText}`}><WalletCards className="h-3.5 w-3.5" /></span> : null}
              <p className="text-[10px] uppercase tracking-wide text-muted-foreground">{label}</p>
            </div>
            <p className={`mt-1 text-lg font-semibold tabular-nums ${valueClass}`}>{value}</p>
          </div>
        </TooltipTrigger>
        {hint ? <TooltipContent><p className="max-w-xs text-xs">{hint}</p></TooltipContent> : null}
      </TooltipUI>
    </TooltipProvider>
  );
}

function SpendingRow({ label, value, currency, color, hint }: { label: string; value: number | null | undefined; currency: string; color: string; hint?: string }) {
  return (
    <TooltipProvider>
      <TooltipUI>
        <TooltipTrigger asChild>
          <div className="flex items-center justify-between">
            <span className="flex items-center gap-2 text-muted-foreground">
              <span className="inline-block h-2.5 w-2.5 rounded-full" style={{ backgroundColor: color }} />
              {label}
            </span>
            <span className="font-medium tabular-nums">{money(value, currency)}</span>
          </div>
        </TooltipTrigger>
        {hint ? <TooltipContent><p className="max-w-xs text-xs">{hint}</p></TooltipContent> : null}
      </TooltipUI>
    </TooltipProvider>
  );
}

function BudgetLinesView({ items, currency, canManage, expandedBudgetId, onToggleExpanded, onEdit, onDelete, t }: { items: readonly BudgetDto[]; currency: string; canManage: boolean; expandedBudgetId: string | null; onToggleExpanded: (_id: string) => void; onEdit: (_item: BudgetDto) => void; onDelete: (_item: BudgetDto) => void; t: ReturnType<typeof useTranslations> }) {
  return <>
    <div className="hidden md:block"><Table><TableHeader><TableRow><TableHead>{t("budgetLine")}</TableHead><TableHead className="text-end">{t("plannedAmount")}</TableHead><TableHead className="text-end">{t("approvedAmount")}</TableHead><TableHead className="text-end">{t("forecastAmount")}</TableHead><TableHead>{t("fiscalPeriod")}</TableHead><TableHead>{t("status")}</TableHead><TableHead className="text-end">{t("actions")}</TableHead></TableRow></TableHeader><TableBody>{items.map((item) => <BudgetLineTableRow key={item.id} item={item} currency={currency} canManage={canManage} onEdit={onEdit} onDelete={onDelete} t={t} />)}</TableBody></Table></div>
    <div className="space-y-2 md:hidden">{items.map((item) => <BudgetLineMobileRow key={item.id} item={item} currency={currency} canManage={canManage} expanded={expandedBudgetId === item.id} onToggle={() => onToggleExpanded(item.id)} onEdit={onEdit} onDelete={onDelete} t={t} />)}</div>
  </>;
}

function BudgetLineTableRow({ item, currency, canManage, onEdit, onDelete, t }: { item: BudgetDto; currency: string; canManage: boolean; onEdit: (_item: BudgetDto) => void; onDelete: (_item: BudgetDto) => void; t: ReturnType<typeof useTranslations> }) {
  const variance = budgetVariance(item);
  const lineCurrency = item.currency || currency;
  return <TableRow><TableCell><p className="font-medium">{item.name}</p><p className="mt-0.5 text-[11px] text-muted-foreground">{item.category}</p></TableCell><TableCell className="text-end font-medium tabular-nums">{money(item.plannedAmount, lineCurrency)}</TableCell><TableCell className="text-end font-semibold tabular-nums">{money(item.approvedAmount, lineCurrency)}</TableCell><TableCell className="text-end tabular-nums"><span className="font-medium">{money(item.forecastAmount, lineCurrency)}</span>{variance !== null ? <span className="mt-0.5 block text-[10px] text-muted-foreground">{t("approvedMinusForecast")}: {money(variance, lineCurrency)} · {varianceLabel(t, variance)}</span> : null}</TableCell><TableCell className="whitespace-nowrap">{fiscalPeriodLabel(t, item)}</TableCell><TableCell><Badge status={item.status}>{budgetStatusLabel(t, item.status)}</Badge></TableCell><TableCell><div className="nexus-table-actions flex justify-end gap-1">{canManage ? <><Button type="button" size="icon" variant="ghost" className="h-8 w-8" aria-label={`${t("editBudgetLine")}: ${item.name}`} title={`${t("editBudgetLine")}: ${item.name}`} onClick={() => onEdit(item)}><Pencil className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="ghost" className="h-8 w-8 text-destructive hover:text-destructive" aria-label={`${t("deleteBudgetLine")}: ${item.name}`} title={`${t("deleteBudgetLine")}: ${item.name}`} onClick={() => onDelete(item)}><Trash2 className="h-3.5 w-3.5" /></Button></> : null}</div></TableCell></TableRow>;
}

function BudgetLineMobileRow({ item, currency, canManage, expanded, onToggle, onEdit, onDelete, t }: { item: BudgetDto; currency: string; canManage: boolean; expanded: boolean; onToggle: () => void; onEdit: (_item: BudgetDto) => void; onDelete: (_item: BudgetDto) => void; t: ReturnType<typeof useTranslations> }) {
  const variance = budgetVariance(item);
  const lineCurrency = item.currency || currency;
  return <div className="rounded-lg border p-3"><div className="flex items-start gap-3"><button type="button" className="min-w-0 flex-1 text-start" aria-expanded={expanded} onClick={onToggle}><p className="truncate text-sm font-semibold">{item.name}</p><p className="mt-1 truncate text-xs text-muted-foreground">{item.category} · {fiscalPeriodLabel(t, item)}</p><div className="mt-2 flex items-center gap-2"><span className="text-base font-semibold tabular-nums">{money(item.approvedAmount, lineCurrency)}</span><Badge status={item.status}>{budgetStatusLabel(t, item.status)}</Badge></div></button><div className="flex shrink-0 items-center gap-1">{canManage ? <><Button type="button" size="icon" variant="ghost" className="h-8 w-8" aria-label={`${t("editBudgetLine")}: ${item.name}`} title={`${t("editBudgetLine")}: ${item.name}`} onClick={() => onEdit(item)}><Pencil className="h-3.5 w-3.5" /></Button><Button type="button" size="icon" variant="ghost" className="h-8 w-8 text-destructive hover:text-destructive" aria-label={`${t("deleteBudgetLine")}: ${item.name}`} title={`${t("deleteBudgetLine")}: ${item.name}`} onClick={() => onDelete(item)}><Trash2 className="h-3.5 w-3.5" /></Button></> : null}<Button type="button" size="icon" variant="ghost" className="h-8 w-8" aria-label={expanded ? t("collapseBudgetLine") : t("expandBudgetLine")} title={expanded ? t("collapseBudgetLine") : t("expandBudgetLine")} onClick={onToggle}>{expanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}</Button></div></div>{expanded ? <div className="mt-3 grid grid-cols-2 gap-2 border-t pt-3 text-xs"><AmountDetail label={t("plannedAmount")} value={money(item.plannedAmount, lineCurrency)} /><AmountDetail label={t("forecastAmount")} value={money(item.forecastAmount, lineCurrency)} />{variance !== null ? <div className="col-span-2 rounded-md bg-muted/30 px-2.5 py-2 text-muted-foreground">{t("approvedMinusForecast")}: <span className="font-medium text-foreground">{money(variance, lineCurrency)}</span> · {varianceLabel(t, variance)}</div> : null}</div> : null}</div>;
}

function AmountDetail({ label, value }: { label: string; value: string }) { return <div className="rounded-md bg-muted/20 px-2.5 py-2"><p className="text-[10px] uppercase tracking-wide text-muted-foreground">{label}</p><p className="mt-1 font-medium tabular-nums">{value}</p></div>; }
function BudgetLinesSkeleton() { return <div className="space-y-2">{[1, 2, 3].map((value) => <Skeleton key={value} className="h-14 w-full" />)}</div>; }
function BudgetLinesState({ message, action }: { message: string; action?: React.ReactNode }) { return <div className="flex min-h-32 flex-col items-center justify-center rounded-lg border border-dashed bg-muted/20 px-6 py-8 text-center"><WalletCards className="h-8 w-8 text-muted-foreground/50" /><p className="mt-2 max-w-md text-sm text-muted-foreground">{message}</p>{action ? <div className="mt-3">{action}</div> : null}</div>; }
function BudgetFormSection({ title, children }: { title: string; children: React.ReactNode }) { return <section className="space-y-3"><h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{title}</h3>{children}</section>; }
function Field({ id, label, required = false, hint, children }: { id: string; label: string; required?: boolean; hint?: string; children: React.ReactNode }) { return <div className="space-y-1.5"><Label htmlFor={id} className="text-xs font-medium">{label}{required ? <span className="ms-1 text-destructive" aria-hidden="true">*</span> : null}</Label>{children}{hint ? <p className="text-[11px] text-muted-foreground">{hint}</p> : null}</div>; }
function DataCard({ title, description, meta, action, children }: { title: string; description?: string; meta?: React.ReactNode; action?: React.ReactNode; children: React.ReactNode }) { return <Card className="shadow-none"><CardHeader className="flex flex-row items-start justify-between gap-3"><div className="min-w-0"><h2 className="text-sm font-semibold">{title}</h2>{description ? <p className="mt-1 text-xs text-muted-foreground">{description}</p> : null}{meta ? <div className="mt-2">{meta}</div> : null}</div>{action}</CardHeader><CardContent>{children}</CardContent></Card>; }
function FinancialListSheet({ open, onOpenChange, title, description, total, page, totalPages, loading, error, onRetry, onPageChange, t, common, children }: { open: boolean; onOpenChange: (_open: boolean) => void; title: string; description?: string; total: number; page: number; totalPages: number; loading: boolean; error: boolean; onRetry: () => void; onPageChange: (_page: number) => void; t: ReturnType<typeof useTranslations>; common: ReturnType<typeof useTranslations>; children: React.ReactNode }) {
  return <Sheet open={open} onOpenChange={onOpenChange}><SheetContent side="end" className="flex w-full flex-col overflow-hidden p-0 sm:max-w-5xl"><SheetHeader className="shrink-0 border-b px-6 py-5 text-start"><SheetTitle>{title}</SheetTitle>{description ? <SheetDescription>{description}</SheetDescription> : null}<p className="text-xs text-muted-foreground">{t("totalRecords", { count: total })}</p></SheetHeader><div className="min-h-0 flex-1 overflow-y-auto px-6 py-5"><div className="min-w-0">{loading ? <div className="space-y-3"><Skeleton className="h-10 w-full" /><Skeleton className="h-10 w-full" /><Skeleton className="h-10 w-full" /></div> : error ? <div className="flex min-h-40 flex-col items-center justify-center gap-3 text-center"><AlertCircle className="h-7 w-7 text-destructive/70" /><p role="alert" className="text-sm text-destructive">{common("error")}</p><Button variant="outline" size="sm" onClick={onRetry}><RotateCcw className="me-1.5 h-3.5 w-3.5" />{common("retry")}</Button></div> : total === 0 ? <Empty text={t("noRecords")} /> : children}</div></div>{!loading && !error && totalPages > 1 ? <div className="shrink-0 border-t px-6 py-4"><ListPagination page={page} totalPages={totalPages} onPageChange={onPageChange} previous={common("previous")} next={common("next")} pageOf={t("pageOf", { page, totalPages })} disabled={false} /></div> : null}</SheetContent></Sheet>;
}
function ListPagination({ page, totalPages, onPageChange, previous, next, pageOf, disabled }: { page: number; totalPages: number; onPageChange: (_page: number) => void; previous: string; next: string; pageOf: string; disabled: boolean }) { return <TablePagination page={page} totalPages={totalPages} summary={pageOf} previousLabel={previous} nextLabel={next} onPageChange={onPageChange} disabled={disabled} />; }
function Empty({ text }: { text: string }) { return <div className="flex min-h-16 items-center justify-center rounded-lg border border-dashed text-xs text-muted-foreground"><WalletCards className="me-2 h-4 w-4" />{text}</div>; }
