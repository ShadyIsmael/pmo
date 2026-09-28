"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Clock, Plus, PauseCircle } from "lucide-react";

const scheduledReports = [
  { id: "sch-1", name: "Weekly Portfolio Dashboard", frequency: "Every Monday", recipients: 12, nextRun: "2026-08-04", active: true },
  { id: "sch-2", name: "Monthly Executive Summary", frequency: "1st of Month", recipients: 8, nextRun: "2026-08-01", active: true },
  { id: "sch-3", name: "Quarterly Board Report", frequency: "Quarterly", recipients: 15, nextRun: "2026-10-01", active: false },
];

export default function ScheduledReportsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Scheduled Reports"
        description="Automated report distribution"
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />Schedule Report</Button>}
      />

      <div className="space-y-3">
        {scheduledReports.map((sr) => (
          <Card key={sr.id}>
            <CardContent className="flex items-center gap-4 py-4">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
                <Clock className="h-5 w-5 text-muted-foreground" />
              </div>
              <div className="flex-1">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium">{sr.name}</p>
                  <Badge status={sr.active ? "Active" : "Paused"} className="text-[10px]">{sr.active ? "Active" : "Paused"}</Badge>
                </div>
                <p className="text-xs text-muted-foreground">{sr.frequency} · {sr.recipients} recipients · Next: {sr.nextRun}</p>
              </div>
              <Button size="sm" variant="outline">{sr.active ? <PauseCircle className="mr-1 h-4 w-4" /> : <Clock className="mr-1 h-4 w-4" />}{sr.active ? "Pause" : "Resume"}</Button>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
