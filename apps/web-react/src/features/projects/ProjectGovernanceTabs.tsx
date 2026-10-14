import * as React from "react";
import { Link, useOutletContext, useParams } from "react-router-dom";
import { AlertTriangle, ArrowLeft, CalendarCheck, CalendarDays, Camera, Download, ExternalLink, Eye, GitBranch, History, Loader2, Lock, Mail, Pencil, Plus, RefreshCw, Search, Send, Trash2, UserRound, UserRoundCheck, Users, X } from "lucide-react";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Button } from "@components/ui/Button";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Textarea } from "@components/ui/Textarea";
import { Badge } from "@components/ui/Badge";
import { Skeleton } from "@components/ui/Skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { Pagination } from "@components/ui/Pagination";
import { PageHeader } from "@components/layout/PageHeader";
import { useDebounce } from "use-debounce";
import { ApiError } from "@api/client";
import {
  useCancelProjectChangeRequest,
  useCreateProjectHandoverRequest,
  useCreateProjectStatusReport,
  useDeleteProjectStatusReport,
  useProjectChangeRequests,
  useProjectHandoverManagers,
  useProjectHandoverRequests,
  useProjectManagerHistory,
  useProjectSnapshots,
  useProjectSnapshot,
  useProjectStatusReports,
  useProjectStatusReport,
  useProjectStatusReportCurrentProgress,
  useUpdateProjectStatusReport,
  usePublishProjectStatusReport,
  type ProjectChangeRequestDto,
  type ProjectDto,
  type ProjectManagerHistoryDto,
  type ProjectSnapshotSummaryDto,
  type ProjectStatusReportDto,
  type ProjectStatusReportInput,
} from "./api/projects";
import { useUserLookup } from "./lib/project-lookups";
import { toHandoverManagerOptions } from "./lib/handover-manager-options";
import { useFullMeetings, useMeetingFullMutations, type MeetingInput } from "@api/meetingsFull";
import { createMeetingDraft, isValidMeetingEmail, toMeetingInput, validateMeetingDraft, type MeetingAttendeeDraft } from "@features/delivery/meeting-form";
import { exportProjectStatusReportPdf } from "@lib/export/project-status-report-export";
import { useProjectEnumLabels } from "./lib/project-labels";
import { PROJECT_STATUSES } from "@lib/enums";
import { formatDateTime } from "@lib/utils";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { getRiskLevelTextClass, getRiskStatusTextClass } from "@lib/status-presentation";
import { formatProjectChangeRequestAmount } from "./lib/project-change-request-format";

function formatPercent1(value: number | null | undefined, locale: string): string {
  if (typeof value !== "number" || !Number.isFinite(value)) return "—";
  return `${new Intl.NumberFormat(locale === "ar" ? "ar-EG-u-nu-latn" : "en-US", { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(value)}%`;
}

interface ProjectContext { projectId: string; project: ProjectDto }
interface UserLookupDto { id: string; fullName: string; email: string; jobTitle?: string }

function useProjectContext(): ProjectContext {
  return useOutletContext<ProjectContext>();
}

function Loading({ t }: { t: (key: string) => string }) {
  return <p className="py-12 text-center text-sm text-muted-foreground">{t("common.loading")}</p>;
}

function ErrorState({ t, onRetry }: { t: (key: string) => string; onRetry?: () => void }) {
  return <div className="py-12 text-center text-sm text-destructive"><p>{t("common.errorLoading")}</p>{onRetry ? <Button className="mt-3" size="sm" variant="outline" onClick={onRetry}><RefreshCw className="me-1.5 h-3.5 w-3.5" />{t("common.retry")}</Button> : null}</div>;
}

function Empty({ children }: { children: React.ReactNode }) {
  return <p className="rounded border border-dashed p-8 text-center text-sm text-muted-foreground">{children}</p>;
}

export function ProjectStatusReportsTab() {
  const { projectId, project } = useProjectContext();
  const { t, locale, formatDate, formatCurrency } = useI18n();
  const { statusLabel, healthLabel } = useProjectEnumLabels();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const query = useProjectStatusReports(projectId);
  const create = useCreateProjectStatusReport(projectId);
  const [detailId, setDetailId] = React.useState("");
  const detail = useProjectStatusReport(projectId, detailId, Boolean(detailId));
  const [editingReport, setEditingReport] = React.useState<ProjectStatusReportDto | null>(null);
  const selectedReportId = editingReport?.id ?? "";
  const update = useUpdateProjectStatusReport(projectId, selectedReportId);
  const publish = usePublishProjectStatusReport(projectId);
  const remove = useDeleteProjectStatusReport(projectId);
  const [formOpen, setFormOpen] = React.useState(false);
  const currentProgress = useProjectStatusReportCurrentProgress(projectId, formOpen);
  const today = React.useMemo(() => new Date().toISOString().slice(0, 10), []);
  const [exporting, setExporting] = React.useState(false);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Project.Edit) && canMutate && !effectiveReadOnly);
  const canExport = Boolean(user && hasPermission(user.permissions, Permissions.Project.View) && hasPermission(user.permissions, Permissions.Reports.Export));
  const reports = query.data?.items ?? [];

  function openNewReport() { setActionError(null); setEditingReport(null); setFormOpen(true); }
  function openReport(reportId: string) { setActionError(null); setDetailId(reportId); }

  async function exportReport() {
    if (!detail.data || detail.data.reportState !== "Published" || !canExport || exporting) return;
    setActionError(null);
    setExporting(true);
    try {
      await exportProjectStatusReportPdf({ report: detail.data, publishedHistory: reports, locale: document.documentElement.lang });
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : t("projects.statusReportExportFailed"));
    } finally {
      setExporting(false);
    }
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const periodFrom = String(data.get("periodFrom") || "");
    const periodTo = String(data.get("periodTo") || "");
    if (periodFrom && periodTo && periodTo < periodFrom) {
      setActionError(t("projects.statusReportPeriodInvalid"));
      return;
    }
    const progressRaw = String(data.get("overallProgressPercent") || "").trim();
    const input: ProjectStatusReportInput = {
      reportDate: String(data.get("reportDate") || ""),
      periodFrom,
      periodTo,
      projectLifecycleStatusAtReport: String(data.get("projectLifecycleStatusAtReport") || project.status || ""),
      overallHealth: String(data.get("overallHealth") || "") || undefined,
      overallProgressPercent: progressRaw === "" ? undefined : Number(progressRaw),
      executiveSummary: String(data.get("executiveSummary") || "").trim() || undefined,
      keyAchievements: String(data.get("keyAchievements") || "").trim() || undefined,
      challengesConcerns: String(data.get("challengesConcerns") || "").trim() || undefined,
      nextPeriodPriorities: String(data.get("nextPeriodPriorities") || "").trim() || undefined,
      scheduleHealth: String(data.get("scheduleHealth") || "") || undefined,
      costHealth: String(data.get("costHealth") || "") || undefined,
      scopeHealth: String(data.get("scopeHealth") || "") || undefined,
      qualityHealth: String(data.get("qualityHealth") || "") || undefined,
      resourceHealth: String(data.get("resourceHealth") || "") || undefined,
      rowVersion: editingReport?.rowVersion,
    };
    setActionError(null);
    try {
      if (editingReport) await update.mutateAsync(input);
      else await create.mutateAsync(input);
      setFormOpen(false);
      setEditingReport(null);
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : t("projects.statusReportSaveFailed"));
    }
  }

  async function confirmPublish(reportId: string) {
    if (!window.confirm(t("projects.publishStatusReportConfirm"))) return;
    setActionError(null);
    try { await publish.mutateAsync(reportId); } catch (reason) { setActionError(reason instanceof Error ? reason.message : t("projects.statusReportPublishFailed")); }
  }

  async function confirmDelete(reportId: string) {
    if (!window.confirm(t("projects.deleteStatusReportConfirm"))) return;
    setActionError(null);
    try { await remove.mutateAsync(reportId); setDetailId(""); } catch (reason) { setActionError(reason instanceof Error ? reason.message : t("projects.statusReportDeleteFailed")); }
  }

  return <div className="space-y-4">
    <Card>
      <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div><CardTitle className="flex flex-wrap items-center gap-2"><Send className="h-4 w-4" />{t("projects.statusReportsTitle")}<Badge status={project.status} className="text-[10px]">{statusLabel(project.status)}</Badge></CardTitle><CardDescription>{t("projects.statusReportsDescription")}</CardDescription></div>
        {canManage ? <Button size="sm" onClick={openNewReport}><Plus className="me-1.5 h-4 w-4" />{t("projects.newStatusReport")}</Button> : null}
      </CardHeader>
      <CardContent className="p-0">
        {query.isLoading ? <Loading t={t} /> : query.isError ? <ErrorState t={t} onRetry={() => void query.refetch()} /> : reports.length === 0 ? <Empty>{t("projects.noStatusReportsDescription")}</Empty> :
          <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead className="whitespace-nowrap">{t("projects.reportDate")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.reportingPeriod")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.projectStatusAtReport")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.overallHealth")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.overallProgress")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.reportState")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.preparedBy")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.publishedAt")}</TableHead><TableHead className="text-end">{t("projects.view")}</TableHead></TableRow></TableHeader><TableBody>
            {reports.map((report) => <TableRow key={report.id}><TableCell>{formatDate(report.reportDate)}</TableCell><TableCell className="whitespace-nowrap">{formatDate(report.periodFrom)} → {formatDate(report.periodTo)}</TableCell><TableCell><Badge status={report.projectLifecycleStatusAtReport} className="text-[10px]">{report.projectLifecycleStatusAtReport ? statusLabel(report.projectLifecycleStatusAtReport) : "—"}</Badge></TableCell><TableCell><Badge status={report.overallHealth} className="text-[10px]">{report.overallHealth ? healthLabel(report.overallHealth) : "—"}</Badge></TableCell><TableCell className="tabular-nums">{formatPercent1(report.overallProgressPercent, locale)}</TableCell><TableCell><Badge status={report.reportState}>{t(`projects.statusReportStates.${report.reportState}`)}</Badge></TableCell><TableCell>{report.preparedByNameSnapshot || "—"}</TableCell><TableCell className="whitespace-nowrap">{report.publishedAt ? formatDateTime(report.publishedAt, locale) : "—"}</TableCell><TableCell><div className="flex justify-end"><Button size="sm" variant="ghost" onClick={() => openReport(report.id)}><Eye className="me-1.5 h-3.5 w-3.5" />{t("projects.view")}</Button></div></TableCell></TableRow>)}</TableBody></Table></div>}
          
      </CardContent>
    </Card>
    <SideSheet open={formOpen} onOpenChange={(next) => { setFormOpen(next); if (!next) { setEditingReport(null); setActionError(null); } }} title={editingReport ? t("projects.editStatusReport") : t("projects.newStatusReport")} description={t("projects.statusReportFormDescription")}>
      <form key={selectedReportId || "new"} onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-4">
        <p className="text-[10px] font-semibold uppercase tracking-[0.14em] text-muted-foreground">{t("projects.reportingPeriodSection")}</p>
        <div className="rounded-lg border bg-muted/20 p-3 text-xs text-muted-foreground"><div className="grid gap-2 sm:grid-cols-2"><span>{t("projects.name")}: <strong className="text-foreground">{project.name} ({project.code || "—"})</strong></span><span>{t("projects.projectManager")}: <strong className="text-foreground">{project.projectManagerName || "—"}</strong></span><span>{t("projects.status")}: <strong className="text-foreground">{project.status ? statusLabel(project.status) : "—"}</strong></span><span>{t("projects.preparedBy")}: <strong className="text-foreground">{user?.fullName || "—"}</strong></span><span>{t("projects.health")}: <strong className="text-foreground">{project.health ? healthLabel(project.health) : "—"} · {formatPercent1(currentProgress.data?.projectPlanProgressPercent ?? project.progress, locale)}</strong></span></div></div>
        <div className="grid gap-3 sm:grid-cols-4"><div><Label>{t("projects.reportDate")}</Label><Input className="mt-1" name="reportDate" type="date" defaultValue={editingReport?.reportDate?.slice(0, 10) ?? today} required /></div><div><Label>{t("projects.periodFrom")}</Label><Input className="mt-1" name="periodFrom" type="date" defaultValue={editingReport?.periodFrom?.slice(0, 10) ?? today} required /></div><div><Label>{t("projects.periodTo")}</Label><Input className="mt-1" name="periodTo" type="date" defaultValue={editingReport?.periodTo?.slice(0, 10) ?? today} required /></div><div><Label>{t("projects.projectStatusAtReport")}</Label><select className="input-field mt-1 w-full" name="projectLifecycleStatusAtReport" defaultValue={editingReport?.projectLifecycleStatusAtReport || project.status || ""}>{PROJECT_STATUSES.map((s) => <option key={s} value={s}>{statusLabel(s)}</option>)}</select></div></div>
        <p className="text-[10px] font-semibold uppercase tracking-[0.14em] text-muted-foreground">{t("projects.overallStatusSection")}</p>
        <div className="grid gap-3 sm:grid-cols-2"><div><Label>{t("projects.overallHealth")}</Label><select className="input-field mt-1 w-full" name="overallHealth" defaultValue={editingReport?.overallHealth ?? "Green"}><option value="">—</option>{["Green", "Amber", "Red"].map((h) => <option key={h} value={h}>{t(`projects.healthValues.${h}`)}</option>)}</select></div><div><Label>{t("projects.overallProgress")}</Label><Input className="mt-1" name="overallProgressPercent" type="number" min="0" max="100" value={currentProgress.data?.projectPlanProgressPercent ?? editingReport?.overallProgressPercent ?? project.progress ?? ""} readOnly /></div></div>
        <p className="text-[10px] font-semibold uppercase tracking-[0.14em] text-muted-foreground">{t("projects.healthDimensionsSection")}</p>
        <div className="grid gap-3 sm:grid-cols-2"><HealthField name="scheduleHealth" label={t("projects.scheduleHealth")} value={editingReport?.scheduleHealth} t={t} /><HealthField name="costHealth" label={t("projects.costHealth")} value={editingReport?.costHealth} t={t} /><HealthField name="scopeHealth" label={t("projects.scopeHealth")} value={editingReport?.scopeHealth} t={t} /><HealthField name="qualityHealth" label={t("projects.qualityHealth")} value={editingReport?.qualityHealth} t={t} /><HealthField name="resourceHealth" label={t("projects.resourceHealth")} value={editingReport?.resourceHealth} t={t} /></div>
        <p className="text-[10px] font-semibold uppercase tracking-[0.14em] text-muted-foreground">{t("projects.narrativesSection")}</p>
        <NarrativeField name="executiveSummary" label={t("projects.executiveSummary")} defaultValue={editingReport?.executiveSummary} /><NarrativeField name="keyAchievements" label={t("projects.keyAchievements")} defaultValue={editingReport?.keyAchievements} /><NarrativeField name="challengesConcerns" label={t("projects.challengesConcerns")} defaultValue={editingReport?.challengesConcerns} /><NarrativeField name="nextPeriodPriorities" label={t("projects.nextPeriodPriorities")} defaultValue={editingReport?.nextPeriodPriorities} />
        {actionError ? <p className="text-sm text-destructive">{actionError}</p> : null}
      </SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => { setFormOpen(false); setEditingReport(null); }}>{t("common.cancel")}</Button><Button type="submit" disabled={create.isPending || update.isPending}>{editingReport ? t("projects.saveChanges") : t("projects.saveDraft")}</Button></SideSheetFooter></form>
    </SideSheet>
    <StatusReportDetailsDialog open={Boolean(detailId)} onOpenChange={(next) => { if (!next) { setDetailId(""); setActionError(null); } }} report={detail.data} loading={detail.isLoading} error={detail.isError} actionError={actionError} canManage={canManage} canExport={canExport} exporting={exporting} isPublishing={publish.isPending} isDeleting={remove.isPending} onEdit={() => { if (detail.data) { setEditingReport(detail.data); setDetailId(""); setActionError(null); setFormOpen(true); } }} onPublish={() => { if (detail.data) void confirmPublish(detail.data.id); }} onDelete={() => { if (detail.data) void confirmDelete(detail.data.id); }} onExport={() => void exportReport()} t={t} locale={locale} statusLabel={statusLabel} healthLabel={healthLabel} formatDate={formatDate} formatDateTime={(value) => formatDateTime(value, locale)} formatCurrency={formatCurrency} projectCurrency={project.currency} />
  </div>;
}

function StatusReportDetailsDialog({ open, onOpenChange, report, loading, error, actionError, canManage, canExport, exporting, isPublishing, isDeleting, onEdit, onPublish, onDelete, onExport, t, locale, statusLabel, healthLabel, formatDate, formatDateTime, formatCurrency, projectCurrency }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  report?: ProjectStatusReportDto;
  loading: boolean;
  error: boolean;
  actionError: string | null;
  canManage: boolean;
  canExport: boolean;
  exporting: boolean;
  isPublishing: boolean;
  isDeleting: boolean;
  onEdit: () => void;
  onPublish: () => void;
  onDelete: () => void;
  onExport: () => void;
  t: (key: string) => string;
  locale: string;
  statusLabel: (value?: string | null) => string;
  healthLabel: (value?: string | null) => string;
  formatDate: (value: string | Date, options?: Intl.DateTimeFormatOptions) => string;
  formatDateTime: (value: string | Date) => string;
  formatCurrency: (value: number, currency?: string) => string;
  projectCurrency: string;
}) {
  const isDraft = report?.reportState === "Draft";
  const health = (value?: string) => <Badge status={value}>{healthLabel(value)}</Badge>;
  const dimensions = [
    [t("projects.scheduleHealth"), report?.scheduleHealth],
    [t("projects.costHealth"), report?.costHealth],
    [t("projects.scopeHealth"), report?.scopeHealth],
    [t("projects.qualityHealth"), report?.qualityHealth],
    [t("projects.resourceHealth"), report?.resourceHealth],
  ] as const;
  return <Dialog open={open} onOpenChange={onOpenChange} ariaLabel={t("projects.statusReportDetails")} contentClassName="max-h-[calc(100vh-2rem)] max-w-4xl overflow-y-auto sm:max-w-4xl">
    <DialogContent className="space-y-6">
      <DialogHeader>
        <DialogTitle>{report?.projectNameSnapshot || t("projects.statusReportDetails")}</DialogTitle>
        <DialogDescription>{report ? `${formatDate(report.periodFrom)} – ${formatDate(report.periodTo)} · ${t(`projects.statusReportStates.${report.reportState}`)}` : t("projects.statusReportDetails")}</DialogDescription>
      </DialogHeader>
      {loading ? <Loading t={t} /> : error || !report ? <p role="alert" className="py-8 text-sm text-destructive">{t("projects.statusReportLoadFailed")}</p> : <div className="space-y-6">
        <div className="grid gap-3 sm:grid-cols-4"><ReportDetail label={t("projects.reportDate")} value={formatDate(report.reportDate)} /><ReportDetail label={t("projects.sourceDataCapturedAt")} value={report.sourceDataCapturedAt ? formatDateTime(report.sourceDataCapturedAt) : "—"} /><ReportDetail label={t("projects.preparedBy")} value={report.preparedByNameSnapshot} /><ReportDetail label={t("projects.publishedAt")} value={report.publishedAt ? formatDateTime(report.publishedAt) : "—"} /></div>
        <div className="grid gap-3 sm:grid-cols-3"><ReportDetail label={t("projects.overallHealth")} value={health(report.overallHealth)} /><ReportDetail label={t("projects.overallProgress")} value={formatPercent1(report.overallProgressPercent, locale)} /><ReportDetail label={t("projects.projectStatusAtReport")} value={statusLabel(report.projectLifecycleStatusAtReport)} /></div>
        <div className="grid gap-3 sm:grid-cols-5">{dimensions.map(([label, value]) => <ReportDetail key={label} label={label} value={health(value)} />)}</div>
        <div className="grid gap-4 md:grid-cols-2"><ReportNarrative label={t("projects.executiveSummary")} value={report.executiveSummary} /><ReportNarrative label={t("projects.keyAchievements")} value={report.keyAchievements} /><ReportNarrative label={t("projects.challengesConcerns")} value={report.challengesConcerns} /><ReportNarrative label={t("projects.nextPeriodPriorities")} value={report.nextPeriodPriorities} /></div>
        <StatusReportSnapshotDetails report={report} projectCurrency={projectCurrency} t={t} formatDate={formatDate} formatCurrency={formatCurrency} />
      </div>}
      {actionError ? <p role="alert" className="text-sm text-destructive">{actionError}</p> : null}
      {report && ((canManage && isDraft) || (canExport && report.reportState === "Published")) ? <DialogFooter className="gap-2 pt-2">
        {canExport && report.reportState === "Published" ? <Button type="button" variant="outline" onClick={onExport} disabled={exporting}><Download className="me-1.5 h-3.5 w-3.5" />{exporting ? t("projects.generatingPdf") : t("projects.exportPdf")}</Button> : null}
        {canManage && isDraft ? <><Button type="button" variant="outline" onClick={onDelete} disabled={isDeleting}><Trash2 className="me-1.5 h-3.5 w-3.5" />{t("projects.delete")}</Button><Button type="button" variant="outline" onClick={onEdit}><Pencil className="me-1.5 h-3.5 w-3.5" />{t("common.edit")}</Button><Button type="button" onClick={onPublish} disabled={isPublishing}>{isPublishing ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : <Send className="me-1.5 h-3.5 w-3.5" />}{t("projects.publish")}</Button></> : null}
      </DialogFooter> : null}
    </DialogContent>
  </Dialog>;
}

function HealthField({ name, label, value, disabled, t }: { name: string; label: string; value?: string; disabled?: boolean; t: (key: string) => string }) {
  return <div><Label>{label}</Label><select className="input-field mt-1 w-full" name={name} defaultValue={value || ""} disabled={disabled}><option value="">—</option>{["Green", "Amber", "Red"].map((h) => <option key={h} value={h}>{t(`projects.healthValues.${h}`)}</option>)}</select></div>;
}

function NarrativeField({ name, label, defaultValue, readOnly }: { name: string; label: string; defaultValue?: string; readOnly?: boolean }) {
  return <div><Label>{label}</Label><Textarea className="mt-1" name={name} rows={3} defaultValue={defaultValue} readOnly={readOnly} /> </div>;
}

function StatusReportSnapshotDetails({ report, projectCurrency, t, formatDate, formatCurrency }: { report: ProjectStatusReportDto; projectCurrency: string; t: (key: string, values?: Record<string, string | number>) => string; formatDate: (value: string | Date, options?: Intl.DateTimeFormatOptions) => string; formatCurrency: (value: number, currency?: string) => string }) {
  const currency = report.currencyCodeSnapshot || projectCurrency;
  const money = (value?: number) => typeof value === "number" && Number.isFinite(value) ? formatCurrency(value, currency) : "—";
  const list = (values: readonly { title: string; status: string; owner?: string; detail?: string }[], empty: string) => values.length ? <div className="space-y-2">{values.map((item, index) => <div key={`${item.title}-${index}`} className="flex items-start justify-between gap-3 rounded border px-3 py-2 text-sm"><div className="min-w-0"><p className="font-medium">{item.title}</p>{item.detail ? <p className="mt-0.5 text-xs text-muted-foreground">{item.detail}</p> : null}</div><div className="shrink-0 text-end text-xs text-muted-foreground"><p>{item.status || "—"}</p>{item.owner ? <p>{item.owner}</p> : null}</div></div>)}</div> : <p className="text-sm text-muted-foreground">{empty}</p>;
  return <div className="space-y-4 border-t pt-4"><div className="grid gap-2 sm:grid-cols-2"><SnapshotValue label={t("projects.name")} value={report.projectNameSnapshot} /><SnapshotValue label={t("projects.code")} value={report.projectCodeSnapshot} /><SnapshotValue label={t("projects.projectManager")} value={report.projectManagerNameSnapshot} /><SnapshotValue label={t("projects.preparedBy")} value={report.preparedByNameSnapshot} /><SnapshotValue label={t("projects.publishedBy")} value={report.publishedByNameSnapshot} /><SnapshotValue label={t("projects.sourceDataCapturedAt")} value={report.sourceDataCapturedAt ? formatDate(report.sourceDataCapturedAt) : undefined} /></div><div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4"><SnapshotValue label={t("projects.budget")} value={money(report.budgetAmountSnapshot)} /><SnapshotValue label={t("projects.actualCost")} value={money(report.actualCostAmountSnapshot)} /><SnapshotValue label={t("projects.committedCost")} value={money(report.committedCostAmountSnapshot)} /><SnapshotValue label={t("projects.forecast")} value={money(report.forecastCostAmountSnapshot)} /><SnapshotValue label={t("projects.costVariance")} value={money(report.costVarianceAmountSnapshot)} /><SnapshotValue label={t("projects.scheduleVariance")} value={report.scheduleVarianceDaysSnapshot == null ? undefined : `${report.scheduleVarianceDaysSnapshot} ${t("projects.days")}`} /><SnapshotValue label={t("projects.baseline")} value={report.baselineNameSnapshot ? `${report.baselineNameSnapshot}${report.baselineVersionSnapshot == null ? "" : ` · v${report.baselineVersionSnapshot}`}` : undefined} /><SnapshotValue label={t("projects.publishedAt")} value={report.publishedAt ? formatDate(report.publishedAt) : undefined} /></div><div className="grid gap-4 lg:grid-cols-3"><Card><CardHeader><CardTitle className="text-sm">{t("projects.topRisks")}</CardTitle></CardHeader><CardContent>{list((report.risks ?? []).map((item) => ({ title: item.title, status: item.riskLevel, owner: item.ownerNameSnapshot, detail: item.riskStatus })), t("projects.noSnapshotItems"))}</CardContent></Card><Card><CardHeader><CardTitle className="text-sm">{t("projects.topIssues")}</CardTitle></CardHeader><CardContent>{list((report.issues ?? []).map((item) => ({ title: item.title, status: item.severity, owner: item.assigneeNameSnapshot, detail: item.issueStatus })), t("projects.noSnapshotItems"))}</CardContent></Card><Card><CardHeader><CardTitle className="text-sm">{t("projects.topChangeRequests")}</CardTitle></CardHeader><CardContent>{list((report.changeRequests ?? []).map((item) => ({ title: `#${item.number} · ${item.reason}`, status: item.changeRequestStatus, owner: item.requestedByNameSnapshot, detail: formatDate(item.requestedAt) })), t("projects.noSnapshotItems"))}</CardContent></Card></div></div>;
}

export function ProjectHandoverTab() {
  const { projectId, project } = useProjectContext();
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const query = useProjectHandoverRequests(projectId);
  const [open, setOpen] = React.useState(false);
  const [newManagerId, setNewManagerId] = React.useState("");
  const [actionError, setActionError] = React.useState<string | null>(null);
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.Handover.Manage) && canMutate && !effectiveReadOnly);
  const handoverManagers = useProjectHandoverManagers(projectId, open && canManage);
  const create = useCreateProjectHandoverRequest(projectId);
  const requests = query.data ?? [];
  const pendingRequest = requests.find((item) => item.status === "Pending");

  React.useEffect(() => {
    if (open) { setNewManagerId(""); setActionError(null); }
  }, [open]);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setActionError(null);
    const reason = String(data.get("reason") || "").trim();
    if (!reason) {
      setActionError(t("projects.handoverReasonRequired"));
      return;
    }
    try {
      await create.mutateAsync({ newProjectManagerId: newManagerId || String(data.get("newProjectManagerId") || ""), effectiveHandoverDate: String(data.get("effectiveHandoverDate") || ""), reason, notes: String(data.get("notes") || "").trim() || undefined });
      setOpen(false);
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : t("projects.handoverFailed"));
    }
  }

  const managerOptions = toHandoverManagerOptions(handoverManagers.data, {
    projectManager: t("projects.handoverManagerTypeProject"),
    portfolioManager: t("projects.handoverManagerTypePortfolio"),
    programManager: t("projects.handoverManagerTypeProgram"),
  });
  const managersLoading = handoverManagers.isLoading;
  const managersError = handoverManagers.isError;
  const minDate = project.startDate?.slice(0, 10) || undefined;
  const maxDate = project.endDate?.slice(0, 10) || undefined;

  return <div className="space-y-4"><Card><CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><CardTitle className="flex items-center gap-2"><UserRoundCheck className="h-4 w-4" />{t("projects.handoverTitle")}</CardTitle><CardDescription>{t("projects.handoverDescription")}</CardDescription></div>{canManage && !pendingRequest ? <Button size="sm" onClick={() => setOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("projects.submitHandover")}</Button> : null}</CardHeader><CardContent className="p-0">{query.isLoading ? <Loading t={t} /> : query.isError ? <ErrorState t={t} onRetry={() => void query.refetch()} /> : requests.length === 0 ? <Empty>{t("projects.noHandoverRequests")}</Empty> : <div className="divide-y">{requests.map((item) => <div key={item.id} className="flex flex-col gap-3 px-4 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="min-w-0"><p className="text-sm font-medium">{item.currentProjectManagerName} → {item.newProjectManagerName}</p><p className="mt-1 text-xs text-muted-foreground">{t("projects.effectiveHandoverDate")}: {formatDate(item.effectiveHandoverDate)} · {t("projects.requestedBy")}: {item.requestedByName}</p>{item.reason ? <p className="mt-1 text-xs text-muted-foreground">{item.reason}</p> : null}{item.rejectionReason ? <p className="mt-1 text-xs text-destructive">{t("projects.rejectionReason")}: {item.rejectionReason}</p> : null}</div><Badge status={item.status} className="w-fit text-[10px]">{t(`projects.handoverStatus.${item.status}`)}</Badge></div>)}</div>}</CardContent></Card>
    <SideSheet open={open} onOpenChange={setOpen} title={t("projects.handoverTitle")} description={t("projects.handoverDescription")}>
      {pendingRequest ? <><SideSheetBody className="space-y-4"><p className="text-sm font-semibold">{t("projects.handoverPendingTitle")}</p><p className="text-sm text-muted-foreground">{t("projects.handoverPendingDescription")}</p><div className="rounded border px-3 py-2 text-sm"><p className="font-medium">{pendingRequest.currentProjectManagerName} → {pendingRequest.newProjectManagerName}</p><p className="mt-1 text-xs text-muted-foreground">{formatDate(pendingRequest.effectiveHandoverDate)}</p></div></SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("common.close")}</Button></SideSheetFooter></>
      : <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-4">
        {actionError ? <p className="rounded border border-destructive/20 bg-destructive/10 p-3 text-sm text-destructive">{actionError}</p> : null}
        <div><Label>{t("projects.project")}</Label><Input className="mt-1" value={project.name} readOnly /></div>
        <div><Label>{t("projects.currentProjectManager")}</Label><Input className="mt-1" value={project.projectManagerName || "—"} readOnly /></div>
        <div><Label>{t("projects.newManager")}</Label><SearchableSelect options={managerOptions} value={newManagerId} onValueChange={setNewManagerId} placeholder={t("projects.selectProjectManager")} searchPlaceholder={t("projects.selectProjectManager")} loading={managersLoading} emptyText={t("projects.noEligibleManagers")} disabled={managersError} /><input type="hidden" name="newProjectManagerId" value={newManagerId} />{managersError ? <p role="alert" className="mt-1 text-xs text-destructive">{t("common.errorLoading")}</p> : null}{!managersLoading && !managersError && managerOptions.length === 0 ? <div className="mt-2 space-y-1 text-xs text-muted-foreground"><p>{t("projects.noEligibleManagersHint")}</p><Link to="/administration/users" className="text-primary underline-offset-4 hover:underline">{t("projects.manageUsers")}</Link></div> : null}</div>
        <div><Label>{t("projects.effectiveHandoverDate")}</Label><Input className="mt-1" name="effectiveHandoverDate" type="date" required min={minDate} max={maxDate} /></div>
        <div><Label>{t("projects.handoverReason")}</Label><Textarea className="mt-1" name="reason" required maxLength={500} /></div>
        <div><Label>{t("projects.handoverNotes")}</Label><Textarea className="mt-1" name="notes" rows={3} maxLength={2000} /></div>
      </SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={create.isPending || managersLoading || managersError || !newManagerId}>{create.isPending ? t("common.saving") : t("projects.submitHandover")}</Button></SideSheetFooter></form>}
    </SideSheet>
  </div>;
}

export function ProjectHistoryTab() {
  const { projectId } = useProjectContext();
  const { t, formatDate } = useI18n();
  const query = useProjectManagerHistory(projectId);
  const rows: ProjectManagerHistoryDto[] = query.data ?? [];
  return <Card><CardHeader><CardTitle className="flex items-center gap-2"><History className="h-4 w-4" />{t("projects.managerHistory")}</CardTitle><CardDescription>{t("projects.managerHistoryDescription")}</CardDescription></CardHeader><CardContent className="p-0">{query.isLoading ? <Loading t={t} /> : query.isError ? <ErrorState t={t} onRetry={() => void query.refetch()} /> : rows.length === 0 ? <Empty>{t("projects.noManagerHistory")}</Empty> : <div className="divide-y">{rows.map((row) => <div key={row.id} className="flex flex-col gap-2 px-4 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="min-w-0"><p className="text-sm font-medium">{row.projectManagerName}</p><p className="mt-1 text-xs text-muted-foreground">{formatDate(row.startDate)} – {row.endDate ? formatDate(row.endDate) : t("projects.current")}{row.handoverFromProjectManagerName ? ` · ${t("common.from")}: ${row.handoverFromProjectManagerName}` : ""}</p>{row.handoverReason ? <p className="mt-1 text-xs text-muted-foreground">{row.handoverReason}</p> : null}</div>{row.isCurrent ? <Badge status="Active" className="w-fit text-[10px]">{t("projects.current")}</Badge> : <Badge status="Closed" className="w-fit text-[10px]">{row.endDate ? formatDate(row.endDate) : t("projects.current")}</Badge>}</div>)}</div>}</CardContent></Card>;
}

export function ProjectSnapshotDetailPage() {
  const { t } = useI18n();
  return <div className="space-y-6"><PageHeader title={t("projects.snapshot")} description={t("projects.snapshotsDescription")} /><ProjectSnapshotsTab /></div>;
}

export function ProjectSnapshotsTab() {
  const context = useOutletContext<ProjectContext | undefined>();
  const { id: routeProjectId, snapshotId } = useParams<{ id?: string; snapshotId?: string }>();
  const projectId = context?.projectId ?? routeProjectId ?? "";
  const { t, formatDate, formatCurrency } = useI18n();
  const query = useProjectSnapshots(projectId, !snapshotId && Boolean(projectId));
  const detail = useProjectSnapshot(projectId, snapshotId ?? "", Boolean(snapshotId));
  const rows: ProjectSnapshotSummaryDto[] = query.data ?? [];
  if (snapshotId) {
    const snapshot = detail.data;
    if (detail.isLoading) return <Loading t={t} />;
    if (detail.isError || !snapshot) return <Card><CardContent className="p-8 text-center text-sm text-destructive">{t("projects.noSnapshots")}</CardContent></Card>;
    const data = snapshot.data;
    const sections = [
      ["projects.snapshotTeam", "members", "projects.noTeamMembers"], ["projects.snapshotWorkItems", "workItems", "projects.noTasksYet"], ["projects.snapshotMilestones", "milestones", "projects.noMilestonesYet"],
      ["projects.snapshotDeliverables", "deliverables", "projects.noDeliverablesYet"], ["projects.snapshotPhases", "phases", "projects.noPhasesYet"], ["projects.snapshotRisks", "risks", "projects.noRisksYet"], ["projects.snapshotIssues", "issues", "projects.noIssuesYet"],
      ["projects.snapshotBudgets", "budgets", "projects.noBudgetsYet"], ["projects.snapshotCostEntries", "costEntries", "projects.noCostEntriesYet"], ["projects.snapshotMeetings", "meetings", "projects.noMeetingsYet"], ["projects.snapshotDocuments", "documents", "projects.noDocumentsYet"], ["projects.snapshotStatusReports", "statusReports", "projects.noStatusReportsYet"],
    ] as const;
    return (
      <div className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <Badge variant="secondary"><Lock className="me-1 h-3 w-3" />{t("projects.readOnlySnapshot")}</Badge>
            <Badge variant="outline">{t("projects.snapshotNumber")} #{snapshot.snapshotNumber}</Badge>
          </div>
          <Link to={`/projects/${encodeURIComponent(projectId)}`}><Button size="sm" variant="outline"><ArrowLeft className="me-1.5 h-4 w-4 rtl:rotate-180" />{t("projects.backToProject")}</Button></Link>
        </div>
        <Card>
          <CardHeader><CardTitle>{snapshot.name}</CardTitle><CardDescription>{t("projects.readOnlySnapshotDescription")}</CardDescription></CardHeader>
          <CardContent className="space-y-1">
            <SnapshotValue label={t("projects.snapshotDate")} value={formatDate(snapshot.snapshotDate)} />
            <SnapshotValue label={t("projects.effectiveEndDate")} value={formatDate(snapshot.effectiveEndDate)} />
            <SnapshotValue label={t("projects.previousManager")} value={snapshot.previousProjectManagerName} />
            <SnapshotValue label={t("projects.newManager")} value={snapshot.newProjectManagerName} />
            <SnapshotValue label={t("projects.handoverReason")} value={snapshot.handoverReason} />
            {snapshot.handoverNotes ? <SnapshotValue label={t("projects.handoverNotes")} value={snapshot.handoverNotes} /> : null}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-3"><CardTitle className="text-sm">{t("projects.snapshotProjectDetails")}</CardTitle></CardHeader>
          <CardContent className="space-y-1">
            <SnapshotValue label={t("projects.name")} value={snapshot.name} />
            <SnapshotValue label={t("projects.code")} value={snapshot.code} />
            <SnapshotValue label={t("common.status")} value={snapshot.status} />
            <SnapshotValue label={t("projects.priority")} value={snapshot.priority} />
            <SnapshotValue label={t("projects.phase")} value={snapshot.phase} />
            <SnapshotValue label={t("projects.health")} value={snapshot.health} />
            <SnapshotValue label={t("projects.confidenceLevel")} value={snapshot.confidenceLevel} />
            <SnapshotValue label={t("projects.riskRating")} value={snapshot.riskRating} />
            <SnapshotValue label={t("projects.portfolio")} value={snapshot.portfolioName} />
            <SnapshotValue label={t("projects.program")} value={snapshot.programName} />
            <SnapshotValue label={t("projects.department")} value={snapshot.departmentName} />
            <SnapshotValue label={t("projects.sponsor")} value={snapshot.sponsorName} />
            <SnapshotValue label={t("projects.startDate")} value={snapshot.startDate ? formatDate(snapshot.startDate) : undefined} />
            <SnapshotValue label={t("projects.endDate")} value={snapshot.endDate ? formatDate(snapshot.endDate) : undefined} />
            <SnapshotValue label={t("projects.budget")} value={snapshot.budget == null ? undefined : formatCurrency(snapshot.budget, snapshot.currency)} />
            <SnapshotValue label={t("projects.actualCost")} value={snapshot.actualCost == null ? undefined : formatCurrency(snapshot.actualCost, snapshot.currency)} />
            <SnapshotValue label={t("projects.progress")} value={snapshot.progress == null ? undefined : `${snapshot.progress}%`} />
            <SnapshotValue label={t("projects.projectDescription")} value={snapshot.description} />
            <SnapshotValue label={t("projects.businessCase")} value={snapshot.businessCase} />
            <SnapshotValue label={t("projects.notes")} value={snapshot.notes} />
          </CardContent>
        </Card>
        <div className="grid gap-4 lg:grid-cols-2">
          {sections.map(([title, key, emptyKey]) => {
            const items = data?.[key] ?? [];
            return <Card key={key}><CardHeader><CardTitle className="flex items-center gap-2 text-sm"><Camera className="h-4 w-4" />{t(title)} <span className="font-normal text-muted-foreground">({items.length})</span></CardTitle></CardHeader><CardContent>{items.length === 0 ? <p className="text-sm text-muted-foreground">{t(emptyKey)}</p> : <div className="space-y-2">{items.map((item) => <div key={item.originalId} className="flex items-center justify-between gap-3 text-sm"><SnapshotRow item={item} section={key} currency={snapshot.currency} /></div>)}</div>}</CardContent></Card>;
          })}
        </div>
      </div>
    );
  }
  return <Card><CardHeader><CardTitle className="flex items-center gap-2"><History className="h-4 w-4" />{t("projects.snapshots")}</CardTitle><CardDescription>{t("projects.snapshotsDescription")}</CardDescription></CardHeader><CardContent>{query.isLoading ? <Loading t={t} /> : query.isError ? <ErrorState t={t} onRetry={() => void query.refetch()} /> : rows.length === 0 ? <Empty>{t("projects.noSnapshots")}</Empty> : <div className="divide-y">{rows.map((row) => <div key={row.id} className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="flex min-w-0 items-start gap-3"><Camera className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" /><div className="min-w-0"><p className="text-sm font-medium">{row.snapshotName}</p><p className="mt-1 text-xs text-muted-foreground">{t("projects.effectiveEndDate")}: {formatDate(row.effectiveEndDate)} · {t("projects.snapshotDate")}: {formatDate(row.snapshotDate)}</p><p className="mt-1 text-xs text-muted-foreground">{row.previousProjectManagerName || "—"} → {row.newProjectManagerName || "—"}</p></div></div><Link to={row.id}><Button variant="outline" size="sm"><Camera className="me-1.5 h-3.5 w-3.5" />{t("projects.viewSnapshot")}</Button></Link></div>)}</div>}</CardContent></Card>;
}

function SnapshotValue({ label, value }: { label: string; value?: string | number | null }) {
  return <div className="flex justify-between py-1 text-sm"><span className="text-muted-foreground">{label}</span><span className="max-w-[60%] text-right font-medium">{value ?? "-"}</span></div>;
}

function ReportDetail({ label, value }: { label: string; value?: React.ReactNode }) {
  return <div className="rounded-lg border bg-muted/20 p-3"><p className="text-xs text-muted-foreground">{label}</p><div className="mt-1 text-sm font-medium">{value ?? "—"}</div></div>;
}

function ReportNarrative({ label, value }: { label: string; value?: string }) {
  return <div className="rounded-lg border p-3"><p className="text-xs font-semibold text-muted-foreground">{label}</p><p className="mt-2 whitespace-pre-wrap text-sm">{value || "—"}</p></div>;
}

type SnapshotItem = { name?: string; title?: string; summary?: string; description?: string; status?: string; role?: string; resourceName?: string; assigneeName?: string; ownerName?: string; category?: string; amount?: number; approvedAmount?: number; currency?: string; allocationPercentage?: number; progress?: number; riskLevel?: string; severity?: string; version?: number; startDate?: string; endDate?: string; dueDate?: string; startTime?: string; incurredDate?: string; reportedAt?: string; [key: string]: string | number | boolean | undefined };

function SnapshotRow({ item, section, currency }: { item: SnapshotItem; section: string; currency: string }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const title = item.name || item.title || item.summary || item.description || item.category || "—";
  const money = (v: unknown, code?: string) => typeof v === "number" && Number.isFinite(v) ? formatCurrency(v, code || currency) : "—";
  const localizedEnum = (group: "riskLevel" | "riskStatus", value?: string) => {
    if (!value) return undefined;
    const key = `enums.${group}.${value.charAt(0).toLowerCase()}${value.slice(1)}`;
    const label = t(key);
    return label === key ? value : label;
  };
  let detail: React.ReactNode;
  switch (section) {
    case "members":
    case "allocations": {
      const dates = [item.startDate ? formatDate(item.startDate) : undefined, item.endDate ? formatDate(item.endDate) : undefined];
      const metadata = [item.role, typeof item.allocationPercentage === "number" ? `${item.allocationPercentage}%` : undefined, ...dates].filter(Boolean);
      detail = <>{item.resourceName || title}{metadata.length ? <span className="ms-1.5 text-muted-foreground">{metadata.join(" · ")}</span> : null}</>;
      break;
    }
    case "risks": {
      const level = localizedEnum("riskLevel", item.riskLevel);
      const status = localizedEnum("riskStatus", item.status);
      detail = <span className="inline-flex items-center gap-1.5">{level ? <span className={getRiskLevelTextClass(item.riskLevel)}>{level}</span> : null}{level && status ? <span aria-hidden="true">·</span> : null}{status ? <span className={getRiskStatusTextClass(item.status)}>{status}</span> : null}</span>;
      break;
    }
    case "issues":
      detail = <span>{[item.severity, item.status].filter(Boolean).join(" · ") || "—"}</span>;
      break;
    case "budgets":
      detail = <span className="tabular-nums">{money(item.approvedAmount ?? item.amount, item.currency)}</span>;
      break;
    case "costEntries":
      detail = <span className="tabular-nums">{money(item.amount, item.currency)}{item.incurredDate ? ` · ${formatDate(item.incurredDate)}` : ""}</span>;
      break;
    case "milestones":
    case "deliverables": {
      const dueDate = item.dueDate ? formatDate(item.dueDate) : undefined;
      detail = <span>{[item.status, dueDate].filter(Boolean).join(" · ") || "—"}</span>;
      break;
    }
    case "phases":
      detail = <span>{[item.status, typeof item.progress === "number" ? `${item.progress}%` : undefined].filter(Boolean).join(" · ") || "—"}</span>;
      break;
    case "meetings":
      detail = <span>{item.startTime ? formatDate(item.startTime) : item.status || "—"}</span>;
      break;
    case "documents":
      detail = <span>{[typeof item.version === "number" ? `v${item.version}` : undefined, item.status].filter(Boolean).join(" · ") || "—"}</span>;
      break;
    case "statusReports":
      detail = <span>{item.reportedAt ? formatDate(item.reportedAt) : item.status || "—"}</span>;
      break;
    case "workItems":
      detail = <span>{[item.status, item.assigneeName].filter(Boolean).join(" · ") || "—"}</span>;
      break;
    default:
      detail = <span>{item.status || item.role || item.resourceName || item.assigneeName || (typeof item.amount === "number" ? money(item.amount, item.currency) : t("common.notAvailable"))}</span>;
      break;
  }
  return <><span className="min-w-0 truncate font-medium">{title}</span><span className="shrink-0 text-xs text-muted-foreground">{detail}</span></>;
}

export function ProjectChangeTrackerTab() {
  const { projectId, project } = useProjectContext();
  const { t, locale, formatDate } = useI18n();
  const { user, status: authStatus } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.View));
  const query = useProjectChangeRequests(projectId, authStatus === "authenticated" && canView);
  const cancel = useCancelProjectChangeRequest(projectId);
  const [cancellingRequest, setCancellingRequest] = React.useState<ProjectChangeRequestDto | null>(null);
  const [cancelError, setCancelError] = React.useState<string | null>(null);
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.Manage) && canMutate && !effectiveReadOnly);
  const rows: ProjectChangeRequestDto[] = canView ? query.data ?? [] : [];
  function changeTypeLabel(type: string) {
    return type === "Baseline" ? t("projects.changeTypeBaseline") : t("projects.changeTypeAdministrative");
  }
  function confirmCancel(request: ProjectChangeRequestDto) {
    setCancelError(null);
    setCancellingRequest(request);
  }
  async function handleCancel() {
    if (!cancellingRequest) return;
    setCancelError(null);
    try {
      await cancel.mutateAsync(cancellingRequest.id);
      setCancellingRequest(null);
    } catch (reason) {
      setCancelError(reason instanceof Error ? reason.message : t("projects.changeRequestFailed"));
    }
  }
  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <GitBranch className="h-4 w-4" />
              {t("projects.changeTracker")}
              <span className="text-sm font-normal text-muted-foreground">({rows.length})</span>
            </CardTitle>
            <CardDescription>{t("projects.changeTrackerDescription")}</CardDescription>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          {authStatus === "loading" ? <Skeleton className="h-40 w-full rounded-none" /> : !canView ? (
            <p role="alert" className="m-4 rounded-md border border-border/70 p-4 text-sm text-muted-foreground">{t("projects.changeRequestViewDenied")}</p>
          ) : query.isLoading ? <Skeleton className="h-40 w-full rounded-none" /> : query.isError ? (
            <div role="alert" className="flex flex-col items-center gap-3 py-12 text-center">
              <AlertTriangle className="h-10 w-10 text-destructive" />
              <div className="space-y-1">
                <p className="text-sm font-medium text-foreground">{t("projects.changeTrackerError")}</p>
                <p className="text-xs text-muted-foreground">{t("projects.changeTrackerErrorDescription")}</p>
              </div>
              <Button type="button" variant="outline" size="sm" disabled={query.isFetching} onClick={() => void query.refetch()}>
                <RefreshCw className={`me-1.5 h-3.5 w-3.5 ${query.isFetching ? "animate-spin" : ""}`} />
                {t("common.retry")}
              </Button>
            </div>
          ) : rows.length === 0 ? (
            <div className="flex flex-col items-center gap-2 py-12 text-center">
              <GitBranch className="h-10 w-10 text-muted-foreground/40" />
              <p className="text-sm font-medium text-foreground">{t("projects.noChangeRequests")}</p>
              <p className="text-xs text-muted-foreground">{t("projects.noChangeRequestsDescription")}</p>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("projects.changeType")}</TableHead>
                  <TableHead>{t("projects.changeNumber")}</TableHead>
                  <TableHead>{t("projects.changeReason")}</TableHead>
                  <TableHead>{t("projects.budgetChangeAmount")}</TableHead>
                  <TableHead>{t("common.status")}</TableHead>
                  <TableHead>{t("projects.requestedBy")}</TableHead>
                  <TableHead>{t("projects.requestedAt")}</TableHead>
                  <TableHead>{t("projects.reviewedBy")}</TableHead>
                  <TableHead>{t("projects.reviewedAt")}</TableHead>
                  <TableHead>{t("projects.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.id}>
                    <TableCell>{changeTypeLabel(row.type)}</TableCell>
                    <TableCell>#{row.number}</TableCell>
                    <TableCell className="max-w-[240px] truncate">{row.reason}</TableCell>
                    <TableCell className="whitespace-nowrap tabular-nums">
                      {typeof row.budgetChangeAmount === "number" && Number.isFinite(row.budgetChangeAmount) ? (
                        <span className={row.budgetChangeAmount < 0 ? "text-destructive" : row.budgetChangeAmount > 0 ? "text-success" : undefined}>
                          {row.budgetChangeAmount > 0 ? "+" : ""}{formatProjectChangeRequestAmount(row.budgetChangeAmount, project.currency, locale)}
                          {typeof row.proposedProjectBudget === "number" && Number.isFinite(row.proposedProjectBudget) ? <span className="block text-[11px] text-muted-foreground">{t("projects.proposedBudget")}: {formatProjectChangeRequestAmount(row.proposedProjectBudget, project.currency, locale)}</span> : null}
                        </span>
                      ) : "—"}
                    </TableCell>
                    <TableCell><Badge status={row.status}>{t(`projects.changeStatus.${row.status}`)}</Badge></TableCell>
                    <TableCell>{row.requestedByName}</TableCell>
                    <TableCell className="whitespace-nowrap">{formatDate(row.requestedAt)}</TableCell>
                    <TableCell>{row.reviewedByName || "—"}</TableCell>
                    <TableCell className="whitespace-nowrap">{row.reviewedAt ? formatDate(row.reviewedAt) : "—"}</TableCell>
                    <TableCell>
                      {canManage && row.status === "Pending" && row.requestedBy === user?.id ? <Button size="sm" variant="outline" onClick={() => confirmCancel(row)} disabled={cancel.isPending}>{t("projects.cancelChangeRequest")}</Button> : null}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    <Dialog open={Boolean(cancellingRequest)} ariaLabel={t("projects.cancelChangeRequestTitle")} onOpenChange={(isOpen) => { if (!isOpen && !cancel.isPending) { setCancellingRequest(null); setCancelError(null); } }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("projects.cancelChangeRequestTitle")}</DialogTitle>
          <DialogDescription>{t("projects.cancelChangeRequestDescription")}</DialogDescription>
        </DialogHeader>
        {cancelError ? <p role="alert" className="text-sm text-destructive">{cancelError}</p> : null}
        <DialogFooter>
          <Button type="button" variant="outline" disabled={cancel.isPending} onClick={() => setCancellingRequest(null)}>{t("common.cancel")}</Button>
          <Button type="button" variant="destructive" disabled={cancel.isPending} onClick={() => void handleCancel()}>{cancel.isPending ? <Loader2 className="me-1.5 h-4 w-4 animate-spin" /> : null}{t("projects.confirmCancel")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>);
}

export function ProjectMeetingsTab() {
  const { projectId, project } = useProjectContext();
  const { t, locale } = useI18n();
  const { user, status: authStatus } = useAuth();
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState("all");
  const [page, setPage] = React.useState(1);
  const [deferredSearch] = useDebounce(search.trim(), 400);
  const [open, setOpen] = React.useState(false);
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Create));
  const query = useFullMeetings({ projectId, page, pageSize: 10, search: deferredSearch || undefined, status: status === "all" ? undefined : status }, authStatus === "authenticated" && Boolean(projectId) && canView);
  const rows = query.data?.items ?? [];
  const totalPages = query.data?.totalPages ?? 1;
  function meetingStatusLabel(value?: string | null) {
    const key = (value || "").trim().toLowerCase();
    if (key === "scheduled" || key === "completed" || key === "cancelled") return t(`meetings.status.${key}`);
    return value?.trim() || t("meetings.status.unknown");
  }
  function formatTime(value?: string | null) {
    if (!value) return "—";
    return formatDateTime(value, locale);
  }
  if (authStatus === "loading") return <div className="space-y-2">{[0, 1, 2].map((item) => <Skeleton key={item} className="h-16 w-full" />)}</div>;
  if (!canView) return <Empty>{t("projects.projectMeetings.permissionDenied")}</Empty>;
  return <div className="space-y-4"><Card><CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><CardTitle className="flex items-center gap-2"><CalendarDays className="h-4 w-4" />{t("projects.projectMeetings.title")}</CardTitle><CardDescription>{t("projects.projectMeetings.description")}</CardDescription></div>{canCreate ? <Button size="sm" onClick={() => setOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t("meetings.createMeeting")}</Button> : null}</CardHeader>
  <div className="flex flex-col gap-2 px-4 pb-3 sm:flex-row">
    <div className="relative flex-1">
      <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
      <Input value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} placeholder={t("meetings.searchPlaceholder")} aria-label={t("meetings.searchPlaceholder")} className="bg-background ps-9" />
    </div>
    <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
      <SelectTrigger className="sm:w-44" aria-label={t("common.status")}><SelectValue placeholder={t("common.status")} /></SelectTrigger>
      <SelectContent>
        <SelectItem value="all">{t("common.all")}</SelectItem>
        {["Scheduled", "Completed", "Cancelled"].map((s) => <SelectItem key={s} value={s}>{meetingStatusLabel(s)}</SelectItem>)}
      </SelectContent>
    </Select>
  </div>
  <CardContent className="p-0">{query.isLoading ? <Loading t={t} /> : query.isError ? query.error instanceof ApiError && query.error.status === 403 ? <div className="rounded border border-dashed p-8 text-center text-sm text-muted-foreground"><p>{t("projects.projectMeetings.permissionDenied")}</p><Button className="mt-3" variant="outline" onClick={() => void query.refetch()}>{t("common.retry")}</Button></div> : <ErrorState t={t} onRetry={() => void query.refetch()} /> : rows.length === 0 ? <Empty><span className="block font-medium text-foreground">{t("projects.projectMeetings.emptyTitle")}</span><span className="mt-1 block">{t("projects.projectMeetings.emptyDescription")}</span></Empty> : <div className="divide-y px-4">{rows.map((row) => <Link key={row.id} to={`/meetings/${encodeURIComponent(row.id)}`} className="group flex items-center gap-3 py-3 hover:bg-muted/20"><span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary"><CalendarCheck className="h-4 w-4" /></span><span className="min-w-0 flex-1"><span className="block truncate text-sm font-semibold">{row.title}</span><span className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-[11px] text-muted-foreground"><span>{formatTime(row.startTime)}</span><span aria-hidden="true">·</span><span>{row.organizerName || "—"}</span><span aria-hidden="true">·</span><span className="inline-flex items-center gap-1"><Users className="h-3 w-3" />{row.attendeeCount}</span></span></span><Badge status={row.status || undefined}>{meetingStatusLabel(row.status)}</Badge><ExternalLink className="h-3.5 w-3.5 shrink-0 text-muted-foreground opacity-0 transition-opacity group-hover:opacity-100" /></Link>)}
  {totalPages > 1 ? <div className="py-3"><Pagination page={page} totalPages={totalPages} total={query.data?.totalCount} pageSize={10} onChange={setPage} /></div> : null}</div>}</CardContent></Card><ProjectMeetingCreateSheet open={open} onOpenChange={setOpen} projectId={projectId} project={project} /></div>;
}

function ProjectMeetingCreateSheet({ open, onOpenChange, projectId, project }: { open: boolean; onOpenChange: (open: boolean) => void; projectId: string; project: ProjectDto }) {
  const { t } = useI18n();
  const [draft, setDraft] = React.useState(() => createMeetingDraft());
  const [attendeeSearch, setAttendeeSearch] = React.useState("");
  const [error, setError] = React.useState<string | null>(null);
  const mutations = useMeetingFullMutations();
  const usersQuery = useUserLookup(undefined, open);
  const users = usersQuery.data;
  const userOptions = React.useMemo(() => (users ?? []).map((user) => ({ value: user.id, label: `${user.fullName} · ${user.email}`, keywords: [user.jobTitle ?? ""] })), [users]);
  const attendeeQuery = attendeeSearch.trim().toLocaleLowerCase();
  const visibleUsers = (users ?? []).filter((candidate) => !attendeeQuery || `${candidate.fullName} ${candidate.email} ${candidate.jobTitle ?? ""}`.toLocaleLowerCase().includes(attendeeQuery));
  const externalEmail = attendeeSearch.trim();
  const matchingInternalUser = (users ?? []).some((candidate) => candidate.email.trim().toLocaleLowerCase() === externalEmail.toLocaleLowerCase());
  const canAddExternal = isValidMeetingEmail(externalEmail)
    && !matchingInternalUser
    && !draft.attendees.some((attendee) => attendee.email.trim().toLocaleLowerCase() === externalEmail.toLocaleLowerCase());

  React.useEffect(() => {
    if (!open) return;
    setDraft(createMeetingDraft());
    setAttendeeSearch("");
    setError(null);
  }, [open]);

  function toggleAttendee(person: UserLookupDto) {
    const selected = draft.attendees.some((attendee) => attendee.userId === person.id);
    setDraft((current) => ({ ...current, attendees: selected
      ? current.attendees.filter((attendee) => attendee.userId !== person.id)
      : [...current.attendees, { userId: person.id, email: person.email, displayName: person.fullName }] }));
  }

  function addExternalAttendee() {
    if (!canAddExternal) return;
    setDraft((current) => ({ ...current, attendees: [...current.attendees, { email: externalEmail }] }));
    setAttendeeSearch("");
  }

  function removeAttendee(attendee: MeetingAttendeeDraft) {
    setDraft((current) => ({ ...current, attendees: current.attendees.filter((candidate) => candidate.userId !== attendee.userId || candidate.email !== attendee.email) }));
  }

  function updateExternalName(email: string, displayName: string) {
    setDraft((current) => ({ ...current, attendees: current.attendees.map((attendee) => attendee.email === email && !attendee.userId ? { ...attendee, displayName } : attendee) }));
  }

  function save() {
    const validationError = validateMeetingDraft(draft);
    if (validationError) {
      setError(t(`meetings.validation.${validationError}`));
      return;
    }
    setError(null);
    const body: MeetingInput = toMeetingInput(draft, projectId);
    mutations.create.mutate(body, {
      onSuccess: () => onOpenChange(false),
      onError: () => setError(t("meetings.saveError")),
    });
  }

  return <SideSheet open={open} onOpenChange={onOpenChange} title={t("meetings.createMeeting")} description={t("meetings.createDescription")}>
    <SideSheetBody className="space-y-4">
      <div className="space-y-1.5"><Label htmlFor="project-meeting-title">{t("meetings.titleField")} *</Label><Input id="project-meeting-title" maxLength={500} value={draft.title} onChange={(event) => setDraft({ ...draft, title: event.target.value })} /></div>
      <div className="space-y-1.5"><Label htmlFor="project-meeting-objective">{t("meetings.objective")}</Label><Textarea id="project-meeting-objective" rows={3} value={draft.objective} onChange={(event) => setDraft({ ...draft, objective: event.target.value })} /></div>
      <div className="space-y-1.5"><Label>{t("meetings.project")}</Label><div className="rounded-md border bg-muted/20 px-3 py-2"><p className="text-sm font-medium">{project.name}</p><p className="mt-0.5 text-xs text-muted-foreground">{project.code}</p></div></div>
      <div className="space-y-1.5"><Label>{t("meetings.organizer")} *</Label><SearchableSelect options={userOptions} value={draft.organizerId} onValueChange={(organizerId) => setDraft({ ...draft, organizerId })} placeholder={t("meetings.selectOrganizer")} searchPlaceholder={t("meetings.searchUsers")} emptyText={t("meetings.noUsers")} loading={usersQuery.isLoading} /></div>
      <div className="space-y-2">
        <Label htmlFor="project-meeting-attendee-search">{t("meetings.attendees")}</Label>
        <Input id="project-meeting-attendee-search" value={attendeeSearch} onChange={(event) => setAttendeeSearch(event.target.value)} placeholder={t("meetings.searchUsers")} />
        {draft.attendees.length > 0 ? <p className="text-xs text-muted-foreground">{t("meetings.selectedUsers", { count: draft.attendees.length })}</p> : null}
        {usersQuery.isLoading ? <p className="text-xs text-muted-foreground">{t("meetings.loadingUsers")}</p> : visibleUsers.length > 0 ? <div className="max-h-36 space-y-1 overflow-y-auto rounded-md border p-1">{visibleUsers.map((person) => {
          const selected = draft.attendees.some((attendee) => attendee.userId === person.id);
          return <button key={person.id} type="button" aria-pressed={selected} onClick={() => toggleAttendee(person)} className="flex w-full items-center gap-2 rounded px-2 py-1.5 text-start text-sm hover:bg-accent"><input type="checkbox" checked={selected} readOnly tabIndex={-1} /><UserRound className="h-3.5 w-3.5 shrink-0 text-muted-foreground" /><span className="min-w-0 flex-1 truncate">{person.fullName}<span className="ms-1 text-xs text-muted-foreground">{person.email}</span></span></button>;
        })}</div> : <p className="text-xs text-muted-foreground">{t("meetings.noUsers")}</p>}
        {canAddExternal ? <Button type="button" size="sm" variant="outline" onClick={addExternalAttendee}><Mail className="me-1.5 h-3.5 w-3.5" />{t("meetings.addExternalAttendee", { email: externalEmail })}</Button> : null}
        {draft.attendees.length > 0 ? <div className="space-y-2">{draft.attendees.map((attendee) => <div key={attendee.userId ?? attendee.email.toLocaleLowerCase()} className="flex items-start gap-2 rounded-md border bg-muted/20 p-2"><div className="min-w-0 flex-1"><p className="truncate text-xs font-medium">{attendee.displayName || attendee.email}</p><p className="truncate text-xs text-muted-foreground">{t(attendee.userId ? "meetings.internalUser" : "meetings.externalAttendee")} · {attendee.email}</p>{!attendee.userId ? <Input className="mt-1.5 h-8" maxLength={200} value={attendee.displayName ?? ""} onChange={(event) => updateExternalName(attendee.email, event.target.value)} placeholder={t("meetings.optionalDisplayName")} aria-label={t("meetings.optionalDisplayName")} /> : null}</div><Button type="button" size="sm" variant="ghost" aria-label={t("meetings.removeUser", { name: attendee.displayName || attendee.email })} onClick={() => removeAttendee(attendee)}><X className="h-4 w-4" /></Button></div>)}</div> : null}
      </div>
      <div className="grid gap-3 sm:grid-cols-2"><div className="space-y-1.5"><Label htmlFor="project-meeting-start">{t("meetings.start")} *</Label><Input id="project-meeting-start" type="datetime-local" value={draft.startTime} onChange={(event) => setDraft({ ...draft, startTime: event.target.value })} /></div><div className="space-y-1.5"><Label htmlFor="project-meeting-end">{t("meetings.end")} *</Label><Input id="project-meeting-end" type="datetime-local" value={draft.endTime} onChange={(event) => setDraft({ ...draft, endTime: event.target.value })} /></div></div>
      <div className="grid gap-3 sm:grid-cols-2"><div className="space-y-1.5"><Label htmlFor="project-meeting-location">{t("meetings.location")}</Label><Input id="project-meeting-location" value={draft.location} onChange={(event) => setDraft({ ...draft, location: event.target.value })} /></div><div className="space-y-1.5"><Label htmlFor="project-meeting-link">{t("meetings.meetingLink")}</Label><Input id="project-meeting-link" type="text" value={draft.meetingLink} onChange={(event) => setDraft({ ...draft, meetingLink: event.target.value })} /></div></div>
      <div className="space-y-1.5"><Label htmlFor="project-meeting-reminder">{t("meetings.reminder")}</Label><select id="project-meeting-reminder" className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={draft.reminderOffsetMinutes} onChange={(event) => setDraft({ ...draft, reminderOffsetMinutes: event.target.value })}>{[["", "noReminder"], ["15", "fifteenMinutesBefore"], ["30", "thirtyMinutesBefore"], ["60", "oneHourBefore"], ["1440", "oneDayBefore"]].map(([value, key]) => <option key={value || "none"} value={value}>{t(`meetings.${key}`)}</option>)}</select></div>
      {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
    </SideSheetBody>
    <SideSheetFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button><Button type="button" onClick={save} disabled={mutations.create.isPending}>{mutations.create.isPending ? t("common.saving") : t("common.save")}</Button></SideSheetFooter>
  </SideSheet>;
}
