"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Download, FileText } from "lucide-react";
import { formatCurrency } from "@/lib/utils";
import { mockProjects } from "@/lib/mock-data";

export default function ExecutiveReportsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Executive Reports"
        description="High-level reports for leadership"
        actions={<Button variant="outline" size="sm"><Download className="mr-2 h-4 w-4" />Download All</Button>}
      />

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {[
          { name: "Monthly Executive Summary", desc: "Portfolio health, financials, and key metrics", pages: 4 },
          { name: "Quarterly Board Report", desc: "Strategic initiative progress and outcomes", pages: 8 },
          { name: "Risk & Compliance Report", desc: "Regulatory compliance and risk posture", pages: 6 },
          { name: "Financial Performance Report", desc: "Budget vs actuals and forecast analysis", pages: 5 },
          { name: "Resource & Capacity Report", desc: "Workforce allocation and utilization", pages: 3 },
          { name: "Benefits Realisation Report", desc: "Project benefits tracking and value delivery", pages: 7 },
        ].map((report) => (
          <Card key={report.name}>
            <CardHeader>
              <div className="flex items-start justify-between">
                <FileText className="h-5 w-5 text-muted-foreground" />
                <Badge variant="outline" className="text-[10px]">{report.pages} pages</Badge>
              </div>
              <CardTitle className="text-base mt-2">{report.name}</CardTitle>
              <CardDescription>{report.desc}</CardDescription>
            </CardHeader>
            <CardContent>
              <Button variant="outline" size="sm" className="w-full"><Download className="mr-1 h-4 w-4" />Download PDF</Button>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
