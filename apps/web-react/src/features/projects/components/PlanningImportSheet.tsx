import * as React from "react";
import { AlertTriangle, CheckCircle2, FileUp, Loader2 } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Progress } from "@components/ui/Progress";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { useToast } from "@hooks/use-toast";
import { planningApiFull, type PlanImportSessionDto, type PlanSummaryDto, type PlanType } from "../api/planningFull";
import { planningImportErrorMessage, planningImportNoticeMessage } from "../lib/planning-import-messages";
import { roundPlanProgress } from "../lib/plan-progress";

const PLAN_TYPES: PlanType[] = ["Execution", "Recovery", "Tender", "Construction", "Shutdown", "Scenario", "Custom"];

type ImportMode = "existing" | "new";
type ImportState = "ready" | "parsing" | "reviewing" | "committing";
type Message = (key: string, params?: Record<string, string | number>) => string;
type CommitMutation = {
  mutateAsync: (input: { sessionId: string; body: { targetPlanId?: string; newPlanName?: string; newPlanType?: PlanType } }) => Promise<{ planId: string; versionNumber: number; statistics: { tasks: number; milestones: number } }>;
  isPending: boolean;
};

export function PlanningImportSheet({
  projectId,
  plans,
  selectedPlanId,
  commitMutation,
  t,
  onOpenChange,
  onImported,
}: {
  projectId: string;
  plans: PlanSummaryDto[];
  selectedPlanId: string;
  commitMutation: CommitMutation;
  t: Message;
  onOpenChange: (open: boolean) => void;
  onImported: (planId: string) => void | Promise<void>;
}) {
  const { toast } = useToast();
  const [mode, setMode] = React.useState<ImportMode>(plans.length ? "existing" : "new");
  const [targetPlanId, setTargetPlanId] = React.useState(selectedPlanId || plans[0]?.id || "");
  const [newPlanName, setNewPlanName] = React.useState("");
  const [newPlanType, setNewPlanType] = React.useState<PlanType>("Custom");
  const [file, setFile] = React.useState<File>();
  const [session, setSession] = React.useState<PlanImportSessionDto>();
  const [state, setState] = React.useState<ImportState>("ready");
  const [tab, setTab] = React.useState("overview");
  const [error, setError] = React.useState<string>();
  const [resolving, setResolving] = React.useState(false);
  const [reviewTarget, setReviewTarget] = React.useState<{ tab: string; sourceId: string }>();
  const reviewRows = React.useRef(new Map<string, HTMLTableRowElement>());

  const busy = state === "parsing" || state === "committing" || commitMutation.isPending;
  const validFile = Boolean(file && file.name.toLowerCase().endsWith(".mpp"));
  const canParse = validFile && (mode === "new" ? Boolean(newPlanType) : Boolean(targetPlanId));
  const totalRows = session ? session.statistics.tasks + session.statistics.milestones : 0;
  const resourceResolutionCounts = session?.resources.reduce((counts, resource) => {
    const resolution = resource.resolution;
    if (resolution === "MapExisting") counts.mapped++;
    else if (resolution === "CreateNew") counts.toCreate++;
    else if (resolution === "Ignore") counts.ignored++;
    else counts.needsReview++;
    return counts;
  }, { mapped: 0, toCreate: 0, ignored: 0, needsReview: 0 });
  const resourceResolutionSummary = resourceResolutionCounts ? [
    resourceResolutionCounts.mapped ? t("planning.import.resourceSummary.mapped", { count: resourceResolutionCounts.mapped }) : undefined,
    resourceResolutionCounts.toCreate ? t("planning.import.resourceSummary.toCreate", { count: resourceResolutionCounts.toCreate }) : undefined,
    resourceResolutionCounts.ignored ? t("planning.import.resourceSummary.ignored", { count: resourceResolutionCounts.ignored }) : undefined,
    resourceResolutionCounts.needsReview ? t("planning.import.resourceSummary.needsReview", { count: resourceResolutionCounts.needsReview }) : undefined,
  ].filter(Boolean).join(" · ") : undefined;
  const assignmentResolutionCounts = session?.assignments.reduce((counts, assignment) => {
    if (assignment.status === "Ignore" || assignment.status === "IgnoredByResource") counts.ignored++;
    else if (assignment.status === "NeedsReview" || assignment.status === "NeedsResource") counts.needsReview++;
    return counts;
  }, { ignored: 0, needsReview: 0 });
  const assignmentResolutionSummary = assignmentResolutionCounts ? [
    assignmentResolutionCounts.ignored ? t("planning.import.assignmentSummary.ignored", { count: assignmentResolutionCounts.ignored }) : undefined,
    assignmentResolutionCounts.needsReview ? t("planning.import.assignmentSummary.needsReview", { count: assignmentResolutionCounts.needsReview }) : undefined,
  ].filter(Boolean).join(" · ") : undefined;

  React.useEffect(() => {
    if (!reviewTarget || reviewTarget.tab !== tab) return;
    const row = reviewRows.current.get(reviewRowKey(reviewTarget.tab, reviewTarget.sourceId));
    row?.scrollIntoView?.({ block: "nearest" });
    row?.focus();
  }, [reviewTarget, tab]);

  function reviewIssue(notice: { category?: string; entityType?: string; code: string; sourceId?: string }) {
    if (!notice.sourceId) return;
    const targetTab = reviewTabForIssue(notice);
    if (!targetTab) return;
    setReviewTarget({ tab: targetTab, sourceId: notice.sourceId });
    setTab(targetTab);
  }

  async function parse() {
    if (!file || !canParse) return;
    setError(undefined);
    setState("parsing");
    try {
      const result = await planningApiFull.importSession(projectId, file, mode === "existing" ? targetPlanId : undefined);
      setSession(result);
      setNewPlanName((current) => current.trim() ? current : result.suggestedPlanName);
      setState("reviewing");
    } catch (cause) {
      setError(planningImportErrorMessage(cause, "planning.import.parseFailed", t));
      setState("ready");
    }
  }

  async function resolve(kind: "resource" | "assignment" | "calendar", sourceId: string, body: Record<string, unknown>) {
    if (!session || resolving) return;
    setResolving(true);
    setError(undefined);
    try {
      const updated = kind === "resource"
        ? await planningApiFull.resolveImportResource(projectId, session.id, sourceId, body as { resolution: "MapExisting" | "CreateNew" | "Ignore"; resourceId?: string; createResource?: Record<string, unknown> })
        : kind === "assignment"
          ? await planningApiFull.resolveImportAssignment(projectId, session.id, sourceId, body as { resolution: "Ready" | "Ignore" })
          : await planningApiFull.resolveImportCalendar(projectId, session.id, sourceId, body as { resolution: "Import" | "Simplify" | "Ignore" });
      setSession(updated);
    } catch (cause) {
      setError(planningImportErrorMessage(cause, "planning.import.resolutionFailed", t));
    } finally {
      setResolving(false);
    }
  }

  async function cancelSession() {
    if (!session) return;
    setError(undefined);
    try {
      await planningApiFull.cancelImport(projectId, session.id);
      onOpenChange(false);
    } catch (cause) {
      setError(planningImportErrorMessage(cause, "planning.import.cancelFailed", t));
    }
  }

  async function commit() {
    if (!session || session.blockingIssueCount > 0 || session.status !== "ReadyToCommit") return;
    setError(undefined);
    setState("committing");
    try {
      const result = await commitMutation.mutateAsync({
        sessionId: session.id,
        body: mode === "existing"
          ? { targetPlanId }
          : { newPlanName: newPlanName.trim() || session.suggestedPlanName, newPlanType },
      });
      await onImported(result.planId);
      toast({
        title: t("planning.import.successTitle"),
        description: t("planning.import.successDescription", { count: result.statistics.tasks + result.statistics.milestones, version: result.versionNumber }),
        variant: "success",
      });
      onOpenChange(false);
    } catch (cause) {
      setError(planningImportErrorMessage(cause, "planning.import.commitFailed", t));
      setState("reviewing");
    }
  }

  const handleOpenChange = (open: boolean) => {
    if (!busy && !resolving) onOpenChange(open);
  };

  return (
    <SideSheet open onOpenChange={handleOpenChange} title={t("planning.import.title")} description={t("planning.import.description")} size="xl" closeLabel={t("common.close")}>
      <SideSheetBody className="space-y-4">
        {busy ? <div className="rounded-md border bg-muted/20 p-4" role="status">
          <div className="mb-3 flex items-center gap-2 text-sm font-medium"><Loader2 className="h-4 w-4 animate-spin" />{state === "parsing" ? t("planning.import.parsing") : t("planning.import.importing")}</div>
          <Progress value={state === "parsing" ? 45 : 78} />
          <p className="mt-2 text-xs text-muted-foreground">{state === "parsing" ? t("planning.import.parsingHint") : t("planning.import.atomicHint")}</p>
        </div> : null}

        {!session && !busy ? <div className="grid gap-4 lg:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="planning-import-file">{t("planning.import.fileLabel")}</Label>
            <Input id="planning-import-file" type="file" accept=".mpp,application/vnd.ms-project" onChange={(event) => {
              const selected = event.target.files?.[0];
              setFile(selected);
              setError(selected && !selected.name.toLowerCase().endsWith(".mpp") ? t("planning.import.mppOnly") : undefined);
            }} />
            <p className="text-[11px] text-muted-foreground">{file ? t("planning.import.selectedFile", { name: file.name, size: formatBytes(file.size) }) : t("planning.import.fileHint")}</p>
          </div>
          <div className="space-y-3 rounded-md border p-3">
            <div className="flex gap-1 rounded-md bg-muted/40 p-1">
              {plans.length > 0 ? <Button type="button" size="sm" className="flex-1" variant={mode === "existing" ? "secondary" : "ghost"} onClick={() => setMode("existing")}>{t("planning.import.existingPlan")}</Button> : null}
              <Button type="button" size="sm" className="flex-1" variant={mode === "new" ? "secondary" : "ghost"} onClick={() => setMode("new")}>{t("planning.import.newPlan")}</Button>
            </div>
            {mode === "existing" ? <div className="space-y-2">
              <Label>{t("planning.import.targetPlan")}</Label>
              <Select value={targetPlanId} onValueChange={setTargetPlanId}>
                <SelectTrigger aria-label={t("planning.import.selectPlan")}><SelectValue placeholder={t("planning.import.selectPlan")} /></SelectTrigger>
                <SelectContent>{plans.map((plan) => <SelectItem key={plan.id} value={plan.id}>{plan.name}</SelectItem>)}</SelectContent>
              </Select>
              <p className="text-[11px] text-muted-foreground">{t("planning.import.workingVersionRule")}</p>
            </div> : <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-2"><Label htmlFor="planning-import-name">{t("planning.import.planName")}</Label><Input id="planning-import-name" value={newPlanName} onChange={(event) => setNewPlanName(event.target.value)} placeholder={t("planning.import.planNamePlaceholder")} /></div>
              <div className="space-y-2"><Label>{t("planning.import.planType")}</Label><Select value={newPlanType} onValueChange={(value) => setNewPlanType(value as PlanType)}><SelectTrigger aria-label={t("planning.import.planType")}><SelectValue /></SelectTrigger><SelectContent>{PLAN_TYPES.map((type) => <SelectItem key={type} value={type}>{t(`enums.planning.planType.${type}`)}</SelectItem>)}</SelectContent></Select></div>
            </div>}
          </div>
        </div> : null}

        {session && !busy ? <div className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-muted/20 px-3 py-2">
            <div><p className="text-sm font-medium">{session.fileName}</p><p className="text-xs text-muted-foreground">{session.format ? `${session.format} · ` : ""}{session.suggestedPlanName}</p></div>
            <div className="flex items-center gap-2"><Badge status={session.blockingIssueCount ? "NeedsReview" : "ReadyToCommit"}>{session.blockingIssueCount ? t("planning.import.needsReview") : t("planning.import.readyToCommit")}</Badge><Button type="button" size="sm" variant="outline" disabled={resolving} onClick={() => { setSession(undefined); setError(undefined); setTab("overview"); }}>{t("planning.import.changeFile")}</Button></div>
          </div>

          {mode === "new" ? <div className="grid gap-3 rounded-md border p-3 sm:grid-cols-2"><div className="space-y-1"><Label htmlFor="planning-import-name-review">{t("planning.import.planName")}</Label><Input id="planning-import-name-review" value={newPlanName} onChange={(event) => setNewPlanName(event.target.value)} /></div><div className="space-y-1"><Label>{t("planning.import.planType")}</Label><Select value={newPlanType} onValueChange={(value) => setNewPlanType(value as PlanType)}><SelectTrigger aria-label={t("planning.import.planType")}><SelectValue /></SelectTrigger><SelectContent>{PLAN_TYPES.map((type) => <SelectItem key={type} value={type}>{t(`enums.planning.planType.${type}`)}</SelectItem>)}</SelectContent></Select></div></div> : <p className="text-xs text-muted-foreground">{t("planning.import.targetPlan")}: {plans.find((plan) => plan.id === targetPlanId)?.name ?? t("planning.import.selectPlan")}</p>}

          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-8">
            <Stat label={t("planning.import.stats.tasks")} value={session.statistics.tasks} />
            <Stat label={t("planning.import.stats.milestones")} value={session.statistics.milestones} />
            <Stat label={t("planning.import.stats.levels")} value={session.statistics.wbsLevels} />
            <Stat label={t("planning.import.stats.dependencies")} value={session.statistics.dependencies} />
            <Stat label={t("planning.import.stats.resources")} value={session.statistics.resources} detail={resourceResolutionSummary} />
            <Stat label={t("planning.import.stats.assignments")} value={session.statistics.assignments} detail={assignmentResolutionSummary} />
            <Stat label={t("planning.import.stats.calendars")} value={session.statistics.calendars} />
            <Stat label={t("planning.import.stats.actualDates")} value={session.statistics.actualDates} />
          </div>

          {session.issues.length > 0 ? <NoticeList title={t("planning.import.issues")} notices={session.issues} t={t} onReview={reviewIssue} /> : null}
          <Tabs value={tab} onValueChange={setTab}>
            <TabsList className="flex h-auto w-full justify-start gap-1 overflow-x-auto">
              <TabsTrigger value="overview">{t("planning.import.tabs.overview")}</TabsTrigger>
              <TabsTrigger value="tasks">{t("planning.import.tabs.tasks", { count: totalRows })}</TabsTrigger>
              <TabsTrigger value="resources">{t("planning.import.tabs.resources", { count: session.resources.length })}</TabsTrigger>
              <TabsTrigger value="assignments">{t("planning.import.tabs.assignments", { count: session.assignments.length })}</TabsTrigger>
              <TabsTrigger value="calendars">{t("planning.import.tabs.calendars", { count: session.calendars.length })}</TabsTrigger>
            </TabsList>
            <TabsContent value="overview" className="space-y-3"><NoticeList title={t("planning.import.warnings")} notices={session.warnings} t={t} /><NoticeList title={t("planning.import.unsupported")} notices={session.unsupportedFields} t={t} /></TabsContent>
            <TabsContent value="tasks"><div className="max-h-[32rem] overflow-auto rounded border"><Table><TableHeader><TableRow><TableHead>{t("planning.import.columns.wbs")}</TableHead><TableHead>{t("planning.import.columns.name")}</TableHead><TableHead>{t("planning.import.columns.duration")}</TableHead><TableHead>{t("planning.import.columns.start")}</TableHead><TableHead>{t("planning.import.columns.finish")}</TableHead><TableHead>{t("planning.import.columns.complete")}</TableHead><TableHead>{t("planning.import.columns.constraintType")}</TableHead><TableHead>{t("planning.import.columns.deadline")}</TableHead><TableHead>{t("planning.import.columns.resources")}</TableHead></TableRow></TableHeader><TableBody>{session.tasks.map((task) => <TableRow key={task.sourceId}><TableCell className="text-xs">{task.code}</TableCell><TableCell><div className="flex items-center gap-1" style={{ paddingInlineStart: `${Math.max(0, task.outlineLevel - 1) * 14}px` }}>{task.isMilestone ? <span aria-hidden>◆</span> : null}<span className="text-xs font-medium">{task.name}</span></div></TableCell><TableCell className="text-xs">{task.durationDays}d</TableCell><TableCell className="text-xs">{dateOnly(task.start)}</TableCell><TableCell className="text-xs">{dateOnly(task.finish)}</TableCell><TableCell className="text-xs">{roundPlanProgress(task.percentComplete) ?? 0}%</TableCell><TableCell className="text-xs">{t(`planning.constraintTypes.${task.constraintType}`)}</TableCell><TableCell className="text-xs">{dateOnly(task.deadline)}</TableCell><TableCell className="max-w-48 text-xs">{task.resources.join(", ") || "—"}</TableCell></TableRow>)}</TableBody></Table></div></TabsContent>
            <TabsContent value="resources"><div className="overflow-auto rounded border"><Table><TableHeader><TableRow><TableHead>{t("planning.import.resourceColumns.source")}</TableHead><TableHead>{t("planning.import.resourceColumns.match")}</TableHead><TableHead>{t("planning.import.resourceColumns.candidates")}</TableHead><TableHead>{t("planning.import.resourceColumns.action")}</TableHead></TableRow></TableHeader><TableBody>{session.resources.map((resource) => {
              const resolution = resource.resolution;
              const resolutionLabel = resolution === "MapExisting" ? "matched" : resolution === "CreateNew" ? "createSelected" : resolution === "Ignore" ? "ignored" : "needsReview";
              return <TableRow key={resource.sourceId} tabIndex={-1} ref={(node) => { const key = reviewRowKey("resources", resource.sourceId); if (node) reviewRows.current.set(key, node); else reviewRows.current.delete(key); }} className={reviewTarget?.tab === "resources" && reviewTarget.sourceId === resource.sourceId ? "outline outline-2 outline-primary/40 -outline-offset-2" : undefined}>
                <TableCell><div className="text-xs font-medium">{resource.name}</div><div className="text-[10px] text-muted-foreground">{resource.email || t("planning.import.noEmail")} · {t("planning.import.assignmentCount", { count: resource.assignmentCount })}</div></TableCell>
                <TableCell><Badge status={resolution === "MapExisting" ? "Matched" : resolution === "Ignore" ? "Ignored" : resolution === "CreateNew" ? "New" : "NeedsReview"}>{resolution === "MapExisting" ? <CheckCircle2 className="h-3 w-3" /> : resolution === "Unresolved" ? <AlertTriangle className="h-3 w-3" /> : null}{t(`planning.import.resourceResolutions.${resolutionLabel}`)}</Badge></TableCell>
                <TableCell className="min-w-56">{resource.candidates.length ? <Select value={resource.nexusResourceId || "none"} onValueChange={(value) => { if (value !== "none") void resolve("resource", resource.sourceId, { resolution: "MapExisting", resourceId: value }); }}><SelectTrigger disabled={resolving} aria-label={`${t("planning.import.selectResource")}: ${resource.name}`} className="h-8 text-xs"><SelectValue placeholder={t("planning.import.selectResource")} /></SelectTrigger><SelectContent><SelectItem value="none">{t("planning.import.selectResource")}</SelectItem>{resource.candidates.map((candidate) => <SelectItem key={candidate.id} value={candidate.id}>{candidate.name}{candidate.email ? ` · ${candidate.email}` : ""}</SelectItem>)}</SelectContent></Select> : <span className="text-xs text-muted-foreground">{t("planning.import.noCandidates")}</span>}</TableCell>
                <TableCell className="whitespace-nowrap"><div className="flex gap-1"><Button type="button" size="sm" variant={resolution === "CreateNew" ? "secondary" : "outline"} disabled={resolving} onClick={() => void resolve("resource", resource.sourceId, { resolution: "CreateNew", createResource: { name: resource.name, type: "Staff", status: "Active", email: resource.email } })}>{resolution === "CreateNew" ? t("planning.import.createSelectedResource") : t("planning.import.createResource")}</Button><Button type="button" size="sm" variant={resolution === "Ignore" ? "secondary" : "ghost"} disabled={resolving || resolution === "Ignore"} onClick={() => void resolve("resource", resource.sourceId, { resolution: "Ignore" })}>{resolution === "Ignore" ? t("planning.import.ignored") : t("planning.import.ignore")}</Button></div></TableCell>
              </TableRow>;
            })}</TableBody></Table></div></TabsContent>
            <TabsContent value="assignments"><div className="overflow-auto rounded border"><Table><TableHeader><TableRow><TableHead>{t("planning.import.assignmentColumns.task")}</TableHead><TableHead>{t("planning.import.assignmentColumns.resource")}</TableHead><TableHead>{t("planning.import.assignmentColumns.work")}</TableHead><TableHead>{t("planning.import.assignmentColumns.allocation")}</TableHead><TableHead>{t("planning.import.assignmentColumns.dates")}</TableHead><TableHead>{t("planning.import.assignmentColumns.status")}</TableHead><TableHead /></TableRow></TableHeader><TableBody>{session.assignments.map((assignment) => {
              const task = session.tasks.find((item) => item.sourceId === assignment.taskSourceId);
              const resource = session.resources.find((item) => item.sourceId === assignment.resourceSourceId);
              const isIgnored = assignment.status === "Ignore" || assignment.status === "IgnoredByResource";
              return <TableRow key={assignment.sourceId} tabIndex={-1} ref={(node) => { const key = reviewRowKey("assignments", assignment.sourceId); if (node) reviewRows.current.set(key, node); else reviewRows.current.delete(key); }} className={reviewTarget?.tab === "assignments" && reviewTarget.sourceId === assignment.sourceId ? "outline outline-2 outline-primary/40 -outline-offset-2" : undefined}>
                <TableCell className="text-xs"><div className="font-medium">{task?.name ?? t("planning.import.unknownTask")}</div>{task?.code ? <div className="text-muted-foreground">{task.code}</div> : null}</TableCell>
                <TableCell className="text-xs"><div className="font-medium">{resource?.name ?? t("planning.import.unknownResource")}</div>{resource?.email ? <div className="text-muted-foreground">{resource.email}</div> : null}</TableCell>
                <TableCell className="whitespace-nowrap text-xs">{assignment.plannedHours == null ? "—" : t("planning.import.hoursValue", { value: assignment.plannedHours })}</TableCell>
                <TableCell className="whitespace-nowrap text-xs">{assignment.allocationPercent == null ? "—" : `${assignment.allocationPercent}%`}</TableCell>
                <TableCell className="whitespace-nowrap text-xs">{assignment.start || assignment.finish ? `${dateOnly(assignment.start)} – ${dateOnly(assignment.finish)}` : "—"}</TableCell>
                <TableCell><Badge status={assignment.status === "Ready" ? "Matched" : isIgnored ? "Ignored" : "NeedsReview"}>{t(`planning.import.assignmentStatuses.${assignment.status}`)}</Badge></TableCell>
                <TableCell>{isIgnored ? null : <Button type="button" size="sm" variant="ghost" disabled={resolving} onClick={() => void resolve("assignment", assignment.sourceId, { resolution: "Ignore" })}>{t("planning.import.ignoreAssignment")}</Button>}</TableCell>
              </TableRow>;
            })}</TableBody></Table></div></TabsContent>
            <TabsContent value="calendars"><div className="overflow-auto rounded border"><Table><TableHeader><TableRow><TableHead>{t("planning.import.calendarColumns.name")}</TableHead><TableHead>{t("planning.import.calendarColumns.shape")}</TableHead><TableHead>{t("planning.import.calendarColumns.action")}</TableHead></TableRow></TableHeader><TableBody>{session.calendars.map((calendar) => <TableRow key={calendar.sourceId} tabIndex={-1} ref={(node) => { const key = reviewRowKey("calendars", calendar.sourceId); if (node) reviewRows.current.set(key, node); else reviewRows.current.delete(key); }} className={reviewTarget?.tab === "calendars" && reviewTarget.sourceId === calendar.sourceId ? "outline outline-2 outline-primary/40 -outline-offset-2" : undefined}>
              <TableCell className="text-xs">{calendar.name}{calendar.isDefault ? <Badge className="ms-2" variant="secondary">{t("planning.import.default")}</Badge> : null}</TableCell>
              <TableCell className="text-xs">{t("planning.import.calendarShape", { days: calendar.workingDays, holidays: calendar.holidays })}</TableCell>
              <TableCell><Select value={calendar.resolution} onValueChange={(value) => void resolve("calendar", calendar.sourceId, { resolution: value })}><SelectTrigger disabled={resolving} aria-label={`${t("planning.import.calendarColumns.action")}: ${calendar.name}`} className="h-8 w-40 text-xs"><SelectValue /></SelectTrigger><SelectContent>{calendar.resolution === "Pending" ? <SelectItem value="Pending" disabled>{t("planning.import.calendarActions.needsReview")}</SelectItem> : null}<SelectItem value="Import">{t("planning.import.calendarActions.import")}</SelectItem><SelectItem value="Simplify">{t("planning.import.calendarActions.simplify")}</SelectItem><SelectItem value="Ignore">{t("planning.import.ignore")}</SelectItem></SelectContent></Select></TableCell>
            </TableRow>)}</TableBody></Table></div></TabsContent>
          </Tabs>
        </div> : null}

        {error ? <div role="alert" className="flex items-start gap-2 rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" /><span>{error}</span></div> : null}
      </SideSheetBody>
      <SideSheetFooter>
        <Button type="button" variant="outline" disabled={busy || resolving} onClick={() => session ? void cancelSession() : onOpenChange(false)}>{t("planning.import.cancel")}</Button>
        {!session ? <Button type="button" disabled={!canParse || busy} onClick={() => void parse()}>{t("planning.import.preview")}</Button> : <Button type="button" disabled={busy || resolving || session.blockingIssueCount > 0 || session.status !== "ReadyToCommit"} onClick={() => void commit()}><FileUp className="me-1.5 h-4 w-4" />{t("planning.import.confirm")}</Button>}
      </SideSheetFooter>
    </SideSheet>
  );
}

function Stat({ label, value, detail }: { label: string; value: number; detail?: string }) {
  return <div className="rounded-md border bg-background p-2"><p className="text-lg font-semibold tabular-nums">{value}</p><p className="text-[10px] text-muted-foreground">{label}</p>{detail ? <p className="mt-0.5 break-words text-[10px] leading-snug text-muted-foreground">{detail}</p> : null}</div>;
}

function NoticeList({ title, notices, t, onReview }: { title: string; notices: Array<{ id?: string; category?: string; entityType?: string; sourceId?: string; code: string; message: string; count?: number; isBlocking?: boolean }>; t: Message; onReview?: (notice: { category?: string; entityType?: string; code: string; sourceId?: string }) => void }) {
  return <div className="rounded-md border bg-muted/20 p-3"><p className="mb-2 text-xs font-semibold">{title}</p>{notices.length ? <ul className="space-y-1.5">{notices.map((notice, index) => <li key={notice.id ?? `${notice.code}-${notice.sourceId ?? ""}-${index}`} className="flex items-start gap-2 text-[11px]"><AlertTriangle className={`mt-0.5 h-3.5 w-3.5 shrink-0 ${notice.isBlocking ? "text-destructive" : "text-warning"}`} /><span className="min-w-0 flex-1">{planningImportNoticeMessage(notice, t)}{notice.count ? ` ${t("planning.import.countSuffix", { count: notice.count })}` : ""}</span>{notice.isBlocking && notice.sourceId && onReview && reviewTabForIssue(notice) ? <Button type="button" size="sm" variant="outline" className="shrink-0" onClick={() => onReview(notice)}>{t("planning.import.reviewAction")}</Button> : null}</li>)}</ul> : <p className="text-xs text-muted-foreground">—</p>}</div>;
}

function reviewTabForIssue(notice: { category?: string; entityType?: string; code: string }) {
  const category = `${notice.category ?? ""} ${notice.entityType ?? ""}`.toLowerCase();
  const code = notice.code.toLowerCase();
  if (category.includes("assignment") || code.startsWith("assignment-")) return "assignments";
  if (category.includes("resource") || code.startsWith("resource-")) return "resources";
  if (category.includes("calendar") || code.startsWith("calendar-")) return "calendars";
  return undefined;
}

function reviewRowKey(tab: string, sourceId: string) { return `${tab}:${sourceId}`; }
function dateOnly(value?: string) { return value ? value.slice(0, 10) : "—"; }
function formatBytes(bytes: number) { return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`; }
