"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { TrendingUp, TrendingDown } from "lucide-react";
import { formatCurrency } from "@/lib/utils";
import { mockProjects } from "@/lib/mock-data";

export default function ForecastsPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Forecasts" description="Project cost forecasts vs budget" />

      <div className="grid gap-4 md:grid-cols-2">
        {mockProjects.filter(p => p.status !== "Completed").slice(0, 6).map((p) => {
          const diff = p.budget - p.forecast;
          const isOver = diff < 0;
          const pct = Math.round((p.forecast / p.budget) * 100);
          return (
            <Card key={p.id}>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle className="text-base">{p.name}</CardTitle>
                  <Badge variant={isOver ? "destructive" : "default"} className="text-[10px]">{isOver ? "Over Budget" : "Under Budget"}</Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-3">
                <Progress value={pct} className="h-2" />
                <div className="grid grid-cols-3 gap-2 text-sm">
                  <div><span className="text-xs text-muted-foreground">Budget</span><p className="font-medium">{formatCurrency(p.budget)}</p></div>
                  <div><span className="text-xs text-muted-foreground">Forecast</span><p className="font-medium">{formatCurrency(p.forecast)}</p></div>
                  <div>
                    <span className="text-xs text-muted-foreground">Variance</span>
                    <p className={`font-medium ${isOver ? "text-status-red" : "text-status-green"}`}>
                      {isOver ? <TrendingDown className="inline h-3 w-3 mr-1" /> : <TrendingUp className="inline h-3 w-3 mr-1" />}
                      {formatCurrency(Math.abs(diff))}
                    </p>
                  </div>
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
