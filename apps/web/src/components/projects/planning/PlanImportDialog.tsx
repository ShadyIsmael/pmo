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
import { useToast } from "@/hooks/use-toast";
import { planningApi, type PlanImportPreviewDto, type PlanSummaryDto, type PlanType } from "@/lib/api/planning";
import { PLANNING_PLAN_TYPES } from "@/lib/enums";

type ImportMode = "existing" | "new";
type ImportState = "ready" | "parsing" | "preview" | "importing";

export function PlanImportDialog({
  projectId,
  plans,
  selectedPlanId,
  open,
  onOpenChange,
  onImported,
}: {
  projectId: string;
  plans: PlanSummaryDto[];
  selectedPlanId: string;
  open: boolean;
  onOpenChange: (_open: boolean) => void;
  onImported: (_planId: string) => Promise<void>;
}) {
  const t = useTranslations("projects.planning.import");
  const enums = useTranslations("enums");
  const { toast } = useToast();
  const [mode, setMode] = useState<ImportMode>(plans.length ? "existing" : "new");
  const [targetPlanId, setTargetPlanId] = useState(selectedPlanId || plans[0]?.id || "");
  const [newPlanName, setNewPlanName] = useState("");
  const [newPlanType, setNewPlanType] = useState<PlanType>("Custom");
  const [file, setFile] = useState<File>();
  const [state, setState] = useState<ImportState>("ready");
  const [preview, setPreview] = useState<PlanImportPreviewDto>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (!open) {
      setFile(undefined);
      setPreview(undefined);
      setError(undefined);
      setState("ready");
      setNewPlanName("");
      return;
    }
    setMode(plans.length ? "existing" : "new");
    setTargetPlanId(selectedPlanId || plans[0]?.id || "");
  }, [open, plans, selectedPlanId]);

  const busy = state === "parsing" || state === "importing";
  const canPreview = !!file && (mode === "new" ? !!newPlanType : !!targetPlanId);
  const totalRows = preview ? preview.statistics.tasks + preview.statistics.milestones : 0;
  const unmatchedResources = useMemo(() => preview?.resources.filter((resource) => resource.matchStatus === "Unmatched").length ?? 0, [preview]);

  const resetPreview = () => {
    setPreview(undefined);
    setError(undefined);
    setState("ready");
  };

  const parse = async () => {
    if (!file || !canPreview) return;
    setError(undefined);
    setState("parsing");
    try {
      const result = await planningApi.previewImport(projectId, file, mode === "existing" ? targetPlanId : undefined);
      setPreview(result);
      if (!newPlanName.trim()) setNewPlanName(result.suggestedPlanName);
      setState("preview");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("parseFailed"));
      setState("ready");
    }
  };

  const commit = async () => {
    if (!file || !preview) return;
    setError(undefined);
    setState("importing");
    try {
      const result = await planningApi.commitImport(projectId, file, mode === "existing"
        ? { targetPlanId }
        : { newPlanName: newPlanName.trim() || preview.suggestedPlanName, newPlanType });
      await onImported(result.planId);
      toast({ title: t("successTitle"), description: t("successDescription", { count: result.statistics.tasks + result.statistics.milestones, version: result.versionNumber }), variant: "success" });
      onOpenChange(false);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t("commitFailed"));
      setState("preview");
    }
  };

  return <Sheet open={open} onOpenChange={(next) => { if (!busy) onOpenChange(next); }}>
    <SheetContent side="end" className="flex w-full flex-col overflow-hidden p-0 sm:max-w-3xl lg:max-w-5xl" data-testid="planning-import-sheet">
      <SheetHeader className="shrink-0 border-b px-6 py-5 text-start">
        <SheetTitle className="flex items-center gap-2"><FileUp className="h-5 w-5" />{t("title")}</SheetTitle>
        <SheetDescription>{t("description")}</SheetDescription>
      </SheetHeader>
      <div className="min-h-0 flex-1 overflow-y-auto px-6 py-5">
        <div className="space-y-4">
        {busy && <div className="rounded-md border bg-muted/20 p-4" role="status">
          <div className="mb-3 flex items-center gap-2 text-sm font-medium"><Loader2 className="h-4 w-4 animate-spin" />{state === "parsing" ? t("parsing") : t("importing")}</div>
          <Progress value={state === "parsing" ? 45 : 78} />
          <p className="mt-2 text-xs text-muted-foreground">{state === "parsing" ? t("parsingHint") : t("atomicHint")}</p>
        </div>}

        {!preview && !busy && <div className="grid gap-4 lg:grid-cols-2">
          <div className="space-y-2">
            <label className="text-xs font-medium" htmlFor="planning-import-file">{t("fileLabel")}</label>
            <Input id="planning-import-file" type="file" accept=".mpp,application/vnd.ms-project" onChange={(event) => {
              const selected = event.target.files?.[0];
              setFile(selected);
              setError(selected && !selected.name.toLowerCase().endsWith(".mpp") ? t("mppOnly") : undefined);
            }} />
            <p className="text-[11px] text-muted-foreground">{file ? t("selectedFile", { name: file.name, size: formatBytes(file.size) }) : t("fileHint")}</p>
          </div>
          <div className="space-y-3 rounded-md border p-3">
            <div className="flex gap-1 rounded-md bg-muted/40 p-1">
              {plans.length > 0 && <Button type="button" size="sm" className="flex-1" variant={mode === "existing" ? "secondary" : "ghost"} onClick={() => { setMode("existing"); resetPreview(); }}>{t("existingPlan")}</Button>}
              <Button type="button" size="sm" className="flex-1" variant={mode === "new" ? "secondary" : "ghost"} onClick={() => { setMode("new"); resetPreview(); }}>{t("newPlan")}</Button>
            </div>
            {mode === "existing" ? <div className="space-y-2">
              <label className="text-xs font-medium">{t("targetPlan")}</label>
              <Select value={targetPlanId} onValueChange={setTargetPlanId}>
                <SelectTrigger><SelectValue placeholder={t("selectPlan")} /></SelectTrigger>
                <SelectContent>{plans.map((plan) => <SelectItem key={plan.id} value={plan.id}>{plan.name}</SelectItem>)}</SelectContent>
              </Select>
              <p className="text-[11px] text-muted-foreground">{t("workingVersionRule")}</p>
            </div> : <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-2"><label className="text-xs font-medium" htmlFor="planning-import-name">{t("planName")}</label><Input id="planning-import-name" value={newPlanName} onChange={(event) => setNewPlanName(event.target.value)} placeholder={t("planNamePlaceholder")} /></div>
              <div className="space-y-2"><label className="text-xs font-medium">{t("planType")}</label><Select value={newPlanType} onValueChange={(value) => setNewPlanType(value as PlanType)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{PLANNING_PLAN_TYPES.map((type) => <SelectItem key={type} value={type}>{enums(`planning.planType.${type}`)}</SelectItem>)}</SelectContent></Select></div>
            </div>}
          </div>
        </div>}

        {preview && !busy && <>
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-muted/20 px-3 py-2">
            <div><p className="text-sm font-medium">{preview.fileName}</p><p className="text-xs text-muted-foreground">{preview.format} · {preview.suggestedPlanName}</p></div>
            <Button type="button" size="sm" variant="outline" onClick={resetPreview}>{t("changeFile")}</Button>
          </div>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-8">
            <Stat label={t("stats.tasks")} value={preview.statistics.tasks} />
            <Stat label={t("stats.milestones")} value={preview.statistics.milestones} />
            <Stat label={t("stats.levels")} value={preview.statistics.wbsLevels} />
            <Stat label={t("stats.dependencies")} value={preview.statistics.dependencies} />
            <Stat label={t("stats.resources")} value={preview.statistics.resources} detail={unmatchedResources ? t("unmatched", { count: unmatchedResources }) : undefined} />
            <Stat label={t("stats.assignments")} value={preview.statistics.assignments} />
            <Stat label={t("stats.calendars")} value={preview.statistics.calendars} />
            <Stat label={t("stats.actualDates")} value={preview.statistics.actualDates} />
          </div>

          {(preview.warnings.length > 0 || preview.unsupportedFields.length > 0) && <div className="grid gap-3 lg:grid-cols-2">
            <NoticeList title={t("warnings")} notices={preview.warnings} warning />
            <NoticeList title={t("unsupported")} notices={preview.unsupportedFields} />
          </div>}

          <div className="nexus-table-surface">
            <div className="flex items-center justify-between border-b px-3 py-2"><p className="text-xs font-semibold">{t("taskPreview")}</p><Badge variant="secondary">{t("rows", { count: totalRows })}</Badge></div>
            <div className="max-h-72 overflow-auto">
              <Table density="compact">
                <TableHeader><TableRow><TableHead className="w-20">{t("columns.wbs")}</TableHead><TableHead>{t("columns.name")}</TableHead><TableHead>{t("columns.duration")}</TableHead><TableHead>{t("columns.start")}</TableHead><TableHead>{t("columns.finish")}</TableHead><TableHead>{t("columns.constraintType")}</TableHead><TableHead>{t("columns.constraintDate")}</TableHead><TableHead>{t("columns.deadline")}</TableHead><TableHead>{t("columns.actualStart")}</TableHead><TableHead>{t("columns.actualFinish")}</TableHead><TableHead>{t("columns.complete")}</TableHead><TableHead>{t("columns.predecessors")}</TableHead><TableHead>{t("columns.resources")}</TableHead></TableRow></TableHeader>
                <TableBody>{preview.tasks.map((task) => <TableRow key={task.sourceId}>
                  <TableCell className="whitespace-nowrap text-xs">{task.code}</TableCell>
                  <TableCell><div className="flex items-center gap-1" style={{ paddingInlineStart: `${Math.max(0, task.outlineLevel - 1) * 14}px` }}>{task.isMilestone && <span aria-hidden>◆</span>}<span className="text-xs font-medium">{task.name}</span></div></TableCell>
                  <TableCell className="whitespace-nowrap text-xs">{task.durationDays}d</TableCell>
                  <TableCell className="whitespace-nowrap text-xs">{dateOnly(task.start)}</TableCell>
                  <TableCell className="whitespace-nowrap text-xs">{dateOnly(task.finish)}</TableCell>
                  <TableCell className="whitespace-nowrap text-xs">{t(`constraintTypes.${task.constraintType}`)}</TableCell>
                  <TableCell className="whitespace-nowrap text-xs">{dateOnly(task.constraintDate)}</TableCell>
                  <TableCell className="whitespace-nowrap text-xs">{dateOnly(task.deadline)}</TableCell>
                  <TableCell className="whitespace-nowrap text-xs">{dateOnly(task.actualStart)}</TableCell>
                  <TableCell className="whitespace-nowrap text-xs">{dateOnly(task.actualFinish)}</TableCell>
                  <TableCell className="text-xs">{task.percentComplete ?? 0}%</TableCell>
                  <TableCell className="max-w-44 text-xs">{task.predecessors.join(", ") || "—"}</TableCell>
                  <TableCell className="max-w-48 text-xs">{task.resources.join(", ") || "—"}</TableCell>
                </TableRow>)}</TableBody>
              </Table>
            </div>
          </div>

          {preview.resources.length > 0 && <div className="rounded-md border p-3"><p className="mb-2 text-xs font-semibold">{t("resourcePreview")}</p><div className="flex flex-wrap gap-2">{preview.resources.map((resource) => <Badge key={resource.sourceId} status={resource.matchStatus} className="gap-1"><span>{resource.name}</span>{resource.matchStatus === "Matched" ? <CheckCircle2 className="h-3 w-3 text-green-600" /> : <AlertTriangle className="h-3 w-3 text-amber-600" />}</Badge>)}</div></div>}
        </>}

        {error && <div role="alert" className="flex items-start gap-2 rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" /><span>{error}</span></div>}
        </div>
      </div>
      <SheetFooter className="shrink-0 border-t px-6 py-4">
        <Button type="button" variant="outline" disabled={busy} onClick={() => onOpenChange(false)}>{t("cancel")}</Button>
        {!preview ? <Button type="button" disabled={!canPreview || busy || (!!file && !file.name.toLowerCase().endsWith(".mpp"))} onClick={() => void parse()}>{t("preview")}</Button>
          : <Button type="button" disabled={busy} onClick={() => void commit()}><FileUp className="mr-1.5 h-4 w-4" />{t("confirm")}</Button>}
      </SheetFooter>
    </SheetContent>
  </Sheet>;
}

function Stat({ label, value, detail }: { label: string; value: number; detail?: string }) {
  return <div className="rounded-md border bg-background p-2"><p className="text-lg font-semibold tabular-nums">{value}</p><p className="text-[10px] text-muted-foreground">{label}</p>{detail && <p className="mt-0.5 text-[9px] text-amber-700">{detail}</p>}</div>;
}

function NoticeList({ title, notices, warning = false }: { title: string; notices: Array<{ code: string; message: string; count: number }>; warning?: boolean }) {
  return <div className={`rounded-md border p-3 ${warning ? "border-amber-300 bg-amber-50/50 dark:bg-amber-950/10" : "bg-muted/20"}`}><p className="mb-2 text-xs font-semibold">{title}</p><ul className="space-y-1.5">{notices.map((notice) => <li key={`${notice.code}-${notice.message}`} className="flex gap-2 text-[11px]"><AlertTriangle className={`mt-0.5 h-3.5 w-3.5 shrink-0 ${warning ? "text-amber-600" : "text-muted-foreground"}`} /><span>{notice.message}{notice.count > 0 ? ` (${notice.count})` : ""}</span></li>)}</ul></div>;
}

function dateOnly(value?: string) { return value ? value.slice(0, 10) : "—"; }
function formatBytes(bytes: number) { return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`; }
