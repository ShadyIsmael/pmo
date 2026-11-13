import * as React from "react";
import { ClipboardList, FileSliders, FolderKanban, GitBranch, Landmark, Pencil } from "lucide-react";
import { ApiError } from "@api/client";
import { useOutletContext } from "react-router-dom";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import { ChangePhaseSettings } from "./ChangePhaseSettings";
import { ProjectAnnualBudgetApprovalSettings } from "./ProjectAnnualBudgetApprovalSettings";
import { ProjectSheet } from "./components/CreateProjectSheet";
import { ProjectWorkflowSettings } from "./ProjectWorkflowSettings";
import { useProjectFinancialFoundation, useUpdateProjectFinanceApprovalMode, type ProjectFinancialFoundation } from "./api/project-financials";
import type { ProjectDto } from "./api/projects";

type ProjectContext = { projectId: string; project: ProjectDto };

export function ProjectSettingsTab() {
  const { projectId, project } = useOutletContext<ProjectContext>();
  return <ProjectSettingsPanel projectId={projectId} project={project} />;
}

export function ProjectSettingsPanel({
  projectId,
  project,
  showHeading = true,
}: {
  projectId: string;
  project: ProjectDto;
  showHeading?: boolean;
}) {
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const { t, locale } = useI18n();
  // The Change Phase section is a project lifecycle workspace, so it is gated on
  // project/document read access rather than ProjectSettings.View.
  const canViewPhaseGate = Boolean(user
    && hasPermission(user.permissions, Permissions.Project.View)
    && hasPermission(user.permissions, Permissions.Document.View));
  const canViewProjectSettings = Boolean(user && hasPermission(user.permissions, Permissions.ProjectSettings.View));
  const canEditProjectSettings = Boolean(user && hasPermission(user.permissions, Permissions.ProjectSettings.Edit));
  const canEditProject = Boolean(user && hasPermission(user.permissions, Permissions.Project.Edit));
  const canViewAnnualBudgets = Boolean(canViewProjectSettings && user && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.View));
  const canViewFinancialFoundation = Boolean(canViewProjectSettings && user
    && hasPermission(user.permissions, Permissions.ProjectFinancialFoundation.View));
  const canManageGeneralSettings = Boolean(canViewProjectSettings && canEditProjectSettings && canEditProject
    && canMutate && !effectiveReadOnly);
  const canManageAnnualBudgetApprovalConfiguration = Boolean(user && canViewProjectSettings && canEditProjectSettings && canViewAnnualBudgets && canMutate && !effectiveReadOnly
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.ManageApprovalConfiguration));
  const canManageFoundationApprovalConfiguration = Boolean(user && canViewProjectSettings && canEditProjectSettings && canViewFinancialFoundation && canMutate && !effectiveReadOnly
    && hasPermission(user.permissions, Permissions.ProjectAnnualBudget.ManageApprovalConfiguration));
  const foundation = useProjectFinancialFoundation(projectId, Boolean(projectId) && canViewFinancialFoundation);
  const updateMode = useUpdateProjectFinanceApprovalMode(projectId);
  const [mode, setMode] = React.useState<ProjectFinancialFoundation["financeApprovalMode"]>(null);
  const [editingGeneralSettings, setEditingGeneralSettings] = React.useState(false);
  const [activeSection, setActiveSection] = React.useState("project-general-settings");

  React.useEffect(() => {
    setMode(foundation.data?.financeApprovalMode ?? null);
  }, [foundation.data?.financeApprovalMode, foundation.data?.rowVersion]);

  React.useEffect(() => {
    setEditingGeneralSettings(false);
    setActiveSection("project-general-settings");
  }, [projectId]);

  const saveMode = async () => {
    if (!canManageFoundationApprovalConfiguration || !foundation.data) return;
    try {
      await updateMode.mutateAsync({ mode, rowVersion: foundation.data.rowVersion });
    } catch {
      // Mutation state carries the visible error; keep this event handler settled.
    }
  };

  const navigation = [
    ...(canViewPhaseGate ? [{ id: "project-change-phase", label: t("projects.phaseGate.changePhaseTitle"), description: t("projects.phaseGate.changePhaseDescription"), icon: GitBranch }] : []),
    ...(canViewProjectSettings ? [
      { id: "project-general-settings", label: t("projects.generalSettings"), description: t("projects.generalSettingsDescription"), icon: FolderKanban },
      { id: "project-workflow-settings", label: t("projects.workflowApprovalConfigurationTitle"), description: t("projects.workflowSettingsDescription"), icon: FileSliders },
    ] : []),
    ...(canViewFinancialFoundation ? [{ id: "project-financial-approval-settings", label: t("projects.financialApprovalSettingsTitle"), description: t("projects.financeApprovalModeDescription"), icon: Landmark }] : []),
    ...(canViewAnnualBudgets ? [{ id: "project-annual-budget-approval-defaults", label: t("projects.annualBudgetApprovalDefaultsTitle"), description: t("projects.annualBudgetProjectDefaultsDescription"), icon: ClipboardList }] : []),
  ];
  const currentSection = navigation.some(item => item.id === activeSection) ? activeSection : navigation[0]?.id;

  return <div className="space-y-6" dir={locale === "ar" ? "rtl" : "ltr"}>
    {showHeading ? <header className="rounded-2xl border bg-gradient-to-br from-primary/10 via-card to-card p-5 sm:p-7">
      <div className="flex items-start gap-4">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary"><FileSliders aria-hidden="true" className="h-5 w-5" /></div>
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold tracking-tight">{t("projects.projectSettings")}</h1>
          <p className="mt-1 break-words text-sm text-muted-foreground">{project.name || project.code}</p>
        </div>
      </div>
    </header> : null}

    <div className="grid items-start gap-6 lg:grid-cols-[minmax(15rem,19rem)_minmax(0,1fr)]">
    {navigation.length > 0 ? <nav aria-label={t("projects.projectSettings")} className="flex gap-2 overflow-x-auto pb-1 lg:sticky lg:top-4 lg:flex-col lg:overflow-visible lg:rounded-xl lg:border lg:bg-card lg:p-2">
      {navigation.map(({ id, label, description, icon: Icon }) => <button key={id} type="button" aria-current={currentSection === id ? "page" : undefined} onClick={() => setActiveSection(id)} className={`flex min-w-48 shrink-0 items-start gap-3 rounded-lg px-3 py-3 text-start transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring lg:min-w-0 ${currentSection === id ? "bg-primary/10 text-primary" : "text-foreground hover:bg-muted"}`}>
        <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${currentSection === id ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground"}`}><Icon aria-hidden="true" className="h-4 w-4" /></span><span className="min-w-0"><span className="block text-sm font-semibold">{label}</span><span className="mt-1 hidden text-xs leading-relaxed text-muted-foreground lg:block">{description}</span></span>
      </button>)}
    </nav> : null}
    <div className="min-w-0 space-y-6">

    {canViewPhaseGate ? <section hidden={currentSection !== "project-change-phase"} id="project-change-phase" aria-label={t("projects.phaseGate.changePhaseTitle")}>
      <ChangePhaseSettings projectId={projectId} project={project} />
    </section> : null}

    {canViewProjectSettings ? <section hidden={currentSection !== "project-general-settings"} id="project-general-settings" aria-labelledby="project-general-settings-title" className="space-y-4">
      <div>
        <h2 id="project-general-settings-title" className="text-lg font-semibold">{t("projects.generalSettings")}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{t("projects.generalSettingsDescription")}</p>
      </div>
      {editingGeneralSettings && canManageGeneralSettings ? <ProjectSheet
        open
        onOpenChange={setEditingGeneralSettings}
        project={project}
        embedded
      /> : <Card className="overflow-hidden">
        <CardContent className="space-y-6 p-5 sm:p-6">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex min-w-0 items-start gap-3">
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><FolderKanban aria-hidden="true" className="h-5 w-5" /></span>
            <div className="min-w-0"><CardTitle className="break-words text-base">{project.name}</CardTitle><p dir="auto" className="mt-1 break-all text-sm text-muted-foreground">{project.code}</p></div>
          </div>
          {canManageGeneralSettings ? <Button type="button" variant="outline" size="sm" className="self-start sm:self-center" onClick={() => setEditingGeneralSettings(true)}>
            <Pencil aria-hidden="true" className="me-2 h-4 w-4" />{t("common.edit")}
          </Button> : <p role="note" className="text-sm text-muted-foreground">{t("projects.generalSettingsEditRequirements")}</p>}
          </div>
          <dl className="grid gap-px overflow-hidden rounded-xl border bg-border sm:grid-cols-2">
            {[[t("projects.projectDescription"), project.description], [t("projects.portfolio"), project.portfolioName], [t("projects.projectManager"), project.projectManagerName], [t("projects.sponsor"), project.sponsorName], [t("projects.department"), project.departmentName]].map(([label, value]) => <div key={label} className="min-w-0 bg-card p-4"><dt className="text-xs font-medium text-muted-foreground">{label}</dt><dd className="mt-1 break-words text-sm font-medium">{value || t("common.notAvailable")}</dd></div>)}
          </dl>
        </CardContent>
      </Card>}
    </section> : null}

    {canViewProjectSettings ? <div hidden={currentSection !== "project-workflow-settings"} id="project-workflow-settings"><ProjectWorkflowSettings projectId={projectId} showHeading={false} /></div> : null}

    {canViewFinancialFoundation ? <section hidden={currentSection !== "project-financial-approval-settings"} id="project-financial-approval-settings" aria-labelledby="project-financial-approval-settings-title" className="space-y-4">
      <h2 id="project-financial-approval-settings-title" className="text-lg font-semibold">{t("projects.financialApprovalSettingsTitle")}</h2>
      {!canManageFoundationApprovalConfiguration ? <p role="note" className="text-sm text-muted-foreground">{t("projects.financialApprovalEditRequirements")}</p> : null}
      <Card className="overflow-hidden">
        <CardHeader className="border-b bg-muted/30 pb-5">
          <CardTitle className="text-lg">{t("projects.financeApprovalMode")}</CardTitle>
          <p className="text-sm text-muted-foreground">{t("projects.financeApprovalModeDescription")}</p>
        </CardHeader>
        <CardContent className="space-y-3 p-5 sm:p-6">
          {foundation.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
            : foundation.isError || !foundation.data ? <div className="flex items-center justify-between gap-3">
              <p role="alert" className="text-sm text-destructive">{foundation.error instanceof ApiError && foundation.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.financialFoundationLoadFailed")}</p>
              <Button size="sm" variant="outline" onClick={() => void foundation.refetch()}>{t("common.retry")}</Button>
            </div> : <div className="flex flex-col gap-5 sm:flex-row sm:items-end">
              <div className="min-w-0 flex-1">
                <label htmlFor="project-finance-approval-mode" className="text-sm font-medium">{t("projects.financeApprovalMode")}</label>
                <select id="project-finance-approval-mode" className="mt-1 flex h-10 w-full rounded-md border bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" value={mode ?? ""} disabled={!canManageFoundationApprovalConfiguration || updateMode.isPending} onChange={event => setMode((event.target.value || null) as ProjectFinancialFoundation["financeApprovalMode"])}>
                  <option value="">{t("projects.financeApprovalModeUnconfigured")}</option>
                  <option value="Sequential">{t("projects.financeApprovalSequential")}</option>
                  <option value="Parallel">{t("projects.financeApprovalParallel")}</option>
                  <option value="Both">{t("projects.financeApprovalBoth")}</option>
                </select>
              </div>
              {canManageFoundationApprovalConfiguration ? <Button size="sm" onClick={() => void saveMode()} disabled={updateMode.isPending || mode === foundation.data.financeApprovalMode}>
                {updateMode.isPending ? t("common.saving") : t("common.save")}
              </Button> : null}
              {updateMode.isError ? <p role="alert" className="w-full text-sm text-destructive">{t("projects.financeApprovalModeSaveFailed")}</p> : null}
            </div>}
        </CardContent>
      </Card>
    </section> : null}

    {canViewAnnualBudgets ? <section hidden={currentSection !== "project-annual-budget-approval-defaults"} id="project-annual-budget-approval-defaults" aria-labelledby="project-annual-budget-approval-defaults-title" className="space-y-4">
      <h2 id="project-annual-budget-approval-defaults-title" className="text-lg font-semibold">{t("projects.annualBudgetApprovalDefaultsTitle")}</h2>
      {!canManageAnnualBudgetApprovalConfiguration ? <p role="note" className="text-sm text-muted-foreground">{t("projects.annualBudgetApprovalDefaultsEditRequirements")}</p> : null}
      <ProjectAnnualBudgetApprovalSettings projectId={projectId} canManage={canManageAnnualBudgetApprovalConfiguration} enabled />
    </section> : null}
    </div>
    </div>
  </div>;
}
