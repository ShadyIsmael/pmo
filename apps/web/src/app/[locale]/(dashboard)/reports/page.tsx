"use client";

import Link from "next/link";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { BarChart3, FileText, Plus, Download, Clock, ArrowRight } from "lucide-react";

const reportCategories = [
  { id: "executive", name: "Executive Reports", count: 4, icon: "📊" },
  { id: "portfolio", name: "Portfolio Reports", count: 6, icon: "📁" },
  { id: "project", name: "Project Reports", count: 8, icon: "📋" },
  { id: "financial", name: "Financial Reports", count: 5, icon: "💰" },
  { id: "risk", name: "Risk Reports", count: 3, icon: "⚠️" },
  { id: "custom", name: "Custom Reports", count: 12, icon: "📝" },
];

const recentReports = [
  { id: "rpt-1", name: "Monthly Portfolio Dashboard", type: "Executive", lastRun: "2026-07-29", status: "Ready" },
  { id: "rpt-2", name: "Q2 Financial Summary", type: "Financial", lastRun: "2026-07-25", status: "Ready" },
  { id: "rpt-3", name: "Risk Register Export", type: "Risk", lastRun: "2026-07-28", status: "Ready" },
  { id: "rpt-4", name: "Resource Utilization Report", type: "Project", lastRun: "2026-07-27", status: "Scheduled" },
  { id: "rpt-5", name: "Project Health Overview", type: "Portfolio", lastRun: "2026-07-29", status: "Ready" },
];

export default function ReportsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Reports"
        description="Create, schedule, and export reports"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" size="sm"><Clock className="mr-2 h-4 w-4" />Schedule</Button>
            <Button size="sm"><Plus className="mr-2 h-4 w-4" />New Report</Button>
          </div>
        }
      />

      <div className="grid gap-4 md:grid-cols-3 lg:grid-cols-6">
        {reportCategories.map((cat) => (
          <Card key={cat.id} className="cursor-pointer hover:bg-muted/50 transition-colors">
            <CardContent className="flex flex-col items-center gap-2 py-6">
              <span className="text-2xl">{cat.icon}</span>
              <p className="text-sm font-medium text-center">{cat.name}</p>
              <p className="text-xs text-muted-foreground">{cat.count} reports</p>
            </CardContent>
          </Card>
        ))}
      </div>

      <Tabs defaultValue="recent">
        <TabsList>
          <TabsTrigger value="recent">Recent Reports</TabsTrigger>
          <TabsTrigger value="scheduled">Scheduled</TabsTrigger>
          <TabsTrigger value="templates">Report Templates</TabsTrigger>
        </TabsList>

        <TabsContent value="recent">
          <div className="rounded-md border">
            {recentReports.map((rpt) => (
              <div key={rpt.id} className="flex items-center gap-4 border-b px-4 py-3 last:border-0 hover:bg-muted/30">
                <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-muted">
                  <FileText className="h-4 w-4" />
                </div>
                <div className="flex-1">
                  <p className="text-sm font-medium">{rpt.name}</p>
                  <p className="text-xs text-muted-foreground">{rpt.type} · Last run: {rpt.lastRun}</p>
                </div>
                <Badge variant={rpt.status === "Ready" ? "default" : "secondary"} className="text-[10px]">{rpt.status}</Badge>
                <Button size="sm" variant="outline"><Download className="mr-1 h-4 w-4" />Download</Button>
                <Button size="sm" variant="ghost"><ArrowRight className="h-4 w-4" /></Button>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="scheduled">
          <Card><CardContent className="py-8 text-center text-muted-foreground">No scheduled reports yet</CardContent></Card>
        </TabsContent>

        <TabsContent value="templates">
          <div className="grid gap-4 md:grid-cols-3">
            {["Executive Dashboard", "Portfolio Summary", "Project Status"].map((name) => (
              <Card key={name}>
                <CardHeader><CardTitle className="text-base">{name}</CardTitle><CardDescription>Pre-built report template</CardDescription></CardHeader>
                <CardContent><Button size="sm" variant="outline" className="w-full">Use Template</Button></CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
