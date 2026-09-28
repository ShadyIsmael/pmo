"use client";

import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Filter, ZoomOut, CalendarDays } from "lucide-react";
import { formatDate } from "@/lib/utils";
import { mockProjects } from "@/lib/mock-data";

export default function GanttPage() {
  const [project, setProject] = useState(mockProjects[0]!.id);
  const selected = mockProjects.find(p => p.id === project) ?? mockProjects[0]!;

  const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
  const startYear = 2025;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Gantt View"
        description="Visual timeline of project schedules"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" size="sm"><ZoomOut className="mr-1 h-4 w-4" />Zoom</Button>
            <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
          </div>
        }
      />

      <div className="flex items-center gap-2">
        <Select value={project} onValueChange={setProject}>
          <SelectTrigger className="w-[300px]">
            <SelectValue placeholder="Select project" />
          </SelectTrigger>
          <SelectContent>
            {mockProjects.filter(p => p.status !== "Completed").map((p) => (
              <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="text-base">{selected.name}</CardTitle>
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <CalendarDays className="h-3.5 w-3.5" />
              {formatDate(selected.startDate)} - {formatDate(selected.plannedFinish)}
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto">
            <div className="min-w-[800px]">
              <div className="flex border-b">
                <div className="w-64 shrink-0 border-r px-3 py-2 text-xs font-medium text-muted-foreground">Task / Phase</div>
                {months.slice(0, 12).map((m, i) => (
                  <div key={m} className="flex-1 border-r px-2 py-2 text-center text-[10px] font-medium text-muted-foreground last:border-r-0">
                    {m} {startYear + (i > 5 ? 1 : 0)}
                  </div>
                ))}
              </div>
              {[
                { name: "Requirements", start: 0, duration: 3, progress: 100, health: "green" },
                { name: "Architecture Design", start: 2, duration: 4, progress: 100, health: "green" },
                { name: "Development", start: 5, duration: 8, progress: 75, health: "amber" },
                { name: "Testing", start: 10, duration: 4, progress: 40, health: "amber" },
                { name: "Deployment", start: 13, duration: 2, progress: 0, health: "green" },
                { name: "Milestone: Phase 1 Complete", start: 3, duration: 0, progress: 100, health: "green", isMilestone: true },
                { name: "Milestone: UAT Sign-off", start: 12, duration: 0, progress: 0, health: "green", isMilestone: true },
              ].map((phase, i) => (
                <div key={i} className="flex items-center border-b py-2 text-sm last:border-0 hover:bg-muted/30">
                  <div className="flex w-64 shrink-0 items-center gap-2 border-r px-3">
                    {phase.isMilestone ? (
                      <span className="h-3 w-3 rounded-full bg-primary" />
                    ) : (
                      <span className={`h-2.5 w-2.5 rounded-full bg-status-${phase.health}`} />
                    )}
                    <span className={`text-xs ${phase.isMilestone ? "font-medium text-primary" : ""}`}>{phase.name}</span>
                  </div>
                  {months.slice(0, 12).map((_, mi) => {
                    const isActive = mi >= phase.start && mi < phase.start + phase.duration;
                    const bgClass = phase.isMilestone ? "" :
                      phase.health === "green" ? "bg-status-green" :
                      phase.health === "amber" ? "bg-status-amber" : "bg-status-red";
                    return (
                      <div key={mi} className="relative flex-1 border-r px-1 py-1 last:border-r-0">
                        {isActive && (
                          <div
                            className={`h-6 rounded ${bgClass} ${phase.isMilestone ? "h-3 w-3 rounded-full mx-auto" : ""}`}
                            style={{ width: phase.isMilestone ? "" : `${phase.progress}%`, minWidth: phase.isMilestone ? "" : "20px", opacity: phase.isMilestone ? 1 : 0.7 }}
                          />
                        )}
                      </div>
                    );
                  })}
                </div>
              ))}
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
