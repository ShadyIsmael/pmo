"use client";

import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Filter, TrendingUp } from "lucide-react";
import { getInitials } from "@/lib/utils";
import { useCapacityAvailability } from "@/hooks/resources/useCapacity";

export default function UtilizationPage() {
  const [departmentId, setDepartmentId] = useState("");
  const { data: availability, isLoading } = useCapacityAvailability({ departmentId: departmentId || undefined });

  const items = availability?.items ?? [];
  const sorted = [...items].sort((a, b) => b.currentUtilization - a.currentUtilization);

  const avgUtilization = items.length > 0
    ? Math.round(items.reduce((s, r) => s + r.currentUtilization, 0) / items.length)
    : 0;
  const overAllocated = items.filter((r) => r.currentUtilization > 80).length;
  const underUtilized = items.filter((r) => r.currentUtilization < 40).length;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Resource Utilization"
        description="Track resource allocation and utilization rates"
        actions={
          <div className="flex items-center gap-2">
            <Select value={departmentId} onValueChange={setDepartmentId}>
              <SelectTrigger className="w-40">
                <SelectValue placeholder="All Departments" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Departments</SelectItem>
              </SelectContent>
            </Select>
            <Button variant="outline" size="sm">
              <Filter className="mr-2 h-4 w-4" />Filter
            </Button>
          </div>
        }
      />

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Avg Utilization</CardTitle>
            <TrendingUp className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-8 w-24" />
            ) : (
              <>
                <div className="text-2xl font-bold">{avgUtilization}%</div>
                <Progress value={avgUtilization} className="mt-2 h-2" />
              </>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Over-allocated</CardTitle>
            <TrendingUp className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-8 w-16" />
            ) : (
              <>
                <div className="text-2xl font-bold text-status-red">{overAllocated}</div>
                <p className="text-xs text-muted-foreground">Resources above 80%</p>
              </>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Under-utilized</CardTitle>
            <TrendingUp className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-8 w-16" />
            ) : (
              <>
                <div className="text-2xl font-bold text-status-amber">{underUtilized}</div>
                <p className="text-xs text-muted-foreground">Resources below 40%</p>
              </>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="nexus-table-surface">
        <div className="nexus-table-header grid grid-cols-12 gap-2">
          <div className="col-span-3">Resource</div>
          <div className="col-span-2">Role</div>
          <div className="col-span-2">Department</div>
          <div className="col-span-2">Allocation</div>
          <div className="col-span-2">Available</div>
          <div className="col-span-1">Status</div>
        </div>
        {isLoading ? (
          <div className="space-y-2 p-4">
            {Array.from({ length: 8 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        ) : sorted.length === 0 ? (
          <div className="p-6 text-center text-sm text-muted-foreground">
            No utilization data available
          </div>
        ) : (
          sorted.slice(0, 15).map((res) => (
            <div key={res.resourceId} className="nexus-table-row grid grid-cols-12 gap-2 px-4 py-3 text-sm">
              <div className="col-span-3 flex items-center gap-2 font-medium">
                <Avatar className="h-6 w-6">
                  <AvatarFallback className="text-[8px]">{getInitials(res.resourceName)}</AvatarFallback>
                </Avatar>
                {res.resourceName}
              </div>
              <div className="col-span-2 text-muted-foreground">{res.jobTitle ?? "-"}</div>
              <div className="col-span-2 text-muted-foreground">{res.departmentName ?? "-"}</div>
              <div className="col-span-2">
                <div className="flex items-center gap-2">
                  <Progress value={Math.min(res.currentUtilization, 100)} className="h-2 w-16" />
                  <span className={`text-xs font-medium ${
                    res.currentUtilization > 80 ? "text-status-red" :
                    res.currentUtilization > 60 ? "text-status-amber" : "text-status-green"
                  }`}>{Math.round(res.currentUtilization)}%</span>
                </div>
              </div>
              <div className="col-span-2">
                <div className="flex items-center gap-2">
                  <Progress value={Math.min(res.availableCapacityHours > 0 ? 100 - res.currentUtilization : 0, 100)} className="h-2 w-16" />
                  <span className="text-xs">{Math.round(res.availableCapacityHours)} hrs</span>
                </div>
              </div>
              <div className="col-span-1">
                <span className={`inline-block h-2 w-2 rounded-full ${
                  res.currentUtilization > 80 ? "bg-status-red" :
                  res.currentUtilization > 60 ? "bg-status-amber" : "bg-status-green"
                }`} />
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
