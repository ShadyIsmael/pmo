import * as React from "react";
import { Link, useOutletContext } from "react-router-dom";
import { AlertTriangle, CalendarDays, Download, Eye, FileSpreadsheet, FileText, GitBranch, Headphones, ListChecks, Loader2, Pencil, Plus, Printer, RefreshCw, Send, TrendingUp, Trash2, UserRoundCheck } from "lucide-react";
import { useI18n } from "@i18n/messages";
import { getApiFieldError } from "@api/client";
import { useConfirm } from "@components/ui/ConfirmProvider";
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
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import {
  useCancelProjectHandoverRequest,
  useCreateProjectHandoverRequest,
  useCancelProjectChangeRequest,
  useProjectChangeRequestApproverOptions,
  useRemediateProjectChangeRequest,
  useReassignProjectChangeRequestApproval,
  useCreateProjectStatusReport,
  useDeleteProjectStatusReport,
  useProjectChangeRequests,
  useProjectHandoverRequests,
  useProjectHandoverCapabilities,
  useProjectHandoverManagers,
  useProjectStatusReports,
  useProjectStatusReport,
  requestProjectStatusReportExport,
  useProjectStatusReportComparison,
  useProjectStatusReportCurrentProgress,
  useUpdateProjectStatusReport,
  usePublishProjectStatusReport,
  useProjectFinancialSummary,
  useProjectIssues,
  type ProjectChangeRequestDto,
  type ProjectDto,
  type ProjectHandoverInitiationType,
  type ProjectStatusReportListDto,
  type ProjectStatusReportDto,
  type ProjectStatusReportComparisonDto,
  type ProjectStatusReportInput,
} from "./api/projects";
import { toHandoverManagerOptions } from "./lib/handover-manager-options";
import { MeetingsBrowser } from "@features/delivery/MeetingsBrowser";
import { exportProjectStatusReportExcel, exportProjectStatusReportPdf, printProjectStatusReport } from "@lib/export/project-status-report-export";
import { getEvmReasonTranslationCandidates, useProjectEnumLabels } from "./lib/project-labels";
import { useWorkItemStatuses } from "@api/workItems";
import { PROJECT_STATUSES } from "@lib/enums";
import { formatDateTime } from "@lib/utils";
import { formatNumberValue } from "@lib/formatting/number";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { getStatusTextClass, getStatusTone, statusToneDotClasses } from "@lib/status-presentation";
import { useRisks } from "@features/risks/api/risks";
import { formatProjectChangeRequestAmount } from "./lib/project-change-request-format";
import { ProjectChangeRequestActionSheet } from "./components/ProjectOverviewActionSheets";

function localizedEnumValue(t: (key: string) => string, namespace: string, value?: string | null): string {
  if (!value) return "—";
  const key = `${namespace}.${value.charAt(0).toLowerCase()}${value.slice(1)}`;
  const label = t(key);
  return label === key ? value : label;
}

function localizedEvmReasonLabel(reason: string, t: (key: string) => string): string {
  for (const candidate of getEvmReasonTranslationCandidates(reason)) {
    const key = `projects.evmReasons.${candidate}`;
    const label = t(key);
    if (label !== key) return label;
  }
  return reason;
}

function workItemStatusLabel(status: string, statuses: { code: string; nameEn: string; nameAr: string }[], locale: string): string {
  const match = statuses.find((item) => item.code === status);
  if (!match) return status;
  const names = locale === "ar" ? [match.nameAr, match.nameEn] : [match.nameEn, match.nameAr];
  return names.find((name) => name.trim())?.trim() ?? status;
}

function formatPercent1(value: number | null | undefined, locale: string): string {
  if (typeof value !== "number" || !Number.isFinite(value)) return "—";
  return `${formatNumberValue(value, locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
}

function resourceUtilizationTextClass(status?: string | null): string {
  if (status === "Overallocated") return "text-destructive";
  if (status === "WithinCapacity") return "text-success";
  if (status === "Unavailable") return "text-warning";
  return "text-muted-foreground";
}

function statusReportInput(report: ProjectStatusReportDto): ProjectStatusReportInput {
  return {
    reportDate: report.reportDate.slice(0, 10),
    periodFrom: report.periodFrom.slice(0, 10),
    periodTo: report.periodTo.slice(0, 10),
    statusDate: (report.statusDate || report.reportDate).slice(0, 10),
    projectLifecycleStatusAtReport: report.projectLifecycleStatusAtReport,
    overallHealth: report.overallHealth,
    overallProgressPercent: report.overallProgressPercent,
    costHealth: report.costHealth,
    qualityHealth: report.qualityHealth,
    executiveSummary: report.executiveSummary,
    keyAchievements: report.keyAchievements,
    challengesConcerns: report.challengesConcerns,
    nextPeriodPriorities: report.nextPeriodPriorities,
    actionsNeeded: report.actionsNeeded,
    supportNeeded: report.supportNeeded,
    rowVersion: report.rowVersion,
  };
}

interface ProjectContext { projectId: string; project: ProjectDto }

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
  const confirm = useConfirm();
  const { statusLabel, healthLabel } = useProjectEnumLabels();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canViewIssues = Boolean(user && hasPermission(user.permissions, Permissions.Issue.View));
  const query = useProjectStatusReports(projectId);
  const create = useCreateProjectStatusReport(projectId);
  const today = React.useMemo(() => new Date().toISOString().slice(0, 10), []);
  const [periodFrom, setPeriodFrom] = React.useState(today);
  const [periodTo, setPeriodTo] = React.useState(today);
  const [detailId, setDetailId] = React.useState("");
  const detail = useProjectStatusReport(projectId, detailId, Boolean(detailId));
  const [editingReport, setEditingReport] = React.useState<ProjectStatusReportDto | null>(null);
  const selectedReportId = editingReport?.id ?? "";
  const update = useUpdateProjectStatusReport(projectId, selectedReportId);
  const publish = usePublishProjectStatusReport(projectId);
  const remove = useDeleteProjectStatusReport(projectId);
  const [formOpen, setFormOpen] = React.useState(false);
  const [formSession, setFormSession] = React.useState(0);
  const [progressDraft, setProgressDraft] = React.useState("");
  const progressTouched = React.useRef(false);
  const currentProgress = useProjectStatusReportCurrentProgress(projectId, today, formOpen, periodFrom, periodTo);
  const financialSummary = useProjectFinancialSummary(projectId, periodFrom, periodTo, Boolean(formOpen));
  const formatFinancialAmount = (amount: number | null | undefined) =>
    amount == null || !financialSummary.data?.currencyCode
      ? "—"
      : formatCurrency(amount, financialSummary.data.currencyCode);
  const financialBudgetStatus = financialSummary.data
    ? t(`projects.financialSummaryBudgetStatus.${financialSummary.data.budgetStatus}`)
    : "";
  const risks = useRisks({ projectId, pageSize: 100 }, Boolean(formOpen));
  const issues = useProjectIssues(projectId, Boolean(formOpen && canViewIssues));
  const workItemStatusesQuery = useWorkItemStatuses(Boolean(formOpen || detailId));
  React.useEffect(() => {
    if (!formOpen || (editingReport?.overallProgressPercent != null) || currentProgress.isLoading || currentProgress.isFetching || currentProgress.isError || !currentProgress.data) return;
    const suggestedProgress = currentProgress.data.projectPlanProgressPercent;
    const value = suggestedProgress == null ? "" : String(suggestedProgress);
    if (!progressTouched.current) setProgressDraft(value);
  }, [currentProgress.data, currentProgress.isError, currentProgress.isFetching, currentProgress.isLoading, editingReport?.overallProgressPercent, formOpen]);
  const [exporting, setExporting] = React.useState(false);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.StatusReport.Create) && canMutate && !effectiveReadOnly);
  const canEdit = Boolean(user && hasPermission(user.permissions, Permissions.StatusReport.Edit) && canMutate && !effectiveReadOnly);
  const canPublish = Boolean(user && hasPermission(user.permissions, Permissions.StatusReport.Publish) && canMutate && !effectiveReadOnly);
  const canExport = Boolean(user && hasPermission(user.permissions, Permissions.StatusReport.Export));
  const workflowPending = publish.isPending;
  const reports = query.data?.items ?? [];
  const lifecycleStatusDefault = editingReport ? editingReport.projectLifecycleStatusAtReport : project.status;
  const hasNonstandardLifecycleStatus = Boolean(lifecycleStatusDefault && !PROJECT_STATUSES.some((status) => status === lifecycleStatusDefault));
  const progressForSummary = progressTouched.current
    ? (progressDraft === "" ? null : Number(progressDraft))
    : editingReport
      ? editingReport.overallProgressPercent ?? (progressDraft === "" ? project.progress : Number(progressDraft))
      : progressDraft !== ""
        ? Number(progressDraft)
        : currentProgress.data?.projectPlanProgressPercent ?? project.progress;
  const progressBarValue = typeof progressForSummary === "number" && Number.isFinite(progressForSummary)
    ? Math.min(100, Math.max(0, progressForSummary))
    : null;
  const [comparisonSelection, setComparisonSelection] = React.useState<string[]>([]);
  const [comparisonOpen, setComparisonOpen] = React.useState(false);
  const comparisonQuery = useProjectStatusReportComparison(
    projectId,
    comparisonSelection[0] ?? "",
    comparisonSelection[1] ?? "",
    comparisonOpen,
  );
  const comparisonFirstReport = useProjectStatusReport(projectId, comparisonSelection[0] ?? "", comparisonOpen);
  const comparisonSecondReport = useProjectStatusReport(projectId, comparisonSelection[1] ?? "", comparisonOpen);
  const comparisonEarlierCurrency = comparisonQuery.data?.earlierReportId === comparisonSelection[0]
    ? comparisonFirstReport.data?.performanceSnapshot?.currency ?? comparisonFirstReport.data?.currencyCodeSnapshot
    : comparisonSecondReport.data?.performanceSnapshot?.currency ?? comparisonSecondReport.data?.currencyCodeSnapshot;
  const comparisonLaterCurrency = comparisonQuery.data?.laterReportId === comparisonSelection[0]
    ? comparisonFirstReport.data?.performanceSnapshot?.currency ?? comparisonFirstReport.data?.currencyCodeSnapshot
    : comparisonSecondReport.data?.performanceSnapshot?.currency ?? comparisonSecondReport.data?.currencyCodeSnapshot;

  function openNewReport() {
    setActionError(null);
    setEditingReport(null);
    progressTouched.current = false;
    setProgressDraft(project.progress == null ? "" : String(project.progress));
    setFormSession((session) => session + 1);
    setPeriodFrom(today);
    setPeriodTo(today);
    setFormOpen(true);
  }
  function openReport(reportId: string) { setActionError(null); setDetailId(reportId); }
  function toggleComparison(report: ProjectStatusReportListDto) {
    if (report.reportState !== "Published") return;
    setComparisonSelection((current) => current.includes(report.id)
      ? current.filter((id) => id !== report.id)
      : current.length < 2 ? [...current, report.id] : [current[1], report.id]);
  }

  async function exportReport() {
    if (!detail.data || detail.data.reportState !== "Published" || !canExport || exporting) return;
    setActionError(null);
    setExporting(true);
    try {
      const exportableReport = await requestProjectStatusReportExport(projectId, detail.data.id);
      await exportProjectStatusReportPdf({ report: exportableReport, publishedHistory: reports, locale: document.documentElement.lang, workItemStatuses: workItemStatusesQuery.data });
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : t("projects.statusReportExportFailed"));
    } finally {
      setExporting(false);
    }
  }

  async function exportExcelReport() {
    if (!detail.data || detail.data.reportState !== "Published" || !canExport || exporting) return;
    setActionError(null);
    setExporting(true);
    try {
      const exportableReport = await requestProjectStatusReportExport(projectId, detail.data.id);
      await exportProjectStatusReportExcel({ report: exportableReport, locale });
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : t("projects.statusReportExportFailed"));
    } finally {
      setExporting(false);
    }
  }

  async function printReport() {
    if (!detail.data || detail.data.reportState !== "Published" || !canExport || exporting) return;
    setActionError(null);
    setExporting(true);
    try {
      const exportableReport = await requestProjectStatusReportExport(projectId, detail.data.id);
      await printProjectStatusReport({ report: exportableReport, publishedHistory: reports, locale: document.documentElement.lang, workItemStatuses: workItemStatusesQuery.data });
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : t("projects.statusReportPrintFailed"));
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
      reportDate: today,
      periodFrom,
      periodTo,
      statusDate: today,
      projectLifecycleStatusAtReport: String(data.get("projectLifecycleStatusAtReport") || "") || undefined,
      overallHealth: String(data.get("overallHealth") || "") || undefined,
      overallProgressPercent: progressRaw === "" ? undefined : Number(progressRaw),
      executiveSummary: String(data.get("executiveSummary") || "").trim() || undefined,
      keyAchievements: String(data.get("keyAchievements") || "").trim() || undefined,
      challengesConcerns: String(data.get("challengesConcerns") || "").trim() || undefined,
      nextPeriodPriorities: String(data.get("nextPeriodPriorities") || "").trim() || undefined,
      costHealth: String(data.get("costHealth") || "") || undefined,
      qualityHealth: String(data.get("qualityHealth") || "") || undefined,
      actionsNeeded: String(data.get("actionsNeeded") || "").trim() || undefined,
      supportNeeded: String(data.get("supportNeeded") || "").trim() || undefined,
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

  async function publishReport(report: ProjectStatusReportDto) {
    if (!await confirm({ description: t("projects.publishStatusReportConfirm") })) return;
    setActionError(null);
    try {
      await publish.mutateAsync({ reportId: report.id, content: statusReportInput(report) });
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : t("projects.statusReportPublishFailed"));
    }
  }

  async function confirmDelete(reportId: string) {
    if (!await confirm({ description: t("projects.deleteStatusReportConfirm"), destructive: true })) return;
    setActionError(null);
    try { await remove.mutateAsync(reportId); setDetailId(""); } catch (reason) { setActionError(reason instanceof Error ? reason.message : t("projects.statusReportDeleteFailed")); }
  }

  return <div className="space-y-4">
    <Card>
      <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div><CardTitle className="flex flex-wrap items-center gap-2"><Send className="h-4 w-4" />{t("projects.statusReportsTitle")}<Badge status={project.status} className="text-[10px]">{statusLabel(project.status)}</Badge></CardTitle><CardDescription>{t("projects.statusReportsDescription")}</CardDescription></div>
        <div className="flex flex-wrap gap-2">
          {comparisonSelection.length === 2 ? <Button size="sm" variant="outline" onClick={() => setComparisonOpen(true)}>{t("projects.statusReportComparison.compareSelected")}</Button> : null}
          {canCreate ? <Button size="sm" onClick={openNewReport}><Plus className="me-1.5 h-4 w-4" />{t("projects.newStatusReport")}</Button> : null}
        </div>
      </CardHeader>
      <CardContent className="p-0">
        {query.isLoading ? <Loading t={t} /> : query.isError ? <ErrorState t={t} onRetry={() => void query.refetch()} /> : reports.length === 0 ? <Empty>{t("projects.noStatusReportsDescription")}</Empty> :
          <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("projects.statusReportComparison.select")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.reportDate")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.statusDate")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.reportingPeriod")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.projectStatusAtReport")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.overallHealth")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.overallProgress")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.reportState")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.preparedBy")}</TableHead><TableHead className="whitespace-nowrap">{t("projects.publishedAt")}</TableHead><TableHead className="text-end">{t("projects.view")}</TableHead></TableRow></TableHeader><TableBody>
            {reports.map((report) => { const selected = comparisonSelection.includes(report.id); return <TableRow key={report.id}><TableCell><input type="checkbox" aria-label={t("projects.statusReportComparison.selectReport", { date: formatDate(report.statusDate) })} checked={selected} disabled={report.reportState !== "Published" || (comparisonSelection.length === 2 && !selected)} onChange={() => toggleComparison(report)} /></TableCell><TableCell>{formatDate(report.reportDate)}</TableCell><TableCell>{formatDate(report.statusDate)}</TableCell><TableCell className="whitespace-nowrap">{formatDate(report.periodFrom)} → {formatDate(report.periodTo)}</TableCell><TableCell><Badge status={report.projectLifecycleStatusAtReport} className="text-[10px]">{report.projectLifecycleStatusAtReport ? statusLabel(report.projectLifecycleStatusAtReport) : "—"}</Badge></TableCell><TableCell><Badge status={report.overallHealth} className="text-[10px]">{report.overallHealth ? healthLabel(report.overallHealth) : "—"}</Badge></TableCell><TableCell className="tabular-nums">{formatPercent1(report.overallProgressPercent, locale)}</TableCell><TableCell><Badge status={report.reportState}>{t(`projects.statusReportStates.${report.reportState}`)}</Badge></TableCell><TableCell>{report.preparedByNameSnapshot || "—"}</TableCell><TableCell className="whitespace-nowrap">{report.publishedAt ? formatDateTime(report.publishedAt, locale) : "—"}</TableCell><TableCell><div className="flex justify-end"><Button size="sm" variant="ghost" onClick={() => openReport(report.id)}><Eye className="me-1.5 h-3.5 w-3.5" />{t("projects.view")}</Button></div></TableCell></TableRow>; })}</TableBody></Table></div>}
          
      </CardContent>
    </Card>
    <StatusReportComparisonDialog open={comparisonOpen} onOpenChange={setComparisonOpen} comparison={comparisonQuery.data} loading={comparisonQuery.isLoading} error={comparisonQuery.isError} earlierCurrency={comparisonEarlierCurrency} laterCurrency={comparisonLaterCurrency} locale={locale} t={t} formatDate={formatDate} formatCurrency={formatCurrency} />
    <SideSheet open={formOpen} onOpenChange={(next) => { setFormOpen(next); if (!next) { setEditingReport(null); setActionError(null); } }} title={editingReport ? t("projects.editStatusReport") : t("projects.newStatusReport")} description={t("projects.statusReportFormDescription")} size="xl">
      <form key={formSession} onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col">
        <SideSheetBody className="space-y-4">
          <div className="grid gap-3 xl:grid-cols-[minmax(0,1.25fr)_minmax(260px,0.75fr)]">
            <section className="rounded-xl border p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="text-xs font-medium text-muted-foreground">{t("projects.name")}</p>
                  <h3 className="mt-1 truncate text-base font-semibold text-foreground">{project.name}</h3>
                  <p className="mt-0.5 text-xs text-muted-foreground">{project.code || "—"}</p>
                </div>
                {project.status ? <Badge status={project.status} className="shrink-0 text-[10px]">{statusLabel(project.status)}</Badge> : null}
              </div>
              <div className="mt-4 grid gap-x-4 gap-y-3 border-t pt-3 sm:grid-cols-3">
                <div><p className="text-[11px] text-muted-foreground">{t("projects.projectManager")}</p><p className="mt-0.5 truncate text-sm font-medium">{project.projectManagerName || "—"}</p></div>
                <div><p className="text-[11px] text-muted-foreground">{t("projects.preparedBy")}</p><p className="mt-0.5 truncate text-sm font-medium">{user?.fullName || "—"}</p></div>
                <div><p className="text-[11px] text-muted-foreground">{t("projects.health")}</p><p className="mt-0.5 text-sm font-medium">{project.health ? healthLabel(project.health) : "—"}</p></div>
              </div>
            </section>

            <section className="rounded-xl border border-primary/20 p-4" aria-label={t("projects.overallProgress")}>
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="text-xs font-medium text-muted-foreground">{t("projects.overallProgress")}</p>
                  <p className="mt-1 text-3xl font-semibold tabular-nums text-foreground">{formatPercent1(progressForSummary, locale)}</p>
                </div>
                <span className="rounded-lg border p-2 text-primary" aria-hidden="true"><TrendingUp className="h-4 w-4" /></span>
              </div>
              {progressBarValue !== null ? <div className="mt-3 h-2 overflow-hidden rounded-full bg-muted" role="progressbar" aria-label={t("projects.overallProgress")} aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(progressBarValue)}><div className="h-full rounded-full bg-primary transition-[width]" style={{ width: String(progressBarValue) + "%" }} /></div> : null}
              <p className="mt-2 text-xs text-muted-foreground" role={!editingReport && currentProgress.isFetching ? "status" : undefined}>
                {editingReport?.overallProgressPercent != null
                  ? t("projects.statusReportProgressDraftValue")
                  : progressTouched.current
                    ? t("projects.statusReportProgressEditable")
                    : currentProgress.isFetching
                      ? t("projects.statusReportProgressLoading")
                      : currentProgress.isError
                        ? project.progress == null ? t("projects.statusReportProgressUnavailable") : t("projects.statusReportProgressFallback")
                        : progressForSummary == null
                          ? t("projects.statusReportProgressNoSource")
                          : t("projects.statusReportProgressAutoAtPublish")}
              </p>
              {!progressTouched.current && currentProgress.isError ? <Button type="button" size="sm" variant="outline" className="mt-2" onClick={() => void currentProgress.refetch()}><RefreshCw className="me-1.5 h-3.5 w-3.5" />{t("common.retry")}</Button> : null}
            </section>
          </div>

          <section className="space-y-4 rounded-xl border p-4">
            <div className="flex items-center gap-2 border-b pb-3"><CalendarDays className="h-4 w-4 text-primary" aria-hidden="true" /><h3 className="text-sm font-semibold">{t("projects.reportingPeriodSection")}</h3></div>
            <div className="grid gap-3 sm:grid-cols-2">
<div><Label htmlFor="status-report-period-from">{t("projects.periodFrom")}</Label><Input id="status-report-period-from" className="mt-1" name="periodFrom" type="date" value={periodFrom} onChange={(event) => setPeriodFrom(event.currentTarget.value)} max={periodTo} required /></div>
               <div><Label htmlFor="status-report-period-to">{t("projects.periodTo")}</Label><Input id="status-report-period-to" className="mt-1" name="periodTo" type="date" value={periodTo} onChange={(event) => setPeriodTo(event.currentTarget.value)} max={today} required /></div>
            </div>
          </section>

          <section className="space-y-4 rounded-xl border p-4">
            <div className="flex items-center gap-2 border-b pb-3"><TrendingUp className="h-4 w-4 text-primary" aria-hidden="true" /><h3 className="text-sm font-semibold">{t("projects.overallStatusSection")}</h3></div>
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              <div><Label htmlFor="status-report-lifecycle">{t("projects.projectStatusAtReport")}</Label><select id="status-report-lifecycle" className="input-field mt-1 w-full" name="projectLifecycleStatusAtReport" defaultValue={editingReport ? editingReport.projectLifecycleStatusAtReport ?? "" : project.status || ""}><option value="">—</option>{hasNonstandardLifecycleStatus ? <option value={lifecycleStatusDefault!}>{statusLabel(lifecycleStatusDefault)}</option> : null}{PROJECT_STATUSES.map((state) => <option key={state} value={state}>{statusLabel(state)}</option>)}</select></div>
              <div><Label htmlFor="status-report-overall-health">{t("projects.overallHealth")}</Label><select id="status-report-overall-health" className="input-field mt-1 w-full" name="overallHealth" defaultValue={editingReport ? editingReport.overallHealth ?? "" : project.health || ""}><option value="">—</option>{"Green,Amber,Red".split(",").map((health) => <option key={health} value={health}>{healthLabel(health)}</option>)}</select></div>
              <div><Label htmlFor="status-report-progress">{t("projects.overallProgress")}</Label><Input id="status-report-progress" className="mt-1" name="overallProgressPercent" type="number" min="0" max="100" step="any" value={progressDraft} onChange={(event) => { progressTouched.current = true; setProgressDraft(event.currentTarget.value); }} /><p className="mt-1 text-[11px] leading-4 text-muted-foreground">{t("projects.statusReportProgressEditable")}</p></div>
            </div>
          </section>

          <section className="space-y-4 rounded-xl border p-4">
            <div className="flex items-center gap-2 border-b pb-3"><FileText className="h-4 w-4 text-primary" aria-hidden="true" /><h3 className="text-sm font-semibold">{t("projects.financialSummarySection")}</h3></div>
            {financialSummary.isLoading ? (
              <div className="grid gap-3 sm:grid-cols-2 md:grid-cols-4 xl:grid-cols-7">{[1, 2, 3, 4, 5, 6, 7].map((i) => <Card key={i}><CardContent className="p-3"><Skeleton className="h-8 w-full" /><Skeleton className="mt-2 h-5 w-3/4" /></CardContent></Card>)}</div>
            ) : financialSummary.isError ? (
              <p className="text-sm text-destructive">{t("projects.financialSummaryLoadFailed")}</p>
            ) : !financialSummary.data?.hasFinancialData ? (
              <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">{t("projects.financialSummaryEmpty")}</p>
            ) : (
              <div className="grid gap-3 sm:grid-cols-2 md:grid-cols-4 xl:grid-cols-7">
                <Card><CardContent className="p-3"><div className="text-[11px] text-muted-foreground">{t("projects.approvedAnnualBudget")}</div><div className="mt-1 text-sm font-semibold tabular-nums">{formatFinancialAmount(financialSummary.data.approvedBudget)}</div></CardContent></Card>
                <Card><CardContent className="p-3"><div className="text-[11px] text-muted-foreground">{t("projects.expenditure")}</div><div className="mt-1 text-sm font-semibold tabular-nums">{formatFinancialAmount(financialSummary.data.expenditure)}</div></CardContent></Card>
                <Card><CardContent className="p-3"><div className="text-[11px] text-muted-foreground">{t("projects.committedCost")}</div><div className="mt-1 text-sm font-semibold tabular-nums">{formatFinancialAmount(financialSummary.data.committedCost)}</div></CardContent></Card>
                <Card><CardContent className="p-3"><div className="text-[11px] text-muted-foreground">{t("projects.actualPaid")}</div><div className="mt-1 text-sm font-semibold tabular-nums">{formatFinancialAmount(financialSummary.data.actualPaid)}</div></CardContent></Card>
                <Card><CardContent className="p-3"><div className="text-[11px] text-muted-foreground">{t("projects.outstandingPlannedPayments")}</div><div className="mt-1 text-sm font-semibold tabular-nums">{formatFinancialAmount(financialSummary.data.outstandingPlannedPayments)}</div></CardContent></Card>
                <Card><CardContent className="p-3"><div className="text-[11px] text-muted-foreground">{t("projects.paymentsDueWithinPeriod")}</div><div className="mt-1 text-sm font-semibold tabular-nums">{formatFinancialAmount(financialSummary.data.paymentsDueWithinPeriod)}</div></CardContent></Card>
                <Card><CardContent className="p-3"><div className="text-[11px] text-muted-foreground">{t("projects.remainingUnpaidAtPeriodEnd")}</div><div className="mt-1 text-sm font-semibold tabular-nums">{formatFinancialAmount(financialSummary.data.remainingUnpaidAtPeriodEnd)}</div></CardContent></Card>
              </div>
            )}
            <p className="mt-2 text-xs text-muted-foreground">{t("projects.financialSummaryPeriod", { from: formatDate(financialSummary.data?.periodFrom ?? periodFrom), to: formatDate(financialSummary.data?.periodTo ?? periodTo) })}</p>
            {financialSummary.data?.hasFinancialData && financialSummary.data.dataStatus !== "available" ? <p className="mt-1 text-xs text-amber-700 dark:text-amber-300">{financialSummary.data.dataStatus === "partial" ? t("projects.financialSummaryPartial") : t("projects.financialSummaryUnavailable")}</p> : null}
            <p className="mt-0.5 text-[11px] text-muted-foreground">{t("projects.budgetStatusLabel", { status: financialBudgetStatus })}</p>
          </section>

          <section className="space-y-4 rounded-xl border p-4">
            <div className="grid gap-4 lg:grid-cols-2">
              <Card>
                <CardHeader className="flex items-center justify-between gap-2">
                  <span className="flex items-center gap-1.5"><AlertTriangle className="h-4 w-4 text-primary" aria-hidden="true" /><h3 className="text-sm font-semibold" aria-hidden="true">{t("projects.topRisks")}</h3></span>
                  <span className="text-xs font-semibold tabular-nums text-foreground">{risks.data?.items?.length ?? 0}</span>
                </CardHeader>
                <CardContent className="p-0">
                  {risks.isLoading ? <div className="p-3"><Skeleton className="h-8 w-full" /><Skeleton className="mt-2 h-4 w-2/3" /></div> : risks.isError ? <p className="p-3 text-sm text-destructive">{t("projects.risksIssuesLoadFailed")}</p> : risks.data?.items?.length ? <div className="space-y-1">{risks.data!.items.map((item) => <div key={item.id} className="border-b border-border last:border-b-0 py-2 text-sm"><div className="flex items-start justify-between gap-2"><p className="font-medium truncate">{item.title}</p></div><p className="mt-0.5 text-[11px] text-muted-foreground">{localizedEnumValue(t, "enums.riskStatus", item.status)}</p></div>)}</div> : <p className="p-3 text-sm text-muted-foreground">{t("projects.noRisksYet")}</p>}
                </CardContent>
              </Card>
              {canViewIssues ? <Card>
                <CardHeader className="flex items-center justify-between gap-2">
                  <span className="flex items-center gap-1.5"><FileText className="h-4 w-4 text-primary" aria-hidden="true" /><h3 className="text-sm font-semibold" aria-hidden="true">{t("projects.topIssues")}</h3></span>
                  <span className="text-xs font-semibold tabular-nums text-foreground">{issues.data?.length ?? 0}</span>
                </CardHeader>
                <CardContent className="p-0">
                  {issues.isLoading ? <div className="p-3"><Skeleton className="h-8 w-full" /><Skeleton className="mt-2 h-4 w-2/3" /></div> : issues.isError ? <p className="p-3 text-sm text-destructive">{t("projects.risksIssuesLoadFailed")}</p> : issues.data?.length ? <div className="space-y-1">{issues.data.map((item) => <div key={item.id} className="border-b border-border last:border-b-0 py-2 text-sm"><div className="flex items-start justify-between gap-2"><p className="font-medium truncate">{item.title}</p></div><p className="mt-0.5 text-[11px] text-muted-foreground">{workItemStatusLabel(item.status, workItemStatusesQuery.data ?? [], locale)}</p></div>)}</div> : <p className="p-3 text-sm text-muted-foreground">{t("projects.noIssuesYet")}</p>}
                </CardContent>
              </Card> : null}
            </div>
          </section>

          <section className="space-y-4 rounded-xl border p-4">
            <div className="flex items-center gap-2 border-b pb-3"><AlertTriangle className="h-4 w-4 text-primary" aria-hidden="true" /><h3 className="text-sm font-semibold">{t("projects.healthDimensionsSection")}</h3></div>
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              <HealthField name="costHealth" label={t("projects.costHealth")} value={editingReport?.costHealth} healthLabel={healthLabel} />
              <HealthField name="qualityHealth" label={t("projects.qualityHealth")} value={editingReport?.qualityHealth} healthLabel={healthLabel} />
              <div className="min-w-0 rounded-md border border-border px-3 py-2">
                <p className="text-sm font-medium text-foreground">{t("projects.resourceUtilization")}</p>
                <p className={`mt-1 text-sm font-semibold ${resourceUtilizationTextClass(currentProgress.isError ? "Unavailable" : currentProgress.data?.resourceUtilizationStatus)}`} aria-live={currentProgress.isFetching ? "polite" : undefined}>
                  {currentProgress.isFetching
                    ? t("projects.statusReportUtilizationLoading")
                    : t(`projects.resourceUtilizationStates.${currentProgress.isError ? "Unavailable" : currentProgress.data?.resourceUtilizationStatus || "Unavailable"}`)}
                </p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {currentProgress.isError || currentProgress.data?.resourceUtilizationPercent == null
                    ? t("projects.statusReportUtilizationNoPercentage")
                    : t("projects.statusReportUtilizationPercent", { percent: formatPercent1(currentProgress.data.resourceUtilizationPercent, locale) })}
                </p>
              </div>
            </div>
          </section>

          <section className="space-y-4 rounded-xl border p-4">
            <div className="flex items-center gap-2 border-b pb-3"><FileText className="h-4 w-4 text-primary" aria-hidden="true" /><h3 className="text-sm font-semibold">{t("projects.narrativesSection")}</h3></div>
            <div className="grid gap-4 sm:grid-cols-2"><NarrativeField name="executiveSummary" label={t("projects.executiveSummary")} defaultValue={editingReport?.executiveSummary} /><NarrativeField name="keyAchievements" label={t("projects.keyAchievements")} defaultValue={editingReport?.keyAchievements} /><NarrativeField name="challengesConcerns" label={t("projects.challengesConcerns")} defaultValue={editingReport?.challengesConcerns} /><NarrativeField name="nextPeriodPriorities" label={t("projects.nextPeriodPriorities")} defaultValue={editingReport?.nextPeriodPriorities} /></div>
          </section>

          <section className="space-y-4 rounded-xl border p-4">
            <div className="flex items-center gap-2 border-b pb-3"><ListChecks className="h-4 w-4 text-primary" aria-hidden="true" /><h3 className="text-sm font-semibold">{t("projects.actionsNeededSection")}</h3></div>
            <NarrativeField name="actionsNeeded" label={t("projects.actionsNeeded")} defaultValue={editingReport?.actionsNeeded} />
          </section>

          <section className="space-y-4 rounded-xl border p-4">
            <div className="flex items-center gap-2 border-b pb-3"><Headphones className="h-4 w-4 text-primary" aria-hidden="true" /><h3 className="text-sm font-semibold">{t("projects.supportNeededSection")}</h3></div>
            <NarrativeField name="supportNeeded" label={t("projects.supportNeeded")} defaultValue={editingReport?.supportNeeded} />
          </section>

          {actionError ? <p role="alert" className="flex items-start gap-2 rounded-lg border border-destructive/40 px-3 py-2 text-sm text-destructive"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />{actionError}</p> : null}
        </SideSheetBody>
        <SideSheetFooter><Button type="button" variant="outline" onClick={() => { setFormOpen(false); setEditingReport(null); }}>{t("common.cancel")}</Button><Button type="submit" disabled={create.isPending || update.isPending}>{editingReport ? t("projects.saveChanges") : t("projects.saveDraft")}</Button></SideSheetFooter>
      </form>
    </SideSheet>    <StatusReportDetailsDialog
      open={Boolean(detailId)}
      onOpenChange={(next) => { if (!next) { setDetailId(""); setActionError(null); } }}
      report={detail.data}
      loading={detail.isLoading}
      error={detail.isError}
      actionError={actionError}
      canEdit={canEdit}
      canPublish={canPublish}
      canExport={canExport}
      exporting={exporting}
      workflowPending={workflowPending}
      isDeleting={remove.isPending}
      onEdit={() => { if (detail.data) { setEditingReport(detail.data); progressTouched.current = detail.data.overallProgressPercent != null; setProgressDraft(detail.data.overallProgressPercent == null ? (project.progress == null ? "" : String(project.progress)) : String(detail.data.overallProgressPercent)); setPeriodFrom(detail.data.periodFrom.slice(0, 10)); setPeriodTo(detail.data.periodTo.slice(0, 10)); setFormSession((session) => session + 1); setDetailId(""); setActionError(null); setFormOpen(true); } }}
      onPublish={() => { if (detail.data) void publishReport(detail.data); }}
      onDelete={() => { if (detail.data) void confirmDelete(detail.data.id); }}
      onExport={() => void exportReport()}
      onExportExcel={() => void exportExcelReport()}
      onPrint={() => void printReport()}
      t={t}
      locale={locale}
      statusLabel={statusLabel}
      healthLabel={healthLabel}
      issueStatusLabel={(status) => workItemStatusLabel(status, workItemStatusesQuery.data ?? [], locale)}
      formatDate={formatDate}
      formatDateTime={(value) => formatDateTime(value, locale)}
      formatCurrency={formatCurrency}

    />
  </div>;
}

function StatusReportComparisonDialog({ open, onOpenChange, comparison, loading, error, earlierCurrency, laterCurrency, locale, t, formatDate, formatCurrency }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  comparison?: ProjectStatusReportComparisonDto;
  loading: boolean;
  error: boolean;
  earlierCurrency?: string;
  laterCurrency?: string;
  locale: string;
  t: (key: string, values?: Record<string, string | number>) => string;
  formatDate: (value: string | Date) => string;
  formatCurrency: (value: number, currency?: string) => string;
}) {
  const metricRows = [
    ["OverallProgressPercent", "projects.overallProgress"],
    ["SchedulePerformanceIndex", "projects.statusReportComparison.metrics.spi"],
    ["CostPerformanceIndex", "projects.statusReportComparison.metrics.cpi"],
    ["ScheduleVariance", "projects.statusReportComparison.metrics.scheduleVariance"],
    ["CostVariance", "projects.statusReportComparison.metrics.costVariance"],
  ] as const;
  const formatMetric = (code: string, value?: number | null, currency?: string) => {
    if (value == null || !Number.isFinite(value)) return "—";
    if (code === "OverallProgressPercent") return formatPercent1(value, locale);
    if (code === "SchedulePerformanceIndex" || code === "CostPerformanceIndex")
      return formatNumberValue(value, locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    return currency ? formatCurrency(value, currency) : "—";
  };
  const isCurrencyMetric = (code: string) => code === "ScheduleVariance" || code === "CostVariance";
  const formatMilestoneVariance = (value?: number | null) => value == null ? "—" : `${value} ${t("projects.days")}`;

  return <Dialog open={open} onOpenChange={onOpenChange} ariaLabel={t("projects.statusReportComparison.title")} contentClassName="max-h-[calc(100vh-2rem)] max-w-5xl overflow-y-auto sm:max-w-5xl">
    <DialogContent className="space-y-5">
      <DialogHeader>
        <DialogTitle>{t("projects.statusReportComparison.title")}</DialogTitle>
        <DialogDescription>{comparison ? `${formatDate(comparison.earlierStatusDate)} → ${formatDate(comparison.laterStatusDate)}` : t("projects.statusReportComparison.description")}</DialogDescription>
      </DialogHeader>
      {loading ? <Loading t={t} /> : error || !comparison ? <p role="alert" className="py-6 text-sm text-destructive">{t("projects.statusReportComparison.loadFailed")}</p> : <div className="space-y-5">
        {comparison.reportingConfigurationChanged || comparison.includedPlansChanged || comparison.reportingBaselinesChanged ? <div role="status" className="space-y-1 rounded-md border border-warning/40 bg-warning/10 p-3 text-sm text-warning">
          <p className="font-medium">{t("projects.statusReportComparison.scopeWarning")}</p>
          {comparison.reportingConfigurationChanged ? <p>{t("projects.statusReportComparison.configurationChanged")}</p> : null}
          {comparison.includedPlansChanged ? <p>{t("projects.statusReportComparison.plansChanged")}</p> : null}
          {comparison.reportingBaselinesChanged ? <p>{t("projects.statusReportComparison.baselinesChanged")}</p> : null}
        </div> : null}
        <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("projects.statusReportComparison.metric")}</TableHead><TableHead>{t("projects.statusReportComparison.earlier")}</TableHead><TableHead>{t("projects.statusReportComparison.later")}</TableHead><TableHead>{t("projects.statusReportComparison.change")}</TableHead></TableRow></TableHeader><TableBody>
          {metricRows.map(([code, label]) => { const row = comparison.metrics.find((item) => item.code === code); const sameCurrency = Boolean(earlierCurrency && laterCurrency && earlierCurrency === laterCurrency); return <TableRow key={code}><TableCell>{t(label)}</TableCell><TableCell className="tabular-nums">{formatMetric(code, row?.earlierValue, earlierCurrency)}</TableCell><TableCell className="tabular-nums">{formatMetric(code, row?.laterValue, laterCurrency)}</TableCell><TableCell className="tabular-nums">{isCurrencyMetric(code) && !sameCurrency ? "—" : formatMetric(code, row?.change, earlierCurrency)}</TableCell></TableRow>; })}
        </TableBody></Table></div>
        <div className="grid gap-3 md:grid-cols-2">
          {([["riskTrend", comparison.riskTrend], ["issueTrend", comparison.issueTrend]] as const).map(([key, trend]) => <Card key={key}><CardHeader><CardTitle className="text-sm">{t(`projects.statusReportComparison.${key}`)}</CardTitle></CardHeader><CardContent className="grid grid-cols-3 gap-2 text-sm"><ReportDetail label={t("projects.statusReportComparison.totalEarlier")} value={String(trend.earlierTotal)} /><ReportDetail label={t("projects.statusReportComparison.totalLater")} value={String(trend.laterTotal)} /><ReportDetail label={t("projects.statusReportComparison.openEarlier")} value={String(trend.earlierOpen)} /><ReportDetail label={t("projects.statusReportComparison.openLater")} value={String(trend.laterOpen)} /><ReportDetail label={t("projects.statusReportComparison.highCriticalEarlier")} value={String(trend.earlierHighOrCriticalOpen)} /><ReportDetail label={t("projects.statusReportComparison.highCriticalLater")} value={String(trend.laterHighOrCriticalOpen)} /></CardContent></Card>)}
        </div>
        <p className="text-xs text-muted-foreground">{t("projects.statusReportComparison.capturedItemsNote")}</p>
        <section className="space-y-2"><h3 className="text-sm font-semibold">{t("projects.statusReportComparison.milestoneChanges")}</h3>{comparison.milestoneChanges.length ? <div className="overflow-x-auto"><Table><TableHeader><TableRow><TableHead>{t("projects.plan")}</TableHead><TableHead>{t("projects.reporting.sections.milestones")}</TableHead><TableHead>{t("projects.statusReportComparison.earlierStatus")}</TableHead><TableHead>{t("projects.statusReportComparison.laterStatus")}</TableHead><TableHead>{t("projects.statusReportComparison.earlierPlannedDate")}</TableHead><TableHead>{t("projects.statusReportComparison.laterPlannedDate")}</TableHead><TableHead>{t("projects.statusReportComparison.earlierVariance")}</TableHead><TableHead>{t("projects.statusReportComparison.laterVariance")}</TableHead></TableRow></TableHeader><TableBody>{comparison.milestoneChanges.map((item, index) => <TableRow key={`${item.planId}-${item.name}-${index}`}><TableCell>{item.planName}</TableCell><TableCell>{item.name}</TableCell><TableCell>{item.earlierStatus || "—"}</TableCell><TableCell>{item.laterStatus || "—"}</TableCell><TableCell>{item.earlierPlannedDate ? formatDate(item.earlierPlannedDate) : "—"}</TableCell><TableCell>{item.laterPlannedDate ? formatDate(item.laterPlannedDate) : "—"}</TableCell><TableCell>{formatMilestoneVariance(item.earlierVarianceDays)}</TableCell><TableCell>{formatMilestoneVariance(item.laterVarianceDays)}</TableCell></TableRow>)}</TableBody></Table></div> : <p className="text-sm text-muted-foreground">{t("projects.statusReportComparison.noMilestoneChanges")}</p>}</section>
      </div>}
      <DialogFooter><Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.close")}</Button></DialogFooter>
    </DialogContent>
  </Dialog>;
}

function StatusReportDetailsDialog({ open, onOpenChange, report, loading, error, actionError, canEdit, canPublish, canExport, exporting, workflowPending, isDeleting, onEdit, onPublish, onDelete, onExport, onExportExcel, onPrint, t, locale, statusLabel, healthLabel, issueStatusLabel, formatDate, formatDateTime, formatCurrency }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  report?: ProjectStatusReportDto;
  loading: boolean;
  error: boolean;
  actionError: string | null;
  canEdit: boolean;
  canPublish: boolean;
  canExport: boolean;
  exporting: boolean;
  workflowPending: boolean;
  isDeleting: boolean;
  onEdit: () => void;
  onPublish: () => void;
  onDelete: () => void;
  onExport: () => void;
  onExportExcel: () => void;
  onPrint: () => void;
  t: (key: string) => string;
  locale: string;
  statusLabel: (value?: string | null) => string;
  healthLabel: (value?: string | null) => string;
  issueStatusLabel: (value: string) => string;
  formatDate: (value: string | Date, options?: Intl.DateTimeFormatOptions) => string;
  formatDateTime: (value: string | Date) => string;
  formatCurrency: (value: number, currency?: string) => string;

}) {
  const isDraft = report?.reportState === "Draft";
  const isPublished = report?.reportState === "Published";
  const health = (value?: string) => <ReportStatus status={value} label={healthLabel(value)} />;
  const resourceStatus = report?.resourceHealth;
  const legacyResourceHealth = resourceStatus === "Green" || resourceStatus === "Amber" || resourceStatus === "Red";
  const resourceUtilizationValue = resourceStatus
    ? legacyResourceHealth
      ? health(resourceStatus)
      : <ReportStatus
          status={resourceStatus}
          label={t(`projects.resourceUtilizationStates.${resourceStatus}`)}
          textClass={resourceUtilizationTextClass(resourceStatus)}
          dotClass={resourceStatus === "Overallocated" ? "bg-destructive" : resourceStatus === "WithinCapacity" ? "bg-success" : resourceStatus === "Unavailable" ? "bg-warning" : "bg-muted-foreground"}
        />
    : "—";
  const dimensions = [
    [t("projects.costHealth"), report?.costHealth ? health(report.costHealth) : "—"],
    [t("projects.qualityHealth"), report?.qualityHealth ? health(report.qualityHealth) : "—"],
    [t("projects.resourceUtilization"), resourceUtilizationValue],
  ] as const;
  const includedSections = report?.includedSectionsSnapshot?.length
    ? report.includedSectionsSnapshot
    : report?.performanceSnapshot?.includedReportSections ?? [];
  const hasSection = (section: string) => includedSections.length === 0 || includedSections.includes(section);
  const hasFinancialSummary = Boolean(report?.currencyCodeSnapshot || report?.budgetAmountSnapshot != null);
  const actualPaidValue = report?.actualPaidSnapshot;
  const outstandingPaymentsValue = report?.outstandingPlannedPaymentsSnapshot;
  const actualPaidDataQuality = report?.actualPaidDataQualitySnapshot
    ?? (actualPaidValue == null ? "NotRecorded" : "Available");
  const plannedPaymentsDataQuality = report?.plannedPaymentsDataQualitySnapshot
    ?? (outstandingPaymentsValue == null ? "NotRecorded" : "Available");
  const chartActualPaid = typeof actualPaidValue === "number" && Number.isFinite(actualPaidValue) && actualPaidValue > 0 ? actualPaidValue : 0;
  const chartOutstanding = typeof outstandingPaymentsValue === "number" && Number.isFinite(outstandingPaymentsValue) && outstandingPaymentsValue > 0 ? outstandingPaymentsValue : 0;
  const chartScale = Math.max(chartActualPaid, chartOutstanding);
  const scaledActualPaid = chartScale > 0 ? chartActualPaid / chartScale : 0;
  const scaledOutstanding = chartScale > 0 ? chartOutstanding / chartScale : 0;
  const chartActualPaidShare = scaledActualPaid + scaledOutstanding > 0 ? scaledActualPaid / (scaledActualPaid + scaledOutstanding) * 100 : 0;
  const financeDonutBackground = chartScale > 0
    ? `conic-gradient(from -90deg, hsl(var(--chart-1)) 0 ${chartActualPaidShare}%, hsl(var(--chart-2)) ${chartActualPaidShare}% 100%)`
    : "hsl(var(--muted))";
  return <Dialog open={open} onOpenChange={onOpenChange} ariaLabel={t("projects.statusReportDetails")} contentClassName="max-h-[calc(100vh-1rem)] w-[calc(100vw-1rem)] max-w-6xl overflow-y-auto p-4 sm:max-w-6xl sm:p-6">
    <DialogContent className="space-y-6">
      <DialogHeader className="border-b border-border pb-4 text-start sm:!text-start">
        <p className="text-xs font-semibold tracking-wide text-muted-foreground">{t("projects.statusReportDetails")}</p>
        <DialogTitle className="mt-1 flex flex-wrap items-baseline gap-x-2 gap-y-1 text-2xl leading-tight">
          <span>{report?.projectNameSnapshot || t("projects.statusReportDetails")}</span>
          {report?.projectCodeSnapshot ? <span className="text-sm font-medium text-muted-foreground">({report.projectCodeSnapshot})</span> : null}
        </DialogTitle>
        <DialogDescription className="text-start">{report ? <ReportStatus status={report.reportState} label={t(`projects.statusReportStates.${report.reportState}`)} /> : t("projects.statusReportDetails")}</DialogDescription>
      </DialogHeader>
      {loading ? <Loading t={t} /> : error || !report ? <p role="alert" className="py-8 text-sm text-destructive">{t("projects.statusReportLoadFailed")}</p> : <div className="divide-y divide-border">
        <section className="space-y-3 py-5 first:pt-0">
          <h3 className="text-base font-semibold text-foreground">{t("projects.statusReportView.sections.projectInformation")}</h3>
          <dl className="grid grid-cols-1 gap-x-6 sm:grid-cols-2 lg:grid-cols-4">
            <ReportMetaField label={t("projects.reportDate")} value={formatDate(report.reportDate)} />
            <ReportMetaField label={t("projects.preparedBy")} value={report.preparedByNameSnapshot} />
            <ReportMetaField label={t("projects.sourceDataCapturedAt")} value={report.sourceDataCapturedAt ? formatDateTime(report.sourceDataCapturedAt) : "—"} />
            <ReportMetaField label={t("projects.publishedAt")} value={report.publishedAt ? formatDateTime(report.publishedAt) : "—"} />
          </dl>
        </section>
        <section className="space-y-3 py-5">
          <h3 className="text-base font-semibold text-foreground">{t("projects.statusReportView.sections.reportingPeriodStatus")}</h3>
          <dl className="grid grid-cols-1 gap-x-6 sm:grid-cols-2 lg:grid-cols-3">
            <ReportMetaField label={t("projects.reportingPeriod")} value={`${formatDate(report.periodFrom)} – ${formatDate(report.periodTo)}`} />
            <ReportMetaField label={t("projects.statusDate")} value={formatDate(report.statusDate || report.reportDate)} />
            <ReportMetaField label={t("projects.overallHealth")} value={health(report.overallHealth)} />
            <ReportMetaField label={t("projects.overallProgress")} value={formatPercent1(report.overallProgressPercent, locale)} />
            <ReportMetaField label={t("projects.projectStatusAtReport")} value={<ReportStatus status={report.projectLifecycleStatusAtReport} label={statusLabel(report.projectLifecycleStatusAtReport)} />} />
            {dimensions.map(([label, value]) => <ReportMetaField key={label} label={label} value={value} />)}
          </dl>
        </section>
        <div dir="ltr" className={`grid grid-cols-1 gap-x-8 lg:items-start ${hasFinancialSummary ? "lg:grid-cols-[minmax(0,1fr)_18rem]" : ""}`}>
          <main dir={locale === "ar" ? "rtl" : "ltr"} className="min-w-0 divide-y divide-border">
            {hasSection("ExecutiveSummary") ? <section className="space-y-3 py-5 first:pt-0"><ReportNarrative headingLevel={3} label={t("projects.executiveSummary")} value={report.executiveSummary} /></section> : null}
            {hasSection("PerformanceKPIs") ? <section className="space-y-3 py-5">
              <h3 className="text-base font-semibold text-foreground">{t("projects.statusReportView.sections.progressPerformance")}</h3>
              <StatusReportPerformanceSnapshot report={report} locale={locale} t={t} formatDate={formatDate} formatCurrency={formatCurrency} />
            </section> : null}
            {hasSection("ExecutiveSummary") ? <section className="space-y-3 py-5"><ReportNarrative headingLevel={3} label={t("projects.keyAchievements")} value={report.keyAchievements} /></section> : null}
            {report.performanceSnapshot && hasSection("Milestones") ? <div className="py-5"><StatusReportMilestones report={report} t={t} formatDate={formatDate} /></div> : null}
          </main>
          {hasFinancialSummary ? <aside dir={locale === "ar" ? "rtl" : "ltr"} className="min-w-0 border-t border-border py-5 lg:border-l lg:border-t-0 lg:pl-6">
            <section className="space-y-3">
              <h3 className="text-base font-semibold text-foreground">{t("projects.statusReportView.sections.financialSummary")}</h3>
              {actualPaidDataQuality === "CurrencyUnresolved"
                || actualPaidDataQuality === "CurrencyMismatch"
                || actualPaidDataQuality === "ProjectCurrencyUnavailable"
                ? <p role="status" className="text-xs leading-5 text-warning">{t("projects.statusReportActualPaidCurrencyUnavailable")}</p>
                : actualPaidDataQuality === "NotRecorded"
                  ? <p role="status" className="text-xs leading-5 text-muted-foreground">{t("projects.statusReportActualPaidNotRecorded")}</p>
                  : null}
              {plannedPaymentsDataQuality === "CurrencyUnresolved"
                || plannedPaymentsDataQuality === "CurrencyMismatch"
                || plannedPaymentsDataQuality === "ProjectCurrencyUnavailable"
                || plannedPaymentsDataQuality === "IntegrityIssue"
                ? <p role="status" className="text-xs leading-5 text-warning">{t("projects.statusReportPlannedPaymentsUnavailable")}</p>
                : plannedPaymentsDataQuality === "NotRecorded"
                  ? <p role="status" className="text-xs leading-5 text-muted-foreground">{t("projects.statusReportPlannedPaymentsNotRecorded")}</p>
                  : null}
              <figure className="space-y-3">
                <div
                  role="img"
                  aria-label={`${t("projects.financialSummary")}. ${t("projects.approvedBudget")}: ${report.budgetAmountSnapshot == null ? "—" : formatCurrency(report.budgetAmountSnapshot, report.currencyCodeSnapshot)}. ${t("projects.actualPaid")}: ${actualPaidValue == null ? "—" : formatCurrency(actualPaidValue, report.currencyCodeSnapshot)}. ${t("projects.outstandingPlannedPayments")}: ${outstandingPaymentsValue == null ? "—" : formatCurrency(outstandingPaymentsValue, report.currencyCodeSnapshot)}`}
                  className="relative mx-auto h-40 w-40 rounded-full"
                  style={{ background: financeDonutBackground }}
                >
                  <div aria-hidden="true" className="absolute inset-5 flex flex-col items-center justify-center rounded-full border border-border bg-background px-2 text-center">
                    <span className="text-[10px] leading-4 text-muted-foreground">{t("projects.approvedBudget")}</span>
                    <span className="max-w-full break-words text-xs font-semibold leading-4 tabular-nums">{report.budgetAmountSnapshot == null ? "—" : formatCurrency(report.budgetAmountSnapshot, report.currencyCodeSnapshot)}</span>
                  </div>
                </div>
                <figcaption className="space-y-2">
                  <div className="flex items-baseline justify-between gap-3 text-xs">
                    <span className="flex min-w-0 items-center gap-2"><span aria-hidden="true" className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: "hsl(var(--chart-1))" }} /><span className="text-muted-foreground">{t("projects.actualPaid")}</span></span>
                    <span className="break-words text-end font-medium tabular-nums">{actualPaidValue == null ? "—" : formatCurrency(actualPaidValue, report.currencyCodeSnapshot)}</span>
                  </div>
                  <div className="flex items-baseline justify-between gap-3 text-xs">
                    <span className="flex min-w-0 items-center gap-2"><span aria-hidden="true" className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: "hsl(var(--chart-2))" }} /><span className="text-muted-foreground">{t("projects.outstandingPlannedPayments")}</span></span>
                    <span className="break-words text-end font-medium tabular-nums">{outstandingPaymentsValue == null ? "—" : formatCurrency(outstandingPaymentsValue, report.currencyCodeSnapshot)}</span>
                  </div>
                  <p className="text-xs leading-5 text-muted-foreground">{t("projects.financialDonutNote")}</p>
                </figcaption>
              </figure>
              <dl>
                <ReportMetaField label={t("projects.currencyCode")} value={report.currencyCodeSnapshot || "—"} />
                <ReportMetaField label={t("projects.costVariance")} value={<span className="break-words tabular-nums">{report.costVarianceAmountSnapshot == null ? "—" : formatCurrency(report.costVarianceAmountSnapshot, report.currencyCodeSnapshot)}</span>} />
              </dl>
            </section>
          </aside> : null}
          {hasSection("RisksIssues") ? <div dir={locale === "ar" ? "rtl" : "ltr"} className={`min-w-0 border-t border-border py-5 ${hasFinancialSummary ? "lg:col-span-2" : ""}`}><StatusReportSnapshotDetails report={report} includeRisksIssues t={t} issueStatusLabel={issueStatusLabel} /></div> : null}
          <section dir={locale === "ar" ? "rtl" : "ltr"} className={`space-y-3 border-t border-border py-5 ${hasFinancialSummary ? "lg:col-span-2" : ""}`}>
            <h3 className="text-base font-semibold text-foreground">{t("projects.statusReportView.sections.challengesDependencies")}</h3>
            {hasSection("ExecutiveSummary") ? <ReportNarrative label={t("projects.challengesConcerns")} value={report.challengesConcerns} /> : null}
            <ReportNarrative label={t("projects.supportNeeded")} value={report.supportNeeded} />
          </section>
          <section dir={locale === "ar" ? "rtl" : "ltr"} className={`space-y-3 border-t border-border py-5 ${hasFinancialSummary ? "lg:col-span-2" : ""}`}>
            <h3 className="text-base font-semibold text-foreground">{t("projects.statusReportView.sections.nextStepsActions")}</h3>
            {hasSection("ExecutiveSummary") ? <ReportNarrative label={t("projects.nextPeriodPriorities")} value={report.nextPeriodPriorities} /> : null}
            <ReportNarrative label={t("projects.actionsNeeded")} value={report.actionsNeeded} />
          </section>
          {hasSection("DecisionsRequired") ? <div dir={locale === "ar" ? "rtl" : "ltr"} className={`border-t border-border py-5 ${hasFinancialSummary ? "lg:col-span-2" : ""}`}><StatusReportDecisions report={report} t={t} formatDate={formatDate} /></div> : null}
        </div>
      </div>}
      {actionError ? <p role="alert" className="text-sm text-destructive">{actionError}</p> : null}
      {report && !isDraft && !isPublished ? <p className="border-s-2 border-muted-foreground/40 py-1 ps-3 text-sm text-muted-foreground">{t("projects.legacyStatusReportReadOnly")}</p> : null}
      {report && ((isDraft && (canEdit || canPublish)) || (isPublished && canExport)) ? <DialogFooter className="gap-2 border-t border-border pt-4">
        {isPublished && canExport ? <><Button type="button" variant="outline" onClick={onPrint} disabled={exporting}><Printer className="me-1.5 h-3.5 w-3.5" />{t("projects.printReport")}</Button><Button type="button" variant="outline" onClick={onExport} disabled={exporting}><Download className="me-1.5 h-3.5 w-3.5" />{exporting ? t("projects.generatingPdf") : t("projects.exportPdf")}</Button><Button type="button" variant="outline" onClick={onExportExcel} disabled={exporting}><FileSpreadsheet className="me-1.5 h-3.5 w-3.5" />{exporting ? t("projects.generatingExcel") : t("projects.exportExcel")}</Button></> : null}
        {canEdit && isDraft ? <><Button type="button" variant="outline" onClick={onDelete} disabled={isDeleting}><Trash2 className="me-1.5 h-3.5 w-3.5" />{t("projects.delete")}</Button><Button type="button" variant="outline" onClick={onEdit}><Pencil className="me-1.5 h-3.5 w-3.5" />{t("common.edit")}</Button></> : null}
        {isDraft && canPublish ? <Button type="button" onClick={onPublish} disabled={workflowPending}>{workflowPending ? <Loader2 className="me-1.5 h-3.5 w-3.5 animate-spin" /> : <Send className="me-1.5 h-3.5 w-3.5" />}{t("projects.reportActions.publish")}</Button> : null}
      </DialogFooter> : null}
    </DialogContent>
  </Dialog>;
}

function HealthField({ name, label, value, disabled, healthLabel }: { name: string; label: string; value?: string; disabled?: boolean; healthLabel: (value?: string | null) => string }) {
  return <div><Label htmlFor={name}>{label}</Label><select id={name} className="input-field mt-1 w-full" name={name} defaultValue={value || ""} disabled={disabled}><option value="">—</option>{["Green", "Amber", "Red"].map((h) => <option key={h} value={h}>{healthLabel(h)}</option>)}</select></div>;
}

function NarrativeField({ name, label, defaultValue, readOnly }: { name: string; label: string; defaultValue?: string; readOnly?: boolean }) {
  return <div><Label htmlFor={name}>{label}</Label><Textarea id={name} className="mt-1" name={name} rows={3} defaultValue={defaultValue} readOnly={readOnly} /> </div>;
}

function StatusReportPerformanceSnapshot({ report, locale, t, formatDate, formatCurrency }: { report: ProjectStatusReportDto; locale: string; t: (key: string, values?: Record<string, string | number>) => string; formatDate: (value: string | Date) => string; formatCurrency: (value: number, currency?: string) => string }) {
  const performance = report.performanceSnapshot;
  const currency = performance?.currency || report.currencyCodeSnapshot;
  const money = (value?: number | null) => typeof value === "number" && Number.isFinite(value) ? formatCurrency(value, currency) : "—";
  return <section className="space-y-3">
      <div className="space-y-1">
        <h4 className="text-sm font-semibold">{t("projects.reporting.sections.performanceKpis")}</h4>
        <p className="text-sm text-muted-foreground">{performance ? <>{t("projects.performanceSnapshotDescription", { date: formatDate(performance.statusDate) })} · {t(`projects.reporting.quality.${performance.dataQuality.toLowerCase()}`)}</> : t("projects.statusReportComparison.kpisUnavailable")}</p>
      </div>
      <dl className="grid grid-cols-1 gap-x-6 sm:grid-cols-2 lg:grid-cols-4">
        <ReportMetric label={t("projects.budget")} value={money(report.budgetAmountSnapshot)} />
        <ReportMetric label={t("projects.actualCost")} value={money(report.actualCostAmountSnapshot)} />
        <ReportMetric label={t("projects.committedCost")} value={money(report.committedCostAmountSnapshot)} />
        <ReportMetric label={t("projects.forecast")} value={money(report.forecastCostAmountSnapshot)} />
        <ReportMetric label={t("projects.costVariance")} value={money(report.costVarianceAmountSnapshot)} />
        {performance ? <>
          <ReportMetric label="BAC" value={money(performance.budgetAtCompletion)} />
          <ReportMetric label="PV" value={money(performance.plannedValue)} />
          <ReportMetric label="EV" value={money(performance.earnedValue)} />
          <ReportMetric label="AC" value={money(performance.actualCost)} />
          <ReportMetric label="SPI" value={performance.schedulePerformanceIndex == null ? undefined : formatNumberValue(performance.schedulePerformanceIndex, locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} />
          <ReportMetric label="CPI" value={performance.costPerformanceIndex == null ? undefined : formatNumberValue(performance.costPerformanceIndex, locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} />
          <ReportMetric label="SV" value={money(performance.scheduleVariance)} />
          <ReportMetric label="CV" value={money(performance.costVariance)} />
          <ReportMetric label={t("projects.unallocatedProjectActualCost")} value={money(performance.unallocatedProjectActualCost)} />
        </> : null}
        <ReportMetric label={t("projects.reportingConfigurationVersion")} value={report.reportingConfigurationVersionSnapshot ?? undefined} />
      </dl>
      {performance?.dataQualityReasons.length ? <p className="text-sm text-muted-foreground">{performance.dataQualityReasons.map((reason) => localizedEvmReasonLabel(reason, t)).join(" · ")}</p> : null}
  </section>;
}

function StatusReportMilestones({ report, t, formatDate }: { report: ProjectStatusReportDto; t: (key: string) => string; formatDate: (value: string | Date) => string }) {
  const milestones = report.performanceSnapshot?.milestones ?? [];
  return <section className="space-y-3"><h3 className="text-base font-semibold text-foreground">{t("projects.reporting.sections.milestones")}</h3>{milestones.length ? <Table className="min-w-[64rem] table-fixed"><TableHeader><TableRow><TableHead className="w-[19%] text-start">{t("projects.plan")}</TableHead><TableHead className="w-[19%] text-start">{t("projects.statusReportComparison.milestone")}</TableHead><TableHead className="text-start">{t("projects.statusReportComparison.baselineDate")}</TableHead><TableHead className="text-start">{t("projects.statusReportComparison.plannedDate")}</TableHead><TableHead className="text-start">{t("projects.statusReportComparison.actualDate")}</TableHead><TableHead className="text-end">{t("projects.statusReportComparison.variance")}</TableHead><TableHead className="text-start">{t("projects.status")}</TableHead></TableRow></TableHeader><TableBody>{milestones.map((milestone, index) => <TableRow key={`${milestone.planId}-${milestone.name}-${index}`}><TableCell className="break-words text-start">{milestone.planName}</TableCell><TableCell className="break-words text-start">{milestone.name}</TableCell><TableCell className="break-words text-start">{milestone.baselineDate ? formatDate(milestone.baselineDate) : "—"}</TableCell><TableCell className="break-words text-start">{milestone.plannedDate ? formatDate(milestone.plannedDate) : "—"}</TableCell><TableCell className="break-words text-start">{milestone.actualCompletionDate ? formatDate(milestone.actualCompletionDate) : "—"}</TableCell><TableCell className="break-words text-end tabular-nums">{milestone.varianceDays == null ? "—" : `${milestone.varianceDays} ${t("projects.days")}`}</TableCell><TableCell className="break-words text-start">{t(`projects.statusReportComparison.milestoneStatuses.${milestone.status}`)}</TableCell></TableRow>)}</TableBody></Table> : <p className="text-sm text-muted-foreground">{t("projects.noSnapshotItems")}</p>}</section>;
}

function StatusReportSnapshotDetails({ report, includeRisksIssues, t, issueStatusLabel }: { report: ProjectStatusReportDto; includeRisksIssues: boolean; t: (key: string, values?: Record<string, string | number>) => string; issueStatusLabel: (status: string) => string }) {
  if (!includeRisksIssues) return null;
  const severityLabel = (value: string) => {
    const key = `workItems.severity${value}`;
    const label = t(key);
    return label === key ? value : label;
  };
  return (
    <section className="space-y-3">
      <h3 className="text-base font-semibold text-foreground">{t("projects.reporting.sections.risksIssues")}</h3>
      <div className="grid gap-6 lg:grid-cols-2">
        <section className="min-w-0 space-y-2">
          <h4 className="text-sm font-semibold">{t("projects.topRisks")} <span className="font-normal text-muted-foreground">({report.risks.length})</span></h4>
          {report.risks.length ? (
            <Table className="min-w-[32rem] table-fixed">
              <TableHeader><TableRow><TableHead className="w-[38%] text-start">{t("projects.name")}</TableHead><TableHead className="text-start">{t("risks.riskLevel")}</TableHead><TableHead className="text-start">{t("common.status")}</TableHead><TableHead className="text-start">{t("projects.owner")}</TableHead></TableRow></TableHeader>
              <TableBody>{report.risks.map((item, index) => (
                <TableRow key={`${item.title}-${index}`}>
                  <TableCell className="break-words text-start">{item.title}</TableCell>
                  <TableCell className="break-words text-start"><ReportStatus status={item.riskLevel} label={localizedEnumValue(t, "enums.riskLevel", item.riskLevel)} /></TableCell>
                  <TableCell className="break-words text-start"><ReportStatus status={item.riskStatus} label={localizedEnumValue(t, "enums.riskStatus", item.riskStatus)} /></TableCell>
                  <TableCell className="break-words text-start">{item.ownerNameSnapshot || "—"}</TableCell>
                </TableRow>
              ))}</TableBody>
            </Table>
          ) : <p className="text-sm text-muted-foreground">{t("projects.noSnapshotItems")}</p>}
        </section>
        <section className="min-w-0 space-y-2">
          <h4 className="text-sm font-semibold">{t("projects.topIssues")} <span className="font-normal text-muted-foreground">({report.issues.length})</span></h4>
          {report.issues.length ? (
            <Table className="min-w-[32rem] table-fixed">
              <TableHeader><TableRow><TableHead className="w-[38%] text-start">{t("projects.name")}</TableHead><TableHead className="text-start">{t("workItems.severity")}</TableHead><TableHead className="text-start">{t("common.status")}</TableHead><TableHead className="text-start">{t("projects.assignee")}</TableHead></TableRow></TableHeader>
              <TableBody>{report.issues.map((item, index) => (
                <TableRow key={`${item.title}-${index}`}>
                  <TableCell className="break-words text-start">{item.title}</TableCell>
                  <TableCell className="break-words text-start"><ReportStatus status={item.severity} label={severityLabel(item.severity)} /></TableCell>
                  <TableCell className="break-words text-start"><ReportStatus status={item.issueStatus} label={issueStatusLabel(item.issueStatus)} /></TableCell>
                  <TableCell className="break-words text-start">{item.assigneeNameSnapshot || "—"}</TableCell>
                </TableRow>
              ))}</TableBody>
            </Table>
          ) : <p className="text-sm text-muted-foreground">{t("projects.noSnapshotItems")}</p>}
        </section>
      </div>
    </section>
  );
}

function StatusReportDecisions({ report, t, formatDate }: { report: ProjectStatusReportDto; t: (key: string) => string; formatDate: (value: string | Date) => string }) {
  const pending = (report.changeRequests ?? []).filter((item) => item.changeRequestStatus === "Pending");
  return (
    <section className="space-y-3">
      <h3 className="text-base font-semibold text-foreground">{t("projects.reporting.sections.decisionsRequired")}</h3>
      <p className="text-sm text-muted-foreground">{t("projects.statusReportComparison.decisionsSourceNote")}</p>
      {pending.length ? (
        <Table className="min-w-[52rem] table-fixed">
          <TableHeader><TableRow><TableHead className="w-[42%] text-start">{t("projects.changeRequest")}</TableHead><TableHead className="text-start">{t("common.status")}</TableHead><TableHead className="text-start">{t("projects.requestedBy")}</TableHead><TableHead className="text-start">{t("projects.requestedAt")}</TableHead></TableRow></TableHeader>
          <TableBody>{pending.map((item) => (
            <TableRow key={item.sourceChangeRequestId}>
              <TableCell className="break-words text-start">
                <p className="font-medium">#{item.number} · {item.reason}</p>
                {item.justification ? <p className="mt-1 whitespace-pre-wrap text-sm text-muted-foreground">{item.justification}</p> : null}
                {item.impact ? <p className="mt-1 text-sm"><span className="font-medium">{t("projects.statusReportComparison.impact")}:</span> {item.impact}</p> : null}
              </TableCell>
              <TableCell className="break-words text-start"><ReportStatus status={item.changeRequestStatus} label={t("projects.statusReportComparison.pendingDecision")} /></TableCell>
              <TableCell className="break-words text-start">{item.requestedByNameSnapshot || "—"}</TableCell>
              <TableCell className="break-words text-start tabular-nums">{formatDate(item.requestedAt)}</TableCell>
            </TableRow>
          ))}</TableBody>
        </Table>
      ) : <p className="text-sm text-muted-foreground">{t("projects.statusReportComparison.noDecisionsRequired")}</p>}
    </section>
  );
}

export function ProjectHandoverTab() {
  const { projectId, project } = useProjectContext();
  const { t, formatDate } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canAccessHandover = Boolean(user && (hasPermission(user.permissions, Permissions.Handover.View) || hasPermission(user.permissions, Permissions.Handover.Manage) || hasPermission(user.permissions, Permissions.Handover.AdminInitiate)));
  const query = useProjectHandoverRequests(projectId, canAccessHandover);
  const capabilities = useProjectHandoverCapabilities(projectId, canAccessHandover);
  const [newManagerId, setNewManagerId] = React.useState("");
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [handoverDateError, setHandoverDateError] = React.useState<string | null>(null);
  const [initiationType, setInitiationType] = React.useState<ProjectHandoverInitiationType>("ManagerInitiated");
  const [open, setOpen] = React.useState(false);
  const canChangeHandover = canMutate && !effectiveReadOnly;
  const selectedAllowed = initiationType === "ManagerInitiated" ? capabilities.data?.canInitiate : initiationType === "AdminInitiated" ? capabilities.data?.canAdminInitiate : capabilities.data?.canAssignInitial;
  const handoverManagers = useProjectHandoverManagers(projectId, open && canChangeHandover && Boolean(selectedAllowed));
  const create = useCreateProjectHandoverRequest(projectId);
  const cancel = useCancelProjectHandoverRequest(projectId);
  const requests = query.data ?? [];
  const pendingRequest = requests.find((item) => item.status === "Pending");
  const canCreateHandover = Boolean(canChangeHandover && !pendingRequest && (capabilities.data?.canInitiate || capabilities.data?.canAdminInitiate || capabilities.data?.canAssignInitial));
  const canCancelPending = Boolean(pendingRequest && capabilities.data?.canCancelOwnPending && canChangeHandover && pendingRequest.requestedBy === user?.id);

  React.useEffect(() => {
    if (capabilities.data?.canInitiate) setInitiationType("ManagerInitiated");
    else if (capabilities.data?.canAdminInitiate) setInitiationType("AdminInitiated");
    else if (capabilities.data?.canAssignInitial) setInitiationType("InitialAssignment");
  }, [capabilities.data]);

  React.useEffect(() => {
    if (open) { setNewManagerId(""); setActionError(null); setHandoverDateError(null); }
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
      await create.mutateAsync({ initiationType, newProjectManagerId: newManagerId || String(data.get("newProjectManagerId") || ""), effectiveHandoverDate: String(data.get("effectiveHandoverDate") || ""), reason, notes: String(data.get("notes") || "").trim() || undefined });
      setOpen(false);
    } catch (reason) {
      const fieldError = getApiFieldError(reason, "effectiveHandoverDate");
      setHandoverDateError(fieldError ?? null);
      setActionError(fieldError ? null : reason instanceof Error ? reason.message : t("projects.handoverFailed"));
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

  async function cancelPendingRequest() {
    if (!pendingRequest || !canCancelPending) return;
    try {
      await cancel.mutateAsync(pendingRequest.id);
      setActionError(null);
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : t("managerHandover.cancelFailed"));
    }
  }

  return <div className="space-y-4"><Card><CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><CardTitle className="flex items-center gap-2"><UserRoundCheck className="h-4 w-4" />{t("projects.handoverTitle")}</CardTitle><CardDescription>{t("projects.handoverDescription")}</CardDescription></div>{canCreateHandover ? <Button size="sm" onClick={() => setOpen(true)}><Plus className="me-1.5 h-4 w-4" />{t(capabilities.data?.canInitiate ? "managerHandover.request" : capabilities.data?.canAdminInitiate ? "managerHandover.adminRequest" : "managerHandover.initialAssignment")}</Button> : null}</CardHeader><CardContent className="p-0">{query.isLoading ? <Loading t={t} /> : query.isError ? <ErrorState t={t} onRetry={() => void query.refetch()} /> : requests.length === 0 ? <Empty>{t("projects.noHandoverRequests")}</Empty> : <div className="divide-y">{requests.map((item) => <div key={item.id} className="flex flex-col gap-3 px-4 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="min-w-0"><p className="text-sm font-medium">{item.currentProjectManagerName || t("managerHandover.notAssigned")} → {item.newProjectManagerName}</p>{item.initiationType !== "ManagerInitiated" ? <p className="text-xs text-muted-foreground">{t(item.initiationType === "InitialAssignment" ? "managerHandover.initialAssignment" : "managerHandover.adminRequest")}</p> : null}<p className="mt-1 text-xs text-muted-foreground">{t("projects.effectiveHandoverDate")}: {formatDate(item.effectiveHandoverDate)} · {t("projects.requestedBy")}: {item.requestedByName}</p>{item.reason ? <p className="mt-1 text-xs text-muted-foreground">{item.reason}</p> : null}{item.rejectionReason ? <p className="mt-1 text-xs text-destructive">{t("projects.rejectionReason")}: {item.rejectionReason}</p> : null}</div><div className="flex items-center gap-2"><Badge status={item.status} className="w-fit text-[10px]">{t(`projects.handoverStatus.${item.status}`)}</Badge>{canCancelPending && pendingRequest?.id === item.id ? <Button size="sm" variant="outline" onClick={() => void cancelPendingRequest()} disabled={cancel.isPending}>{t("managerHandover.cancel")}</Button> : null}</div></div>)}</div>}</CardContent></Card>
    <SideSheet open={open} onOpenChange={setOpen} title={t("projects.handoverTitle")} description={t("projects.handoverDescription")}>
      {pendingRequest ? <><SideSheetBody className="space-y-4"><p className="text-sm font-semibold">{t("projects.handoverPendingTitle")}</p><p className="text-sm text-muted-foreground">{t("projects.handoverPendingDescription")}</p><div className="rounded border px-3 py-2 text-sm"><p className="font-medium">{pendingRequest.currentProjectManagerName || t("managerHandover.notAssigned")} → {pendingRequest.newProjectManagerName}</p><p className="mt-1 text-xs text-muted-foreground">{formatDate(pendingRequest.effectiveHandoverDate)}</p>{pendingRequest.initiationType !== "ManagerInitiated" ? <p className="mt-1 text-xs text-muted-foreground">{t(pendingRequest.initiationType === "InitialAssignment" ? "managerHandover.initialAssignment" : "managerHandover.adminRequest")}</p> : null}</div>{canCancelPending ? <Button type="button" variant="outline" onClick={() => void cancelPendingRequest()} disabled={cancel.isPending}>{t("managerHandover.cancel")}</Button> : null}</SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("common.close")}</Button></SideSheetFooter></>
      : <form onSubmit={(event) => void submit(event)} className="flex min-h-0 flex-1 flex-col"><SideSheetBody className="space-y-4">
        {actionError ? <p className="rounded border border-destructive/20 bg-destructive/10 p-3 text-sm text-destructive">{actionError}</p> : null}
        <div className="space-y-2"><Label>{t("managerHandover.handover")}</Label><div className="flex flex-wrap gap-2">{([{ value: "ManagerInitiated", label: "managerHandover.request", allowed: capabilities.data?.canInitiate }, { value: "AdminInitiated", label: "managerHandover.adminRequest", allowed: capabilities.data?.canAdminInitiate }, { value: "InitialAssignment", label: "managerHandover.initialAssignment", allowed: capabilities.data?.canAssignInitial }] as const).filter((mode) => mode.allowed).map((mode) => <Button key={mode.value} type="button" size="sm" variant={initiationType === mode.value ? "default" : "outline"} onClick={() => { setInitiationType(mode.value); setNewManagerId(""); }}>{t(mode.label)}</Button>)}</div></div>
        <div><Label>{t("projects.project")}</Label><Input className="mt-1" value={project.name} readOnly /></div>
        <div><Label>{t("projects.currentProjectManager")}</Label><Input className="mt-1" value={capabilities.data?.currentManagerName || project.projectManagerName || t("managerHandover.notAssigned")} readOnly /></div>
        <div><Label>{t("projects.newManager")}</Label><SearchableSelect options={managerOptions} value={newManagerId} onValueChange={setNewManagerId} placeholder={t("projects.selectProjectManager")} searchPlaceholder={t("projects.selectProjectManager")} loading={managersLoading} emptyText={t("projects.noEligibleManagers")} disabled={managersError} /><input type="hidden" name="newProjectManagerId" value={newManagerId} />{managersError ? <p role="alert" className="mt-1 text-xs text-destructive">{t("common.errorLoading")}</p> : null}{!managersLoading && !managersError && managerOptions.length === 0 ? <div className="mt-2 space-y-1 text-xs text-muted-foreground"><p>{t("projects.noEligibleManagersHint")}</p><Link to="/administration/users" className="text-primary underline-offset-4 hover:underline">{t("projects.manageUsers")}</Link></div> : null}</div>
        <div><Label htmlFor="project-handover-effective-date">{t("projects.effectiveHandoverDate")}</Label><Input id="project-handover-effective-date" className="mt-1" name="effectiveHandoverDate" type="date" required min={minDate} max={maxDate} aria-invalid={Boolean(handoverDateError)} aria-describedby={handoverDateError ? "project-handover-date-error" : undefined} onChange={() => setHandoverDateError(null)} />{handoverDateError ? <p id="project-handover-date-error" role="alert" className="mt-1 text-xs text-destructive">{handoverDateError}</p> : <p className="mt-1 text-xs text-muted-foreground">{t("managerHandover.effectiveDateHint")}</p>}</div>
        <div><Label>{t(initiationType === "ManagerInitiated" ? "projects.handoverReason" : "managerHandover.administrativeReason")}</Label><Textarea className="mt-1" name="reason" required maxLength={500} /></div>
        <div><Label>{t("projects.handoverNotes")}</Label><Textarea className="mt-1" name="notes" rows={3} maxLength={2000} /></div>
      </SideSheetBody><SideSheetFooter><Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("common.cancel")}</Button><Button type="submit" disabled={create.isPending || query.isLoading || query.isError || managersLoading || managersError || !newManagerId || !selectedAllowed}>{create.isPending ? t("common.saving") : t("projects.submitHandover")}</Button></SideSheetFooter></form>}
    </SideSheet>
  </div>;
}

// The project history tab is now the backend-driven audit workspace. It lives in its
// own module to keep this already-large governance file focused.
export { ProjectHistoryTab } from "./ProjectAuditTrailTab";
export { ProjectSnapshotDetailPage, ProjectSnapshotsTab } from "./ProjectSnapshotsTab";

function ReportDetail({ label, value }: { label: string; value?: React.ReactNode }) {
  return <div className="flex items-baseline justify-between gap-4 border-b border-border/40 py-1.5"><span className="text-xs text-muted-foreground">{label}</span><div className="text-sm font-medium text-end">{value ?? "—"}</div></div>;
}

function ReportMetaField({ label, value }: { label: string; value?: React.ReactNode }) {
  return <div className="min-w-0 border-b border-border/60 py-2.5"><dt className="break-words text-xs text-muted-foreground">{label}</dt><dd className="mt-1 break-words text-sm font-medium leading-5 text-foreground">{value ?? "—"}</dd></div>;
}

function ReportMetric({ label, value }: { label: string; value?: React.ReactNode }) {
  return <div className="min-w-0 border-b border-border/60 py-2.5"><dt className="break-words text-xs text-muted-foreground">{label}</dt><dd className="mt-1 break-words text-base font-semibold leading-6 text-foreground tabular-nums">{value ?? "—"}</dd></div>;
}

function ReportStatus({ status, label, textClass, dotClass }: { status?: string | null; label: string; textClass?: string; dotClass?: string }) {
  const tone = getStatusTone(status);
  return <span className={`inline-flex min-w-0 items-center gap-2 text-sm font-medium ${textClass ?? getStatusTextClass(status)}`}><span aria-hidden="true" className={`h-2 w-2 shrink-0 rounded-full ${dotClass ?? statusToneDotClasses[tone]}`} /><span className="break-words">{label}</span></span>;
}

function ReportNarrative({ label, value, headingLevel = 4 }: { label: string; value?: string; headingLevel?: 3 | 4 }) {
  const Heading = headingLevel === 3 ? "h3" : "h4";
  return <div className="max-w-4xl space-y-1.5"><Heading className="text-sm font-semibold text-foreground">{label}</Heading><p className="whitespace-pre-wrap break-words text-sm leading-7 text-foreground">{value || "—"}</p></div>;
}

export function ProjectChangeTrackerTab() {
  const { projectId, project } = useProjectContext();
  const { t, locale, formatDate } = useI18n();
  const { user, status: authStatus } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.View));
  const query = useProjectChangeRequests(projectId, authStatus === "authenticated" && canView);
  const cancel = useCancelProjectChangeRequest(projectId);
  const remediate = useRemediateProjectChangeRequest(projectId);
  const reassign = useReassignProjectChangeRequestApproval(projectId);
  const [cancellingRequest, setCancellingRequest] = React.useState<ProjectChangeRequestDto | null>(null);
  const [cancelError, setCancelError] = React.useState<string | null>(null);
  const [createOpen, setCreateOpen] = React.useState(false);
  const [editingDraft, setEditingDraft] = React.useState<ProjectChangeRequestDto | null>(null);
  const [reassignTarget, setReassignTarget] = React.useState<{ request: ProjectChangeRequestDto; assignmentId: string } | null>(null);
  const [replacementApproverId, setReplacementApproverId] = React.useState("");
  const [reassignReason, setReassignReason] = React.useState("");
  const [approvalActionError, setApprovalActionError] = React.useState<string | null>(null);
  const reassignOptions = useProjectChangeRequestApproverOptions(project.publicId, Boolean(reassignTarget));
  const canManage = Boolean(user && hasPermission(user.permissions, Permissions.ChangeRequest.Manage) && canMutate && !effectiveReadOnly);
  const rows: ProjectChangeRequestDto[] = canView ? query.data ?? [] : [];
   function changeTypeLabel(type: string) {
     if (type === "Baseline") return t("projects.changeTypeBaseline");
     if (type === "BudgetChange") return t("projects.changeTypeBudgetChange");
     return t("projects.changeTypeAdministrative");
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
  async function handleRemediate(row: ProjectChangeRequestDto) {
    setApprovalActionError(null);
    try {
      const draft = await remediate.mutateAsync(row.id);
      setEditingDraft(draft);
      setCreateOpen(true);
    } catch (reason) {
      setApprovalActionError(reason instanceof Error ? reason.message : t("projects.changeRequestFailed"));
    }
  }
  async function handleReassign() {
    if (!reassignTarget || !replacementApproverId || !reassignReason.trim()) return;
    setApprovalActionError(null);
    try {
      await reassign.mutateAsync({ id: reassignTarget.request.id, assignmentId: reassignTarget.assignmentId, newApproverUserId: replacementApproverId, reason: reassignReason.trim() });
      setReassignTarget(null);
      setReplacementApproverId("");
      setReassignReason("");
    } catch (reason) {
      setApprovalActionError(reason instanceof Error ? reason.message : t("projects.changeRequestFailed"));
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
          {canManage && canView ? (
            <Button
              size="sm"
              onClick={() => { setEditingDraft(null); setCreateOpen(true); }}
              className="group shrink-0 gap-2 rounded-full bg-gradient-to-r from-primary to-primary/80 px-4 shadow-sm shadow-primary/25 transition-all hover:from-primary/90 hover:to-primary/70 hover:shadow-md hover:shadow-primary/30"
            >
              <Plus className="h-4 w-4 transition-transform duration-200 group-hover:rotate-90" />
              {t("projects.newChangeRequest")}
            </Button>
          ) : null}
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
              {canManage ? (
                <Button size="sm" onClick={() => { setEditingDraft(null); setCreateOpen(true); }} className="mt-3 gap-2 rounded-full bg-gradient-to-r from-primary to-primary/80 px-4 shadow-sm shadow-primary/25 transition-all hover:from-primary/90 hover:to-primary/70 hover:shadow-md">
                  <Plus className="h-4 w-4" />
                  {t("projects.newChangeRequest")}
                </Button>
              ) : null}
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
                  <TableHead>{t("projects.approvalProgress")}</TableHead>
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
                    <TableCell className="min-w-[180px]">
                      {row.status === "Pending" && !row.requiresApprovalSetup ? <div className="space-y-1 text-xs">
                        <span className="font-medium tabular-nums">{row.approvalCount}/{row.requiredApprovalCount}</span>
                        {(row.pendingWith ?? []).length ? <p className="text-muted-foreground">{t("projects.pendingWith")}: {row.pendingWith.join(", ")}</p> : null}
                        {canManage ? (row.approvals ?? []).filter((approval) => approval.status === "Pending").map((approval) => <button key={approval.id} type="button" className="block text-primary underline-offset-2 hover:underline" onClick={() => { setApprovalActionError(null); setReassignTarget({ request: row, assignmentId: approval.id }); setReplacementApproverId(""); setReassignReason(""); }}>
                          {t("projects.reassignApproval")}: {approval.approverName}
                        </button>) : null}
                      </div> : row.status === "Pending" ? <span className="text-xs text-warning">{t("projects.legacyApprovalSetup")}</span> : "—"}
                    </TableCell>
                    <TableCell>{row.requestedByName}</TableCell>
                    <TableCell className="whitespace-nowrap">{formatDate(row.requestedAt)}</TableCell>
                    <TableCell>{row.reviewedByName || "—"}</TableCell>
                    <TableCell className="whitespace-nowrap">{row.reviewedAt ? formatDate(row.reviewedAt) : "—"}</TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        {canManage && row.status === "Draft" ? <Button size="sm" variant="outline" onClick={() => { setEditingDraft(row); setCreateOpen(true); }}>{t("projects.editDraft")}</Button> : null}
                        {canManage && row.status === "Pending" && row.requiresApprovalSetup ? <Button size="sm" variant="outline" onClick={() => void handleRemediate(row)} disabled={remediate.isPending}>{remediate.isPending ? <Loader2 className="me-1 h-3.5 w-3.5 animate-spin" /> : null}{t("projects.setupApprovals")}</Button> : null}
                        {canManage && (row.status === "Pending" || row.status === "Draft") && row.requestedBy === user?.id ? <Button size="sm" variant="outline" onClick={() => confirmCancel(row)} disabled={cancel.isPending}>{t("projects.cancelChangeRequest")}</Button> : null}
                      </div>
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
    <ProjectChangeRequestActionSheet open={createOpen} onOpenChange={(open) => { setCreateOpen(open); if (!open) setEditingDraft(null); }} project={project} request={editingDraft} />
    <Dialog open={Boolean(reassignTarget)} ariaLabel={t("projects.reassignApprovalTitle")} onOpenChange={(open) => { if (!open && !reassign.isPending) { setReassignTarget(null); setApprovalActionError(null); } }}>
      <DialogContent>
        <DialogHeader><DialogTitle>{t("projects.reassignApprovalTitle")}</DialogTitle><DialogDescription>{reassignTarget?.request.reason}</DialogDescription></DialogHeader>
        <div className="space-y-3">
          <div><Label>{t("projects.selectReplacementApprover")}</Label><select className="input-field mt-1 w-full" value={replacementApproverId} onChange={(event) => setReplacementApproverId(event.target.value)} disabled={reassignOptions.isLoading || reassign.isPending}>
            <option value="">{t("projects.selectReplacementApprover")}</option>
            {reassignOptions.data?.approvers.map((approver) => <option key={approver.userId} value={approver.userId}>{approver.fullName} · {approver.projectRole}</option>)}
          </select></div>
          <div><Label>{t("projects.reassignReason")}</Label><Textarea className="mt-1" value={reassignReason} onChange={(event) => setReassignReason(event.target.value)} placeholder={t("projects.reassignReasonPlaceholder")} maxLength={2000} rows={3} /></div>
          {approvalActionError ? <p role="alert" className="text-sm text-destructive">{approvalActionError}</p> : null}
        </div>
        <DialogFooter><Button type="button" variant="outline" onClick={() => setReassignTarget(null)} disabled={reassign.isPending}>{t("common.cancel")}</Button><Button type="button" onClick={() => void handleReassign()} disabled={!replacementApproverId || !reassignReason.trim() || reassign.isPending}>{reassign.isPending ? <Loader2 className="me-1 h-4 w-4 animate-spin" /> : null}{t("projects.reassignApproval")}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  </div>);
}

export function ProjectMeetingsTab() {
  const { projectId, project } = useProjectContext();
  const { t } = useI18n();
  const { user, status: authStatus } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canView = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.View));
  const canCreate = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Create) && canMutate && !effectiveReadOnly);
  const canManageMinutes = Boolean(user && hasPermission(user.permissions, Permissions.Meeting.Manage) && canMutate && !effectiveReadOnly);

  return <Card>
    <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
      <div>
        <CardTitle className="flex items-center gap-2"><CalendarDays className="h-4 w-4" />{t("projects.projectMeetings.title")}</CardTitle>
        <CardDescription>{t("projects.projectMeetings.description")}</CardDescription>
      </div>
      {canCreate ? <Button asChild size="sm"><Link to={`/meetings/new?projectId=${encodeURIComponent(project.publicId || projectId)}`}><Plus className="me-1.5 h-4 w-4" />{t("meetings.createMeeting")}</Link></Button> : null}
    </CardHeader>
    <CardContent>
      <MeetingsBrowser
        lockedProject={{ publicId: project.publicId || projectId, name: project.name, code: project.code }}
        pageSize={10}
        canView={canView}
        canManageMinutes={canManageMinutes}
        authLoading={authStatus === "loading"}
        permissionDeniedText={t("projects.projectMeetings.permissionDenied")}
        emptyTitle={t("projects.projectMeetings.emptyTitle")}
        emptyDescription={t("projects.projectMeetings.emptyDescription")}
      />
    </CardContent>
  </Card>;
}
