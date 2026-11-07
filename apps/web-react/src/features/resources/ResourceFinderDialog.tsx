import * as React from "react";
import { Crosshair, GitBranch, List, X } from "lucide-react";
import { useI18n } from "@i18n/messages";
import { Button } from "@components/ui/Button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@components/ui/Dialog";
import { Input } from "@components/ui/Input";
import { Progress } from "@components/ui/Progress";
import { SearchableSelect, type SelectOption } from "@components/ui/SearchableSelect";
import { Skeleton } from "@components/ui/Skeleton";
import { getResourceTypeTextClass, getStatusTextClass } from "@lib/status-presentation";
import { useResources, useResourceFinder, useResourceSkills, useResourceReferenceData } from "./api/resources";
import { useVendors } from "../vendors/api/vendors";

type ViewMode = "list" | "gantt";

export function ResourceFinderDialog() {
  const { t, locale } = useI18n();
  const [open, setOpen] = React.useState(false);
  const [role, setRole] = React.useState("");
  const [seniority, setSeniority] = React.useState("");
  const [skillIds, setSkillIds] = React.useState<string[]>([]);
  const [departmentId, setDepartmentId] = React.useState("");
  const [locationId, setLocationId] = React.useState("");
  const [vendorId, setVendorId] = React.useState("");
  const [requiredAllocation, setRequiredAllocation] = React.useState("");
  const [startDate, setStartDate] = React.useState("");
  const [endDate, setEndDate] = React.useState("");
  const [view, setView] = React.useState<ViewMode>("list");

  const skills = useResourceSkills(open);
  const referenceData = useResourceReferenceData(open);
  const vendors = useVendors(undefined, undefined, "active", open);
  const resources = useResources({ pageSize: 200 }, open);
  const query = React.useMemo(() => ({
    ...(role.trim() ? { role: role.trim() } : {}), ...(seniority ? { seniority } : {}), ...(skillIds.length ? { skillIds } : {}),
    ...(departmentId ? { departmentId } : {}), ...(locationId ? { locationId } : {}), ...(vendorId ? { vendorId } : {}),
    ...(requiredAllocation ? { requiredAllocation: Number(requiredAllocation) } : {}), ...(startDate ? { startDate } : {}), ...(endDate ? { endDate } : {}),
  }), [role, seniority, skillIds, departmentId, locationId, vendorId, requiredAllocation, startDate, endDate]);
  const hasFilters = Boolean(role.trim() || seniority || skillIds.length || departmentId || locationId || vendorId || requiredAllocation);
  const dateRangeValid = !startDate || !endDate || endDate >= startDate;
  const results = useResourceFinder(query, open && hasFilters && dateRangeValid);

  const departmentOptions = React.useMemo(() => {
    const options = new Map<string, string>();
    for (const item of resources.data?.items ?? []) if (item.departmentId && item.departmentName) options.set(item.departmentId, item.departmentName);
    return Array.from(options, ([value, label]) => ({ value, label }));
  }, [resources.data]);
  const locationOptions = React.useMemo(() => {
    const options = new Map<string, string>();
    for (const item of resources.data?.items ?? []) if (item.locationId && item.locationName) options.set(item.locationId, item.locationName);
    return Array.from(options, ([value, label]) => ({ value, label }));
  }, [resources.data]);
  const vendorOptions = (vendors.data?.items ?? []).map((vendor) => ({ value: vendor.id, label: vendor.name }));
  const seniorityOptions: SelectOption[] = (referenceData.data?.seniorityLevels ?? []).map((level) => ({ value: level.code, label: locale === "ar" ? level.nameAr : level.nameEn }));

  const reset = () => { setRole(""); setSeniority(""); setSkillIds([]); setDepartmentId(""); setLocationId(""); setVendorId(""); setRequiredAllocation(""); setStartDate(""); setEndDate(""); setView("list"); };

  return <>
    <Button type="button" variant="outline" size="sm" onClick={() => setOpen(true)}><Crosshair className="me-2 h-4 w-4" />{t("resources.finder.findResource")}</Button>
    <Dialog open={open} onOpenChange={setOpen} ariaLabel={t("resources.finder.title")}>
      <DialogContent className="max-w-5xl max-h-[90vh] overflow-y-auto">
        <DialogHeader><DialogTitle>{t("resources.finder.title")}</DialogTitle><DialogDescription>{t("resources.finder.enterCriteria")}</DialogDescription></DialogHeader>
        <div className="space-y-4">
          <div className="grid gap-3 md:grid-cols-3">
            <Input aria-label={t("resources.finder.role")} placeholder={t("resources.finder.searchPlaceholder")} value={role} onChange={(event) => setRole(event.target.value)} />
            <SearchableSelect aria-label={t("resources.finder.seniority")} options={seniorityOptions} value={seniority} onValueChange={setSeniority} placeholder={t("resources.finder.seniorityPlaceholder")} searchPlaceholder={t("resources.finder.seniorityPlaceholder")} />
            <SearchableSelect aria-label={t("resources.finder.department")} options={departmentOptions} value={departmentId} onValueChange={setDepartmentId} placeholder={t("resources.finder.selectDepartment")} searchPlaceholder={t("resources.finder.selectDepartment")} />
            <SearchableSelect aria-label={t("resources.finder.location")} options={locationOptions} value={locationId} onValueChange={setLocationId} placeholder={t("resources.finder.selectLocation")} searchPlaceholder={t("resources.finder.selectLocation")} />
            <SearchableSelect aria-label={t("resources.finder.vendor")} options={vendorOptions} value={vendorId} onValueChange={setVendorId} placeholder={t("resources.finder.selectVendor")} searchPlaceholder={t("resources.finder.selectVendor")} />
            <Input aria-label={t("resources.finder.allocation")} type="number" min="1" max="100" placeholder={t("resources.finder.allocPlaceholder")} value={requiredAllocation} onChange={(event) => setRequiredAllocation(event.target.value)} />
          </div>
          <fieldset className="rounded-md border p-3">
            <legend className="px-1 text-sm font-medium">{t("resources.finder.skills")}</legend>
            <div className="grid max-h-28 gap-2 overflow-auto sm:grid-cols-2 lg:grid-cols-3">{skills.data?.map((skill) => <label key={skill.id} className="flex items-center gap-2 text-sm"><input type="checkbox" checked={skillIds.includes(skill.id)} onChange={(event) => setSkillIds((current) => event.target.checked ? [...current, skill.id] : current.filter((id) => id !== skill.id))} />{skill.name}{skill.category ? <span className="text-xs text-muted-foreground">· {skill.category}</span> : null}</label>)}</div>
          </fieldset>
          <div className="flex flex-wrap items-center gap-3">
            <label className="flex items-center gap-2 text-sm"><span>{t("resources.finder.startDate")}</span><Input aria-label={t("resources.finder.startDate")} type="date" value={startDate} onChange={(event) => setStartDate(event.target.value)} /></label>
            <label className="flex items-center gap-2 text-sm"><span>{t("resources.finder.endDate")}</span><Input aria-label={t("resources.finder.endDate")} type="date" value={endDate} onChange={(event) => setEndDate(event.target.value)} /></label>
            <div className="ms-auto flex items-center gap-1 rounded-md border p-1">
              <Button type="button" variant={view === "list" ? "default" : "ghost"} size="sm" aria-label={t("resources.finder.listView")} onClick={() => setView("list")}><List className="h-4 w-4" /></Button>
              <Button type="button" variant={view === "gantt" ? "default" : "ghost"} size="sm" aria-label={t("resources.finder.availability")} disabled={!startDate || !endDate || !dateRangeValid} onClick={() => setView("gantt")}><GitBranch className="h-4 w-4" /></Button>
            </div>
            <Button type="button" variant="ghost" size="sm" onClick={reset}><X className="me-1 h-4 w-4" />{t("resources.finder.clearFilters")}</Button>
          </div>
          {!dateRangeValid ? <p role="alert" className="text-sm text-destructive">{t("resources.finder.invalidDateRange")}</p> : null}
          {!hasFilters ? <p className="py-5 text-center text-sm text-muted-foreground">{t("resources.finder.enterCriteria")}</p> : null}
          {results.isLoading ? <div className="space-y-2">{[1, 2, 3].map((index) => <Skeleton key={index} className="h-16 w-full" />)}</div> : null}
          {results.isError ? <p role="alert" className="text-sm text-destructive">{t("resources.finder.failedToSearch")}: {results.error instanceof Error ? results.error.message : ""}</p> : null}
          {results.data && hasFilters && dateRangeValid ? <div className="space-y-2">
            <p className="text-xs text-muted-foreground">{results.data.totalCount} {t("resources.finder.results")}</p>
            {results.data.items.length === 0 ? <p className="py-6 text-center text-sm text-muted-foreground">{t("resources.finder.noMatches")}</p> : view === "gantt" ? <AvailabilityChart items={results.data.items} start={startDate} end={endDate} t={t} /> : <div className="max-h-[38vh] space-y-2 overflow-auto">{results.data.items.map((item) => <article key={item.resourceId} className="flex flex-col gap-3 rounded-md border p-3 md:flex-row md:items-center">
              <div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{item.resourceName}</span><span className="text-xs font-medium text-info">{Math.round(item.matchScore)}% {t("resources.finder.matchScore")}</span><span className={`text-xs font-medium ${getResourceTypeTextClass(item.resourceType)}`}>{referenceData.data?.resourceTypes.find((type) => type.code === item.resourceType)?.[locale === "ar" ? "nameAr" : "nameEn"] ?? item.resourceType}</span></div><p className="text-xs text-muted-foreground">{item.jobTitle || t("resources.directory.notProvided")} · {item.departmentName || t("resources.directory.notProvided")} {item.seniority ? `· ${referenceData.data?.seniorityLevels.find((level) => level.code === item.seniority)?.[locale === "ar" ? "nameAr" : "nameEn"] ?? item.seniority}` : ""}</p>
                {item.skillMatches.length ? <p className="mt-1 text-xs">{item.skillMatches.map((skill) => <span key={skill.skillName} className={`me-2 font-medium ${skill.isMatch ? "text-success" : "text-muted-foreground"}`}>{skill.isMatch ? "✓" : "—"} {skill.skillName}</span>)}</p> : null}
                {item.conflictingAllocations.length ? <p className="mt-1 text-xs text-muted-foreground">{item.conflictingAllocations.map((conflict) => `${conflict.projectName} (${conflict.allocationPercentage}%)`).join(" · ")}</p> : null}
              </div><div className="w-full space-y-1 md:w-40"><div className="flex items-center gap-2"><span className="text-xs text-muted-foreground">{t("resources.finder.load")}</span><Progress value={item.hasCapacityData ? item.currentLoadPercent : 0} tone={item.currentLoadPercent > 80 ? "warning" : "success"} className="h-1.5" /><span className={`text-xs font-medium ${getStatusTextClass(item.currentLoadPercent > 80 ? "High" : "Active")}`}>{item.hasCapacityData ? `${Math.round(item.currentLoadPercent)}%` : "—"}</span></div><p className="text-xs text-muted-foreground">{item.hasCapacityData ? `${Math.round(item.availableCapacityHours)} ${t("resources.finder.hoursAvailable")}` : t("resources.finder.capacityUnavailable")}{item.earliestAvailableDate ? ` · ${t("resources.finder.from")} ${item.earliestAvailableDate.slice(0, 10)}` : ""}</p></div>
            </article>)}</div>}
          </div> : null}
        </div>
        <DialogFooter><Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("resources.directory.close")}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  </>;
}

function AvailabilityChart({ items, start, end, t }: { items: NonNullable<ReturnType<typeof useResourceFinder>["data"]>["items"]; start: string; end: string; t: (key: string) => string }) {
  const startTime = new Date(start).getTime();
  const endTime = new Date(end).getTime();
  const total = Math.max(1, endTime - startTime);
  return <div className="max-h-[38vh] space-y-2 overflow-auto">{items.map((item) => <div key={item.resourceId} className="flex items-center gap-3 rounded-md border p-2"><span className="w-36 shrink-0 truncate text-sm font-medium" title={item.resourceName}>{item.resourceName}</span><div className="relative h-6 flex-1 rounded bg-muted/50">{item.conflictingAllocations.map((allocation) => { const left = Math.max(0, (new Date(allocation.startDate).getTime() - startTime) / total * 100); const right = Math.min(100, (new Date(allocation.endDate).getTime() - startTime) / total * 100); if (right <= left) return null; return <div key={allocation.allocationId} className="absolute inset-y-1 rounded bg-primary/60" style={{ left: `${left}%`, width: `${right - left}%` }} title={`${allocation.projectName} (${allocation.allocationPercentage}%)`} />; })}</div><span className="w-20 text-end text-xs text-muted-foreground">{item.hasCapacityData ? `${Math.round(item.availableCapacityHours)} ${t("resources.finder.hoursAvailable")}` : t("resources.finder.capacityUnavailable")}</span></div>)}</div>;
}
