import * as React from "react";
import { ApiError } from "@api/client";
import { useOutletContext } from "react-router-dom";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
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
  const { t } = useI18n();
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

  React.useEffect(() => {
    setMode(foundation.data?.financeApprovalMode ?? null);
  }, [foundation.data?.financeApprovalMode, foundation.data?.rowVersion]);

  React.useEffect(() => {
    setEditingGeneralSettings(false);
  }, [projectId]);

  const saveMode = async () => {
    if (!canManageFoundationApprovalConfiguration || !foundation.data) return;
    try {
      await updateMode.mutateAsync({ mode, rowVersion: foundation.data.rowVersion });
    } catch {
      // Mutation state carries the visible error; keep this event handler settled.
    }
  };

  return <div className="space-y-6">
    {showHeading ? <header>
      <h1 className="text-2xl font-semibold tracking-tight">{t("projects.projectSettings")}</h1>
      <p className="mt-1 text-sm text-muted-foreground">{project.name || project.code}</p>
    </header> : null}

    {canViewProjectSettings ? <section aria-labelledby="project-general-settings-title" className="space-y-4">
      <div>
        <h2 id="project-general-settings-title" className="text-lg font-semibold">{t("projects.generalSettings")}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{t("projects.generalSettingsDescription")}</p>
      </div>
      {editingGeneralSettings && canManageGeneralSettings ? <ProjectSheet
        open
        onOpenChange={setEditingGeneralSettings}
        project={project}
        embedded
      /> : <Card>
        <CardHeader className="pb-3">
          <CardTitle>{project.name}</CardTitle>
          <p className="text-sm text-muted-foreground">{project.code}</p>
        </CardHeader>
        <CardContent className="space-y-3">
          {canManageGeneralSettings ? <Button type="button" variant="outline" size="sm" onClick={() => setEditingGeneralSettings(true)}>
            {t("common.edit")}
          </Button> : <p role="note" className="text-sm text-muted-foreground">{t("projects.generalSettingsEditRequirements")}</p>}
        </CardContent>
      </Card>}
    </section> : null}

    {canViewProjectSettings ? <ProjectWorkflowSettings projectId={projectId} /> : null}

    {canViewFinancialFoundation ? <section aria-labelledby="project-financial-approval-settings-title" className="space-y-4">
      <h2 id="project-financial-approval-settings-title" className="text-lg font-semibold">{t("projects.financialApprovalSettingsTitle")}</h2>
      {!canManageFoundationApprovalConfiguration ? <p role="note" className="text-sm text-muted-foreground">{t("projects.financialApprovalEditRequirements")}</p> : null}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle>{t("projects.financeApprovalMode")}</CardTitle>
          <p className="text-sm text-muted-foreground">{t("projects.financeApprovalModeDescription")}</p>
        </CardHeader>
        <CardContent className="space-y-3">
          {foundation.isLoading ? <p role="status" className="text-sm text-muted-foreground">{t("common.loading")}</p>
            : foundation.isError || !foundation.data ? <div className="flex items-center justify-between gap-3">
              <p role="alert" className="text-sm text-destructive">{foundation.error instanceof ApiError && foundation.error.status === 403 ? t("projects.financeWorkspaceAccessDenied") : t("projects.financialFoundationLoadFailed")}</p>
              <Button size="sm" variant="outline" onClick={() => void foundation.refetch()}>{t("common.retry")}</Button>
            </div> : <div className="flex flex-wrap items-end gap-3">
              <div className="min-w-56 flex-1">
                <label htmlFor="project-finance-approval-mode" className="text-sm font-medium">{t("projects.financeApprovalMode")}</label>
                <select id="project-finance-approval-mode" className="mt-1 flex h-10 w-full rounded-md border bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" value={mode ?? ""} disabled={!canManageFoundationApprovalConfiguration || updateMode.isPending} onChange={event => setMode((event.target.value || null) as ProjectFinancialFoundation["financeApprovalMode"])}>
                  <option value="">{t("projects.financeApprovalModeUnconfigured")}</option>
                  <option value="Sequential">{t("projects.financeApprovalSequential")}</option>
                  <option value="Parallel">{t("projects.financeApprovalParallel")}</option>
                  <option value="Both">{t("projects.financeApprovalBoth")}</option>
                </select>
                <p className="mt-1 text-xs text-muted-foreground">{t("projects.financeApprovalModeDescription")}</p>
              </div>
              {canManageFoundationApprovalConfiguration ? <Button size="sm" onClick={() => void saveMode()} disabled={updateMode.isPending || mode === foundation.data.financeApprovalMode}>
                {updateMode.isPending ? t("common.saving") : t("common.save")}
              </Button> : null}
              {updateMode.isError ? <p role="alert" className="w-full text-sm text-destructive">{t("projects.financeApprovalModeSaveFailed")}</p> : null}
            </div>}
        </CardContent>
      </Card>
    </section> : null}

    {canViewAnnualBudgets ? <section aria-labelledby="project-annual-budget-approval-defaults-title" className="space-y-4">
      <h2 id="project-annual-budget-approval-defaults-title" className="text-lg font-semibold">{t("projects.annualBudgetApprovalDefaultsTitle")}</h2>
      {!canManageAnnualBudgetApprovalConfiguration ? <p role="note" className="text-sm text-muted-foreground">{t("projects.annualBudgetApprovalDefaultsEditRequirements")}</p> : null}
      <ProjectAnnualBudgetApprovalSettings projectId={projectId} canManage={canManageAnnualBudgetApprovalConfiguration} enabled />
    </section> : null}
  </div>;
}
