"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Input } from "@/components/ui/input";
import { Plus, Search, Filter, Clock, CheckCircle2, AlertCircle } from "lucide-react";
import { formatDate, getInitials } from "@/lib/utils";
import { mockTasks, mockUsers } from "@/lib/mock-data";

export default function MyTasksPage() {
  const myUserId = "user-10";
  const myTasks = mockTasks.filter((t) => t.assignee === myUserId);
  const statusCounts = { "Not Started": 0, "In Progress": 0, Completed: 0, Blocked: 0 };
  myTasks.forEach((t) => { statusCounts[t.status]++; });

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Tasks"
        description="All tasks assigned to you"
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />New Task</Button>}
      />

      <div className="grid gap-4 md:grid-cols-4">
        {Object.entries(statusCounts).map(([status, count]) => (
          <Card key={status}>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">{status}</CardTitle>
            </CardHeader>
            <CardContent>
              <div className={`text-2xl font-bold ${status === "Completed" ? "text-status-green" : status === "Blocked" ? "text-status-red" : ""}`}>{count}</div>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input placeholder="Search tasks..." className="pl-8" />
        </div>
        <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
      </div>

      <div className="rounded-md border">
        <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
          <div className="col-span-4">Task</div>
          <div className="col-span-2">Project</div>
          <div className="col-span-2">Priority</div>
          <div className="col-span-2">Due Date</div>
          <div className="col-span-2">Status</div>
        </div>
        {myTasks.map((task) => {
          const priorityColors: Record<string, string> = { Critical: "bg-status-red", High: "bg-status-amber", Medium: "bg-blue-500", Low: "bg-muted-foreground" };
          return (
            <div key={task.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
              <div className="col-span-4 flex items-center gap-2 font-medium">
                {task.status === "Completed" ? <CheckCircle2 className="h-4 w-4 text-status-green" /> :
                 task.status === "Blocked" ? <AlertCircle className="h-4 w-4 text-status-red" /> :
                 <Clock className="h-4 w-4 text-muted-foreground" />}
                {task.title}
                {task.isMilestone && <Badge variant="outline" className="text-[10px]">Milestone</Badge>}
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
    </div>
  );
}
