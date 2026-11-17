import * as React from "react";
import { useOutletContext, useSearchParams } from "react-router-dom";
import { Banknote, CalendarDays, ClipboardList, History, Landmark, LayoutDashboard } from "lucide-react";
import { Card, CardContent } from "@components/ui/Card";
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
  const [searchParams] = useSearchParams();
  const { t } = useI18n();
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
  const canManageAssignmentRecovery = Boolean(user && canWriteInScope
    && hasPermission(user.permissions, Permissions.AssignmentRecovery.Manage));
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
      <header className="flex flex-col gap-4 border-b pb-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold tracking-tight">{t("projects.financeWorkspaceTitle")}</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {t("projects.financeWorkspaceProjectLabel")}: {selectedProjectName}
            {project.code && project.name ? ` (${project.code})` : ""}
          </p>
        </div>
        {canViewAnnualBudgets ? <div className="w-full sm:w-56">
          <label htmlFor="project-finance-fiscal-year" className="text-sm font-medium">
            {t("projects.financeWorkspaceFiscalYearLabel")}
          </label>
          <select
            id="project-finance-fiscal-year"
            className="mt-1 flex h-10 w-full rounded-md border bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            value={selectedFiscalYear ?? ""}
            disabled={annualBudgetContext.isLoading || fiscalYears.length === 0}
            onChange={event => setSelectedFiscalYear(Number(event.target.value))}
          >
            {fiscalYears.length === 0 ? <option value="">
              {annualBudgetContext.isLoading ? t("common.loading") : t("projects.financeKpiNoYearAvailable")}
            </option> : null}
            {fiscalYears.map(year => {
              const fiscalYear = annualBudgetContext.data?.items.find(item => item.fiscalYear === year);
              const label = fiscalYear?.fiscalYearLabel ?? `${t("projects.fiscalYearShort")} ${year}`;
              return <option key={year} value={year}>{label}</option>;
            })}
          </select>
        </div> : null}
      </header>

      <div className="grid gap-4 lg:grid-cols-[15rem_minmax(0,1fr)]">
        <nav aria-label={t("projects.financeWorkspaceNavigation")}
          className="sticky top-4 hidden self-start rounded-lg border bg-card p-2 lg:block">
          <ul className="space-y-1">
            {visibleSections.map(({ id, labelKey, icon: Icon }) => <li key={id}>
              <button
                type="button"
                aria-current={section === id ? "page" : undefined}
                aria-controls="project-finance-workspace"
                onClick={() => setActiveSection(id)}
                className={`flex min-h-10 w-full items-center gap-3 rounded-md border-s-2 px-3 py-2 text-start text-sm transition-colors hover:bg-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${section === id ? "border-s-primary bg-accent font-semibold text-accent-foreground" : "border-s-transparent text-muted-foreground"}`}
              >
                <Icon aria-hidden="true" className="size-4 shrink-0" />
                <span>{t(labelKey)}</span>
              </button>
            </li>)}
          </ul>
        </nav>

        <main id="project-finance-workspace" aria-labelledby="project-finance-section-title"
          className="min-w-0 space-y-5">
          <div className="lg:hidden">
            <label htmlFor="project-finance-section-select" className="text-sm font-medium">
              {t("projects.financeWorkspaceMobileNavigation")}
            </label>
            <select
              id="project-finance-section-select"
              className="mt-1 flex h-10 w-full rounded-md border bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              value={section}
              onChange={event => setActiveSection(event.target.value as FinanceWorkspaceSection)}
            >
              {visibleSections.map(item => <option key={item.id} value={item.id}>{t(item.labelKey)}</option>)}
            </select>
          </div>

          <h2 id="project-finance-section-title" className="sr-only">{t(activeSectionLabel)}</h2>

          {canViewFinancialFoundation ? <section hidden={!overviewActive} aria-label={t("projects.financeWorkspaceOverview")}>
            <ProjectFinanceOverview
              projectId={projectId}
              selectedFiscalYear={selectedFiscalYear}
              active={overviewActive}
              canViewFoundation={canViewFinancialFoundation}
              canViewAnnualBudgets={canViewAnnualBudgets}
              canViewPaymentRequests={canViewPaymentRequests}
              canViewAnnualBudgetClosing={canViewAnnualBudgetClosing}
              canManage={canManage}
              onOpenAnnualBudgets={() => setActiveSection("annualBudgets")}
            />
          </section> : null}

          <ProjectFinancialWorkspace
            projectId={projectId}
            canViewPaymentRequests={canViewPaymentRequests}
            canViewAnnualBudgetClosing={canViewAnnualBudgetClosing}
            canCreateAnnualBudget={canCreateAnnualBudget}
            canEditAnnualBudget={canEditAnnualBudget}
            canConfigureDraftApproval={canConfigureDraftAnnualBudgetApproval}
            canDeleteAnnualBudget={canDeleteAnnualBudget}
            canWithdrawAnnualBudget={canWithdrawAnnualBudget}
            canSubmitAnnualBudget={canSubmitAnnualBudget}
            canReviseAnnualBudget={canReviseAnnualBudget}
            canApprove={canApproveAnnualBudget}
            canPrepareClosing={canPrepareAnnualBudgetClosing}
            canApproveClosing={canApproveAnnualBudgetClosing}
            canManageAssignmentRecovery={canManageAssignmentRecovery}
            canCarryForward={canCarryForwardAnnualBudget}
            projectCurrency={project.currency ?? null}
            activeSection={section}
            selectedFiscalYear={selectedFiscalYear}
            onFiscalYearChange={setSelectedFiscalYear}
            onOpenClosing={() => setActiveSection("closing")}
          />

          {(canViewPaymentRequests || canReviewPaymentRequests || canReviewReversals) ? <div hidden={!paymentRequestsActive && !settlementsActive}>
            <ProjectPaymentRequests
              projectId={projectId}
              canViewRequests={canViewPaymentRequests}
              canReviewRequests={canReviewPaymentRequests}
              canReviewReversals={canReviewReversals}
              canCreateRequests={canCreatePaymentRequests}
              canEditRequests={canEditPaymentRequests}
              canSubmitRequests={canSubmitPaymentRequests}
              canCancelRequests={canCancelPaymentRequests}
              canApproveRequests={canApprovePaymentRequests}
              canManageAssignmentRecovery={canManageAssignmentRecovery}
              canRecordSettlements={canRecordSettlements}
              canCreateReversals={canCreateReversals}
              canApproveReversals={canApproveReversals}
              section={settlementsActive ? "settlements" : "requests"}
              active={paymentRequestsActive || settlementsActive}
            />
          </div> : null}

          {canViewPaymentObligations ? <div hidden={!settlementsActive}>
            <PlannedPaymentsPage
              embeddedProjectId={projectId}
              embeddedProject={{
                id: project.id,
                publicId: project.publicId ?? projectId,
                name: project.name ?? "",
                code: project.code ?? "",
                currency: project.currency,
              }}
              active={settlementsActive}
            />
          </div> : null}
        </main>
      </div>
    </div>
  );
}
