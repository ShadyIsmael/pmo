"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { PuzzleIcon, Trash2, Settings } from "lucide-react";

export default function AdminPluginsPage() {
  const installed = [
    { id: "plg-1", name: "Advanced Gantt Chart", version: "2.1.0", status: "Active", author: "NEXUS Labs" },
    { id: "plg-2", name: "SAP Integration", version: "1.0.2", status: "Active", author: "NEXUS Labs" },
    { id: "plg-5", name: "Timesheet Automation", version: "2.0.0", status: "Active", author: "NEXUS Labs" },
  ];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Installed Plugins"
        description={`${installed.length} plugins installed`}
        actions={<Button size="sm"><PuzzleIcon className="mr-2 h-4 w-4" />Browse Marketplace</Button>}
      />

      <div className="space-y-3">
        {installed.map((plugin) => (
          <Card key={plugin.id}>
            <CardContent className="flex items-center gap-4 py-4">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
                <PuzzleIcon className="h-5 w-5 text-primary" />
              </div>
              <div className="flex-1">
                <div className="flex items-center gap-2">
                  <p className="font-medium">{plugin.name}</p>
              <Badge status={plugin.status} className="text-[10px]">{plugin.status}</Badge>
                </div>
                <p className="text-xs text-muted-foreground">v{plugin.version} by {plugin.author}</p>
              </div>
              <Button size="sm" variant="outline"><Settings className="mr-1 h-4 w-4" />Configure</Button>
              <Button size="sm" variant="outline" className="text-status-red"><Trash2 className="h-4 w-4" /></Button>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
