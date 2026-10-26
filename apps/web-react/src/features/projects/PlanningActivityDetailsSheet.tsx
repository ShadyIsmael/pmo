import * as React from "react";
import { Save } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { usePlanningCalendarsFull, usePlanningFullMutations, usePlanningSchedulingExplanationFull, usePlanningActivitiesFull, usePlanningAssignmentsFull, usePlanningDependenciesFull, usePlanningResourcesFull, usePlanningWbsFull, type PlanActivityDto, type PlanDependencyDto, type PlanConstraintType, type PlanSchedulingExplanationDto, type PlanSchedulingMode } from "./api/planningFull";
import { formatPredecessor, parsePredecessor } from "./lib/predecessor-syntax";
import { roundPlanProgress } from "./lib/plan-progress";
import { buildPlanWorkspaceRows, type PlanWorkspaceRow } from "./lib/plan-workspace-rows";

const DETAIL_TABS = ["general", "schedule", "dependencies", "resources", "analysis", "formatting"] as const;
type DetailTab = typeof DETAIL_TABS[number];
type Props = { open: boolean; onOpenChange: (open: boolean) => void; activity?: PlanActivityDto; row?: PlanWorkspaceRow; rows?: PlanWorkspaceRow[]; activities?: PlanActivityDto[]; dependencies?: PlanDependencyDto[]; predecessorText?: string; projectId: string; planId: string; editable: boolean; t: (key: string) => string };
type Draft = { name: string; description: string; duration: number; progress: number | null; startDate: string; endDate: string; actualStart: string; actualFinish: string; estimateHours: string; schedulingMode: PlanSchedulingMode; constraintType: PlanConstraintType; constraintDate: string; deadline: string; calendarId: string };
const constraintTypes: PlanConstraintType[] = ["AsSoonAsPossible", "AsLateAsPossible", "MustStartOn", "MustFinishOn", "StartNoEarlierThan", "StartNoLaterThan", "FinishNoEarlierThan", "FinishNoLaterThan"];
const reasonCodes = new Set(["ManualDatesPreserved", "MovedByPredecessor", "ConstraintApplied", "SkippedNonWorkingTime", "TaskCalendarOverride", "DeadlineExceeded", "Critical", "HasPredecessors", "HasSuccessors", "SchedulingConflict", "DependencyCycle"]);
const EMPTY_ACTIVITIES: PlanActivityDto[] = [];
const EMPTY_DEPENDENCIES: PlanDependencyDto[] = [];
function dateValue(value?: string) { return value?.slice(0, 10) ?? ""; }
function varianceValue(value?: number | null) { return value == null ? "—" : `${value > 0 ? "+" : ""}${value} ${Math.abs(value) === 1 ? "day" : "days"}`; }
function schedulingReasonLabel(reason: PlanSchedulingExplanationDto["reasons"][number], activityNameById: Map<string, string>, t: (key: string) => string) {
  const label = reasonCodes.has(reason.code) ? t(`projects.planning.schedulingReasons.${reason.code}`) : reason.code;
  const related = reason.relatedActivityId ? activityNameById.get(reason.relatedActivityId) ?? reason.relatedActivityId : undefined;
  if (reason.code === "MovedByPredecessor" && related) return `${label} · ${related}`;
  if (reason.code === "DeadlineExceeded" && reason.workingDays) return `${label} · ${reason.workingDays}d`;
  return label;
}
function formatLag(value?: number) { return value === undefined || value === 0 ? "0d" : `${value > 0 ? "+" : ""}${value}d`; }
function emptyDraft(): Draft { return { name: "", description: "", duration: 0, progress: null, startDate: "", endDate: "", actualStart: "", actualFinish: "", estimateHours: "", schedulingMode: "Automatic", constraintType: "AsSoonAsPossible", constraintDate: "", deadline: "", calendarId: "" }; }
function draftFromActivity(activity?: PlanActivityDto): Draft { if (!activity) return emptyDraft(); return { name: activity.name, description: activity.description ?? "", duration: activity.duration, progress: roundPlanProgress(activity.progress), startDate: dateValue(activity.startDate), endDate: dateValue(activity.endDate), actualStart: dateValue(activity.actualStart), actualFinish: dateValue(activity.actualFinish), estimateHours: activity.estimateHours == null ? "" : String(activity.estimateHours), schedulingMode: activity.schedulingMode ?? "Automatic", constraintType: activity.constraintType ?? "AsSoonAsPossible", constraintDate: dateValue(activity.constraintDate), deadline: dateValue(activity.deadline), calendarId: activity.calendarId ?? "" }; }

export function PlanningActivityDetailsSheet({ open, onOpenChange, activity: selectedActivity, row: suppliedRow, rows: suppliedRows, activities: suppliedActivities, dependencies: suppliedDependencies, predecessorText: suppliedPredecessorText, projectId, planId, editable, t }: Props) {
  const [tab, setTab] = React.useState<DetailTab>("general");
  const tabButtons = React.useRef<(HTMLButtonElement | null)[]>([]);
  const [draft, setDraft] = React.useState<Draft>(emptyDraft());
  const mutations = usePlanningFullMutations(projectId, planId);
  const calendars = usePlanningCalendarsFull(projectId, planId, open && tab === "schedule");
  const activitiesQuery = usePlanningActivitiesFull(projectId, planId);
  const assignmentsQuery = usePlanningAssignmentsFull(projectId, planId, open && tab === "resources");
  const dependenciesQuery = usePlanningDependenciesFull(projectId, planId);
  const resourcesQuery = usePlanningResourcesFull(open && tab === "resources");
  const wbsQuery = usePlanningWbsFull(projectId, planId);
  const activities = activitiesQuery.data ?? suppliedActivities ?? EMPTY_ACTIVITIES;
  const dependencies = dependenciesQuery.data ?? suppliedDependencies ?? EMPTY_DEPENDENCIES;
  const activity = activities.find((candidate) => candidate.id === selectedActivity?.id) ?? selectedActivity;
  const explanation = usePlanningSchedulingExplanationFull(projectId, planId, activity?.id, open && tab === "schedule" && Boolean(activity));
  const rows = React.useMemo(() => suppliedRows ?? buildPlanWorkspaceRows(wbsQuery.data ?? [], activities), [activities, suppliedRows, wbsQuery.data]);
  const row = suppliedRow ?? rows.find((candidate) => candidate.activity?.id === activity?.id);
  const predecessorRowNumber = new Map(rows.flatMap((item) => item.activity ? [[item.activity.id, item.rowNumber] as const] : []));
  const predecessorText = suppliedPredecessorText ?? dependencies.filter((dependency) => dependency.activityId === activity?.id).map((dependency) => {
    const rowNumber = predecessorRowNumber.get(dependency.predecessorActivityId);
    return rowNumber === undefined ? "" : formatPredecessor(rowNumber, dependency.type, dependency.lag);
  }).filter(Boolean).join(", ");
  const [predecessors, setPredecessors] = React.useState("");
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const initializedActivity = React.useRef<string | null>(null);
  const initializationData = React.useRef({ activity, predecessorText });
  initializationData.current = { activity, predecessorText };
  const activityDeps = React.useMemo(() => dependencies.filter((d) => d.activityId === activity?.id), [dependencies, activity?.id]);
  const successorDeps = React.useMemo(() => dependencies.filter((d) => d.predecessorActivityId === activity?.id), [dependencies, activity?.id]);
  const activityNameById = React.useMemo(() => new Map(activities.map((a) => [a.id, `${a.code} · ${a.name}`])), [activities]);
  const rowNumberByActivity = React.useMemo(() => new Map(rows.flatMap((item) => item.activity ? [[item.activity.id, item.rowNumber] as const] : [])), [rows]);
  const activityByRowNumber = React.useMemo(() => new Map(rows.flatMap((item) => item.activity ? [[item.rowNumber, item.activity] as const] : [])), [rows]);
  const activityAssignments = React.useMemo(() => (assignmentsQuery.data ?? []).filter((a) => a.activityId === activity?.id), [assignmentsQuery.data, activity?.id]);
  const resourceNames = React.useMemo(() => new Map((resourcesQuery.data ?? []).map((resource) => [resource.id, resource.name])), [resourcesQuery.data]);
  const calendarNames = React.useMemo(() => new Map((calendars.data ?? []).map((calendar) => [calendar.id, calendar.name])), [calendars.data]);
  React.useEffect(() => {
    if (!open) { initializedActivity.current = null; return; }
    const current = initializationData.current;
    if (!current.activity || initializedActivity.current === current.activity.id) return;
    initializedActivity.current = current.activity.id;
    setDraft(draftFromActivity(current.activity));
    setTab("general");
    setPredecessors(current.predecessorText);
    setError(null);
  }, [activity?.id, open]);
  const set = <K extends keyof Draft>(key: K, value: Draft[K]) => setDraft((current) => ({ ...current, [key]: value }));
  async function savePredecessors(text: string) {
    if (!activity) return;
    const current = dependencies.filter((dependency) => dependency.activityId === activity.id);
    const currentKeys = new Set(current.map((dependency) => {
      const rowNumber = rowNumberByActivity.get(dependency.predecessorActivityId);
      return rowNumber === undefined ? null : formatPredecessor(rowNumber, dependency.type, dependency.lag);
    }));
    const parts = text.split(",").map((part) => part.trim()).filter(Boolean);
    const parsed = parts.map(parsePredecessor);
    if (parsed.some((part) => !part)) throw new Error(t("projects.planning.predecessorSyntaxError"));
    const desired = new Set<string>();
    const toCreate: { predecessorActivityId: string; type: PlanDependencyDto["type"]; lag: number }[] = [];
    for (const part of parsed as NonNullable<(typeof parsed)[number]>[]) {
      const predecessor = activityByRowNumber.get(part.rowNumber);
      if (!predecessor || predecessor.id === activity.id) throw new Error(t("projects.planning.predecessorSyntaxError"));
      const key = formatPredecessor(part.rowNumber, part.type, part.lag);
      if (desired.has(key)) continue;
      desired.add(key);
      if (!currentKeys.has(key)) toCreate.push({ predecessorActivityId: predecessor.id, type: part.type, lag: part.lag ?? 0 });
    }
    for (const dependency of current) {
      const rowNumber = rowNumberByActivity.get(dependency.predecessorActivityId);
      const key = rowNumber === undefined ? null : formatPredecessor(rowNumber, dependency.type, dependency.lag);
      if (!key || !desired.has(key)) await mutations.deleteDependency.mutateAsync({ id: dependency.id, rowVersion: dependency.rowVersion });
    }
    for (const dependency of toCreate) await mutations.createDependency.mutateAsync({ activityId: activity.id, ...dependency });
  }
  function DependenciesTab() {
    if (!activity) return <p className="text-sm text-muted-foreground">{t("projects.planning.saveFirst")}</p>;
    return <div className="space-y-3">
      <section className="rounded-lg border p-3">
        <h3 className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.planning.detailSections.dependencies")}</h3>
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="space-y-1.5 sm:col-span-2"><Label htmlFor="planning-predecessors">{t("projects.planning.predecessors")}</Label><Input id="planning-predecessors" value={predecessors} onChange={(e) => setPredecessors(e.target.value)} placeholder="3FS+2d" disabled={!editable || saving} aria-describedby="planning-predecessor-help" /><p id="planning-predecessor-help" className="text-xs text-muted-foreground">{t("projects.planning.predecessorPlaceholder")}</p></div>
          <Metric label={t("projects.planning.predecessorCount")} value={String(activityDeps.length)} />
          <Metric label={t("projects.planning.successorCount")} value={String(successorDeps.length)} />
        </div>
      </section>
      <div className="space-y-2">{activityDeps.map((d) => <div key={d.id} className="rounded border px-3 py-2 text-sm"><span>{activityNameById.get(d.predecessorActivityId) ?? d.predecessorActivityId} · {t(`projects.planning.dependencyTypes.${d.type}`)} · {formatLag(d.lag)}</span></div>)}{activityDeps.length === 0 ? <p className="text-sm text-muted-foreground">{t("projects.planning.noPredecessors")}</p> : null}</div>
      <div className="space-y-2">{successorDeps.map((d) => <div key={d.id} className="rounded border px-3 py-2 text-sm">{activityNameById.get(d.activityId) ?? d.activityId} · {t(`projects.planning.dependencyTypes.${d.type}`)} · {formatLag(d.lag)}</div>)}{successorDeps.length === 0 ? <p className="text-sm text-muted-foreground">{t("projects.planning.noSuccessors")}</p> : null}</div>
    </div>;
  }
  function ResourcesTab() {
    if (!activity) return <p className="text-sm text-muted-foreground">{t("projects.planning.saveFirst")}</p>;
    return <div className="space-y-3">
      <section className="rounded-lg border p-3"><h3 className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.planning.detailSections.resources")}</h3><div className="grid gap-3 sm:grid-cols-2"><div className="space-y-1.5"><Label htmlFor="planning-estimate-hours">{t("projects.planning.estimateHours")}</Label><Input id="planning-estimate-hours" type="number" min="0" step="0.5" disabled={!editable || saving} value={draft.estimateHours} onChange={(event) => set("estimateHours", event.target.value)} /></div><Metric label={t("projects.planning.assignmentCount")} value={String(activityAssignments.length)} /></div></section>
      {assignmentsQuery.isLoading ? <p className="text-sm text-muted-foreground">{t("projects.planning.loadingDetails")}</p> : <div className="space-y-2">{activityAssignments.map((assignment) => <div key={assignment.id} className="rounded border px-3 py-2 text-sm"><div className="flex items-center justify-between gap-2"><span className="truncate font-medium">{resourceNames.get(assignment.resourceId) ?? assignment.resourceName ?? assignment.resourceId}</span><span className="font-semibold tabular-nums">{t("projects.planning.assignedWorkHours")}: {assignment.assignedWorkHours ?? assignment.plannedHours ?? "—"}</span></div><div className="mt-2 grid gap-1 text-xs text-muted-foreground sm:grid-cols-2"><span>{t("projects.planning.assignmentStart")}: {dateValue(assignment.startDate) || "—"}</span><span>{t("projects.planning.assignmentFinish")}: {dateValue(assignment.endDate) || "—"}</span></div></div>)}{activityAssignments.length === 0 ? <p className="text-sm text-muted-foreground">{t("projects.planning.noResourceAssignments")}</p> : null}</div>}
      <p className="text-xs text-muted-foreground">{t("projects.planning.resourceAssignments")}</p>
    </div>;
  }
  function onTabListKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
    const currentIndex = DETAIL_TABS.indexOf(tab);
    const rtl = document.documentElement.dir === "rtl";
    const direction = rtl ? -1 : 1;
    const nextIndex = event.key === "Home" ? 0
      : event.key === "End" ? DETAIL_TABS.length - 1
        : (currentIndex + (event.key === "ArrowRight" ? direction : -direction) + DETAIL_TABS.length) % DETAIL_TABS.length;
    event.preventDefault();
    setTab(DETAIL_TABS[nextIndex]!);
    tabButtons.current[nextIndex]?.focus();
  }
  async function save(event: React.FormEvent) {
    event.preventDefault();
    if (!activity || !draft || !editable || saving) return;
    if (draft.actualFinish && !draft.actualStart) { setError(t("projects.planning.actualFinishRequiresStart")); return; }
    if (!new Set(["AsSoonAsPossible", "AsLateAsPossible"]).has(draft.constraintType) && !draft.constraintDate) { setError(t("projects.planning.constraintDateRequired")); return; }
    if ((draft.progress != null && (draft.progress < 0 || draft.progress > 100)) || draft.duration < 0 || Number(draft.estimateHours) < 0) { setError(t("projects.planning.saveFailed")); return; }
    setSaving(true);
    setError(null);
    try {
      await mutations.updateActivity.mutateAsync({ id: activity.id, body: {
        code: activity.code,
        name: draft.name.trim(),
        description: draft.description.trim() || null,
        type: activity.type,
        schedulingMode: draft.schedulingMode,
        constraintType: draft.constraintType,
        constraintDate: draft.constraintDate || null,
        deadline: draft.deadline || null,
        startDate: draft.startDate || null,
        endDate: draft.endDate || null,
        actualStart: draft.actualStart || null,
        actualFinish: draft.actualFinish || null,
        calendarId: draft.calendarId || null,
        duration: activity.type === "Milestone" ? 0 : draft.duration,
        estimateHours: draft.estimateHours === "" ? null : Number(draft.estimateHours),
        progress: roundPlanProgress(activity.isSummaryTask ? activity.progress : draft.progress),
        wbsId: activity.wbsId ?? null,
        sortOrder: activity.sortOrder,
        textFormat: activity.textFormat ?? null,
        rowVersion: activity.rowVersion,
      } });
      if (predecessors.trim() !== predecessorText.trim()) await savePredecessors(predecessors);
      onOpenChange(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : t("projects.planning.saveFailed"));
    } finally {
      setSaving(false);
    }
  }
  const explanationData = explanation.data;
  const format = (activity?.textFormat ?? null) as { isBold?: boolean; isItalic?: boolean; isUnderline?: boolean; textColorToken?: string; highlightColorToken?: string; fontSizeToken?: string } | null;
  function nextFormat(patch: Record<string, unknown>) {
    return { isBold: format?.isBold ?? false, isItalic: format?.isItalic ?? false, isUnderline: format?.isUnderline ?? false, ...format, ...patch };
  }
  async function saveFormat(next: Record<string, unknown> | null) {
    if (!activity || !editable || saving) return;
    setSaving(true);
    setError(null);
    try {
      await mutations.updateActivity.mutateAsync({ id: activity.id, body: { code: activity.code, name: activity.name, description: activity.description ?? null, type: activity.type, schedulingMode: activity.schedulingMode, constraintType: activity.constraintType, constraintDate: activity.constraintDate ?? null, deadline: activity.deadline ?? null, startDate: activity.startDate ?? null, endDate: activity.endDate ?? null, actualStart: activity.actualStart ?? null, actualFinish: activity.actualFinish ?? null, calendarId: activity.calendarId ?? null, duration: activity.duration, estimateHours: activity.estimateHours ?? null, progress: roundPlanProgress(activity.progress), wbsId: activity.wbsId ?? null, sortOrder: activity.sortOrder, textFormat: next, rowVersion: activity.rowVersion } });
    } catch (e) {
      setError(e instanceof Error ? e.message : t("projects.planning.saveFailed"));
    } finally {
      setSaving(false);
    }
  }
  if (!activity || !row) return null;
  return <SideSheet open={open} onOpenChange={onOpenChange} ariaLabel={t("projects.planning.taskDetails")} title={<span className="flex items-center gap-2">{t("projects.planning.taskDetails")}<Badge status={editable ? "Active" : "ReadOnly"}>{editable ? t("projects.planning.editable") : t("projects.planning.readOnly")}</Badge></span>} description={t("projects.planning.taskDetailsDescription")} className="sm:max-w-2xl lg:max-w-2xl"><form onSubmit={(event) => void save(event)} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-4"><div role="tablist" aria-label={t("projects.planning.taskDetails")} onKeyDown={onTabListKeyDown} className="sticky top-0 z-10 -mx-5 border-y bg-background/95 px-5 py-2 backdrop-blur supports-[backdrop-filter]:bg-background/80"><div className="flex h-9 w-full justify-start gap-0.5 overflow-x-auto rounded-md bg-muted/70 p-0.5">{DETAIL_TABS.map((value, index) => <Button key={value} ref={(element) => { tabButtons.current[index] = element; }} id={`planning-task-tab-${value}`} type="button" role="tab" aria-selected={tab === value} aria-controls="planning-task-details-panel" tabIndex={tab === value ? 0 : -1} size="sm" variant={tab === value ? "default" : "ghost"} className="shrink-0 px-2 py-1 text-[11px]" onClick={() => setTab(value)}>{t(`projects.planning.detailTabs.${value}`)}</Button>)}</div></div>{error ? <p role="alert" className="rounded border border-destructive/30 px-3 py-2 text-xs text-destructive">{error}</p> : null}
    <div role="tabpanel" id="planning-task-details-panel" aria-labelledby={`planning-task-tab-${tab}`} tabIndex={0}>
    {tab === "general" ? <div className="space-y-3">
      <section className="rounded-lg border p-3">
        <h3 className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.planning.detailSections.identity")}</h3>
        <div className="grid gap-3 sm:grid-cols-2">
          <Metric label={t("projects.planning.activityType")} value={activity.type === "Milestone" ? t("projects.planning.milestone") : t("projects.planning.task")} />
          <Metric label={t("projects.planning.code")} value={activity.code} />
          <div className="space-y-1.5 sm:col-span-2"><Label>{t("projects.planning.taskName")}</Label><Input required disabled={!editable || saving} value={draft.name} onChange={(event) => set("name", event.target.value)} /></div>
          <div className="space-y-1.5 sm:col-span-2"><Label>{t("projects.planning.description")}</Label><Textarea disabled={!editable || saving} value={draft.description} onChange={(event) => set("description", event.target.value)} rows={4} /></div>
        </div>
      </section>
      <section className="rounded-lg border p-3">
        <h3 className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.planning.detailSections.context")}</h3>
        <div className="grid gap-3 sm:grid-cols-2">
          <Metric label={t("projects.planning.wbsContext")} value={`${row.wbs?.code ?? t("projects.planning.none")} · ${row.wbs?.name ?? t("projects.planning.none")}`} />
          <Metric label={t("projects.planning.wbsId")} value={row.wbs?.id ?? t("projects.planning.none")} />
          <Metric label={t("projects.planning.parentWbsId")} value={row.wbs?.parentWbsId ?? t("projects.planning.none")} />
          <Metric label={t("projects.planning.deliverableId")} value={row.wbs?.deliverableId ?? t("projects.planning.none")} />
          <Metric label={t("projects.planning.hierarchyLevel")} value={String(row.depth + 1)} />
          <Metric label={t("projects.planning.rowNumber")} value={String(row.rowNumber)} />
          <Metric label={t("projects.planning.sortOrder")} value={String(activity.sortOrder ?? "—")} />
          <Metric label={t("projects.planning.planVersionId")} value={activity.planVersionId} />
        </div>
      </section>
      <section className="rounded-lg border p-3">
        <h3 className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.planning.detailSections.progress")}</h3>
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="space-y-1.5"><Label>{t("projects.planning.schedulingMode")}</Label><select className="input-field" disabled={!editable || saving} value={draft.schedulingMode} onChange={(event) => set("schedulingMode", event.target.value as PlanSchedulingMode)}><option value="Automatic">{t("projects.planning.automatic")}</option><option value="Manual">{t("projects.planning.manual")}</option></select></div>
          {activity.isSummaryTask ? <><Metric label={t("projects.planning.complete")} value={activity.progress == null ? "—" : `${roundPlanProgress(activity.progress)}%`} /><p className="text-xs text-muted-foreground sm:col-span-2">{t("projects.planning.calculatedFromSubtasks")}</p></> : <div className="space-y-1.5"><Label htmlFor="planning-progress">{t("projects.planning.complete")}</Label><Input id="planning-progress" type="number" min="0" max="100" step="1" disabled={!editable || saving} value={draft.progress ?? ""} onChange={(event) => set("progress", event.target.value === "" ? null : Number(event.target.value))} onBlur={() => set("progress", roundPlanProgress(draft.progress))} /></div>}
        </div>
      </section>
    </div> : null}
    {tab === "schedule" ? <div className="space-y-3"><section className="rounded-lg border p-3"><h3 className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.planning.detailSections.schedule")}</h3><div className="grid gap-3 sm:grid-cols-2">{activity?.isSummaryTask ? <><Metric label={t("projects.planning.durationShort")} value={String(activity.duration)} /><Metric label={t("projects.planning.start")} value={dateValue(activity.startDate) || "—"} /><Metric label={t("projects.planning.finish")} value={dateValue(activity.endDate) || "—"} /></> : <><div className="space-y-1.5"><Label htmlFor="planning-duration">{t("projects.planning.durationShort")}</Label><Input id="planning-duration" type="number" min="0" disabled={!editable || saving || activity?.type === "Milestone"} value={draft.duration} onChange={(event) => set("duration", Number(event.target.value))} /></div><div className="space-y-1.5"><Label htmlFor="planning-start">{t("projects.planning.start")}</Label><Input id="planning-start" type="date" disabled={!editable || saving} value={draft.startDate} onChange={(event) => set("startDate", event.target.value)} /></div><div className="space-y-1.5"><Label htmlFor="planning-finish">{t("projects.planning.finish")}</Label><Input id="planning-finish" type="date" disabled={!editable || saving} value={draft.endDate} onChange={(event) => set("endDate", event.target.value)} /></div></>}<div className="space-y-1.5"><Label htmlFor="planning-actual-start">{t("projects.planning.actualStart")}</Label><Input id="planning-actual-start" type="date" disabled={!editable || saving} value={draft.actualStart} onChange={(event) => set("actualStart", event.target.value)} /></div><div className="space-y-1.5"><Label htmlFor="planning-actual-finish">{t("projects.planning.actualFinish")}</Label><Input id="planning-actual-finish" type="date" disabled={!editable || saving} value={draft.actualFinish} onChange={(event) => set("actualFinish", event.target.value)} /></div><div className="space-y-1.5"><Label htmlFor="planning-constraint-type">{t("projects.planning.constraintType")}</Label><select id="planning-constraint-type" className="input-field" disabled={!editable || saving} value={draft.constraintType} onChange={(event) => set("constraintType", event.target.value as PlanConstraintType)}>{constraintTypes.map((value) => <option key={value} value={value}>{t(`projects.planning.constraintTypes.${value}`)}</option>)}</select></div><div className="space-y-1.5"><Label htmlFor="planning-constraint-date">{t("projects.planning.constraintDate")}</Label><Input id="planning-constraint-date" type="date" disabled={!editable || saving || draft.constraintType === "AsSoonAsPossible" || draft.constraintType === "AsLateAsPossible"} value={draft.constraintDate} onChange={(event) => set("constraintDate", event.target.value)} /></div><div className="space-y-1.5"><Label htmlFor="planning-deadline">{t("projects.planning.deadline")}</Label><Input id="planning-deadline" type="date" disabled={!editable || saving} value={draft.deadline} onChange={(event) => set("deadline", event.target.value)} /></div><div className="space-y-1.5 sm:col-span-2"><Label htmlFor="planning-effective-calendar">{t("projects.planning.effectiveCalendar")}</Label><select id="planning-effective-calendar" className="input-field" disabled={!editable || saving || calendars.isLoading} value={draft.calendarId} onChange={(event) => set("calendarId", event.target.value)}><option value="">{t("projects.planning.planDefaultCalendar")}</option>{(calendars.data ?? []).map((calendar) => <option key={calendar.id} value={calendar.id}>{calendar.name}{calendar.isDefault ? ` · ${t("projects.planning.default")}` : ""}</option>)}</select></div></div></section>{activity?.deadline ? <p className="rounded border px-3 py-2 text-xs text-muted-foreground">{t("projects.planning.deadlineStatus")}: {activity.deadlineExceeded ? t("projects.planning.deadlineExceeded") : t("projects.planning.deadlineOnTrack")}</p> : null}{activity?.schedulingConflict ? <p className="text-sm text-destructive">{t("projects.planning.schedulingConflict")}</p> : null}</div> : null}
    {tab === "schedule" ? <section className="space-y-3 rounded-lg border border-border/60 bg-muted/20 p-3"><h3 className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.planning.schedulingExplanation")}</h3>{explanation.isLoading ? <p className="text-sm text-muted-foreground">{t("projects.planning.loadingDetails")}</p> : explanation.isError || !explanationData ? <p className="text-sm text-muted-foreground">{t("projects.planning.unavailable")}</p> : <><div className="grid gap-3 sm:grid-cols-2"><Metric label={t("projects.planning.schedulingMode")} value={t(explanationData.schedulingMode === "Manual" ? "projects.planning.manual" : "projects.planning.automatic")} /><Metric label={t("projects.planning.effectiveCalendar")} value={explanationData.effectiveCalendarId ? calendarNames.get(explanationData.effectiveCalendarId) ?? explanationData.effectiveCalendarId : t("projects.planning.planDefaultCalendar")} /><Metric label={t("projects.planning.calculatedStart")} value={dateValue(explanationData.start) || "—"} /><Metric label={t("projects.planning.calculatedFinish")} value={dateValue(explanationData.finish) || "—"} /><Metric label={t("projects.planning.critical")} value={explanationData.critical ? t("common.yes") : t("common.no")} /><Metric label={t("projects.planning.totalSlack")} value={explanationData.totalSlack == null ? "—" : String(explanationData.totalSlack)} /><Metric label={t("projects.planning.freeSlack")} value={explanationData.freeSlack == null ? "—" : String(explanationData.freeSlack)} /></div><div><p className="mb-1 text-xs font-medium text-muted-foreground">{t("projects.planning.schedulingReasonsTitle")}</p><div className="flex flex-wrap gap-1.5">{explanationData.reasons.map((reason, index) => <Badge key={`${reason.code}-${index}`} status={reason.code === "SchedulingConflict" || reason.code === "DeadlineExceeded" ? "Warning" : "Info"}>{schedulingReasonLabel(reason, activityNameById, t)}</Badge>)}{explanationData.reasons.length === 0 ? <span className="text-sm text-muted-foreground">—</span> : null}</div></div>{explanationData.issues.length ? <div className="space-y-1 rounded-md border border-destructive/30 bg-destructive/5 p-2 text-xs text-destructive" role="alert">{explanationData.issues.map((issue) => <p key={`${issue.code}-${issue.activityId}`}>{issue.message}</p>)}</div> : null}</>}</section> : null}
    {tab === "dependencies" ? <DependenciesTab /> : null}
    {tab === "resources" ? <ResourcesTab /> : null}
    {tab === "analysis" ? <div className="space-y-3"><section className="rounded-lg border p-3"><h3 className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.planning.detailSections.baseline")}</h3><div className="grid grid-cols-2 gap-3"><Metric label={t("projects.planning.currentStart")} value={dateValue(activity.startDate) || "—"} /><Metric label={t("projects.planning.currentFinish")} value={dateValue(activity.endDate) || "—"} /><Metric label={t("projects.planning.currentDuration")} value={String(activity.duration)} /><Metric label={t("projects.planning.baselineStart")} value={dateValue(activity.baselineStartDate) || "—"} /><Metric label={t("projects.planning.baselineFinish")} value={dateValue(activity.baselineFinishDate) || "—"} /><Metric label={t("projects.planning.baselineDuration")} value={activity.baselineDuration == null ? "—" : String(activity.baselineDuration)} /><Metric label={t("projects.planning.startVariance")} value={varianceValue(activity.startVarianceDays)} /><Metric label={t("projects.planning.finishVariance")} value={varianceValue(activity.finishVarianceDays)} /><Metric label={t("projects.planning.durationVariance")} value={varianceValue(activity.durationVarianceDays)} /></div></section><section className="rounded-lg border p-3"><h3 className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.planning.detailSections.analysis")}</h3><div className="grid grid-cols-2 gap-3"><Metric label={t("projects.planning.totalSlack")} value={activity.totalSlack == null ? "—" : String(activity.totalSlack)} /><Metric label={t("projects.planning.freeSlack")} value={activity.freeSlack == null ? "—" : String(activity.freeSlack)} /><Metric label={t("projects.planning.critical")} value={activity.critical ? t("common.yes") : t("common.no")} /></div></section></div> : null}
    {tab === "formatting" ? <div className="space-y-4">{!activity ? <p className="text-sm text-muted-foreground">{t("projects.planning.saveFirst")}</p> : <section className="rounded-lg border p-3"><h3 className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{t("projects.planning.formatting")}</h3><div className="grid gap-3 sm:grid-cols-2">
      <div className="flex flex-wrap gap-2 sm:col-span-2">
        <Button type="button" size="sm" variant={format?.isBold ? "secondary" : "outline"} disabled={!editable || saving} onClick={() => void saveFormat(nextFormat({ isBold: !format?.isBold }))}>{t("projects.planning.formatBold")}</Button>
        <Button type="button" size="sm" variant={format?.isItalic ? "secondary" : "outline"} disabled={!editable || saving} onClick={() => void saveFormat(nextFormat({ isItalic: !format?.isItalic }))}>{t("projects.planning.formatItalic")}</Button>
        <Button type="button" size="sm" variant={format?.isUnderline ? "secondary" : "outline"} disabled={!editable || saving} onClick={() => void saveFormat(nextFormat({ isUnderline: !format?.isUnderline }))}>{t("projects.planning.formatUnderline")}</Button>
      </div>
      <div><Label>{t("projects.planning.formatTextColor")}</Label><select value={format?.textColorToken ?? ""} disabled={!editable || saving} onChange={(e) => void saveFormat(nextFormat({ textColorToken: e.target.value || undefined }))} className="mt-1 h-10 w-full rounded-md border bg-background px-3 text-sm"><option value="">{t("projects.planning.formatNone")}</option>{["Slate", "Blue", "Teal", "Green", "Amber", "Red", "Violet"].map((value) => <option key={value} value={value}>{t(`projects.planning.formatColors.${value}`)}</option>)}</select></div>
      <div><Label>{t("projects.planning.formatHighlight")}</Label><select value={format?.highlightColorToken ?? ""} disabled={!editable || saving} onChange={(e) => void saveFormat(nextFormat({ highlightColorToken: e.target.value || undefined }))} className="mt-1 h-10 w-full rounded-md border bg-background px-3 text-sm"><option value="">{t("projects.planning.formatNone")}</option>{["Yellow", "Blue", "Green", "Orange", "Pink"].map((value) => <option key={value} value={value}>{t(`projects.planning.formatHighlights.${value}`)}</option>)}</select></div>
      <div><Label>{t("projects.planning.formatFontSize")}</Label><select value={format?.fontSizeToken ?? ""} disabled={!editable || saving} onChange={(e) => void saveFormat(nextFormat({ fontSizeToken: e.target.value || undefined }))} className="mt-1 h-10 w-full rounded-md border bg-background px-3 text-sm"><option value="">{t("projects.planning.formatNone")}</option>{["Small", "Normal", "Large", "ExtraLarge"].map((value) => <option key={value} value={value}>{value}</option>)}</select></div>
      <div className="sm:col-span-2"><Button type="button" variant="ghost" size="sm" disabled={!editable || saving || !format} onClick={() => void saveFormat(null)}>{t("projects.planning.clearFormatting")}</Button></div>
    </div></section>}</div> : null}
    </div>
    <div className="flex flex-wrap gap-2 border-t pt-3 text-xs"><Badge status={activity.critical ? "Critical" : "Active"}>{activity.critical ? t("projects.planning.critical") : t("common.active")}</Badge>{activity.deadlineExceeded ? <Badge status="Overdue">{t("projects.planning.deadlineExceeded")}</Badge> : null}</div></SideSheetBody><SideSheetFooter><Button type="button" variant="outline" disabled={saving} onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>{editable ? <Button type="submit" disabled={saving || mutations.updateActivity.isPending}><Save className="me-1.5 h-4 w-4" />{saving ? t("projects.planning.saving") : t("common.save")}</Button> : null}</SideSheetFooter></form></SideSheet>;
}
function Metric({ label, value }: { label: string; value: string }) { return <div className="rounded border p-2"><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1 font-medium">{value}</p></div>; }
