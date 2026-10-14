import * as React from "react";
import { useOutletContext } from "react-router-dom";
import { CalendarDays, ChevronRight, Eye, FileUp, GitCompareArrows, Plus, Save, Trash2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Badge } from "@components/ui/Badge";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { usePlanningActivitiesFull, usePlanningAssignmentsFull, usePlanningCalendarsFull, usePlanningDependenciesFull, usePlanningFullMutations, usePlanningPlansFull, usePlanningPlanFull, usePlanningResourcesFull, usePlanningWbsFull, type PlanActivityDto, type PlanAssignmentDto } from "./api/planningFull";
import { usePlanningTableMutations } from "./api/planning-table-mutations";
import { PlanningActivityDetailsSheet } from "./PlanningActivityDetailsSheet";
import { PlanningCreatePlanPanel } from "./components/PlanningCreatePlanPanel";
import { PlanningBaselineAccessHint } from "./components/PlanningBaselineAccessHint";
import { PlanningImportSheet } from "./components/PlanningImportSheet";
import { PlanningRibbonGroup, PlanningRibbonTabs, WorkspaceButton, type PlanningRibbonTab } from "./components/PlanningRibbon";
import { PlanningGrid } from "./components/PlanningGrid";
import { PlanningCalendarsPanel } from "./components/PlanningCalendarsPanel";
import { PlanTimelineGantt } from "./components/PlanTimelineGantt";
import { buildPlanWorkspaceRows, visiblePlanWorkspaceRows } from "./lib/plan-workspace-rows";
import { canPerformPlanningWrites, getPlanningBaselineUnavailableReason } from "./lib/planning-access";
import { GANTT_ZOOM_LEVELS, type GanttZoom } from "./lib/gantt-constants";

type ViewMode = "table" | "timeline";
function planningLabel(t: (key: string) => string, group: "planType" | "versionType" | "versionStatus" | "activityType" | "dependencyType", value?: string | null) {
  if (!value) return "—";
  const label = t(`enums.planning.${group}.${value}`);
  return label === `enums.planning.${group}.${value}` ? value : label;
}
function Loading({ text }: { text: string }) { return <p className="py-10 text-center text-sm text-muted-foreground">{text}</p>; }

export function ProjectPlanningTabFull() {
  const { projectId } = useOutletContext<{ projectId: string }>();
  const { t: translate } = useI18n();
  const t = React.useCallback((key: string, params?: Record<string, string | number>) => translate(key.startsWith("planning.") ? `projects.${key}` : key, params), [translate]);
  const { user } = useAuth();
  const { effectiveReadOnly, canMutate } = useManagement();
  const [ribbonTab, setRibbonTab] = React.useState<PlanningRibbonTab>("project");
  const [versionsOpen, setVersionsOpen] = React.useState(false);
  const [baselineOpen, setBaselineOpen] = React.useState(false);
  const hasManagePlanningPermission = Boolean(user && hasPermission(user.permissions, Permissions.Planning.Manage));
  const canManagePlanning = canPerformPlanningWrites(hasManagePlanningPermission, canMutate, effectiveReadOnly);
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
  const mutations = usePlanningTableMutations(projectId, planId);
  const [view, setView] = React.useState<ViewMode>("table");
  const [zoom, setZoom] = React.useState<GanttZoom>("day");
  const [showBaseline, setShowBaseline] = React.useState(false);
  const [collapsedWbs, setCollapsedWbs] = React.useState<ReadonlySet<string>>(() => new Set());
  const [selectedRowId, setSelectedRowId] = React.useState("");
  const versions = planDetail.data?.versions ?? [];
  const workingVersion = versions.find((v) => v.type === "Working" && v.status === "Active") ?? versions.find((v) => v.type === "Working");
  const currentBaseline = versions.filter((v) => v.type === "Baseline" && v.status === "Approved").sort((a, b) => b.versionNumber - a.versionNumber)[0];
  const editable = Boolean(workingVersion?.id && canManagePlanning && canMutate && !effectiveReadOnly);
  const [planOpen, setPlanOpen] = React.useState(false);
  const [activityOpen, setActivityOpen] = React.useState(false);
  const [selectedActivity, setSelectedActivity] = React.useState<PlanActivityDto | undefined>();
  const [importOpen, setImportOpen] = React.useState(false);
  const [planningPanel, setPlanningPanel] = React.useState<"assignments" | "calendar" | null>(null);
  const [selectedCalendarId, setSelectedCalendarId] = React.useState("");
  const [assignmentFormOpen, setAssignmentFormOpen] = React.useState(false);
  const [selectedAssignment, setSelectedAssignment] = React.useState<PlanAssignmentDto | null>(null);
  const calendarMutations = usePlanningFullMutations(projectId, planId);
  const assignments = usePlanningAssignmentsFull(projectId, planId, planningPanel === "assignments");
  const resources = usePlanningResourcesFull(planningPanel === "assignments");
  const calendars = usePlanningCalendarsFull(projectId, planId, planningPanel === "calendar");
  const workspaceRows = React.useMemo(() => buildPlanWorkspaceRows(wbs.data ?? [], activities.data ?? []), [wbs.data, activities.data]);
  const visibleRows = React.useMemo(() => visiblePlanWorkspaceRows(workspaceRows, collapsedWbs), [workspaceRows, collapsedWbs]);
  React.useEffect(() => { if (!selectedPlanId && plans.data?.length && !plans.isFetching) setSelectedPlanId(plans.data[0].id); }, [plans.data, plans.isFetching, selectedPlanId]);
  React.useEffect(() => { setSelectedCalendarId(""); }, [planId]);
  if (plans.isLoading) return <Loading text={t("common.loading")} />;
  if (plans.isError) return <p className="py-10 text-center text-sm text-destructive">{t("common.errorLoading")}</p>;
  const selectedPlan = plans.data?.find(plan => plan.id === planId);
  const toggleCollapse = (row: { wbs?: { id: string } }) => {
    if (!row.wbs) return;
    setCollapsedWbs((prev) => { const next = new Set(prev); if (next.has(row.wbs!.id)) next.delete(row.wbs!.id); else next.add(row.wbs!.id); return next; });
  };
  function openCreatePlan() { setRibbonTab("project"); mutations.createPlan.reset(); setPlanOpen(true); }
  function cancelCreatePlan() { mutations.createPlan.reset(); setPlanOpen(false); }
  function handlePlanCreated(createdPlanId: string) { setSelectedPlanId(createdPlanId); setPlanOpen(false); }
  async function captureBaseline() { const versions = planDetail.data?.versions ?? []; const hasBaseline = versions.some((item) => item.type === "Baseline"); if (hasBaseline && !window.confirm(t("planning.replaceBaselineWarning"))) return; const working = versions.find((item) => item.type === "Working" && item.status === "Active") ?? versions.find((item) => item.type === "Working"); if (working) await mutations.captureBaseline.mutateAsync(working.rowVersion); }
  async function saveAssignment(event: React.FormEvent<HTMLFormElement>) { event.preventDefault(); if (!planId) return; const form = new FormData(event.currentTarget); const body = { activityId: String(form.get("activityId") ?? ""), resourceId: String(form.get("resourceId") ?? ""), allocationPct: Number(form.get("allocationPct") ?? 0) || undefined, hoursPerWeek: Number(form.get("hoursPerWeek") ?? 0) || undefined, startDate: String(form.get("startDate") ?? "") || undefined, endDate: String(form.get("endDate") ?? "") || undefined }; if (!body.activityId || !body.resourceId) return; if (selectedAssignment) await mutations.updateAssignment.mutateAsync({ id: selectedAssignment.id, body: { ...body, rowVersion: selectedAssignment.rowVersion } }); else await mutations.createAssignment.mutateAsync(body); setAssignmentFormOpen(false); setSelectedAssignment(null); }
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
<div className="overflow-x-auto border-b px-2 pt-1"><PlanningRibbonTabs ariaLabel={t("planning.ribbonTabs.label")} activeTab={ribbonTab} tabs={[{ id: "project", label: t("planning.ribbonTabs.project") }, { id: "task", label: t("planning.ribbonTabs.task") }, { id: "view", label: t("planning.ribbonTabs.view") }, { id: "resource", label: t("planning.ribbonTabs.resource") }, { id: "format", label: t("planning.ribbonTabs.format") }]} onChange={(tab) => { setRibbonTab(tab); if (tab === "task" || tab === "resource" || tab === "format") setView("table"); }} /></div>
{(ribbonTab === "project" || ribbonTab === "view") && <div className="min-h-[4.25rem] overflow-x-auto px-2 py-1.5">
{ribbonTab === "project" && <div className="flex min-w-max items-stretch gap-2">
<PlanningRibbonGroup label={t("planning.ribbon.plan")} className="min-w-[19rem]">
<div className="w-48 max-w-[13rem] shrink-0"><Select value={planId} onValueChange={setSelectedPlanId}>
<SelectTrigger aria-label={t("planning.selectPlan")} className="h-7 w-48 max-w-[13rem] shrink-0 text-[11px]"><SelectValue placeholder={t("planning.selectPlan")} /></SelectTrigger>
<SelectContent>{(plans.data ?? []).map((plan) => <SelectItem key={plan.id} value={plan.id}>{plan.name} · {planningLabel(t, "planType", plan.planType)}</SelectItem>)}</SelectContent>
</Select></div>
{canManagePlanning ? <Button type="button" variant="default" size="sm" className="h-7 shrink-0 gap-1 px-2 text-[11px]" onClick={planOpen ? cancelCreatePlan : openCreatePlan}><Plus className="h-3.5 w-3.5" />{t(planOpen ? "planning.cancelCreatePlan" : "planning.createPlan")}</Button> : null}
</PlanningRibbonGroup>
<PlanningRibbonGroup label={t("planning.ribbon.tools")}>
{canManagePlanning ? <WorkspaceButton onClick={() => setImportOpen(true)} icon={<FileUp className="h-3.5 w-3.5" />}>{t("planning.import.action")}</WorkspaceButton> : null}
{canBaseline ? <WorkspaceButton onClick={() => void captureBaseline()} disabled={!workingVersion?.rowVersion} icon={<Save className="h-3.5 w-3.5" />}>{t("planning.setBaseline")}</WorkspaceButton> : null}
{planId ? <PlanningBaselineAccessHint reason={baselineUnavailableReason} t={t} /> : null}
{currentBaseline ? <WorkspaceButton onClick={() => setBaselineOpen(true)} icon={<Eye className="h-3.5 w-3.5" />}>{t("planning.viewBaseline")}</WorkspaceButton> : null}
<WorkspaceButton onClick={() => setPlanningPanel("calendar")} icon={<CalendarDays className="h-3.5 w-3.5" />}>{t("planning.calendar")}</WorkspaceButton>
<WorkspaceButton onClick={() => setVersionsOpen(true)} icon={<ChevronRight className="h-3.5 w-3.5" />}>{t("planning.versions")}</WorkspaceButton>
</PlanningRibbonGroup>
</div>}
{ribbonTab === "view" && <div className="flex min-w-max items-stretch gap-2">
<PlanningRibbonGroup label={t("planning.ribbon.view")} className="min-w-[15rem]">
<div className="flex items-center rounded-md border bg-background p-0.5">
{(["table", "gantt"] as const).map((option) => { const active = view === (option === "gantt" ? "timeline" : "table"); return <Button key={option} type="button" size="sm" variant={active ? "secondary" : "ghost"} aria-pressed={active} onClick={() => setView(option === "gantt" ? "timeline" : "table")} className={`h-7 px-2 text-[11px] ${active ? "font-medium" : "font-normal text-muted-foreground"}`}>{t(`planning.views.${option}`)}</Button>;})}
</div>
{view === "timeline" && <div className="flex items-center gap-0.5 rounded-md border bg-background p-0.5">
{GANTT_ZOOM_LEVELS.map((level) => <Button key={level} type="button" size="sm" variant={zoom === level ? "secondary" : "ghost"} aria-pressed={zoom === level} onClick={() => setZoom(level)} className="h-7 px-1.5 text-[10px]">{t(`common.${level}`)}</Button>)}
</div>}
{view === "timeline" && currentBaseline ? <Button type="button" size="sm" variant={showBaseline ? "secondary" : "ghost"} aria-pressed={showBaseline} onClick={() => setShowBaseline((v) => !v)} className="h-7 gap-1 px-2 text-[11px]"><GitCompareArrows className="h-3.5 w-3.5" />{t(showBaseline ? "planning.hideBaseline" : "planning.showBaseline")}</Button> : null}
</PlanningRibbonGroup>
</div>}
</div>}
{planOpen ? <div className="border-t p-3"><PlanningCreatePlanPanel mutation={mutations.createPlan} t={t} onCreated={handlePlanCreated} onCancel={cancelCreatePlan} /></div> : null}
</div>
{planningPanel ? (
  <SideSheet open onOpenChange={(open) => { if (!open) setPlanningPanel(null); }} title={t(`planning.panelTitles.${planningPanel}`)} description={t(`planning.panelDescriptions.${planningPanel}`)} closeLabel={t("common.close")}>
    <SideSheetBody className="space-y-4">
      {planningPanel === "assignments" ? (
        <PlanningAssignmentsPanel activities={activities.data ?? []} assignments={assignments.data ?? []} resources={resources.data ?? []} editable={editable} t={t} onAdd={() => { setSelectedAssignment(null); setAssignmentFormOpen(true); }} onEdit={(item) => { setSelectedAssignment(item); setAssignmentFormOpen(true); }} onDelete={(item) => void mutations.deleteAssignment.mutateAsync({ id: item.id, rowVersion: item.rowVersion })} />
      ) : (
        <PlanningCalendarsPanel calendars={calendars.data ?? []} isLoading={calendars.isLoading} isFetching={calendars.isFetching} isError={calendars.isError} onRetry={() => void calendars.refetch()} editable={editable} selectedCalendarId={selectedCalendarId} onSelectCalendar={setSelectedCalendarId} mutations={calendarMutations} onCalendarCreated={(calendar) => setSelectedCalendarId(calendar.id)} t={t} />
      )}
    </SideSheetBody>
  </SideSheet>
) : null}
{!planId ? <p className="m-4 rounded border border-dashed p-8 text-center text-sm text-muted-foreground">{t("planning.emptyWorkspace")}</p> : wbs.isLoading || activities.isLoading ? <div className="p-4"><Loading text={t("common.loading")} /></div> : view === "table" ? <PlanningGrid projectId={projectId} planId={planId} wbs={wbs.data ?? []} activities={activities.data ?? []} dependencies={dependencies.data ?? []} mutations={mutations} editable={editable} showBaseline={showBaseline} ribbonTab={ribbonTab} t={t} onOpenAssignments={() => setPlanningPanel("assignments")} onEdit={(activity) => { setSelectedActivity(activity); setActivityOpen(true); }} onDelete={(activity) => void mutations.deleteActivity.mutateAsync({ id: activity.id, rowVersion: activity.rowVersion })} /> : <div className="m-4 overflow-x-auto rounded-lg border"><PlanTimelineGantt rows={visibleRows} dependencies={dependencies.data ?? []} selectedId={selectedRowId} zoom={zoom} collapsed={collapsedWbs} showBaseline={showBaseline} onSelect={setSelectedRowId} onToggleCollapse={toggleCollapse} onOpenDetails={(row) => { const activity = row.activity; if (activity) { setSelectedActivity(activity); setActivityOpen(true); } }} /></div>}
</CardContent></Card>
<SideSheet open={assignmentFormOpen} onOpenChange={setAssignmentFormOpen} title={selectedAssignment ? t("common.edit") : t("planning.assignResource")}>
  <form onSubmit={(event) => void saveAssignment(event)} className="flex min-h-0 flex-1 flex-col">
    <SideSheetBody className="space-y-3">
      <Label>{t("planning.selectActivity")}</Label><select className="input-field" name="activityId" defaultValue={selectedAssignment?.activityId ?? ""} required><option value="">{t("planning.selectActivity")}</option>{(activities.data ?? []).map(activity => <option key={activity.id} value={activity.id}>{activity.code} · {activity.name}</option>)}</select>
      <Label>{t("planning.selectResource")}</Label><select className="input-field" name="resourceId" defaultValue={selectedAssignment?.resourceId ?? ""} required><option value="">{t("planning.selectResource")}</option>{(resources.data ?? []).map(resource => <option key={resource.id} value={resource.id}>{resource.name}{resource.jobTitle ? ` — ${resource.jobTitle}` : ""}</option>)}</select>
      <Label>{t("planning.allocation")}</Label><Input name="allocationPct" type="number" min="0" max="100" step="0.1" defaultValue={selectedAssignment?.allocationPct ?? ""} />
      <Label>{t("planning.hoursPerWeek")}</Label><Input name="hoursPerWeek" type="number" min="0" step="0.5" defaultValue={selectedAssignment?.hoursPerWeek ?? ""} />
      <div className="grid grid-cols-2 gap-2"><Input name="startDate" type="date" defaultValue={selectedAssignment?.startDate?.slice(0, 10) ?? ""} /><Input name="endDate" type="date" defaultValue={selectedAssignment?.endDate?.slice(0, 10) ?? ""} /></div>
    </SideSheetBody>
    <SideSheetFooter><Button type="button" variant="outline" onClick={() => setAssignmentFormOpen(false)}>{t("common.cancel")}</Button><Button type="submit">{t("common.save")}</Button></SideSheetFooter>
  </form>
</SideSheet>
<SideSheet open={versionsOpen} onOpenChange={setVersionsOpen} title={t("planning.versions")} description={t("planning.versionsDescription")}>
  <SideSheetBody className="space-y-2">{versions.length === 0 ? <p className="text-sm text-muted-foreground">{t("common.noResults")}</p> : versions.map((v) => <div key={v.id} className="flex items-center justify-between gap-2 rounded border px-3 py-2 text-sm"><span className="font-medium">{planningLabel(t, "versionType", v.type)} · {t("planning.versionShort")} {v.versionNumber}</span><Badge status={v.status}>{planningLabel(t, "versionStatus", v.status)}</Badge></div>)}</SideSheetBody>
</SideSheet>
<SideSheet open={baselineOpen} onOpenChange={setBaselineOpen} title={t("planning.viewBaseline")} description={t("planning.baselineSummaryDescription")}><SideSheetBody><BaselinePanel baseline={currentBaseline} activities={activities.data ?? []} t={t} onCompare={() => { setShowBaseline(true); setBaselineOpen(false); setRibbonTab("view"); setView("table"); }} /></SideSheetBody></SideSheet>
<PlanningActivityDetailsSheet open={activityOpen} onOpenChange={(open) => { setActivityOpen(open); if (!open) setSelectedActivity(undefined); }} activity={selectedActivity} projectId={projectId} planId={planId} editable={editable} t={t} />
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
  return <Card><CardHeader className="flex !flex-row items-center justify-between gap-3"><div><CardTitle className="text-sm">{t("planning.assignmentsTitle")}</CardTitle><p className="mt-1 text-xs text-muted-foreground">{t("planning.assignmentsDescription")}</p></div>{editable ? <Button size="sm" onClick={onAdd}><Plus className="me-1 h-4 w-4" />{t("planning.assignResource")}</Button> : null}</CardHeader><CardContent className="p-0">{assignments.length === 0 ? <p className="p-5 text-center text-sm text-muted-foreground">{t("planning.noAssignments")}</p> : <Table><TableHeader><TableRow><TableHead>{t("planning.selectActivity")}</TableHead><TableHead>{t("planning.selectResource")}</TableHead><TableHead>{t("planning.allocation")}</TableHead><TableHead>{t("planning.hoursPerWeek")}</TableHead>{editable ? <TableHead>{t("common.actions")}</TableHead> : null}</TableRow></TableHeader><TableBody>{assignments.map((item) => <TableRow key={item.id}><TableCell>{activityNames.get(item.activityId) ?? item.activityId}</TableCell><TableCell>{item.resourceName ?? resourceNames.get(item.resourceId) ?? item.resourceId}</TableCell><TableCell>{item.allocationPct ?? "—"}%</TableCell><TableCell>{item.hoursPerWeek ?? "—"}</TableCell>{editable ? <TableCell><div className="flex gap-1"><Button size="sm" variant="ghost" onClick={() => onEdit(item)}>{t("common.edit")}</Button><Button size="sm" variant="ghost" onClick={() => onDelete(item)}><Trash2 className="h-4 w-4 text-destructive" /></Button></div></TableCell> : null}</TableRow>)}</TableBody></Table>}</CardContent></Card>;
}

