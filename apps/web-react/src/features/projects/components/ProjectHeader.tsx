import * as React from "react";
import { NavLink, useNavigate } from "react-router-dom";
import { cn, formatDate } from "@lib/utils";
import {
  LayoutDashboard, GanttChartSquare, Users, WalletCards, TriangleAlert, Send, CalendarDays, Files, ArrowRightLeft, History, Camera, GitBranch, MoreHorizontal, ChevronRight, Pencil, FilePen, ClipboardList, Settings2,
} from "lucide-react";
import { useI18n } from "@i18n/messages";
import { Button } from "@components/ui/Button";
import { Badge } from "@components/ui/Badge";
import { Skeleton } from "@components/ui/Skeleton";
import { ConfirmDialog } from "@components/ui/ConfirmDialog";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@components/ui/DropdownMenu";
import { SideSheet, SideSheetBody } from "@components/ui/SideSheet";
import { useDeleteProject, type ProjectDto } from "../api/projects";
import { EditProjectSheet } from "./CreateProjectSheet";
import { useProjectEnumLabels } from "../lib/project-labels";
import { useManagement } from "@api/management";
import { hasAnyPermission, hasPermission, Permissions } from "@lib/permissions";
import { useAuth } from "@auth/AuthProvider";
import { toast } from "@hooks/use-toast";
import { ProjectSettingsPanel } from "../ProjectSettingsTab";
import { ProjectPhaseGateSideSheet } from "./ProjectPhaseGateSideSheet";
import { ProjectStatusChangeSideSheet } from "./ProjectStatusChangeSideSheet";

interface ProjectHeaderProps {
  project: ProjectDto;
  statusOverride?: string;
  healthOverride?: string;
  onChangeRequest?: () => void;
  onHandover?: () => void;
  canInitiateHandover?: boolean;
  projectSettingsOpen?: boolean;
  onProjectSettingsOpenChange?: (open: boolean) => void;
}

// Canonical project tabs. Legacy routes remain addressable or redirect without
// adding more navigation entries to the Project Header.
const TABS = [
  { id: "overview", labelKey: "projects.tabs.overview", href: (id: string) => `/projects/${id}/overview`, icon: LayoutDashboard },
  { id: "plan", labelKey: "projects.tabs.plan", href: (id: string) => `/projects/${id}/plan`, icon: GanttChartSquare },
  { id: "team", labelKey: "projects.tabs.team", href: (id: string) => `/projects/${id}/team`, icon: Users },
  { id: "financials", labelKey: "projects.tabs.financials", href: (id: string) => `/projects/${id}/financials`, icon: WalletCards },
  { id: "risks-issues", labelKey: "projects.tabs.risksIssues", href: (id: string) => `/projects/${id}/risks-issues`, icon: TriangleAlert },
  { id: "status-reports", labelKey: "projects.tabs.statusReports", href: (id: string) => `/projects/${id}/status-reports`, icon: Send },
  { id: "meetings", labelKey: "projects.tabs.meetings", href: (id: string) => `/projects/${id}/meetings`, icon: CalendarDays },
  { id: "documents", labelKey: "projects.tabs.documents", href: (id: string) => `/projects/${id}/documents`, icon: Files },
  { id: "handover", labelKey: "projects.tabs.handover", href: (id: string) => `/projects/${id}/handover`, icon: ArrowRightLeft },
  { id: "history", labelKey: "projects.tabs.history", href: (id: string) => `/projects/${id}/history`, icon: History },
  { id: "snapshots", labelKey: "projects.tabs.snapshots", href: (id: string) => `/projects/${id}/snapshots`, icon: Camera },
  { id: "change-tracker", labelKey: "projects.tabs.changeTracker", href: (id: string) => `/projects/${id}/change-tracker`, icon: GitBranch },
] as const;

export function ProjectHeader({ project, statusOverride, healthOverride, onChangeRequest, onHandover, canInitiateHandover, projectSettingsOpen, onProjectSettingsOpenChange }: ProjectHeaderProps) {
  const { t, locale, formatCurrency } = useI18n();
  const { healthStatusLabel, priorityLabel } = useProjectEnumLabels();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const navigate = useNavigate();
  const remove = useDeleteProject();
  const [editOpen, setEditOpen] = React.useState(false);
  const [deleteOpen, setDeleteOpen] = React.useState(false);
  const [phaseGateOpen, setPhaseGateOpen] = React.useState(false);
  const [statusChangeOpen, setStatusChangeOpen] = React.useState(false);
  const [localProjectSettingsOpen, setLocalProjectSettingsOpen] = React.useState(false);
  const settingsSheetOpen = projectSettingsOpen ?? localProjectSettingsOpen;
  const setSettingsSheetOpen = React.useCallback((open: boolean) => {
    if (onProjectSettingsOpenChange) onProjectSettingsOpenChange(open);
    else setLocalProjectSettingsOpen(open);
  }, [onProjectSettingsOpenChange]);

  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.Project.Edit) && canMutate);
  const canViewProjectSettings = Boolean(user && hasPermission(user.permissions, Permissions.ProjectSettings.View));
  const canEditGeneralSettingsInSheet = Boolean(canViewProjectSettings && user
    && hasPermission(user.permissions, Permissions.ProjectSettings.Edit)
    && canEdit && !effectiveReadOnly);
  const canViewProjectFinanceWorkspace = Boolean(user && hasAnyPermission(user.permissions, [
    Permissions.ProjectFinance.View,
    Permissions.ProjectFinancialFoundation.View,
    Permissions.ProjectAnnualBudget.View, Permissions.ProjectPaymentRequest.View,
    Permissions.ProjectSettlement.View, Permissions.PaymentObligation.View, Permissions.ProjectAnnualBudgetClosing.View,
    Permissions.ProjectFinancialAuditTrail.View, Permissions.ProjectPaymentRequest.Approve,
    Permissions.ProjectPaymentReversal.Approve,
  ]));
  const canDelete = Boolean(user && hasPermission(user.permissions, Permissions.Project.Delete) && canMutate);
  const canManageReporting = Boolean(user && hasPermission(user.permissions, Permissions.StatusReport.ManageConfiguration) && canMutate && !effectiveReadOnly);
  const canManageChangeRequests = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.Manage) && canMutate && !effectiveReadOnly);
  const canManageHandovers = canInitiateHandover ?? Boolean(user && hasPermission(user.permissions, Permissions.Handover.Manage) && canMutate && !effectiveReadOnly);
  const canViewPhaseGate = Boolean(user
    && hasPermission(user.permissions, Permissions.Project.View)
    && hasPermission(user.permissions, Permissions.Document.View));
  const canChangePhase = Boolean(user
    && hasPermission(user.permissions, Permissions.Project.View)
    && hasPermission(user.permissions, Permissions.ProjectPhase.Change)
    && canMutate && !effectiveReadOnly);
  const canOpenPhaseGate = canViewPhaseGate || canChangePhase;
  const canChangeStatus = Boolean(user
    && hasPermission(user.permissions, Permissions.ProjectStatus.Change)
    && canMutate && !effectiveReadOnly);
  const displayStatus = statusOverride
    || (locale === "ar" ? project.statusNameAr : project.statusNameEn)
    || project.status;
  const displayHealth = healthOverride || project.health || project.ragStatus;
  const displayProjectPhase = (locale === "ar" ? project.phaseNameAr : project.phaseNameEn) || project.phase;

  async function handleDelete() {
    await remove.mutateAsync(project.publicId);
    navigate("/projects", { replace: true });
  }

  return (
    <div dir={locale === "ar" ? "rtl" : "ltr"} className="nexus-project-detail space-y-4">
      <nav aria-label={t("common.breadcrumb")} className="flex items-center gap-1.5 text-xs text-muted-foreground">
        <NavLink to="/projects" className="hover:text-foreground hover:underline">
          {t("projects.title")}
        </NavLink>
        <ChevronRight className="h-3 w-3 rtl:rotate-180" aria-hidden="true" />
        <span className="font-mono text-foreground">{project.code}</span>
      </nav>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="outline" className="text-[10px]">{displayStatus}</Badge>
            {displayHealth ? (
              <Badge status={displayHealth} className="text-[10px]">
                <span className="me-1.5 h-1.5 w-1.5 rounded-full bg-current" />
                {healthStatusLabel(displayHealth)}
              </Badge>
            ) : null}
            {displayProjectPhase ? <Badge variant="outline" aria-label={t("projects.phaseGate.currentPhase")} className="text-[10px]">{displayProjectPhase}</Badge> : null}
            {project.priority ? <Badge status={project.priority} className="text-[10px]">{priorityLabel(project.priority)}</Badge> : null}
            {project.portfolioName ? <span className="truncate text-xs text-muted-foreground">{project.portfolioName}</span> : null}
          </div>
          <h1 className="mt-2 truncate text-2xl font-bold tracking-tight">{project.name}</h1>
          <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">
            <span className="font-semibold text-foreground">{project.code}</span>
            {project.description ? ` · ${project.description}` : ""}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            {project.startDate || project.endDate
              ? `${project.startDate ? formatDate(project.startDate, locale) : "—"} → ${project.endDate ? formatDate(project.endDate, locale) : "—"}`
              : ""}
            {project.budget ? ` · ${formatCurrency(project.budget, project.currency)}` : ""}
          </p>
        </div>
        <div className="flex shrink-0 flex-wrap items-center gap-2">
          {canViewProjectSettings ? (
            <Button
              variant="outline"
              size="sm"
              className="gap-2"
              aria-label={t("projects.projectSettings")}
              title={t("projects.projectSettings")}
              onClick={() => setSettingsSheetOpen(true)}
            >
              <Settings2 className="h-4 w-4" />
              <span className="hidden sm:inline">{t("projects.projectSettings")}</span>
            </Button>
          ) : null}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="icon" aria-label={t("common.actions")} title={t("common.actions")}>
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {canEdit && !canEditGeneralSettingsInSheet ? (
                <DropdownMenuItem onSelect={() => setEditOpen(true)}>
                  <Pencil className="me-2 h-4 w-4" />
                  {t("common.edit")}
                </DropdownMenuItem>
              ) : null}
              {canManageChangeRequests && onChangeRequest ? (
                <DropdownMenuItem onSelect={() => onChangeRequest()}>
                  <FilePen className="me-2 h-4 w-4" />
                  {t("projects.changeRequest")}
                </DropdownMenuItem>
              ) : null}
              {canChangeStatus ? <DropdownMenuItem onSelect={() => setStatusChangeOpen(true)}>
                <ArrowRightLeft className="me-2 h-4 w-4" />
                {t("projects.lifecycle.changeStatus")}
              </DropdownMenuItem> : null}
              {canOpenPhaseGate ? <DropdownMenuItem onSelect={() => setPhaseGateOpen(true)}>
                <ArrowRightLeft className="me-2 h-4 w-4" />
                {t(canChangePhase ? "projects.phaseGate.changePhaseAction" : "projects.phaseGate.openPhaseGate")}
              </DropdownMenuItem> : null}
              <DropdownMenuItem onSelect={() => navigate(`/projects/${project.publicId}/plan`)}>
                <GanttChartSquare className="me-2 h-4 w-4" />
                {t("projects.plan")}
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={() => navigate(`/projects/${project.publicId}/status-reports`)}>
                <ClipboardList className="me-2 h-4 w-4" />
                {t("projects.statusReports")}
              </DropdownMenuItem>
              {canManageReporting ? <DropdownMenuItem onSelect={() => navigate(`/projects/${project.publicId}/settings/reporting`)}>
                <Settings2 className="me-2 h-4 w-4" />
                {t("projects.reporting.configurationTitle")}
              </DropdownMenuItem> : null}
              {canManageHandovers ? (
                <DropdownMenuItem onSelect={() => onHandover ? onHandover() : navigate(`/projects/${project.publicId}/handover`)}>
                  <ArrowRightLeft className="me-2 h-4 w-4" />
                  {t("projects.handover")}
                </DropdownMenuItem>
              ) : null}
              {canDelete ? (
                <>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem className="text-destructive" onSelect={() => setDeleteOpen(true)}>
                    {t("projects.delete")}
                  </DropdownMenuItem>
                </>
              ) : null}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      <div className="nexus-fullbleed-tabs sticky top-0 z-20 isolate border-y border-border bg-muted/70 py-1">
        <div role="tablist" aria-label={t("projects.title")} className="flex h-auto w-full min-w-0 items-center justify-between gap-1 overflow-x-auto overflow-y-hidden rounded-none bg-transparent px-2">
          {TABS.filter(tab => tab.id !== "financials" || canViewProjectFinanceWorkspace).map((tab) => (
            <NavLink
              key={tab.id}
              to={tab.href(project.publicId)}
              end={tab.id !== "snapshots"}
              role="tab"
              className={({ isActive }) =>
                cn(
                  "flex h-auto shrink-0 items-center whitespace-nowrap rounded-none border-b-2 border-transparent px-3 py-1.5 text-[13px] font-semibold leading-5 text-muted-foreground transition-colors rtl:flex-row-reverse",
                  isActive && "border-primary bg-transparent px-3.5 py-1.5 text-[15px] font-semibold text-primary shadow-none"
                )
              }
            >
              <tab.icon className="me-1.5 h-3.5 w-3.5" aria-hidden="true" />
              {t(tab.labelKey)}
            </NavLink>
          ))}
        </div>
      </div>

      {canViewProjectSettings ? <SideSheet
        open={settingsSheetOpen}
        onOpenChange={setSettingsSheetOpen}
        title={<span dir={locale === "ar" ? "rtl" : "ltr"}>{t("projects.projectSettings")}</span>}
        description={<span dir={locale === "ar" ? "rtl" : "ltr"}>{project.name}</span>}
        ariaLabel={t("projects.projectSettings")}
        closeLabel={t("common.close")}
        size="xl"
      >
        {settingsSheetOpen ? <SideSheetBody dir={locale === "ar" ? "rtl" : "ltr"}>
          <ProjectSettingsPanel projectId={project.publicId} project={project} showHeading={false} />
        </SideSheetBody> : null}
      </SideSheet> : null}

      {canOpenPhaseGate ? <ProjectPhaseGateSideSheet open={phaseGateOpen} onOpenChange={setPhaseGateOpen} project={project} /> : null}
      {canChangeStatus ? <ProjectStatusChangeSideSheet open={statusChangeOpen} onOpenChange={setStatusChangeOpen} project={project} /> : null}

      {editOpen && canEdit ? <EditProjectSheet
        open={editOpen}
        onOpenChange={setEditOpen}
        project={project}
        canInitiateHandover={canManageHandovers}
        onHandover={() => {
          setEditOpen(false);
          if (onHandover) onHandover();
          else navigate(`/projects/${project.publicId}/handover`);
        }}
      /> : null}
      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title={`${t("projects.delete")}: ${project.name}`}
        description={t("projects.confirmDelete")}
        confirmLabel={t("common.delete")}
        destructive
        onConfirm={handleDelete}
      />
      {remove.isError && (
        <p role="alert" className="text-sm text-destructive">
          {t("common.errorLoading")}
        </p>
      )}
      {remove.isPending ? (
        <div className="flex items-center gap-2 text-xs text-muted-foreground" role="status">
          <Skeleton className="h-4 w-4 rounded-full" />
          {t("common.saving")}
        </div>
      ) : null}
    </div>
  );
}
