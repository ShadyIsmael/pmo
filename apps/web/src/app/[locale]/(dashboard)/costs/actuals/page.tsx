"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { DollarSign } from "lucide-react";
import { formatCurrency } from "@/lib/utils";
import { mockProjects } from "@/lib/mock-data";

export default function ActualsPage() {
  const totalActual = mockProjects.reduce((s, p) => s + p.actualCost, 0);

  return (
    <div className="space-y-6">
      <PageHeader title="Actual Costs" description="Recorded project expenditure" actions={<Button size="sm">Record Expense</Button>} />

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Total Actuals</CardTitle>
            <DollarSign className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatCurrency(totalActual)}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">This Quarter</CardTitle><DollarSign className="h-4 w-4 text-muted-foreground" /></CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatCurrency(Math.round(totalActual * 0.35))}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2"><CardTitle className="text-sm font-medium text-muted-foreground">Last Quarter</CardTitle><DollarSign className="h-4 w-4 text-muted-foreground" /></CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatCurrency(Math.round(totalActual * 0.28))}</div>
            <p className="text-xs text-muted-foreground">+25% from previous</p>
          </CardContent>
        </Card>
      </div>

      <div className="rounded-md border">
        <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
          <div className="col-span-3">Project</div>
          <div className="col-span-2">Budget</div>
          <div className="col-span-2">Actual</div>
          <div className="col-span-2">% Spent</div>
          <div className="col-span-2">Remaining</div>
          <div className="col-span-1"></div>
        </div>
        {mockProjects.filter(p => p.status !== "Completed").slice(0, 12).map((p) => {
          const pct = Math.round((p.actualCost / p.budget) * 100);
          const remaining = p.budget - p.actualCost;
          return (
            <div key={p.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
              <div className="col-span-3 font-medium">{p.name}</div>
              <div className="col-span-2">{formatCurrency(p.budget)}</div>
              <div className="col-span-2">{formatCurrency(p.actualCost)}</div>
              <div className="col-span-2">{pct}%</div>
              <div className={`col-span-2 font-medium ${remaining < 0 ? "text-status-red" : "text-status-green"}`}>{formatCurrency(remaining)}</div>
              <div className="col-span-1"><span className={`h-2 w-2 inline-block rounded-full ${remaining < 0 ? "bg-status-red" : pct > 75 ? "bg-status-amber" : "bg-status-green"}`} /></div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
