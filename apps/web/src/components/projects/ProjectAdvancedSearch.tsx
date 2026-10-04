"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { useProjectEnumLabels } from "@/hooks/projects/useProjectEnumLabels";
import { toUserSelectOptions, useUserLookup } from "@/hooks/projects/useProjectLookups";
import { shouldLoadProjectLookup } from "@/hooks/projects/project-loading";
import { ProjectHealthHelper, ProjectStatusHelper } from "@/lib/enums";
import { ChevronDown, Filter, X } from "lucide-react";

interface ProjectAdvancedSearchProps {
  filterName: string;
  setFilterName: (_value: string) => void;
  filterCode: string;
  setFilterCode: (_value: string) => void;
  filterStatuses: string[];
  toggleStatus: (_status: string) => void;
  filterHealths: string[];
  toggleHealth: (_health: string) => void;
  filterProgressMin: string;
  setFilterProgressMin: (_value: string) => void;
  filterProgressMax: string;
  setFilterProgressMax: (_value: string) => void;
  filterBudgetMin: string;
  setFilterBudgetMin: (_value: string) => void;
  filterBudgetMax: string;
  setFilterBudgetMax: (_value: string) => void;
  filterForecastMin: string;
  setFilterForecastMin: (_value: string) => void;
  filterForecastMax: string;
  setFilterForecastMax: (_value: string) => void;
  filterProjectManager: string;
  setFilterProjectManager: (_value: string) => void;
  setFilterProjectManagerName: (_value: string) => void;
  filterDueDateFrom: string;
  setFilterDueDateFrom: (_value: string) => void;
  filterDueDateTo: string;
  setFilterDueDateTo: (_value: string) => void;
  activeFilterCount: number;
  onClearFilters: () => void;
}

export function ProjectAdvancedSearch({
  filterName,
  setFilterName,
  filterCode,
  setFilterCode,
  filterStatuses,
  toggleStatus,
  filterHealths,
  toggleHealth,
  filterProgressMin,
  setFilterProgressMin,
  filterProgressMax,
  setFilterProgressMax,
  filterBudgetMin,
  setFilterBudgetMin,
  filterBudgetMax,
  setFilterBudgetMax,
  filterForecastMin,
  setFilterForecastMin,
  filterForecastMax,
  setFilterForecastMax,
  filterProjectManager,
  setFilterProjectManager,
  setFilterProjectManagerName,
  filterDueDateFrom,
  setFilterDueDateFrom,
  filterDueDateTo,
  setFilterDueDateTo,
  activeFilterCount,
  onClearFilters,
}: ProjectAdvancedSearchProps) {
  const t = useTranslations("projects");
  const common = useTranslations("common");
  const { statusLabel, healthLabel } = useProjectEnumLabels();
  const [open, setOpen] = useState(false);
  const { data: projectManagers, isLoading: projectManagersLoading } = useUserLookup("project.manager", shouldLoadProjectLookup(open));
  const projectManagerOptions = toUserSelectOptions(projectManagers);

  const healthOptions = ProjectHealthHelper.allValues().map((value) => ({
    value,
    label: healthLabel(value),
  }));

  const allStatuses = ProjectStatusHelper.allValues();

  return (
    <Collapsible open={open} onOpenChange={setOpen}>
      <Card>
        <CollapsibleTrigger asChild>
          <CardHeader className="cursor-pointer hover:bg-muted/50 transition-colors">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Filter className="h-4 w-4 text-muted-foreground" />
                <CardTitle className="text-base">{common("advancedSearch") || "Advanced Search"}</CardTitle>
                {activeFilterCount > 0 && (
                  <Badge variant="secondary" className="text-xs">{activeFilterCount}</Badge>
                )}
              </div>
              <ChevronDown className={`h-4 w-4 text-muted-foreground transition-transform ${open ? "rotate-180" : ""}`} />
            </div>
          </CardHeader>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <CardContent className="pt-0">
            <div className="space-y-4">
              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{common("project")}</Label>
                  <Input placeholder={common("project")} value={filterName} onChange={(e) => setFilterName(e.target.value)} />
                </div>
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{common("code")}</Label>
                  <Input placeholder={common("code")} value={filterCode} onChange={(e) => setFilterCode(e.target.value)} />
                </div>
              </div>

              <div className="space-y-2">
                <Label className="text-xs font-medium">{common("status")}</Label>
                <div className="flex flex-wrap gap-2">
                  {allStatuses.map((status) => (
                    <div key={status} className="flex items-center gap-2">
                      <Checkbox
                        id={`status-${status}`}
                        checked={filterStatuses.includes(status)}
                        onCheckedChange={() => toggleStatus(status)}
                      />
                      <Label htmlFor={`status-${status}`} className="text-sm cursor-pointer">
                        {statusLabel(status)}
                      </Label>
                    </div>
                  ))}
                </div>
              </div>

              <div className="space-y-2">
                <Label className="text-xs font-medium">{common("health")}</Label>
                <div className="flex flex-wrap gap-2">
                  {healthOptions.map((health) => (
                    <div key={health.value} className="flex items-center gap-2">
                      <Checkbox
                        id={`health-${health.value}`}
                        checked={filterHealths.includes(health.value)}
                        onCheckedChange={() => toggleHealth(health.value)}
                      />
                      <Label htmlFor={`health-${health.value}`} className="text-sm cursor-pointer">{health.label}</Label>
                    </div>
                  ))}
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-3">
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{common("progress")} (%)</Label>
                  <div className="flex gap-2">
                    <Input placeholder="Min" type="number" min="0" max="100" value={filterProgressMin} onChange={(e) => setFilterProgressMin(e.target.value)} />
                    <Input placeholder="Max" type="number" min="0" max="100" value={filterProgressMax} onChange={(e) => setFilterProgressMax(e.target.value)} />
                  </div>
                </div>
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{common("budget")}</Label>
                  <div className="flex gap-2">
                    <Input placeholder="Min" type="number" value={filterBudgetMin} onChange={(e) => setFilterBudgetMin(e.target.value)} />
                    <Input placeholder="Max" type="number" value={filterBudgetMax} onChange={(e) => setFilterBudgetMax(e.target.value)} />
                  </div>
                </div>
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{common("forecast")}</Label>
                  <div className="flex gap-2">
                    <Input placeholder="Min" type="number" value={filterForecastMin} onChange={(e) => setFilterForecastMin(e.target.value)} />
                    <Input placeholder="Max" type="number" value={filterForecastMax} onChange={(e) => setFilterForecastMax(e.target.value)} />
                  </div>
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{t("pm")}</Label>
                  <SearchableSelect options={projectManagerOptions} value={filterProjectManager} onValueChange={(value) => { setFilterProjectManager(value); setFilterProjectManagerName(projectManagers?.find((manager) => manager.id === value)?.fullName ?? ""); }} placeholder={t("pm")} searchPlaceholder={t("searchManagerPlaceholder")} loading={projectManagersLoading} emptyText={t("noEligibleManagers")} />
                </div>
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{common("dueDate")}</Label>
                  <div className="flex gap-2">
                    <Input type="date" value={filterDueDateFrom} onChange={(e) => setFilterDueDateFrom(e.target.value)} />
                    <Input type="date" value={filterDueDateTo} onChange={(e) => setFilterDueDateTo(e.target.value)} />
                  </div>
                </div>
              </div>

              {activeFilterCount > 0 && (
                <Button variant="ghost" size="sm" onClick={onClearFilters} className="text-muted-foreground">
                  <X className="mr-2 h-4 w-4" />
                  {common("clearFilters") || "Clear Filters"}
                </Button>
              )}
            </div>
          </CardContent>
        </CollapsibleContent>
      </Card>
    </Collapsible>
  );
}
