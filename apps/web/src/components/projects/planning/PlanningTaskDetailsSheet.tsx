"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { usePlanningAssignments, usePlanningCalendars, usePlanningResources, usePlanningSchedulingExplanation, type PlanActivityDto, type PlanAssignmentDto, type PlanDependencyDto, type PlanConstraintType, type PlanSchedulingMode, type PlanTextFormatDto } from "@/lib/api/planning";
import { PLANNING_ACTIVITY_TYPE } from "@/lib/enums";
import { isPlanFontSizeToken, isPlanHighlightColorToken, isPlanTextColorToken, PLAN_FONT_SIZE_TOKENS, PLAN_HIGHLIGHT_COLOR_TOKENS, PLAN_TEXT_COLOR_TOKENS } from "./plan-text-format";
import type { PlanWorkspaceRow } from "./plan-workspace-rows";

type DetailTab = "general" | "schedule" | "dependencies" | "resources" | "analysis" | "formatting";

export type ActivityDetailDraft = {
  name: string;
  description: string;
  duration: number;
  startDate: string;
  endDate: string;
  actualStart: string;
  actualFinish: string;
  progress: number | null;
  schedulingMode: PlanSchedulingMode;
  constraintType: PlanConstraintType;
  constraintDate: string;
  deadline: string;
  estimateHours: number | null;
  calendarId: string | null;
};

export function PlanningTaskDetailsSheet({ projectId, planId, row, activities, dependencies, editable, open, onOpenChange, predecessorText, onSaveActivity, onSavePredecessors, onSaveTextFormat }: {
  projectId: string;
  planId: string;
  row?: PlanWorkspaceRow;
  activities: PlanActivityDto[];
  dependencies: PlanDependencyDto[];
  editable: boolean;
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  predecessorText: string;
  onSaveActivity: (_activity: PlanActivityDto, _patch: ActivityDetailDraft) => Promise<void>;
  onSavePredecessors: (_activity: PlanActivityDto, _value: string) => Promise<void>;
  onSaveTextFormat: (_format: PlanTextFormatDto | null) => Promise<void>;
}) {
  const t = useTranslations("projects.planning");
  const common = useTranslations("common");
  const selectedActivity = row?.activity;
  const [tab, setTab] = useState<DetailTab>("general");
  const [draft, setDraft] = useState<ActivityDetailDraft | null>(null);
  const [predecessors, setPredecessors] = useState(predecessorText);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const initializedActivityRef = useRef<string | null>(null);

  // Secondary data is lazy: the workspace already supplies activities and dependencies.
  const assignmentsQuery = usePlanningAssignments(projectId, planId, open && Boolean(selectedActivity) && tab === "resources");
  const resourcesQuery = usePlanningResources(open && Boolean(selectedActivity) && tab === "resources");
  const calendarsQuery = usePlanningCalendars(projectId, planId, open && Boolean(selectedActivity) && tab === "schedule");
  const explanationQuery = usePlanningSchedulingExplanation(projectId, planId, selectedActivity?.id, open && Boolean(selectedActivity) && tab === "schedule");

  useEffect(() => {
    if (!open) {
      initializedActivityRef.current = null;
      return;
    }
    if (!selectedActivity || initializedActivityRef.current === selectedActivity.id) return;
    initializedActivityRef.current = selectedActivity.id;
    setTab("general");
    setDraft({
      name: selectedActivity.name,
      description: selectedActivity.description ?? "",
      duration: selectedActivity.duration,
      startDate: selectedActivity.startDate?.slice(0, 10) ?? "",
      endDate: selectedActivity.endDate?.slice(0, 10) ?? "",
      actualStart: selectedActivity.actualStart?.slice(0, 10) ?? "",
      actualFinish: selectedActivity.actualFinish?.slice(0, 10) ?? "",
      progress: selectedActivity.progress ?? null,
      schedulingMode: selectedActivity.schedulingMode,
      constraintType: selectedActivity.constraintType ?? "AsSoonAsPossible",
      constraintDate: selectedActivity.constraintDate?.slice(0, 10) ?? "",
      deadline: selectedActivity.deadline?.slice(0, 10) ?? "",
      estimateHours: selectedActivity.estimateHours ?? null,
      calendarId: selectedActivity.calendarId ?? null,
    });
    setError(null);
  }, [selectedActivity, open]);

  useEffect(() => {
    if (open) setPredecessors(predecessorText);
  }, [open, predecessorText]);

  const activity = selectedActivity;
  const resourceNames = useMemo(() => new Map((resourcesQuery.data ?? []).map((resource) => [resource.id, resource.name])), [resourcesQuery.data]);
  const calendarNames = useMemo(() => new Map((calendarsQuery.data ?? []).map((calendar) => [calendar.id, calendar.name])), [calendarsQuery.data]);
  const assignments = useMemo(() => (assignmentsQuery.data ?? []).filter((assignment) => assignment.activityId === activity?.id), [activity?.id, assignmentsQuery.data]);
  const predecessorLinks = useMemo(() => dependencies.filter((dependency) => dependency.activityId === activity?.id), [activity?.id, dependencies]);
  const successorLinks = useMemo(() => dependencies.filter((dependency) => dependency.predecessorActivityId === activity?.id), [activity?.id, dependencies]);
  const activityById = useMemo(() => new Map(activities.map((item) => [item.id, item])), [activities]);

  if (!activity || !row) return null;
  const readOnly = !editable;
  const summaryTask = activity.isSummaryTask;
  const format = activity.textFormat;
  const dependencyType = (value: PlanDependencyDto["type"]) => t(`dependencyTypes.${value}`);
  const save = async () => {
    if (!draft || !editable || saving) return;
    setSaving(true);
    setError(null);
    try {
      await onSaveActivity(activity, draft);
      if (predecessors.trim() !== predecessorText.trim()) await onSavePredecessors(activity, predecessors);
      onOpenChange(false);
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : t("saveFailed"));
    } finally {
      setSaving(false);
    }
  };

  return <Sheet open={open} onOpenChange={onOpenChange}>
    <SheetContent side="end" className="w-full overflow-y-auto sm:max-w-2xl" data-testid="planning-task-details">
      <SheetHeader>
        <div className="flex items-center gap-2"><SheetTitle>{t("taskDetails")}</SheetTitle><Badge status={readOnly ? "ReadOnly" : "Active"}>{readOnly ? t("readOnly") : t("editable")}</Badge></div>
        <SheetDescription>{t("taskDetailsDescription")}</SheetDescription>
      </SheetHeader>
      {error && <p role="alert" className="mt-4 rounded border border-destructive/30 bg-destructive/5 px-3 py-2 text-xs text-destructive">{error}</p>}
      <Tabs value={tab} onValueChange={(value) => setTab(value as DetailTab)} className="mt-4">
        <div className="sticky top-0 z-10 -mx-6 border-y bg-background/95 px-6 py-2 backdrop-blur supports-[backdrop-filter]:bg-background/80">
          <TabsList className="flex h-8 w-full justify-start gap-0.5 overflow-x-auto rounded-md bg-muted/70 p-0.5">
            {(["general", "schedule", "dependencies", "resources", "analysis", "formatting"] as DetailTab[]).map((value) => <TabsTrigger key={value} value={value} className="shrink-0 px-2 py-1 text-[11px]">{t(`detailTabs.${value}`)}</TabsTrigger>)}
          </TabsList>
        </div>

        <TabsContent value="general" className="space-y-4">
          <Section title={t("detailSections.identity")}>
            <EditableField label={t("taskName")} className="sm:col-span-2"><Input value={draft?.name ?? activity.name} disabled={readOnly || saving} onChange={(event) => setDraft((current) => current ? { ...current, name: event.target.value } : current)} /></EditableField>
            <ReadOnlyValue label={t("activityType")} value={activity.type === PLANNING_ACTIVITY_TYPE.milestone ? t("milestone") : t("task")} />
            <ReadOnlyValue label={t("code")} value={activity.code} />
            <EditableField label={t("description")} className="sm:col-span-2"><Textarea value={draft?.description ?? activity.description ?? ""} disabled={readOnly || saving} onChange={(event) => setDraft((current) => current ? { ...current, description: event.target.value } : current)} className="min-h-20" /></EditableField>
          </Section>
          <Section title={t("detailSections.context")}>
            <ReadOnlyValue label={t("wbsContext")} value={`${row.wbs?.code ?? t("none")} · ${row.wbs?.name ?? t("none")}`} />
            <ReadOnlyValue label={t("wbsId")} value={row.wbs?.id ?? t("none")} />
            <ReadOnlyValue label={t("parentWbsId")} value={row.wbs?.parentWbsId ?? t("none")} />
            <ReadOnlyValue label={t("deliverableId")} value={row.wbs?.deliverableId ?? t("none")} />
            <ReadOnlyValue label={t("hierarchyLevel")} value={String(row.depth + 1)} />
            <ReadOnlyValue label={t("rowNumber")} value={String(row.rowNumber)} />
            <ReadOnlyValue label={t("sortOrder")} value={String(activity.sortOrder)} />
            <ReadOnlyValue label={t("planVersionId")} value={activity.planVersionId} className="sm:col-span-2" />
          </Section>
          <Section title={t("detailSections.progress")}>
            <EditableField label={t("schedulingMode")}><select value={draft?.schedulingMode ?? activity.schedulingMode} disabled={readOnly || saving} onChange={(event) => setDraft((current) => current ? { ...current, schedulingMode: event.target.value as PlanSchedulingMode } : current)} className="h-10 w-full rounded-md border bg-background px-3 text-sm"><option value="Automatic">{t("automatic")}</option><option value="Manual">{t("manual")}</option></select></EditableField>
             {summaryTask ? <ReadOnlyValue label={t("complete")} value={display(activity.progress)} /> : <EditableField label={t("complete")}><Input type="number" min={0} max={100} value={draft?.progress == null ? "" : draft.progress} disabled={readOnly || saving} onChange={(event) => setDraft((current) => current ? { ...current, progress: event.target.value === "" ? null : Number(event.target.value) } : current)} /></EditableField>}
             {summaryTask && <ReadOnlyNotice text={t("calculatedFromSubtasks")} />}
          </Section>
        </TabsContent>

        <TabsContent value="schedule" className="space-y-4">
          <Section title={t("detailSections.schedule")}>
             {summaryTask ? <ReadOnlyValue label={t("durationShort")} value={display(activity.duration)} /> : <EditableField label={t("durationShort")}><Input type="number" min={0} value={draft?.duration ?? activity.duration} disabled={readOnly || saving || activity.type === PLANNING_ACTIVITY_TYPE.milestone} onChange={(event) => setDraft((current) => current ? { ...current, duration: Math.max(0, Number(event.target.value)) } : current)} /></EditableField>}
             {summaryTask ? <ReadOnlyValue label={t("start")} value={dateValue(activity.startDate)} /> : <EditableField label={t("start")}><Input type="date" value={draft?.startDate ?? ""} disabled={readOnly || saving} onChange={(event) => setDraft((current) => current ? { ...current, startDate: event.target.value } : current)} /></EditableField>}
             {summaryTask ? <ReadOnlyValue label={t("finish")} value={dateValue(activity.endDate)} /> : <EditableField label={t("finish")}><Input type="date" value={draft?.endDate ?? ""} disabled={readOnly || saving} onChange={(event) => setDraft((current) => current ? { ...current, endDate: event.target.value } : current)} /></EditableField>}
            <EditableField label={t("actualStart")}><Input type="date" value={draft?.actualStart ?? ""} disabled={readOnly || saving} onChange={(event) => setDraft((current) => current ? { ...current, actualStart: event.target.value } : current)} /></EditableField>
            <EditableField label={t("actualFinish")}><Input type="date" value={draft?.actualFinish ?? ""} disabled={readOnly || saving} onChange={(event) => setDraft((current) => current ? { ...current, actualFinish: event.target.value } : current)} /></EditableField>
            <EditableField label={t("constraintType")}><select value={draft?.constraintType ?? activity.constraintType} disabled={readOnly || saving} onChange={(event) => setDraft((current) => current ? { ...current, constraintType: event.target.value as PlanConstraintType } : current)} className="h-10 w-full rounded-md border bg-background px-3 text-sm">{(["AsSoonAsPossible", "AsLateAsPossible", "MustStartOn", "MustFinishOn", "StartNoEarlierThan", "StartNoLaterThan", "FinishNoEarlierThan", "FinishNoLaterThan"] as PlanConstraintType[]).map((value) => <option key={value} value={value}>{t(`constraintTypes.${value}`)}</option>)}</select></EditableField>
            <EditableField label={t("constraintDate")}><Input type="date" value={draft?.constraintDate ?? ""} disabled={readOnly || saving || draft?.constraintType === "AsSoonAsPossible" || draft?.constraintType === "AsLateAsPossible"} onChange={(event) => setDraft((current) => current ? { ...current, constraintDate: event.target.value } : current)} /></EditableField>
            <EditableField label={t("deadline")}><Input type="date" value={draft?.deadline ?? ""} disabled={readOnly || saving} onChange={(event) => setDraft((current) => current ? { ...current, deadline: event.target.value } : current)} /></EditableField>
            <EditableField label={t("calendar")}><select value={draft?.calendarId ?? ""} disabled={readOnly || saving || calendarsQuery.isLoading} onChange={(event) => setDraft((current) => current ? { ...current, calendarId: event.target.value || null } : current)} className="h-10 w-full rounded-md border bg-background px-3 text-sm"><option value="">{t("planDefaultCalendar")}</option>{(calendarsQuery.data ?? []).map((calendar) => <option key={calendar.id} value={calendar.id}>{calendar.name}{calendar.isDefault ? ` · ${t("default")}` : ""}</option>)}</select></EditableField>
          </Section>
          {activity.deadline && <ReadOnlyNotice label={t("deadlineStatus")} text={activity.deadlineExceeded ? t("deadlineExceeded") : t("deadlineOnTrack")} />}
          <ReadOnlyNotice label={t("effectiveCalendar")} text={calendarNames.get(activity.calendarId ?? "") ?? (activity.calendarId ? activity.calendarId : t("planDefaultCalendar"))} />
          <SchedulingExplanationPanel explanation={explanationQuery.data} activityById={activityById} calendarNames={calendarNames} t={t} common={common} loading={explanationQuery.isLoading} />
        </TabsContent>

        <TabsContent value="dependencies" className="space-y-4">
          <Section title={t("detailSections.dependencies")}>
            <EditableField label={t("predecessors")} className="sm:col-span-2"><Input value={predecessors} placeholder={t("predecessorPlaceholder")} disabled={readOnly || saving} onChange={(event) => setPredecessors(event.target.value)} /></EditableField>
            <ReadOnlyValue label={t("predecessorCount")} value={String(predecessorLinks.length)} />
            <ReadOnlyValue label={t("successorCount")} value={String(successorLinks.length)} />
          </Section>
          <DependencyList title={t("predecessors")} empty={t("noPredecessors")} links={predecessorLinks} side="predecessor" activityById={activityById} dependencyType={dependencyType} />
          <DependencyList title={t("successors")} empty={t("noSuccessors")} links={successorLinks} side="successor" activityById={activityById} dependencyType={dependencyType} />
        </TabsContent>

        <TabsContent value="resources" className="space-y-4">
          <Section title={t("detailSections.resources")}>
            <EditableField label={t("estimateHours")}><Input type="number" min={0} value={draft?.estimateHours == null ? "" : draft.estimateHours} disabled={readOnly || saving} onChange={(event) => setDraft((current) => current ? { ...current, estimateHours: event.target.value === "" ? null : Number(event.target.value) } : current)} /></EditableField>
            <ReadOnlyValue label={t("assignmentCount")} value={String(assignments.length)} />
          </Section>
          {assignmentsQuery.isLoading ? <ReadOnlyNotice text={t("loadingDetails")} /> : assignments.length === 0 ? <ReadOnlyNotice text={t("noResourceAssignments")} /> : <div className="space-y-2">{assignments.map((assignment) => <AssignmentRow key={assignment.id} assignment={assignment} name={resourceNames.get(assignment.resourceId) ?? assignment.resourceId} t={t} />)}</div>}
        </TabsContent>

        <TabsContent value="analysis" className="space-y-4">
          <Section title={t("detailSections.baseline")} readOnly>
            <ReadOnlyValue label={t("currentStart")} value={dateValue(activity.startDate)} />
            <ReadOnlyValue label={t("currentFinish")} value={dateValue(activity.endDate)} />
            <ReadOnlyValue label={t("currentDuration")} value={display(activity.duration)} />
            <ReadOnlyValue label={t("baselineStart")} value={dateValue(activity.baselineStartDate)} />
            <ReadOnlyValue label={t("baselineFinish")} value={dateValue(activity.baselineFinishDate)} />
            <ReadOnlyValue label={t("baselineDuration")} value={display(activity.baselineDuration)} />
            <ReadOnlyValue label={t("startVariance")} value={formatVariance(activity.startVarianceDays)} />
            <ReadOnlyValue label={t("finishVariance")} value={formatVariance(activity.finishVarianceDays)} />
            <ReadOnlyValue label={t("durationVariance")} value={formatVariance(activity.durationVarianceDays)} />
          </Section>
          <Section title={t("detailSections.analysis")} readOnly>
            <ReadOnlyValue label={t("totalSlack")} value={display(activity.totalSlack)} />
            <ReadOnlyValue label={t("freeSlack")} value={display(activity.freeSlack)} />
            <ReadOnlyValue label={t("critical")} value={activity.critical ? common("yes") : common("no")} />
          </Section>
        </TabsContent>

        <TabsContent value="formatting" className="space-y-4">
          <Section title={t("formatting")}>
            <div className="flex flex-wrap gap-2 sm:col-span-2">
              <Button type="button" size="sm" variant={format?.isBold ? "secondary" : "outline"} disabled={readOnly || saving} onClick={() => void onSaveTextFormat(nextFormat(format, { isBold: !format?.isBold }))}>{t("formatBold")}</Button>
              <Button type="button" size="sm" variant={format?.isItalic ? "secondary" : "outline"} disabled={readOnly || saving} onClick={() => void onSaveTextFormat(nextFormat(format, { isItalic: !format?.isItalic }))}>{t("formatItalic")}</Button>
              <Button type="button" size="sm" variant={format?.isUnderline ? "secondary" : "outline"} disabled={readOnly || saving} onClick={() => void onSaveTextFormat(nextFormat(format, { isUnderline: !format?.isUnderline }))}>{t("formatUnderline")}</Button>
            </div>
            <EditableField label={t("formatTextColor")}><select value={format?.textColorToken ?? ""} disabled={readOnly || saving} onChange={(event) => void onSaveTextFormat(nextFormat(format, { textColorToken: isPlanTextColorToken(event.target.value) ? event.target.value : undefined }))} className="h-10 w-full rounded-md border bg-background px-3 text-sm"><option value="">{t("formatNone")}</option>{PLAN_TEXT_COLOR_TOKENS.map((value) => <option key={value} value={value}>{t(`formatColors.${value}`)}</option>)}</select></EditableField>
            <EditableField label={t("formatHighlight")}><select value={format?.highlightColorToken ?? ""} disabled={readOnly || saving} onChange={(event) => void onSaveTextFormat(nextFormat(format, { highlightColorToken: isPlanHighlightColorToken(event.target.value) ? event.target.value : undefined }))} className="h-10 w-full rounded-md border bg-background px-3 text-sm"><option value="">{t("formatNone")}</option>{PLAN_HIGHLIGHT_COLOR_TOKENS.map((value) => <option key={value} value={value}>{t(`formatHighlights.${value}`)}</option>)}</select></EditableField>
            <EditableField label={t("formatFontSize")}><select value={format?.fontSizeToken ?? ""} disabled={readOnly || saving} onChange={(event) => void onSaveTextFormat(nextFormat(format, { fontSizeToken: isPlanFontSizeToken(event.target.value) ? event.target.value : undefined }))} className="h-10 w-full rounded-md border bg-background px-3 text-sm"><option value="">{t("formatNone")}</option>{PLAN_FONT_SIZE_TOKENS.map((value) => <option key={value} value={value}>{value}</option>)}</select></EditableField>
            <div className="sm:col-span-2"><Button type="button" variant="ghost" size="sm" disabled={readOnly || saving || !format} onClick={() => void onSaveTextFormat(null)}>{t("clearFormatting")}</Button></div>
          </Section>
        </TabsContent>
      </Tabs>
      {editable && <div className="sticky bottom-0 z-10 -mx-6 mt-4 border-t bg-background/95 px-6 py-3 backdrop-blur supports-[backdrop-filter]:bg-background/80"><Button type="button" onClick={() => void save()} disabled={saving || !draft}>{saving ? t("saving") : t("save")}</Button></div>}
    </SheetContent>
  </Sheet>;
}

function SchedulingExplanationPanel({ explanation, activityById, calendarNames, t, common, loading }: { explanation?: import("@/lib/api/planning").PlanSchedulingExplanationDto; activityById: Map<string, PlanActivityDto>; calendarNames: Map<string, string>; t: ReturnType<typeof useTranslations>; common: ReturnType<typeof useTranslations>; loading: boolean }) {
  if (loading) return <ReadOnlyNotice label={t("schedulingExplanation")} text={t("loadingDetails")} />;
  if (!explanation) return null;
  const reasonText = (reason: (typeof explanation.reasons)[number]) => {
    const related = reason.relatedActivityId ? activityById.get(reason.relatedActivityId)?.code ?? reason.relatedActivityId : undefined;
    if (reason.code === "MovedByPredecessor" && related) return `${t("schedulingReasons.MovedByPredecessor")} · ${related}`;
    if (reason.code === "DeadlineExceeded" && reason.workingDays) return `${t("schedulingReasons.DeadlineExceeded")} · ${reason.workingDays}d`;
    return t(`schedulingReasons.${reason.code}` as never);
  };
  return <Section title={t("schedulingExplanation")} readOnly>
    <ReadOnlyValue label={t("schedulingMode")} value={t(explanation.schedulingMode === "Manual" ? "manual" : "automatic")} />
    <ReadOnlyValue label={t("effectiveCalendar")} value={explanation.effectiveCalendarId ? (calendarNames.get(explanation.effectiveCalendarId) ?? explanation.effectiveCalendarId) : t("planDefaultCalendar")} />
    <ReadOnlyValue label={t("calculatedStart")} value={dateValue(explanation.start)} />
    <ReadOnlyValue label={t("calculatedFinish")} value={dateValue(explanation.finish)} />
    <ReadOnlyValue label={t("critical")} value={explanation.critical ? common("yes") : common("no")} />
    <ReadOnlyValue label={t("totalSlack")} value={display(explanation.totalSlack)} />
    <div className="sm:col-span-2"><p className="mb-1 text-xs font-medium text-muted-foreground">{t("schedulingReasonsTitle")}</p><div className="flex flex-wrap gap-1.5">{explanation.reasons.map((reason, index) => <Badge key={`${reason.code}-${index}`} status={reason.code === "SchedulingConflict" || reason.code === "DeadlineExceeded" ? "Warning" : "Info"}>{reasonText(reason)}</Badge>)}</div></div>
    {explanation.issues.length > 0 && <div className="sm:col-span-2 space-y-1 rounded-md border border-destructive/30 bg-destructive/5 p-2 text-xs text-destructive">{explanation.issues.map((issue) => <p key={`${issue.code}-${issue.activityId}`}>{issue.message}</p>)}</div>}
  </Section>;
}

function nextFormat(format: PlanTextFormatDto | null | undefined, patch: Partial<PlanTextFormatDto>): PlanTextFormatDto {
  return { isBold: format?.isBold ?? false, isItalic: format?.isItalic ?? false, isUnderline: format?.isUnderline ?? false, ...format, ...patch };
}

function dateValue(value?: string | null) { return value?.slice(0, 10) ?? "—"; }
function display(value?: string | number | null) { return value === undefined || value === null || value === "" ? "—" : String(value); }
function formatVariance(value?: number | null) { return value === undefined || value === null ? "—" : `${value > 0 ? "+" : ""}${value} ${value === 1 || value === -1 ? "day" : "days"}`; }
function formatLag(value?: number) { return value === undefined || value === 0 ? "0d" : `${value > 0 ? "+" : ""}${value}d`; }

function Section({ title, children, readOnly = false }: { title: string; children: React.ReactNode; readOnly?: boolean }) {
  return <section className={`rounded-lg border p-3 ${readOnly ? "border-border/60 bg-muted/20" : "bg-background"}`}><h3 className="mb-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{title}</h3><div className="grid gap-3 sm:grid-cols-2">{children}</div></section>;
}

function EditableField({ label, children, className = "" }: { label: string; children: React.ReactNode; className?: string }) {
  return <div className={`space-y-1.5 ${className}`}><label className="text-xs font-medium">{label}</label>{children}</div>;
}

function ReadOnlyValue({ label, value, className = "" }: { label: string; value: string; className?: string }) {
  return <div className={`space-y-1.5 ${className}`}><p className="text-xs font-medium text-muted-foreground">{label}</p><p className="min-h-10 truncate rounded-md border border-border/60 bg-muted/30 px-3 py-2 text-sm tabular-nums" title={value}>{value || "—"}</p></div>;
}

function ReadOnlyNotice({ label, text }: { label?: string; text: string }) {
  return <div className="rounded-md border border-border/60 bg-muted/20 px-3 py-2 text-xs text-muted-foreground">{label && <span className="me-2 font-medium">{label}:</span>}{text}</div>;
}

function DependencyList({ title, empty, links, side, activityById, dependencyType }: { title: string; empty: string; links: PlanDependencyDto[]; side: "predecessor" | "successor"; activityById: Map<string, PlanActivityDto>; dependencyType: (_type: PlanDependencyDto["type"]) => string }) {
  return <section className="space-y-2"><h3 className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{title}</h3>{links.length === 0 ? <ReadOnlyNotice text={empty} /> : <div className="space-y-2">{links.map((link) => { const related = activityById.get(side === "predecessor" ? link.predecessorActivityId : link.activityId); return <div key={link.id} className="grid gap-2 rounded-md border border-border/60 bg-muted/20 p-3 text-xs sm:grid-cols-[minmax(0,1fr)_auto_auto]"><div className="min-w-0"><p className="truncate font-medium">{related?.code ?? related?.name ?? "—"}</p><p className="truncate text-muted-foreground">{related?.name ?? link[side === "predecessor" ? "predecessorActivityId" : "activityId"]}</p></div><span className="rounded bg-background px-2 py-1 text-muted-foreground">{dependencyType(link.type)}</span><span className="rounded bg-background px-2 py-1 tabular-nums text-muted-foreground">{formatLag(link.lag)}</span></div>; })}</div>}</section>;
}

function AssignmentRow({ assignment, name, t }: { assignment: PlanAssignmentDto; name: string; t: ReturnType<typeof useTranslations> }) {
  return <div className="rounded-md border border-border/60 bg-muted/20 p-3 text-xs"><div className="flex items-center justify-between gap-2"><span className="truncate font-medium">{name}</span><span className="text-muted-foreground">{assignment.allocationPct == null ? "—" : `${assignment.allocationPct}%`}</span></div><div className="mt-2 grid gap-1 text-muted-foreground sm:grid-cols-3"><span>{t("hoursPerWeek")}: {display(assignment.hoursPerWeek)}</span><span>{t("assignmentStart")}: {dateValue(assignment.startDate)}</span><span>{t("assignmentFinish")}: {dateValue(assignment.endDate)}</span></div></div>;
}
