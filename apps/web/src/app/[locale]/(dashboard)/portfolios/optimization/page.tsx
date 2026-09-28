"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { BarChart3, TrendingUp, Target, Lightbulb } from "lucide-react";
import { formatCurrency } from "@/lib/utils";

export default function OptimizationPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Portfolio Optimization"
        description="Analyse and optimise portfolio mix for maximum value"
        actions={<Button size="sm"><Lightbulb className="mr-2 h-4 w-4" />Run Optimization</Button>}
      />

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Portfolio Value</CardTitle>
            <Target className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatCurrency(465_000_000)}</div>
            <p className="text-xs text-muted-foreground">Total projected value</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">ROI</CardTitle>
            <TrendingUp className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-status-green">18.5%</div>
            <p className="text-xs text-muted-foreground">Weighted average</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Optimization Score</CardTitle>
            <BarChart3 className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">72/100</div>
            <Progress value={72} className="mt-2 h-2" />
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="scenarios">
        <TabsList>
          <TabsTrigger value="scenarios">What-If Scenarios</TabsTrigger>
          <TabsTrigger value="constraints">Constraints</TabsTrigger>
        </TabsList>

        <TabsContent value="scenarios">
          <div className="grid gap-4 md:grid-cols-2">
            <Card>
              <CardHeader><CardTitle className="text-base">Balanced Growth</CardTitle><CardDescription>Optimize for risk-adjusted return</CardDescription></CardHeader>
              <CardContent className="space-y-2">
                <div className="flex justify-between text-sm"><span>Expected ROI</span><span className="font-medium text-status-green">22.3%</span></div>
                <div className="flex justify-between text-sm"><span>Risk Score</span><span className="font-medium text-status-amber">Medium</span></div>
                <div className="flex justify-between text-sm"><span>Budget Required</span><span className="font-medium">{formatCurrency(412_000_000)}</span></div>
                <div className="flex justify-between text-sm"><span>Projects</span><span className="font-medium">18</span></div>
                <Button size="sm" variant="outline" className="mt-2 w-full">Apply Scenario</Button>
              </CardContent>
            </Card>
            <Card>
              <CardHeader><CardTitle className="text-base">Maximize Value</CardTitle><CardDescription>Prioritize high-ROI initiatives</CardDescription></CardHeader>
              <CardContent className="space-y-2">
                <div className="flex justify-between text-sm"><span>Expected ROI</span><span className="font-medium text-status-green">28.7%</span></div>
                <div className="flex justify-between text-sm"><span>Risk Score</span><span className="font-medium text-status-red">High</span></div>
                <div className="flex justify-between text-sm"><span>Budget Required</span><span className="font-medium">{formatCurrency(385_000_000)}</span></div>
                <div className="flex justify-between text-sm"><span>Projects</span><span className="font-medium">14</span></div>
                <Button size="sm" variant="outline" className="mt-2 w-full">Apply Scenario</Button>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="constraints">
          <Card>
            <CardHeader><CardTitle>Current Constraints</CardTitle></CardHeader>
            <CardContent>
              <div className="space-y-3">
                {[
                  { name: "Budget Capacity", current: "78%", limit: "£465M", status: "amber" },
                  { name: "Resource Capacity", current: "92%", limit: "120 FTE", status: "red" },
                  { name: "Risk Appetite", current: "Medium", limit: "Medium", status: "green" },
                  { name: "Time Horizon", current: "18 months", limit: "24 months", status: "green" },
                ].map((c) => (
                  <div key={c.name} className="flex items-center justify-between border-b pb-2 last:border-0">
                    <span className="text-sm font-medium">{c.name}</span>
                    <div className="flex items-center gap-2">
                      <span className="text-sm text-muted-foreground">{c.current} / {c.limit}</span>
                      <span className={`h-2 w-2 rounded-full bg-status-${c.status}`} />
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
