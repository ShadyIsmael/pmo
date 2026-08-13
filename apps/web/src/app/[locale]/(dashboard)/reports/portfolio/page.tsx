"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Download, BarChart3 } from "lucide-react";
import { formatCurrency } from "@/lib/utils";
import { mockPortfolios, mockProjects } from "@/lib/mock-data";

export default function PortfolioReportsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Portfolio Reports"
        description="Performance reports by portfolio"
        actions={<Button variant="outline" size="sm"><Download className="mr-2 h-4 w-4" />Export All</Button>}
      />

      <div className="grid gap-4 md:grid-cols-2">
        {mockPortfolios.map((portfolio) => {
          const pProjects = mockProjects.filter(p => p.portfolio === portfolio.id);
          const budget = pProjects.reduce((s, p) => s + p.budget, 0);
          const actual = pProjects.reduce((s, p) => s + p.actualCost, 0);
          return (
            <Card key={portfolio.id}>
              <CardHeader>
                <CardTitle className="text-base">{portfolio.name}</CardTitle>
                <CardDescription>{pProjects.length} projects</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="flex justify-between text-sm"><span className="text-muted-foreground">Budget</span><span className="font-medium">{formatCurrency(budget)}</span></div>
                <div className="flex justify-between text-sm"><span className="text-muted-foreground">Actual</span><span className="font-medium">{formatCurrency(actual)}</span></div>
                <div className="flex justify-between text-sm"><span className="text-muted-foreground">Progress</span><span className="font-medium">{portfolio.progress}%</span></div>
                <div className="flex gap-2 pt-2">
                  <Button size="sm" variant="outline" className="flex-1"><Download className="mr-1 h-4 w-4" />Export</Button>
                  <Button size="sm" variant="outline" className="flex-1"><BarChart3 className="mr-1 h-4 w-4" />View</Button>
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
