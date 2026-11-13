import * as React from "react";
import { Link, useParams } from "react-router-dom";
import { CalendarDays, FileText, Filter, FolderOpen, Users, ZoomOut } from "lucide-react";
import { useI18n } from "@i18n/messages";
import { PageHeader } from "@components/layout/PageHeader";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Progress } from "@components/ui/Progress";
import { useFullWorkItemAssignees, useFullWorkItemMutations, useFullWorkItems, type SaveWorkItemDto, type WorkflowTransitionOption, type WorkItemDto } from "@api/workItemsFull";
import { WorkflowMoveError, useWorkItemWorkflowTransition, workflowMoveErrorMessage } from "@api/workItemWorkflow";
import { useWorkItemStatuses, type WorkItemStatusDto } from "@api/workItems";
import { WorkItemForm } from "@features/delivery/WorkItemForm";
import { WorkItemsKanban } from "@features/delivery/WorkItemsKanban";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { useDocuments, type DocumentDto } from "@features/delivery/api/delivery";
import { useProjects, type ProjectSummaryDto } from "./api/projects";
import { usePlanningActivitiesFull, usePlanningPlansFull, type PlanActivityDto } from "./api/planningFull";
import { roundPlanProgress } from "./lib/plan-progress";
import { useResources } from "@features/resources/api/resources";

const emptyProjectBoardItems: WorkItemDto[] = [];
const emptyProjectBoardStatuses: WorkItemStatusDto[] = [];

function queryError(query: { error: unknown }): string | null {
  return query.error instanceof Error ? query.error.message : query.error ? "Request failed" : null;
}

function ProjectPicker({ projects, value, onChange, label, locale }: { projects: readonly ProjectSummaryDto[]; value: string; onChange: (value: string) => void; label: string; locale: string }) {
  return (
    <label className="flex min-w-0 flex-col gap-1.5 text-sm">
      <span className="text-xs font-medium text-muted-foreground">{label}</span>
      <select className="h-10 min-w-[16rem] rounded-md border border-input bg-background px-3 text-sm" value={value} onChange={(event) => onChange(event.target.value)} disabled={!projects.length}>
        {!projects.length ? <option value="">—</option> : null}
        {projects.map((project) => {
          const statusName = locale === "ar" ? project.statusNameAr || project.statusNameEn : project.statusNameEn || project.statusNameAr;
          return <option key={project.publicId} value={project.publicId}>{project.name} · {project.code}{statusName ? ` · ${statusName}` : ""}</option>;
        })}
      </select>
    </label>
  );
}

function useWorkspaceProjectId(projects: readonly ProjectSummaryDto[], routeProjectId?: string) {
  const [selectedId, setSelectedId] = React.useState(routeProjectId ?? "");
  React.useEffect(() => {
    if (routeProjectId) setSelectedId(routeProjectId);
  }, [routeProjectId]);
  React.useEffect(() => {
    if (!selectedId && projects[0]) setSelectedId(projects[0].publicId);
  }, [projects, selectedId]);
  return [selectedId, setSelectedId] as const;
}

function WorkspaceLoading({ label }: { label: string }) {
  return <p className="py-12 text-center text-sm text-muted-foreground">{label}</p>;
}

function WorkspaceError({ message }: { message: string }) {
  return <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-destructive">{message}</p>;
}

function isWorkspaceExcluded(project: ProjectSummaryDto) { return project.reportingCategory === "Excluded"; }

function emptyProjectBoardTask(projectId?: string): SaveWorkItemDto {
  return { title: "", description: "", type: "Task", priority: "Medium", projectId, dueDate: "" };
}

export function ProjectBoardPage() {
  const { t, locale } = useI18n();
  const { id: routeProjectId } = useParams<{ id: string }>();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const projectsQuery = useProjects({ page: 1, pageSize: 200 });
  const allProjects = projectsQuery.data?.items ?? [];
  const projects = routeProjectId ? allProjects : allProjects.filter((project) => !isWorkspaceExcluded(project));
  const [projectId, setProjectId] = useWorkspaceProjectId(projects, routeProjectId);
  const workItemsQuery = useFullWorkItems({ projectId, page: 1, pageSize: 200, sortBy: "dueDate", sortDirection: "asc" }, Boolean(projectId));
  const statusesQuery = useWorkItemStatuses(true);
  const items = workItemsQuery.data?.items ?? emptyProjectBoardItems;
  const statuses = statusesQuery.data ?? emptyProjectBoardStatuses;
  const activeStatuses = React.useMemo(() => statuses.filter((status) => status.isActive).sort((a, b) => a.displayOrder - b.displayOrder), [statuses]);
  const boardStatuses = React.useMemo(() => [...activeStatuses, ...statuses.filter((status) => !status.isActive && items.some((item) => item.status === status.code))], [activeStatuses, items, statuses]);
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Create) && canMutate && !effectiveReadOnly);
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.WorkItem.Edit) && canMutate && !effectiveReadOnly);
  const mutations = useFullWorkItemMutations();
  const selectedProject = projects.find((project) => project.publicId === projectId);
  const [createOpen, setCreateOpen] = React.useState(false);
  const [draft, setDraft] = React.useState<SaveWorkItemDto>(() => emptyProjectBoardTask(selectedProject?.id));
  const [createError, setCreateError] = React.useState("");
  const createAssignees = useFullWorkItemAssignees(selectedProject?.publicId, createOpen);
  const [movingItemId, setMovingItemId] = React.useState<string>();
  const [moveError, setMoveError] = React.useState("");
  const [pendingTransition, setPendingTransition] = React.useState<{ itemId: string; targetStatus: string; options: WorkflowTransitionOption[] }>();
  const transitionMove = useWorkItemWorkflowTransition();

  function openCreateTask() {
    setDraft(emptyProjectBoardTask(selectedProject?.id));
    setCreateError("");
    setCreateOpen(true);
  }
  function closeCreate(open: boolean) {
    setCreateOpen(open);
    if (!open) { setDraft(emptyProjectBoardTask(selectedProject?.id)); setCreateError(""); }
  }
  async function saveTask() {
    if (!draft.title.trim()) { setCreateError(t("workItems.validation")); return; }
    setCreateError("");
    try {
      await mutations.create.mutateAsync({ ...draft, title: draft.title.trim(), projectId: selectedProject?.id, dueDate: draft.dueDate || undefined });
      closeCreate(false);
    } catch {
      setCreateError(t("workItems.createError"));
    }
  }
  async function executeTransition(itemId: string, targetStatus: string, option: WorkflowTransitionOption) {
    setMovingItemId(itemId);
    setMoveError("");
    try {
      await transitionMove.mutateAsync({ itemId, targetStateCode: targetStatus, transitionCode: option.code });
      setPendingTransition(undefined);
    } catch (cause) {
      setMoveError(workflowMoveErrorMessage(cause, t("workItems.moveError")));
    } finally {
      setMovingItemId(undefined);
    }
  }
  async function moveItem(itemId: string, targetStatus: string) {
    const item = items.find((candidate) => candidate.id === itemId);
    if (!item || !canEdit || movingItemId || item.status === targetStatus || !activeStatuses.some((status) => status.code === targetStatus)) return;
    setMoveError("");
    setMovingItemId(itemId);
    try {
      await transitionMove.mutateAsync({ itemId, targetStateCode: targetStatus });
      setPendingTransition(undefined);
    } catch (cause) {
      if (cause instanceof WorkflowMoveError && cause.failure.kind === "multiple") {
        setPendingTransition({ itemId, targetStatus, options: cause.failure.options });
        return;
      }
      setMoveError(workflowMoveErrorMessage(cause, t("workItems.moveError")));
    } finally {
      setMovingItemId(undefined);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t("projects.workspaceViews.boardTitle")} description={t("projects.workspaceViews.boardDescription")} actions={canCreate && projectId ? <Button type="button" size="sm" onClick={openCreateTask}>{t("projects.workspaceViews.addTask")}</Button> : undefined} />
      {projectsQuery.isError ? <WorkspaceError message={queryError(projectsQuery) ?? t("common.errorLoading")} /> : null}
      <ProjectPicker projects={projects} value={projectId} onChange={setProjectId} label={t("projects.workspaceViews.selectProject")} locale={locale} />
      {projectsQuery.isLoading || workItemsQuery.isLoading || statusesQuery.isLoading ? <WorkspaceLoading label={t("common.loading")} /> : workItemsQuery.isError || statusesQuery.isError ? <WorkspaceError message={queryError(workItemsQuery.isError ? workItemsQuery : statusesQuery) ?? t("common.errorLoading")} /> : !projectId ? <WorkspaceLoading label={t("projects.workspaceViews.noProjects")} /> : <WorkItemsKanban items={items} statuses={boardStatuses} locale={locale} canEdit={canEdit} movingItemId={movingItemId} moveError={moveError} onMove={(itemId, targetStatus) => void moveItem(itemId, targetStatus)} t={(key) => t(key)} />}
      <SideSheet open={createOpen} onOpenChange={closeCreate} title={t("projects.workspaceViews.addTask")} description={selectedProject?.name} closeLabel={t("common.close")}>
        <SideSheetBody className="space-y-4">{createError ? <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{createError}</p> : null}<WorkItemForm draft={draft} onChange={(patch) => setDraft((current) => ({ ...current, ...patch }))} t={t} assignees={createAssignees.data ?? []} assigneesLoading={createAssignees.isLoading} assigneesError={createAssignees.isError} onRetryAssignees={() => { void createAssignees.refetch(); }} projects={selectedProject ? [selectedProject] : []} allowTypeChange={false} projectLocked /></SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => closeCreate(false)}>{t("common.cancel")}</Button><Button type="button" onClick={() => void saveTask()} disabled={mutations.create.isPending}><span>{mutations.create.isPending ? t("common.loading") : t("common.save")}</span></Button></SideSheetFooter>
      </SideSheet>
      <SideSheet open={Boolean(pendingTransition)} onOpenChange={(open) => { if (!open && !movingItemId) setPendingTransition(undefined); }} title={t("workItems.chooseTransition")} description={t("workItems.multipleTransitions")} size="md" closeLabel={t("common.close")}>
        <SideSheetBody className="space-y-2">{(pendingTransition?.options ?? []).map((option) => <Button key={option.code} type="button" variant="outline" className="w-full justify-between" disabled={Boolean(movingItemId)} onClick={() => { if (pendingTransition) void executeTransition(pendingTransition.itemId, pendingTransition.targetStatus, option); }}><span>{locale === "ar" ? option.nameAr : option.nameEn}</span><span className="font-mono text-xs text-muted-foreground">{option.code}</span></Button>)}{moveError ? <p role="alert" className="text-sm text-destructive">{moveError}</p> : null}</SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => setPendingTransition(undefined)} disabled={Boolean(movingItemId)}>{t("common.cancel")}</Button></SideSheetFooter>
      </SideSheet>
    </div>
  );
}

const DAY_MS = 24 * 60 * 60 * 1000;
const MONTH_WIDTHS = [56, 88, 120] as const;
const ZOOM_LABELS = [75, 100, 125] as const;

function utcDateValue(value?: string) {
  if (!value) return undefined;
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) return undefined;
  const result = Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  return Number.isFinite(result) ? result : undefined;
}

function monthStart(value: number) {
  const date = new Date(value);
  return Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), 1);
}

function buildGanttMonths(values: readonly (number | undefined)[]) {
  const timestamps = values.filter((value): value is number => value !== undefined);
  const earliest = timestamps.length ? Math.min(...timestamps) : Date.now();
  const latest = timestamps.length ? Math.max(...timestamps) : earliest;
  const firstMonth = monthStart(earliest);
  const lastMonth = monthStart(latest);
  const span = (new Date(lastMonth).getUTCFullYear() - new Date(firstMonth).getUTCFullYear()) * 12 + new Date(lastMonth).getUTCMonth() - new Date(firstMonth).getUTCMonth() + 1;
  const count = Math.max(12, span);
  return Array.from({ length: count }, (_, index) => {
    const date = new Date(firstMonth);
    date.setUTCMonth(date.getUTCMonth() + index);
    return date.getTime();
  });
}

function formatGanttDate(value: string | undefined, locale: string) {
  const timestamp = utcDateValue(value);
  if (timestamp === undefined) return undefined;
  return new Intl.DateTimeFormat(locale === "ar" ? "ar-SA-u-nu-latn" : "en-US", { dateStyle: "medium", timeZone: "UTC" }).format(timestamp);
}

function GanttActivityRow({ activity, rangeStart, rangeEnd, months, monthWidth, locale, t }: {
  activity: PlanActivityDto;
  rangeStart: number;
  rangeEnd: number;
  months: readonly number[];
  monthWidth: number;
  locale: string;
  t: (key: string, params?: Record<string, string | number>) => string;
}) {
  const start = utcDateValue(activity.startDate);
  const finish = utcDateValue(activity.endDate) ?? start;
  const range = Math.max(DAY_MS, rangeEnd - rangeStart);
  const left = start === undefined ? undefined : Math.max(0, Math.min(1, (start - rangeStart) / range));
  const end = start === undefined ? undefined : Math.max(start + DAY_MS, finish ?? start + DAY_MS);
  const width = left === undefined || end === undefined || start === undefined ? 0 : Math.max(0, Math.min(1 - left, (end - start) / range));
  const isMilestone = activity.type.toLowerCase() === "milestone";
  const startLabel = formatGanttDate(activity.startDate, locale) ?? t("common.notAvailable");
  return (
    <div className="grid items-center border-b last:border-0" style={{ gridTemplateColumns: `16rem repeat(${months.length}, ${monthWidth}px)` }} data-testid="gantt-activity-row">
      <div className="border-e px-3 py-2" dir={locale === "ar" ? "rtl" : "ltr"}>
        <div className="flex items-center gap-2">
          <span aria-hidden="true" className={`h-2.5 w-2.5 shrink-0 rounded-full ${isMilestone ? "bg-primary" : activity.critical ? "bg-destructive" : "bg-success"}`} />
          <p className={`truncate text-xs ${isMilestone ? "font-semibold text-primary" : "font-medium"}`}>{activity.name}</p>
        </div>
        <p className="truncate ps-[1.125rem] font-mono text-[10px] text-muted-foreground">{activity.code}</p>
      </div>
      <div className="relative col-span-full h-12" style={{ gridColumn: `2 / span ${months.length}` }}>
        <div className="absolute inset-0 grid" style={{ gridTemplateColumns: `repeat(${months.length}, ${monthWidth}px)` }} aria-hidden="true">
          {months.map((month) => <span key={month} className="border-e border-border/70" />)}
        </div>
        {left !== undefined ? (
          <div
            className={`absolute top-3 h-5 ${isMilestone ? `h-3 w-3 ${locale === "ar" ? "translate-x-1/2" : "-translate-x-1/2"} rotate-45 rounded-sm` : "rounded"} ${isMilestone ? "bg-primary" : activity.critical ? "bg-destructive" : "bg-primary"}`}
            style={{ insetInlineStart: `${left * 100}%`, width: isMilestone ? undefined : `${width * 100}%`, minWidth: isMilestone ? undefined : "3px" }}
            title={`${roundPlanProgress(activity.progress) ?? 0}%`}
            role="img"
            aria-label={`${activity.name}: ${roundPlanProgress(activity.progress) ?? 0}%`}
          >
            {!isMilestone ? <div className="h-full rounded bg-white/25" style={{ width: `${Math.max(0, Math.min(100, roundPlanProgress(activity.progress) ?? 0))}%` }} /> : null}
          </div>
        ) : null}
        <span className="absolute bottom-0 start-1 text-[9px] text-muted-foreground">{startLabel}</span>
      </div>
    </div>
  );
}

export function ProjectGanttPage() {
  const { t, locale } = useI18n();
  const { id: routeProjectId } = useParams<{ id: string }>();
  const projectsQuery = useProjects({ page: 1, pageSize: 200 });
  const projects = React.useMemo(() => (projectsQuery.data?.items ?? []).filter((item) => !isWorkspaceExcluded(item)), [projectsQuery.data?.items]);
  const [projectId, setProjectId] = useWorkspaceProjectId(projects, routeProjectId);
  const plansQuery = usePlanningPlansFull(projectId, Boolean(projectId));
  const plans = React.useMemo(() => plansQuery.data ?? [], [plansQuery.data]);
  const [selectedPlanId, setSelectedPlanId] = React.useState("");
  const [filterOpen, setFilterOpen] = React.useState(false);
  const [filterText, setFilterText] = React.useState("");
  const [zoomIndex, setZoomIndex] = React.useState(1);
  React.useEffect(() => {
    if (!plans.length) setSelectedPlanId("");
    else if (!selectedPlanId || !plans.some((plan) => plan.id === selectedPlanId)) setSelectedPlanId(plans[0].id);
  }, [plans, selectedPlanId]);
  const planId = selectedPlanId;
  const activitiesQuery = usePlanningActivitiesFull(projectId, planId);
  const activities = React.useMemo(() => activitiesQuery.data ?? [], [activitiesQuery.data]);
  const visibleActivities = React.useMemo(() => activities
    .filter((activity) => `${activity.name} ${activity.code}`.toLocaleLowerCase().includes(filterText.trim().toLocaleLowerCase()))
    .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.name.localeCompare(b.name)), [activities, filterText]);
  const project = projects.find((item) => item.publicId === projectId);
  const months = React.useMemo(() => buildGanttMonths([
    utcDateValue(project?.startDate),
    utcDateValue(project?.endDate),
    ...activities.flatMap((activity) => [utcDateValue(activity.startDate), utcDateValue(activity.endDate)]),
  ]), [activities, project?.endDate, project?.startDate]);
  const rangeStart = months[0] ?? monthStart(Date.now());
  const rangeEnd = months.length ? new Date(months[months.length - 1]!).setUTCMonth(new Date(months[months.length - 1]!).getUTCMonth() + 1) : rangeStart + DAY_MS;
  const monthWidth = MONTH_WIDTHS[zoomIndex] ?? MONTH_WIDTHS[1];
  const monthFormatter = new Intl.DateTimeFormat(locale === "ar" ? "ar-SA-u-nu-latn" : "en-US", { month: "short", year: "numeric", timeZone: "UTC" });
  const projectStart = formatGanttDate(project?.startDate, locale);
  const projectEnd = formatGanttDate(project?.endDate, locale);

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("projects.workspaceViews.ganttTitle")}
        description={t("projects.workspaceViews.ganttDescription")}
        actions={<>
          <Button type="button" variant="outline" size="sm" aria-label={`${t("projects.workspaceViews.zoom")} ${ZOOM_LABELS[zoomIndex]}%`} onClick={() => setZoomIndex((current) => (current + 1) % MONTH_WIDTHS.length)}><ZoomOut className="me-1 h-4 w-4" />{t("projects.workspaceViews.zoom")}</Button>
          <Button type="button" variant={filterOpen ? "secondary" : "outline"} size="sm" aria-pressed={filterOpen} onClick={() => setFilterOpen((open) => !open)}><Filter className="me-1 h-4 w-4" />{t("projects.workspaceViews.filter")}</Button>
        </>}
      />
      {projectsQuery.isError || plansQuery.isError || activitiesQuery.isError ? <WorkspaceError message={queryError(projectsQuery) ?? queryError(plansQuery) ?? queryError(activitiesQuery) ?? t("common.errorLoading")} /> : null}
      <div className="flex flex-wrap items-end gap-4">
        <ProjectPicker projects={projects} value={projectId} onChange={setProjectId} label={t("projects.workspaceViews.selectProject")} locale={locale} />
        <label className="flex min-w-0 flex-col gap-1.5 text-sm"><span className="text-xs font-medium text-muted-foreground">{t("projects.workspaceViews.selectPlan")}</span><select className="h-10 min-w-[16rem] rounded-md border border-input bg-background px-3 text-sm" value={planId} onChange={(event) => setSelectedPlanId(event.target.value)} disabled={!plans.length}><option value="">{plans.length ? t("projects.workspaceViews.selectPlan") : t("projects.workspaceViews.noPlans")}</option>{plans.map((plan) => <option key={plan.id} value={plan.id}>{plan.name} · {plan.status}</option>)}</select></label>
      </div>
      {filterOpen ? <Input aria-label={t("projects.workspaceViews.filterTasks")} placeholder={t("projects.workspaceViews.filterTasks")} value={filterText} onChange={(event) => setFilterText(event.target.value)} className="max-w-sm" /> : null}
      {projectsQuery.isLoading || plansQuery.isLoading ? <WorkspaceLoading label={t("common.loading")} /> : !projectId ? <WorkspaceLoading label={t("projects.workspaceViews.noProjects")} /> : !plans.length ? <WorkspaceLoading label={t("projects.workspaceViews.noPlans")} /> : activitiesQuery.isLoading ? <WorkspaceLoading label={t("common.loading")} /> : !activities.length ? <WorkspaceLoading label={t("projects.workspaceViews.noActivities")} /> : (
        <Card>
          <CardHeader><div className="flex flex-wrap items-center justify-between gap-3"><div><CardTitle className="flex items-center gap-2 text-base"><CalendarDays className="h-4 w-4" />{project?.name ?? t("projects.workspaceViews.ganttTitle")}</CardTitle><CardDescription className="mt-1">{t("projects.workspaceViews.activitiesFromPlan")}</CardDescription></div><div className="flex items-center gap-2 text-xs text-muted-foreground"><CalendarDays className="h-3.5 w-3.5 shrink-0" /><span>{projectStart ?? t("common.notAvailable")} – {projectEnd ?? t("common.notAvailable")}</span></div></div></CardHeader>
          <CardContent className="overflow-x-auto p-0">
            <div dir={locale === "ar" ? "rtl" : "ltr"}>
              <div className="grid border-b bg-muted/20" style={{ gridTemplateColumns: `16rem repeat(${months.length}, ${monthWidth}px)` }}>
                <div className="flex items-center border-e px-3 py-2 text-xs font-medium text-muted-foreground">{t("projects.workspaceViews.taskPhase")}</div>
                {months.map((month) => <div key={month} className="border-e px-1 py-2 text-center text-[10px] font-medium text-muted-foreground">{monthFormatter.format(month)}</div>)}
              </div>
              {visibleActivities.length ? visibleActivities.map((activity) => <GanttActivityRow key={activity.id} activity={activity} rangeStart={rangeStart} rangeEnd={rangeEnd} months={months} monthWidth={monthWidth} locale={locale} t={t} />) : <p className="px-4 py-10 text-center text-sm text-muted-foreground">{filterText.trim() ? t("projects.workspaceViews.noMatchingActivities") : t("projects.workspaceViews.noActivities")}</p>}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

export function ProjectResourceMatrixPage() {
  const { t } = useI18n();
  const projectsQuery = useProjects({ page: 1, pageSize: 200 });
  const resourcesQuery = useResources({ page: 1, pageSize: 200, isActive: true });
  const projects = (projectsQuery.data?.items ?? []).filter((project) => !isWorkspaceExcluded(project)).slice(0, 6);
  const resources = resourcesQuery.data?.items ?? [];
  const isLoading = projectsQuery.isLoading || resourcesQuery.isLoading;
  const error = queryError(projectsQuery) ?? queryError(resourcesQuery);
  return (
    <div className="space-y-6">
      <PageHeader title={t("projects.workspaceViews.matrixTitle")} description={t("projects.workspaceViews.matrixDescription")} />
      {error ? <WorkspaceError message={error} /> : null}
      {isLoading ? <WorkspaceLoading label={t("common.loading")} /> : !resources.length ? <WorkspaceLoading label={t("projects.workspaceViews.noResources")} /> : (
        <Card><CardContent className="overflow-x-auto p-0"><table className="min-w-[60rem] w-full text-sm"><thead><tr className="border-b bg-muted/30"><th className="px-4 py-3 text-start">{t("projects.workspaceViews.resource")}</th><th className="px-4 py-3 text-start">{t("projects.workspaceViews.role")}</th><th className="px-4 py-3 text-start">{t("projects.workspaceViews.utilization")}</th>{projects.map((project) => <th key={project.id} className="px-4 py-3 text-start">{project.code}</th>)}<th className="px-4 py-3 text-start">{t("projects.workspaceViews.totalAllocation")}</th></tr></thead><tbody>{resources.map((resource) => { const total = resource.allocations.reduce((sum, allocation) => sum + (allocation.allocationPercentage ?? 0), 0); return <tr key={resource.id} className="border-b last:border-0 hover:bg-muted/30"><td className="px-4 py-3"><div className="flex items-center gap-2 font-medium"><Users className="h-4 w-4 text-muted-foreground" />{resource.name}</div></td><td className="px-4 py-3 text-muted-foreground">{resource.jobTitle ?? t("common.notAvailable")}</td><td className="min-w-40 px-4 py-3"><div className="flex items-center gap-2"><Progress value={resource.currentUtilization ?? total} className="h-2" /><span className="text-xs tabular-nums">{Math.round(resource.currentUtilization ?? total)}%</span></div></td>{projects.map((project) => { const allocation = resource.allocations.filter((item) => item.projectId === project.id).reduce((sum, item) => sum + (item.allocationPercentage ?? 0), 0); return <td key={project.id} className="px-4 py-3">{allocation ? <Badge status={allocation > 100 ? "Critical" : allocation > 80 ? "Medium" : "Low"}>{Math.round(allocation)}%</Badge> : <span className="text-muted-foreground">—</span>}</td>})}<td className="px-4 py-3 font-medium">{Math.round(total)}%</td></tr>; })}</tbody></table></CardContent></Card>
      )}
    </div>
  );
}

export function ProjectTemplatesPage() {
  const { t, formatDate } = useI18n();
  const documentsQuery = useDocuments({ page: "1", pageSize: "100", templatesOnly: "true" });
  const documents = documentsQuery.data?.items ?? [];
  const categories = Array.from(new Set(documents.map((document) => document.categoryName).filter(Boolean))) as string[];
  const [category, setCategory] = React.useState("all");
  const visible = category === "all" ? documents : documents.filter((document) => document.categoryName === category);
  return (
    <div className="space-y-6">
      <PageHeader title={t("projects.workspaceViews.templatesTitle")} description={t("projects.workspaceViews.templatesDescription")} actions={<Link to="/documents" className="inline-flex h-9 items-center gap-2 rounded-md bg-primary px-3 text-sm font-medium text-primary-foreground hover:bg-primary/90"><FolderOpen className="h-4 w-4" />{t("projects.workspaceViews.openDocuments")}</Link>} />
      {documentsQuery.isError ? <WorkspaceError message={queryError(documentsQuery) ?? t("common.errorLoading")} /> : null}
      {documentsQuery.isLoading ? <WorkspaceLoading label={t("common.loading")} /> : !documents.length ? <Card><CardContent className="flex flex-col items-center gap-3 p-10 text-center"><FileText className="h-10 w-10 text-muted-foreground" /><p className="font-medium">{t("documents.templatesEmptyTitle")}</p><p className="max-w-lg text-sm text-muted-foreground">{t("documents.templatesEmptyDescription")}</p></CardContent></Card> : (
        <>
          {categories.length ? <label className="flex max-w-xs flex-col gap-1.5 text-sm"><span className="text-xs font-medium text-muted-foreground">{t("documents.table.type")}</span><select className="h-10 rounded-md border border-input bg-background px-3 text-sm" value={category} onChange={(event) => setCategory(event.target.value)}><option value="all">{t("common.all")}</option>{categories.map((item) => <option key={item} value={item}>{item}</option>)}</select></label> : null}
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{visible.map((document: DocumentDto) => <Card key={document.id}><CardHeader><CardTitle className="flex items-start gap-2 text-base"><FileText className="mt-0.5 h-4 w-4 shrink-0 text-primary" />{document.name}</CardTitle><CardDescription>{document.categoryName ?? t("documents.categoryFallback")}</CardDescription></CardHeader><CardContent className="space-y-3 text-sm"><p className="text-muted-foreground">{document.description ?? document.fileName ?? t("common.notAvailable")}</p><div className="flex justify-between text-xs text-muted-foreground"><span>{t("projects.workspaceViews.version")} {document.version}</span><span>{document.updatedAt ? formatDate(document.updatedAt) : t("common.notAvailable")}</span></div><Link to="/documents" className="inline-flex h-8 w-full items-center justify-center rounded-md border border-input bg-background px-3 text-xs font-medium hover:bg-accent">{t("projects.workspaceViews.openDocuments")}</Link></CardContent></Card>)}</div>
        </>
      )}
    </div>
  );
}
