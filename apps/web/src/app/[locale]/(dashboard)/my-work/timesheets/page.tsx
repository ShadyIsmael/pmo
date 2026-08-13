"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Plus, Clock, CalendarDays } from "lucide-react";
import { formatDate } from "@/lib/utils";
import { mockTasks } from "@/lib/mock-data";

export default function TimesheetsPage() {
  const myUserId = "user-10";
  const myTasks = mockTasks.filter(t => t.assignee === myUserId);
  const totalEstimated = myTasks.reduce((s, t) => s + t.estimatedHours, 0);
  const totalActual = myTasks.reduce((s, t) => s + t.actualHours, 0);
  const thisWeek = myTasks.slice(0, 5);

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Timesheets"
        description="Track your time across projects"
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />Log Time</Button>}
      />

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">This Week</CardTitle>
            <Clock className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{totalActual}h</div>
            <p className="text-xs text-muted-foreground">of {totalEstimated}h estimated</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">This Month</CardTitle>
            <CalendarDays className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">124h</div>
            <p className="text-xs text-muted-foreground">82% utilization</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Remaining PTO</CardTitle>
            <CalendarDays className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">12 days</div>
            <p className="text-xs text-muted-foreground">Used 8 of 20 this year</p>
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="entries">
        <TabsList>
          <TabsTrigger value="entries">Time Entries</TabsTrigger>
          <TabsTrigger value="summary">Weekly Summary</TabsTrigger>
        </TabsList>

        <TabsContent value="entries">
          <div className="rounded-md border">
            <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
              <div className="col-span-4">Task</div>
              <div className="col-span-2">Date</div>
              <div className="col-span-2">Estimated</div>
              <div className="col-span-2">Actual</div>
              <div className="col-span-2">Variance</div>
            </div>
            {thisWeek.map((task) => (
              <div key={task.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <div className="col-span-4 font-medium">{task.title}</div>
                <div className="col-span-2 text-muted-foreground">{formatDate(task.startDate)}</div>
                <div className="col-span-2">{task.estimatedHours}h</div>
                <div className="col-span-2">{task.actualHours}h</div>
                <div className="col-span-2">
                  <span className={task.actualHours <= task.estimatedHours ? "text-status-green" : "text-status-amber"}>
                    {((task.actualHours - task.estimatedHours) / task.estimatedHours * 100).toFixed(0)}%
                  </span>
                </div>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="summary">
          <Card>
            <CardHeader><CardTitle>Weekly Hours</CardTitle></CardHeader>
            <CardContent>
              <div className="flex items-end gap-2">
                {["Mon", "Tue", "Wed", "Thu", "Fri"].map((day, i) => (
                  <div key={day} className="flex flex-1 flex-col items-center gap-1">
                    <span className="text-xs text-muted-foreground">{6 + i * 2}h</span>
                    <div className="h-24 w-full rounded-md bg-muted" style={{ height: `${40 + i * 15}px` }} />
                    <span className="text-xs text-muted-foreground">{day}</span>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
