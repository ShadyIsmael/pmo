import * as React from "react";
import { ChevronDown, Filter, X } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Checkbox } from "@components/ui/Checkbox";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@components/ui/Collapsible";
import { Input } from "@components/ui/Input";
import { Label } from "@components/ui/Label";
import { SearchableSelect } from "@components/ui/SearchableSelect";
import { useI18n } from "@i18n/messages";
import { useProjectEnumLabels } from "../lib/project-labels";
import { ProjectHealthHelper } from "@lib/enums";
import { shouldLoadProjectLookup, toUserSelectOptions, useProjectManagerCandidateLookup } from "../lib/project-lookups";
import { projectLifecycleLabel, type ProjectLifecycleOption } from "../api/project-lifecycle";

interface ProjectAdvancedSearchProps {
  filterName: string;
  setFilterName: (value: string) => void;
  filterCode: string;
  setFilterCode: (value: string) => void;
  filterStatuses: string[];
  toggleStatus: (status: string) => void;
  statusOptions: ProjectLifecycleOption[];
  statusOptionsLoading: boolean;
  statusOptionsError: boolean;
  retryStatusOptions: () => void;
  filterHealths: string[];
  toggleHealth: (health: string) => void;
  filterProgressMin: string;
  setFilterProgressMin: (value: string) => void;
  filterProgressMax: string;
  setFilterProgressMax: (value: string) => void;
  filterBudgetMin: string;
  setFilterBudgetMin: (value: string) => void;
  filterBudgetMax: string;
  setFilterBudgetMax: (value: string) => void;
  filterProjectManager: string;
  setFilterProjectManager: (value: string) => void;
  setFilterProjectManagerName: (value: string) => void;
  filterDueDateFrom: string;
  setFilterDueDateFrom: (value: string) => void;
  filterDueDateTo: string;
  setFilterDueDateTo: (value: string) => void;
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
  statusOptions,
  statusOptionsLoading,
  statusOptionsError,
  retryStatusOptions,
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
  const { t, locale } = useI18n();
  const { healthLabel } = useProjectEnumLabels();
  const [open, setOpen] = React.useState(false);
  const { data: projectManagers, isLoading: projectManagersLoading } = useProjectManagerCandidateLookup(shouldLoadProjectLookup(open));
  const projectManagerOptions = toUserSelectOptions(projectManagers);

  const healthOptions = ProjectHealthHelper.allValues().map((value) => ({
    value,
    label: healthLabel(value),
  }));

  return (
    <Collapsible open={open} onOpenChange={setOpen}>
      <Card>
        <CollapsibleTrigger asChild>
          <CardHeader className="cursor-pointer transition-colors hover:bg-muted/50">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Filter className="h-4 w-4 text-muted-foreground" />
                <CardTitle className="text-base">{t("common.advancedSearch")}</CardTitle>
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
                  <Label className="text-xs font-medium">{t("common.project")}</Label>
                  <Input placeholder={t("common.project")} value={filterName} onChange={(e) => setFilterName(e.target.value)} />
                </div>
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{t("common.code")}</Label>
                  <Input placeholder={t("common.code")} value={filterCode} onChange={(e) => setFilterCode(e.target.value)} />
                </div>
              </div>

              <div className="space-y-2">
                <Label className="text-xs font-medium">{t("common.status")}</Label>
                {statusOptionsLoading ? <p role="status" className="text-xs text-muted-foreground">{t("projects.lifecycle.statusLookupLoading")}</p> : null}
                {statusOptionsError ? <div className="flex items-center gap-3"><p role="alert" className="text-xs text-destructive">{t("projects.lifecycle.lookupError")}</p><Button type="button" size="sm" variant="outline" onClick={retryStatusOptions}>{t("projects.lifecycle.retry")}</Button></div> : null}
                <div className="flex flex-wrap gap-2">
                  {statusOptions.filter((status) => status.isActive !== false).map((status) => (
                    <div key={status.id} className="flex items-center gap-2">
                      <Checkbox
                        id={`status-${status.id}`}
                        checked={filterStatuses.includes(status.code)}
                        onCheckedChange={() => toggleStatus(status.code)}
                      />
                      <Label htmlFor={`status-${status.id}`} className="cursor-pointer text-sm">
                        {projectLifecycleLabel(status, locale, status.code)}
                      </Label>
                    </div>
                  ))}
                </div>
                {!statusOptionsLoading && !statusOptionsError && statusOptions.length === 0 ? <p className="text-xs text-muted-foreground">{t("projects.lifecycle.noStatusesAvailable")}</p> : null}
              </div>

              <div className="space-y-2">
                <Label className="text-xs font-medium">{t("common.health")}</Label>
                <div className="flex flex-wrap gap-2">
                  {healthOptions.map((health) => (
                    <div key={health.value} className="flex items-center gap-2">
                      <Checkbox
                        id={`health-${health.value}`}
                        checked={filterHealths.includes(health.value)}
                        onCheckedChange={() => toggleHealth(health.value)}
                      />
                      <Label htmlFor={`health-${health.value}`} className="cursor-pointer text-sm">{health.label}</Label>
                    </div>
                  ))}
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{t("projects.progressSourceWorkingPlan")} (%)</Label>
                  <div className="flex gap-2">
                    <Input placeholder={t("common.min")} type="number" min="0" max="100" value={filterProgressMin} onChange={(e) => setFilterProgressMin(e.target.value)} />
                    <Input placeholder={t("common.max")} type="number" min="0" max="100" value={filterProgressMax} onChange={(e) => setFilterProgressMax(e.target.value)} />
                  </div>
                </div>
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{t("common.budget")}</Label>
                  <div className="flex gap-2">
                    <Input placeholder={t("common.min")} type="number" value={filterBudgetMin} onChange={(e) => setFilterBudgetMin(e.target.value)} />
                    <Input placeholder={t("common.max")} type="number" value={filterBudgetMax} onChange={(e) => setFilterBudgetMax(e.target.value)} />
                  </div>
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{t("projects.pm")}</Label>
                  <SearchableSelect options={projectManagerOptions} value={filterProjectManager} onValueChange={(value) => { setFilterProjectManager(value); setFilterProjectManagerName(projectManagers?.find((manager) => manager.id === value)?.fullName ?? ""); }} placeholder={t("projects.pm")} searchPlaceholder={t("projects.pm")} loading={projectManagersLoading} emptyText={t("common.noResults")} />
                </div>
                <div className="space-y-2">
                  <Label className="text-xs font-medium">{t("common.dueDate")}</Label>
                  <div className="flex gap-2">
                    <Input type="date" value={filterDueDateFrom} onChange={(e) => setFilterDueDateFrom(e.target.value)} />
                    <Input type="date" value={filterDueDateTo} onChange={(e) => setFilterDueDateTo(e.target.value)} />
                  </div>
                </div>
              </div>

              {activeFilterCount > 0 && (
                <Button variant="ghost" size="sm" onClick={onClearFilters} className="text-muted-foreground">
                  <X className="mr-2 h-4 w-4" />
                  {t("common.clearFilters")}
                </Button>
              )}
            </div>
          </CardContent>
        </CollapsibleContent>
      </Card>
    </Collapsible>
  );
}
