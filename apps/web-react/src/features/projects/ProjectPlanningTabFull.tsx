import * as React from "react";
import { useNavigate, useOutletContext } from "react-router-dom";
import { CalendarDays, ChevronDown, ChevronRight, ClipboardList, Download, Eye, FileUp, GitBranch, GitCompareArrows, Plus, RefreshCw, Save, Trash2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Badge } from "@components/ui/Badge";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { ApiError } from "@api/client";
import { useManagement } from "@api/management";
import { useToast } from "@hooks/use-toast";
import { hasPermission, Permissions } from "@lib/permissions";
import { usePlanningActivitiesFull, usePlanningAssignmentsFull, usePlanningAssignmentCapacityFull, usePlanningCalendarsFull, usePlanningDependenciesFull, usePlanningFullMutations, usePlanningPlansFull, usePlanningPlanFull, usePlanningSchedulingAnalysisFull, usePlanningTablePreferencesFull, usePlanningWbsFull, type PlanActivityDto, type PlanAssignmentDto } from "./api/planningFull";
import { useProjectTeam, type ProjectDto } from "./api/projects";
import { usePlanningTableMutations } from "./api/planning-table-mutations";
import { PlanningActivityDetailsSheet } from "./PlanningActivityDetailsSheet";
import { PlanningCreatePlanPanel } from "./components/PlanningCreatePlanPanel";
import { PlanningBaselineAccessHint } from "./components/PlanningBaselineAccessHint";
import { PlanningImportSheet } from "./components/PlanningImportSheet";
import { PlanningRibbonGroup, PlanningRibbonTabs, WorkspaceButton, type PlanningRibbonTab } from "./components/PlanningRibbon";
import { PlanningGrid } from "./components/PlanningGrid";
import { PlanningCalendarsPanel } from "./components/PlanningCalendarsPanel";
import { PlanTimelineGantt } from "./components/PlanTimelineGantt";
import { CriticalPathNetworkDiagram } from "./components/CriticalPathNetworkDiagram";
import { buildPlanWorkspaceRows, visiblePlanWorkspaceRows } from "./lib/plan-workspace-rows";
import { downloadPlanningWorkbook } from "./lib/planning-export";
import { canPerformPlanningWrites, findActiveWorkingVersion, getPlanningBaselineUnavailableReason } from "./lib/planning-access";
import { GANTT_ZOOM_LEVELS, type GanttZoom } from "./lib/gantt-constants";

type ViewMode = "split" | "table" | "timeline" | "network";
type AssignmentFormState = { activityId: string; resourceId: string; assignedWorkHours: string; startDate: string; endDate: string };
const emptyAssignmentForm = (): AssignmentFormState => ({ activityId: "", resourceId: "", assignedWorkHours: "", startDate: "", endDate: "" });
function planningLabel(t: (key: string) => string, group: "planType" | "versionType" | "versionStatus" | "activityType" | "dependencyType", value?: string | null) {
  if (!value) return "—";
  const label = t(`enums.planning.${group}.${value}`);
  return label === `enums.planning.${group}.${value}` ? value : label;
}
function Loading({ text }: { text: string }) { return <p className="py-10 text-center text-sm text-muted-foreground">{text}</p>; }

export function ProjectPlanningTabFull() {
  const { projectId, project } = useOutletContext<{ projectId: string; project: ProjectDto }>();
  const navigate = useNavigate();
  const { t: translate, formatNumber } = useI18n();
  const confirm = useConfirm();
  const { toast } = useToast();
  const t = React.useCallback((key: string, params?: Record<string, string | number>) => translate(key.startsWith("planning.") ? `projects.${key}` : key, params), [translate]);
  const { user } = useAuth();
  const { effectiveReadOnly, canMutate } = useManagement();
  const [ribbonTab, setRibbonTab] = React.useState<PlanningRibbonTab>("project");
  const [ribbonExpanded, setRibbonExpanded] = React.useState(true);
  const [versionsOpen, setVersionsOpen] = React.useState(false);
  const [baselineOpen, setBaselineOpen] = React.useState(false);
  const hasManagePlanningPermission = Boolean(user && hasPermission(user.permissions, Permissions.Planning.Edit));
  const canManagePlanning = canPerformPlanningWrites(hasManagePlanningPermission, canMutate, effectiveReadOnly);
  const canViewStatusReports = Boolean(user && hasPermission(user.permissions, Permissions.StatusReport.View));
  const baselineUnavailableReason = getPlanningBaselineUnavailableReason(
    Boolean(user && hasPermission(user.permissions, Permissions.Planning.Lifecycle)),
    canMutate,
    effectiveReadOnly,
  );
  const canBaseline = baselineUnavailableReason === null;
  const plans = usePlanningPlansFull(projectId);
  const [selectedPlanId, setSelectedPlanId] = React.useState("");
  const planId = selectedPlanId || plans.data?.[0]?.id || "";
  const planDetail = usePlanningPlanFull(projectId, planId);
  const wbs = usePlanningWbsFull(projectId, planId);
  const activities = usePlanningActivitiesFull(projectId, planId);
  const dependencies = usePlanningDependenciesFull(projectId, planId);
  const tablePreferences = usePlanningTablePreferencesFull(projectId, planId);
  const mutations = usePlanningTableMutations(projectId, planId);
  const [view, setView] = React.useState<ViewMode>("table");
  const schedulingAnalysis = usePlanningSchedulingAnalysisFull(projectId, planId, view === "network");
  const [zoom, setZoom] = React.useState<GanttZoom>("day");
  const [showBaseline, setShowBaseline] = React.useState(false);
  const [collapsedWbs, setCollapsedWbs] = React.useState<ReadonlySet<string>>(() => new Set());
  const [selectedRowId, setSelectedRowId] = React.useState("");
  const [splitWidthPct, setSplitWidthPct] = React.useState(52);
  const splitContainerRef = React.useRef<HTMLDivElement>(null);
  const gridPaneRef = React.useRef<HTMLDivElement>(null);
  const ganttPaneRef = React.useRef<HTMLDivElement>(null);
  const [ganttRowOffset, setGanttRowOffset] = React.useState(0);
  const splitDraggingRef = React.useRef(false);
  const beginSplitDrag = (event: React.PointerEvent<HTMLDivElement>) => { splitDraggingRef.current = true; event.currentTarget.setPointerCapture(event.pointerId); };
  const moveSplitDrag = (event: React.PointerEvent<HTMLDivElement>) => {
    if (!splitDraggingRef.current || !splitContainerRef.current) return;
    const rect = splitContainerRef.current.getBoundingClientRect();
    if (rect.width === 0) return;
    const rtl = getComputedStyle(splitContainerRef.current).direction === "rtl";
    const pct = (rtl ? (rect.right - event.clientX) : (event.clientX - rect.left)) / rect.width * 100;
    setSplitWidthPct(Math.min(75, Math.max(25, pct)));
  };
  const endSplitDrag = () => { splitDraggingRef.current = false; };
  const versions = planDetail.data?.versions ?? [];
  const activeWorkingVersion = findActiveWorkingVersion(versions);
  const workingVersion = activeWorkingVersion ?? versions.find((v) => v.type === "Working");
  const currentBaseline = versions.filter((v) => v.type === "Baseline" && v.status === "Approved").sort((a, b) => b.versionNumber - a.versionNumber)[0];
  const editable = Boolean(activeWorkingVersion?.id && canManagePlanning && canMutate && !effectiveReadOnly);
  const [planOpen, setPlanOpen] = React.useState(false);
  const [activityOpen, setActivityOpen] = React.useState(false);
  const [selectedActivity, setSelectedActivity] = React.useState<PlanActivityDto | undefined>();
  const [importOpen, setImportOpen] = React.useState(false);
  const [planExportPending, setPlanExportPending] = React.useState(false);
  const [planExportError, setPlanExportError] = React.useState(false);
  const [planningPanel, setPlanningPanel] = React.useState<"assignments" | "calendar" | null>(null);
  const [selectedCalendarId, setSelectedCalendarId] = React.useState("");
  const [assignmentFormOpen, setAssignmentFormOpen] = React.useState(false);
  const [selectedAssignment, setSelectedAssignment] = React.useState<PlanAssignmentDto | null>(null);
  const [assignmentForm, setAssignmentForm] = React.useState<AssignmentFormState>(emptyAssignmentForm);
  const assignmentSaveInFlight = React.useRef(false);
  const calendarMutations = usePlanningFullMutations(projectId, planId);
  const assignments = usePlanningAssignmentsFull(projectId, planId, planningPanel === "assignments");
  const projectTeam = useProjectTeam(projectId);
  const assignmentResources = React.useMemo(() => (projectTeam.data ?? [])
    .filter((member) => member.status === "Active")
    .map((member) => ({ id: member.resourceId, name: member.resourceName, jobTitle: member.resourceJobTitle })), [projectTeam.data]);
  const assignmentCapacity = usePlanningAssignmentCapacityFull(
    projectId, planId, assignmentForm.resourceId, assignmentForm.startDate, assignmentForm.endDate,
    selectedAssignment?.id, assignmentFormOpen,
  );
  const calendars = usePlanningCalendarsFull(projectId, planId, planningPanel === "calendar");
  const workspaceRows = React.useMemo(() => buildPlanWorkspaceRows(wbs.data ?? [], activities.data ?? []), [wbs.data, activities.data]);
  const visibleRows = React.useMemo(() => visiblePlanWorkspaceRows(workspaceRows, collapsedWbs), [workspaceRows, collapsedWbs]);
  React.useEffect(() => { setCollapsedWbs(new Set()); }, [planId]);
  React.useEffect(() => {
    if (!tablePreferences.data || tablePreferences.data.planId !== planId) return;
    const savedCollapsed = new Set(tablePreferences.data.collapsedRowIds);
    setCollapsedWbs(savedCollapsed);
  }, [planId, tablePreferences.data, workspaceRows]);
  React.useEffect(() => {
    if (view !== "split") return undefined;
    const measure = () => {
      const container = splitContainerRef.current;
      const gridRow = gridPaneRef.current?.querySelector("tbody tr");
      const ganttRow = ganttPaneRef.current?.querySelector('[data-testid="planning-gantt-row"]');
      if (!container || !gridRow || !ganttRow) return;
      const containerTop = container.getBoundingClientRect().top;
      const delta = (gridRow.getBoundingClientRect().top - containerTop) - (ganttRow.getBoundingClientRect().top - containerTop);
      setGanttRowOffset((prev) => {
        const next = Math.max(0, Math.round(prev + delta));
        return next === prev ? prev : next;
      });
    };
    const raf = requestAnimationFrame(measure);
    const observer = new ResizeObserver(measure);
    if (gridPaneRef.current) observer.observe(gridPaneRef.current);
    if (ganttPaneRef.current) observer.observe(ganttPaneRef.current);
    return () => { cancelAnimationFrame(raf); observer.disconnect(); };
  }, [view, visibleRows]);
  React.useEffect(() => { if (!selectedPlanId && plans.data?.length && !plans.isFetching) setSelectedPlanId(plans.data[0].id); }, [plans.data, plans.isFetching, selectedPlanId]);
  React.useEffect(() => { setSelectedCalendarId(""); }, [planId]);
  if (plans.isLoading) return <Loading text={t("common.loading")} />;
  if (plans.isError) return <p className="py-10 text-center text-sm text-destructive">{t("common.errorLoading")}</p>;
  const selectedPlan = plans.data?.find(plan => plan.id === planId);
  function openAssignmentForm(item?: PlanAssignmentDto) {
    const activityId = item?.activityId ?? "";
    const activity = (activities.data ?? []).find(candidate => candidate.id === activityId);
    setSelectedAssignment(item ?? null);
    setAssignmentForm({
      activityId,
      resourceId: item?.resourceId ?? "",
      assignedWorkHours: item?.assignedWorkHours == null && item?.plannedHours == null
        ? ""
        : String(item.assignedWorkHours ?? item.plannedHours),
      startDate: item?.startDate?.slice(0, 10) ?? activity?.startDate?.slice(0, 10) ?? "",
      endDate: item?.endDate?.slice(0, 10) ?? activity?.endDate?.slice(0, 10) ?? "",
    });
    setAssignmentFormOpen(true);
  }
  async function exportCurrentPlan() {
    if (!selectedPlan || !workingVersion || planExportPending) return;
    setPlanExportPending(true);
    setPlanExportError(false);
    const localizeValue = (group: "planType" | "activityType" | "schedulingMode" | "constraintType" | "dependencyType", value: string) => {
      const key = group === "planType" || group === "activityType"
        ? `enums.planning.${group}.${value}`
        : group === "schedulingMode"
          ? `projects.planning.schedulingStatusValues.${value}`
          : group === "constraintType"
            ? `projects.planning.constraintTypes.${value}`
            : `enums.planning.dependencyType.${value}`;
      const translated = translate(key);
      return translated === key ? value : translated;
    };
    try {
      await downloadPlanningWorkbook({
        project: { name: project.name, code: project.code },
        plan: { name: selectedPlan.name, planType: selectedPlan.planType },
        workingVersionNumber: workingVersion.versionNumber,
        wbs: wbs.data ?? [],
        activities: activities.data ?? [],
        dependencies: dependencies.data ?? [],
        localizeValue,
        labels: {
          planInfoSheet: t("planning.export.planInfoSheet"),
          tasksSheet: t("planning.export.tasksSheet"),
          project: t("planning.export.project"),
          projectCode: t("planning.export.projectCode"),
          plan: t("planning.export.plan"),
          planType: t("planning.export.planType"),
          workingVersion: t("planning.export.workingVersion"),
          outline: t("planning.export.outline"),
          taskId: t("planning.export.taskId"),
          taskName: t("planning.export.taskName"),
          taskType: t("planning.export.taskType"),
          schedulingMode: t("planning.export.schedulingMode"),
          start: t("planning.export.start"),
          finish: t("planning.export.finish"),
          duration: t("planning.export.duration"),
          progress: t("planning.export.progress"),
          predecessors: t("planning.export.predecessors"),
          constraint: t("planning.export.constraint"),
          constraintDate: t("planning.export.constraintDate"),
          deadline: t("planning.export.deadline"),
          workPackage: t("planning.export.workPackage"),
          task: t("planning.task"),
          milestone: t("planning.milestone"),
          summary: t("planning.export.summary"),
        },
      });
    } catch {
      setPlanExportError(true);
    } finally {
      setPlanExportPending(false);
    }
  }
  const toggleCollapse = (row: { wbs?: { id: string } }) => {
    if (!row.wbs) return;
    setCollapsedWbs((prev) => { const next = new Set(prev); if (next.has(row.wbs!.id)) next.delete(row.wbs!.id); else next.add(row.wbs!.id); return next; });
  };
  function openCreatePlan() { mutations.createPlan.reset(); setPlanOpen(true); }
  function cancelCreatePlan() { mutations.createPlan.reset(); setPlanOpen(false); }
  function handlePlanCreated(createdPlanId: string) { setSelectedPlanId(createdPlanId); setPlanOpen(false); }
  async function captureBaseline() { const versions = planDetail.data?.versions ?? []; const hasBaseline = versions.some((item) => item.type === "Baseline"); if (hasBaseline && !await confirm({ description: t("planning.replaceBaselineWarning") })) return; if (activeWorkingVersion) mutations.captureBaseline.mutate(activeWorkingVersion.rowVersion); }
  async function recalculateSchedule() {
    try {
      await calendarMutations.recalculateSchedule.mutateAsync();
      toast({ title: t("planning.scheduleRecalculated") });
    } catch (error) {
      toast({ title: t("common.error"), description: error instanceof Error ? error.message : t("planning.recalculateFailed"), variant: "destructive" });
    }
  }
  async function saveAssignment(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!planId || assignmentSaveInFlight.current) return;
    const assignedWorkHours = Number(assignmentForm.assignedWorkHours);
    if (!assignmentForm.activityId || !assignmentForm.resourceId || !assignmentForm.startDate || !assignmentForm.endDate
      || !Number.isFinite(assignedWorkHours) || assignedWorkHours <= 0) return;
    if (assignmentCapacity.isFetching) {
      toast({ title: t("common.error"), description: t("planning.capacityLoading"), variant: "destructive" });
      return;
    }
    if (assignmentCapacity.isError || !assignmentCapacity.data) {
      toast({ title: t("common.error"), description: t("planning.capacityLoadFailed"), variant: "destructive" });
      return;
    }
    if (assignmentCapacity.data.isScheduleWithinMembership === false) {
      toast({ title: t("common.error"), description: t("planning.assignmentMembershipLimit"), variant: "destructive" });
      return;
    }
    if (assignedWorkHours > assignmentCapacity.data.remainingAssignableHours + 0.0001) {
      toast({ title: t("common.error"), description: t("planning.capacityOverLimit"), variant: "destructive" });
      return;
    }
    assignmentSaveInFlight.current = true;

    try {
      const body = {
        activityId: assignmentForm.activityId,
        resourceId: assignmentForm.resourceId,
        assignedWorkHours,
        startDate: assignmentForm.startDate,
        endDate: assignmentForm.endDate,
      };

      const alreadyAssigned = (assignments.data ?? []).some((assignment) =>
        assignment.activityId === body.activityId
        && assignment.resourceId === body.resourceId
        && assignment.id !== selectedAssignment?.id);
      if (alreadyAssigned) {
        toast({ title: t("common.error"), description: t("planning.assignmentAlreadyAssigned"), variant: "destructive" });
        return;
      }

      if (selectedAssignment) {
        await mutations.updateAssignment.mutateAsync({ id: selectedAssignment.id, body: { ...body, rowVersion: selectedAssignment.rowVersion } });
      } else {
        await mutations.createAssignment.mutateAsync(body);
      }

      setAssignmentFormOpen(false);
      setSelectedAssignment(null);
      setAssignmentForm(emptyAssignmentForm());
    } catch (error) {
      const isConflict = error instanceof ApiError && error.status === 409;
      if (isConflict) void assignments.refetch();

      const isDuplicateConflict = isConflict
        && error.message.toLowerCase().includes("already assigned to the activity");
      const messageKey = isDuplicateConflict
        ? "planning.assignmentAlreadyAssigned"
        : isConflict
          ? "planning.assignmentConflict"
          : "planning.assignmentSaveFailed";
      toast({ title: t("common.error"), description: t(messageKey), variant: "destructive" });
    } finally {
      assignmentSaveInFlight.current = false;
    }
  }
  return <div className="space-y-6"><Card className="planning-workspace font-planning shadow-none"><CardContent className="p-0">
<div className="sticky top-0 z-10 border-b bg-background/95 shadow-sm backdrop-blur">
<div className="flex min-w-0 items-center gap-3 border-b bg-muted/20 px-3 py-1.5">
<div className="min-w-0 flex-1">
<p className="truncate text-xs font-semibold" title={selectedPlan?.name ?? t("planning.selectPlan")}>{selectedPlan?.name ?? t("planning.selectPlan")}</p>
<p className="truncate text-[10px] text-muted-foreground">{selectedPlan ? planningLabel(t, "planType", selectedPlan.planType) : "—"}</p>
</div>
<div className="flex shrink-0 flex-wrap items-center justify-end gap-1">
<Badge className="h-5 px-1.5 text-[10px]" status={editable ? "Active" : "ReadOnly"}>{planningLabel(t, "versionType", "Working")}: {workingVersion ? `${t("planning.versionShort")} ${workingVersion.versionNumber}` : t("planning.unavailable")}</Badge>
<Badge className="h-5 px-1.5 text-[10px]" status={editable ? "Active" : "ReadOnly"}>{planningLabel(t, "versionStatus", "Active")}: {editable ? t("planning.editable") : t("planning.readOnly")}</Badge>
<Badge className="h-5 px-1.5 text-[10px]" status={currentBaseline ? "Approved" : "ReadOnly"} title={currentBaseline ? t("planning.baselineReadOnly") : undefined}>{planningLabel(t, "versionType", "Baseline")}: {currentBaseline ? `${t("planning.versionShort")} ${currentBaseline.versionNumber} · ${currentBaseline.createdAt?.slice(0, 10) ?? ""}` : t("planning.none")}</Badge>
</div>
</div>
<div className="flex items-center gap-2 border-b px-2 pt-1"><div className="min-w-0 overflow-x-auto"><PlanningRibbonTabs ariaLabel={t("planning.ribbonTabs.label")} activeTab={ribbonTab} tabs={[{ id: "file", label: t("planning.ribbonTabs.file") }, { id: "task", label: t("planning.ribbonTabs.task") }, { id: "resource", label: t("planning.ribbonTabs.resource") }, { id: "project", label: t("planning.ribbonTabs.project") }, { id: "view", label: t("planning.ribbonTabs.view") }, { id: "report", label: t("planning.ribbonTabs.report") }]} onChange={(tab) => { setRibbonTab(tab); if (tab === "task" || tab === "resource") setView("table"); }} /></div><Button type="button" variant="ghost" size="sm" className="h-7 shrink-0 px-2 text-[11px]" aria-expanded={ribbonExpanded} aria-controls="planning-ribbon-panel" onClick={() => setRibbonExpanded((expanded) => !expanded)}><ChevronDown className={`me-1 h-3.5 w-3.5 transition-transform ${ribbonExpanded ? "" : "-rotate-90"}`} />{t(ribbonExpanded ? "planning.collapseRibbon" : "planning.expandRibbon")}</Button></div>
{ribbonExpanded && <div id="planning-ribbon-panel" className="overflow-x-auto px-1.5 py-1">
{ribbonTab === "file" && <div className="flex min-w-max items-stretch gap-1">
<PlanningRibbonGroup label={t("planning.ribbon.file")}>
{canManagePlanning ? <WorkspaceButton onClick={() => setImportOpen(true)} icon={<FileUp className="h-3.5 w-3.5" />}>{t("planning.import.action")}</WorkspaceButton> : null}
<WorkspaceButton onClick={() => void exportCurrentPlan()} disabled={!selectedPlan || !workingVersion || planExportPending} icon={<Download className="h-3.5 w-3.5" />}>{t(planExportPending ? "planning.export.exporting" : "planning.export.action")}</WorkspaceButton>
</PlanningRibbonGroup>
{planExportError ? <p role="alert" className="self-center rounded border border-destructive/30 bg-destructive/5 px-2 py-1 text-xs text-destructive">{t("planning.export.failed")}</p> : null}
</div>}
{ribbonTab === "project" && <div className="flex min-w-max items-stretch gap-1">
<PlanningRibbonGroup label={t("planning.ribbon.plan")}>
<div className="w-48 max-w-[13rem] shrink-0"><Select value={planId} onValueChange={setSelectedPlanId}>
<SelectTrigger aria-label={t("planning.selectPlan")} className="h-7 w-48 max-w-[13rem] shrink-0 text-[11px]"><SelectValue placeholder={t("planning.selectPlan")} /></SelectTrigger>
<SelectContent>{(plans.data ?? []).map((plan) => <SelectItem key={plan.id} value={plan.id}>{plan.name} · {planningLabel(t, "planType", plan.planType)}</SelectItem>)}</SelectContent>
</Select></div>
{canManagePlanning ? <Button type="button" variant="default" size="sm" className="h-7 shrink-0 gap-1 px-2 text-[11px]" onClick={planOpen ? cancelCreatePlan : openCreatePlan}><Plus className="h-3.5 w-3.5" />{t(planOpen ? "planning.cancelCreatePlan" : "planning.createPlan")}</Button> : null}
</PlanningRibbonGroup>
<PlanningRibbonGroup label={t("planning.ribbon.tools")}>
{canBaseline ? <WorkspaceButton onClick={() => void captureBaseline()} disabled={!activeWorkingVersion?.rowVersion || mutations.captureBaseline.isPending} icon={<Save className="h-3.5 w-3.5" />}>{t("planning.setBaseline")}</WorkspaceButton> : null}
{canManagePlanning ? <WorkspaceButton onClick={() => void recalculateSchedule()} disabled={!editable || !planId || calendarMutations.recalculateSchedule.isPending} icon={<RefreshCw className={`h-3.5 w-3.5 ${calendarMutations.recalculateSchedule.isPending ? "animate-spin" : ""}`} />}>{t(calendarMutations.recalculateSchedule.isPending ? "planning.recalculatingSchedule" : "planning.recalculateSchedule")}</WorkspaceButton> : null}
{planId ? <PlanningBaselineAccessHint reason={baselineUnavailableReason} t={t} /> : null}
{currentBaseline ? <WorkspaceButton onClick={() => setBaselineOpen(true)} icon={<Eye className="h-3.5 w-3.5" />}>{t("planning.viewBaseline")}</WorkspaceButton> : null}
<WorkspaceButton onClick={() => setPlanningPanel("calendar")} icon={<CalendarDays className="h-3.5 w-3.5" />}>{t("planning.calendar")}</WorkspaceButton>
<WorkspaceButton onClick={() => setVersionsOpen(true)} icon={<ChevronRight className="h-3.5 w-3.5" />}>{t("planning.versions")}</WorkspaceButton>
</PlanningRibbonGroup>
</div>}
{ribbonTab === "project" && mutations.captureBaseline.isError ? <p role="alert" className="mt-1 rounded-md border border-destructive/30 bg-destructive/5 px-2 py-1 text-xs text-destructive">{t("planning.baselineCaptureFailed")}</p> : null}
{ribbonTab === "view" && <div className="flex min-w-max items-stretch gap-1">
<PlanningRibbonGroup label={t("planning.ribbon.view")}>
            <div className="flex items-center gap-0.5">
            {(["split", "table", "gantt", "criticalPath"] as const).map((option) => { const targetView = option === "gantt" ? "timeline" : option === "criticalPath" ? "network" : option; const active = view === targetView; return <Button key={option} type="button" size="sm" variant={active ? "secondary" : "ghost"} aria-pressed={active} onClick={() => setView(targetView)} className={`h-7 px-2 text-[11px] ${active ? "font-medium" : "font-normal text-muted-foreground"}`}>{t(`planning.views.${option}`)}</Button>;})}
          </div>
          {(view === "timeline" || view === "split") && <div className="flex items-center gap-0.5">
            {GANTT_ZOOM_LEVELS.map((level) => <Button key={level} type="button" size="sm" variant={zoom === level ? "secondary" : "ghost"} aria-pressed={zoom === level} onClick={() => setZoom(level)} className="h-7 px-1.5 text-[10px]">{t(`common.${level}`)}</Button>)}
          </div>}
          {(view === "timeline" || view === "split") && currentBaseline ? <Button type="button" size="sm" variant={showBaseline ? "secondary" : "ghost"} aria-pressed={showBaseline} onClick={() => setShowBaseline((v) => !v)} className="h-7 gap-1 px-2 text-[11px]"><GitCompareArrows className="h-3.5 w-3.5" />{t(showBaseline ? "planning.hideBaseline" : "planning.showBaseline")}</Button> : null}
</PlanningRibbonGroup>
</div>}
{ribbonTab === "report" && <div className="flex min-w-max items-stretch gap-1">
<PlanningRibbonGroup label={t("planning.ribbon.report")}>
<WorkspaceButton onClick={() => setView("network")} icon={<GitBranch className="h-3.5 w-3.5" />}>{t("planning.reportActions.criticalPath")}</WorkspaceButton>
{canViewStatusReports ? <WorkspaceButton onClick={() => navigate(`/projects/${projectId}/status-reports`)} icon={<ClipboardList className="h-3.5 w-3.5" />}>{t("planning.reportActions.statusReports")}</WorkspaceButton> : null}
</PlanningRibbonGroup>
</div>}
</div>}
{planOpen ? <div className="border-t p-3"><PlanningCreatePlanPanel mutation={mutations.createPlan} t={t} onCreated={handlePlanCreated} onCancel={cancelCreatePlan} /></div> : null}
</div>
{planId && planDetail.data && !activeWorkingVersion ? <p role="status" className="mx-4 mt-3 border-s-2 border-warning bg-warning/5 px-3 py-2 text-sm text-warning">{t("planning.noActiveWorkingVersion")}</p> : null}
{planningPanel ? (
  <SideSheet open onOpenChange={(open) => { if (!open) setPlanningPanel(null); }} title={t(`planning.panelTitles.${planningPanel}`)} description={t(`planning.panelDescriptions.${planningPanel}`)} closeLabel={t("common.close")}>
    <SideSheetBody className="space-y-4">
      {planningPanel === "assignments" ? (
        <PlanningAssignmentsPanel activities={activities.data ?? []} assignments={assignments.data ?? []} resources={assignmentResources} editable={editable} t={t} onAdd={() => openAssignmentForm()} onEdit={openAssignmentForm} onDelete={(item) => void mutations.deleteAssignment.mutateAsync({ id: item.id, rowVersion: item.rowVersion })} />
      ) : (
        <PlanningCalendarsPanel calendars={calendars.data ?? []} isLoading={calendars.isLoading} isFetching={calendars.isFetching} isError={calendars.isError} onRetry={() => void calendars.refetch()} editable={editable} selectedCalendarId={selectedCalendarId} onSelectCalendar={setSelectedCalendarId} mutations={calendarMutations} onCalendarCreated={(calendar) => setSelectedCalendarId(calendar.id)} t={t} />
      )}
    </SideSheetBody>
  </SideSheet>
) : null}
{!planId ? <p className="m-4 rounded border border-dashed p-8 text-center text-sm text-muted-foreground">{t("planning.emptyWorkspace")}</p> : wbs.isLoading || activities.isLoading ? <div className="p-4"><Loading text={t("common.loading")} /></div> : view === "split" ? (
  <div ref={splitContainerRef} className="flex items-stretch border-t">
    <div ref={gridPaneRef} className="min-w-0 shrink-0" style={{ width: `${splitWidthPct}%` }}>
      <PlanningGrid projectId={projectId} planId={planId} projectStartDate={project.startDate} wbs={wbs.data ?? []} activities={activities.data ?? []} dependencies={dependencies.data ?? []} mutations={mutations} editable={editable} showBaseline={showBaseline} ribbonTab={ribbonTab} ribbonExpanded={ribbonExpanded} t={t} selectedRowId={selectedRowId} collapsedWbsIds={collapsedWbs} onSelectionChange={setSelectedRowId} onCollapsedChange={(next) => setCollapsedWbs(new Set(next))} onOpenAssignments={() => setPlanningPanel("assignments")} onEdit={(activity) => { setSelectedActivity(activity); setActivityOpen(true); }} />
    </div>
    <div role="separator" aria-orientation="vertical" aria-label={t("planning.resizeSplitPane")} tabIndex={0} onPointerDown={beginSplitDrag} onPointerMove={moveSplitDrag} onPointerUp={endSplitDrag} onPointerCancel={endSplitDrag} className="w-1 shrink-0 cursor-col-resize touch-none bg-border hover:bg-primary/40 focus-visible:bg-primary/40" onKeyDown={(event) => { if (event.key === "ArrowLeft") { event.preventDefault(); setSplitWidthPct((p) => Math.max(25, p - 2)); } if (event.key === "ArrowRight") { event.preventDefault(); setSplitWidthPct((p) => Math.min(75, p + 2)); } }} />
    <div ref={ganttPaneRef} className="min-w-0 flex-1 overflow-x-auto" style={{ paddingTop: ganttRowOffset }}>
      <PlanTimelineGantt rows={visibleRows} dependencies={dependencies.data ?? []} selectedId={selectedRowId} zoom={zoom} collapsed={collapsedWbs} showBaseline={showBaseline} showIdentityPanel={false} onSelect={setSelectedRowId} onToggleCollapse={toggleCollapse} onOpenDetails={(row) => { const activity = row.activity; if (activity) { setSelectedActivity(activity); setActivityOpen(true); } }} />
    </div>
  </div>
) : view === "table" ? (
  <PlanningGrid projectId={projectId} planId={planId} projectStartDate={project.startDate} wbs={wbs.data ?? []} activities={activities.data ?? []} dependencies={dependencies.data ?? []} mutations={mutations} editable={editable} showBaseline={showBaseline} ribbonTab={ribbonTab} ribbonExpanded={ribbonExpanded} t={t} selectedRowId={selectedRowId} collapsedWbsIds={collapsedWbs} onSelectionChange={setSelectedRowId} onCollapsedChange={(next) => setCollapsedWbs(new Set(next))} onOpenAssignments={() => setPlanningPanel("assignments")} onEdit={(activity) => { setSelectedActivity(activity); setActivityOpen(true); }} />
) : view === "network" ? (
  <CriticalPathNetworkDiagram
    projectId={projectId}
    projectName={project.name}
    projectCode={project.code}
    planId={planId}
    planName={selectedPlan?.name ?? ""}
    workingVersionId={workingVersion?.id ?? ""}
    workingVersionNumber={workingVersion?.versionNumber ?? 0}
    activities={activities.data ?? []}
    dependencies={dependencies.data ?? []}
    wbs={wbs.data ?? []}
    analysis={schedulingAnalysis.data}
    isLoading={dependencies.isLoading || schedulingAnalysis.isLoading}
    isError={schedulingAnalysis.isError}
    onRetry={() => void schedulingAnalysis.refetch()}
    onOpenDetails={(activity) => { setSelectedActivity(activity); setActivityOpen(true); }}
    onOpenInGrid={(activityId) => { setCollapsedWbs(new Set()); setSelectedRowId(`activity:${activityId}`); setRibbonTab("view"); setView("table"); }}
    t={t}
  />
) : (
  <div className="m-4 overflow-x-auto rounded-lg border"><PlanTimelineGantt rows={visibleRows} dependencies={dependencies.data ?? []} selectedId={selectedRowId} zoom={zoom} collapsed={collapsedWbs} showBaseline={showBaseline} onSelect={setSelectedRowId} onToggleCollapse={toggleCollapse} onOpenDetails={(row) => { const activity = row.activity; if (activity) { setSelectedActivity(activity); setActivityOpen(true); } }} /></div>
)}
</CardContent></Card>
<SideSheet open={assignmentFormOpen} onOpenChange={setAssignmentFormOpen} title={selectedAssignment ? t("common.edit") : t("planning.assignResource")} description={t("planning.panelDescriptions.assignments")} size="lg">
  <form onSubmit={(event) => void saveAssignment(event)} className="flex min-h-0 flex-1 flex-col">
    <SideSheetBody className="space-y-6">
      <section className="space-y-4">
        <h3 className="border-b pb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("planning.assignmentDetailsSection")}</h3>
        <div className="space-y-1.5">
          <Label htmlFor="planning-assignment-activity">{t("planning.selectActivity")} <span aria-hidden="true" className="ms-1 text-destructive">*</span></Label>
          <select id="planning-assignment-activity" className="input-field w-full" name="activityId" value={assignmentForm.activityId} onChange={(event) => {
            const activity = (activities.data ?? []).find(candidate => candidate.id === event.target.value);
            setAssignmentForm(current => ({ ...current, activityId: event.target.value, startDate: activity?.startDate?.slice(0, 10) ?? "", endDate: activity?.endDate?.slice(0, 10) ?? "" }));
          }} required>
            <option value="">{t("planning.selectActivity")}</option>
            {(activities.data ?? []).map((activity) => <option key={activity.id} value={activity.id}>{activity.code} · {activity.name}</option>)}
          </select>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="planning-assignment-resource">{t("planning.selectResource")} <span aria-hidden="true" className="ms-1 text-destructive">*</span></Label>
          <select id="planning-assignment-resource" className="input-field w-full" name="resourceId" value={assignmentForm.resourceId} onChange={(event) => setAssignmentForm(current => ({ ...current, resourceId: event.target.value }))} required>
            <option value="">{t("planning.selectResource")}</option>
            {assignmentResources.map((resource) => <option key={resource.id} value={resource.id}>{resource.name}{resource.jobTitle ? ` — ${resource.jobTitle}` : ""}</option>)}
          </select>
        </div>
      </section>
      <section className="space-y-4">
        <h3 className="border-b pb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{t("planning.allocationScheduleSection")}</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="planning-assignment-work-hours">{t("planning.assignedWorkHours")} <span aria-hidden="true" className="ms-1 text-destructive">*</span></Label>
            <Input id="planning-assignment-work-hours" name="assignedWorkHours" type="number" min="0.5" step="0.5" value={assignmentForm.assignedWorkHours} onChange={(event) => setAssignmentForm(current => ({ ...current, assignedWorkHours: event.target.value }))} required />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="planning-assignment-start-date">{t("projects.startDate")} <span aria-hidden="true" className="ms-1 text-destructive">*</span></Label>
            <Input id="planning-assignment-start-date" name="startDate" type="date" value={assignmentForm.startDate} onChange={(event) => setAssignmentForm(current => ({ ...current, startDate: event.target.value }))} required />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="planning-assignment-end-date">{t("projects.endDate")} <span aria-hidden="true" className="ms-1 text-destructive">*</span></Label>
            <Input id="planning-assignment-end-date" name="endDate" type="date" value={assignmentForm.endDate} onChange={(event) => setAssignmentForm(current => ({ ...current, endDate: event.target.value }))} required />
          </div>
        </div>
        {assignmentForm.resourceId && assignmentForm.startDate && assignmentForm.endDate ? <section className="space-y-3 rounded-md border bg-muted/20 p-3" aria-live="polite">
          {assignmentCapacity.isFetching ? <p className="text-xs text-muted-foreground">{t("common.loading")}</p> : assignmentCapacity.isError ? <p className="text-sm text-destructive" role="alert">{t("planning.capacityLoadFailed")}</p> : assignmentCapacity.data ? <>
            <div className="grid grid-cols-2 gap-3">
              <div><p className="text-xs text-muted-foreground">{t("planning.projectAllocation")}</p><p className="mt-1 text-sm font-semibold tabular-nums">{formatNumber(assignmentCapacity.data.projectAllocationPercentage)}%</p></div>
              <div><p className="text-xs text-muted-foreground">{t("planning.projectCapacityForPeriod")}</p><p className="mt-1 text-sm font-semibold tabular-nums">{formatNumber(assignmentCapacity.data.projectCapacityHours)} {t("common.hours")}</p><p className="text-xs text-muted-foreground">{formatNumber(assignmentCapacity.data.projectCapacityHoursPerWeek)} {t("planning.hoursShort")}</p></div>
              <div><p className="text-xs text-muted-foreground">{t("planning.workAssignedInPeriod")}</p><p className="mt-1 text-sm font-semibold tabular-nums">{formatNumber(assignmentCapacity.data.assignedWorkHours)} {t("common.hours")}</p></div>
              <div><p className="text-xs text-muted-foreground">{t("planning.remainingAssignableHours")}</p><p className={`mt-1 text-sm font-semibold tabular-nums ${assignmentCapacity.data.remainingAssignableHours > 0 ? "text-success" : "text-destructive"}`}>{formatNumber(assignmentCapacity.data.remainingAssignableHours)} {t("common.hours")}</p></div>
            </div>
            {assignmentCapacity.data.isScheduleWithinMembership === false ? <div className="space-y-1" role="alert"><p className="text-xs text-warning">{t("planning.assignmentMembershipLimit")}</p><p className="text-xs text-muted-foreground">{t("planning.activeMembershipPeriod")}: {assignmentCapacity.data.membershipStartDate?.slice(0, 10) ?? "—"} – {assignmentCapacity.data.membershipEndDate?.slice(0, 10) ?? "—"}</p></div> : null}
            {assignmentCapacity.data.isScheduleWithinMembership !== false && Number(assignmentForm.assignedWorkHours) > assignmentCapacity.data.remainingAssignableHours + 0.0001 ? <p className="text-xs text-destructive" role="alert">{t("planning.capacityOverLimit")}</p> : null}
          </> : null}
        </section> : null}
      </section>
    </SideSheetBody>
    <SideSheetFooter><Button type="button" variant="outline" onClick={() => setAssignmentFormOpen(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={mutations.createAssignment.isPending || mutations.updateAssignment.isPending || !assignmentCapacity.data || assignmentCapacity.data.isScheduleWithinMembership === false || assignmentCapacity.isFetching || assignmentCapacity.isError || !Number.isFinite(Number(assignmentForm.assignedWorkHours)) || Number(assignmentForm.assignedWorkHours) <= 0 || Number(assignmentForm.assignedWorkHours) > (assignmentCapacity.data?.remainingAssignableHours ?? 0)}>{mutations.createAssignment.isPending || mutations.updateAssignment.isPending ? t("common.saving") : t("common.save")}</Button></SideSheetFooter>
  </form>
</SideSheet>
<SideSheet open={versionsOpen} onOpenChange={setVersionsOpen} title={t("planning.versions")} description={t("planning.versionsDescription")}>
  <SideSheetBody className="space-y-2">{versions.length === 0 ? <p className="text-sm text-muted-foreground">{t("common.noResults")}</p> : versions.map((v) => <div key={v.id} className="flex items-center justify-between gap-2 rounded border px-3 py-2 text-sm"><span className="font-medium">{planningLabel(t, "versionType", v.type)} · {t("planning.versionShort")} {v.versionNumber}</span><Badge status={v.status}>{planningLabel(t, "versionStatus", v.status)}</Badge></div>)}</SideSheetBody>
</SideSheet>
<SideSheet open={baselineOpen} onOpenChange={setBaselineOpen} title={t("planning.viewBaseline")} description={t("planning.baselineSummaryDescription")}><SideSheetBody><BaselinePanel baseline={currentBaseline} activities={activities.data ?? []} t={t} onCompare={() => { setShowBaseline(true); setBaselineOpen(false); setRibbonTab("view"); setView("table"); }} /></SideSheetBody></SideSheet>
<PlanningActivityDetailsSheet open={activityOpen} onOpenChange={(open) => { setActivityOpen(open); if (!open) setSelectedActivity(undefined); }} activity={selectedActivity} projectId={projectId} projectName={project.name} planName={selectedPlan?.name} planId={planId} editable={editable} t={t} />
{importOpen ? <PlanningImportSheet
  projectId={projectId}
  plans={plans.data ?? []}
  selectedPlanId={planId}
  commitMutation={mutations.commitImport}
  t={t}
  onOpenChange={setImportOpen}
  onImported={async (importedPlanId) => {
    setSelectedPlanId(importedPlanId);
    setRibbonTab("view");
    setView("table");
  }}
/> : null}</div>;
}

function BaselinePanel({ baseline, activities, t, onCompare }: { baseline?: { versionNumber: number; createdAt?: string; createdBy?: string }; activities: { baselineStartDate?: string; baselineFinishDate?: string; baselineDuration?: number; finishVarianceDays?: number; type: string }[]; t: (key: string) => string; onCompare: () => void }) {
  if (!baseline) return <div className="rounded-lg border border-dashed p-5 text-sm"><p className="font-medium">{t("planning.noBaseline")}</p><p className="mt-1 text-muted-foreground">{t("planning.noBaselineDescription")}</p></div>;
  const captured = activities.filter((a) => a.baselineStartDate || a.baselineFinishDate || a.baselineDuration !== undefined);
  const late = activities.filter((a) => (a.finishVarianceDays ?? 0) > 0).length;
  const early = activities.filter((a) => (a.finishVarianceDays ?? 0) < 0).length;
  const unchanged = activities.filter((a) => (a.finishVarianceDays ?? 0) === 0).length;
  const dated = captured.flatMap((a) => [a.baselineStartDate, a.baselineFinishDate].filter(Boolean).map((v) => v!.slice(0, 10))).sort();
  const duration = captured.reduce((m, a) => Math.max(m, a.baselineDuration ?? 0), 0);
  return <div className="space-y-4">
    <div className="rounded-lg border bg-muted/20 p-4"><div className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold">{t("planning.baselineCreated")} {t("planning.versionShort")}{baseline.versionNumber}</p><p className="mt-1 text-xs text-muted-foreground">{t("planning.baselineSummaryDescription")}</p></div><Badge status="Approved">{t("planning.readOnly")}</Badge></div><div className="mt-3 grid gap-2 text-xs sm:grid-cols-2"><ReadOnlyMeta label={t("planning.baselineCapturedAt")} value={(baseline.createdAt ?? "").slice(0, 10) || "—"} /><ReadOnlyMeta label={t("planning.baselineCreatedBy")} value={baseline.createdBy || "—"} /><ReadOnlyMeta label={t("planning.baselineActivities")} value={String(captured.length)} /><ReadOnlyMeta label={t("planning.baselineMilestones")} value={String(activities.filter((a) => a.type === "Milestone" && a.baselineFinishDate).length)} /></div></div>
    <div className="grid gap-2 sm:grid-cols-3"><StatCard label={t("planning.baselineStartDate")} value={dated.length ? dated[0] : "—"} /><StatCard label={t("planning.baselineFinishDate")} value={dated.length ? dated[dated.length - 1] : "—"} /><StatCard label={t("planning.baselinePlanDuration")} value={`${duration} ${t("planning.durationDays")}`} /></div>
    <div className="grid gap-2 sm:grid-cols-3"><StatCard label={t("planning.baselineLate")} value={String(late)} /><StatCard label={t("planning.baselineEarly")} value={String(early)} /><StatCard label={t("planning.baselineUnchanged")} value={String(unchanged)} /></div>
    <Button type="button" className="w-full gap-2" onClick={onCompare}><GitCompareArrows className="h-4 w-4" />{t("planning.compareBaseline")}</Button>
  </div>;
}

function ReadOnlyMeta({ label, value }: { label: string; value: string }) { return <div className="rounded border bg-background px-3 py-2"><p className="text-[10px] uppercase tracking-wide text-muted-foreground">{label}</p><p className="mt-0.5 truncate font-medium tabular-nums" title={value}>{value}</p></div>; }
function StatCard({ label, value }: { label: string; value: string }) { return <div className="rounded-lg border bg-background p-3"><p className="text-[10px] uppercase tracking-wide text-muted-foreground">{label}</p><p className="mt-1 text-sm font-semibold tabular-nums">{value}</p></div>; }

function PlanningAssignmentsPanel({ activities, assignments, resources, editable, t, onAdd, onEdit, onDelete }: { activities: PlanActivityDto[]; assignments: PlanAssignmentDto[]; resources: { id: string; name: string; jobTitle?: string }[]; editable: boolean; t: (key: string) => string; onAdd: () => void; onEdit: (item: PlanAssignmentDto) => void; onDelete: (item: PlanAssignmentDto) => void; onCompare?: never }) {
  const activityNames = new Map(activities.map((activity) => [activity.id, `${activity.code} · ${activity.name}`]));
  const resourceNames = new Map(resources.map((resource) => [resource.id, resource.name]));
  return <Card><CardHeader className="flex !flex-row items-center justify-between gap-3"><div><CardTitle className="text-sm">{t("planning.assignmentsTitle")}</CardTitle><p className="mt-1 text-xs text-muted-foreground">{t("planning.assignmentsDescription")}</p></div>{editable ? <Button size="sm" onClick={onAdd}><Plus className="me-1 h-4 w-4" />{t("planning.assignResource")}</Button> : null}</CardHeader><CardContent className="p-0">{assignments.length === 0 ? <p className="p-5 text-center text-sm text-muted-foreground">{t("planning.noAssignments")}</p> : <Table><TableHeader><TableRow><TableHead>{t("planning.selectActivity")}</TableHead><TableHead>{t("planning.selectResource")}</TableHead><TableHead>{t("planning.assignedWorkHours")}</TableHead><TableHead>{t("projects.startDate")}</TableHead><TableHead>{t("projects.endDate")}</TableHead>{editable ? <TableHead>{t("common.actions")}</TableHead> : null}</TableRow></TableHeader><TableBody>{assignments.map((item) => <TableRow key={item.id}><TableCell>{activityNames.get(item.activityId) ?? item.activityId}</TableCell><TableCell>{item.resourceName ?? resourceNames.get(item.resourceId) ?? item.resourceId}</TableCell><TableCell>{item.assignedWorkHours ?? item.plannedHours ?? "—"}</TableCell><TableCell>{item.startDate?.slice(0, 10) ?? "—"}</TableCell><TableCell>{item.endDate?.slice(0, 10) ?? "—"}</TableCell>{editable ? <TableCell><div className="flex gap-1"><Button size="sm" variant="ghost" onClick={() => onEdit(item)}>{t("common.edit")}</Button><Button size="sm" variant="ghost" onClick={() => onDelete(item)}><Trash2 className="h-4 w-4 text-destructive" /></Button></div></TableCell> : null}</TableRow>)}</TableBody></Table>}</CardContent></Card>;
}

