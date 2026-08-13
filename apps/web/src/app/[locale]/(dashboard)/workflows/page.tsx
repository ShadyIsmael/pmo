"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { GitBranch, Plus, Play, PauseCircle, CheckCircle2, AlertCircle, Clock } from "lucide-react";

const workflows = [
  { id: "wf-1", name: "Project Approval", status: "Active", trigger: "New Project Created", steps: 4, lastRun: "2026-07-29", runs: 145 },
  { id: "wf-2", name: "Budget Change Request", status: "Active", trigger: "Budget Change > 10%", steps: 5, lastRun: "2026-07-28", runs: 32 },
  { id: "wf-3", name: "Risk Escalation", status: "Active", trigger: "Risk Score >= 15", steps: 3, lastRun: "2026-07-28", runs: 18 },
  { id: "wf-4", name: "Resource Request", status: "Active", trigger: "New Resource Request", steps: 4, lastRun: "2026-07-27", runs: 67 },
  { id: "wf-5", name: "Issue Resolution", status: "Paused", trigger: "Critical Issue Logged", steps: 4, lastRun: "2026-07-25", runs: 23 },
  { id: "wf-6", name: "Milestone Sign-off", status: "Active", trigger: "Milestone Complete", steps: 3, lastRun: "2026-07-29", runs: 89 },
  { id: "wf-7", name: "Vendor Onboarding", status: "Draft", trigger: "New Vendor", steps: 6, lastRun: "-", runs: 0 },
  { id: "wf-8", name: "Project Closure", status: "Active", trigger: "Project Complete", steps: 5, lastRun: "2026-07-20", runs: 12 },
];

export default function WorkflowsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Workflows"
        description="Automated business processes and approval flows"
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />New Workflow</Button>}
      />

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Total</CardTitle>
            <GitBranch className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold">{workflows.length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Active</CardTitle>
            <Play className="h-4 w-4 text-status-green" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold text-status-green">{workflows.filter(w => w.status === "Active").length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Paused</CardTitle>
            <PauseCircle className="h-4 w-4 text-status-amber" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold text-status-amber">{workflows.filter(w => w.status === "Paused").length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Total Runs</CardTitle>
            <CheckCircle2 className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold">{workflows.reduce((s, w) => s + w.runs, 0)}</div></CardContent>
        </Card>
      </div>

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All</TabsTrigger>
          <TabsTrigger value="active">Active</TabsTrigger>
          <TabsTrigger value="paused">Paused</TabsTrigger>
          <TabsTrigger value="draft">Draft</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="grid gap-4 md:grid-cols-2">
            {workflows.map((wf) => (
              <Card key={wf.id}>
                <CardHeader>
                  <div className="flex items-start justify-between">
                    <div>
                      <CardTitle className="text-base">{wf.name}</CardTitle>
                      <CardDescription>Trigger: {wf.trigger}</CardDescription>
                    </div>
                    <Badge variant={wf.status === "Active" ? "default" : wf.status === "Paused" ? "secondary" : "outline"}>{wf.status}</Badge>
                  </div>
                </CardHeader>
                <CardContent>
                  <div className="grid grid-cols-3 gap-3 text-sm">
                    <div>
                      <span className="text-xs text-muted-foreground">Steps</span>
                      <p className="font-medium">{wf.steps}</p>
                    </div>
                    <div>
                      <span className="text-xs text-muted-foreground">Runs</span>
                      <p className="font-medium">{wf.runs}</p>
                    </div>
                    <div>
                      <span className="text-xs text-muted-foreground">Last Run</span>
                      <p className="font-medium text-xs">{wf.lastRun}</p>
                    </div>
                  </div>
                  <div className="mt-3 flex gap-2">
                    {wf.status === "Active" && <Button size="sm" variant="outline"><PauseCircle className="mr-1 h-4 w-4" />Pause</Button>}
                    {wf.status === "Paused" && <Button size="sm" variant="outline"><Play className="mr-1 h-4 w-4" />Resume</Button>}
                    <Button size="sm" variant="outline">Edit</Button>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="active">
          <div className="grid gap-4 md:grid-cols-2">
            {workflows.filter(w => w.status === "Active").map((wf) => (
              <Card key={wf.id}>
                <CardHeader><CardTitle className="text-base">{wf.name}</CardTitle><CardDescription>{wf.trigger}</CardDescription></CardHeader>
                <CardContent className="space-y-2">
                  <div className="flex justify-between text-sm"><span className="text-muted-foreground">Steps</span><span>{wf.steps}</span></div>
                  <div className="flex justify-between text-sm"><span className="text-muted-foreground">Runs</span><span>{wf.runs}</span></div>
                  <Button size="sm" variant="outline" className="mt-2">Manage</Button>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="paused">
          <Card><CardContent className="py-8 text-center text-muted-foreground">No paused workflows</CardContent></Card>
        </TabsContent>

        <TabsContent value="draft">
          <Card><CardContent className="py-8 text-center text-muted-foreground">No draft workflows</CardContent></Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
