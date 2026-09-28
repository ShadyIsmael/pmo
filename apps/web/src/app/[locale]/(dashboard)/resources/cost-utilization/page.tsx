"use client";

import { useMemo, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { AlertTriangle, BarChart3, Clock3, Coins, Gauge, UsersRound, TrendingUp } from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { useCostUtilization, type CostUtilizationAttribution, type CostUtilizationFilter, type CostUtilizationMetric } from "@/hooks/resources/useCostUtilization";

const dateValue = (date: Date) => date.toISOString().slice(0, 10);
const initialFrom = dateValue(new Date(Date.now() - 29 * 86_400_000));
const initialTo = dateValue(new Date());

function hours(value: number) { return `${value.toLocaleString(undefined, { maximumFractionDigits: 1 })}h`; }
function percent(value: number | undefined, na: string) { return value == null ? na : `${value.toLocaleString(undefined, { maximumFractionDigits: 1 })}%`; }
function money(value: number | undefined, currency: string | undefined, locale: string, na: string) {
  if (value == null || !currency) return na;
  try { return new Intl.NumberFormat(locale, { style: "currency", currency, maximumFractionDigits: 2 }).format(value); } catch { return `${value.toLocaleString(locale, { maximumFractionDigits: 2 })} ${currency}`; }
}

function Kpi({ label, value, icon: Icon }: { label: string; value: string; icon: typeof Gauge }) {
  return <Card><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle>{label}</CardTitle><Icon className="h-4 w-4 text-muted-foreground" /></CardHeader><CardContent><div className="text-2xl font-semibold tracking-tight">{value}</div></CardContent></Card>;
}

function CostLine({ metric, t, locale }: { metric: CostUtilizationMetric; t: ReturnType<typeof useTranslations>; locale: string }) {
  return <div className="grid grid-cols-2 gap-3 md:grid-cols-6 text-sm">
    <div><div className="text-xs text-muted-foreground">{t("plannedCost")}</div><div>{money(metric.plannedLaborCost, metric.currency, locale, t("na"))}</div></div>
    <div><div className="text-xs text-muted-foreground">{t("actualCost")}</div><div>{money(metric.actualLaborCost, metric.currency, locale, t("na"))}</div></div>
    <div><div className="text-xs text-muted-foreground">{t("variance")}</div><div>{money(metric.laborCostVariance, metric.currency, locale, t("na"))}</div></div>
    <div><div className="text-xs text-muted-foreground">{t("overtimeCost")}</div><div>{money(metric.overtimeCost, metric.currency, locale, t("na"))}</div></div>
    <div><div className="text-xs text-muted-foreground">{t("unusedCost")}</div><div>{money(metric.unusedCapacityCost, metric.currency, locale, t("na"))}</div></div>
    <div><div className="text-xs text-muted-foreground">{t("totalCost")}</div><div className="font-semibold">{money(metric.totalResourceCost, metric.currency, locale, t("na"))}</div></div>
  </div>;
}

function AttributionTable({ rows, t, locale }: { rows: CostUtilizationAttribution[]; t: ReturnType<typeof useTranslations>; locale: string }) {
  return <div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-start text-xs text-muted-foreground"><th className="px-3 py-2 text-start">{t("resource")}</th><th className="px-3 py-2 text-start">{t("plannedWork")}</th><th className="px-3 py-2 text-start">{t("actualWork")}</th><th className="px-3 py-2 text-start">{t("actualCost")}</th><th className="px-3 py-2 text-start">{t("overtimeCost")}</th></tr></thead><tbody>{rows.slice(0, 20).map((row) => <tr key={`${row.id ?? "unassigned"}-${row.currency ?? ""}`} className="border-b last:border-0"><td className="px-3 py-2 font-medium">{row.name}<span className="ms-2 text-xs text-muted-foreground">{row.currency}</span></td><td className="px-3 py-2">{hours(row.plannedHours)}</td><td className="px-3 py-2">{hours(row.actualHours)}</td><td className="px-3 py-2">{money(row.actualLaborCost, row.currency, locale, t("na"))}</td><td className="px-3 py-2">{money(row.overtimeCost, row.currency, locale, t("na"))}</td></tr>)}</tbody></table></div>;
}

export default function CostUtilizationPage() {
  const t = useTranslations("costUtilization");
  const locale = useLocale();
  const [filters, setFilters] = useState<CostUtilizationFilter>({ from: initialFrom, to: initialTo });
  const { data, isLoading, isError } = useCostUtilization(filters);
  const resourceOptions = useMemo(() => data?.resources.filter((row, index, all) => all.findIndex(item => item.resourceId === row.resourceId) === index) ?? [], [data?.resources]);
  const vendorOptions = useMemo(() => data?.vendors.filter(row => row.id) ?? [], [data?.vendors]);
  const projectOptions = useMemo(() => data?.projects.filter(row => row.id) ?? [], [data?.projects]);
  const roleOptions = useMemo(() => data?.roles.filter(row => row.id) ?? [], [data?.roles]);
  const departmentOptions = useMemo(() => data?.resources
    .filter(row => row.departmentName)
    .map(row => ({ value: row.departmentName!, label: row.departmentName! }))
    .filter((opt, idx, arr) => arr.findIndex(o => o.value === opt.value) === idx) ?? [], [data?.resources]);
  const update = (key: keyof CostUtilizationFilter, value: string) => setFilters(current => ({ ...current, [key]: value === "all" || value === "" ? undefined : value }));
  const metric = data?.operational;

  const overAllocated = data ? data.resources.filter(r => (r.metrics.actualUtilization ?? 0) > 80).length : 0;
  const underUtilized = data ? data.resources.filter(r => (r.metrics.actualUtilization ?? 0) > 0 && (r.metrics.actualUtilization ?? 0) < 40).length : 0;

  return <div className="space-y-6">
    <PageHeader title={t("title")} description={t("description")} />
    <Card><CardContent className="grid gap-3 pt-4 md:grid-cols-3 lg:grid-cols-7">
      <label className="space-y-1 text-xs text-muted-foreground"><span>{t("from")}</span><Input type="date" value={filters.from ?? ""} onChange={event => update("from", event.target.value)} /></label>
      <label className="space-y-1 text-xs text-muted-foreground"><span>{t("to")}</span><Input type="date" value={filters.to ?? ""} onChange={event => update("to", event.target.value)} /></label>
      <FilterSelect value={filters.resourceId} placeholder={t("allResources")} options={resourceOptions.map(row => ({ value: row.resourceId, label: row.resourceName }))} onChange={value => update("resourceId", value)} />
      <FilterSelect value={filters.projectId} placeholder={t("allProjects")} options={projectOptions.map(row => ({ value: row.id!, label: row.name }))} onChange={value => update("projectId", value)} />
      <FilterSelect value={filters.vendorId} placeholder={t("allVendors")} options={vendorOptions.map(row => ({ value: row.id!, label: row.name }))} onChange={value => update("vendorId", value)} />
      <FilterSelect value={filters.projectRoleId} placeholder={t("allRoles")} options={roleOptions.map(row => ({ value: row.id!, label: row.name }))} onChange={value => update("projectRoleId", value)} />
      <FilterSelect value={filters.departmentId} placeholder={t("allDepartments")} options={departmentOptions} onChange={value => update("departmentId", value)} />
    </CardContent></Card>

    {isError ? <Card><CardContent className="flex items-center gap-2 pt-6 text-sm text-destructive"><AlertTriangle className="h-4 w-4" />{t("noData")}</CardContent></Card> : null}
    {isLoading ? <div className="grid gap-4 md:grid-cols-3"><Skeleton className="h-28" /><Skeleton className="h-28" /><Skeleton className="h-28" /></div> : null}
    {data && metric ? <>
      <div className="grid gap-4 md:grid-cols-3 lg:grid-cols-7">
        <Kpi label={t("capacity")} value={hours(metric.capacityHours)} icon={Gauge} />
        <Kpi label={t("plannedWork")} value={hours(metric.plannedHours)} icon={BarChart3} />
        <Kpi label={t("actualWork")} value={hours(metric.actualHours)} icon={Clock3} />
        <Kpi label={t("plannedUtilization")} value={percent(metric.plannedUtilization, t("na"))} icon={Gauge} />
        <Kpi label={t("actualUtilization")} value={percent(metric.actualUtilization, t("na"))} icon={UsersRound} />
        <Kpi label={t("overAllocated")} value={overAllocated.toString()} icon={TrendingUp} />
        <Kpi label={t("underUtilized")} value={underUtilized.toString()} icon={TrendingUp} />
      </div>
      {!data.canViewCosts ? <Card><CardContent className="flex items-center gap-2 pt-6 text-sm text-muted-foreground"><Coins className="h-4 w-4" />{t("costPermission")}</CardContent></Card> : null}
      {data.byCurrency.length > 0 ? <Card><CardHeader><CardTitle>{t("currencyCosts")}</CardTitle></CardHeader><CardContent className="space-y-5">{data.byCurrency.map((item) => <div key={item.currency} className="space-y-3"><div className="flex items-center gap-2 font-medium"><span className="rounded bg-primary/10 px-2 py-1 text-xs">{item.currency}</span><span>{hours(item.actualHours)} {t("actualWork")}</span></div><CostLine metric={item} t={t} locale={locale} /></div>)}</CardContent></Card> : null}
      <Card><CardHeader><CardTitle>{t("resourceBreakdown")}</CardTitle></CardHeader><CardContent className="overflow-x-auto"><table className="w-full min-w-[1100px] text-sm"><thead><tr className="border-b text-xs text-muted-foreground"><th className="px-3 py-2 text-start">{t("resource")}</th><th className="px-3 py-2 text-start">{t("type")}</th><th className="px-3 py-2 text-start">{t("department")}</th><th className="px-3 py-2 text-start">{t("vendor")}</th><th className="px-3 py-2 text-start">{t("capacity")}</th><th className="px-3 py-2 text-start">{t("plannedWork")}</th><th className="px-3 py-2 text-start">{t("actualWork")}</th><th className="px-3 py-2 text-start">{t("plannedUtilization")}</th><th className="px-3 py-2 text-start">{t("actualUtilization")}</th><th className="px-3 py-2 text-start">{t("excess")}</th><th className="px-3 py-2 text-start">{t("overtime")}</th><th className="px-3 py-2 text-start">{t("actualCost")}</th></tr></thead><tbody>{data.resources.map(row => <tr key={`${row.resourceId}-${row.metrics.currency}`} className="border-b last:border-0"><td className="px-3 py-2 font-medium">{row.resourceName}<span className="ms-2 text-xs text-muted-foreground">{row.metrics.currency ?? t("na")}</span></td><td className="px-3 py-2">{row.resourceType}</td><td className="px-3 py-2">{row.departmentName ?? t("na")}</td><td className="px-3 py-2">{row.vendorName ?? t("na")}</td><td className="px-3 py-2">{hours(row.metrics.capacityHours)}</td><td className="px-3 py-2">{hours(row.metrics.plannedHours)}</td><td className="px-3 py-2">{hours(row.metrics.actualHours)}</td><td className="px-3 py-2">{percent(row.metrics.plannedUtilization, t("na"))}</td><td className="px-3 py-2">{percent(row.metrics.actualUtilization, t("na"))}</td><td className="px-3 py-2">{hours(row.metrics.excessHours)}</td><td className="px-3 py-2">{hours(row.metrics.overtimeHours)}</td><td className="px-3 py-2">{money(row.metrics.actualLaborCost, row.metrics.currency, locale, t("na"))}</td></tr>)}</tbody></table></CardContent></Card>
      <div className="grid gap-4 lg:grid-cols-3"><Card><CardHeader><CardTitle>{t("projects")}</CardTitle></CardHeader><CardContent><AttributionTable rows={data.projects} t={t} locale={locale} /></CardContent></Card><Card><CardHeader><CardTitle>{t("vendor")}</CardTitle></CardHeader><CardContent><AttributionTable rows={data.vendors} t={t} locale={locale} /></CardContent></Card><Card><CardHeader><CardTitle>{t("roles")}</CardTitle></CardHeader><CardContent><AttributionTable rows={data.roles} t={t} locale={locale} /></CardContent></Card></div>
      <Card><CardHeader><CardTitle>{t("daily")}</CardTitle></CardHeader><CardContent><div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="border-b text-xs text-muted-foreground"><th className="px-3 py-2 text-start">{t("from")}</th><th className="px-3 py-2 text-start">{t("resource")}</th><th className="px-3 py-2 text-start">{t("capacity")}</th><th className="px-3 py-2 text-start">{t("plannedWork")}</th><th className="px-3 py-2 text-start">{t("actualWork")}</th><th className="px-3 py-2 text-start">{t("excess")}</th><th className="px-3 py-2 text-start">{t("overtime")}</th></tr></thead><tbody>{data.daily.slice(0, 60).map(row => <tr key={`${row.resourceId}-${row.workDate}`} className="border-b last:border-0"><td className="px-3 py-2">{row.workDate.slice(0, 10)}</td><td className="px-3 py-2">{row.resourceName}</td><td className="px-3 py-2">{hours(row.capacityHours)}</td><td className="px-3 py-2">{hours(row.plannedHours)}</td><td className="px-3 py-2">{hours(row.actualHours)}</td><td className="px-3 py-2">{hours(row.excessHours)}</td><td className="px-3 py-2">{hours(row.overtimeHours)}</td></tr>)}</tbody></table></div></CardContent></Card>
    </> : null}
  </div>;
}

function FilterSelect({ value, placeholder, options, onChange }: { value?: string; placeholder: string; options: Array<{ value: string; label: string }>; onChange: (_value: string) => void }) {
  return <Select value={value ?? "all"} onValueChange={onChange}><SelectTrigger className="w-full"><SelectValue placeholder={placeholder} /></SelectTrigger><SelectContent><SelectItem value="all">{placeholder}</SelectItem>{options.map(option => <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>)}</SelectContent></Select>;
}
