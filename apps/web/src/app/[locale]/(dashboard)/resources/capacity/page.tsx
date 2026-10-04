"use client";

import { useState, useEffect } from "react";
import { useTranslations } from "next-intl";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { getLookupTextClass } from "@/lib/status-presentation";
import { Progress } from "@/components/ui/progress";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import { Filter, Users, Briefcase, AlertTriangle, LayoutGrid, GitBranch } from "lucide-react";
import { useCapacityMatrix, useDemandVsCapacity, useCapacityAvailability, useCapacityGantt } from "@/hooks/resources/useCapacity";
import { ResourceGanttView } from "@/components/resources/ResourceGanttView";
import { AllocationDialog } from "@/components/resources/AllocationDialog";
import { useAuth } from "@/lib/auth";
import { hasPermission, Permissions } from "@/lib/permissions";

const VIEW_STORAGE_KEY = "nexus_capacity_view";

export default function CapacityPlanningPage() {
  const t = useTranslations("resources.capacity");
  const tc = useTranslations("common");
  const { user } = useAuth();
  const canAssignResources = !!user && [Permissions.Resources.Assign, Permissions.Resources.Allocate, Permissions.Resources.Manage]
    .some((permission) => hasPermission(user.permissions, permission));
  const [periodType, setPeriodType] = useState("month");
  const [departmentId, setDepartmentId] = useState("");
  const [matrixOrGantt, setMatrixOrGantt] = useState<"matrix" | "gantt">(() => {
    if (typeof window !== "undefined") {
      return (localStorage.getItem(VIEW_STORAGE_KEY) as "matrix" | "gantt") || "matrix";
    }
    return "matrix";
  });
  const [showCreateDialog, setShowCreateDialog] = useState(false);

  useEffect(() => {
    localStorage.setItem(VIEW_STORAGE_KEY, matrixOrGantt);
  }, [matrixOrGantt]);

  const today = new Date();
  const baseStart = new Date(today.getFullYear(), today.getMonth(), 1);
  const baseEnd = new Date(baseStart);
  baseEnd.setMonth(baseEnd.getMonth() + 3);

  const [startDate, setStartDate] = useState(baseStart.toISOString().split("T")[0]);
  const [endDate, setEndDate] = useState(baseEnd.toISOString().split("T")[0]);

  const baseFilter = {
    periodType,
    startDate,
    endDate,
    ...(departmentId ? { departmentId } : {}),
  };

  const { data: matrix, isLoading: matrixLoading } = useCapacityMatrix(baseFilter);
  const { data: demandVsCapacity, isLoading: demandLoading } = useDemandVsCapacity(baseFilter);
  const { data: availability, isLoading: availLoading } = useCapacityAvailability(baseFilter);
  const { data: ganttData, isLoading: ganttLoading } = useCapacityGantt(baseFilter);

  const summary = matrix?.summary;
  const departments = matrix?.rows
    ? [...new Set(matrix.rows.map((r) => r.departmentName).filter(Boolean))].slice(0, 8)
    : [];

  return (
    <div className="nexus-page nexus-capacity space-y-6">
      <PageHeader
        title={t("title")}
        description={t("description")}
        actions={
          <div className="nexus-page-actions flex flex-wrap items-center justify-end gap-2">
            <div className="flex items-center rounded-md border p-0.5">
              <Button
                variant={matrixOrGantt === "matrix" ? "default" : "ghost"}
                size="sm"
                className="h-7 px-2"
                onClick={() => setMatrixOrGantt("matrix")}
              >
                <LayoutGrid className="h-3.5 w-3.5" />
              </Button>
              <Button
                variant={matrixOrGantt === "gantt" ? "default" : "ghost"}
                size="sm"
                className="h-7 px-2"
                onClick={() => setMatrixOrGantt("gantt")}
              >
                <GitBranch className="h-3.5 w-3.5" />
              </Button>
            </div>
            <Select value={periodType} onValueChange={setPeriodType}>
              <SelectTrigger className="w-28">
                <SelectValue placeholder={t("period")} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="week">{t("weekly")}</SelectItem>
                <SelectItem value="month">{t("monthly")}</SelectItem>
                <SelectItem value="quarter">{t("quarterly")}</SelectItem>
              </SelectContent>
            </Select>
            <input type="date" className="h-9 rounded-md border px-2 text-xs" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            <input type="date" className="h-9 rounded-md border px-2 text-xs" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
            <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />{t("filter")}</Button>
            {canAssignResources && <Button size="sm" onClick={() => setShowCreateDialog(true)}>{t("createAllocation")}</Button>}
          </div>
        }
      />

      {matrixOrGantt === "matrix" ? (
        <>
          <div className="grid gap-4 md:grid-cols-4">
            <Card className="nexus-metric-card">
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">{t("totalCapacity")}</CardTitle>
                <Users className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                {matrixLoading ? <Skeleton className="h-8 w-24" /> : (
                  <>
                    <div className="text-2xl font-bold">{Math.round(summary?.totalNetCapacityHours ?? 0).toLocaleString()} {tc("hours")}</div>
                    <p className="text-xs text-muted-foreground">{t("netCapacity")}</p>
                  </>
                )}
              </CardContent>
            </Card>
            <Card className="nexus-metric-card">
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">{t("demand")}</CardTitle>
                <Briefcase className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                {demandLoading ? <Skeleton className="h-8 w-24" /> : (
                  <>
                    <div className="text-2xl font-bold">{Math.round(demandVsCapacity?.total.totalDemandHours ?? 0).toLocaleString()} {tc("hours")}</div>
                    <p className="text-xs text-muted-foreground">
                      {demandVsCapacity?.total.totalCapacityHours
                        ? `${demandVsCapacity.total.totalDemandHours > demandVsCapacity.total.totalCapacityHours ? t("exceeds") : t("within")} capacity`
                        : t("thisPeriod")}
                    </p>
                  </>
                )}
              </CardContent>
            </Card>
            <Card className="nexus-metric-card">
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">{t("gap")}</CardTitle>
                <AlertTriangle className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                {demandLoading ? <Skeleton className="h-8 w-24" /> : (
                  <>
                    <div className={`text-2xl font-bold ${(demandVsCapacity?.total.totalGapHours ?? 0) < 0 ? "text-status-amber" : "text-status-green"}`}>
                      {demandVsCapacity?.total.totalGapHours ?? 0 >= 0 ? "+" : ""}{Math.round(demandVsCapacity?.total.totalGapHours ?? 0).toLocaleString()} {tc("hours")}
                    </div>
                    <p className="text-xs text-muted-foreground">{(demandVsCapacity?.total.totalGapHours ?? 0) < 0 ? t("shortfall") : t("surplus")}</p>
                  </>
                )}
              </CardContent>
            </Card>
            <Card className="nexus-metric-card">
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">{t("utilization")}</CardTitle>
                <Users className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                {matrixLoading ? <Skeleton className="h-8 w-full" /> : (
                  <>
                    <div className="text-2xl font-bold">{summary?.overallUtilizationPercent ?? 0}%</div>
                    <Progress value={summary?.overallUtilizationPercent ?? 0} className="mt-2 h-2" />
                  </>
                )}
              </CardContent>
            </Card>
          </div>

          <Tabs defaultValue="matrix">
            <TabsList>
              <TabsTrigger value="matrix">{t("capacityMatrix")}</TabsTrigger>
              <TabsTrigger value="demand">{t("demandVsCapacity")}</TabsTrigger>
              <TabsTrigger value="availability">{t("resourceAvailability")}</TabsTrigger>
            </TabsList>

            <TabsContent value="matrix" className="mt-4">
              <div className="nexus-table-surface">
                <div className="nexus-table-header">
                  {t("departmentOverview")}
                </div>
                {matrixLoading ? (
                  <div className="space-y-2 p-4">
                    {Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-12 w-full" />)}
                  </div>
                ) : departments.length === 0 ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">{t("noData")}</div>
                ) : (
                  departments.map((dept) => {
                    const deptRows = matrix?.rows.filter((r) => r.departmentName === dept) ?? [];
                    const totalCapacity = deptRows.reduce((s, r) => s + r.weeklyCapacityHours, 0);
                    const avgUtil = deptRows.length > 0 ? Math.round(deptRows.reduce((s, r) => s + r.currentUtilization, 0) / deptRows.length) : 0;
                    const latestCells = deptRows.flatMap((r) => r.cells).filter((c) => c.periodKey === matrix?.periods[0]?.key);
                    const avgAvail = latestCells.length > 0 ? Math.round(latestCells.reduce((s, c) => s + c.availableHours, 0) / latestCells.length) : 0;
                    const isOver = avgUtil > 80;
                    return (
                      <div key={dept} className="nexus-table-row flex items-center gap-4 px-4 py-3 text-sm last:border-0">
                        <div className="flex-1 font-medium">{dept}</div>
                        <div className="w-20 text-right text-xs text-muted-foreground">{deptRows.length} {t("resources")}</div>
                        <div className="flex items-center gap-2">
                          <span className="text-xs">{t("capacityLabel")}</span>
                          <Progress value={Math.min(avgAvail, 100)} className="h-2 w-16" />
                          <span className="text-xs">{Math.round(totalCapacity)} {tc("hours")}</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <span className="text-xs">{t("allocationLabel")}</span>
                          <Progress value={Math.min(avgUtil, 100)} className="h-2 w-16" />
                          <span className={`text-xs font-medium ${isOver ? "text-status-red" : "text-status-green"}`}>{avgUtil}%</span>
                        </div>
                        <Badge status={isOver ? "Overdue" : "OK"} className="w-16 justify-center text-[10px]">{isOver ? t("over") : t("ok")}</Badge>
                      </div>
                    );
                  })
                )}
              </div>
            </TabsContent>

            <TabsContent value="demand" className="mt-4">
              <div className="nexus-table-surface">
                <div className="nexus-table-header">
                  {t("demandVsCapacity")}
                </div>
                {demandLoading ? (
                  <div className="space-y-2 p-4">
                    {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-12 w-full" />)}
                  </div>
                ) : !demandVsCapacity?.groups.length ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">{t("noDemandData")}</div>
                ) : (
                  <>
                    <div className="nexus-table-header grid grid-cols-5 gap-2">
                      <div className="col-span-2">{t("group")}</div>
                      <div className="col-span-1 text-right">{t("demandLabel")}</div>
                      <div className="col-span-1 text-right">{t("capacityLabelShort")}</div>
                      <div className="col-span-1 text-right">{t("gapLabel")}</div>
                    </div>
                    {demandVsCapacity.groups.map((g, i) => (
                      <div key={i} className="nexus-table-row grid grid-cols-5 gap-2 px-4 py-3 text-sm last:border-0">
                        <div className="col-span-2 flex items-center gap-2 font-medium">
                          <span className={`text-xs font-medium ${getLookupTextClass(g.groupType)}`}>{g.groupType}</span>
                          {g.groupName}
                        </div>
                        <div className="col-span-1 text-right">{Math.round(g.demandHours).toLocaleString()} {tc("hours")}</div>
                        <div className="col-span-1 text-right">{Math.round(g.capacityHours).toLocaleString()} {tc("hours")}</div>
                        <div className="col-span-1 text-right">
                          <span className={g.gapHours < 0 ? "text-status-red" : "text-status-green"}>
                            {g.gapHours >= 0 ? "+" : ""}{Math.round(g.gapHours).toLocaleString()} {tc("hours")}
                          </span>
                        </div>
                      </div>
                    ))}
                  </>
                )}
              </div>
            </TabsContent>

            <TabsContent value="availability" className="mt-4">
              <div className="nexus-table-surface">
                <div className="nexus-table-header">{t("resourceAvailability")}</div>
                {availLoading ? (
                  <div className="space-y-2 p-4">
                    {Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-12 w-full" />)}
                  </div>
                ) : !availability?.items.length ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">{t("noAvailabilityData")}</div>
                ) : (
                  <>
                    <div className="nexus-table-header grid grid-cols-12 gap-2">
                      <div className="col-span-3">{t("resourceLabel")}</div>
                      <div className="col-span-2">{t("roleLabel")}</div>
                      <div className="col-span-2">{t("deptLabel")}</div>
                      <div className="col-span-2">{t("availableLabel")}</div>
                      <div className="col-span-2">{t("utilization")}</div>
                      <div className="col-span-1">{t("statusLabel")}</div>
                    </div>
                    {availability.items.slice(0, 15).map((r) => (
                      <div key={r.resourceId} className="nexus-table-row grid grid-cols-12 gap-2 px-4 py-3 text-sm">
                        <div className="col-span-3 font-medium">{r.resourceName}</div>
                        <div className="col-span-2 text-muted-foreground">{r.jobTitle ?? "-"}</div>
                        <div className="col-span-2 text-muted-foreground">{r.departmentName ?? "-"}</div>
                        <div className="col-span-2">{Math.round(r.availableCapacityHours)} {tc("hours")}</div>
                        <div className="col-span-2">
                          <div className="flex items-center gap-2">
                            <Progress value={Math.min(r.currentUtilization, 100)} className="h-2 w-12" />
                            <span className="text-xs">{Math.round(r.currentUtilization)}%</span>
                          </div>
                        </div>
                        <div className="col-span-1">
                          <span className={`inline-block h-2 w-2 rounded-full ${r.currentUtilization > 80 ? "bg-status-red" : r.currentUtilization > 60 ? "bg-status-amber" : "bg-status-green"}`} />
                        </div>
                      </div>
                    ))}
                  </>
                )}
              </div>
            </TabsContent>
          </Tabs>
        </>
      ) : (
        <div className="space-y-4">
          <ResourceGanttView
            data={ganttData}
            isLoading={ganttLoading}
            periodType={periodType}
            startDate={new Date(startDate || today)}
            endDate={new Date(endDate || today)}
          />
        </div>
      )}

      {canAssignResources && <AllocationDialog open={showCreateDialog} onOpenChange={setShowCreateDialog} />}
    </div>
  );
}
