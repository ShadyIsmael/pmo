import * as React from "react";
import { AlertTriangle, Briefcase, Filter, GitBranch, LayoutGrid, Pencil, Plus, Trash2, Users } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Input } from "@components/ui/Input";
import { Progress } from "@components/ui/Progress";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { useI18n } from "@i18n/messages";
import { hasAnyPermission, Permissions } from "@lib/permissions";
import { useAuth } from "@auth/AuthProvider";
import { useManagement } from "@api/management";
import { AllocationDialog } from "./AllocationDialog";
import { useAllocations, useDeleteAllocation, type AllocationDto } from "./api/allocations";
import { useCapacityAvailability, useCapacityGantt, useCapacityMatrix, useDemandVsCapacity } from "./api/capacity";
import { summarizeCapacityByDepartment } from "./capacity-aggregates";
import { ResourceGanttView } from "./ResourceGanttView";
import { useDepartments } from "@features/teams/api/teams";

const VIEW_STORAGE_KEY = "nexus_capacity_view";

function utilizationTone(value: number): "success" | "warning" | "destructive" {
  return value > 80 ? "destructive" : value > 60 ? "warning" : "success";
}

export function CapacityPage() {
  const { t, formatNumber, formatPercent } = useI18n();
  const confirm = useConfirm();
  const { user } = useAuth();
  const { canMutate, effectiveReadOnly } = useManagement();
  const canAssign = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Resources.Assign, Permissions.Resources.Allocate, Permissions.Resources.Manage]) && canMutate && !effectiveReadOnly);
  const canDelete = Boolean(user && hasAnyPermission(user.permissions, [Permissions.Resources.Manage]) && canMutate && !effectiveReadOnly);
  const [periodType, setPeriodType] = React.useState("month");
  const [departmentId, setDepartmentId] = React.useState("");
  const [view, setView] = React.useState<"matrix" | "gantt">(() => (typeof window === "undefined" ? "matrix" : (localStorage.getItem(VIEW_STORAGE_KEY) as "matrix" | "gantt") || "matrix"));
  const today = React.useMemo(() => new Date(), []);
  const defaultStart = React.useMemo(() => new Date(today.getFullYear(), today.getMonth(), 1).toISOString().slice(0, 10), [today]);
  const defaultEnd = React.useMemo(() => new Date(today.getFullYear(), today.getMonth() + 3, 1).toISOString().slice(0, 10), [today]);
  const [startDate, setStartDate] = React.useState(defaultStart);
  const [endDate, setEndDate] = React.useState(defaultEnd);
  const [allocationSheet, setAllocationSheet] = React.useState<{ open: boolean; allocation: AllocationDto | null }>({ open: false, allocation: null });
  const [actionError, setActionError] = React.useState<string | null>(null);

  React.useEffect(() => { localStorage.setItem(VIEW_STORAGE_KEY, view); }, [view]);

  const filter = { periodType, startDate, endDate, ...(departmentId ? { departmentId } : {}) };
  const matrixQuery = useCapacityMatrix(filter);
  const demandQuery = useDemandVsCapacity(filter);
  const availabilityQuery = useCapacityAvailability(filter);
  const ganttQuery = useCapacityGantt(filter);
  const allocationsQuery = useAllocations({ page: 1, pageSize: 100 });
  const departmentsQuery = useDepartments(false);
  const deleteAllocation = useDeleteAllocation();
  const matrix = matrixQuery.data;
  const departmentSummaries = React.useMemo(() => summarizeCapacityByDepartment(matrix), [matrix]);
  const openCreate = () => { setActionError(null); setAllocationSheet({ open: true, allocation: null }); };
  const openEdit = (allocation: AllocationDto) => { setActionError(null); setAllocationSheet({ open: true, allocation }); };
  const removeAllocation = async (allocation: AllocationDto) => {
    if (!await confirm({ description: t("resources.capacity.deleteConfirm"), destructive: true })) return;
    try { await deleteAllocation.mutateAsync(allocation.id); } catch (reason) { setActionError(reason instanceof Error ? reason.message : t("resources.capacity.deleteError")); }
  };

  return <div className="nexus-page nexus-capacity space-y-6">
    <PageHeader title={t("resources.capacity.title")} description={t("resources.capacity.description")} actions={<div className="flex flex-wrap items-center justify-end gap-2"><div className="flex items-center rounded-md border p-0.5"><Button variant={view === "matrix" ? "default" : "ghost"} size="icon-sm" aria-label={t("resources.capacity.matrix")} onClick={() => setView("matrix")}><LayoutGrid className="h-3.5 w-3.5" /></Button><Button variant={view === "gantt" ? "default" : "ghost"} size="icon-sm" aria-label={t("resources.capacity.gantt")} onClick={() => setView("gantt")}><GitBranch className="h-3.5 w-3.5" /></Button></div><select className="input-field max-w-32" value={periodType} onChange={(event) => setPeriodType(event.target.value)} aria-label={t("resources.capacity.period")}><option value="week">{t("resources.capacity.weekly")}</option><option value="month">{t("resources.capacity.monthly")}</option><option value="quarter">{t("resources.capacity.quarterly")}</option></select><select className="input-field max-w-48" value={departmentId} onChange={(event) => setDepartmentId(event.target.value)} aria-label={t("resources.capacity.deptLabel")}><option value="">{t("resources.utilization.allDepartments")}</option>{(departmentsQuery.data ?? []).map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select><Input type="date" className="h-9 w-40 text-xs" value={startDate} onChange={(event) => setStartDate(event.target.value)} /><Input type="date" className="h-9 w-40 text-xs" value={endDate} onChange={(event) => setEndDate(event.target.value)} /><Button variant="outline" size="sm"><Filter className="me-2 h-4 w-4" />{t("resources.capacity.filter")}</Button>{canAssign ? <Button size="sm" onClick={openCreate}><Plus className="me-1.5 h-4 w-4" />{t("resources.capacity.createAllocation")}</Button> : null}</div>} />

    {actionError ? <p className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{actionError}</p> : null}
    {view === "gantt" ? <Card><CardHeader><CardTitle>{t("resources.capacity.gantt")}</CardTitle></CardHeader><CardContent><ResourceGanttView data={ganttQuery.data} isLoading={ganttQuery.isLoading} startDate={new Date(startDate)} endDate={new Date(endDate)} canEdit={canAssign} onEdit={openEdit} /></CardContent></Card> : <>
      <div className="grid gap-4 md:grid-cols-4"><Card><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">{t("resources.capacity.totalCapacity")}</CardTitle><Users className="h-4 w-4 text-muted-foreground" /></CardHeader><CardContent><p className="text-2xl font-bold tabular-nums">{formatNumber(Math.round(matrix?.summary.totalNetCapacityHours ?? matrix?.summary.totalAvailableHours ?? 0))} {t("common.hours")}</p><p className="text-xs text-muted-foreground">{t("resources.capacity.netCapacity")}</p></CardContent></Card><Card><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">{t("resources.capacity.demand")}</CardTitle><Briefcase className="h-4 w-4 text-muted-foreground" /></CardHeader><CardContent><p className="text-2xl font-bold tabular-nums">{formatNumber(Math.round(demandQuery.data?.total.totalDemandHours ?? 0))} {t("common.hours")}</p><p className="text-xs text-muted-foreground">{t("resources.capacity.thisPeriod")}</p></CardContent></Card><Card><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">{t("resources.capacity.gap")}</CardTitle><AlertTriangle className="h-4 w-4 text-muted-foreground" /></CardHeader><CardContent><p className={`text-2xl font-bold tabular-nums ${(demandQuery.data?.total.totalGapHours ?? 0) < 0 ? "text-warning" : "text-success"}`}>{formatNumber(Math.round(demandQuery.data?.total.totalGapHours ?? 0))} {t("common.hours")}</p><p className="text-xs text-muted-foreground">{(demandQuery.data?.total.totalGapHours ?? 0) < 0 ? t("resources.capacity.shortfall") : t("resources.capacity.surplus")}</p></CardContent></Card><Card><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">{t("resources.capacity.utilization")}</CardTitle><Users className="h-4 w-4 text-muted-foreground" /></CardHeader><CardContent><p className="text-2xl font-bold tabular-nums">{formatPercent(matrix?.summary.overallUtilizationPercent ?? 0)}</p><Progress value={matrix?.summary.overallUtilizationPercent ?? 0} tone={utilizationTone(matrix?.summary.overallUtilizationPercent ?? 0)} className="mt-2" /></CardContent></Card></div>
      <Tabs defaultValue="matrix">
        <TabsList><TabsTrigger value="matrix">{t("resources.capacity.capacityMatrix")}</TabsTrigger><TabsTrigger value="demand">{t("resources.capacity.demandVsCapacity")}</TabsTrigger><TabsTrigger value="availability">{t("resources.capacity.resourceAvailability")}</TabsTrigger></TabsList>
        <TabsContent value="matrix"><Card><CardHeader><CardTitle>{t("resources.capacity.departmentOverview")}</CardTitle></CardHeader><CardContent className="p-0">
          {matrixQuery.isLoading ? <p className="py-10 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : matrixQuery.isError ? <p className="py-10 text-center text-sm text-destructive">{t("common.errorLoading")}</p> : departmentSummaries.length === 0 ? <p className="py-10 text-center text-sm text-muted-foreground">{t("resources.capacity.noData")}</p> : <div className="divide-y">{departmentSummaries.map((department) => {
            const isOver = department.averageUtilizationPercent > 80;
            return <div key={department.departmentName} className="flex flex-col items-start gap-3 px-4 py-3 text-sm sm:flex-row sm:items-center sm:gap-4">
              <div className="flex-1 font-medium">{department.departmentName}</div>
              <div className="w-20 text-xs text-muted-foreground">{formatNumber(department.resourceCount)} {t("resources.capacity.resources")}</div>
              <div className="flex items-center gap-2"><span className="text-xs">{t("resources.capacity.capacityLabel")}</span><Progress value={Math.min(department.averageAvailableHours, 100)} className="h-2 w-16" /><span className="whitespace-nowrap text-xs">{formatNumber(department.weeklyCapacityHours)} {t("common.hours")}</span></div>
              <div className="flex items-center gap-2"><span className="text-xs">{t("resources.capacity.allocationLabel")}</span><Progress value={Math.min(department.averageUtilizationPercent, 100)} tone={utilizationTone(department.averageUtilizationPercent)} className="h-2 w-16" /><span className={`text-xs font-medium ${isOver ? "text-destructive" : "text-success"}`}>{formatPercent(department.averageUtilizationPercent)}</span></div>
              <Badge status={isOver ? "Overdue" : "OK"} className="w-16 justify-center text-[10px]">{isOver ? t("resources.capacity.over") : t("resources.capacity.ok")}</Badge>
            </div>;
          })}</div>}
        </CardContent></Card></TabsContent>
        <TabsContent value="demand"><Card><CardHeader><CardTitle>{t("resources.capacity.demandVsCapacity")}</CardTitle></CardHeader><CardContent className="p-0">{demandQuery.isLoading ? <p className="py-10 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : !demandQuery.data?.groups.length ? <p className="py-10 text-center text-sm text-muted-foreground">{t("resources.capacity.noDemandData")}</p> : <Table><TableHeader><TableRow><TableHead>{t("resources.capacity.group")}</TableHead><TableHead>{t("resources.capacity.demandLabel")}</TableHead><TableHead>{t("resources.capacity.capacityLabelShort")}</TableHead><TableHead>{t("resources.capacity.gapLabel")}</TableHead><TableHead>{t("resources.capacity.statusLabel")}</TableHead></TableRow></TableHeader><TableBody>{demandQuery.data.groups.map((group) => <TableRow key={`${group.groupType}:${group.groupName}`}><TableCell><span className="me-2 text-xs text-muted-foreground">{group.groupType}</span>{group.groupName}</TableCell><TableCell>{formatNumber(Math.round(group.demandHours))} {t("common.hours")}</TableCell><TableCell>{formatNumber(Math.round(group.capacityHours))} {t("common.hours")}</TableCell><TableCell className={group.gapHours < 0 ? "text-destructive" : "text-success"}>{group.gapHours >= 0 ? "+" : ""}{formatNumber(Math.round(group.gapHours))} {t("common.hours")}</TableCell><TableCell><Badge status={group.status}>{group.status}</Badge></TableCell></TableRow>)}</TableBody></Table>}</CardContent></Card></TabsContent>
        <TabsContent value="availability"><Card><CardHeader><CardTitle>{t("resources.capacity.resourceAvailability")}</CardTitle></CardHeader><CardContent className="p-0">{availabilityQuery.isLoading ? <p className="py-10 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : !availabilityQuery.data?.items.length ? <p className="py-10 text-center text-sm text-muted-foreground">{t("resources.capacity.noAvailabilityData")}</p> : <Table><TableHeader><TableRow><TableHead>{t("resources.capacity.resourceLabel")}</TableHead><TableHead>{t("resources.capacity.roleLabel")}</TableHead><TableHead>{t("resources.capacity.deptLabel")}</TableHead><TableHead>{t("resources.capacity.availableLabel")}</TableHead><TableHead>{t("resources.capacity.utilization")}</TableHead><TableHead>{t("resources.capacity.statusLabel")}</TableHead></TableRow></TableHeader><TableBody>{availabilityQuery.data.items.slice(0, 100).map((resource) => <TableRow key={resource.resourceId}><TableCell className="font-medium">{resource.resourceName}</TableCell><TableCell>{resource.jobTitle ?? "—"}</TableCell><TableCell>{resource.departmentName ?? "—"}</TableCell><TableCell>{formatNumber(Math.round(resource.availableCapacityHours))} {t("resources.capacity.hoursAbbr")}</TableCell><TableCell>{formatPercent(resource.currentUtilization)}</TableCell><TableCell><span className={resource.currentUtilization > 80 ? "text-destructive" : resource.currentUtilization > 60 ? "text-warning" : "text-success"}>{resource.currentUtilization > 80 ? t("resources.utilization.over") : resource.currentUtilization > 60 ? t("resources.utilization.atRisk") : t("resources.utilization.ok")}</span></TableCell></TableRow>)}</TableBody></Table>}</CardContent></Card></TabsContent>
      </Tabs>
    </>}
    <Card><CardHeader className="flex !flex-row items-center justify-between gap-3"><div><CardTitle>{t("resources.capacity.allocationTable")}</CardTitle><p className="mt-1 text-sm text-muted-foreground">{t("resources.capacity.allocationTableDescription")}</p></div>{canAssign ? <Button size="sm" onClick={openCreate}><Plus className="me-1.5 h-4 w-4" />{t("resources.capacity.createAllocation")}</Button> : null}</CardHeader><CardContent className="p-0">{allocationsQuery.isLoading ? <p className="py-10 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : allocationsQuery.isError ? <p className="py-10 text-center text-sm text-destructive">{t("common.errorLoading")}</p> : !(allocationsQuery.data?.items.length ?? 0) ? <p className="py-10 text-center text-sm text-muted-foreground">{t("resources.capacity.emptyAllocations")}</p> : <Table><TableHeader><TableRow><TableHead>{t("resources.capacity.resourceName")}</TableHead><TableHead>{t("resources.capacity.projectName")}</TableHead><TableHead>{t("resources.capacity.roleLabel")}</TableHead><TableHead>{t("resources.capacity.allocationLabel")}</TableHead><TableHead>{t("resources.capacity.statusLabel")}</TableHead><TableHead /></TableRow></TableHeader><TableBody>{allocationsQuery.data?.items.map((allocation) => <TableRow key={allocation.id}><TableCell className="font-medium">{allocation.resourceName}</TableCell><TableCell>{allocation.projectName || t("resources.gantt.unnamedProject")}</TableCell><TableCell>{allocation.role}</TableCell><TableCell>{formatNumber(allocation.allocationPercentage)}%</TableCell><TableCell><Badge status={allocation.status}>{allocation.status}</Badge></TableCell><TableCell><div className="flex justify-end gap-1">{canAssign ? <Button size="icon-sm" variant="ghost" aria-label={t("resources.capacity.editAllocation")} onClick={() => openEdit(allocation)}><Pencil className="h-4 w-4" /></Button> : null}{canDelete ? <Button size="icon-sm" variant="ghost" aria-label={t("resources.capacity.deleteAllocation")} onClick={() => void removeAllocation(allocation)}><Trash2 className="h-4 w-4 text-destructive" /></Button> : null}</div></TableCell></TableRow>)}</TableBody></Table>}</CardContent></Card>
    <AllocationDialog open={allocationSheet.open} allocation={allocationSheet.allocation} onOpenChange={(open) => setAllocationSheet((current) => ({ ...current, open }))} />
  </div>;
}
