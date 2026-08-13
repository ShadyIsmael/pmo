"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Filter, Download } from "lucide-react";
import { formatDate, getInitials, getStatusVariant } from "@/lib/utils";
import { mockProjects, mockUsers, mockResources, mockTasks } from "@/lib/mock-data";

export default function ResourceMatrixPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Resource Matrix"
        description="Resource allocation across projects"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
            <Button variant="outline" size="sm"><Download className="mr-2 h-4 w-4" />Export</Button>
          </div>
        }
      />

      <div className="overflow-x-auto">
        <div className="min-w-[900px] rounded-md border">
          <div className="grid grid-cols-10 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
            <div className="col-span-2">Resource</div>
            <div className="col-span-1">Role</div>
            <div className="col-span-1">Availability</div>
            <div className="col-span-1">Rate</div>
            {mockProjects.filter(p => p.status !== "Completed").slice(0, 4).map(p => (
              <div key={p.id} className="col-span-1">{p.code}</div>
            ))}
            <div className="col-span-1">Allocation</div>
          </div>
          {mockResources.slice(0, 12).map((res) => (
            <div key={res.id} className="grid grid-cols-10 gap-2 border-b px-4 py-3 text-sm last:border-0 hover:bg-muted/30">
              <div className="col-span-2 flex items-center gap-2 font-medium">
                <Avatar className="h-6 w-6"><AvatarFallback className="text-[8px]">{getInitials(res.name)}</AvatarFallback></Avatar>
                {res.name}
              </div>
              <div className="col-span-1 text-muted-foreground">{res.role}</div>
              <div className="col-span-1">
                <div className="flex items-center gap-1">
                  <Progress value={res.availability} className="h-1.5 w-10" />
                  <span className="text-xs">{res.availability}%</span>
                </div>
              </div>
              <div className="col-span-1 text-muted-foreground">£{res.dailyRate}</div>
              {mockProjects.filter(p => p.status !== "Completed").slice(0, 4).map(p => (
                <div key={p.id} className="col-span-1">
                  <span className={`h-2 w-2 inline-block rounded-full ${Math.random() > 0.5 ? "bg-status-green" : Math.random() > 0.5 ? "bg-status-amber" : "bg-muted"}`} />
                </div>
              ))}
              <div className="col-span-1">
                <span className={`font-medium ${res.allocated > 80 ? "text-status-red" : res.allocated > 60 ? "text-status-amber" : "text-status-green"}`}>
                  {res.allocated}%
                </span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
