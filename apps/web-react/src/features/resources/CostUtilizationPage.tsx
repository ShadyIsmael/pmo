import * as React from "react";
import { AlertTriangle, BarChart3, Clock3, Coins, Gauge, TrendingUp, UsersRound } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@components/ui/Select";
import { Skeleton } from "@components/ui/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useDepartments } from "@features/teams/api/teams";
import { useCostUtilization, type CostUtilizationAttribution, type CostUtilizationFilter, type CostUtilizationMetric } from "./api/cost-utilization";

function dateValue(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function hours(value: number, locale: "en" | "ar"): string {
  return `${new Intl.NumberFormat(locale === "ar" ? "ar-SA" : "en-US", { maximumFractionDigits: 1, numberingSystem: "latn" }).format(value)}h`;
}

function percent(value: number | null | undefined, na: string, locale: "en" | "ar"): string {
  if (value == null) return na;
  return `${new Intl.NumberFormat(locale === "ar" ? "ar-SA" : "en-US", { maximumFractionDigits: 1, numberingSystem: "latn" }).format(value)}%`;
}

function money(value: number | null | undefined, currency: string | null | undefined, locale: "en" | "ar", na: string): string {
  if (value == null || !currency) return na;
  try {
    return new Intl.NumberFormat(locale === "ar" ? "ar-SA" : "en-US", {
      style: "currency",
      currency,
      maximumFractionDigits: 2,
      numberingSystem: "latn",
    }).format(value);
  } catch {
    return `${new Intl.NumberFormat(locale === "ar" ? "ar-SA" : "en-US", { maximumFractionDigits: 2, numberingSystem: "latn" }).format(value)} ${currency}`;
  }
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
  const [filters, setFilters] = React.useState<CostUtilizationFilter>(() => ({
    from: dateValue(new Date(Date.now() - 29 * 86_400_000)),
    to: dateValue(new Date()),
  }));
  const query = useCostUtilization(filters);
  const departmentsQuery = useDepartments(false);
  const report = query.data;
  const resourceOptions = React.useMemo(() => report?.resources.filter((row, index, all) => all.findIndex((item) => item.resourceId === row.resourceId) === index) ?? [], [report?.resources]);
  const vendorOptions = React.useMemo(() => report?.vendors.filter((row) => row.id) ?? [], [report?.vendors]);
  const projectOptions = React.useMemo(() => report?.projects.filter((row) => row.id) ?? [], [report?.projects]);
  const roleOptions = React.useMemo(() => report?.roles.filter((row) => row.id) ?? [], [report?.roles]);
  const update = (key: keyof CostUtilizationFilter, value: string) => setFilters((current) => ({ ...current, [key]: value === "all" || value === "" ? undefined : value }));
  const overAllocated = report?.resources.filter((resource) => (resource.metrics.actualUtilization ?? 0) > 80).length ?? 0;
  const underUtilized = report?.resources.filter((resource) => (resource.metrics.actualUtilization ?? 0) > 0 && (resource.metrics.actualUtilization ?? 0) < 40).length ?? 0;
  const title = t("costUtilization.title");

  return <div className="space-y-6">
    <PageHeader title={title} description={t("costUtilization.description")} />

    <Card><CardContent className="grid gap-3 pt-6 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-7">
      <label className="space-y-1 text-xs text-muted-foreground"><span>{t("costUtilization.from")}</span><Input type="date" value={filters.from ?? ""} onChange={(event) => update("from", event.target.value)} /></label>
      <label className="space-y-1 text-xs text-muted-foreground"><span>{t("costUtilization.to")}</span><Input type="date" value={filters.to ?? ""} onChange={(event) => update("to", event.target.value)} /></label>
      <FilterSelect value={filters.resourceId} placeholder={t("costUtilization.allResources")} options={resourceOptions.map((row) => ({ value: row.resourceId, label: row.resourceName }))} onChange={(value) => update("resourceId", value)} />
      <FilterSelect value={filters.projectId} placeholder={t("costUtilization.allProjects")} options={projectOptions.filter((row) => row.projectPublicId).map((row) => ({ value: row.projectPublicId!, label: row.name }))} onChange={(value) => update("projectId", value)} />
      <FilterSelect value={filters.vendorId} placeholder={t("costUtilization.allVendors")} options={vendorOptions.map((row) => ({ value: row.id!, label: row.name }))} onChange={(value) => update("vendorId", value)} />
      <FilterSelect value={filters.projectRoleId} placeholder={t("costUtilization.allRoles")} options={roleOptions.map((row) => ({ value: row.id!, label: row.name }))} onChange={(value) => update("projectRoleId", value)} />
      <FilterSelect value={filters.departmentId} placeholder={t("costUtilization.allDepartments")} options={(departmentsQuery.data ?? []).map((department) => ({ value: department.id, label: department.name }))} onChange={(value) => update("departmentId", value)} />
    </CardContent></Card>

    {query.isError ? <Card><CardContent role="alert" className="flex items-center gap-2 pt-6 text-sm text-destructive"><AlertTriangle className="h-4 w-4" />{t("costUtilization.noData")}</CardContent></Card> : null}
    {query.isLoading ? <div className="grid gap-4 md:grid-cols-3">{[0, 1, 2].map((item) => <Skeleton key={item} className="h-28" />)}</div> : null}

    {report ? <>
      <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-7">
        <Kpi label={t("costUtilization.capacity")} value={hours(report.operational.capacityHours, locale)} icon={Gauge} />
        <Kpi label={t("costUtilization.plannedWork")} value={hours(report.operational.plannedHours, locale)} icon={BarChart3} />
        <Kpi label={t("costUtilization.actualWork")} value={hours(report.operational.actualHours, locale)} icon={Clock3} />
        <Kpi label={t("costUtilization.plannedUtilization")} value={percent(report.operational.plannedUtilization, t("costUtilization.na"), locale)} icon={Gauge} />
        <Kpi label={t("costUtilization.actualUtilization")} value={percent(report.operational.actualUtilization, t("costUtilization.na"), locale)} icon={UsersRound} />
        <Kpi label={t("costUtilization.overAllocated")} value={new Intl.NumberFormat("en-US", { numberingSystem: "latn" }).format(overAllocated)} icon={TrendingUp} />
        <Kpi label={t("costUtilization.underUtilized")} value={new Intl.NumberFormat("en-US", { numberingSystem: "latn" }).format(underUtilized)} icon={TrendingUp} />
      </div>

      {!report.canViewCosts ? <Card><CardContent className="flex items-center gap-2 pt-6 text-sm text-muted-foreground"><Coins className="h-4 w-4" />{t("costUtilization.costPermission")}</CardContent></Card> : null}

      {report.canViewCosts && report.byCurrency.length > 0 ? <Card><CardHeader><CardTitle>{t("costUtilization.currencyCosts")}</CardTitle></CardHeader><CardContent className="space-y-5">{report.byCurrency.map((metric) => <div key={metric.currency ?? "unknown"} className="space-y-3">
        <div className="flex items-center gap-2 font-medium">{metric.currency ? <span className="rounded bg-primary/10 px-2 py-1 text-xs">{metric.currency}</span> : null}<span>{hours(metric.actualHours, locale)} {t("costUtilization.actualWork")}</span></div>
        <CostLine metric={metric} t={t} locale={locale} />
      </div>)}</CardContent></Card> : null}

      <Card><CardHeader><CardTitle>{t("costUtilization.resourceBreakdown")}</CardTitle></CardHeader><CardContent>
        <Table className="min-w-[1100px]">
          <TableHeader><TableRow><TableHead>{t("costUtilization.resource")}</TableHead><TableHead>{t("costUtilization.type")}</TableHead><TableHead>{t("costUtilization.department")}</TableHead><TableHead>{t("costUtilization.vendor")}</TableHead><TableHead>{t("costUtilization.capacity")}</TableHead><TableHead>{t("costUtilization.plannedWork")}</TableHead><TableHead>{t("costUtilization.actualWork")}</TableHead><TableHead>{t("costUtilization.plannedUtilization")}</TableHead><TableHead>{t("costUtilization.actualUtilization")}</TableHead><TableHead>{t("costUtilization.excess")}</TableHead><TableHead>{t("costUtilization.overtime")}</TableHead>{report.canViewCosts ? <TableHead>{t("costUtilization.actualCost")}</TableHead> : null}</TableRow></TableHeader>
          <TableBody>{report.resources.map((resource) => <TableRow key={`${resource.resourceId}-${resource.metrics.currency ?? ""}`}>
            <TableCell className="font-medium">{resource.resourceName}<span className="ms-2 text-xs text-muted-foreground">{resource.metrics.currency ?? t("costUtilization.na")}</span></TableCell>
            <TableCell>{resource.resourceType}</TableCell><TableCell>{resource.departmentName ?? t("costUtilization.na")}</TableCell><TableCell>{resource.vendorName ?? t("costUtilization.na")}</TableCell>
            <TableCell className="tabular-nums">{hours(resource.metrics.capacityHours, locale)}</TableCell><TableCell className="tabular-nums">{hours(resource.metrics.plannedHours, locale)}</TableCell><TableCell className="tabular-nums">{hours(resource.metrics.actualHours, locale)}</TableCell>
            <TableCell className="tabular-nums">{percent(resource.metrics.plannedUtilization, t("costUtilization.na"), locale)}</TableCell><TableCell className="tabular-nums">{percent(resource.metrics.actualUtilization, t("costUtilization.na"), locale)}</TableCell>
            <TableCell className="tabular-nums">{hours(resource.metrics.excessHours, locale)}</TableCell><TableCell className="tabular-nums">{hours(resource.metrics.overtimeHours, locale)}</TableCell>
            {report.canViewCosts ? <TableCell className="tabular-nums">{money(resource.metrics.actualLaborCost, resource.metrics.currency, locale, t("costUtilization.na"))}</TableCell> : null}
          </TableRow>)}</TableBody>
        </Table>
      </CardContent></Card>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card><CardHeader><CardTitle>{t("costUtilization.projects")}</CardTitle></CardHeader><CardContent><AttributionTable rows={report.projects} t={t} locale={locale} canViewCosts={report.canViewCosts} /></CardContent></Card>
        <Card><CardHeader><CardTitle>{t("costUtilization.vendor")}</CardTitle></CardHeader><CardContent><AttributionTable rows={report.vendors} t={t} locale={locale} canViewCosts={report.canViewCosts} /></CardContent></Card>
        <Card><CardHeader><CardTitle>{t("costUtilization.roles")}</CardTitle></CardHeader><CardContent><AttributionTable rows={report.roles} t={t} locale={locale} canViewCosts={report.canViewCosts} /></CardContent></Card>
      </div>

      <Card><CardHeader><CardTitle>{t("costUtilization.daily")}</CardTitle></CardHeader><CardContent>
        <Table><TableHeader><TableRow><TableHead>{t("costUtilization.from")}</TableHead><TableHead>{t("costUtilization.resource")}</TableHead><TableHead>{t("costUtilization.capacity")}</TableHead><TableHead>{t("costUtilization.plannedWork")}</TableHead><TableHead>{t("costUtilization.actualWork")}</TableHead><TableHead>{t("costUtilization.excess")}</TableHead><TableHead>{t("costUtilization.overtime")}</TableHead></TableRow></TableHeader>
          <TableBody>{report.daily.slice(0, 60).map((item) => <TableRow key={`${item.resourceId}-${item.workDate}`}><TableCell>{item.workDate.slice(0, 10)}</TableCell><TableCell>{item.resourceName}</TableCell><TableCell className="tabular-nums">{hours(item.capacityHours, locale)}</TableCell><TableCell className="tabular-nums">{hours(item.plannedHours, locale)}</TableCell><TableCell className="tabular-nums">{hours(item.actualHours, locale)}</TableCell><TableCell className="tabular-nums">{hours(item.excessHours, locale)}</TableCell><TableCell className="tabular-nums">{hours(item.overtimeHours, locale)}</TableCell></TableRow>)}</TableBody>
        </Table>
      </CardContent></Card>
    </> : null}
  </div>;
}
