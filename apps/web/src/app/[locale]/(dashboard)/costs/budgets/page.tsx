"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { Plus, Banknote } from "lucide-react";
import { formatCurrency } from "@/lib/utils";
import { mockProjects } from "@/lib/mock-data";

export default function BudgetsPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Budgets" description="Project budget overview" actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />Create Budget</Button>} />

      <div className="rounded-md border">
        <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
          <div className="col-span-3">Project</div>
          <div className="col-span-2">Budget</div>
          <div className="col-span-2">Spent</div>
          <div className="col-span-2">Remaining</div>
          <div className="col-span-2">Spend Rate</div>
          <div className="col-span-1">Status</div>
        </div>
        {mockProjects.filter(p => p.status !== "Completed").slice(0, 10).map((p) => {
          const remaining = p.budget - p.actualCost;
          const pct = Math.round((p.actualCost / p.budget) * 100);
          return (
            <div key={p.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
              <div className="col-span-3 font-medium">{p.name}</div>
              <div className="col-span-2">{formatCurrency(p.budget)}</div>
              <div className="col-span-2">{formatCurrency(p.actualCost)}</div>
              <div className={`col-span-2 font-medium ${remaining < 0 ? "text-status-red" : "text-status-green"}`}>{formatCurrency(remaining)}</div>
              <div className="col-span-2">
                <Progress value={pct} className="h-2 w-16" />
                <span className="ml-1 text-xs">{pct}%</span>
              </div>
              <div className="col-span-1">
                <span className={`h-2 w-2 inline-block rounded-full ${remaining < 0 ? "bg-status-red" : pct > 75 ? "bg-status-amber" : "bg-status-green"}`} />
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
