import * as React from "react";
import { Download, FileSpreadsheet, FileText, Loader2, Sparkles } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Checkbox } from "@components/ui/Checkbox";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { SideSheet, SideSheetBody, SideSheetFooter } from "@components/ui/SideSheet";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { useActiveTeams } from "@features/teams/api/teams";
import { useI18n } from "@i18n/messages";
import { hasPermission, Permissions } from "@lib/permissions";
import { dateKey, parseDateKey } from "./cost-utilization-calendar";
import { useCostUtilizationExportData, type CostUtilizationFilter } from "./api/cost-utilization";
import {
  exportCostUtilizationExcel,
  exportCostUtilizationPdf,
  type CostUtilizationExportFormat,
  type CostUtilizationExportReportType,
  type CostUtilizationExportInput,
} from "./cost-utilization-export";

type DatePreset = "custom" | "today" | "week" | "month";

const exportLocalizationKeys = new Set([
  "export", "exportSettings", "exportSettingsDescription", "exportFormat", "pdf", "excel", "exportReportType", "exportDateRange",
  "custom", "today", "thisWeek", "thisMonth", "reportSummary", "calendarSummary", "exportFilters", "team", "role", "currency",
  "allTeams", "allActivities", "activitiesUnavailable", "activitiesUnavailableDescription", "teamPermissionRequired", "loadingOptions",
  "optionsUnavailable", "includeBreakdowns", "includeBreakdownsDescription", "loadingExportData", "exportLoadFailed", "emptyExport",
  "exportFailed", "exportGenerating", "generateExport", "invalidDateRange", "exportRangeLimit", "dayOutsideRange", "dayDate", "allFilters",
  "reportPeriod", "reportFilters", "reportTotals", "metric", "value", "section", "unit", "hoursUnit", "calendarHours", "calendarCosts",
  "workItems", "page", "of",
]);

interface ExportForm {
  format: CostUtilizationExportFormat;
  datePreset: DatePreset;
  reportType: CostUtilizationExportReportType;
  includeBreakdowns: boolean;
  dayDate: string;
  filters: CostUtilizationFilter;
}

interface CostUtilizationExportSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  currentFilters: CostUtilizationFilter;
  selectedDay?: string | null;
}

function currentDateKey() {
  return dateKey(new Date());
}

function rangeForPreset(preset: Exclude<DatePreset, "custom">, anchor = new Date()) {
  const start = new Date(anchor.getFullYear(), anchor.getMonth(), anchor.getDate());
  if (preset === "today") return { from: dateKey(start), to: dateKey(start) };
  if (preset === "week") {
    start.setDate(start.getDate() - start.getDay());
    const end = new Date(start.getFullYear(), start.getMonth(), start.getDate() + 6);
    return { from: dateKey(start), to: dateKey(end) };
  }
  const monthStart = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
  const monthEnd = new Date(anchor.getFullYear(), anchor.getMonth() + 1, 0);
  return { from: dateKey(monthStart), to: dateKey(monthEnd) };
}

function createForm(filters: CostUtilizationFilter, selectedDay?: string | null): ExportForm {
  const today = currentDateKey();
  const from = filters.from ?? today;
  const to = filters.to ?? today;
  return {
    format: "pdf",
    datePreset: "custom",
    reportType: "summary",
    includeBreakdowns: true,
    dayDate: selectedDay ?? to,
    filters: { ...filters, from, to },
  };
}

function rangeLengthDays(from: string | undefined, to: string | undefined): number | null {
  if (!from || !to) return null;
  const start = parseDateKey(from);
  const end = parseDateKey(to);
  if (!start || !end) return null;
  return Math.floor((Date.UTC(end.getFullYear(), end.getMonth(), end.getDate()) - Date.UTC(start.getFullYear(), start.getMonth(), start.getDate())) / 86_400_000) + 1;
}

function FilterSelect({
  value,
  placeholder,
  options,
  onChange,
  disabled = false,
}: {
  value?: string;
  placeholder: string;
  options: Array<{ value: string; label: string }>;
  onChange: (value: string) => void;
  disabled?: boolean;
}) {
  return <Select value={value || "all"} onValueChange={onChange}>
    <SelectTrigger aria-label={placeholder} disabled={disabled}><SelectValue placeholder={placeholder} /></SelectTrigger>
    <SelectContent>
      <SelectItem value="all">{placeholder}</SelectItem>
      {options.map((option) => <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>)}
    </SelectContent>
  </Select>;
}

export function CostUtilizationExportSheet({ open, onOpenChange, currentFilters, selectedDay }: CostUtilizationExportSheetProps) {
  const { t: translate, locale } = useI18n();
  const t = React.useCallback((key: string) => {
    const prefix = "costUtilization.";
    const exportKey = key.startsWith(prefix) ? key.slice(prefix.length) : "";
    return exportKey && exportLocalizationKeys.has(exportKey) ? translate(`costUtilizationExport.${exportKey}`) : translate(key);
  }, [translate]);
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canExport = Boolean(user
    && hasPermission(user.permissions, Permissions.Resources.View)
    && hasPermission(user.permissions, Permissions.CostUtilization.Export)
    && canMutate && !effectiveReadOnly);
  const canViewTeams = Boolean(user && hasPermission(user.permissions, Permissions.Team.View));
  const [form, setForm] = React.useState<ExportForm>(() => createForm(currentFilters, selectedDay));
  const [isExporting, setIsExporting] = React.useState(false);
  const [exportError, setExportError] = React.useState(false);
  const teamQuery = useActiveTeams(open && canViewTeams);

  React.useEffect(() => {
    if (open) {
      setForm(createForm(currentFilters, selectedDay));
      setExportError(false);
    }
  }, [open, currentFilters.from, currentFilters.to, currentFilters.resourceId, currentFilters.projectId, currentFilters.vendorId, currentFilters.projectRoleId, currentFilters.departmentId, selectedDay]);

  const from = form.filters.from;
  const to = form.filters.to;
  const rangeDays = rangeLengthDays(from, to);
  const dateRangeValid = Boolean(rangeDays && rangeDays > 0 && rangeDays <= 366);
  const filterOptions = React.useMemo<CostUtilizationFilter>(() => ({ from, to }), [from, to]);
  const exportQueryEnabled = open && canExport && dateRangeValid;
  const filterOptionsQuery = useCostUtilizationExportData(filterOptions, exportQueryEnabled);
  const filterOptionsReport = filterOptionsQuery.data;
  const dayInRange = !form.reportType.includes("dayDetails") || Boolean(form.dayDate && form.dayDate >= (from ?? "") && form.dayDate <= (to ?? ""));
  const requestFilters = React.useMemo(() => form.reportType === "dayDetails"
    ? { ...form.filters, from: form.dayDate, to: form.dayDate }
    : form.filters,
  [form.filters, form.reportType, form.dayDate]);
  const reportQuery = useCostUtilizationExportData(requestFilters, exportQueryEnabled && dayInRange);
  const report = reportQuery.data;
  const isEmpty = Boolean(report && report.resources.length === 0 && report.daily.length === 0);

  const activities = React.useMemo(() => {
    const byId = new Map<string, string>();
    for (const row of filterOptionsReport?.daily ?? report?.daily ?? []) {
      for (const activity of row.activities ?? []) {
        if (activity.planActivityId) byId.set(activity.planActivityId, activity.activityName ?? t("costUtilization.activityNameUnavailable"));
      }
    }
    return [...byId.entries()].sort(([, left], [, right]) => left.localeCompare(right)).map(([value, label]) => ({ value, label }));
  }, [filterOptionsReport?.daily, report?.daily, t]);

  const setFilter = (key: keyof CostUtilizationFilter, value: string) => {
    setForm((current) => ({ ...current, filters: { ...current.filters, [key]: value === "all" || value === "" ? undefined : value } }));
  };

  const setPreset = (preset: DatePreset) => {
    if (preset === "custom") {
      setForm((current) => ({ ...current, datePreset: preset }));
      return;
    }
    const range = rangeForPreset(preset);
    setForm((current) => ({ ...current, datePreset: preset, dayDate: range.to, filters: { ...current.filters, ...range } }));
  };

  const setDate = (key: "from" | "to", value: string) => {
    setForm((current) => {
      const filters = { ...current.filters, [key]: value };
      return { ...current, datePreset: "custom", dayDate: current.reportType === "dayDetails" && (current.dayDate < (filters.from ?? "") || current.dayDate > (filters.to ?? "")) ? (filters.to ?? value) : current.dayDate, filters };
    });
  };

  const optionNames = React.useMemo(() => {
    const project = filterOptionsReport?.projects.find((row) => row.projectPublicId === form.filters.projectId)?.name;
    const resource = filterOptionsReport?.resources.find((row) => row.resourceId === form.filters.resourceId)?.resourceName;
    const vendor = filterOptionsReport?.vendors.find((row) => row.id === form.filters.vendorId)?.name;
    const role = filterOptionsReport?.roles.find((row) => row.id === form.filters.projectRoleId)?.name;
    const team = teamQuery.data?.find((row) => row.id === form.filters.teamId)?.name;
    const department = filterOptionsReport?.departments?.find((row) => row.departmentId === form.filters.departmentId)?.name;
    const activity = activities.find((row) => row.value === form.filters.planActivityId)?.label;
    return { project, resource, vendor, role, team, department, activity };
  }, [filterOptionsReport, form.filters, teamQuery.data, activities]);

  const exportInput = React.useMemo<CostUtilizationExportInput | null>(() => {
    if (!report) return null;
    const selectedFilters: Array<{ label: string; value: string }> = [];
    const selections: Array<[string, string | undefined, string | undefined]> = [
      ["costUtilization.project", form.filters.projectId, optionNames.project],
      ["costUtilization.team", form.filters.teamId, optionNames.team],
      ["costUtilization.resource", form.filters.resourceId, optionNames.resource],
      ["costUtilization.activity", form.filters.planActivityId, optionNames.activity],
      ["costUtilization.vendor", form.filters.vendorId, optionNames.vendor],
      ["costUtilization.role", form.filters.projectRoleId, optionNames.role],
      ["costUtilization.department", form.filters.departmentId, optionNames.department],
    ];
    for (const [label, id, value] of selections) if (id) selectedFilters.push({ label: t(label), value: value ?? t("costUtilization.na") });
    const reportTypes: Record<CostUtilizationExportReportType, string> = {
      summary: t("costUtilization.reportSummary"),
      dailyDetail: t("costUtilization.daily"),
      calendarSummary: t("costUtilization.calendarSummary"),
      dayDetails: t("costUtilization.dayDetails"),
    };
    const reportFrom = report.from.slice(0, 10);
    const reportTo = report.to.slice(0, 10);
    return {
      report,
      locale: locale === "ar" ? "ar" : "en",
      reportType: form.reportType,
      reportTypeLabel: reportTypes[form.reportType],
      title: `${t("costUtilization.title")} — ${reportTypes[form.reportType]}`,
      from: reportFrom,
      to: reportTo,
      filters: selectedFilters,
      includeBreakdowns: form.includeBreakdowns,
      translate: t,
    };
  }, [report, locale, form.reportType, form.includeBreakdowns, form.filters, optionNames, t]);

  const generateExport = async () => {
    if (!canExport || !exportInput) return;
    setIsExporting(true);
    setExportError(false);
    try {
      if (form.format === "pdf") await exportCostUtilizationPdf(exportInput);
      else await exportCostUtilizationExcel(exportInput);
    } catch {
      setExportError(true);
    } finally {
      setIsExporting(false);
    }
  };

  const reportTypeChange = (value: string) => {
    const reportType = value as CostUtilizationExportReportType;
    setForm((current) => {
      const start = current.filters.from ?? "";
      const end = current.filters.to ?? "";
      const candidateDay = selectedDay && selectedDay >= start && selectedDay <= end ? selectedDay : end;
      return { ...current, reportType, dayDate: candidateDay };
    });
  };

  const validationMessage = !dateRangeValid
    ? rangeDays && rangeDays > 366 ? t("costUtilization.exportRangeLimit") : t("costUtilization.invalidDateRange")
    : !dayInRange ? t("costUtilization.dayOutsideRange") : null;

  if (!canExport) return null;

  return <SideSheet
    open={open}
    onOpenChange={onOpenChange}
    title={t("costUtilization.exportSettings")}
    description={t("costUtilization.exportSettingsDescription")}
    size="lg"
    ariaLabel={t("costUtilization.exportSettings")}
    closeLabel={t("common.close")}
  >
    <SideSheetBody className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label>{t("costUtilization.exportFormat")}</Label>
          <Select value={form.format} onValueChange={(value) => setForm((current) => ({ ...current, format: value as CostUtilizationExportFormat }))}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="pdf">{t("costUtilization.pdf")}</SelectItem>
              <SelectItem value="xlsx">{t("costUtilization.excel")}</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label>{t("costUtilization.exportReportType")}</Label>
          <Select value={form.reportType} onValueChange={reportTypeChange}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="summary">{t("costUtilization.reportSummary")}</SelectItem>
              <SelectItem value="dailyDetail">{t("costUtilization.daily")}</SelectItem>
              <SelectItem value="calendarSummary">{t("costUtilization.calendarSummary")}</SelectItem>
              <SelectItem value="dayDetails">{t("costUtilization.dayDetails")}</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="space-y-2">
        <Label>{t("costUtilization.exportDateRange")}</Label>
        <Select value={form.datePreset} onValueChange={(value) => setPreset(value as DatePreset)}>
          <SelectTrigger><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="custom">{t("costUtilization.custom")}</SelectItem>
            <SelectItem value="today">{t("costUtilization.today")}</SelectItem>
            <SelectItem value="week">{t("costUtilization.thisWeek")}</SelectItem>
            <SelectItem value="month">{t("costUtilization.thisMonth")}</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <label className="space-y-1 text-sm"><span>{t("costUtilization.from")}</span><Input type="date" value={from ?? ""} onChange={(event) => setDate("from", event.target.value)} /></label>
        <label className="space-y-1 text-sm"><span>{t("costUtilization.to")}</span><Input type="date" value={to ?? ""} onChange={(event) => setDate("to", event.target.value)} /></label>
        {form.reportType === "dayDetails" ? <label className="space-y-1 text-sm sm:col-span-2"><span>{t("costUtilization.dayDate")}</span><Input type="date" value={form.dayDate} min={from} max={to} onChange={(event) => setForm((current) => ({ ...current, dayDate: event.target.value }))} /></label> : null}
      </div>

      {validationMessage ? <p role="alert" className="text-sm text-destructive">{validationMessage}</p> : null}

      <section className="space-y-3 rounded-lg border p-4">
        <h3 className="text-sm font-semibold">{t("costUtilization.exportFilters")}</h3>
        {filterOptionsQuery.isLoading ? <p role="status" className="text-xs text-muted-foreground">{t("costUtilization.loadingOptions")}</p> : null}
        {filterOptionsQuery.isError ? <p role="alert" className="text-xs text-destructive">{t("costUtilization.optionsUnavailable")}</p> : null}
        <div className="grid gap-3 sm:grid-cols-2">
          <FilterSelect value={form.filters.projectId} placeholder={t("costUtilization.allProjects")} options={(filterOptionsReport?.projects ?? []).filter((row) => row.projectPublicId).map((row) => ({ value: row.projectPublicId!, label: row.name }))} onChange={(value) => setFilter("projectId", value)} />
          <div className="space-y-1">
            <FilterSelect value={form.filters.teamId} placeholder={canViewTeams ? t("costUtilization.allTeams") : t("costUtilization.teamPermissionRequired")} options={(teamQuery.data ?? []).map((row) => ({ value: row.id, label: row.name }))} onChange={(value) => setFilter("teamId", value)} disabled={!canViewTeams || teamQuery.isLoading} />
            {teamQuery.isLoading ? <p className="text-xs text-muted-foreground">{t("costUtilization.loadingOptions")}</p> : null}
            {teamQuery.isError && canViewTeams ? <p role="alert" className="text-xs text-destructive">{t("costUtilization.optionsUnavailable")}</p> : null}
          </div>
          <FilterSelect value={form.filters.resourceId} placeholder={t("costUtilization.allResources")} options={(filterOptionsReport?.resources ?? []).map((row) => ({ value: row.resourceId, label: row.resourceName }))} onChange={(value) => setFilter("resourceId", value)} />
          <div className="space-y-1">
            <FilterSelect value={form.filters.planActivityId} placeholder={activities.length ? t("costUtilization.allActivities") : t("costUtilization.activitiesUnavailable")} options={activities} onChange={(value) => setFilter("planActivityId", value)} disabled={activities.length === 0} />
            {activities.length === 0 ? <p className="text-xs text-muted-foreground">{t("costUtilization.activitiesUnavailableDescription")}</p> : null}
          </div>
          <FilterSelect value={form.filters.vendorId} placeholder={t("costUtilization.allVendors")} options={(filterOptionsReport?.vendors ?? []).filter((row) => row.id).map((row) => ({ value: row.id!, label: row.name }))} onChange={(value) => setFilter("vendorId", value)} />
          <FilterSelect value={form.filters.projectRoleId} placeholder={t("costUtilization.allRoles")} options={(filterOptionsReport?.roles ?? []).filter((row) => row.id).map((row) => ({ value: row.id!, label: row.name }))} onChange={(value) => setFilter("projectRoleId", value)} />
          <FilterSelect value={form.filters.departmentId} placeholder={t("costUtilization.allDepartments")} options={(filterOptionsReport?.departments ?? []).map((row) => ({ value: row.departmentId, label: row.name }))} onChange={(value) => setFilter("departmentId", value)} disabled={filterOptionsQuery.isLoading} />
        </div>
      </section>

      <label className="flex items-start gap-3 rounded-lg border p-4 text-sm">
        <Checkbox checked={form.includeBreakdowns} onCheckedChange={() => setForm((current) => ({ ...current, includeBreakdowns: !current.includeBreakdowns }))} className="mt-0.5" />
        <span><span className="font-medium">{t("costUtilization.includeBreakdowns")}</span><span className="mt-1 block text-xs text-muted-foreground">{t("costUtilization.includeBreakdownsDescription")}</span></span>
      </label>

      {reportQuery.isLoading || reportQuery.isFetching ? <div role="status" className="rounded-md border p-3 text-sm text-muted-foreground">{t("costUtilization.loadingExportData")}</div> : null}
      {reportQuery.isError ? <div role="alert" className="rounded-md border border-destructive/40 p-3 text-sm text-destructive">{t("costUtilization.exportLoadFailed")}</div> : null}
      {report && isEmpty && !reportQuery.isFetching ? <div role="status" className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">{t("costUtilization.emptyExport")}</div> : null}
      {exportError ? <div role="alert" className="rounded-md border border-destructive/40 p-3 text-sm text-destructive">{t("costUtilization.exportFailed")}</div> : null}
    </SideSheetBody>
    <SideSheetFooter>
      <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>{t("common.cancel")}</Button>
      <Button type="button" onClick={() => { void generateExport(); }} disabled={Boolean(validationMessage) || !report || reportQuery.isLoading || reportQuery.isFetching || isExporting}>
        {isExporting ? <Loader2 className="me-2 h-4 w-4 animate-spin" /> : form.format === "pdf" ? <FileText className="me-2 h-4 w-4" /> : <FileSpreadsheet className="me-2 h-4 w-4" />}
        {isExporting ? t("costUtilization.exportGenerating") : t("costUtilization.generateExport")}
      </Button>
    </SideSheetFooter>
  </SideSheet>;
}

export function CostUtilizationExportButton({ onClick }: { onClick: () => void }) {
  const { t } = useI18n();
  return <Button
    type="button"
    variant="default"
    onClick={onClick}
    className="group shadow-sm transition-shadow hover:shadow-md"
  >
    <Download className="me-2 h-4 w-4" />
    {t("costUtilizationExport.export")}
    <Sparkles className="ms-2 h-3.5 w-3.5 text-primary-foreground/80 transition-transform group-hover:rotate-12" />
  </Button>;
}
