import * as React from "react";
import { useNavigate, useOutletContext } from "react-router-dom";
import { AlertTriangle, Archive, Ban, Pencil, Plus, Receipt, Trash2, TrendingDown } from "lucide-react";
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
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { PlannedPaymentsPage } from "@features/finance/PlannedPaymentsPage";
import { ProjectFinanceOverview } from "./ProjectFinanceOverview";
import { ProjectFinancialWorkspace, type FinanceWorkspaceSection } from "./ProjectFinancialWorkspace";
import { ProjectPaymentRequests } from "./ProjectPaymentRequests";
import { useProjectAnnualBudgets } from "./api/project-annual-budgets";

type ProjectContext = {
  projectId: string;
  project: { id: string; publicId?: string; name?: string; code?: string; currency?: string };
};

const FINANCE_SECTIONS = [
  { id: "overview", labelKey: "projects.financeWorkspaceOverview", icon: LayoutDashboard },
  { id: "annualBudgets", labelKey: "projects.financeWorkspaceAnnualBudgets", icon: Landmark },
  { id: "paymentRequests", labelKey: "projects.financeWorkspacePaymentRequests", icon: ClipboardList },
  { id: "settlements", labelKey: "projects.financeWorkspaceSettlements", icon: Banknote },
  { id: "closing", labelKey: "projects.financeWorkspaceFiscalYearClosing", icon: CalendarDays },
  { id: "auditTrail", labelKey: "projects.financeWorkspaceAuditTrail", icon: History },
] as const satisfies ReadonlyArray<{
  id: FinanceWorkspaceSection;
  labelKey: string;
  icon: React.ComponentType<{ className?: string }>;
}>;

export function ProjectFinanceTab() {
  const { projectId, project } = useOutletContext<ProjectContext>();
  const navigate = useNavigate();
  const { t, formatCurrency } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();

  const canViewFinancialFoundation = Boolean(user
    && hasPermission(user.permissions, Permissions.ProjectFinancialFoundation.View));
  const canViewAnnualBudgets = Boolean(user && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.View));
  const canViewPaymentRequests = Boolean(user && hasPermission(user.permissions, Permissions.ProjectPaymentRequest.View));
  const canViewSettlements = Boolean(user && hasPermission(user.permissions, Permissions.ProjectSettlement.View));
  const canViewPaymentObligations = Boolean(user && hasPermission(user.permissions, Permissions.PaymentObligation.View));
  const canReviewPaymentRequests = Boolean(user
    && hasPermission(user.permissions, Permissions.ProjectPaymentRequest.Approve));
  const canReviewReversals = Boolean(user
    && hasPermission(user.permissions, Permissions.ProjectPaymentReversal.Approve));
  const canViewAnnualBudgetClosing = Boolean(user && hasPermission(user.permissions, Permissions.ProjectAnnualBudgetClosing.View));
  const canViewAuditTrail = Boolean(user && hasPermission(user.permissions, Permissions.ProjectFinancialAuditTrail.View));
  const canViewFinanceShell = Boolean(user && hasPermission(user.permissions, Permissions.ProjectFinance.View));
  const canView = canViewFinancialFoundation || canViewAnnualBudgets || canViewPaymentRequests
    || canViewSettlements || canViewPaymentObligations || canReviewPaymentRequests || canReviewReversals
    || canViewAnnualBudgetClosing || canViewAuditTrail || canViewFinanceShell;
  const canWriteInScope = canMutate && !effectiveReadOnly;
  const canCreateAnnualBudget = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.Create));
  const canManage = canCreateAnnualBudget;
  const canEditAnnualBudget = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.EditDraft));
  const canConfigureDraftAnnualBudgetApproval = Boolean(user && canViewAnnualBudgets && canEditAnnualBudget
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.ConfigureDraftApproval));
  const canDeleteAnnualBudget = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.Delete));
  const canWithdrawAnnualBudget = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.Withdraw));
  const canSubmitAnnualBudget = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.SubmitForReview));
  const canReviseAnnualBudget = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.ReviseApproved));
  const canApproveAnnualBudget = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.Approve));
  const canPrepareAnnualBudgetClosing = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.Close));
  const canApproveAnnualBudgetClosing = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.CloseApprove));
  const canCarryForwardAnnualBudget = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.CarryForward));
  const canRecordSettlements = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.PaymentSettlement.Record));
  const canCreateReversals = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectPaymentReversal.Create));
  const canApproveReversals = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectPaymentReversal.Approve));
  const canCreatePaymentRequests = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectPaymentRequest.Create));
  const canEditPaymentRequests = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectPaymentRequest.EditDraft));
  const canSubmitPaymentRequests = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectPaymentRequest.Submit));
  const canApprovePaymentRequests = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectPaymentRequest.Approve));
  const canCancelPaymentRequests = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.ProjectPaymentRequest.Cancel));

  const annualBudgetYearParam = searchParams.get("fiscalYear");
  const parsedAnnualBudgetYear = annualBudgetYearParam && /^\d{1,4}$/.test(annualBudgetYearParam)
    ? Number(annualBudgetYearParam)
    : null;
  const requestedAnnualBudgetYear = parsedAnnualBudgetYear !== null && parsedAnnualBudgetYear > 0
    ? parsedAnnualBudgetYear
    : null;
  const [activeSection, setActiveSection] = React.useState<FinanceWorkspaceSection>(
    searchParams.get("section") === "annualBudgets" ? "annualBudgets" : "overview",
  );
  const [selectedFiscalYear, setSelectedFiscalYear] = React.useState<number | null>(requestedAnnualBudgetYear);
  const annualBudgetContext = useProjectAnnualBudgets(projectId, canViewAnnualBudgets && Boolean(projectId));

  const visibleSections = FINANCE_SECTIONS.filter(({ id }) => {
    switch (id) {
      case "overview": return canViewFinancialFoundation;
      case "annualBudgets": return canViewAnnualBudgets;
      case "paymentRequests": return canViewPaymentRequests || canReviewPaymentRequests || canReviewReversals;
      case "settlements": return canViewSettlements || canViewPaymentObligations;
      case "closing": return canViewAnnualBudgetClosing;
      case "auditTrail": return canViewAuditTrail;
      default: return false;
    }
  });
  const visibleSectionIds = visibleSections.map(item => item.id).join("|");
  const firstVisibleSection = visibleSections[0]?.id ?? "overview";
  const section = visibleSections.some(item => item.id === activeSection) ? activeSection : firstVisibleSection;

  React.useEffect(() => {
    if (!visibleSections.some(item => item.id === activeSection)) setActiveSection(firstVisibleSection);
  }, [activeSection, firstVisibleSection, visibleSectionIds]);

  React.useEffect(() => {
    if (selectedFiscalYear === null && annualBudgetContext.data) {
      setSelectedFiscalYear(annualBudgetContext.data.currentFiscalYear);
    }
  }, [annualBudgetContext.data?.currentFiscalYear, selectedFiscalYear]);

  if (!canView) {
    return <Card><CardContent className="p-6 text-sm text-muted-foreground">
      {t("projects.financialsPermissionRequired")}
    </CardContent></Card>;
  }

  if (visibleSections.length === 0) {
    return <Card><CardContent className="p-6 text-sm text-muted-foreground">
      <h1 className="text-lg font-semibold text-foreground">{t("projects.financeWorkspaceTitle")}</h1>
      <p className="mt-2">{t("projects.financeWorkspaceSectionPermissionRequired")}</p>
    </CardContent></Card>;
  }

  const fiscalYears = [...new Set([
    ...(annualBudgetContext.data ? [annualBudgetContext.data.currentFiscalYear] : []),
    ...(annualBudgetContext.data?.items ?? []).map(item => item.fiscalYear),
  ])].sort((left, right) => right - left);
  const activeSectionLabel = visibleSections.find(item => item.id === section)?.labelKey
    ?? FINANCE_SECTIONS[0].labelKey;
  const overviewActive = section === "overview";
  const paymentRequestsActive = section === "paymentRequests";
  const settlementsActive = section === "settlements";
  const selectedProjectName = project.name || project.code || "-";

  React.useEffect(() => {
    if (searchParams.get("section") === "annualBudgets" && visibleSections.some(item => item.id === "annualBudgets")) {
      setActiveSection("annualBudgets");
    }
    if (requestedAnnualBudgetYear !== null && requestedAnnualBudgetYear > 0) {
      setSelectedFiscalYear(requestedAnnualBudgetYear);
    }
  }, [projectId, requestedAnnualBudgetYear, searchParams, visibleSectionIds]);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="text-base font-semibold">{t("projects.financialSummary")}</h2><p className="mt-1 text-sm text-muted-foreground">{t("projects.financialSummaryDescription")}</p></div><div className="flex flex-wrap gap-2"><Button size="sm" variant="outline" onClick={() => navigate(`/costs/payments?projectId=${encodeURIComponent(projectId)}`)}><Receipt className="me-1.5 h-4 w-4" />{t("payments.plannedPayments")}</Button>{canManage ? <><Button size="sm" variant="outline" onClick={() => openBudget()}><Plus className="me-1.5 h-4 w-4" />{t("projects.addBudgetLine")}</Button><Button size="sm" variant="outline" onClick={() => openCost()}><Plus className="me-1.5 h-4 w-4" />{t("projects.addCostEntry")}</Button><Button size="sm" onClick={() => openCommitment()}><Plus className="me-1.5 h-4 w-4" />{t("projects.addCommitment")}</Button></> : null}</div></div>
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
    </div>
  );
}
