"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Plus, FileText } from "lucide-react";

export default function CustomReportsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Custom Reports"
        description="Build and manage custom report configurations"
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />Create Report</Button>}
      />

      <Card><CardContent className="py-12 text-center text-muted-foreground">
        <FileText className="mx-auto h-12 w-12 mb-3" />
        <p className="font-medium">No custom reports yet</p>
        <p className="text-sm">Create your first custom report to get started</p>
        <Button className="mt-4"><Plus className="mr-2 h-4 w-4" />Create Report</Button>
      </CardContent></Card>
    </div>
  );
}
