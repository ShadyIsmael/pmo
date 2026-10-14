import * as React from "react";
import { Filter, TrendingUp } from "lucide-react";
import { PageHeader } from "@components/layout/PageHeader";
import { Avatar, AvatarFallback } from "@components/ui/Avatar";
import { Button } from "@components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@components/ui/Card";
import { Progress } from "@components/ui/Progress";
import { Skeleton } from "@components/ui/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import { useCapacityAvailability } from "./api/capacity";
import { useDepartments } from "@features/teams/api/teams";

function initials(name: string): string {
  return name.trim().split(/\s+/).map((part) => part[0]).join("").slice(0, 2).toUpperCase() || "—";
}

export function ResourceUtilizationPage() {
  const { t, formatPercent, formatNumber } = useI18n();
  const [departmentId, setDepartmentId] = React.useState("");
  const query = useCapacityAvailability({ departmentId: departmentId || undefined });
  const departmentsQuery = useDepartments(false);
  const items = [...(query.data?.items ?? [])].sort((left, right) => right.currentUtilization - left.currentUtilization);
  const average = items.length ? Math.round(items.reduce((sum, item) => sum + item.currentUtilization, 0) / items.length) : 0;
  const overAllocated = items.filter((item) => item.currentUtilization > 80).length;
  const underUtilized = items.filter((item) => item.currentUtilization < 40).length;
  const statusClass = (value: number) => value > 80 ? "text-destructive" : value > 60 ? "text-warning" : "text-success";
  const statusLabel = (value: number) => value > 80 ? t("resources.utilization.over") : value > 60 ? t("resources.utilization.atRisk") : t("resources.utilization.ok");

  return <div className="space-y-6">
    <PageHeader
      title={t("resources.utilization.title")}
      description={t("resources.utilization.description")}
      actions={<div className="flex items-center gap-2"><select className="input-field max-w-48" value={departmentId} onChange={(event) => setDepartmentId(event.target.value)} aria-label={t("resources.utilization.department")}><option value="">{t("resources.utilization.allDepartments")}</option>{(departmentsQuery.data ?? []).map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select><Button variant="outline" size="sm"><Filter className="me-2 h-4 w-4" />{t("resources.capacity.filter")}</Button></div>}
    />

    <div className="grid gap-4 md:grid-cols-3">
      <Card><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">{t("resources.utilization.average")}</CardTitle><TrendingUp className="h-4 w-4 text-muted-foreground" /></CardHeader><CardContent>{query.isLoading ? <Skeleton className="h-8 w-24" /> : <><p className="text-2xl font-bold tabular-nums">{formatPercent(average)}</p><Progress value={average} className="mt-2 h-2" /></>}</CardContent></Card>
      <Card><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">{t("resources.utilization.overAllocated")}</CardTitle><TrendingUp className="h-4 w-4 text-muted-foreground" /></CardHeader><CardContent>{query.isLoading ? <Skeleton className="h-8 w-16" /> : <><p className="text-2xl font-bold text-destructive">{formatNumber(overAllocated)}</p><p className="text-xs text-muted-foreground">{t("resources.utilization.aboveEighty")}</p></>}</CardContent></Card>
      <Card><CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">{t("resources.utilization.underUtilized")}</CardTitle><TrendingUp className="h-4 w-4 text-muted-foreground" /></CardHeader><CardContent>{query.isLoading ? <Skeleton className="h-8 w-16" /> : <><p className="text-2xl font-bold text-warning">{formatNumber(underUtilized)}</p><p className="text-xs text-muted-foreground">{t("resources.utilization.belowForty")}</p></>}</CardContent></Card>
    </div>

    <Card><CardContent className="p-0">
      {query.isLoading ? <Table><TableHeader><TableRow><TableHead>{t("resources.utilization.resource")}</TableHead><TableHead>{t("resources.utilization.role")}</TableHead><TableHead>{t("resources.utilization.department")}</TableHead><TableHead>{t("resources.utilization.allocation")}</TableHead><TableHead>{t("resources.utilization.available")}</TableHead><TableHead>{t("common.status")}</TableHead></TableRow></TableHeader><TableBody>{Array.from({ length: 8 }, (_, index) => <TableRow key={index}>{Array.from({ length: 6 }, (_, cell) => <TableCell key={cell}><Skeleton className="h-4 w-full max-w-32" /></TableCell>)}</TableRow>)}</TableBody></Table>
        : query.isError ? <p className="py-12 text-center text-sm text-destructive">{t("common.errorLoading")}</p>
          : items.length === 0 ? <p className="py-12 text-center text-sm text-muted-foreground">{t("resources.capacity.noAvailabilityData")}</p>
            : <Table><TableHeader><TableRow><TableHead>{t("resources.utilization.resource")}</TableHead><TableHead>{t("resources.utilization.role")}</TableHead><TableHead>{t("resources.utilization.department")}</TableHead><TableHead>{t("resources.utilization.allocation")}</TableHead><TableHead>{t("resources.utilization.available")}</TableHead><TableHead>{t("common.status")}</TableHead></TableRow></TableHeader><TableBody>{items.slice(0, 15).map((item) => <TableRow key={item.resourceId}>
              <TableCell><div className="flex items-center gap-2 font-medium"><Avatar className="h-6 w-6"><AvatarFallback className="text-[8px]">{initials(item.resourceName)}</AvatarFallback></Avatar>{item.resourceName}</div></TableCell>
              <TableCell className="text-muted-foreground">{item.jobTitle ?? "—"}</TableCell>
              <TableCell className="text-muted-foreground">{item.departmentName ?? "—"}</TableCell>
              <TableCell><div className="flex min-w-32 items-center gap-2"><Progress value={Math.min(item.currentUtilization, 100)} className="h-2 w-16" /><span className={`text-xs font-medium tabular-nums ${statusClass(item.currentUtilization)}`}>{formatPercent(item.currentUtilization)}</span></div></TableCell>
              <TableCell><div className="flex items-center gap-2"><Progress value={Math.min(item.availableCapacityHours > 0 ? 100 - item.currentUtilization : 0, 100)} className="h-2 w-16" /><span className="whitespace-nowrap text-xs">{formatNumber(Math.round(item.availableCapacityHours))} {t("resources.capacity.hoursAbbr")}</span></div></TableCell>
              <TableCell><span aria-hidden="true" className={`inline-block h-2 w-2 rounded-full ${item.currentUtilization > 80 ? "bg-destructive" : item.currentUtilization > 60 ? "bg-warning" : "bg-success"}`} /><span className={`ms-1 text-xs ${statusClass(item.currentUtilization)}`}>{statusLabel(item.currentUtilization)}</span></TableCell>
            </TableRow>)}</TableBody></Table>}
    </CardContent></Card>
  </div>;
}
