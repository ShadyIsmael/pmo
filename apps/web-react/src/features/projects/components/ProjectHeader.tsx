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
import { useArchiveProject, useReactivateProject, useDeleteProject, type ProjectDto } from "../api/projects";
import { EditProjectSheet } from "./CreateProjectSheet";
import { useProjectEnumLabels } from "../lib/project-labels";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { useAuth } from "@auth/AuthProvider";
import { toast } from "@hooks/use-toast";

interface ProjectHeaderProps {
  project: ProjectDto;
  statusOverride?: string;
  healthOverride?: string;
  onChangeRequest?: () => void;
  onHandover?: () => void;
    canInitiateHandover?: boolean;
}

// Canonical 12 tabs mirroring the golden copy (Next TAB_VALUES). Alias routes
// (tasks/planning/workflows) keep working via URL but are not in the tab bar.
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

export function ProjectHeader({ project, statusOverride, healthOverride, onChangeRequest, onHandover, canInitiateHandover }: ProjectHeaderProps) {
  const { t, locale, formatCurrency } = useI18n();
  const { statusLabel, healthStatusLabel, priorityLabel } = useProjectEnumLabels();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const navigate = useNavigate();
  const archive = useArchiveProject();
  const reactivate = useReactivateProject();
  const remove = useDeleteProject();
  const [editOpen, setEditOpen] = React.useState(false);
  const [deleteOpen, setDeleteOpen] = React.useState(false);

  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.Project.Edit) && canMutate);
  const canDelete = Boolean(user && hasPermission(user.permissions, Permissions.Project.Delete) && canMutate);
  const canManageReporting = Boolean(user && hasPermission(user.permissions, Permissions.StatusReport.ManageConfiguration) && canMutate && !effectiveReadOnly);
  const canManageChangeRequests = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.Manage) && canMutate && !effectiveReadOnly);
    const canManageHandovers = canInitiateHandover ?? Boolean(user && hasPermission(user.permissions, Permissions.Handover.Manage) && canMutate && !effectiveReadOnly);
  const displayStatus = statusOverride || project.status;
  const displayHealth = healthOverride || project.health || project.ragStatus;

  async function handleArchive() {
    try {
      await archive.mutateAsync(project.publicId);
      toast({ title: t("projects.archiveSuccess") });
    } catch (err) {
      toast({ title: t("common.error"), description: err instanceof Error ? err.message : undefined, variant: "destructive" });
    }
  }

  async function handleReactivate() {
    try {
      await reactivate.mutateAsync(project.publicId);
      toast({ title: t("projects.reactivateSuccess") });
    } catch (err) {
      toast({ title: t("common.error"), description: err instanceof Error ? err.message : undefined, variant: "destructive" });
    }
  }

  async function handleDelete() {
    await remove.mutateAsync(project.publicId);
    navigate("/projects", { replace: true });
  }

  return (
    <div dir={locale === "ar" ? "rtl" : "ltr"} className="nexus-project-detail space-y-4">
      <nav aria-label="Breadcrumb" className="flex items-center gap-1.5 text-xs text-muted-foreground">
        <NavLink to="/projects" className="hover:text-foreground hover:underline">
          {t("projects.title")}
        </NavLink>
        <ChevronRight className="h-3 w-3 rtl:rotate-180" aria-hidden="true" />
        <span className="font-mono text-foreground">{project.code}</span>
      </nav>

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <Badge status={displayStatus} className="text-[10px]">{statusLabel(displayStatus)}</Badge>
            {displayHealth ? (
              <Badge status={displayHealth} className="text-[10px]">
                <span className="me-1.5 h-1.5 w-1.5 rounded-full bg-current" />
                {healthStatusLabel(displayHealth)}
              </Badge>
            ) : null}
            {project.priority ? <Badge status={project.priority} className="text-[10px]">{priorityLabel(project.priority)}</Badge> : null}
            {project.portfolioName ? <span className="truncate text-xs text-muted-foreground">{project.portfolioName}</span> : null}
          </div>
          <h1 className="mt-2 truncate text-2xl font-bold tracking-tight">{project.name}</h1>
          <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">
            <span className="font-semibold text-foreground">{project.code}</span>
            {project.phase ? ` · ${project.phase}` : ""}
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
          {canEdit ? (
            <Button
              variant="outline"
              size="icon"
              aria-label={t("projects.projectSettings")}
              title={t("projects.projectSettings")}
              onClick={() => navigate(`/projects/${project.publicId}/workflows`)}
            >
              <Settings2 className="h-4 w-4" />
            </Button>
          ) : null}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="icon" aria-label={t("common.actions")} title={t("common.actions")}>
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {canEdit ? (
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
              {canMutate && (canEdit || canDelete) ? (
                <>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onSelect={() => void handleArchive()}>{t("projects.archive")}</DropdownMenuItem>
                  <DropdownMenuItem onSelect={() => void handleReactivate()}>{t("projects.reactivate")}</DropdownMenuItem>
                  {canDelete ? (
                    <>
                      <DropdownMenuSeparator />
                      <DropdownMenuItem className="text-destructive" onSelect={() => setDeleteOpen(true)}>
                        {t("projects.delete")}
                      </DropdownMenuItem>
                    </>
                  ) : null}
                </>
              ) : null}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      <div className="nexus-fullbleed-tabs sticky top-0 z-20 isolate border-y border-border bg-muted/70 py-1">
        <div role="tablist" aria-label={t("projects.title")} className="flex h-auto w-full min-w-0 items-center justify-between gap-1 overflow-x-auto overflow-y-hidden rounded-none bg-transparent px-2">
          {TABS.map((tab) => (
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

      {editOpen && canEdit ? <EditProjectSheet
        open={editOpen}
        onOpenChange={setEditOpen}
        project={project}
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
      {(archive.isError || reactivate.isError || remove.isError) && (
        <p role="alert" className="text-sm text-destructive">
          {t("common.errorLoading")}
        </p>
      )}
      {(archive.isPending || reactivate.isPending || remove.isPending) ? (
        <div className="flex items-center gap-2 text-xs text-muted-foreground" role="status">
          <Skeleton className="h-4 w-4 rounded-full" />
          {t("common.saving")}
        </div>
      ) : null}
    </div>
  );
}
