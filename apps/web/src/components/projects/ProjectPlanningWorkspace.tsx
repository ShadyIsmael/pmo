"use client";

import dynamic from "next/dynamic";
import { useEffect, useMemo, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { CalendarDays, ChevronRight, Edit3, Eye, FileUp, GitCompareArrows, Plus, Save, Trash2, X } from "lucide-react";
import { useToast } from "@/hooks/use-toast";
import {
  planningApi,
  planningKeys,
  useCreatePlanningPlan,
  usePlanningActivities,
  usePlanningAssignments,
  usePlanningCalendars,
  usePlanningDependencies,
  usePlanningMutation,
  useUpdatePlanningCalendar,
  usePlanningPlan,
  usePlanningBootstrap,
  usePlanningResources,
  usePlanningWbs,
  usePlanningWorkingVersion,
  type PlanActivityDto,
  type CalendarDayRuleDto,
  type CalendarHolidayDto,
  type PlanDto,
  type PlanType,
} from "@/lib/api/planning";
import { PLANNING_PLAN_TYPES, PLANNING_VERSION_STATUS, PLANNING_VERSION_TYPE, planningEnumLabelKey as planningLabelKey } from "@/lib/enums";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { PlanningGridEditor, type PlanningView } from "./planning/PlanningGridEditor";
import { GANTT_ZOOM_LEVELS, type GanttZoom } from "./planning/gantt-constants";
import { PlanningRibbonGroup, PlanningRibbonTabs, type PlanningRibbonTab } from "./planning/PlanningRibbonGroup";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";

const LazyPlanImportDialog = dynamic(
  () => import("./planning/PlanImportDialog").then((module) => module.PlanImportDialog),
  { ssr: false },
);

type PlanningPanel = "assignments" | "calendar" | "versions" | "baseline";
type AssignmentDraft = { activityId: string; resourceId: string; allocationPct: number | undefined; hoursPerWeek: number | undefined; startDate: string; endDate: string };

function dateInput(value?: string) { return value ? value.slice(0, 10) : ""; }
function nullableDate(value: string) { return value ? value : undefined; }
function errorMessage(error: unknown, fallback: string) { return error instanceof Error ? error.message : fallback; }

export function ProjectPlanningWorkspace({ projectId }: { projectId: string }) {
  const t = useTranslations("projects");
  const common = useTranslations("common");
  const enums = useTranslations("enums");
  const { toast } = useToast();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const mutationLock = useRef(false);
  const [panel, setPanel] = useState<PlanningPanel | null>(null);
  const [selectedPlanId, setSelectedPlanId] = useState("");
  const [selectedCalendarId, setSelectedCalendarId] = useState("");
  const [createPlanOpen, setCreatePlanOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [view, setView] = useState<PlanningView>("table");
  const [ganttZoom, setGanttZoom] = useState<GanttZoom>("day");
  const [showBaseline, setShowBaseline] = useState(false);
  const [ribbonTab, setRibbonTab] = useState<PlanningRibbonTab>("project");

  const bootstrapQuery = usePlanningBootstrap(projectId);
  const createPlan = useCreatePlanningPlan(projectId);
  const plans = useMemo(() => bootstrapQuery.data?.plans ?? [], [bootstrapQuery.data?.plans]);
  const planId = selectedPlanId || bootstrapQuery.data?.selectedPlan?.id || plans[0]?.id || "";
  const selectedPlan = plans.find((plan) => plan.id === planId) ?? (planId === bootstrapQuery.data?.selectedPlan?.id ? bootstrapQuery.data.selectedPlan : undefined);
  const planQuery = usePlanningPlan(projectId, planId);
  const workingQuery = usePlanningWorkingVersion(projectId, planId);
  const workingVersion = workingQuery.data ?? (planId === bootstrapQuery.data?.selectedPlan?.id ? bootstrapQuery.data.workingVersion : undefined);
  const currentBaseline = planQuery.data?.versions.filter((version) => version.type === PLANNING_VERSION_TYPE.baseline && version.status === PLANNING_VERSION_STATUS.approved).sort((left, right) => right.versionNumber - left.versionNumber)[0];
  const canManagePlanning = Boolean(user && hasPermission(user.permissions, Permissions.Planning.Manage));
  const canManagePlanningLifecycle = Boolean(user && hasPermission(user.permissions, Permissions.Planning.Lifecycle));
  const editable = !!workingVersion?.id && canManagePlanning;

  const wbsQuery = usePlanningWbs(projectId, planId, Boolean(planId));
  const activitiesQuery = usePlanningActivities(projectId, planId, Boolean(planId));
  const dependenciesQuery = usePlanningDependencies(projectId, planId, Boolean(planId));
  const assignmentsQuery = usePlanningAssignments(projectId, planId, panel === "assignments");
  const calendarsQuery = usePlanningCalendars(projectId, planId, panel === "calendar");
  const resourcesQuery = usePlanningResources(panel === "assignments");

  useEffect(() => {
    if (!selectedPlanId && plans[0]) setSelectedPlanId(plans[0].id);
    if (selectedPlanId && plans.length > 0 && !plans.some((plan) => plan.id === selectedPlanId)) setSelectedPlanId(plans[0]?.id ?? "");
  }, [plans, selectedPlanId]);

  useEffect(() => {
    const firstCalendar = calendarsQuery.data?.[0];
    if (firstCalendar && (!selectedCalendarId || !calendarsQuery.data?.some((calendar) => calendar.id === selectedCalendarId))) setSelectedCalendarId(firstCalendar.id);
  }, [calendarsQuery.data, selectedCalendarId]);

  const run = async (action: () => Promise<unknown>, success: string) => {
    if (mutationLock.current) return;
    mutationLock.current = true;
    try { await action(); toast({ title: t("planning.saved"), description: success, variant: "success" }); }
    catch (error) { toast({ title: common("error"), description: errorMessage(error, t("planning.saveFailed")), variant: "destructive" }); }
    finally { mutationLock.current = false; }
  };

  const captureBaseline = () => {
    if (!workingVersion?.rowVersion) return;
    if (currentBaseline && !window.confirm(t("planning.replaceBaselineWarning"))) return;
    void run(async () => {
      await planningApi.captureBaseline(projectId, planId, workingVersion.rowVersion);
      await Promise.all([
        queryClient.refetchQueries({ queryKey: planningKeys.plan(projectId, planId), type: "active" }),
        queryClient.refetchQueries({ queryKey: planningKeys.activities(projectId, planId), type: "active" }),
      ]);
      setPanel("baseline");
    }, currentBaseline ? t("planning.baselineCapturedAgain") : t("planning.baselineCaptured"));
  };

  const enumLabel = (group: "planType" | "versionType" | "versionStatus" | "activityType" | "dependencyType", value: string) =>
    enums(planningLabelKey(group, value));
  const zoomLabels: Record<GanttZoom, string> = { day: t("planning.zoomDay"), week: t("planning.zoomWeek"), month: t("planning.zoomMonth") };
  const ribbonTabs: Array<{ id: PlanningRibbonTab; label: string }> = [
    { id: "project", label: t("planning.ribbonTabs.project") },
    { id: "task", label: t("planning.ribbonTabs.task") },
    { id: "view", label: t("planning.ribbonTabs.view") },
    { id: "resource", label: t("planning.ribbonTabs.resource") },
    { id: "format", label: t("planning.ribbonTabs.format") },
  ];

  const handlePlanCreated = (plan: PlanDto) => {
    setSelectedPlanId(plan.id);
    setCreatePlanOpen(false);
  };
  const openCreatePlan = () => {
    setRibbonTab("project");
    setCreatePlanOpen(true);
    createPlan.reset();
  };
  const cancelCreatePlan = () => {
    createPlan.reset();
    setCreatePlanOpen(false);
  };
  const handleImported = async (importedPlanId: string) => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: planningKeys.bootstrap(projectId) }),
      queryClient.invalidateQueries({ queryKey: planningKeys.plans(projectId) }),
    ]);
    setSelectedPlanId(importedPlanId);
    setView("table");
    setRibbonTab("view");
  };

  if (bootstrapQuery.isLoading) return <Card className="shadow-none"><CardContent className="p-6"><Skeleton className="h-40 w-full" /></CardContent></Card>;
  if (bootstrapQuery.isError) return <Card className="shadow-none"><CardContent className="p-6 text-sm text-destructive">{t("planning.loadFailed")}</CardContent></Card>;
  if (plans.length === 0) return (<>
    <Card className="planning-workspace font-planning shadow-none">
      <CardContent className="p-3">
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border bg-muted/20 px-3 py-2">
          <div><p className="text-sm font-semibold">{t("planning.title")}</p><p className="mt-0.5 text-xs text-muted-foreground">{t("planning.noPlansDescription")}</p></div>
          <div className="flex gap-2">{canManagePlanning ? <Button type="button" size="sm" variant="outline" onClick={() => setImportOpen(true)}><FileUp className="mr-1.5 h-3.5 w-3.5" />{t("planning.import.action")}</Button> : null}{canManagePlanning ? <Button type="button" size="sm" onClick={openCreatePlan}>{t("planning.createPlan")}</Button> : null}</div>
        </div>
        {createPlanOpen && <div className="mt-2"><CreatePlanPanel mutation={createPlan} onCreated={handlePlanCreated} onCancel={cancelCreatePlan} /></div>}
      </CardContent>
    </Card>
    {importOpen && <LazyPlanImportDialog projectId={projectId} plans={[]} selectedPlanId="" open={importOpen} onOpenChange={setImportOpen} onImported={handleImported} />}
  </>);

  return (
    <Card className="planning-workspace font-planning shadow-none">
      <CardContent className="p-0">
        <div className="sticky top-14 z-20 border-b bg-background/95 shadow-sm backdrop-blur supports-[backdrop-filter]:bg-background/80">
          <div className="flex min-w-0 items-center gap-3 border-b bg-muted/20 px-3 py-1.5">
            <div className="min-w-0 flex-1">
              <p className="truncate text-xs font-semibold" title={selectedPlan?.name ?? t("planning.selectPlan")}>{selectedPlan?.name ?? t("planning.selectPlan")}</p>
              <p className="truncate text-[10px] text-muted-foreground">{selectedPlan ? enumLabel("planType", selectedPlan.planType) : "—"}</p>
            </div>
            <div className="flex shrink-0 flex-wrap items-center justify-end gap-1">
        <Badge className="h-5 px-1.5 text-[10px]" status={editable ? "Active" : "ReadOnly"}>{enumLabel("versionType", PLANNING_VERSION_TYPE.working)}: {workingVersion ? `${t("planning.versionShort")} ${workingVersion.versionNumber}` : t("planning.unavailable")}</Badge>
        <Badge className="h-5 px-1.5 text-[10px]" status={editable ? "Active" : "ReadOnly"}>{enumLabel("versionStatus", PLANNING_VERSION_STATUS.active)}: {editable ? t("planning.editable") : t("planning.readOnly")}</Badge>
        <Badge className="h-5 px-1.5 text-[10px]" status={currentBaseline ? "Approved" : "ReadOnly"} title={currentBaseline ? t("planning.baselineReadOnly") : undefined}>{enumLabel("versionType", PLANNING_VERSION_TYPE.baseline)}: {currentBaseline ? `${t("planning.versionShort")} ${currentBaseline.versionNumber} · ${dateInput(currentBaseline.createdAt)}` : t("planning.none")}</Badge>
            </div>
          </div>
          <div className="overflow-x-auto border-b px-2 pt-1">
            <PlanningRibbonTabs ariaLabel={t("planning.ribbonTabs.label")} activeTab={ribbonTab} tabs={ribbonTabs} onChange={setRibbonTab} />
          </div>
          {(ribbonTab === "project" || ribbonTab === "view") && <div className="min-h-[4.25rem] overflow-x-auto px-2 py-1.5">
            {ribbonTab === "project" && <div className="flex min-w-max items-stretch gap-2">
              <PlanningRibbonGroup label={t("planning.ribbon.plan")} className="min-w-[19rem]">
                <Select value={planId} onValueChange={setSelectedPlanId}>
                  <SelectTrigger aria-label={t("planning.selectPlan")} className="h-7 w-48 max-w-[13rem] shrink-0 text-[11px]"><SelectValue placeholder={t("planning.selectPlan")} /></SelectTrigger>
                  <SelectContent>{plans.map((plan) => <SelectItem key={plan.id} value={plan.id}>{plan.name} · {enumLabel("planType", plan.planType)}</SelectItem>)}</SelectContent>
                </Select>
                {canManagePlanning ? <Button type="button" variant="default" size="sm" className="h-7 shrink-0 gap-1 px-2 text-[11px]" onClick={createPlanOpen ? cancelCreatePlan : openCreatePlan}><Plus className="h-3.5 w-3.5" />{createPlanOpen ? t("planning.cancelCreatePlan") : t("planning.createPlan")}</Button> : null}
              </PlanningRibbonGroup>
              <PlanningRibbonGroup label={t("planning.ribbon.tools")}>
                {canManagePlanning ? <WorkspaceButton onClick={() => setImportOpen(true)} icon={<FileUp className="h-3.5 w-3.5" />}>{t("planning.import.action")}</WorkspaceButton> : null}
                {canManagePlanningLifecycle ? <WorkspaceButton onClick={captureBaseline} disabled={!editable || !workingVersion?.rowVersion} icon={<Save className="h-3.5 w-3.5" />}>{t("planning.setBaseline")}</WorkspaceButton> : null}
                {currentBaseline ? <WorkspaceButton onClick={() => setPanel("baseline")} icon={<Eye className="h-3.5 w-3.5" />}>{t("planning.viewBaseline")}</WorkspaceButton> : null}
                <WorkspaceButton onClick={() => setPanel("calendar")} icon={<CalendarDays className="h-3.5 w-3.5" />}>{t("planning.calendar")}</WorkspaceButton>
                <WorkspaceButton onClick={() => setPanel("versions")} icon={<ChevronRight className="h-3.5 w-3.5" />}>{t("planning.versions")}</WorkspaceButton>
              </PlanningRibbonGroup>
            </div>}
            {ribbonTab === "view" && <div className="flex min-w-max items-stretch gap-2">
              <PlanningRibbonGroup label={t("planning.ribbon.view")} className="min-w-[15rem]">
                <div className="flex items-center rounded-md border bg-background p-0.5">
                  {(["table", "gantt"] as const).map((option) => <Button key={option} type="button" size="sm" variant={view === option ? "secondary" : "ghost"} aria-pressed={view === option} onClick={() => setView(option)} className={`h-7 px-2 text-[11px] ${view === option ? "font-medium" : "font-normal text-muted-foreground"}`}>{t(`planning.views.${option}`)}</Button>)}
                </div>
                {view === "gantt" && <div className="flex items-center gap-0.5 rounded-md border bg-background p-0.5">
                  {GANTT_ZOOM_LEVELS.map((level) => <Button key={level} type="button" size="sm" variant={ganttZoom === level ? "secondary" : "ghost"} aria-pressed={ganttZoom === level} onClick={() => setGanttZoom(level)} className="h-7 px-1.5 text-[10px]">{zoomLabels[level]}</Button>)}
                </div>}
                {view === "gantt" && currentBaseline && <Button type="button" size="sm" variant={showBaseline ? "secondary" : "ghost"} aria-pressed={showBaseline} onClick={() => setShowBaseline((visible) => !visible)} className="h-7 gap-1 px-2 text-[11px]"><GitCompareArrows className="h-3.5 w-3.5" />{t(showBaseline ? "planning.hideBaseline" : "planning.showBaseline")}</Button>}
              </PlanningRibbonGroup>
            </div>}
          </div>}
          {createPlanOpen && <div className="mt-2"><CreatePlanPanel mutation={createPlan} onCreated={handlePlanCreated} onCancel={cancelCreatePlan} /></div>}
        </div>
        <PlanningGridEditor projectId={projectId} planId={planId} editable={editable} view={view} ganttZoom={ganttZoom} ribbonTab={ribbonTab} showBaseline={showBaseline} revealBaselineColumns={showBaseline} wbs={wbsQuery.data ?? []} activities={activitiesQuery.data ?? []} dependencies={dependenciesQuery.data ?? []} loading={wbsQuery.isLoading || activitiesQuery.isLoading || dependenciesQuery.isLoading} onOpenAssignments={() => setPanel("assignments")} />
      </CardContent>
      <Sheet open={panel !== null} onOpenChange={(open) => { if (!open) setPanel(null); }}>
        <SheetContent side="end" className="w-full overflow-y-auto sm:max-w-2xl">
          <SheetHeader><SheetTitle>{panel ? t(`planning.panelTitles.${panel}`) : ""}</SheetTitle><SheetDescription>{panel ? t(`planning.panelDescriptions.${panel}`) : ""}</SheetDescription></SheetHeader>
          <div className="mt-5">{
            panel === "assignments" ? <AssignmentsSection projectId={projectId} planId={planId} editable={editable} query={assignmentsQuery} activities={activitiesQuery.data ?? []} resources={resourcesQuery.data ?? []} run={run} t={t} /> :
            panel === "calendar" ? <CalendarSection projectId={projectId} planId={planId} editable={editable} query={calendarsQuery} selectedId={selectedCalendarId} onSelect={setSelectedCalendarId} run={run} t={t} /> :
            panel === "baseline" ? <BaselinePanel baseline={currentBaseline} activities={activitiesQuery.data ?? []} t={t} onCompare={() => { setShowBaseline(true); setPanel(null); setRibbonTab("view"); setView("table"); }} /> :
            panel === "versions" ? <VersionsPanel plan={planQuery.data} workingVersion={workingVersion} currentBaseline={currentBaseline} t={t} enumLabel={enumLabel} /> : null
          }</div>
        </SheetContent>
      </Sheet>
      {importOpen && <LazyPlanImportDialog projectId={projectId} plans={plans} selectedPlanId={planId} open={importOpen} onOpenChange={setImportOpen} onImported={handleImported} />}
    </Card>
  );
}

function WorkspaceButton({ onClick, icon, children, disabled = false }: { onClick: () => void; icon: React.ReactNode; children: React.ReactNode; disabled?: boolean }) {
  return <Button type="button" variant="ghost" size="sm" disabled={disabled} onClick={onClick} className="h-7 gap-1 px-2 text-[11px]">{icon}{children}</Button>;
}

function VersionsPanel({ plan, workingVersion, currentBaseline, t, enumLabel }: { plan?: PlanDto; workingVersion?: PlanDto["versions"][number]; currentBaseline?: PlanDto["versions"][number]; t: ReturnType<typeof useTranslations>; enumLabel: (_group: "planType" | "versionType" | "versionStatus" | "activityType" | "dependencyType", _value: string) => string }) {
  const versions = plan?.versions ?? [];
  return <div className="space-y-2">{versions.map((version) => <div key={version.id} className="flex items-center justify-between gap-3 rounded border p-3 text-xs"><div><p className="font-medium">{enumLabel("versionType", version.type)} · {t("planning.versionShort")} {version.versionNumber}</p><p className="mt-1 text-muted-foreground">{enumLabel("versionStatus", version.status)}</p></div><Badge variant={version.id === workingVersion?.id ? "default" : version.id === currentBaseline?.id ? "secondary" : "outline"}>{version.id === workingVersion?.id ? t("planning.editable") : t("planning.readOnly")}</Badge></div>)}</div>;
}

function BaselinePanel({ baseline, activities, t, onCompare }: { baseline?: PlanDto["versions"][number]; activities: PlanActivityDto[]; t: ReturnType<typeof useTranslations>; onCompare: () => void }) {
  if (!baseline) return <div className="rounded-lg border border-dashed p-5 text-sm"><p className="font-medium">{t("planning.noBaseline")}</p><p className="mt-1 text-muted-foreground">{t("planning.noBaselineDescription")}</p></div>;
  const captured = activities.filter((activity) => activity.baselineStartDate || activity.baselineFinishDate || activity.baselineDuration !== undefined);
  const late = activities.filter((activity) => (activity.finishVarianceDays ?? 0) > 0).length;
  const early = activities.filter((activity) => (activity.finishVarianceDays ?? 0) < 0).length;
  const unchanged = activities.filter((activity) => (activity.finishVarianceDays ?? 0) === 0).length;
  const dated = captured.flatMap((activity) => [activity.baselineStartDate, activity.baselineFinishDate].filter(Boolean).map((value) => value!.slice(0, 10)));
  const duration = captured.reduce((maximum, activity) => Math.max(maximum, activity.baselineDuration ?? 0), 0);
  return <div className="space-y-4">
    <div className="rounded-lg border bg-muted/20 p-4"><div className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold">{t("planning.baselineCreated")} {t("planning.versionShort")}{baseline.versionNumber}</p><p className="mt-1 text-xs text-muted-foreground">{t("planning.baselineSummaryDescription")}</p></div><Badge status="Approved">{t("planning.readOnly")}</Badge></div><div className="mt-3 grid gap-2 text-xs sm:grid-cols-2"><ReadOnlyMeta label={t("planning.baselineCapturedAt")} value={dateInput(baseline.createdAt)} /><ReadOnlyMeta label={t("planning.baselineCreatedBy")} value={baseline.createdBy || "—"} /><ReadOnlyMeta label={t("planning.baselineActivities")} value={String(captured.length)} /><ReadOnlyMeta label={t("planning.baselineMilestones")} value={String(activities.filter((activity) => activity.type === "Milestone" && activity.baselineFinishDate).length)} /></div></div>
    <div className="grid gap-2 sm:grid-cols-3"><StatCard label={t("planning.baselineStartDate")} value={dated.length ? dated.sort()[0] : "—"} /><StatCard label={t("planning.baselineFinishDate")} value={dated.length ? dated.sort().at(-1) ?? "—" : "—"} /><StatCard label={t("planning.baselinePlanDuration")} value={`${duration} ${t("planning.durationDays")}`} /></div>
    <div className="grid gap-2 sm:grid-cols-3"><StatCard label={t("planning.baselineLate")} value={String(late)} /><StatCard label={t("planning.baselineEarly")} value={String(early)} /><StatCard label={t("planning.baselineUnchanged")} value={String(unchanged)} /></div>
    <Button type="button" className="w-full gap-2" onClick={onCompare}><GitCompareArrows className="h-4 w-4" />{t("planning.compareBaseline")}</Button>
  </div>;
}

function ReadOnlyMeta({ label, value }: { label: string; value: string }) { return <div className="rounded border bg-background px-3 py-2"><p className="text-[10px] uppercase tracking-wide text-muted-foreground">{label}</p><p className="mt-0.5 truncate font-medium tabular-nums" title={value}>{value}</p></div>; }
function StatCard({ label, value }: { label: string; value: string }) { return <div className="rounded-lg border bg-background p-3"><p className="text-[10px] uppercase tracking-wide text-muted-foreground">{label}</p><p className="mt-1 text-sm font-semibold tabular-nums">{value}</p></div>; }

function CreatePlanPanel({ mutation, onCreated, onCancel }: { mutation: ReturnType<typeof useCreatePlanningPlan>; onCreated: (_plan: PlanDto) => void; onCancel: () => void }) {
  const t = useTranslations("projects");
  const enums = useTranslations("enums");
  const [planType, setPlanType] = useState<PlanType>(PLANNING_PLAN_TYPES[0]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (mutation.isPending) return;
    try {
      const plan = await mutation.mutateAsync({ planType, name: name.trim(), description: description.trim() || null });
      onCreated(plan);
    } catch {
      return;
    }
  };

  return (
    <form className="grid gap-3 rounded border bg-muted/20 p-4" onSubmit={submit}>
      <div><h3 className="text-sm font-semibold">{t("planning.createPlan")}</h3><p className="mt-1 text-xs text-muted-foreground">{t("planning.createPlanDescription")}</p></div>
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1.5"><label className="text-xs font-medium" htmlFor="planning-plan-type">{t("planning.planType")}</label><Select value={planType} onValueChange={(value: PlanType) => setPlanType(value)}><SelectTrigger id="planning-plan-type"><SelectValue /></SelectTrigger><SelectContent>{PLANNING_PLAN_TYPES.map((value) => <SelectItem key={value} value={value}>{enums(planningLabelKey("planType", value))}</SelectItem>)}</SelectContent></Select></div>
        <div className="space-y-1.5"><label className="text-xs font-medium" htmlFor="planning-plan-name">{t("planning.planName")}</label><Input id="planning-plan-name" value={name} onChange={(event) => setName(event.target.value)} placeholder={t("planning.planNamePlaceholder")} required /></div>
      </div>
      <div className="space-y-1.5"><label className="text-xs font-medium" htmlFor="planning-plan-description">{t("planning.planDescription")}</label><Input id="planning-plan-description" value={description} onChange={(event) => setDescription(event.target.value)} placeholder={t("planning.planDescriptionPlaceholder")} /></div>
      {mutation.isError && <p role="alert" className="text-xs text-destructive">{errorMessage(mutation.error, t("planning.createPlanFailed"))}</p>}
      <div className="flex gap-2"><Button type="submit" disabled={mutation.isPending || !name.trim()}>{mutation.isPending ? t("planning.creatingPlan") : t("planning.createPlan")}</Button><Button type="button" variant="ghost" onClick={onCancel} disabled={mutation.isPending}>{t("planning.cancelCreatePlan")}</Button></div>
    </form>
  );
}

function SectionHeading({ title, description, children }: { title: string; description: string; children?: React.ReactNode }) {
  return <div className="mb-4 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between"><div><h3 className="text-sm font-semibold">{title}</h3><p className="mt-1 text-xs text-muted-foreground">{description}</p></div>{children}</div>;
}

function EmptySection({ text }: { text: string }) { return <div className="rounded border border-dashed px-4 py-8 text-center text-xs text-muted-foreground">{text}</div>; }

function AssignmentsSection({ projectId, planId, editable, query, activities, resources, run, t }: { projectId: string; planId: string; editable: boolean; query: ReturnType<typeof usePlanningAssignments>; activities: PlanActivityDto[]; resources: Array<{ id: string; name: string; jobTitle?: string; isActive: boolean }>; run: (_action: () => Promise<unknown>, _success: string) => Promise<void>; t: ReturnType<typeof useTranslations> }) {
  const [draft, setDraft] = useState<AssignmentDraft>({ activityId: "", resourceId: "", allocationPct: undefined, hoursPerWeek: undefined, startDate: "", endDate: "" });
  const [editing, setEditing] = useState<string | null>(null);
  const [editDraft, setEditDraft] = useState<AssignmentDraft>(draft);
  const queryKey = planningKeys.assignments(projectId, planId);
  const create = usePlanningMutation(queryKey, (body: AssignmentDraft) => planningApi.createAssignment(projectId, planId, { ...body, startDate: nullableDate(body.startDate), endDate: nullableDate(body.endDate) }));
  const update = usePlanningMutation(queryKey, (body: AssignmentDraft & { id: string; rowVersion: string }) => planningApi.updateAssignment(projectId, planId, body.id, { ...body, startDate: nullableDate(body.startDate), endDate: nullableDate(body.endDate), rowVersion: body.rowVersion }));
  const remove = usePlanningMutation(queryKey, (body: { id: string; rowVersion: string }) => planningApi.deleteAssignment(projectId, planId, body.id, body.rowVersion));
  const resourceName = (id: string) => resources.find((resource) => resource.id === id)?.name ?? id.slice(0, 8);
  const activityName = (id: string) => activities.find((activity) => activity.id === id)?.name ?? id.slice(0, 8);
  const selector = (value: string, onChange: (_value: string) => void, placeholder: string, options: Array<{ id: string; label: string }>) => <Select value={value} onValueChange={onChange}><SelectTrigger><SelectValue placeholder={placeholder} /></SelectTrigger><SelectContent>{options.map((option) => <SelectItem key={option.id} value={option.id}>{option.label}</SelectItem>)}</SelectContent></Select>;
  const editor = (value: AssignmentDraft, setValue: (_next: AssignmentDraft) => void, submit: () => void, label: string, cancel?: () => void) => <div className="grid gap-2 rounded border bg-muted/20 p-3 sm:grid-cols-2 lg:grid-cols-6">{selector(value.activityId, (activityId) => setValue({ ...value, activityId }), t("planning.selectActivity"), activities.map((activity) => ({ id: activity.id, label: `${activity.code} · ${activity.name}` })))}{selector(value.resourceId, (resourceId) => setValue({ ...value, resourceId }), t("planning.selectResource"), resources.map((resource) => ({ id: resource.id, label: resource.name })))}<Input type="number" min={0} max={100} placeholder={t("planning.allocation")} value={value.allocationPct ?? ""} onChange={(event) => setValue({ ...value, allocationPct: event.target.value === "" ? undefined : Number(event.target.value) })} /><Input type="number" min={0} placeholder={t("planning.hoursPerWeek")} value={value.hoursPerWeek ?? ""} onChange={(event) => setValue({ ...value, hoursPerWeek: event.target.value === "" ? undefined : Number(event.target.value) })} /><Input type="date" value={value.startDate} onChange={(event) => setValue({ ...value, startDate: event.target.value })} /><Input type="date" value={value.endDate} onChange={(event) => setValue({ ...value, endDate: event.target.value })} /><div className="flex gap-1 lg:col-span-2"><Button type="button" size="sm" onClick={submit}>{label}</Button>{cancel && <Button type="button" size="sm" variant="ghost" onClick={cancel}><X className="h-4 w-4" /></Button>}</div></div>;
  const createDraft = () => void run(async () => { await create.mutateAsync(draft); setDraft({ activityId: "", resourceId: "", allocationPct: undefined, hoursPerWeek: undefined, startDate: "", endDate: "" }); }, t("planning.created"));
  return <div><SectionHeading title={t("planning.assignmentsTitle")} description={t("planning.assignmentsDescription")} />{editable && <form onSubmit={(event) => { event.preventDefault(); createDraft(); }}>{editor(draft, setDraft, createDraft, commonLabel(t, "create"))}</form>}<div className="mt-4 space-y-2">{query.isLoading ? <Skeleton className="h-28 w-full" /> : query.data?.length ? query.data.map((assignment) => editing === assignment.id ? <div key={assignment.id}>{editor(editDraft, setEditDraft, () => void run(async () => { await update.mutateAsync({ ...editDraft, id: assignment.id, rowVersion: assignment.rowVersion }); setEditing(null); }, t("planning.updated")), commonLabel(t, "save"), () => setEditing(null))}</div> : <div key={assignment.id} className="flex flex-col gap-2 rounded border px-3 py-2 text-xs sm:flex-row sm:items-center"><div className="min-w-0 flex-1"><span className="font-medium">{activityName(assignment.activityId)}</span><span className="mx-1 text-muted-foreground">·</span><span>{resourceName(assignment.resourceId)}</span></div><span className="text-muted-foreground">{assignment.allocationPct ?? "—"}% · {assignment.hoursPerWeek ?? "—"} {t("planning.hoursShort")}</span>{editable && <div className="flex gap-1"><Button size="icon" variant="ghost" onClick={() => { setEditing(assignment.id); setEditDraft({ activityId: assignment.activityId, resourceId: assignment.resourceId, allocationPct: assignment.allocationPct, hoursPerWeek: assignment.hoursPerWeek, startDate: dateInput(assignment.startDate), endDate: dateInput(assignment.endDate) }); }} aria-label={commonLabel(t, "edit")}><Edit3 className="h-4 w-4" /></Button><Button size="icon" variant="ghost" onClick={() => { if (window.confirm(t("planning.confirmDelete"))) void run(() => remove.mutateAsync({ id: assignment.id, rowVersion: assignment.rowVersion }), t("planning.deleted")); }} aria-label={commonLabel(t, "delete")}><Trash2 className="h-4 w-4 text-destructive" /></Button></div>}</div>) : <EmptySection text={t("planning.noAssignments")} />}</div></div>;
}

type CalendarTab = "general" | "week" | "holidays";

type CalendarDraft = {
  name: string;
  description: string;
  isDefault: boolean;
  effectiveFrom: string;
  effectiveTo: string;
  dayRules: CalendarDayRuleDto[];
  holidays: CalendarHolidayDto[];
};

const CALENDAR_WEEKDAYS = [0, 1, 2, 3, 4, 5, 6];
const CALENDAR_DAY_NAMES: Record<string, number> = {
  Sunday: 0,
  Monday: 1,
  Tuesday: 2,
  Wednesday: 3,
  Thursday: 4,
  Friday: 5,
  Saturday: 6,
};

function defaultDayRule(dayOfWeek: number): CalendarDayRuleDto {
  const working = dayOfWeek >= 1 && dayOfWeek <= 5;
  return { dayOfWeek, isWorkingDay: working, startTime: working ? "08:00:00" : undefined, endTime: working ? "17:00:00" : undefined, breakStartTime: working ? "12:00:00" : undefined, breakEndTime: working ? "13:00:00" : undefined };
}

function normalizeDayRules(rules: CalendarDayRuleDto[] | undefined) {
  const byDay = new Map((rules ?? []).map((rule) => {
    const day = typeof rule.dayOfWeek === "number" ? rule.dayOfWeek : CALENDAR_DAY_NAMES[rule.dayOfWeek] ?? Number(rule.dayOfWeek);
    return [day, { ...rule, dayOfWeek: day } satisfies CalendarDayRuleDto];
  }));
  return CALENDAR_WEEKDAYS.map((day) => byDay.get(day) ?? defaultDayRule(day));
}

function calendarDayIndex(value: number | string): number {
  return typeof value === "number" ? value : CALENDAR_DAY_NAMES[value] ?? Number(value);
}

function calendarDate(value?: string) { return value?.slice(0, 10) ?? ""; }
function calendarTime(value?: string) { return value?.slice(0, 5) ?? ""; }
function calendarTimeValue(value: string) { return value ? (value.length === 5 ? value + ":00" : value) : undefined; }

function CalendarSection({ projectId, planId, editable, query, selectedId, onSelect, run, t }: { projectId: string; planId: string; editable: boolean; query: ReturnType<typeof usePlanningCalendars>; selectedId: string; onSelect: (_id: string) => void; run: (_action: () => Promise<unknown>, _success: string) => Promise<void>; t: ReturnType<typeof useTranslations> }) {
  const queryClient = useQueryClient();
  const [newName, setNewName] = useState("");
  const [tab, setTab] = useState<CalendarTab>("general");
  const [draft, setDraft] = useState<CalendarDraft | null>(null);
  const [holidayDraft, setHolidayDraft] = useState<CalendarHolidayDto>({ name: "", description: "", date: "", endDate: "", isRecurring: false });
  const [editingHoliday, setEditingHoliday] = useState<number | null>(null);
  const selected = query.data?.find((calendar) => calendar.id === selectedId);
  const queryKey = planningKeys.calendars(projectId, planId);
  const create = usePlanningMutation(queryKey, (body: { name: string }) => planningApi.createCalendar(projectId, planId, { ...body, dayRules: normalizeDayRules([]), holidays: [] }));
  const update = useUpdatePlanningCalendar(projectId, planId);
  const remove = usePlanningMutation(queryKey, (body: { id: string; rowVersion: string }) => planningApi.deleteCalendar(projectId, planId, body.id, body.rowVersion));

  useEffect(() => {
    if (!selected) { setDraft(null); return; }
    setDraft({
      name: selected.name,
      description: selected.description ?? "",
      isDefault: selected.isDefault,
      effectiveFrom: calendarDate(selected.effectiveFrom),
      effectiveTo: calendarDate(selected.effectiveTo),
      dayRules: normalizeDayRules(selected.dayRules),
      holidays: selected.holidays.map((holiday) => ({ ...holiday, date: calendarDate(holiday.date), endDate: calendarDate(holiday.endDate) || undefined })),
    });
    setTab("general");
    setEditingHoliday(null);
    setHolidayDraft({ name: "", description: "", date: "", endDate: "", isRecurring: false });
  }, [selected]);

  const setDayRule = (dayOfWeek: number | string, patch: Partial<CalendarDayRuleDto>) => setDraft((current) => {
    if (!current) return current;
    const targetDay = calendarDayIndex(dayOfWeek);
    return { ...current, dayRules: current.dayRules.map((rule) => calendarDayIndex(rule.dayOfWeek) !== targetDay ? rule : {
      ...rule,
      dayOfWeek: targetDay,
      ...patch,
      ...(patch.isWorkingDay === false ? { startTime: undefined, endTime: undefined, breakStartTime: undefined, breakEndTime: undefined } : {}),
      ...(patch.isWorkingDay === true && !rule.startTime ? { startTime: "08:00:00", endTime: "17:00:00", breakStartTime: "12:00:00", breakEndTime: "13:00:00" } : {}),
    }) };
  });

  const save = async () => {
    if (!selected || !draft || update.isPending) return;
    await run(async () => {
      await update.mutateAsync({
        calendarId: selected.id,
        body: {
          name: draft.name.trim(),
          description: draft.description.trim() || null,
          isDefault: draft.isDefault,
          templateId: selected.templateId || null,
          effectiveFrom: draft.effectiveFrom || null,
          effectiveTo: draft.effectiveTo || null,
          dayRules: draft.dayRules,
          holidays: draft.holidays.map((holiday) => ({ ...holiday, description: holiday.description?.trim() || null, endDate: holiday.endDate || null })),
          rowVersion: selected.rowVersion,
        },
      });
    }, t("planning.updated"));
  };

  const addOrUpdateHoliday = () => {
    if (!draft || !holidayDraft.name.trim() || !holidayDraft.date) return;
    const value = { ...holidayDraft, name: holidayDraft.name.trim(), description: holidayDraft.description?.trim() || undefined, endDate: holidayDraft.endDate || undefined };
    setDraft((current) => {
      if (!current) return current;
      const holidays = [...current.holidays];
      if (editingHoliday === null) holidays.push(value);
      else holidays[editingHoliday] = value;
      return { ...current, holidays };
    });
    setEditingHoliday(null);
    setHolidayDraft({ name: "", description: "", date: "", endDate: "", isRecurring: false });
  };

  const startHolidayEdit = (index: number) => {
    if (!draft) return;
    const holiday = draft.holidays[index];
    setEditingHoliday(index);
    setHolidayDraft({ ...holiday, date: calendarDate(holiday.date), endDate: calendarDate(holiday.endDate) });
  };

  const invalidateCalendarImpact = async () => {
    await queryClient.invalidateQueries({ queryKey: planningKeys.activities(projectId, planId) });
  };

  const createCalendar = () => {
    if (!newName.trim() || create.isPending) return;
    void run(async () => {
      const created = await create.mutateAsync({ name: newName.trim() });
      await invalidateCalendarImpact();
      onSelect(created.id);
      setNewName("");
    }, t("planning.created"));
  };

  return <div data-testid="planning-calendar-editor">
    <SectionHeading title={t("planning.calendarTitle")} description={t("planning.calendarDescription")} />
      {editable && <form className="mb-4 flex gap-2" onSubmit={(event) => { event.preventDefault(); createCalendar(); }}><Input placeholder={t("planning.calendarName")} value={newName} onChange={(event) => setNewName(event.target.value)} /><Button type="submit" size="sm" disabled={create.isPending || !newName.trim()}><Plus className="mr-1.5 h-3.5 w-3.5" />{commonLabel(t, "create")}</Button></form>}
    {query.isLoading ? <Skeleton className="h-36 w-full" /> : query.data?.length ? <div className="grid gap-4 lg:grid-cols-[minmax(180px,0.35fr)_minmax(0,1fr)]">
      <div className="space-y-1">{query.data.map((calendar) => <div key={calendar.id} className="flex items-stretch gap-1"><button type="button" onClick={() => onSelect(calendar.id)} className={"min-w-0 flex-1 rounded border px-3 py-2 text-start text-xs " + (calendar.id === selectedId ? "border-primary bg-primary/5" : "hover:bg-muted/40")}><span className="block truncate font-medium">{calendar.name}</span><span className="mt-1 block text-[10px] text-muted-foreground">{calendar.dayRules.filter((rule) => rule.isWorkingDay).length} {t("planning.workingDays")} · {calendar.holidays.length} {t("planning.holidays")}</span></button>{editable && <Button type="button" size="icon" variant="ghost" onClick={() => { if (window.confirm(t("planning.confirmDelete"))) void run(async () => { await remove.mutateAsync({ id: calendar.id, rowVersion: calendar.rowVersion }); await invalidateCalendarImpact(); if (calendar.id === selectedId) onSelect(""); }, t("planning.deleted")); }} aria-label={commonLabel(t, "delete")}><Trash2 className="h-4 w-4 text-destructive" /></Button>}</div>)}</div>
      {selected && draft && <div className="min-w-0 rounded border p-4">
        <div className="sticky top-0 z-10 -mx-4 mb-4 flex gap-1 overflow-x-auto border-b bg-background/95 px-3 py-2 backdrop-blur">
          {(["general", "week", "holidays"] as CalendarTab[]).map((value) => <button key={value} type="button" onClick={() => setTab(value)} className={"shrink-0 rounded px-2.5 py-1.5 text-xs " + (tab === value ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-muted")}>{t("planning.calendarTabs." + value)}</button>)}
        </div>
        {tab === "general" && <div className="space-y-3">
          <div className="grid gap-3 sm:grid-cols-2"><label className="space-y-1.5 text-xs font-medium">{t("planning.calendarName")}<Input disabled={!editable} value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} /></label><label className="space-y-1.5 text-xs font-medium">{t("planning.descriptionOptional")}<Input disabled={!editable} value={draft.description} onChange={(event) => setDraft({ ...draft, description: event.target.value })} /></label><label className="space-y-1.5 text-xs font-medium">{t("planning.effectiveFrom")}<Input type="date" disabled={!editable} value={draft.effectiveFrom} onChange={(event) => setDraft({ ...draft, effectiveFrom: event.target.value })} /></label><label className="space-y-1.5 text-xs font-medium">{t("planning.effectiveTo")}<Input type="date" disabled={!editable} value={draft.effectiveTo} onChange={(event) => setDraft({ ...draft, effectiveTo: event.target.value })} /></label></div>
          <label className="flex items-center gap-2 rounded border bg-muted/20 p-3 text-xs"><input type="checkbox" disabled={!editable} checked={draft.isDefault} onChange={(event) => setDraft({ ...draft, isDefault: event.target.checked })} />{t("planning.defaultCalendar")}</label>
          {selected.templateId && <p className="text-[11px] text-muted-foreground">{t("planning.templateId")}: {selected.templateId}</p>}
        </div>}
        {tab === "week" && <div className="space-y-2">
          <p className="text-xs text-muted-foreground">{t("planning.workingWeekDescription")}</p>
          <div className="overflow-x-auto rounded border"><div className="min-w-[660px]">
            <div className="grid grid-cols-[1.2fr_0.8fr_1fr_1fr_1fr_1fr] gap-2 border-b bg-muted/30 px-3 py-2 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground"><span>{t("planning.day")}</span><span>{t("planning.working")}</span><span>{t("planning.startTime")}</span><span>{t("planning.endTime")}</span><span>{t("planning.breakStart")}</span><span>{t("planning.breakEnd")}</span></div>
            {draft.dayRules.map((rule) => <div key={rule.dayOfWeek} className="grid grid-cols-[1.2fr_0.8fr_1fr_1fr_1fr_1fr] items-center gap-2 border-b px-3 py-2 text-xs last:border-b-0"><span>{t("planning.days." + String(rule.dayOfWeek))}</span><input type="checkbox" disabled={!editable} checked={rule.isWorkingDay} onChange={(event) => setDayRule(rule.dayOfWeek, { isWorkingDay: event.target.checked })} /><Input type="time" disabled={!editable || !rule.isWorkingDay} value={calendarTime(rule.startTime)} onChange={(event) => setDayRule(rule.dayOfWeek, { startTime: calendarTimeValue(event.target.value) })} /><Input type="time" disabled={!editable || !rule.isWorkingDay} value={calendarTime(rule.endTime)} onChange={(event) => setDayRule(rule.dayOfWeek, { endTime: calendarTimeValue(event.target.value) })} /><Input type="time" disabled={!editable || !rule.isWorkingDay} value={calendarTime(rule.breakStartTime)} onChange={(event) => setDayRule(rule.dayOfWeek, { breakStartTime: calendarTimeValue(event.target.value) })} /><Input type="time" disabled={!editable || !rule.isWorkingDay} value={calendarTime(rule.breakEndTime)} onChange={(event) => setDayRule(rule.dayOfWeek, { breakEndTime: calendarTimeValue(event.target.value) })} /></div>)}
          </div></div>
        </div>}
        {tab === "holidays" && <div className="space-y-3">
          <p className="text-xs text-muted-foreground">{t("planning.holidaysDescription")}</p>
          {editable && <div className="grid gap-2 rounded border bg-muted/20 p-3 sm:grid-cols-2"><Input placeholder={t("planning.holidayName")} value={holidayDraft.name} onChange={(event) => setHolidayDraft({ ...holidayDraft, name: event.target.value })} /><Input placeholder={t("planning.holidayDescription")} value={holidayDraft.description ?? ""} onChange={(event) => setHolidayDraft({ ...holidayDraft, description: event.target.value })} /><Input type="date" value={calendarDate(holidayDraft.date)} onChange={(event) => setHolidayDraft({ ...holidayDraft, date: event.target.value })} /><Input type="date" value={calendarDate(holidayDraft.endDate)} onChange={(event) => setHolidayDraft({ ...holidayDraft, endDate: event.target.value })} /><label className="flex items-center gap-2 text-xs"><input type="checkbox" checked={holidayDraft.isRecurring} onChange={(event) => setHolidayDraft({ ...holidayDraft, isRecurring: event.target.checked })} />{t("planning.recurring")}</label><div className="flex gap-2"><Button type="button" size="sm" onClick={addOrUpdateHoliday} disabled={!holidayDraft.name.trim() || !holidayDraft.date}>{editingHoliday === null ? commonLabel(t, "create") : commonLabel(t, "save")}</Button>{editingHoliday !== null && <Button type="button" size="sm" variant="ghost" onClick={() => { setEditingHoliday(null); setHolidayDraft({ name: "", description: "", date: "", endDate: "", isRecurring: false }); }}>{commonLabel(t, "cancel")}</Button>}</div></div>}
          {draft.holidays.length === 0 ? <EmptySection text={t("planning.noHolidays")} /> : <div className="overflow-x-auto rounded border"><div className="min-w-[600px] divide-y">{draft.holidays.map((holiday, index) => <div key={index} className="flex items-center gap-3 px-3 py-2 text-xs"><div className="min-w-0 flex-1"><p className="truncate font-medium">{holiday.name}</p><p className="text-muted-foreground">{calendarDate(holiday.date)}{holiday.endDate ? " → " + calendarDate(holiday.endDate) : ""}{holiday.isRecurring ? " · " + t("planning.recurring") : ""}</p>{holiday.description && <p className="truncate text-muted-foreground">{holiday.description}</p>}</div>{editable && <div className="flex gap-1"><Button type="button" size="icon" variant="ghost" onClick={() => startHolidayEdit(index)} aria-label={commonLabel(t, "edit")}><Edit3 className="h-4 w-4" /></Button><Button type="button" size="icon" variant="ghost" onClick={() => setDraft({ ...draft, holidays: draft.holidays.filter((_item, itemIndex) => itemIndex !== index) })} aria-label={commonLabel(t, "delete")}><Trash2 className="h-4 w-4 text-destructive" /></Button></div>}</div>)}</div></div>}
        </div>}
        {editable && <div className="mt-4 flex justify-end border-t pt-3"><Button type="button" size="sm" onClick={() => void save()} disabled={update.isPending || !draft.name.trim()}><Save className="mr-1.5 h-3.5 w-3.5" />{commonLabel(t, "save")}</Button></div>}
      </div>}
    </div> : <EmptySection text={t("planning.noCalendars")} />}
  </div>;
}

function commonLabel(t: ReturnType<typeof useTranslations>, key: "create" | "save" | "cancel" | "edit" | "delete") { return t(`planning.actionButtons.${key}`); }
