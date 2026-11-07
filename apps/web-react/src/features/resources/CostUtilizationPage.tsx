import * as React from "react";
import { BarChart3, ChevronDown, Clock3, Coins, Gauge, TrendingUp, UsersRound } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Skeleton } from "@components/ui/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { useI18n } from "@i18n/messages";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { hasPermission, Permissions } from "@lib/permissions";
import { formatCurrencyValue } from "@lib/formatting/currency";
import { formatDateValue } from "@lib/formatting/date";
import { formatNumberValue } from "@lib/formatting/number";
import { useCostUtilization, type CostUtilizationAttribution, type CostUtilizationFilter, type CostUtilizationMetric } from "./api/cost-utilization";
import { CostUtilizationCalendar } from "./CostUtilizationCalendar";
import { CostUtilizationExportButton, CostUtilizationExportSheet } from "./CostUtilizationExportSheet";
import { getMonthRange, groupCostUtilizationByCurrency, groupCostUtilizationByDate, parseDateKey } from "./cost-utilization-calendar";

function hours(value: number, locale: "en" | "ar"): string {
  return `${formatNumberValue(value, locale, { maximumFractionDigits: 1 })}h`;
}

function percent(value: number | null | undefined, na: string, locale: "en" | "ar"): string {
  if (value == null) return na;
  return `${formatNumberValue(value, locale, { maximumFractionDigits: 1 })}%`;
}

function money(value: number | null | undefined, currency: string | null | undefined, locale: "en" | "ar", na: string): string {
  const formatted = formatCurrencyValue(value, currency, locale, { maximumFractionDigits: 2, numberingSystem: "latn" });
  return formatted === "—" ? na : formatted;
}

function Kpi({ label, value, icon: Icon }: { label: string; value: string; icon: typeof Gauge }) {
  return <Card><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle><Icon className="h-4 w-4 text-muted-foreground" /></CardHeader><CardContent><div className="text-2xl font-semibold tracking-tight tabular-nums">{value}</div></CardContent></Card>;
}

function CostLine({ metric, t, locale }: { metric: CostUtilizationMetric; t: (key: string) => string; locale: "en" | "ar" }) {
  const values: Array<[string, number | null | undefined, boolean]> = [
    ["plannedCost", metric.plannedLaborCost, false],
    ["actualCost", metric.actualLaborCost, false],
    ["variance", metric.laborCostVariance, false],
    ["overtimeCost", metric.overtimeCost, false],
    ["unusedCost", metric.unusedCapacityCost, false],
    ["totalCost", metric.totalResourceCost, true],
  ];
  return <div className="grid grid-cols-2 gap-3 text-sm md:grid-cols-3 xl:grid-cols-6">{values.map(([key, value, strong]) => <div key={key}><div className="text-xs text-muted-foreground">{t(`costUtilization.${key}`)}</div><div className={strong ? "font-semibold tabular-nums" : "tabular-nums"}>{money(value, metric.currency, locale, t("costUtilization.na"))}</div></div>)}</div>;
}

function FilterSelect({ value, placeholder, options, onChange }: { value?: string; placeholder: string; options: Array<{ value: string; label: string }>; onChange: (value: string) => void }) {
  return <Select value={value ?? "all"} onValueChange={onChange}>
    <SelectTrigger aria-label={placeholder}><SelectValue placeholder={placeholder} /></SelectTrigger>
    <SelectContent>
      <SelectItem value="all">{placeholder}</SelectItem>
      {options.map((option) => <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>)}
    </SelectContent>
  </Select>;
}

function AttributionTable({ rows, t, locale, canViewCosts }: { rows: CostUtilizationAttribution[]; t: (key: string) => string; locale: "en" | "ar"; canViewCosts: boolean }) {
  return <Table>
    <TableHeader><TableRow><TableHead>{t("costUtilization.resource")}</TableHead><TableHead>{t("costUtilization.plannedWork")}</TableHead><TableHead>{t("costUtilization.actualWork")}</TableHead>{canViewCosts ? <><TableHead>{t("costUtilization.actualCost")}</TableHead><TableHead>{t("costUtilization.overtimeCost")}</TableHead></> : null}</TableRow></TableHeader>
    <TableBody>{rows.slice(0, 20).map((row) => <TableRow key={`${row.id ?? "unassigned"}-${row.currency ?? ""}`}>
      <TableCell className="font-medium">{row.name}{row.currency ? <span className="ms-2 text-xs text-muted-foreground">{row.currency}</span> : null}</TableCell>
      <TableCell className="tabular-nums">{hours(row.plannedHours, locale)}</TableCell>
      <TableCell className="tabular-nums">{hours(row.actualHours, locale)}</TableCell>
      {canViewCosts ? <><TableCell className="tabular-nums">{money(row.actualLaborCost, row.currency, locale, t("costUtilization.na"))}</TableCell><TableCell className="tabular-nums">{money(row.overtimeCost, row.currency, locale, t("costUtilization.na"))}</TableCell></> : null}
    </TableRow>)}</TableBody>
  </Table>;
}

export function CostUtilizationPage() {
  const { t, locale } = useI18n();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canExport = Boolean(user
    && hasPermission(user.permissions, Permissions.Resources.View)
    && hasPermission(user.permissions, Permissions.CostUtilization.Export)
    && canMutate && !effectiveReadOnly);
  const today = React.useMemo(() => new Date(), []);
  const [filters, setFilters] = React.useState<CostUtilizationFilter>(() => getMonthRange(new Date()));
  const [view, setView] = React.useState<"daily" | "calendar">("calendar");
  const [calendarMonth, setCalendarMonth] = React.useState(() => new Date(today.getFullYear(), today.getMonth(), 1));
  const [selectedDay, setSelectedDay] = React.useState<string | null>(null);
  const [expandedDays, setExpandedDays] = React.useState<ReadonlySet<string>>(() => new Set());
  const [daySheetOpen, setDaySheetOpen] = React.useState(false);
  const [exportSheetOpen, setExportSheetOpen] = React.useState(false);
  const query = useCostUtilization(filters);
  const report = query.data;
  const resourceOptions = React.useMemo(() => report?.resources.filter((row, index, all) => all.findIndex((item) => item.resourceId === row.resourceId) === index) ?? [], [report?.resources]);
  const vendorOptions = React.useMemo(() => report?.vendors.filter((row) => row.id) ?? [], [report?.vendors]);
  const projectOptions = React.useMemo(() => report?.projects.filter((row) => row.id) ?? [], [report?.projects]);
  const roleOptions = React.useMemo(() => report?.roles.filter((row) => row.id) ?? [], [report?.roles]);
  const dailyByDate = React.useMemo(
    () => [...groupCostUtilizationByDate(report?.daily ?? [])].sort(([left], [right]) => left.localeCompare(right)).slice(0, 60),
    [report?.daily],
  );
  const resourceDetails = React.useMemo(
    () => new Map((report?.resources ?? []).map((resource) => [resource.resourceId, resource])),
    [report?.resources],
  );
  const update = (key: keyof CostUtilizationFilter, value: string) => {
    setFilters((current) => ({ ...current, [key]: value === "all" || value === "" ? undefined : value }));
    if (key === "from" && value) {
      const selected = parseDateKey(value);
      if (selected) setCalendarMonth(new Date(selected.getFullYear(), selected.getMonth(), 1));
    }
  };
  const applyCalendarMonth = (month: Date) => {
    const normalizedMonth = new Date(month.getFullYear(), month.getMonth(), 1);
    const range = getMonthRange(normalizedMonth);
    setCalendarMonth(normalizedMonth);
    setFilters((current) => ({ ...current, ...range }));
  };
  const openCalendar = () => {
    setView("calendar");
    applyCalendarMonth(calendarMonth);
  };
  const selectCalendarDay = (key: string) => {
    const selected = parseDateKey(key);
    if (!selected) return;
    setSelectedDay(key);
    setDaySheetOpen(true);
    setCalendarMonth(new Date(selected.getFullYear(), selected.getMonth(), 1));
    if ((filters.from && key < filters.from) || (filters.to && key > filters.to)) applyCalendarMonth(selected);
  };
  const toggleDay = (day: string) => setExpandedDays((current) => {
    const next = new Set(current);
    if (next.has(day)) next.delete(day);
    else next.add(day);
    return next;
  });
  const isReportLoading = Boolean(query.isLoading || query.isFetching);
  const overAllocated = report?.resources.filter((resource) => (resource.metrics.actualUtilization ?? 0) > 80).length ?? 0;
  const underUtilized = report?.resources.filter((resource) => (resource.metrics.actualUtilization ?? 0) > 0 && (resource.metrics.actualUtilization ?? 0) < 40).length ?? 0;
  const title = t("costUtilization.title");

  return <div className="space-y-6">
    <PageHeader title={title} description={t("costUtilization.description")} actions={canExport ? <CostUtilizationExportButton onClick={() => setExportSheetOpen(true)} /> : undefined} />

    <Card><CardContent className="grid gap-3 pt-6 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-7">
      <label className="space-y-1 text-xs text-muted-foreground"><span>{t("costUtilization.from")}</span><Input type="date" value={filters.from ?? ""} onChange={(event) => update("from", event.target.value)} /></label>
      <label className="space-y-1 text-xs text-muted-foreground"><span>{t("costUtilization.to")}</span><Input type="date" value={filters.to ?? ""} onChange={(event) => update("to", event.target.value)} /></label>
      <FilterSelect value={filters.resourceId} placeholder={t("costUtilization.allResources")} options={resourceOptions.map((row) => ({ value: row.resourceId, label: row.resourceName }))} onChange={(value) => update("resourceId", value)} />
      <FilterSelect value={filters.projectId} placeholder={t("costUtilization.allProjects")} options={projectOptions.filter((row) => row.projectPublicId).map((row) => ({ value: row.projectPublicId!, label: row.name }))} onChange={(value) => update("projectId", value)} />
      <FilterSelect value={filters.vendorId} placeholder={t("costUtilization.allVendors")} options={vendorOptions.map((row) => ({ value: row.id!, label: row.name }))} onChange={(value) => update("vendorId", value)} />
      <FilterSelect value={filters.projectRoleId} placeholder={t("costUtilization.allRoles")} options={roleOptions.map((row) => ({ value: row.id!, label: row.name }))} onChange={(value) => update("projectRoleId", value)} />
      <FilterSelect value={filters.departmentId} placeholder={t("costUtilization.allDepartments")} options={(report?.departments ?? []).map((department) => ({ value: department.departmentId, label: department.name }))} onChange={(value) => update("departmentId", value)} />
    </CardContent></Card>

    {query.isLoading ? <div className="grid gap-4 md:grid-cols-3">{[0, 1, 2].map((item) => <Skeleton key={item} className="h-28" />)}</div> : null}

    {report ? <>
      <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-7">
        <Kpi label={t("costUtilization.capacity")} value={hours(report.operational.capacityHours, locale)} icon={Gauge} />
        <Kpi label={t("costUtilization.plannedWork")} value={hours(report.operational.plannedHours, locale)} icon={BarChart3} />
        <Kpi label={t("costUtilization.actualWork")} value={hours(report.operational.actualHours, locale)} icon={Clock3} />
        <Kpi label={t("costUtilization.plannedUtilization")} value={percent(report.operational.plannedUtilization, t("costUtilization.na"), locale)} icon={Gauge} />
        <Kpi label={t("costUtilization.actualUtilization")} value={percent(report.operational.actualUtilization, t("costUtilization.na"), locale)} icon={UsersRound} />
        <Kpi label={t("costUtilization.overAllocated")} value={formatNumberValue(overAllocated, locale)} icon={TrendingUp} />
        <Kpi label={t("costUtilization.underUtilized")} value={formatNumberValue(underUtilized, locale)} icon={TrendingUp} />
      </div>

      {!report.canViewCosts ? <Card><CardContent className="flex items-center gap-2 pt-6 text-sm text-muted-foreground"><Coins className="h-4 w-4" />{t("costUtilization.costPermission")}</CardContent></Card> : null}

      {report.canViewCosts && report.byCurrency.length > 0 ? <Card><CardHeader><CardTitle>{t("costUtilization.currencyCosts")}</CardTitle></CardHeader><CardContent className="space-y-5">{report.byCurrency.map((metric) => <div key={metric.currency ?? "unknown"} className="space-y-3">
        <div className="flex items-center gap-2 font-medium">{metric.currency ? <span className="rounded bg-primary/10 px-2 py-1 text-xs">{metric.currency}</span> : null}<span>{hours(metric.actualHours, locale)} {t("costUtilization.actualWork")}</span></div>
        <CostLine metric={metric} t={t} locale={locale} />
      </div>)}</CardContent></Card> : null}

      <div className="grid gap-4 lg:grid-cols-3">
        <Card><CardHeader><CardTitle>{t("costUtilization.projects")}</CardTitle></CardHeader><CardContent><AttributionTable rows={report.projects} t={t} locale={locale} canViewCosts={report.canViewCosts} /></CardContent></Card>
        <Card><CardHeader><CardTitle>{t("costUtilization.vendor")}</CardTitle></CardHeader><CardContent><AttributionTable rows={report.vendors} t={t} locale={locale} canViewCosts={report.canViewCosts} /></CardContent></Card>
        <Card><CardHeader><CardTitle>{t("costUtilization.roles")}</CardTitle></CardHeader><CardContent><AttributionTable rows={report.roles} t={t} locale={locale} canViewCosts={report.canViewCosts} /></CardContent></Card>
      </div>
    </> : null}

    <Tabs value={view} onValueChange={(value) => value === "calendar" ? openCalendar() : setView("daily")}>
      <TabsList aria-label={t("costUtilization.views")}>
        <TabsTrigger value="daily">{t("costUtilization.daily")}</TabsTrigger>
        <TabsTrigger value="calendar">{t("costUtilization.calendarView")}</TabsTrigger>
      </TabsList>
      <TabsContent value="daily">
        {report ? <Card><CardHeader><CardTitle>{t("costUtilization.daily")}</CardTitle></CardHeader><CardContent>
          <div className="overflow-x-auto"><Table className="min-w-[980px] rtl:[&_th]:text-right"><TableHeader><TableRow><TableHead><span className="sr-only">{t("costUtilization.resourceBreakdown")}</span></TableHead><TableHead>{t("costUtilization.date")}</TableHead><TableHead>{t("costUtilization.capacity")}</TableHead><TableHead>{t("costUtilization.plannedWork")}</TableHead><TableHead>{t("costUtilization.actualWork")}</TableHead><TableHead>{t("costUtilization.plannedUtilization")}</TableHead><TableHead>{t("costUtilization.actualUtilization")}</TableHead><TableHead>{t("costUtilization.excess")}</TableHead><TableHead>{t("costUtilization.overtime")}</TableHead>{report.canViewCosts ? <TableHead>{t("costUtilization.actualCost")}</TableHead> : null}</TableRow></TableHeader>
            <TableBody>{dailyByDate.length === 0 ? <TableRow><TableCell colSpan={report.canViewCosts ? 10 : 9} className="py-8 text-center text-sm text-muted-foreground">{t("costUtilization.noDayData")}</TableCell></TableRow> : dailyByDate.map(([day, rows]) => {
              const expanded = expandedDays.has(day);
              const capacity = rows.reduce((total, row) => total + row.capacityHours, 0);
              const planned = rows.reduce((total, row) => total + row.plannedHours, 0);
              const actual = rows.reduce((total, row) => total + row.actualHours, 0);
              const plannedUtilization = capacity > 0 ? planned / capacity * 100 : undefined;
              const actualUtilization = capacity > 0 ? actual / capacity * 100 : undefined;
              const currencyMetrics = groupCostUtilizationByCurrency(rows);
              const breakdownId = `daily-resource-breakdown-${day}`;
              return <React.Fragment key={day}>
                <TableRow>
                  <TableCell><Button type="button" size="icon" variant="ghost" className="h-9 w-9" aria-label={`${t(expanded ? "costUtilization.collapseDayResources" : "costUtilization.expandDayResources")} ${formatDateValue(day, locale)}`} aria-controls={expanded ? breakdownId : undefined} aria-expanded={expanded} onClick={() => toggleDay(day)}><ChevronDown className={`h-4 w-4 transition-transform ${expanded ? "rotate-180" : ""}`} /></Button></TableCell>
                  <TableCell className="font-medium">{formatDateValue(day, locale)}</TableCell>
                  <TableCell className="tabular-nums">{hours(capacity, locale)}</TableCell><TableCell className="tabular-nums">{hours(planned, locale)}</TableCell><TableCell className="tabular-nums">{hours(actual, locale)}</TableCell>
                  <TableCell className="tabular-nums">{percent(plannedUtilization, t("costUtilization.na"), locale)}</TableCell><TableCell className="tabular-nums">{percent(actualUtilization, t("costUtilization.na"), locale)}</TableCell>
                  <TableCell className="tabular-nums">{hours(rows.reduce((total, row) => total + row.excessHours, 0), locale)}</TableCell><TableCell className="tabular-nums">{hours(rows.reduce((total, row) => total + row.overtimeHours, 0), locale)}</TableCell>
                  {report.canViewCosts ? <TableCell className="space-y-1 tabular-nums">{currencyMetrics.map((metric, index) => <div key={`${metric.currency ?? "unavailable"}-${index}`} className="whitespace-nowrap">{metric.currency ?? t("costUtilization.na")}: {money(metric.actualLaborCost, metric.currency, locale, t("costUtilization.na"))}</div>)}</TableCell> : null}
                </TableRow>
                {expanded ? <TableRow><TableCell colSpan={report.canViewCosts ? 10 : 9} className="bg-muted/20 p-0">
                  <div id={breakdownId} className="space-y-2 p-3 sm:p-4">
                    <h4 className="text-sm font-semibold">{t("costUtilization.resourceBreakdown")}</h4>
                    <div className="overflow-x-auto"><Table className="min-w-[1100px] rtl:[&_th]:text-right">
                      <TableHeader><TableRow><TableHead>{t("costUtilization.resource")}</TableHead><TableHead>{t("costUtilization.type")}</TableHead><TableHead>{t("costUtilization.department")}</TableHead><TableHead>{t("costUtilization.vendor")}</TableHead><TableHead>{t("costUtilization.capacity")}</TableHead><TableHead>{t("costUtilization.plannedWork")}</TableHead><TableHead>{t("costUtilization.actualWork")}</TableHead><TableHead>{t("costUtilization.plannedUtilization")}</TableHead><TableHead>{t("costUtilization.actualUtilization")}</TableHead><TableHead>{t("costUtilization.excess")}</TableHead><TableHead>{t("costUtilization.overtime")}</TableHead>{report.canViewCosts ? <TableHead>{t("costUtilization.actualCost")}</TableHead> : null}</TableRow></TableHeader>
                      <TableBody>{rows.map((item) => {
                        const resource = resourceDetails.get(item.resourceId);
                        return <TableRow key={`${item.resourceId}-${item.currency ?? ""}-${item.workDate}`}>
                          <TableCell className="font-medium">{item.resourceName}<span className="ms-2 text-xs text-muted-foreground">{item.currency ?? t("costUtilization.na")}</span></TableCell>
                          <TableCell>{resource?.resourceType ?? t("costUtilization.na")}</TableCell><TableCell>{resource?.departmentName ?? t("costUtilization.na")}</TableCell><TableCell>{resource?.vendorName ?? t("costUtilization.na")}</TableCell>
                          <TableCell className="tabular-nums">{hours(item.capacityHours, locale)}</TableCell><TableCell className="tabular-nums">{hours(item.plannedHours, locale)}</TableCell><TableCell className="tabular-nums">{hours(item.actualHours, locale)}</TableCell>
                          <TableCell className="tabular-nums">{percent(item.plannedUtilization, t("costUtilization.na"), locale)}</TableCell><TableCell className="tabular-nums">{percent(item.actualUtilization, t("costUtilization.na"), locale)}</TableCell>
                          <TableCell className="tabular-nums">{hours(item.excessHours, locale)}</TableCell><TableCell className="tabular-nums">{hours(item.overtimeHours, locale)}</TableCell>
                          {report.canViewCosts ? <TableCell className="tabular-nums">{money(item.actualLaborCost, item.currency, locale, t("costUtilization.na"))}</TableCell> : null}
                        </TableRow>;
                      })}</TableBody>
                    </Table></div>
                  </div>
                </TableCell></TableRow> : null}
              </React.Fragment>;
            })}</TableBody>
          </Table></div>
        </CardContent></Card> : query.isError ? <Card><CardContent role="alert" className="pt-6 text-sm text-destructive">{t("common.errorLoading")}</CardContent></Card> : query.isLoading ? <div className="space-y-2"><Skeleton className="h-10" /><Skeleton className="h-48" /></div> : null}
      </TabsContent>
      <TabsContent value="calendar">
        <CostUtilizationCalendar
          month={calendarMonth}
          rows={report?.daily ?? []}
          canViewCosts={report?.canViewCosts ?? false}
          isLoading={isReportLoading}
          isError={Boolean(query.isError)}
          from={filters.from}
          to={filters.to}
          selectedDay={selectedDay}
          sheetOpen={daySheetOpen}
          onMonthChange={applyCalendarMonth}
          onDaySelect={selectCalendarDay}
          onSheetOpenChange={setDaySheetOpen}
          onRetry={() => { void query.refetch(); }}
        />
      </TabsContent>
    </Tabs>
    {canExport ? <CostUtilizationExportSheet
      open={exportSheetOpen}
      onOpenChange={setExportSheetOpen}
      currentFilters={filters}
      selectedDay={selectedDay}
    /> : null}
  </div>;
}
