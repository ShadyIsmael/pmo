"use client";

import { useState, useMemo, useRef, useEffect } from "react";
import { useTranslations } from "next-intl";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { ExternalLink, Edit, AlertTriangle, Users } from "lucide-react";
import { AllocationDto } from "@/hooks/resources/useAllocations";
import { AllocationDialog } from "./AllocationDialog";
import { formatDate } from "@/lib/utils";

interface ResourceGroup {
  resourceId: string;
  resourceName: string;
  resourceJobTitle?: string;
  allocations: AllocationDto[];
}

interface ResourceGanttViewProps {
  data: AllocationDto[] | undefined;
  isLoading: boolean;
  periodType: string;
  startDate: Date;
  endDate: Date;
}

const STATUS_COLORS: Record<string, string> = {
  Confirmed: "bg-blue-500/80 hover:bg-blue-500 border-blue-600",
  Proposed: "bg-amber-400/80 hover:bg-amber-400 border-amber-500",
  Tentative: "bg-purple-400/80 hover:bg-purple-400 border-purple-500",
  Cancelled: "bg-gray-300/60 hover:bg-gray-300 border-gray-400",
};

const DAY_WIDTH = 28;
const ROW_HEIGHT = 48;
const LABEL_WIDTH = 200;

function groupByResource(allocations: AllocationDto[]): ResourceGroup[] {
  const map = new Map<string, ResourceGroup>();
  for (const a of allocations) {
    const existing = map.get(a.resourceId);
    if (existing) {
      existing.allocations.push(a);
    } else {
      map.set(a.resourceId, {
        resourceId: a.resourceId,
        resourceName: a.resourceName,
        resourceJobTitle: a.resourceJobTitle,
        allocations: [a],
      });
    }
  }
  return Array.from(map.values());
}

function getTimelineDays(start: Date, end: Date): Date[] {
  const days: Date[] = [];
  const current = new Date(start);
  while (current <= end) {
    days.push(new Date(current));
    current.setDate(current.getDate() + 1);
  }
  return days;
}

function getBarLeft(start: Date, timelineStart: Date, totalDays: number): number {
  const diff = (start.getTime() - timelineStart.getTime()) / (1000 * 60 * 60 * 24);
  return (diff / totalDays) * 100;
}

function getBarWidth(allocation: AllocationDto, timelineStart: Date, totalDays: number): number {
  const start = new Date(allocation.startDate);
  const end = new Date(allocation.endDate);
  const allocDays = Math.max(1, (end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24));
  return (allocDays / totalDays) * 100;
}

function hasConflict(group: ResourceGroup): boolean {
  const sorted = [...group.allocations].sort((a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime());
  for (let i = 1; i < sorted.length; i++) {
    const prev = sorted[i - 1];
    const curr = sorted[i];
    if (prev && curr && new Date(curr.startDate) < new Date(prev.endDate)) return true;
  }
  return false;
}

export function ResourceGanttView({ data, isLoading, periodType, startDate, endDate }: ResourceGanttViewProps) {
  const t = useTranslations("resources.gantt");
  const tc = useTranslations("common");
  const [editingAllocation, setEditingAllocation] = useState<AllocationDto | null>(null);
  const [showEditDialog, setShowEditDialog] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  const groups = useMemo(() => data ? groupByResource(data) : [], [data]);
  const timelineStart = startDate;
  const timelineEnd = endDate;
  const days = useMemo(() => getTimelineDays(timelineStart, timelineEnd), [timelineStart.getTime(), timelineEnd.getTime()]);
  const totalDays = days.length;
  const today = new Date();
  const todayOffset = ((today.getTime() - timelineStart.getTime()) / (1000 * 60 * 60 * 24) / totalDays) * 100;

  const monthLabels = useMemo(() => {
    const labels: { label: string; left: number; width: number }[] = [];
    if (periodType === "quarter") {
      const quarters: Record<string, { left: number; width: number }> = {};
      for (const d of days) {
        const key = `Q${Math.floor(d.getMonth() / 3) + 1} ${d.getFullYear()}`;
        const pct = ((d.getTime() - timelineStart.getTime()) / (1000 * 60 * 60 * 24) / totalDays) * 100;
        if (!quarters[key]) quarters[key] = { left: pct, width: 0 };
        quarters[key].width = pct - quarters[key].left + (100 / totalDays);
      }
      return Object.entries(quarters).map(([label, pos]) => ({ label, left: pos.left, width: pos.width }));
    }
    const months: Record<string, { left: number; width: number }> = {};
    for (const d of days) {
      const key = `${d.getFullYear()}-${d.getMonth()}`;
      const pct = ((d.getTime() - timelineStart.getTime()) / (1000 * 60 * 60 * 24) / totalDays) * 100;
      if (!months[key]) months[key] = { left: pct, width: 0 };
      months[key].width = pct - months[key].left + (100 / totalDays);
    }
    return Object.entries(months).map(([key, pos]) => ({
      label: new Date(parseInt(key.split("-")[0] ?? "0"), parseInt(key.split("-")[1] ?? "0")).toLocaleString("default", { month: "short", year: "2-digit" }),
      left: pos.left,
      width: pos.width,
    }));
  }, [days, timelineStart.getTime(), timelineEnd.getTime(), totalDays, periodType]);

  if (isLoading) {
    return (
      <div className="space-y-2 p-4">
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    );
  }

  if (!data || data.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-sm text-muted-foreground">
        <Users className="mb-2 h-8 w-8" />
        <p>{t("noData")}</p>
      </div>
    );
  }

  return (
    <>
      <div className="overflow-hidden rounded-md border" dir="ltr">
        <div className="flex" ref={scrollRef}>
          <div className="flex-shrink-0 border-r bg-muted/30" style={{ width: LABEL_WIDTH }}>
            <div className="flex h-10 items-center border-b px-3 text-xs font-medium text-muted-foreground">{t("resource")}</div>
            {groups.map((g) => (
              <div
                key={g.resourceId}
                className="flex items-center gap-2 border-b px-3 text-sm"
                style={{ height: ROW_HEIGHT }}
              >
                <span className="truncate font-medium">{g.resourceName}</span>
                {hasConflict(g) && (
                  <AlertTriangle className="h-3.5 w-3.5 flex-shrink-0 text-status-amber" />
                )}
                <span className="ml-auto text-xs text-muted-foreground">{g.resourceJobTitle ?? ""}</span>
              </div>
            ))}
          </div>

          <div className="relative flex-1 overflow-x-auto">
            <div className="relative" style={{ minWidth: totalDays * DAY_WIDTH }}>
              <div className="sticky top-0 z-10 flex h-10 border-b bg-background">
                {monthLabels.map((m, i) => (
                  <div
                    key={i}
                    className="absolute top-0 flex h-full items-center border-r px-2 text-xs font-medium text-muted-foreground"
                    style={{ left: `${m.left}%`, width: `${m.width}%` }}
                  >
                    {m.label}
                  </div>
                ))}
              </div>

              {today >= timelineStart && today <= timelineEnd && (
                <div
                  className="absolute top-0 z-20 h-full w-px bg-red-400"
                  style={{ left: `${todayOffset}%` }}
                  title={t("today")}
                />
              )}

              <div className="relative">
                {groups.map((g) => (
                  <div key={g.resourceId} className="relative border-b" style={{ height: ROW_HEIGHT }}>
                    {g.allocations.map((a) => {
                      const left = getBarLeft(new Date(a.startDate), timelineStart, totalDays);
                      const width = getBarWidth(a, timelineStart, totalDays);
                      return (
                        <Popover key={a.id}>
                          <PopoverTrigger asChild>
                            <button
                              className={`absolute top-1/2 -translate-y-1/2 h-8 rounded border px-1 text-[10px] leading-tight text-white overflow-hidden cursor-pointer transition-all hover:z-10 hover:shadow-md ${STATUS_COLORS[a.status] || "bg-gray-400"}`}
                              style={{ left: `${Math.max(0, left)}%`, width: `${Math.max(2, width)}%` }}
                            >
                              {width > 8 && (
                                <span className="truncate block">{a.projectName || "Unnamed"} ({Math.round(a.allocationPercentage)}%)</span>
                              )}
                            </button>
                          </PopoverTrigger>
                          <PopoverContent className="w-72 p-3" side="top">
                            <div className="space-y-2">
                              <div className="flex items-center justify-between">
                                <span className="font-medium">{a.projectName || t("unnamedProject")}</span>
                                <Badge variant={
                                  a.status === "Confirmed" ? "default" :
                                  a.status === "Proposed" ? "secondary" :
                                  a.status === "Tentative" ? "outline" : "secondary"
                                } className="text-[10px]">{t(a.status.toLowerCase() === "confirmed" ? "confirmed" : a.status.toLowerCase() === "proposed" ? "proposed" : a.status.toLowerCase() === "tentative" ? "tentative" : "cancelled")}</Badge>
                              </div>
                              <div className="grid grid-cols-2 gap-1 text-xs">
                                <span className="text-muted-foreground">{t("role")}:</span><span>{a.role}</span>
                                <span className="text-muted-foreground">{t("allocation")}:</span><span>{Math.round(a.allocationPercentage)}%{a.hoursPerWeek ? ` (${a.hoursPerWeek}h/wk)` : ""}</span>
                                <span className="text-muted-foreground">{t("start")}:</span><span>{formatDate(a.startDate)}</span>
                                <span className="text-muted-foreground">{t("end")}:</span><span>{formatDate(a.endDate)}</span>
                                <span className="text-muted-foreground">{t("billable")}:</span><span>{a.isBillable ? tc("yes") : tc("no")}</span>
                              </div>
                              {a.notes && <p className="text-xs text-muted-foreground">{a.notes}</p>}
                              <div className="flex items-center gap-2 pt-1">
                                <Button variant="ghost" size="sm" className="h-7 text-xs" asChild>
                                  <a href={`/resources/${a.resourceId}`}><ExternalLink className="mr-1 h-3 w-3" />{t("openResource")}</a>
                                </Button>
                                {a.projectId && (
                                  <Button variant="ghost" size="sm" className="h-7 text-xs" asChild>
                                    <a href={`/projects/${a.projectId}`}><ExternalLink className="mr-1 h-3 w-3" />{t("openProject")}</a>
                                  </Button>
                                )}
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  className="h-7 text-xs"
                                  onClick={() => { setEditingAllocation(a); setShowEditDialog(true); }}
                                >
                                  <Edit className="mr-1 h-3 w-3" />{t("edit")}
                                </Button>
                              </div>
                            </div>
                          </PopoverContent>
                        </Popover>
                      );
                    })}
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      </div>

      {showEditDialog && editingAllocation && (
        <AllocationDialog
          open={showEditDialog}
          onOpenChange={setShowEditDialog}
          allocation={editingAllocation}
        />
      )}
    </>
  );
}
