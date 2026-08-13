"use client";

import { useState } from "react";
import Link from "next/link";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Plus, Filter, Search, ClipboardList, CheckCircle2, Clock, AlertCircle,
  FileText, UserCheck, CalendarDays, ArrowRight, Inbox
} from "lucide-react";
import { formatDate, getInitials } from "@/lib/utils";
import { mockTasks, mockUsers, mockApprovals, mockMeetingActions } from "@/lib/mock-data";

export default function MyWorkPage() {
  const myUserId = "user-10";
  const myTasks = mockTasks.filter((t) => t.assignee === myUserId && !t.isMilestone);

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Work"
        description="Your tasks, approvals, and action items"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
            <Button size="sm"><Plus className="mr-2 h-4 w-4" />New Task</Button>
          </div>
        }
      />

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">My Tasks</CardTitle>
            <ClipboardList className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{myTasks.length}</div>
            <p className="mt-1 text-xs text-muted-foreground">{myTasks.filter(t => t.status === "In Progress").length} in progress</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Completed</CardTitle>
            <CheckCircle2 className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-status-green">{myTasks.filter(t => t.status === "Completed").length}</div>
            <p className="mt-1 text-xs text-muted-foreground">This week</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Pending Approvals</CardTitle>
            <Inbox className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-status-amber">{mockApprovals.filter(a => a.status === "Pending").length}</div>
            <p className="mt-1 text-xs text-muted-foreground">{mockApprovals.filter(a => a.urgency === "High" && a.status === "Pending").length} urgent</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Action Items</CardTitle>
            <FileText className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{mockMeetingActions.filter(a => a.status !== "Completed").length}</div>
            <p className="mt-1 text-xs text-muted-foreground">{mockMeetingActions.filter(a => a.status === "Open").length} overdue</p>
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="tasks" className="space-y-4">
        <TabsList>
          <TabsTrigger value="tasks">My Tasks</TabsTrigger>
          <TabsTrigger value="approvals">Approvals</TabsTrigger>
          <TabsTrigger value="actions">Action Items</TabsTrigger>
        </TabsList>

        <TabsContent value="tasks" className="space-y-4">
          <div className="rounded-md border">
            <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
              <div className="col-span-4">Task</div>
              <div className="col-span-2">Project</div>
              <div className="col-span-2">Priority</div>
              <div className="col-span-2">Due Date</div>
              <div className="col-span-2">Status</div>
            </div>
            {myTasks.slice(0, 8).map((task) => {
              const priorityColors: Record<string, string> = { Critical: "bg-status-red", High: "bg-status-amber", Medium: "bg-blue-500", Low: "bg-muted-foreground" };
              return (
                <div key={task.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                  <div className="col-span-4 flex items-center gap-2 font-medium">
                    {task.status === "Completed" ? <CheckCircle2 className="h-4 w-4 text-status-green" /> :
                     task.status === "Blocked" ? <AlertCircle className="h-4 w-4 text-status-red" /> :
                     <Clock className="h-4 w-4 text-muted-foreground" />}
                    {task.title}
                  </div>
                  <div className="col-span-2 text-muted-foreground">{task.projectId}</div>
                  <div className="col-span-2 flex items-center gap-2">
                    <span className={`h-2 w-2 rounded-full ${priorityColors[task.priority]}`} />
                    {task.priority}
                  </div>
                  <div className="col-span-2 text-muted-foreground">{formatDate(task.endDate)}</div>
                  <div className="col-span-2">
                    <Badge variant={task.status === "Completed" ? "default" : task.status === "Blocked" ? "destructive" : "secondary"}>{task.status}</Badge>
                  </div>
                </div>
              );
            })}
          </div>
        </TabsContent>

        <TabsContent value="approvals" className="space-y-4">
          <div className="rounded-md border">
            {mockApprovals.filter(a => a.status === "Pending").map((approval) => {
              const requester = mockUsers.find(u => u.id === approval.requestedBy);
              return (
                <div key={approval.id} className="flex items-start gap-4 border-b px-4 py-3 last:border-0">
                  <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-muted">
                    <FileText className="h-4 w-4" />
                  </div>
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium">{approval.title}</span>
                      <Badge variant={approval.urgency === "High" ? "destructive" : "secondary"} className="text-[10px]">{approval.urgency}</Badge>
                    </div>
                    <p className="text-xs text-muted-foreground">{approval.description}</p>
                    <div className="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
                      <span>{approval.type}</span>
                      <span>·</span>
                      <span>Requested by {requester?.name}</span>
                      <span>·</span>
                      <span>{formatDate(approval.requestedAt)}</span>
                    </div>
                  </div>
                  <div className="flex gap-1">
                    <Button size="sm" variant="outline" className="text-status-green">Approve</Button>
                    <Button size="sm" variant="outline" className="text-status-red">Reject</Button>
                  </div>
                </div>
              );
            })}
          </div>
        </TabsContent>

        <TabsContent value="actions" className="space-y-4">
          <div className="rounded-md border">
            {mockMeetingActions.filter(a => a.status !== "Completed").slice(0, 8).map((action) => {
              const owner = mockUsers.find(u => u.id === action.owner);
              return (
                <div key={action.id} className="flex items-start gap-4 border-b px-4 py-3 last:border-0">
                  <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-muted">
                    <FileText className="h-4 w-4" />
                  </div>
                  <div className="flex-1">
                    <p className="text-sm font-medium">{action.description}</p>
                    <div className="mt-1 flex items-center gap-2 text-xs text-muted-foreground">
                      <Avatar className="h-5 w-5">
                        <AvatarFallback className="text-[8px]">{owner ? getInitials(owner.name) : "?"}</AvatarFallback>
                      </Avatar>
                      <span>{owner?.name}</span>
                      <span>·</span>
                      <span>Due {formatDate(action.dueDate)}</span>
                    </div>
                  </div>
                  <Badge variant={action.status === "Open" ? "destructive" : "secondary"}>{action.status}</Badge>
                </div>
              );
            })}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
