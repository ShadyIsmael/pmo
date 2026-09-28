"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";

export default function LicensePage() {
  return (
    <div className="space-y-6">
      <PageHeader title="License" description="License management and subscription" />

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle className="text-lg">Current License</CardTitle>
              <Badge status="Active" className="text-[10px]">Active</Badge>
            </div>
            <CardDescription>Enterprise Edition</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex justify-between text-sm"><span className="text-muted-foreground">License Key</span><span className="font-medium font-mono text-xs">NEXUS-ENT-2026-XXXXXXXX</span></div>
            <div className="flex justify-between text-sm"><span className="text-muted-foreground">Issued To</span><span className="font-medium">NEXUS Corporation</span></div>
            <div className="flex justify-between text-sm"><span className="text-muted-foreground">Valid From</span><span className="font-medium">Jan 1, 2026</span></div>
            <div className="flex justify-between text-sm"><span className="text-muted-foreground">Expires</span><span className="font-medium">Dec 31, 2027</span></div>
            <div className="flex justify-between text-sm"><span className="text-muted-foreground">Max Users</span><span className="font-medium">100</span></div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-lg">Usage</CardTitle><CardDescription>License utilization</CardDescription></CardHeader>
          <CardContent className="space-y-4">
            <div>
              <div className="flex justify-between text-sm mb-1"><span className="text-muted-foreground">Users</span><span className="font-medium">65 / 100 (65%)</span></div>
              <Progress value={65} className="h-2" />
            </div>
            <div>
              <div className="flex justify-between text-sm mb-1"><span className="text-muted-foreground">Projects</span><span className="font-medium">24 / 50 (48%)</span></div>
              <Progress value={48} className="h-2" />
            </div>
            <div>
              <div className="flex justify-between text-sm mb-1"><span className="text-muted-foreground">Storage</span><span className="font-medium">2.4 GB / 10 GB (24%)</span></div>
              <Progress value={24} className="h-2" />
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
