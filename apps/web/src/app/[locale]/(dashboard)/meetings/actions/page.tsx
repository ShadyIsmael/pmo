"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { CheckCircle2, Clock, AlertCircle } from "lucide-react";
import { formatDate, getInitials } from "@/lib/utils";
import { mockMeetingActions, mockUsers } from "@/lib/mock-data";

export default function MeetingActionsPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Action Items" description="Track meeting action items and follow-ups" />

      <Tabs defaultValue="open">
        <TabsList>
          <TabsTrigger value="open">Open ({mockMeetingActions.filter(a => a.status === "Open").length})</TabsTrigger>
          <TabsTrigger value="in-progress">In Progress ({mockMeetingActions.filter(a => a.status === "In Progress").length})</TabsTrigger>
          <TabsTrigger value="completed">Completed ({mockMeetingActions.filter(a => a.status === "Completed").length})</TabsTrigger>
        </TabsList>

        <TabsContent value="open">
          <div className="rounded-md border">
            {mockMeetingActions.filter(a => a.status === "Open").map((action) => {
              const owner = mockUsers.find(u => u.id === action.owner);
              return (
                <div key={action.id} className="flex items-start gap-4 border-b px-4 py-3 last:border-0 hover:bg-muted/30">
                  <AlertCircle className="mt-1 h-4 w-4 text-status-red shrink-0" />
                  <div className="flex-1">
                    <p className="text-sm font-medium">{action.description}</p>
                    <div className="mt-1 flex items-center gap-2 text-xs text-muted-foreground">
                      <Avatar className="h-5 w-5"><AvatarFallback className="text-[8px]">{owner ? getInitials(owner.name) : ""}</AvatarFallback></Avatar>
                      <span>{owner?.name}</span>
                      <span>· Due {formatDate(action.dueDate)}</span>
                    </div>
                  </div>
                  <Badge variant="destructive" className="text-[10px]">Open</Badge>
                </div>
              );
            })}
          </div>
        </TabsContent>

        <TabsContent value="in-progress">
          <div className="rounded-md border">
            {mockMeetingActions.filter(a => a.status === "In Progress").map((action) => {
              const owner = mockUsers.find(u => u.id === action.owner);
              return (
                <div key={action.id} className="flex items-start gap-4 border-b px-4 py-3 last:border-0 hover:bg-muted/30">
                  <Clock className="mt-1 h-4 w-4 text-status-amber shrink-0" />
                  <div className="flex-1">
                    <p className="text-sm font-medium">{action.description}</p>
                    <div className="mt-1 flex items-center gap-2 text-xs text-muted-foreground">
                      <Avatar className="h-5 w-5"><AvatarFallback className="text-[8px]">{owner ? getInitials(owner.name) : ""}</AvatarFallback></Avatar>
                      <span>{owner?.name}</span>
                      <span>· Due {formatDate(action.dueDate)}</span>
                    </div>
                  </div>
                  <Badge variant="secondary" className="text-[10px]">In Progress</Badge>
                </div>
              );
            })}
          </div>
        </TabsContent>

        <TabsContent value="completed">
          <div className="rounded-md border">
            {mockMeetingActions.filter(a => a.status === "Completed").map((action) => {
              const owner = mockUsers.find(u => u.id === action.owner);
              return (
                <div key={action.id} className="flex items-start gap-4 border-b px-4 py-3 last:border-0 hover:bg-muted/30">
                  <CheckCircle2 className="mt-1 h-4 w-4 text-status-green shrink-0" />
                  <div className="flex-1">
                    <p className="text-sm font-medium line-through text-muted-foreground">{action.description}</p>
                    <div className="mt-1 text-xs text-muted-foreground">{owner?.name} · Due {formatDate(action.dueDate)}</div>
                  </div>
                  <Badge variant="outline" className="text-[10px]">Completed</Badge>
                </div>
              );
            })}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
