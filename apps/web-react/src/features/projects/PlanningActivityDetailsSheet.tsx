import * as React from "react";
import { useQueries } from "@tanstack/react-query";
import { BarChart3, CalendarDays, ClipboardList, GitBranch, Save, Type, Users, X } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Textarea } from "@components/ui/Textarea";
import { useI18n } from "@i18n/messages";
import { formatNumberValue } from "@lib/formatting/number";
import { planningApiFull, planningKeys, usePlanningCalendarsFull, usePlanningFullMutations, usePlanningSchedulingExplanationFull, usePlanningActivitiesFull, usePlanningAssignmentsFull, usePlanningDependenciesFull, usePlanningResourcesFull, usePlanningWbsFull, type PlanActivityDto, type PlanDependencyDto, type PlanConstraintType, type PlanSchedulingExplanationDto, type PlanSchedulingMode } from "./api/planningFull";
import { useProjectTeam } from "./api/projects";
import { distributePlannedTaskWork } from "./lib/assignment-distribution";
import { formatPredecessor, parsePredecessor } from "./lib/predecessor-syntax";
import { roundPlanProgress } from "./lib/plan-progress";
import { buildPlanWorkspaceRows, type PlanWorkspaceRow } from "./lib/plan-workspace-rows";
import { PlanningDetailField, PlanningDetailMetric, PlanningDetailMetrics, PlanningDetailSection, PlanningDetailSummaryItem } from "./PlanningActivityDetailsUI";

const DETAIL_TABS = ["general", "schedule", "dependencies", "resources", "analysis", "formatting"] as const;
type DetailTab = typeof DETAIL_TABS[number];
type Props = { open: boolean; onOpenChange: (open: boolean) => void; activity?: PlanActivityDto; row?: PlanWorkspaceRow; rows?: PlanWorkspaceRow[]; activities?: PlanActivityDto[]; dependencies?: PlanDependencyDto[]; predecessorText?: string; projectId: string; projectName?: string; planName?: string; planId: string; editable: boolean; t: (key: string) => string };
type Draft = { name: string; description: string; duration: number; progress: number | null; startDate: string; endDate: string; actualStart: string; actualFinish: string; estimateHours: string; schedulingMode: PlanSchedulingMode; constraintType: PlanConstraintType; constraintDate: string; deadline: string; calendarId: string };
type PredecessorDraft = { id: string; predecessorActivityId: string; type: PlanDependencyDto["type"]; lag: string };
const constraintTypes: PlanConstraintType[] = ["AsSoonAsPossible", "AsLateAsPossible", "MustStartOn", "MustFinishOn", "StartNoEarlierThan", "StartNoLaterThan", "FinishNoEarlierThan", "FinishNoLaterThan"];
const reasonCodes = new Set(["ManualDatesPreserved", "MovedByPredecessor", "ConstraintApplied", "SkippedNonWorkingTime", "TaskCalendarOverride", "DeadlineExceeded", "Critical", "HasPredecessors", "HasSuccessors", "SchedulingConflict", "DependencyCycle"]);
const EMPTY_ACTIVITIES: PlanActivityDto[] = [];
const EMPTY_DEPENDENCIES: PlanDependencyDto[] = [];
const DETAIL_TAB_ICONS = { general: ClipboardList, schedule: CalendarDays, dependencies: GitBranch, resources: Users, analysis: BarChart3, formatting: Type };
function dateValue(value?: string) { return value?.slice(0, 10) ?? ""; }
function hoursValue(value: number | null | undefined, locale: string) { return value == null ? "—" : formatNumberValue(value, locale, { maximumFractionDigits: 2 }); }
function varianceValue(value: number | null | undefined, locale: string, t: (key: string) => string) { return value == null ? "—" : `${value > 0 ? "+" : ""}${formatNumberValue(value, locale)} ${t("projects.days")}`; }
function schedulingReasonLabel(reason: PlanSchedulingExplanationDto["reasons"][number], activityNameById: Map<string, string>, t: (key: string) => string, locale: string) {
  const label = reasonCodes.has(reason.code) ? t(`projects.planning.schedulingReasons.${reason.code}`) : reason.code;
  const related = reason.relatedActivityId ? activityNameById.get(reason.relatedActivityId) ?? reason.relatedActivityId : undefined;
  if (reason.code === "MovedByPredecessor" && related) return `${label} · ${related}`;
  if (reason.code === "DeadlineExceeded" && reason.workingDays) return `${label} · ${formatNumberValue(reason.workingDays, locale)} ${t("projects.days")}`;
  return label;
}
function formatLag(value: number | undefined, locale: string, t: (key: string) => string) { const lag = value ?? 0; return `${lag > 0 ? "+" : ""}${formatNumberValue(lag, locale)} ${t("projects.days")}`; }
function emptyDraft(): Draft { return { name: "", description: "", duration: 0, progress: null, startDate: "", endDate: "", actualStart: "", actualFinish: "", estimateHours: "", schedulingMode: "Automatic", constraintType: "AsSoonAsPossible", constraintDate: "", deadline: "", calendarId: "" }; }
function draftFromActivity(activity?: PlanActivityDto): Draft { if (!activity) return emptyDraft(); return { name: activity.name, description: activity.description ?? "", duration: activity.duration, progress: roundPlanProgress(activity.progress), startDate: dateValue(activity.startDate), endDate: dateValue(activity.endDate), actualStart: dateValue(activity.actualStart), actualFinish: dateValue(activity.actualFinish), estimateHours: activity.estimateHours == null ? "" : String(activity.estimateHours), schedulingMode: activity.schedulingMode ?? "Automatic", constraintType: activity.constraintType ?? "AsSoonAsPossible", constraintDate: dateValue(activity.constraintDate), deadline: dateValue(activity.deadline), calendarId: activity.calendarId ?? "" }; }

export function PlanningActivityDetailsSheet({ open, onOpenChange, activity: selectedActivity, row: suppliedRow, rows: suppliedRows, activities: suppliedActivities, dependencies: suppliedDependencies, predecessorText: suppliedPredecessorText, projectId, projectName, planName, planId, editable, t }: Props) {
  const { locale, formatDate } = useI18n();
  const [tab, setTab] = React.useState<DetailTab>("general");
  const tabButtons = React.useRef<(HTMLButtonElement | null)[]>([]);
  const [draft, setDraft] = React.useState<Draft>(emptyDraft());
  const mutations = usePlanningFullMutations(projectId, planId);
  const calendars = usePlanningCalendarsFull(projectId, planId, open && tab === "schedule");
  const activitiesQuery = usePlanningActivitiesFull(projectId, planId);
  const assignmentsQuery = usePlanningAssignmentsFull(projectId, planId, open);
  const dependenciesQuery = usePlanningDependenciesFull(projectId, planId);
  const resourcesQuery = usePlanningResourcesFull(open && tab === "resources");
  const projectTeamQuery = useProjectTeam(projectId);
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
  const [predecessorDrafts, setPredecessorDrafts] = React.useState<PredecessorDraft[]>([]);
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const initializedActivity = React.useRef<string | null>(null);
  const predecessorDraftSequence = React.useRef(0);
  const successorDeps = React.useMemo(() => dependencies.filter((d) => d.predecessorActivityId === activity?.id), [dependencies, activity?.id]);
  const activityNameById = React.useMemo(() => new Map(activities.map((a) => [a.id, `${a.code} · ${a.name}`])), [activities]);
  const rowNumberByActivity = React.useMemo(() => new Map(rows.flatMap((item) => item.activity ? [[item.activity.id, item.rowNumber] as const] : [])), [rows]);
  const activityByRowNumber = React.useMemo(() => new Map(rows.flatMap((item) => item.activity ? [[item.rowNumber, item.activity] as const] : [])), [rows]);
  const initializationData = React.useRef({ activity, predecessorText, activityByRowNumber });
  initializationData.current = { activity, predecessorText, activityByRowNumber };
  const activityAssignments = React.useMemo(() => (assignmentsQuery.data ?? []).filter((a) => a.activityId === activity?.id), [assignmentsQuery.data, activity?.id]);
  const resourceNames = React.useMemo(() => new Map((resourcesQuery.data ?? []).map((resource) => [resource.id, resource.name])), [resourcesQuery.data]);
  const projectMembers = React.useMemo(() => projectTeamQuery.data ?? [], [projectTeamQuery.data]);
  const activeProjectMembers = projectMembers.filter((member) => member.status.toLowerCase() === "active" && member.allocationPercentage > 0);
  const memberByResourceId = React.useMemo(() => new Map(projectMembers.map((member) => [member.resourceId, member])), [projectMembers]);
  const [pendingResourceIds, setPendingResourceIds] = React.useState<string[]>([]);
  const [resourceToAdd, setResourceToAdd] = React.useState("");
  const [savingAssignments, setSavingAssignments] = React.useState(false);
  const [assignmentError, setAssignmentError] = React.useState<string | null>(null);
  const [assignmentSaved, setAssignmentSaved] = React.useState(false);
  const taskStartDate = dateValue(activity?.startDate);
  const taskEndDate = dateValue(activity?.endDate);
  const capacityRequests = React.useMemo(() => {
    const byResource = new Map<string, { resourceId: string; startDate: string; endDate: string }>();
    for (const assignment of activityAssignments) {
      const startDate = dateValue(assignment.startDate) || taskStartDate;
      const endDate = dateValue(assignment.endDate) || taskEndDate;
      if (startDate && endDate) byResource.set(assignment.resourceId, { resourceId: assignment.resourceId, startDate, endDate });
    }
    for (const resourceId of pendingResourceIds) {
      if (byResource.has(resourceId) || !taskStartDate || !taskEndDate) continue;
      byResource.set(resourceId, { resourceId, startDate: taskStartDate, endDate: taskEndDate });
    }
    return [...byResource.values()];
  }, [activityAssignments, pendingResourceIds, taskEndDate, taskStartDate]);
  const capacityQueries = useQueries({
    queries: capacityRequests.map((request) => ({
      queryKey: planningKeys.assignmentCapacity(projectId, planId, request.resourceId, request.startDate, request.endDate),
      queryFn: () => planningApiFull.assignmentCapacity(projectId, planId, request.resourceId, request.startDate, request.endDate),
      enabled: Boolean(open && tab === "resources" && planId),
      staleTime: 0,
      retry: false,
    })),
  });
  const capacityByResourceId = React.useMemo(() => new Map(capacityRequests.map((request, index) => [request.resourceId, {
    request,
    query: capacityQueries[index],
  }] as const)), [capacityQueries, capacityRequests]);
  const existingAssignedWorkHours = activityAssignments.reduce((total, assignment) => total + (assignment.assignedWorkHours ?? assignment.plannedHours ?? 0), 0);
  const plannedWorkHours = activity?.estimateHours ?? 0;
  const pendingCapacityStates = pendingResourceIds.map((resourceId) => capacityByResourceId.get(resourceId)?.query);
  const pendingCapacitiesReady = pendingResourceIds.length > 0 && pendingCapacityStates.every((query) => Boolean(query?.data && !query.isError && !query.isFetching));
  const pendingMembershipInvalid = pendingCapacityStates.some((query) => query?.data?.isScheduleWithinMembership === false);
  const pendingCapacityError = pendingCapacityStates.some((query) => query?.isError);
  const distribution = distributePlannedTaskWork(plannedWorkHours, existingAssignedWorkHours, pendingResourceIds.map((resourceId) => {
    const capacity = capacityByResourceId.get(resourceId)?.query?.data;
    return {
      resourceId,
      projectAllocationPercentage: memberByResourceId.get(resourceId)?.allocationPercentage ?? 0,
      remainingAssignableHours: capacity?.isScheduleWithinMembership === false ? 0 : capacity?.remainingAssignableHours ?? 0,
    };
  }));
  const taskWorkHasUnsavedChanges = Number(draft.estimateHours || 0) !== plannedWorkHours;
  const taskScheduleHasUnsavedChanges = taskStartDate !== draft.startDate || taskEndDate !== draft.endDate;
  const calendarNames = React.useMemo(() => new Map((calendars.data ?? []).map((calendar) => [calendar.id, calendar.name])), [calendars.data]);
  React.useEffect(() => {
    if (!open) { initializedActivity.current = null; return; }
    const current = initializationData.current;
    if (!current.activity || initializedActivity.current === current.activity.id) return;
    if (!suppliedPredecessorText && !suppliedDependencies && dependenciesQuery.isLoading) return;
    if (!suppliedPredecessorText && !suppliedRows && wbsQuery.isLoading) return;
    initializedActivity.current = current.activity.id;
    setDraft(draftFromActivity(current.activity));
    setTab("general");
    setPredecessorDrafts(current.predecessorText.split(",").flatMap((raw) => {
      const parsed = parsePredecessor(raw);
      const predecessor = parsed ? current.activityByRowNumber.get(parsed.rowNumber) : undefined;
      return parsed && predecessor ? [{ id: `predecessor-existing-${predecessorDraftSequence.current++}`, predecessorActivityId: predecessor.id, type: parsed.type, lag: String(parsed.lag ?? 0) }] : [];
    }));
    setError(null);
  }, [activity?.id, dependenciesQuery.isLoading, open, suppliedDependencies, suppliedPredecessorText, suppliedRows, wbsQuery.isLoading]);
  React.useEffect(() => {
    setPendingResourceIds([]);
    setResourceToAdd("");
    setAssignmentError(null);
    setAssignmentSaved(false);
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
  function addPredecessorDraft() {
    setPredecessorDrafts((current) => [...current, { id: `predecessor-new-${predecessorDraftSequence.current++}`, predecessorActivityId: "", type: "FinishToStart", lag: "0" }]);
  }
  function updatePredecessorDraft(id: string, changes: Partial<Omit<PredecessorDraft, "id">>) {
    setPredecessorDrafts((current) => current.map((draft) => draft.id === id ? { ...draft, ...changes } : draft));
  }
  function removePredecessorDraft(id: string) {
    setPredecessorDrafts((current) => current.filter((draft) => draft.id !== id));
  }
  function serializePredecessorDrafts() {
    return predecessorDrafts.flatMap((draft) => {
      if (!draft.predecessorActivityId) return [];
      const rowNumber = rowNumberByActivity.get(draft.predecessorActivityId);
      const lag = Number(draft.lag || 0);
      if (rowNumber === undefined) throw new Error(t("projects.planning.predecessorRowError"));
      if (draft.predecessorActivityId === activity?.id) throw new Error(t("projects.planning.predecessorSelfError"));
      if (!Number.isSafeInteger(lag)) throw new Error(t("projects.planning.invalidDependencyLag"));
      return [formatPredecessor(rowNumber, draft.type, lag)];
    }).join(", ");
  }
  function DependenciesTab() {
    if (!activity) return <p className="text-sm text-muted-foreground">{t("projects.planning.saveFirst")}</p>;
    function DependencyTable({ title, items, activityId }: { title: string; items: PlanDependencyDto[]; activityId: (dependency: PlanDependencyDto) => string }) {
      return <section className="min-w-0 space-y-2">
        <div className="flex items-baseline justify-between gap-3">
          <h4 className="text-sm font-semibold">{title}</h4>
          <span className="text-xs tabular-nums text-muted-foreground">{items.length}</span>
        </div>
        {items.length === 0 ? <p className="border-s-2 border-border ps-3 py-2 text-sm text-muted-foreground">{title === t("projects.planning.predecessors") ? t("projects.planning.noPredecessors") : t("projects.planning.noSuccessors")}</p> :
          <div className="overflow-x-auto rounded-sm border border-border/70">
            <table className="w-full min-w-[32rem] text-sm" aria-label={title}>
              <thead className="bg-muted/35 text-start text-xs text-muted-foreground">
                <tr><th className="px-3 py-2 text-start font-medium">{t("projects.planning.dependencyTask")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.dependencyType")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.dependencyLag")}</th></tr>
              </thead>
              <tbody className="divide-y divide-border/70">
                {items.map((dependency) => <tr key={dependency.id}>
                  <td className="max-w-[22rem] break-words px-3 py-2 font-medium">{activityNameById.get(activityId(dependency)) ?? activityId(dependency)}</td>
                  <td className="px-3 py-2 text-muted-foreground">{t(`projects.planning.dependencyTypes.${dependency.type}`)}</td>
                  <td className="whitespace-nowrap px-3 py-2 tabular-nums">{formatLag(dependency.lag, locale, t)}</td>
                </tr>)}
              </tbody>
            </table>
          </div>}
      </section>;
    }
    return <div className="space-y-6">
      <PlanningDetailSection title={t("projects.planning.detailSections.dependencies")}>
        <div className="space-y-2">
          <div className="flex items-baseline justify-between gap-3"><h4 className="text-sm font-semibold">{t("projects.planning.predecessors")}</h4><Button type="button" variant="secondary" size="sm" disabled={!editable || saving} onClick={addPredecessorDraft}>{t("projects.planning.addPredecessor")}</Button></div>
          {predecessorDrafts.length === 0 ? <p className="border-s-2 border-border ps-3 py-2 text-sm text-muted-foreground">{t("projects.planning.noPredecessors")}</p> : <div className="overflow-x-auto rounded-sm border border-border/70">
            <table className="w-full min-w-[46rem] text-sm" aria-label={t("projects.planning.predecessors")}>
              <thead className="bg-muted/35 text-start text-xs text-muted-foreground"><tr><th scope="col" className="px-3 py-2 text-start font-medium">{t("projects.planning.predecessorTask")}</th><th scope="col" className="px-3 py-2 text-start font-medium">{t("projects.planning.dependencyType")}</th><th scope="col" className="px-3 py-2 text-start font-medium">{t("projects.planning.dependencyLag")}</th><th scope="col" className="px-3 py-2 text-end font-medium"><span className="sr-only">{t("projects.planning.removePredecessor")}</span></th></tr></thead>
              <tbody className="divide-y divide-border/70">{predecessorDrafts.map((draft, index) => <tr key={draft.id}>
                <td className="min-w-[19rem] px-3 py-2"><select id={`${draft.id}-task`} className="input-field min-w-[18rem]" value={draft.predecessorActivityId} disabled={!editable || saving} aria-label={`${t("projects.planning.selectPredecessorTask")} ${formatNumberValue(index + 1, locale)}`} onChange={(event) => updatePredecessorDraft(draft.id, { predecessorActivityId: event.target.value })}>
                  <option value="">{t("projects.planning.selectPredecessorTask")}</option>
                  {rows.map((item) => item.activity ? <option key={item.activity.id} value={item.activity.id} disabled={item.activity.id === activity.id || predecessorDrafts.some((other) => other.id !== draft.id && other.predecessorActivityId === item.activity?.id)}>{`${"\u00a0".repeat(Math.min(item.depth, 8) * 2)}${item.activity.code} · ${item.activity.name}`}</option> : item.wbs ? <option key={item.id} value={`group:${item.id}`} disabled>{`${"\u00a0".repeat(Math.min(item.depth, 8) * 2)}${item.wbs.code} · ${item.wbs.name}`}</option> : null)}
                </select></td>
                <td className="min-w-[13rem] px-3 py-2"><select id={`${draft.id}-type`} className="input-field min-w-[12rem]" value={draft.type} disabled={!editable || saving} aria-label={`${t("projects.planning.dependencyType")} ${formatNumberValue(index + 1, locale)}`} onChange={(event) => updatePredecessorDraft(draft.id, { type: event.target.value as PlanDependencyDto["type"] })}>{(["FinishToStart", "StartToStart", "FinishToFinish", "StartToFinish"] as const).map((type) => <option key={type} value={type}>{t(`projects.planning.dependencyTypes.${type}`)}</option>)}</select></td>
                <td className="min-w-[8rem] px-3 py-2"><Input id={`${draft.id}-lag`} type="number" step="1" className="min-w-24" value={draft.lag} disabled={!editable || saving} aria-label={`${t("projects.planning.dependencyLag")} ${formatNumberValue(index + 1, locale)}`} title={t("projects.planning.dependencyLagHint")} onChange={(event) => updatePredecessorDraft(draft.id, { lag: event.target.value })} /></td>
                <td className="px-3 py-2 text-end"><Button type="button" variant="ghost" size="sm" className="h-8 w-8 p-0" aria-label={`${t("projects.planning.removePredecessor")}: ${draft.predecessorActivityId ? activityNameById.get(draft.predecessorActivityId) ?? draft.predecessorActivityId : formatNumberValue(index + 1, locale)}`} disabled={!editable || saving} onClick={() => removePredecessorDraft(draft.id)}><X className="h-4 w-4" /></Button></td>
              </tr>)}</tbody>
            </table>
          </div>}
        </div>
        <PlanningDetailMetrics className="sm:grid-cols-2">
          <PlanningDetailMetric label={t("projects.planning.predecessorCount")} value={String(predecessorDrafts.filter((draft) => draft.predecessorActivityId).length)} />
          <PlanningDetailMetric label={t("projects.planning.successorCount")} value={String(successorDeps.length)} />
        </PlanningDetailMetrics>
      </PlanningDetailSection>
      <div className="grid gap-6 lg:grid-cols-2">
        <DependencyTable title={t("projects.planning.successors")} items={successorDeps} activityId={(dependency) => dependency.activityId} />
      </div>
    </div>;
  }
  function ResourcesTab() {
    if (!activity) return <p className="text-sm text-muted-foreground">{t("projects.planning.saveFirst")}</p>;
    const assignedResourceIds = new Set(activityAssignments.map((assignment) => assignment.resourceId));
    const selectableMembers = activeProjectMembers.filter((member) => !assignedResourceIds.has(member.resourceId) && !pendingResourceIds.includes(member.resourceId));
    const hasTaskSchedule = Boolean(taskStartDate && taskEndDate && taskEndDate >= taskStartDate);
    const taskDetailsMustBeSaved = taskWorkHasUnsavedChanges || taskScheduleHasUnsavedChanges;
    const hasUnavailablePendingResource = distribution.assignments.some((assignment) => assignment.assignedWorkHours <= 0);
    const canAssignResources = editable && activity.type === "Task" && !activity.isSummaryTask
      && plannedWorkHours > existingAssignedWorkHours && hasTaskSchedule && !taskDetailsMustBeSaved;
    const canSaveAssignments = canAssignResources && pendingResourceIds.length > 0 && pendingCapacitiesReady
      && !pendingMembershipInvalid && !pendingCapacityError && !hasUnavailablePendingResource
      && distribution.assignments.some((assignment) => assignment.assignedWorkHours > 0)
      && !savingAssignments && !mutations.createAssignmentsBatch.isPending;
    const proposedWorkHours = distribution.assignments.reduce((total, assignment) => total + assignment.assignedWorkHours, 0);

    async function savePendingAssignments() {
      if (!activity || !canSaveAssignments) return;
      setSavingAssignments(true);
      setAssignmentError(null);
      setAssignmentSaved(false);
      try {
        await mutations.createAssignmentsBatch.mutateAsync({
          activityId: activity.id,
          assignments: distribution.assignments.map((assignment) => ({
            resourceId: assignment.resourceId,
            assignedWorkHours: assignment.assignedWorkHours,
            startDate: taskStartDate,
            endDate: taskEndDate,
          })),
        });
        setPendingResourceIds([]);
        setResourceToAdd("");
        setAssignmentSaved(true);
      } catch {
        setAssignmentError(t("projects.planning.assignmentBatchSaveFailed"));
      } finally {
        setSavingAssignments(false);
      }
    }

    return <div className="space-y-6">
      <PlanningDetailSection title={t("projects.planning.detailSections.resources")}>
        <div className="grid gap-4 sm:grid-cols-2">
          <PlanningDetailField id="planning-estimate-hours" label={t("projects.planning.estimateHours")}>
            <Input id="planning-estimate-hours" type="number" min="0" step="0.5" disabled={!editable || saving} value={draft.estimateHours} onChange={(event) => { set("estimateHours", event.target.value); setAssignmentSaved(false); }} />
          </PlanningDetailField>
          <PlanningDetailMetrics className="grid-cols-2 sm:grid-cols-2 sm:col-span-2 lg:grid-cols-4">
            <PlanningDetailMetric label={t("projects.planning.assignmentCount")} value={String(activityAssignments.length + pendingResourceIds.length)} />
            <PlanningDetailMetric label={t("projects.planning.taskPlannedWork")} value={`${hoursValue(plannedWorkHours, locale)} ${t("common.hours")}`} />
            <PlanningDetailMetric label={t("projects.planning.existingAssignedWork")} value={`${hoursValue(existingAssignedWorkHours, locale)} ${t("common.hours")}`} />
            {pendingResourceIds.length > 0 ? <PlanningDetailMetric label={t("projects.planning.proposedAssignedWork")} value={pendingCapacitiesReady ? `${hoursValue(proposedWorkHours, locale)} ${t("common.hours")}` : "—"} /> : null}
            <PlanningDetailMetric label={t("projects.planning.unassignedWork")} value={`${hoursValue(pendingResourceIds.length > 0 && pendingCapacitiesReady ? distribution.unassignedWorkHours : Math.max(0, plannedWorkHours - existingAssignedWorkHours), locale)} ${t("common.hours")}`} />
          </PlanningDetailMetrics>
        </div>
      </PlanningDetailSection>

      {taskDetailsMustBeSaved ? <p className="border-s-2 border-warning ps-3 py-2 text-xs leading-5 text-warning" role="status">{t("projects.planning.saveTaskDetailsBeforeAssignment")}</p> : null}
      {!hasTaskSchedule ? <p className="border-s-2 border-warning ps-3 py-2 text-xs leading-5 text-warning" role="status">{t("projects.planning.assignmentScheduleRequired")}</p> : null}
      {plannedWorkHours <= 0 ? <p className="border-s-2 border-warning ps-3 py-2 text-xs leading-5 text-warning" role="status">{t("projects.planning.taskPlannedWorkRequired")}</p> : null}
      {projectTeamQuery.isError ? <p className="text-sm text-destructive" role="alert">{t("projects.planning.projectTeamLoadFailed")}</p> : null}
      {activity.type !== "Task" || activity.isSummaryTask ? <p className="text-xs leading-5 text-muted-foreground" role="status">{t("projects.planning.onlyLeafTasksAssignable")}</p> : null}

      <PlanningDetailSection title={t("projects.planning.resourceAssignmentActions")}>
        <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-end">
          <PlanningDetailField id="planning-resource-to-add" label={t("projects.planning.selectProjectResource")}>
            <select
              id="planning-resource-to-add"
              className="input-field w-full"
              value={resourceToAdd}
              disabled={!canAssignResources || projectTeamQuery.isLoading || projectTeamQuery.isError || selectableMembers.length === 0}
              onChange={(event) => {
                const resourceId = event.currentTarget.value;
                setResourceToAdd("");
                if (!resourceId) return;
                setAssignmentError(null);
                setAssignmentSaved(false);
                setPendingResourceIds((current) => current.includes(resourceId) || assignedResourceIds.has(resourceId) ? current : [...current, resourceId]);
              }}
            >
              <option value="">{projectTeamQuery.isLoading ? t("projects.planning.loadingProjectTeam") : t("projects.planning.selectResourceToAdd")}</option>
              {selectableMembers.map((member) => <option key={member.resourceId} value={member.resourceId}>{member.resourceName} · {hoursValue(member.allocationPercentage, locale)}%</option>)}
            </select>
          </PlanningDetailField>
          {projectTeamQuery.isLoading ? <p className="pb-2 text-xs text-muted-foreground" role="status">{t("projects.planning.loadingProjectTeam")}</p> : null}
        </div>
        {!projectTeamQuery.isLoading && !projectTeamQuery.isError && activeProjectMembers.length === 0 ? <p className="text-xs text-muted-foreground">{t("projects.planning.noEligibleProjectResources")}</p> : null}
        {selectableMembers.length === 0 && activeProjectMembers.length > 0 && pendingResourceIds.length === 0 ? <p className="text-xs text-muted-foreground">{t("projects.planning.noEligibleProjectResources")}</p> : null}
      </PlanningDetailSection>

      <PlanningDetailSection title={t("projects.planning.resourceAssignments")}>
        {assignmentsQuery.isLoading ? <p className="text-sm text-muted-foreground" role="status">{t("projects.planning.loadingDetails")}</p> : assignmentsQuery.isError ? <p className="text-sm text-destructive" role="alert">{t("projects.planning.unavailable")}</p> : activityAssignments.length === 0 ? <p className="border-s-2 border-border ps-3 py-2 text-sm text-muted-foreground">{t("projects.planning.noResourceAssignments")}</p> :
          <div className="overflow-x-auto rounded-sm border border-border/70">
            <table className="w-full min-w-[44rem] text-sm" aria-label={t("projects.planning.resourceAssignments")}>
              <thead className="bg-muted/35 text-xs text-muted-foreground">
                <tr><th className="px-3 py-2 text-start font-medium">{t("projects.planning.resourceColumn")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.assignedWorkHours")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.projectAllocation")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.remainingResourceCapacity")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.assignmentStart")} / {t("projects.planning.assignmentFinish")}</th></tr>
              </thead>
              <tbody className="divide-y divide-border/70">
                {activityAssignments.map((assignment) => {
                  const member = memberByResourceId.get(assignment.resourceId);
                  const capacityState = capacityByResourceId.get(assignment.resourceId)?.query;
                  const workHours = assignment.assignedWorkHours ?? assignment.plannedHours ?? 0;
                  return <tr key={assignment.id}>
                    <td className="max-w-[16rem] break-words px-3 py-2 font-medium">{resourceNames.get(assignment.resourceId) ?? member?.resourceName ?? assignment.resourceName ?? assignment.resourceId}</td>
                    <td className="whitespace-nowrap px-3 py-2 tabular-nums">{hoursValue(workHours, locale)} {t("common.hours")}</td>
                    <td className="whitespace-nowrap px-3 py-2 tabular-nums">{hoursValue(capacityState?.data?.projectAllocationPercentage ?? member?.allocationPercentage, locale)}%</td>
                    <td className="whitespace-nowrap px-3 py-2 tabular-nums">{capacityState?.data ? `${hoursValue(capacityState.data.remainingAssignableHours, locale)} ${t("common.hours")}` : capacityState?.isError ? t("projects.planning.resourceCapacityUnavailable") : t("projects.planning.loadingDetails")}</td>
                    <td className="whitespace-nowrap px-3 py-2 text-xs text-muted-foreground">{formatDate(dateValue(assignment.startDate) || taskStartDate)} – {formatDate(dateValue(assignment.endDate) || taskEndDate)}</td>
                  </tr>;
                })}
              </tbody>
            </table>
          </div>}
      </PlanningDetailSection>

      {pendingResourceIds.length > 0 ? <PlanningDetailSection title={t("projects.planning.proposedAssignedWork")}>
        <div className="overflow-x-auto rounded-sm border border-border/70">
          <table className="w-full min-w-[42rem] text-sm" aria-label={t("projects.planning.proposedAssignedWork")}>
            <thead className="bg-muted/35 text-xs text-muted-foreground">
              <tr><th className="px-3 py-2 text-start font-medium">{t("projects.planning.resourceColumn")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.projectAllocation")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.proposedAssignedWork")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.remainingResourceCapacity")}</th><th className="px-3 py-2 text-end font-medium"><span className="sr-only">{t("projects.planning.removePendingResource")}</span></th></tr>
            </thead>
            <tbody className="divide-y divide-border/70">
              {distribution.assignments.map((assignment) => {
                const member = memberByResourceId.get(assignment.resourceId);
                const capacityState = capacityByResourceId.get(assignment.resourceId)?.query;
                const membershipInvalid = capacityState?.data?.isScheduleWithinMembership === false;
                return <tr key={`pending-${assignment.resourceId}`}>
                  <td className="max-w-[16rem] break-words px-3 py-2 font-medium">{member?.resourceName ?? resourceNames.get(assignment.resourceId) ?? assignment.resourceId}{membershipInvalid ? <p className="mt-1 text-xs font-normal text-warning" role="alert">{t("projects.planning.assignmentMembershipLimit")}</p> : null}{capacityState?.isError ? <p className="mt-1 text-xs font-normal text-destructive" role="alert">{t("projects.planning.resourceCapacityUnavailable")}</p> : null}</td>
                  <td className="whitespace-nowrap px-3 py-2 tabular-nums">{hoursValue(member?.allocationPercentage, locale)}%</td>
                  <td className="whitespace-nowrap px-3 py-2 tabular-nums">{pendingCapacitiesReady ? `${hoursValue(assignment.assignedWorkHours, locale)} ${t("common.hours")}` : t("projects.planning.loadingDetails")}</td>
                  <td className="whitespace-nowrap px-3 py-2 tabular-nums">{pendingCapacitiesReady ? `${hoursValue(assignment.remainingCapacityAfterAssignment, locale)} ${t("common.hours")}` : t("projects.planning.loadingDetails")}</td>
                  <td className="px-3 py-2 text-end"><Button type="button" variant="ghost" size="sm" className="h-8 w-8 p-0" aria-label={`${t("projects.planning.removePendingResource")}: ${member?.resourceName ?? assignment.resourceId}`} title={t("projects.planning.removePendingResource")} disabled={savingAssignments} onClick={() => { setPendingResourceIds((current) => current.filter((resourceId) => resourceId !== assignment.resourceId)); setAssignmentError(null); setAssignmentSaved(false); }}><X className="h-4 w-4" /></Button></td>
                </tr>;
              })}
            </tbody>
          </table>
        </div>
      </PlanningDetailSection> : null}

      {pendingResourceIds.length > 0 && !pendingCapacitiesReady && !pendingCapacityError ? <p className="text-xs text-muted-foreground" role="status">{t("projects.planning.loadingResourceCapacities")}</p> : null}
      {pendingResourceIds.length > 0 && hasUnavailablePendingResource && pendingCapacitiesReady && !pendingMembershipInvalid ? <p className="border-s-2 border-warning ps-3 py-2 text-xs leading-5 text-warning" role="status">{t("projects.planning.removeResourceWithoutCapacity")}</p> : null}
      {assignmentError ? <p className="text-sm text-destructive" role="alert">{assignmentError}</p> : null}
      {assignmentSaved ? <p className="text-sm text-success" role="status">{t("projects.planning.resourceAssignmentsSaved")}</p> : null}
      {pendingResourceIds.length > 0 ? <div className="flex justify-end border-t border-border/70 pt-4"><Button type="button" size="sm" disabled={!canSaveAssignments} onClick={() => void savePendingAssignments()}>{savingAssignments || mutations.createAssignmentsBatch.isPending ? t("projects.planning.savingResourceAssignments") : t("projects.planning.saveResourceAssignments")}</Button></div> : null}
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
      const serializedPredecessors = serializePredecessorDrafts();
      if (serializedPredecessors.trim() !== predecessorText.trim()) await savePredecessors(serializedPredecessors);
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
  const assignedResourceNames = Array.from(new Set(activityAssignments.map((assignment) =>
    resourceNames.get(assignment.resourceId) ?? memberByResourceId.get(assignment.resourceId)?.resourceName ?? assignment.resourceName ?? assignment.resourceId,
  )));
  const assignedResourcesSummary = assignmentsQuery.isLoading ? t("projects.planning.loadingDetails") : assignmentsQuery.isError ? t("projects.planning.unavailable") : assignedResourceNames.length > 0 ? assignedResourceNames.join(", ") : t("common.unassigned");
  const progressValue = activity?.isSummaryTask ? activity.progress == null ? null : roundPlanProgress(activity.progress) : draft.progress == null ? null : roundPlanProgress(draft.progress);
  const plannedDateRange = draft.startDate || draft.endDate ? (draft.startDate ? formatDate(draft.startDate) : "—") + " – " + (draft.endDate ? formatDate(draft.endDate) : "—") : t("common.notAvailable");
  if (!activity || !row) return null;
  return (
    <SideSheet open={open} onOpenChange={onOpenChange} ariaLabel={t("projects.planning.taskDetails")}
      title={<span className="inline-flex items-center gap-2">{t("projects.planning.taskDetails")}<Badge status={editable ? "Active" : "ReadOnly"}>{editable ? t("projects.planning.editable") : t("projects.planning.readOnly")}</Badge></span>}
      description={t("projects.planning.taskDetailsDescription")} size="full" className="sm:max-w-6xl">
      <form onSubmit={(event) => void save(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-0">
          <header className="space-y-4 border-b border-border/70 pb-5">
            <div className="grid gap-5 md:grid-cols-[minmax(0,1.5fr)_minmax(15rem,0.7fr)] md:items-center">
              <div className="min-w-0">
                <div className="mt-1 flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-1"><h3 className="min-w-0 break-words text-xl font-semibold tracking-tight">{activity.name}</h3><span className="rounded-sm border border-border/70 px-2 py-0.5 font-mono text-xs text-muted-foreground">{activity.code}</span></div>
                <p className="mt-2 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground"><span className="break-words">{projectName ?? t("common.notAvailable")}</span><span aria-hidden="true">›</span><span className="break-words">{planName ?? t("common.notAvailable")}</span><span aria-hidden="true">›</span><span className="break-words">{row.wbs ? row.wbs.code + " · " + row.wbs.name : t("projects.planning.none")}</span></p>
              </div>
              <div className="min-w-0 space-y-2"><div className="flex items-baseline justify-between gap-3"><span className="text-xs font-medium text-muted-foreground">{t("projects.planning.complete")}</span><span className="text-sm font-semibold tabular-nums">{progressValue == null ? t("common.notAvailable") : formatNumberValue(progressValue, locale) + "%"}</span></div><div className="h-2 overflow-hidden rounded-full bg-muted" role="progressbar" aria-label={t("projects.planning.complete")} aria-valuemin={0} aria-valuemax={100} aria-valuenow={progressValue ?? undefined}><div className="h-full rounded-full bg-primary transition-[width]" style={{ width: Math.max(0, Math.min(100, progressValue ?? 0)) + "%" }} /></div></div>
            </div>
            <dl className="grid grid-cols-2 gap-x-5 gap-y-4 border-t border-border/70 pt-4 sm:grid-cols-4">
              <PlanningDetailSummaryItem label={t("projects.planning.taskScheduleStatus")}><div className="flex flex-wrap gap-1.5">{activity.schedulingConflict ? <Badge status="Warning">{t("projects.planning.schedulingConflict")}</Badge> : null}{activity.deadlineExceeded ? <Badge status="Overdue">{t("projects.planning.deadlineExceeded")}</Badge> : null}{activity.critical ? <Badge status="Critical">{t("projects.planning.critical")}</Badge> : null}{!activity.schedulingConflict && !activity.deadlineExceeded && !activity.critical ? <span className="text-muted-foreground">{t("common.notAvailable")}</span> : null}</div></PlanningDetailSummaryItem>
              <PlanningDetailSummaryItem label={t("projects.planning.assignedResources")}>{assignedResourcesSummary}</PlanningDetailSummaryItem>
              <PlanningDetailSummaryItem label={t("projects.planning.plannedDates")}>{plannedDateRange}</PlanningDetailSummaryItem>
              <PlanningDetailSummaryItem label={t("projects.planning.durationShort")}>{formatNumberValue(activity.duration, locale) + " " + t("projects.days")}</PlanningDetailSummaryItem>
            </dl>
          </header>
          {!editable ? <p className="border-s-2 border-muted-foreground/50 py-2 ps-3 text-xs leading-5 text-muted-foreground" role="status">{t("projects.planning.readOnlyDetails")}</p> : null}
          {error ? <p role="alert" className="my-4 border-s-2 border-destructive py-2 ps-3 text-sm text-destructive">{error}</p> : null}
          <div role="tablist" aria-label={t("projects.planning.taskDetails")} aria-orientation="horizontal" onKeyDown={onTabListKeyDown} className="sticky top-0 z-10 -mx-5 mt-4 border-b border-border bg-background/95 px-5 backdrop-blur supports-[backdrop-filter]:bg-background/85">
            <div className="flex min-w-0 gap-1 overflow-x-auto">{DETAIL_TABS.map((value, index) => { const Icon = DETAIL_TAB_ICONS[value]; const active = tab === value; return <Button key={value} ref={(element) => { tabButtons.current[index] = element; }} id={"planning-task-tab-" + value} type="button" role="tab" aria-selected={active} aria-controls="planning-task-details-panel" tabIndex={active ? 0 : -1} size="sm" variant="ghost" className={active ? "h-11 shrink-0 gap-2 rounded-none border-b-2 border-primary px-3 text-sm font-semibold text-foreground hover:bg-transparent" : "h-11 shrink-0 gap-2 rounded-none border-b-2 border-transparent px-3 text-sm text-muted-foreground hover:bg-muted/60 hover:text-foreground"} onClick={() => setTab(value)}><Icon aria-hidden="true" className="h-4 w-4 shrink-0" /><span>{t("projects.planning.detailTabs." + value)}</span></Button>; })}</div>
          </div>
          <div role="tabpanel" id="planning-task-details-panel" aria-labelledby={"planning-task-tab-" + tab} tabIndex={0} className="space-y-6 py-6">
            {tab === "general" ? <div className="space-y-6">
              <PlanningDetailSection title={t("projects.planning.detailSections.identity")}>
                <PlanningDetailMetrics className="sm:grid-cols-3"><PlanningDetailMetric label={t("projects.planning.activityType")} value={activity.type === "Milestone" ? t("projects.planning.milestone") : t("projects.planning.task")} /><PlanningDetailMetric label={t("projects.planning.wbsContext")} value={(row.wbs?.code ?? t("projects.planning.none")) + " · " + (row.wbs?.name ?? t("projects.planning.none"))} /><PlanningDetailMetric label={t("projects.planning.hierarchyLevel")} value={formatNumberValue(row.depth + 1, locale)} /></PlanningDetailMetrics>
                <div className="grid gap-4 sm:grid-cols-2"><PlanningDetailField id="planning-task-name" label={t("projects.planning.taskName")} className="sm:col-span-2"><Input id="planning-task-name" required disabled={!editable || saving} value={draft.name} onChange={(event) => set("name", event.target.value)} /></PlanningDetailField><PlanningDetailField id="planning-task-description" label={t("projects.planning.description")} className="sm:col-span-2"><Textarea id="planning-task-description" disabled={!editable || saving} value={draft.description} onChange={(event) => set("description", event.target.value)} rows={4} /></PlanningDetailField></div>
              </PlanningDetailSection>
              <PlanningDetailSection title={t("projects.planning.detailSections.progress")}><div className="grid gap-4 sm:grid-cols-2">
                <PlanningDetailField id="planning-scheduling-mode" label={t("projects.planning.schedulingMode")}><select id="planning-scheduling-mode" className="input-field" disabled={!editable || saving} value={draft.schedulingMode} onChange={(event) => set("schedulingMode", event.target.value as PlanSchedulingMode)}><option value="Automatic">{t("projects.planning.automatic")}</option><option value="Manual">{t("projects.planning.manual")}</option></select></PlanningDetailField>
                {activity.isSummaryTask ? <div className="space-y-2"><p className="text-sm font-medium">{t("projects.planning.complete")}</p><p className="text-sm font-semibold tabular-nums">{activity.progress == null ? t("common.notAvailable") : formatNumberValue(roundPlanProgress(activity.progress) ?? 0, locale) + "%"}</p><p className="text-xs leading-5 text-muted-foreground">{t("projects.planning.calculatedFromSubtasks")}</p></div> : <PlanningDetailField id="planning-progress" label={t("projects.planning.complete")}><Input id="planning-progress" type="number" min="0" max="100" step="1" disabled={!editable || saving} value={draft.progress ?? ""} onChange={(event) => set("progress", event.target.value === "" ? null : Number(event.target.value))} onBlur={() => set("progress", roundPlanProgress(draft.progress))} /></PlanningDetailField>}
              </div></PlanningDetailSection>
              <details className="border-t border-border/70 pt-4"><summary className="cursor-pointer select-none text-sm font-semibold marker:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">{t("projects.planning.planMetadata")}</summary><PlanningDetailMetrics className="mt-4 sm:grid-cols-3"><PlanningDetailMetric label={t("projects.planning.taskId")} value={activity.id} /><PlanningDetailMetric label={t("projects.planning.wbsId")} value={row.wbs?.id ?? t("projects.planning.none")} /><PlanningDetailMetric label={t("projects.planning.parentWbsId")} value={row.wbs?.parentWbsId ?? t("projects.planning.none")} /><PlanningDetailMetric label={t("projects.planning.deliverableId")} value={row.wbs?.deliverableId ?? t("projects.planning.none")} /><PlanningDetailMetric label={t("projects.planning.rowNumber")} value={formatNumberValue(row.rowNumber, locale)} /><PlanningDetailMetric label={t("projects.planning.sortOrder")} value={activity.sortOrder == null ? "—" : formatNumberValue(activity.sortOrder, locale)} /><PlanningDetailMetric label={t("projects.planning.planVersionId")} value={activity.planVersionId} /></PlanningDetailMetrics></details>
            </div> : null}
            {tab === "schedule" ? <div className="space-y-6">
              <PlanningDetailSection title={t("projects.planning.detailSections.schedule")}><div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                {activity.isSummaryTask ? <><PlanningDetailMetric label={t("projects.planning.durationShort")} value={formatNumberValue(activity.duration, locale) + " " + t("projects.days")} /><PlanningDetailMetric label={t("projects.planning.start")} value={activity.startDate ? formatDate(dateValue(activity.startDate)) : t("common.notAvailable")} /><PlanningDetailMetric label={t("projects.planning.finish")} value={activity.endDate ? formatDate(dateValue(activity.endDate)) : t("common.notAvailable")} /></> : <><PlanningDetailField id="planning-duration" label={t("projects.planning.durationShort")}><Input id="planning-duration" type="number" min="0" disabled={!editable || saving || activity.type === "Milestone"} value={draft.duration} onChange={(event) => set("duration", Number(event.target.value))} /></PlanningDetailField><PlanningDetailField id="planning-start" label={t("projects.planning.start")}><Input id="planning-start" type="date" disabled={!editable || saving} value={draft.startDate} onChange={(event) => set("startDate", event.target.value)} /></PlanningDetailField><PlanningDetailField id="planning-finish" label={t("projects.planning.finish")}><Input id="planning-finish" type="date" disabled={!editable || saving} value={draft.endDate} onChange={(event) => set("endDate", event.target.value)} /></PlanningDetailField></>}
                <PlanningDetailField id="planning-actual-start" label={t("projects.planning.actualStart")}><Input id="planning-actual-start" type="date" disabled={!editable || saving} value={draft.actualStart} onChange={(event) => set("actualStart", event.target.value)} /></PlanningDetailField><PlanningDetailField id="planning-actual-finish" label={t("projects.planning.actualFinish")}><Input id="planning-actual-finish" type="date" disabled={!editable || saving} value={draft.actualFinish} onChange={(event) => set("actualFinish", event.target.value)} /></PlanningDetailField>
              </div></PlanningDetailSection>
              <PlanningDetailSection title={t("projects.planning.scheduleControls")}><div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                <PlanningDetailField id="planning-constraint-type" label={t("projects.planning.constraintType")}><select id="planning-constraint-type" className="input-field" disabled={!editable || saving} value={draft.constraintType} onChange={(event) => set("constraintType", event.target.value as PlanConstraintType)}>{constraintTypes.map((value) => <option key={value} value={value}>{t("projects.planning.constraintTypes." + value)}</option>)}</select></PlanningDetailField>
                <PlanningDetailField id="planning-constraint-date" label={t("projects.planning.constraintDate")}><Input id="planning-constraint-date" type="date" disabled={!editable || saving || draft.constraintType === "AsSoonAsPossible" || draft.constraintType === "AsLateAsPossible"} value={draft.constraintDate} onChange={(event) => set("constraintDate", event.target.value)} /></PlanningDetailField>
                <PlanningDetailField id="planning-deadline" label={t("projects.planning.deadline")}><Input id="planning-deadline" type="date" disabled={!editable || saving} value={draft.deadline} onChange={(event) => set("deadline", event.target.value)} /></PlanningDetailField>
                <PlanningDetailField id="planning-effective-calendar" label={t("projects.planning.effectiveCalendar")} className="sm:col-span-2 xl:col-span-3"><select id="planning-effective-calendar" className="input-field" disabled={!editable || saving || calendars.isLoading} value={draft.calendarId} onChange={(event) => set("calendarId", event.target.value)}><option value="">{t("projects.planning.planDefaultCalendar")}</option>{(calendars.data ?? []).map((calendar) => <option key={calendar.id} value={calendar.id}>{calendar.name}{calendar.isDefault ? " · " + t("projects.planning.default") : ""}</option>)}</select></PlanningDetailField>
              </div>{activity.deadline ? <p className="border-s-2 border-border ps-3 py-2 text-xs leading-5 text-muted-foreground">{t("projects.planning.deadlineStatus")}: {activity.deadlineExceeded ? t("projects.planning.deadlineExceeded") : t("projects.planning.deadlineOnTrack")}</p> : null}{activity.schedulingConflict ? <p className="border-s-2 border-destructive py-2 ps-3 text-sm text-destructive" role="alert">{t("projects.planning.schedulingConflict")}</p> : null}</PlanningDetailSection>
              <PlanningDetailSection title={t("projects.planning.schedulingExplanation")}>
                {explanation.isLoading ? <p className="text-sm text-muted-foreground" role="status">{t("projects.planning.loadingDetails")}</p> : explanation.isError || !explanationData ? <p className="text-sm text-muted-foreground" role="status">{t("projects.planning.unavailable")}</p> : <>
                  <PlanningDetailMetrics className="xl:grid-cols-4"><PlanningDetailMetric label={t("projects.planning.schedulingMode")} value={t(explanationData.schedulingMode === "Manual" ? "projects.planning.manual" : "projects.planning.automatic")} /><PlanningDetailMetric label={t("projects.planning.effectiveCalendar")} value={explanationData.effectiveCalendarId ? calendarNames.get(explanationData.effectiveCalendarId) ?? explanationData.effectiveCalendarId : t("projects.planning.planDefaultCalendar")} /><PlanningDetailMetric label={t("projects.planning.calculatedStart")} value={explanationData.start ? formatDate(dateValue(explanationData.start)) : t("common.notAvailable")} /><PlanningDetailMetric label={t("projects.planning.calculatedFinish")} value={explanationData.finish ? formatDate(dateValue(explanationData.finish)) : t("common.notAvailable")} /><PlanningDetailMetric label={t("projects.planning.critical")} value={explanationData.critical ? t("common.yes") : t("common.no")} /><PlanningDetailMetric label={t("projects.planning.totalSlack")} value={explanationData.totalSlack == null ? "—" : formatNumberValue(explanationData.totalSlack, locale)} /><PlanningDetailMetric label={t("projects.planning.freeSlack")} value={explanationData.freeSlack == null ? "—" : formatNumberValue(explanationData.freeSlack, locale)} /></PlanningDetailMetrics>
                  <div className="space-y-2"><h4 className="text-xs font-semibold text-muted-foreground">{t("projects.planning.schedulingReasonsTitle")}</h4>{explanationData.reasons.length > 0 ? <ul className="flex flex-wrap gap-x-5 gap-y-2">{explanationData.reasons.map((reason, index) => <li key={reason.code + "-" + index} className="border-s-2 border-border ps-2 text-xs leading-5">{schedulingReasonLabel(reason, activityNameById, t, locale)}</li>)}</ul> : <p className="text-sm text-muted-foreground">—</p>}</div>
                  {explanationData.issues.length > 0 ? <div className="space-y-1 border-s-2 border-destructive py-2 ps-3 text-sm text-destructive" role="alert">{explanationData.issues.map((issue) => <p key={issue.code + "-" + issue.activityId}>{issue.message}</p>)}</div> : null}
                </>}
              </PlanningDetailSection>
            </div> : null}
            {tab === "dependencies" ? <DependenciesTab /> : null}
            {tab === "resources" ? <ResourcesTab /> : null}
            {tab === "analysis" ? <div className="space-y-6">
              <PlanningDetailSection title={t("projects.planning.detailSections.baseline")}><div className="overflow-x-auto rounded-sm border border-border/70"><table className="w-full min-w-[38rem] text-sm" aria-label={t("projects.planning.detailSections.baseline")}>
                <thead className="bg-muted/35 text-xs text-muted-foreground"><tr><th className="px-3 py-2 text-start font-medium">{t("projects.planning.analysisMeasure")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.currentValue")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.baselineValue")}</th><th className="px-3 py-2 text-start font-medium">{t("projects.planning.variance")}</th></tr></thead>
                <tbody className="divide-y divide-border/70">
                  <tr><th scope="row" className="px-3 py-2 text-start font-medium">{t("projects.planning.currentStart")}</th><td className="px-3 py-2">{activity.startDate ? formatDate(dateValue(activity.startDate)) : "—"}</td><td className="px-3 py-2">{activity.baselineStartDate ? formatDate(dateValue(activity.baselineStartDate)) : "—"}</td><td className="px-3 py-2 tabular-nums">{varianceValue(activity.startVarianceDays, locale, t)}</td></tr>
                  <tr><th scope="row" className="px-3 py-2 text-start font-medium">{t("projects.planning.currentFinish")}</th><td className="px-3 py-2">{activity.endDate ? formatDate(dateValue(activity.endDate)) : "—"}</td><td className="px-3 py-2">{activity.baselineFinishDate ? formatDate(dateValue(activity.baselineFinishDate)) : "—"}</td><td className="px-3 py-2 tabular-nums">{varianceValue(activity.finishVarianceDays, locale, t)}</td></tr>
                  <tr><th scope="row" className="px-3 py-2 text-start font-medium">{t("projects.planning.currentDuration")}</th><td className="px-3 py-2 tabular-nums">{formatNumberValue(activity.duration, locale)} {t("projects.days")}</td><td className="px-3 py-2 tabular-nums">{activity.baselineDuration == null ? "—" : formatNumberValue(activity.baselineDuration, locale) + " " + t("projects.days")}</td><td className="px-3 py-2 tabular-nums">{varianceValue(activity.durationVarianceDays, locale, t)}</td></tr>
                </tbody>
              </table></div>{!activity.baselineStartDate && !activity.baselineFinishDate && activity.baselineDuration == null ? <p className="text-xs text-muted-foreground" role="status">{t("projects.planning.baselineUnavailable")}</p> : null}</PlanningDetailSection>
              <PlanningDetailSection title={t("projects.planning.detailSections.analysis")}><PlanningDetailMetrics className="sm:grid-cols-3"><PlanningDetailMetric label={t("projects.planning.totalSlack")} value={activity.totalSlack == null ? "—" : formatNumberValue(activity.totalSlack, locale)} /><PlanningDetailMetric label={t("projects.planning.freeSlack")} value={activity.freeSlack == null ? "—" : formatNumberValue(activity.freeSlack, locale)} /><PlanningDetailMetric label={t("projects.planning.critical")} value={activity.critical ? t("common.yes") : t("common.no")} /></PlanningDetailMetrics></PlanningDetailSection>
            </div> : null}
            {tab === "formatting" ? <PlanningDetailSection title={t("projects.planning.formatting")} description={t("projects.planning.formattingSaveHint")}>
              <div className="flex flex-wrap gap-2"><Button type="button" size="sm" variant={format?.isBold ? "secondary" : "outline"} aria-pressed={Boolean(format?.isBold)} disabled={!editable || saving} onClick={() => void saveFormat(nextFormat({ isBold: !format?.isBold }))}>{t("projects.planning.formatBold")}</Button><Button type="button" size="sm" variant={format?.isItalic ? "secondary" : "outline"} aria-pressed={Boolean(format?.isItalic)} disabled={!editable || saving} onClick={() => void saveFormat(nextFormat({ isItalic: !format?.isItalic }))}>{t("projects.planning.formatItalic")}</Button><Button type="button" size="sm" variant={format?.isUnderline ? "secondary" : "outline"} aria-pressed={Boolean(format?.isUnderline)} disabled={!editable || saving} onClick={() => void saveFormat(nextFormat({ isUnderline: !format?.isUnderline }))}>{t("projects.planning.formatUnderline")}</Button></div>
              <div className="grid gap-4 sm:grid-cols-2">
                <PlanningDetailField id="planning-format-text-color" label={t("projects.planning.formatTextColor")}><select id="planning-format-text-color" value={format?.textColorToken ?? ""} disabled={!editable || saving} onChange={(event) => void saveFormat(nextFormat({ textColorToken: event.target.value || undefined }))} className="input-field"><option value="">{t("projects.planning.formatNone")}</option>{["Slate", "Blue", "Teal", "Green", "Amber", "Red", "Violet"].map((value) => <option key={value} value={value}>{t("projects.planning.formatColors." + value)}</option>)}</select></PlanningDetailField>
                <PlanningDetailField id="planning-format-highlight" label={t("projects.planning.formatHighlight")}><select id="planning-format-highlight" value={format?.highlightColorToken ?? ""} disabled={!editable || saving} onChange={(event) => void saveFormat(nextFormat({ highlightColorToken: event.target.value || undefined }))} className="input-field"><option value="">{t("projects.planning.formatNone")}</option>{["Yellow", "Blue", "Green", "Orange", "Pink"].map((value) => <option key={value} value={value}>{t("projects.planning.formatHighlights." + value)}</option>)}</select></PlanningDetailField>
                <PlanningDetailField id="planning-format-font-size" label={t("projects.planning.formatFontSize")}><select id="planning-format-font-size" value={format?.fontSizeToken ?? ""} disabled={!editable || saving} onChange={(event) => void saveFormat(nextFormat({ fontSizeToken: event.target.value || undefined }))} className="input-field"><option value="">{t("projects.planning.formatNone")}</option>{["Small", "Normal", "Large", "ExtraLarge"].map((value) => <option key={value} value={value}>{t("projects.planning.formatSizes." + value)}</option>)}</select></PlanningDetailField>
              </div><div className="border-t border-border/70 pt-3"><Button type="button" variant="ghost" size="sm" disabled={!editable || saving || !format} onClick={() => void saveFormat(null)}>{t("projects.planning.clearFormatting")}</Button></div>
            </PlanningDetailSection> : null}
          </div>
        </SideSheetBody>
        <SideSheetFooter className="items-center"><Button type="button" variant="outline" disabled={saving} onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>{!editable ? <span className="me-auto text-xs text-muted-foreground">{t("projects.planning.readOnly")}</span> : <Button type="submit" disabled={saving || mutations.updateActivity.isPending}><Save className="me-1.5 h-4 w-4" />{saving ? t("projects.planning.saving") : t("common.save")}</Button>}</SideSheetFooter>
      </form>
    </SideSheet>
  );
}
