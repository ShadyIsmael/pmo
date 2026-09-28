"use client";

import { useEffect, useState, type ReactNode } from "react";
import { ClipboardList, Download, Eye, Loader2, Pencil, Plus, Send, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { FormSheet, FormSheetBody, FormSheetFooter } from "@/components/ui/form-sheet";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { useProjectEnumLabels } from "@/hooks/projects/useProjectEnumLabels";
import { useCreateProjectStatusReport, useDeleteProjectStatusReport, useProjectStatusReport, useProjectStatusReportCurrentProgress, useProjectStatusReports, usePublishProjectStatusReport, useUpdateProjectStatusReport, type ProjectStatusReport, type ProjectStatusReportInput } from "@/hooks/projects/useProjectStatusReports";
import { useAuth } from "@/lib/auth";
import type { ProjectDto } from "@/hooks/projects/useProjects";
import { PROJECT_STATUSES } from "@/lib/enums";
import { hasPermission, Permissions } from "@/lib/permissions";
import { formatDate, formatDateTime, formatNullableCurrency, formatNullablePercentage } from "@/lib/utils";

interface Props { projectId: string; project: ProjectDto; canManage: boolean; enabled?: boolean; }
type FormValues = ProjectStatusReportInput;

const emptyValues = (): FormValues => ({ reportDate: new Date().toISOString().slice(0, 10), periodFrom: new Date().toISOString().slice(0, 10), periodTo: new Date().toISOString().slice(0, 10), overallHealth: "Green", overallProgressPercent: undefined, scheduleHealth: "", costHealth: "", scopeHealth: "", qualityHealth: "", resourceHealth: "", executiveSummary: "", keyAchievements: "", challengesConcerns: "", nextPeriodPriorities: "" });
const dateInput = (value?: string) => value ? value.slice(0, 10) : "";
const healthOptions = ["Green", "Amber", "Red"] as const;

export function ProjectStatusReports({ projectId, project, canManage, enabled = true }: Props) {
  const t = useTranslations("projects");
  const { healthLabel, statusLabel } = useProjectEnumLabels();
  const reportsQuery = useProjectStatusReports(projectId, enabled);
  const { user } = useAuth();
  const canExport = Boolean(user && hasPermission(user.permissions, Permissions.Project.View) && hasPermission(user.permissions, Permissions.Reports.Export));
  const [formOpen, setFormOpen] = useState(false);
  const [editingReport, setEditingReport] = useState<ProjectStatusReport | null>(null);
  const [detailId, setDetailId] = useState<string | null>(null);
  const detailQuery = useProjectStatusReport(projectId, detailId, Boolean(detailId));

  const openCreate = () => { setEditingReport(null); setFormOpen(true); };
  const openDetail = (reportId: string) => setDetailId(reportId);

  return <>
    <Card className="shadow-none">
       <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
         <div><h2 className="text-base font-semibold">{t("statusReportsTitle")}</h2><p className="text-sm text-muted-foreground">{t("statusReportsDescription")}</p></div>
         <div className="flex items-center gap-2 text-sm text-muted-foreground"><span>{t("projectStatus")}:</span><Badge status={project.status}>{statusLabel(project.status)}</Badge></div>
        {canManage ? <Button size="sm" onClick={openCreate}><Plus className="mr-1.5 h-3.5 w-3.5" />{t("newStatusReport")}</Button> : null}
      </CardHeader>
      <CardContent>
        {reportsQuery.isLoading ? <Skeleton className="h-28 w-full" /> : reportsQuery.isError ? <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">{t("statusReportsLoadFailed")} <Button variant="outline" size="sm" className="ml-2" onClick={() => void reportsQuery.refetch()}>{t("retry")}</Button></div> : reportsQuery.data?.items.length ? <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("reportDate")}</TableHead><TableHead>{t("reportingPeriod")}</TableHead><TableHead>{t("projectStatusAtReport")}</TableHead><TableHead>{t("health")}</TableHead><TableHead>{t("progress")}</TableHead><TableHead>{t("reportState")}</TableHead><TableHead>{t("preparedBy")}</TableHead><TableHead>{t("publishedAt")}</TableHead><TableHead className="text-right">{t("view")}</TableHead></TableRow></TableHeader><TableBody>{reportsQuery.data.items.map((report) => <TableRow key={report.id}><TableCell>{formatDate(report.reportDate)}</TableCell><TableCell>{formatDate(report.periodFrom)} – {formatDate(report.periodTo)}</TableCell><TableCell><Badge status={report.projectLifecycleStatusAtReport}>{statusLabel(report.projectLifecycleStatusAtReport)}</Badge></TableCell><TableCell><Badge status={report.overallHealth}>{healthLabel(report.overallHealth)}</Badge></TableCell><TableCell>{formatNullablePercentage(report.overallProgressPercent, 1)}</TableCell><TableCell><Badge status={report.reportState}>{t(`statusReportStates.${report.reportState}`)}</Badge></TableCell><TableCell>{report.preparedByNameSnapshot || "—"}</TableCell><TableCell>{formatDate(report.publishedAt)}</TableCell><TableCell className="text-right"><Button variant="ghost" size="sm" onClick={() => openDetail(report.id)}><Eye className="mr-1.5 h-3.5 w-3.5" />{t("view")}</Button></TableCell></TableRow>)}</TableBody></Table></div> : <div className="py-8 text-center"><ClipboardList className="mx-auto h-8 w-8 text-muted-foreground/60" /><p className="mt-3 text-sm font-medium">{t("noStatusReports")}</p><p className="mt-1 text-sm text-muted-foreground">{t("noStatusReportsDescription")}</p>{canManage ? <Button size="sm" className="mt-4" onClick={openCreate}><Plus className="mr-1.5 h-3.5 w-3.5" />{t("newStatusReport")}</Button> : null}</div>}
      </CardContent>
    </Card>
    <StatusReportFormSheet open={formOpen} onOpenChange={setFormOpen} project={project} report={editingReport} latestProjectStatusAtReport={reportsQuery.data?.items[0]?.projectLifecycleStatusAtReport} preparedByName={editingReport?.preparedByNameSnapshot ?? user?.fullName} />
    <StatusReportDetails open={Boolean(detailId)} onOpenChange={(open) => { if (!open) setDetailId(null); }} report={detailQuery.data} publishedHistory={reportsQuery.data?.items ?? []} loading={detailQuery.isLoading} error={detailQuery.isError} canManage={canManage} canExport={canExport} onEdit={() => { if (detailQuery.data) { setEditingReport(detailQuery.data); setFormOpen(true); setDetailId(null); } }} />
  </>;
}

function StatusReportFormSheet({ open, onOpenChange, project, report, latestProjectStatusAtReport, preparedByName }: { open: boolean; onOpenChange: (open: boolean) => void; project: ProjectDto; report: ProjectStatusReport | null; latestProjectStatusAtReport?: string; preparedByName?: string }) {
  const t = useTranslations("projects");
  const create = useCreateProjectStatusReport(project.id);
  const update = useUpdateProjectStatusReport(project.id, report?.id ?? "");
  const [values, setValues] = useState<FormValues>(emptyValues);
  const [error, setError] = useState<string | null>(null);
  const { healthLabel, statusLabel } = useProjectEnumLabels();
  const isEdit = Boolean(report);
  const progressQuery = useProjectStatusReportCurrentProgress(project.id, open);
  const projectPlanProgress = progressQuery.data?.projectPlanProgressPercent;

  useEffect(() => {
    if (!open) return;
    setError(null);
    setValues(report ? { reportDate: dateInput(report.reportDate), periodFrom: dateInput(report.periodFrom), periodTo: dateInput(report.periodTo), projectLifecycleStatusAtReport: report.projectLifecycleStatusAtReport ?? project.status, overallHealth: report.overallHealth, overallProgressPercent: projectPlanProgress ?? report.overallProgressPercent, scheduleHealth: report.scheduleHealth, costHealth: report.costHealth, scopeHealth: report.scopeHealth, qualityHealth: report.qualityHealth, resourceHealth: report.resourceHealth, executiveSummary: report.executiveSummary ?? "", keyAchievements: report.keyAchievements ?? "", challengesConcerns: report.challengesConcerns ?? "", nextPeriodPriorities: report.nextPeriodPriorities ?? "" } : { ...emptyValues(), projectLifecycleStatusAtReport: latestProjectStatusAtReport ?? project.status, overallProgressPercent: projectPlanProgress ?? project.progress });
  }, [latestProjectStatusAtReport, open, project.progress, project.status, projectPlanProgress, report]);

  const set = (key: keyof FormValues, value: string | number | undefined) => setValues((current) => ({ ...current, [key]: value }));
  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); setError(null);
    if (!values.reportDate || !values.periodFrom || !values.periodTo || values.periodFrom > values.periodTo) { setError(t("statusReportPeriodInvalid")); return; }
    try { if (isEdit) await update.mutateAsync({ ...values, rowVersion: report?.rowVersion }); else await create.mutateAsync(values); onOpenChange(false); } catch (err) { setError(err instanceof Error ? err.message : t("statusReportSaveFailed")); }
  };
  const pending = create.isPending || update.isPending;
  const healthFields = [{ key: "scheduleHealth", label: t("scheduleHealth") }, { key: "costHealth", label: t("costHealth") }, { key: "scopeHealth", label: t("scopeHealth") }, { key: "qualityHealth", label: t("qualityHealth") }, { key: "resourceHealth", label: t("resourceHealth") }] as const;
  return <FormSheet open={open} onOpenChange={onOpenChange} title={isEdit ? t("editStatusReport") : t("newStatusReport")} description={t("statusReportFormDescription")} size="lg"><form onSubmit={submit} className="flex min-h-0 flex-1 flex-col"><FormSheetBody className="space-y-6">
    <section className="space-y-3"><h3 className="text-sm font-semibold">{t("reportingPeriodSection")}</h3><div className="rounded-lg border bg-muted/20 p-3 text-xs text-muted-foreground"><div className="grid gap-2 sm:grid-cols-2"><span>{t("project")}: <strong className="text-foreground">{project.name} ({project.code})</strong></span><span>{t("projectManager")}: <strong className="text-foreground">{project.projectManagerName || "—"}</strong></span><span>{t("projectStatus")}: <strong className="text-foreground">{statusLabel(project.status)}</strong></span><span>{t("preparedBy")}: <strong className="text-foreground">{preparedByName || "—"}</strong></span><span>{t("health")}: <strong className="text-foreground">{healthLabel(project.health)} · {formatNullablePercentage(projectPlanProgress ?? project.progress, 1)}</strong></span></div></div><div className="grid gap-3 sm:grid-cols-4"><Field label={t("reportDate")}><Input type="date" value={values.reportDate} onChange={(e) => set("reportDate", e.target.value)} required /></Field><Field label={t("periodFrom")}><Input type="date" value={values.periodFrom} onChange={(e) => set("periodFrom", e.target.value)} required /></Field><Field label={t("periodTo")}><Input type="date" value={values.periodTo} onChange={(e) => set("periodTo", e.target.value)} required /></Field><Field label={t("projectStatusAtReport")}><StatusSelect value={values.projectLifecycleStatusAtReport} onChange={(v) => set("projectLifecycleStatusAtReport", v)} statusLabel={statusLabel} /></Field></div></section>
    <section className="space-y-3"><h3 className="text-sm font-semibold">{t("overallStatusSection")}</h3><div className="grid gap-3 sm:grid-cols-2"><Field label={t("overallHealth")}><HealthSelect value={values.overallHealth} onChange={(v) => set("overallHealth", v)} t={t} /></Field><Field label={t("overallProgress")}><Input type="number" min={0} max={100} step="0.1" value={values.overallProgressPercent ?? ""} readOnly /></Field></div></section>
    <section className="space-y-3"><h3 className="text-sm font-semibold">{t("healthDimensionsSection")}</h3><div className="grid gap-3 sm:grid-cols-2">{healthFields.map((field) => <Field key={field.key} label={field.label}><HealthSelect value={values[field.key]} onChange={(v) => set(field.key, v)} t={t} /></Field>)}</div></section>
    <section className="space-y-3"><h3 className="text-sm font-semibold">{t("narrativesSection")}</h3><NarrativeField label={t("executiveSummary")} value={values.executiveSummary} onChange={(v) => set("executiveSummary", v)} /><NarrativeField label={t("keyAchievements")} value={values.keyAchievements} onChange={(v) => set("keyAchievements", v)} /><NarrativeField label={t("challengesConcerns")} value={values.challengesConcerns} onChange={(v) => set("challengesConcerns", v)} /><NarrativeField label={t("nextPeriodPriorities")} value={values.nextPeriodPriorities} onChange={(v) => set("nextPeriodPriorities", v)} /></section>
    {error ? <p className="text-sm text-destructive">{error}</p> : null}
  </FormSheetBody><FormSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("cancel")}</Button><Button type="submit" disabled={pending}>{pending ? <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" /> : null}{pending ? t("statusReportSaving") : t("saveDraft")}</Button></FormSheetFooter></form></FormSheet>;
}

function StatusReportDetails({ open, onOpenChange, report, publishedHistory, loading, error, canManage, canExport, onEdit }: { open: boolean; onOpenChange: (open: boolean) => void; report?: ProjectStatusReport; publishedHistory: readonly import("@/hooks/projects/useProjectStatusReports").ProjectStatusReportListDto[]; loading: boolean; error: boolean; canManage: boolean; canExport: boolean; onEdit: () => void }) {
  const t = useTranslations("projects");
  const { healthLabel, statusLabel } = useProjectEnumLabels();
  const publish = usePublishProjectStatusReport(report?.projectId ?? "");
  const remove = useDeleteProjectStatusReport(report?.projectId ?? "");
  const [actionError, setActionError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);
  const isDraft = report?.reportState === "Draft";
  const health = (value?: string) => <Badge status={value}>{healthLabel(value)}</Badge>;
  const publishReport = async () => { if (!report || !window.confirm(t("publishStatusReportConfirm"))) return; setActionError(null); try { await publish.mutateAsync(report.id); } catch (err) { setActionError(err instanceof Error ? err.message : t("statusReportPublishFailed")); } };
  const deleteReport = async () => { if (!report || !window.confirm(t("deleteStatusReportConfirm"))) return; setActionError(null); try { await remove.mutateAsync(report.id); onOpenChange(false); } catch (err) { setActionError(err instanceof Error ? err.message : t("statusReportDeleteFailed")); } };
  const exportReport = async () => { if (!report || report.reportState !== "Published" || !canExport || exporting) return; setActionError(null); setExporting(true); try { const { exportProjectStatusReportPdf } = await import("@/lib/export/project-status-report-export"); await exportProjectStatusReportPdf({ report, publishedHistory, locale: document.documentElement.lang }); } catch { setActionError(t("statusReportExportFailed")); } finally { setExporting(false); } };
  const healthDimensions: Array<[string, string | undefined]> = [[t("scheduleHealth"), report?.scheduleHealth], [t("costHealth"), report?.costHealth], [t("scopeHealth"), report?.scopeHealth], [t("qualityHealth"), report?.qualityHealth], [t("resourceHealth"), report?.resourceHealth]];
  return <Dialog open={open} onOpenChange={onOpenChange}><DialogContent className="max-w-4xl" style={{ left: "50%", top: "50%", transform: "translate(-50%, -50%)", animation: "none" }}><DialogHeader><DialogTitle>{report?.projectNameSnapshot || t("statusReportDetails")}</DialogTitle><DialogDescription>{report ? `${formatDate(report.periodFrom)} – ${formatDate(report.periodTo)} · ${t(`statusReportStates.${report.reportState}`)}` : t("statusReportDetails")}</DialogDescription></DialogHeader><DialogBody>{loading ? <Skeleton className="h-64 w-full" /> : error || !report ? <p className="py-8 text-sm text-destructive">{t("statusReportLoadFailed")}</p> : <div className="space-y-6"><div className="grid gap-3 sm:grid-cols-4"><Detail label={t("reportDate")} value={formatDate(report.reportDate)} /><Detail label={t("sourceDataCapturedAt")} value={formatDateTime(report.sourceDataCapturedAt)} /><Detail label={t("preparedBy")} value={report.preparedByNameSnapshot} /><Detail label={t("publishedAt")} value={formatDateTime(report.publishedAt)} /></div><div className="grid gap-3 sm:grid-cols-3"><Detail label={t("overallHealth")} value={health(report.overallHealth)} /><Detail label={t("overallProgress")} value={formatNullablePercentage(report.overallProgressPercent, 1)} /><Detail label={t("projectStatusAtReport")} value={statusLabel(report.projectLifecycleStatusAtReport)} /></div><div className="grid gap-3 sm:grid-cols-5">{healthDimensions.map(([label, value]) => <Detail key={label} label={label} value={health(value)} />)}</div><div className="grid gap-4 md:grid-cols-2"><Narrative label={t("executiveSummary")} value={report.executiveSummary} /><Narrative label={t("keyAchievements")} value={report.keyAchievements} /><Narrative label={t("challengesConcerns")} value={report.challengesConcerns} /><Narrative label={t("nextPeriodPriorities")} value={report.nextPeriodPriorities} /></div><div className="grid gap-3 sm:grid-cols-3"><Detail label={t("budget")} value={formatNullableCurrency(report.budgetAmountSnapshot, report.currencyCodeSnapshot)} /><Detail label={t("actualCost")} value={formatNullableCurrency(report.actualCostAmountSnapshot, report.currencyCodeSnapshot)} /><Detail label={t("forecast")} value={formatNullableCurrency(report.forecastCostAmountSnapshot, report.currencyCodeSnapshot)} /><Detail label={t("scheduleVariance")} value={report.scheduleVarianceDaysSnapshot == null ? "—" : `${report.scheduleVarianceDaysSnapshot} ${t("days")}`} /><Detail label={t("baseline")} value={report.baselineNameSnapshot ? `${report.baselineNameSnapshot} · v${report.baselineVersionSnapshot ?? "—"}` : "—"} /></div><SnapshotGroup title={t("topRisks")} values={report.risks.map((x) => `${x.title} · ${x.riskLevel}`)} empty={t("noSnapshotItems")} /><SnapshotGroup title={t("topIssues")} values={report.issues.map((x) => `${x.title} · ${x.severity}`)} empty={t("noSnapshotItems")} /><SnapshotGroup title={t("topChangeRequests")} values={report.changeRequests.map((x) => `#${x.number} · ${x.reason}`)} empty={t("noSnapshotItems")} />{actionError ? <p className="text-sm text-destructive">{actionError}</p> : null}</div>}</DialogBody>{report && ((canManage && isDraft) || (canExport && report.reportState === "Published")) ? <DialogFooter>{canExport && report.reportState === "Published" ? <Button variant="outline" onClick={exportReport} disabled={exporting}><Download className="mr-1.5 h-3.5 w-3.5" />{exporting ? t("generatingPdf") : t("exportPdf")}</Button> : null}{canManage && isDraft ? <><Button variant="outline" onClick={deleteReport} disabled={remove.isPending}><Trash2 className="mr-1.5 h-3.5 w-3.5" />{t("delete")}</Button><Button variant="outline" onClick={onEdit}><Pencil className="mr-1.5 h-3.5 w-3.5" />{t("edit")}</Button><Button onClick={publishReport} disabled={publish.isPending}>{publish.isPending ? <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" /> : <Send className="mr-1.5 h-3.5 w-3.5" />}{t("publish")}</Button></> : null}</DialogFooter> : null}</DialogContent></Dialog>;
}

function Field({ label, children }: { label: string; children: ReactNode }) { return <div className="space-y-1.5"><Label>{label}</Label>{children}</div>; }
function StatusSelect({ value, onChange, statusLabel }: { value?: string; onChange: (value: string) => void; statusLabel: (value?: string | null) => string }) { return <select className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" value={value ?? ""} onChange={(e) => onChange(e.target.value)}><option value="">—</option>{PROJECT_STATUSES.map((option) => <option key={option} value={option}>{statusLabel(option)}</option>)}</select>; }
function NarrativeField({ label, value, onChange }: { label: string; value?: string; onChange: (value: string) => void }) { return <Field label={label}><Textarea value={value ?? ""} onChange={(e) => onChange(e.target.value)} rows={3} /></Field>; }
function Narrative({ label, value }: { label: string; value?: string }) { return <div className="rounded-lg border p-3"><p className="text-xs font-semibold text-muted-foreground">{label}</p><p className="mt-2 whitespace-pre-wrap text-sm">{value || "—"}</p></div>; }
function Detail({ label, value }: { label: string; value?: ReactNode }) { return <div className="rounded-lg border bg-muted/20 p-3"><p className="text-xs text-muted-foreground">{label}</p><div className="mt-1 text-sm font-medium">{value || "—"}</div></div>; }
function HealthSelect({ value, onChange, t }: { value?: string; onChange: (value: string) => void; t: (key: string) => string }) { return <select className="flex h-9 w-full rounded-md border border-input bg-background px-3 text-sm" value={value ?? ""} onChange={(e) => onChange(e.target.value)}><option value="">—</option>{healthOptions.map((option) => <option key={option} value={option}>{t(`healthValues.${option}`)}</option>)}</select>; }
function SnapshotGroup({ title, values, empty }: { title: string; values: string[]; empty: string }) { return <div><h3 className="mb-2 text-sm font-semibold">{title}</h3>{values.length ? <ul className="space-y-1 text-sm">{values.map((value) => <li key={value} className="rounded border px-3 py-2">{value}</li>)}</ul> : <p className="text-sm text-muted-foreground">{empty}</p>}</div>; }
