"use client";

import * as React from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Plus, MoreHorizontal } from "lucide-react";
import { getInitials } from "@/lib/utils";
import { mockProjects, mockTasks, mockUsers } from "@/lib/mock-data";

export default function BoardPage() {
  const [project, setProject] = React.useState(mockProjects[0]!.id);
  const selected = mockProjects.find(p => p.id === project) ?? mockProjects[0]!;
  const projectTasks = mockTasks.filter(t => t.projectId === selected.id);
  const columns = [
    { id: "not-started", title: "Not Started", tasks: projectTasks.filter(t => t.status === "Not Started") },
    { id: "in-progress", title: "In Progress", tasks: projectTasks.filter(t => t.status === "In Progress") },
    { id: "blocked", title: "Blocked", tasks: projectTasks.filter(t => t.status === "Blocked") },
    { id: "completed", title: "Completed", tasks: projectTasks.filter(t => t.status === "Completed") },
  ];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Board View"
        description="Drag-and-drop task management"
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />Add Task</Button>}
      />

      <div className="flex items-center gap-2">
        <Select value={project} onValueChange={setProject}>
          <SelectTrigger className="w-[300px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {mockProjects.filter(p => p.status !== "Completed").map((p) => (
              <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="grid grid-cols-4 gap-4">
        {columns.map((col) => (
          <div key={col.id}>
            <div className="mb-3 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <h3 className="text-sm font-medium">{col.title}</h3>
                <Badge variant="secondary" className="text-[10px]">{col.tasks.length}</Badge>
              </div>
              <Button variant="ghost" size="icon" className="h-6 w-6"><Plus className="h-3 w-3" /></Button>
            </div>
            <div className="space-y-2">
              {col.tasks.map((task) => {
                const assignee = mockUsers.find(u => u.id === task.assignee);
                return (
                  <Card key={task.id} className="cursor-grab active:cursor-grabbing">
                    <CardHeader className="p-3 pb-0">
                      <div className="flex items-start justify-between">
                        <Badge variant={task.priority === "Critical" ? "destructive" : "secondary"} className="text-[10px]">{task.priority}</Badge>
                        <Button variant="ghost" size="icon" className="h-5 w-5"><MoreHorizontal className="h-3 w-3" /></Button>
                      </div>
                      <p className="mt-1 text-sm font-medium">{task.title}</p>
                    </CardHeader>
                    <CardContent className="p-3 pt-2">
                      <div className="flex items-center justify-between text-xs text-muted-foreground">
                        <div className="flex items-center gap-1">
                          <Avatar className="h-5 w-5"><AvatarFallback className="text-[8px]">{assignee ? getInitials(assignee.name) : ""}</AvatarFallback></Avatar>
                          <span>{assignee?.name.split(" ")[0]}</span>
                        </div>
                        <span>{task.estimatedHours}h</span>
                      </div>
                    </CardContent>
                  </Card>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
