"use client";

import { useEffect, useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import { AlertTriangle, CheckCircle2, FileUp, Loader2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Progress } from "@/components/ui/progress";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useToast } from "@/hooks/use-toast";
import { planningApi, type PlanImportSessionDto, type PlanSummaryDto, type PlanType } from "@/lib/api/planning";
import { PLANNING_PLAN_TYPES } from "@/lib/enums";

type ImportMode = "existing" | "new";
type ImportState = "ready" | "parsing" | "reviewing" | "committing";

export function PlanImportDialog({
  projectId, plans, selectedPlanId, open, onOpenChange, onImported,
}: {
  projectId: string;
  plans: PlanSummaryDto[];
  selectedPlanId: string;
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  onImported: (_planId: string) => Promise<void>;
}) {
  const t = useTranslations("projects.planning.import");
  const planning = useTranslations("projects.planning");
  const enums = useTranslations("enums");
  const { toast } = useToast();
  const [mode, setMode] = useState<ImportMode>(plans.length ? "existing" : "new");
  const [targetPlanId, setTargetPlanId] = useState(selectedPlanId || plans[0]?.id || "");
  const [newPlanName, setNewPlanName] = useState("");
  const [newPlanType, setNewPlanType] = useState<PlanType>("Custom");
  const [file, setFile] = useState<File>();
  const [session, setSession] = useState<PlanImportSessionDto>();
  const [state, setState] = useState<ImportState>("ready");
  const [tab, setTab] = useState("overview");
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (!open) {
      setFile(undefined); setSession(undefined); setError(undefined); setState("ready"); setNewPlanName(""); setTab("overview");
      return;
    }
    setMode(plans.length ? "existing" : "new");
    setTargetPlanId(selectedPlanId || plans[0]?.id || "");
  }, [open, plans, selectedPlanId]);

  const busy = state === "parsing" || state === "committing";
  const canParse = !!file && (mode === "new" ? !!newPlanType : !!targetPlanId);
  const totalRows = session ? session.statistics.tasks + session.statistics.milestones : 0;
  const unresolved = useMemo(() => session?.resources.filter((resource) => resource.resolution === "Unresolved").length ?? 0, [session]);

  const reset = () => { setSession(undefined); setError(undefined); setState("ready"); setTab("overview"); };

  const parse = async () => {
    if (!file || !canParse) return;
    setError(undefined); setState("parsing");
    try {
      const result = await planningApi.createImportSession(projectId, file, mode === "existing" ? targetPlanId : undefined);
      setSession(result); if (!newPlanName.trim()) setNewPlanName(result.suggestedPlanName); setState("reviewing");
    } catch (cause) { setError(cause instanceof Error ? cause.message : t("parseFailed")); setState("ready"); }
  };

  const update = (result: PlanImportSessionDto) => { setSession(result); setState("reviewing"); };
  const resolveResource = async (sourceId: string, body: Parameters<typeof planningApi.resolveImportResource>[3]) => {
    if (!session) return;
    try { update(await planningApi.resolveImportResource(projectId, session.id, sourceId, body)); }
    catch (cause) { setError(cause instanceof Error ? cause.message : t("resolutionFailed")); }
  };
  const resolveAssignment = async (sourceId: string, resolution: "Ready" | "Ignore") => {
    if (!session) return;
    try { update(await planningApi.resolveImportAssignment(projectId, session.id, sourceId, { resolution })); }
    catch (cause) { setError(cause instanceof Error ? cause.message : t("resolutionFailed")); }
  };
  const resolveCalendar = async (sourceId: string, resolution: "Import" | "Simplify" | "Ignore") => {
    if (!session) return;
    try { update(await planningApi.resolveImportCalendar(projectId, session.id, sourceId, { resolution })); }
    catch (cause) { setError(cause instanceof Error ? cause.message : t("resolutionFailed")); }
  };

  const commit = async () => {
    if (!session || session.blockingIssueCount > 0) return;
    setError(undefined); setState("committing");
    try {
      const result = await planningApi.commitImportSession(projectId, session.id, mode === "existing" ? { targetPlanId } : { newPlanName: newPlanName.trim() || session.suggestedPlanName, newPlanType });
      await onImported(result.planId);
      toast({ title: t("successTitle"), description: t("successDescription", { count: result.statistics.tasks + result.statistics.milestones, version: result.versionNumber }), variant: "success" });
      onOpenChange(false);
    } catch (cause) { setError(cause instanceof Error ? cause.message : t("commitFailed")); setState("reviewing"); }
  };

  return <Sheet open={open} onOpenChange={(next) => { if (!busy) onOpenChange(next); }}>
    <SheetContent side="end" className="flex w-full flex-col overflow-hidden p-0 sm:max-w-3xl lg:max-w-6xl" data-testid="planning-import-sheet">
      <SheetHeader className="shrink-0 border-b px-6 py-5 text-start">
        <SheetTitle className="flex items-center gap-2"><FileUp className="h-5 w-5" />{t("title")}</SheetTitle>
        <SheetDescription>{t("description")}</SheetDescription>
      </SheetHeader>
      <div className="min-h-0 flex-1 overflow-y-auto px-6 py-5">
        {busy && <div className="mb-4 rounded-md border bg-muted/20 p-4" role="status"><div className="mb-3 flex items-center gap-2 text-sm font-medium"><Loader2 className="h-4 w-4 animate-spin" />{state === "parsing" ? t("parsing") : t("importing")}</div><Progress value={state === "parsing" ? 45 : 78} /><p className="mt-2 text-xs text-muted-foreground">{state === "parsing" ? t("parsingHint") : t("atomicHint")}</p></div>}

        {!session && !busy && <div className="grid gap-4 lg:grid-cols-2">
          <div className="space-y-2"><label className="text-xs font-medium" htmlFor="planning-import-file">{t("fileLabel")}</label><Input id="planning-import-file" type="file" accept=".mpp,application/vnd.ms-project" onChange={(event) => { const selected = event.target.files?.[0]; setFile(selected); setError(selected && !selected.name.toLowerCase().endsWith(".mpp") ? t("mppOnly") : undefined); }} /><p className="text-[11px] text-muted-foreground">{file ? t("selectedFile", { name: file.name, size: formatBytes(file.size) }) : t("fileHint")}</p></div>
          <div className="space-y-3 rounded-md border p-3"><div className="flex gap-1 rounded-md bg-muted/40 p-1">{plans.length > 0 && <Button type="button" size="sm" className="flex-1" variant={mode === "existing" ? "secondary" : "ghost"} onClick={() => setMode("existing")}>{t("existingPlan")}</Button>}<Button type="button" size="sm" className="flex-1" variant={mode === "new" ? "secondary" : "ghost"} onClick={() => setMode("new")}>{t("newPlan")}</Button></div>
            {mode === "existing" ? <div className="space-y-2"><label className="text-xs font-medium">{t("targetPlan")}</label><Select value={targetPlanId} onValueChange={setTargetPlanId}><SelectTrigger><SelectValue placeholder={t("selectPlan")} /></SelectTrigger><SelectContent>{plans.map((plan) => <SelectItem key={plan.id} value={plan.id}>{plan.name}</SelectItem>)}</SelectContent></Select><p className="text-[11px] text-muted-foreground">{t("workingVersionRule")}</p></div> : <div className="grid gap-3 sm:grid-cols-2"><div className="space-y-2"><label className="text-xs font-medium" htmlFor="planning-import-name">{t("planName")}</label><Input id="planning-import-name" value={newPlanName} onChange={(event) => setNewPlanName(event.target.value)} placeholder={t("planNamePlaceholder")} /></div><div className="space-y-2"><label className="text-xs font-medium">{t("planType")}</label><Select value={newPlanType} onValueChange={(value) => setNewPlanType(value as PlanType)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{PLANNING_PLAN_TYPES.map((type) => <SelectItem key={type} value={type}>{enums(`planning.planType.${type}`)}</SelectItem>)}</SelectContent></Select></div></div>}
          </div>
        </div>}

        {session && !busy && <div className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-muted/20 px-3 py-2"><div><p className="text-sm font-medium">{session.fileName}</p><p className="text-xs text-muted-foreground">{session.format} · {session.suggestedPlanName}</p></div><div className="flex items-center gap-2"><Badge status={session.blockingIssueCount ? "NeedsReview" : "ReadyToCommit"}>{session.blockingIssueCount ? t("needsReview") : t("readyToCommit")}</Badge><Button type="button" size="sm" variant="outline" onClick={reset}>{t("changeFile")}</Button></div></div>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-8"><Stat label={t("stats.tasks")} value={session.statistics.tasks} /><Stat label={t("stats.milestones")} value={session.statistics.milestones} /><Stat label={t("stats.levels")} value={session.statistics.wbsLevels} /><Stat label={t("stats.dependencies")} value={session.statistics.dependencies} /><Stat label={t("stats.resources")} value={session.statistics.resources} detail={unresolved ? t("unmatched", { count: unresolved }) : undefined} /><Stat label={t("stats.assignments")} value={session.statistics.assignments} /><Stat label={t("stats.calendars")} value={session.statistics.calendars} /><Stat label={t("stats.actualDates")} value={session.statistics.actualDates} /></div>
          {session.issues.length > 0 && <NoticeList title={t("issues")} notices={session.issues.map((issue) => ({ id: issue.id, sourceId: issue.sourceId, code: issue.code, message: issue.message, count: issue.count, isBlocking: issue.isBlocking }))} />}
          <Tabs value={tab} onValueChange={setTab}><TabsList className="flex h-auto w-full justify-start gap-1 overflow-x-auto"><TabsTrigger value="overview">{t("tabs.overview")}</TabsTrigger><TabsTrigger value="tasks">{t("tabs.tasks", { count: totalRows })}</TabsTrigger><TabsTrigger value="resources">{t("tabs.resources", { count: session.resources.length })}</TabsTrigger><TabsTrigger value="assignments">{t("tabs.assignments", { count: session.assignments.length })}</TabsTrigger><TabsTrigger value="calendars">{t("tabs.calendars", { count: session.calendars.length })}</TabsTrigger></TabsList>
            <TabsContent value="overview" className="space-y-3"><NoticeList title={t("warnings")} notices={session.warnings} /><NoticeList title={t("unsupported")} notices={session.unsupportedFields} /></TabsContent>
            <TabsContent value="tasks"><div className="nexus-table-surface max-h-[32rem] overflow-auto"><Table density="compact"><TableHeader><TableRow><TableHead>{t("columns.wbs")}</TableHead><TableHead>{t("columns.name")}</TableHead><TableHead>{t("columns.duration")}</TableHead><TableHead>{t("columns.start")}</TableHead><TableHead>{t("columns.finish")}</TableHead><TableHead>{t("columns.constraintType")}</TableHead><TableHead>{t("columns.deadline")}</TableHead><TableHead>{t("columns.resources")}</TableHead></TableRow></TableHeader><TableBody>{session.tasks.map((task) => <TableRow key={task.sourceId}><TableCell className="text-xs">{task.code}</TableCell><TableCell><div className="flex items-center gap-1" style={{ paddingInlineStart: `${Math.max(0, task.outlineLevel - 1) * 14}px` }}>{task.isMilestone && <span aria-hidden>◆</span>}<span className="text-xs font-medium">{task.name}</span></div></TableCell><TableCell className="text-xs">{task.durationDays}d</TableCell><TableCell className="text-xs">{dateOnly(task.start)}</TableCell><TableCell className="text-xs">{dateOnly(task.finish)}</TableCell><TableCell className="text-xs">{planning(`constraintTypes.${task.constraintType}`)}</TableCell><TableCell className="text-xs">{dateOnly(task.deadline)}</TableCell><TableCell className="max-w-48 text-xs">{task.resources.join(", ") || "—"}</TableCell></TableRow>)}</TableBody></Table></div></TabsContent>
            <TabsContent value="resources"><div className="nexus-table-surface overflow-auto"><Table density="compact"><TableHeader><TableRow><TableHead>{t("resourceColumns.source")}</TableHead><TableHead>{t("resourceColumns.match")}</TableHead><TableHead>{t("resourceColumns.candidates")}</TableHead><TableHead>{t("resourceColumns.action")}</TableHead></TableRow></TableHeader><TableBody>{session.resources.map((resource) => <TableRow key={resource.sourceId}><TableCell><div className="text-xs font-medium">{resource.name}</div><div className="text-[10px] text-muted-foreground">{resource.email || t("noEmail")} · {t("assignmentCount", { count: resource.assignmentCount })}</div></TableCell><TableCell>{resource.resolution === "MapExisting" ? <Badge status="Matched"><CheckCircle2 className="mr-1 h-3 w-3" />{t("matched")}</Badge> : <Badge status="NeedsReview"><AlertTriangle className="mr-1 h-3 w-3" />{t("needsReview")}</Badge>}</TableCell><TableCell className="min-w-56">{resource.candidates.length ? <Select value={resource.nexusResourceId || "none"} onValueChange={(value) => { if (value !== "none") void resolveResource(resource.sourceId, { resolution: "MapExisting", resourceId: value }); }}><SelectTrigger className="h-8 text-xs"><SelectValue placeholder={t("selectResource")} /></SelectTrigger><SelectContent><SelectItem value="none">{t("selectResource")}</SelectItem>{resource.candidates.map((candidate) => <SelectItem key={candidate.id} value={candidate.id}>{candidate.name}{candidate.email ? ` · ${candidate.email}` : ""}</SelectItem>)}</SelectContent></Select> : <span className="text-xs text-muted-foreground">{t("noCandidates")}</span>}</TableCell><TableCell className="whitespace-nowrap"><div className="flex gap-1"><Button type="button" size="sm" variant="outline" onClick={() => void resolveResource(resource.sourceId, { resolution: "CreateNew", createResource: { name: resource.name, type: "Staff", status: "Active", email: resource.email } })}>{t("createResource")}</Button><Button type="button" size="sm" variant="ghost" onClick={() => void resolveResource(resource.sourceId, { resolution: "Ignore" })}>{t("ignore")}</Button></div></TableCell></TableRow>)}</TableBody></Table></div></TabsContent>
            <TabsContent value="assignments"><div className="nexus-table-surface overflow-auto"><Table density="compact"><TableHeader><TableRow><TableHead>{t("assignmentColumns.source")}</TableHead><TableHead>{t("assignmentColumns.work")}</TableHead><TableHead>{t("assignmentColumns.status")}</TableHead><TableHead /></TableRow></TableHeader><TableBody>{session.assignments.map((assignment) => <TableRow key={assignment.sourceId}><TableCell className="text-xs"><div>{assignment.taskSourceId}</div><div className="text-muted-foreground">{assignment.resourceSourceId}</div></TableCell><TableCell className="text-xs">{assignment.plannedHours ?? "—"}h</TableCell><TableCell><Badge status={assignment.status === "Ready" ? "Matched" : "NeedsReview"}>{assignment.status}</Badge></TableCell><TableCell>{assignment.status !== "Ready" && assignment.status !== "IgnoredByResource" && <Button type="button" size="sm" variant="ghost" onClick={() => void resolveAssignment(assignment.sourceId, "Ignore")}>{t("ignoreAssignment")}</Button>}</TableCell></TableRow>)}</TableBody></Table></div></TabsContent>
            <TabsContent value="calendars"><div className="nexus-table-surface overflow-auto"><Table density="compact"><TableHeader><TableRow><TableHead>{t("calendarColumns.name")}</TableHead><TableHead>{t("calendarColumns.shape")}</TableHead><TableHead>{t("calendarColumns.action")}</TableHead></TableRow></TableHeader><TableBody>{session.calendars.map((calendar) => <TableRow key={calendar.sourceId}><TableCell className="text-xs">{calendar.name}{calendar.isDefault && <Badge className="ml-2" variant="secondary">{t("default")}</Badge>}</TableCell><TableCell className="text-xs">{t("calendarShape", { days: calendar.workingDays, holidays: calendar.holidays })}</TableCell><TableCell><Select value={calendar.resolution} onValueChange={(value) => void resolveCalendar(calendar.sourceId, value as "Import" | "Simplify" | "Ignore")}><SelectTrigger className="h-8 w-36 text-xs"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Import">{t("calendarActions.import")}</SelectItem><SelectItem value="Simplify">{t("calendarActions.simplify")}</SelectItem><SelectItem value="Ignore">{t("ignore")}</SelectItem></SelectContent></Select></TableCell></TableRow>)}</TableBody></Table></div></TabsContent>
          </Tabs>
        </div>}
        {error && <div role="alert" className="mt-4 flex items-start gap-2 rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" /><span>{error}</span></div>}
      </div>
      <SheetFooter className="shrink-0 border-t px-6 py-4"><Button type="button" variant="outline" disabled={busy} onClick={() => onOpenChange(false)}>{t("cancel")}</Button>{!session ? <Button type="button" disabled={!canParse || busy || (!!file && !file.name.toLowerCase().endsWith(".mpp"))} onClick={() => void parse()}>{t("preview")}</Button> : <Button type="button" disabled={busy || session.blockingIssueCount > 0 || session.status !== "ReadyToCommit"} onClick={() => void commit()}><FileUp className="mr-1.5 h-4 w-4" />{t("confirm")}</Button>}</SheetFooter>
    </SheetContent>
  </Sheet>;
}

function Stat({ label, value, detail }: { label: string; value: number; detail?: string }) { return <div className="rounded-md border bg-background p-2"><p className="text-lg font-semibold tabular-nums">{value}</p><p className="text-[10px] text-muted-foreground">{label}</p>{detail && <p className="mt-0.5 text-[9px] text-amber-700">{detail}</p>}</div>; }
function NoticeList({ title, notices }: { title: string; notices: Array<{ id?: string; sourceId?: string; code: string; message: string; count: number; isBlocking?: boolean }> }) { return <div className="rounded-md border bg-muted/20 p-3"><p className="mb-2 text-xs font-semibold">{title}</p>{notices.length ? <ul className="space-y-1.5">{notices.map((notice, index) => <li key={notice.id ?? `${notice.code}-${notice.sourceId ?? ""}-${notice.message}-${index}`} className="flex gap-2 text-[11px]"><AlertTriangle className={`mt-0.5 h-3.5 w-3.5 shrink-0 ${notice.isBlocking ? "text-destructive" : "text-amber-600"}`} /><span>{notice.message}{notice.count > 0 ? ` (${notice.count})` : ""}</span></li>)}</ul> : <p className="text-xs text-muted-foreground">—</p>}</div>; }
function dateOnly(value?: string) { return value ? value.slice(0, 10) : "—"; }
function formatBytes(bytes: number) { return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`; }
