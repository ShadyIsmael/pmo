"use client";

import { useState, useMemo, useCallback } from "react";
import { useTranslations } from "next-intl";
import { Dialog, DialogContent, DialogHeader, DialogBody, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import { Search, Crosshair, List, GitBranch, X } from "lucide-react";
import { getInitials, formatDate } from "@/lib/utils";
import { useResourceFinder, ResourceFinderResultDto } from "@/hooks/resources/useResourceFinder";
import { useSkills } from "@/hooks/resources/useSkills";
import { useVendors } from "@/hooks/resources/useVendors";
import { useResources } from "@/hooks/resources/useResources";
import { SearchableSelect, SearchableMultiSelect, type SelectOption } from "@/components/ui/searchable-select";

const BAR_HEIGHT = 6;
const ROW_HEIGHT = 64;
const STATUS_COLORS: Record<string, string> = {
  Confirmed: "bg-blue-400",
  Proposed: "bg-amber-300",
  Tentative: "bg-purple-300",
  Cancelled: "bg-gray-300",
};

function ResourceAvailabilityGantt({ items, startDate, endDate }: { items: ResourceFinderResultDto[]; startDate: string; endDate: string }) {
  const sd = new Date(startDate);
  const ed = new Date(endDate);
  const totalDays = Math.max(1, (ed.getTime() - sd.getTime()) / (1000 * 60 * 60 * 24));

  return (
    <div className="space-y-1">
      {items.map((r) => {
        const allocs = r.conflictingAllocations;
        return (
          <div key={r.resourceId} className="flex items-center gap-3 rounded-md border p-2">
            <div className="w-36 flex-shrink-0">
              <div className="flex items-center gap-1.5">
                <Avatar className="h-5 w-5">
                  <AvatarFallback className="text-[6px]">{getInitials(r.resourceName)}</AvatarFallback>
                </Avatar>
                <span className="truncate text-xs font-medium">{r.resourceName}</span>
              </div>
              <div className="text-[10px] text-muted-foreground ml-6">{Math.round(r.matchScore)}% match</div>
            </div>
            <div className="relative flex-1 h-6">
              <div className="absolute inset-x-0 bottom-0 h-1.5 rounded-full bg-muted" />
              {allocs.map((a) => {
                const aStart = new Date(a.startDate);
                const aEnd = new Date(a.endDate);
                const left = Math.max(0, ((aStart.getTime() - sd.getTime()) / (1000 * 60 * 60 * 24) / totalDays) * 100);
                const width = Math.min(100 - left, Math.max(0.5, ((aEnd.getTime() - aStart.getTime()) / (1000 * 60 * 60 * 24) / totalDays) * 100));
                return (
                  <div
                    key={a.allocationId}
                    className={`absolute bottom-0 h-1.5 rounded-full ${STATUS_COLORS[a.role] || "bg-blue-400"}`}
                    style={{ left: `${left}%`, width: `${width}%` }}
                    title={`${a.projectName} (${a.allocationPercentage}%)`}
                  />
                );
              })}
              <div className="absolute bottom-0 w-px h-3 bg-red-400" style={{ left: `${((Date.now() - sd.getTime()) / (1000 * 60 * 60 * 24) / totalDays) * 100}%` }} />
            </div>
            <div className="w-20 flex-shrink-0 text-right text-[10px] text-muted-foreground">
              {Math.round(r.availableCapacityHours)} hrs
            </div>
          </div>
        );
      })}
    </div>
  );
}

export function ResourceFinderDialog() {
  const t = useTranslations("resources.finder");
  const tc = useTranslations("common");
  const [open, setOpen] = useState(false);
  const [role, setRole] = useState("");
  const [seniority, setSeniority] = useState("");
  const [skillIds, setSkillIds] = useState<string[]>([]);
  const [departmentId, setDepartmentId] = useState("");
  const [locationId, setLocationId] = useState("");
  const [vendorId, setVendorId] = useState("");
  const [requiredAllocation, setRequiredAllocation] = useState("");
  const [finderStartDate, setFinderStartDate] = useState("");
  const [finderEndDate, setFinderEndDate] = useState("");
  const [viewMode, setViewMode] = useState<"list" | "gantt">("list");

  const { data: skillsData } = useSkills();
  const { data: vendorsData } = useVendors();
  const { data: resourcesData } = useResources({ pageSize: 200 });

  const skillOptions: SelectOption[] = useMemo(
    () => (skillsData ?? []).map((s) => ({ value: s.id, label: s.name })),
    [skillsData]
  );

  const vendorOptions: SelectOption[] = useMemo(
    () => (vendorsData ?? []).map((v) => ({ value: v.id, label: v.name })),
    [vendorsData]
  );

  const departmentOptions: SelectOption[] = useMemo(() => {
    const names = new Map<string, string>();
    for (const r of resourcesData?.items ?? []) {
      if (r.departmentId && r.departmentName) {
        names.set(r.departmentId, r.departmentName);
      }
    }
    return Array.from(names.entries()).map(([id, name]) => ({ value: id, label: name }));
  }, [resourcesData]);

  const locationOptions: SelectOption[] = useMemo(() => {
    const names = new Map<string, string>();
    for (const r of resourcesData?.items ?? []) {
      if (r.locationId && r.locationName) {
        names.set(r.locationId, r.locationName);
      }
    }
    return Array.from(names.entries()).map(([id, name]) => ({ value: id, label: name }));
  }, [resourcesData]);

  const seniorityOptions: SelectOption[] = useMemo(
    () => [
      { value: "Junior", label: "Junior" },
      { value: "Mid", label: "Mid" },
      { value: "Senior", label: "Senior" },
      { value: "Lead", label: "Lead" },
      { value: "Principal", label: "Principal" },
    ],
    []
  );

  const query = {
    ...(role ? { role } : {}),
    ...(seniority ? { seniority } : {}),
    ...(skillIds.length > 0 ? { skillIds } : {}),
    ...(departmentId ? { departmentId } : {}),
    ...(locationId ? { locationId } : {}),
    ...(vendorId ? { vendorId } : {}),
    ...(requiredAllocation ? { requiredAllocation: Number(requiredAllocation) } : {}),
    ...(finderStartDate ? { startDate: finderStartDate } : {}),
    ...(finderEndDate ? { endDate: finderEndDate } : {}),
  };

  const hasFilters = !!(role || seniority || skillIds.length > 0 || departmentId || locationId || vendorId || requiredAllocation);
  const { data, isLoading, error } = useResourceFinder(query, open && hasFilters);

  const activeFilters = useMemo(() => {
    const filters: { key: string; label: string }[] = [];
    if (role) filters.push({ key: "role", label: `${t("role")}: ${role}` });
    if (seniority) filters.push({ key: "seniority", label: `${t("seniority")}: ${seniority}` });
    if (skillIds.length > 0) {
      const names = skillOptions.filter((s) => skillIds.includes(s.value)).map((s) => s.label).join(", ");
      filters.push({ key: "skills", label: `${t("skills")}: ${names}` });
    }
    if (departmentId) {
      const dept = departmentOptions.find((d) => d.value === departmentId);
      if (dept) filters.push({ key: "department", label: `${t("department")}: ${dept.label}` });
    }
    if (locationId) {
      const loc = locationOptions.find((l) => l.value === locationId);
      if (loc) filters.push({ key: "location", label: `${t("location")}: ${loc.label}` });
    }
    if (vendorId) {
      const ven = vendorOptions.find((v) => v.value === vendorId);
      if (ven) filters.push({ key: "vendor", label: `${t("vendor")}: ${ven.label}` });
    }
    return filters;
  }, [role, seniority, skillIds, departmentId, locationId, vendorId, skillOptions, departmentOptions, locationOptions, vendorOptions, t]);

  const clearFilter = useCallback((key: string) => {
    switch (key) {
      case "role": setRole(""); break;
      case "seniority": setSeniority(""); break;
      case "skills": setSkillIds([]); break;
      case "department": setDepartmentId(""); break;
      case "location": setLocationId(""); break;
      case "vendor": setVendorId(""); break;
    }
  }, []);

  const clearAllFilters = useCallback(() => {
    setRole("");
    setSeniority("");
    setSkillIds([]);
    setDepartmentId("");
    setLocationId("");
    setVendorId("");
    setRequiredAllocation("");
    setFinderStartDate("");
    setFinderEndDate("");
  }, []);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="outline" size="sm">
          <Crosshair className="mr-2 h-4 w-4" />{t("findResource")}
        </Button>
      </DialogTrigger>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>{t("title")}</DialogTitle>
        </DialogHeader>

        <DialogBody className="flex flex-col gap-3">
          <div className="flex flex-col gap-3">
          <div className="flex items-center gap-2 flex-wrap">
            <div className="relative flex-1 min-w-[160px]">
              <SearchableSelect
                options={[]}
                value={role}
                onValueChange={(val) => setRole(val)}
                placeholder={t("searchPlaceholder")}
                searchPlaceholder={t("searchPlaceholder")}
                className="pl-8"
              />
              <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground pointer-events-none" />
            </div>
            <SearchableSelect
              options={seniorityOptions}
              value={seniority}
              onValueChange={(val) => setSeniority(val)}
              placeholder={t("seniorityPlaceholder")}
              searchPlaceholder={t("seniorityPlaceholder")}
              className="w-28"
            />
            <SearchableMultiSelect
              options={skillOptions}
              values={skillIds}
              onSelect={(vals) => setSkillIds(vals)}
              placeholder={t("selectSkills")}
              searchPlaceholder={t("selectSkills")}
              className="w-40"
            />
            <SearchableSelect
              options={departmentOptions}
              value={departmentId}
              onValueChange={(val) => setDepartmentId(val)}
              placeholder={t("selectDepartment")}
              searchPlaceholder={t("selectDepartment")}
              className="w-36"
            />
            <SearchableSelect
              options={locationOptions}
              value={locationId}
              onValueChange={(val) => setLocationId(val)}
              placeholder={t("selectLocation")}
              searchPlaceholder={t("selectLocation")}
              className="w-32"
            />
            <SearchableSelect
              options={vendorOptions}
              value={vendorId}
              onValueChange={(val) => setVendorId(val)}
              placeholder={t("selectVendor")}
              searchPlaceholder={t("selectVendor")}
              className="w-32"
            />
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            <Input placeholder={t("allocPlaceholder")} type="number" className="w-20" value={requiredAllocation} onChange={(e) => setRequiredAllocation(e.target.value)} />
            <Input type="date" className="w-36" value={finderStartDate} onChange={(e) => setFinderStartDate(e.target.value)} placeholder={t("startPlaceholder")} />
            <Input type="date" className="w-36" value={finderEndDate} onChange={(e) => setFinderEndDate(e.target.value)} placeholder={t("endPlaceholder")} />
            <div className="flex items-center rounded-md border p-0.5">
              <Button variant={viewMode === "list" ? "default" : "ghost"} size="sm" className="h-7 px-2" onClick={() => setViewMode("list")}>
                <List className="h-3.5 w-3.5" />
              </Button>
              <Button variant={viewMode === "gantt" ? "default" : "ghost"} size="sm" className="h-7 px-2" onClick={() => setViewMode("gantt")}>
                <GitBranch className="h-3.5 w-3.5" />
              </Button>
            </div>
          </div>
        </div>

        {activeFilters.length > 0 && (
          <div className="flex flex-wrap items-center gap-1.5 px-1">
            {activeFilters.map((f) => (
              <Badge key={f.key} variant="secondary" className="gap-1 text-[10px]">
                {f.label}
                <X className="h-3 w-3 cursor-pointer" onClick={() => clearFilter(f.key)} />
              </Badge>
            ))}
            {activeFilters.length > 1 && (
              <button className="text-[10px] text-muted-foreground underline hover:text-foreground" onClick={clearAllFilters}>
                Clear all
              </button>
            )}
          </div>
        )}

        {!hasFilters && (
          <div className="flex flex-col items-center justify-center py-12 text-center text-sm text-muted-foreground">
            <Crosshair className="mb-2 h-8 w-8" />
            <p>{t("enterCriteria")}</p>
          </div>
        )}

        {isLoading && (
          <div className="space-y-2">
            {Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-16 w-full" />
            ))}
          </div>
        )}

        {error && (
          <div className="rounded-md border bg-destructive/10 p-4 text-sm text-destructive">{t("failedToSearch")}</div>
        )}

        {data && hasFilters && (
          <div className="space-y-2">
            <div className="flex items-center justify-between text-xs text-muted-foreground">
              <span>{data.totalCount} {t("results")}</span>
              {finderStartDate && finderEndDate && (
                <span>{formatDate(finderStartDate)} — {formatDate(finderEndDate)}</span>
              )}
            </div>

            {data.items.length === 0 ? (
              <div className="py-8 text-center text-sm text-muted-foreground">{t("noMatches")}</div>
            ) : viewMode === "gantt" && finderStartDate && finderEndDate ? (
              <ResourceAvailabilityGantt items={data.items} startDate={finderStartDate} endDate={finderEndDate} />
            ) : (
              <div className="space-y-2">
                {data.items.map((r) => (
                  <div key={r.resourceId} className="flex items-center gap-4 rounded-md border p-3 hover:bg-muted/30">
                    <Avatar className="h-8 w-8">
                      <AvatarFallback className="text-xs">{getInitials(r.resourceName)}</AvatarFallback>
                    </Avatar>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="font-medium">{r.resourceName}</span>
                        <Badge variant="secondary" className="text-[10px]">{Math.round(r.matchScore)}% {t("matchScore")}</Badge>
                        <Badge variant="outline" className="text-[10px]">{r.resourceType}</Badge>
                      </div>
                      <div className="flex items-center gap-3 text-xs text-muted-foreground mt-0.5">
                        <span>{r.jobTitle ?? "-"}</span>
                        <span>{r.departmentName ?? "-"}</span>
                        {r.seniority && <span>{r.seniority}</span>}
                      </div>
                      {r.skillMatches.length > 0 && (
                        <div className="flex flex-wrap gap-1 mt-1">
                          {r.skillMatches.map((s, i) => (
                            <Badge key={i} variant={s.isMatch ? "default" : "secondary"} className="text-[10px]">
                              {s.skillName} {s.isMatch ? "✓" : "✗"}
                            </Badge>
                          ))}
                        </div>
                      )}
                      {r.conflictingAllocations.length > 0 && (
                        <div className="flex flex-wrap gap-1 mt-1">
                          {r.conflictingAllocations.map((c, i) => (
                            <Badge key={i} variant="outline" className="text-[10px] border-status-amber/30">
                              {c.projectName} ({c.allocationPercentage}%)
                            </Badge>
                          ))}
                        </div>
                      )}
                    </div>
                    <div className="flex flex-col items-end gap-1 text-xs">
                      <div className="flex items-center gap-1">
                        <span className="text-muted-foreground">{t("load")}:</span>
                        <Progress value={Math.min(r.currentLoadPercent, 100)} className="h-1.5 w-12" />
                        <span className={r.currentLoadPercent > 80 ? "text-status-red" : "text-status-green"}>
                          {Math.round(r.currentLoadPercent)}%
                        </span>
                      </div>
                      <span className="text-muted-foreground">{Math.round(r.availableCapacityHours)} {t("hoursAvailable")}</span>
                      {r.earliestAvailableDate && (
                        <span className="text-[10px] text-muted-foreground">{t("from")} {formatDate(r.earliestAvailableDate)}</span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
        </DialogBody>
      </DialogContent>
    </Dialog>
  );
}
