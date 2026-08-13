"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Plus, Filter, Download, Banknote, TrendingUp, TrendingDown,
  ArrowUpRight, ArrowDownRight, DollarSign
} from "lucide-react";
import { formatCurrency } from "@/lib/utils";
import { mockProjects } from "@/lib/mock-data";

export default function CostsPage() {
  const totalBudget = mockProjects.reduce((s, p) => s + p.budget, 0);
  const totalForecast = mockProjects.reduce((s, p) => s + p.forecast, 0);
  const totalActual = mockProjects.reduce((s, p) => s + p.actualCost, 0);
  const variance = totalBudget - totalForecast;
  const isOver = variance < 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Cost Dashboard"
        description="Financial performance and budget tracking across all projects"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" size="sm"><Download className="mr-2 h-4 w-4" />Export</Button>
            <Button size="sm"><Plus className="mr-2 h-4 w-4" />New Budget</Button>
          </div>
        }
      />

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Total Budget</CardTitle>
            <Banknote className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatCurrency(totalBudget)}</div>
            <p className="text-xs text-muted-foreground">Across all projects</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Actual Spent</CardTitle>
            <DollarSign className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatCurrency(totalActual)}</div>
            <p className="text-xs text-muted-foreground">{Math.round((totalActual / totalBudget) * 100)}% of budget</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Forecast</CardTitle>
            <TrendingUp className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatCurrency(totalForecast)}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Variance</CardTitle>
            {isOver ? <TrendingDown className="h-4 w-4 text-muted-foreground" /> : <TrendingUp className="h-4 w-4 text-muted-foreground" />}
          </CardHeader>
          <CardContent>
            <div className={`text-2xl font-bold ${isOver ? "text-status-red" : "text-status-green"}`}>
              {isOver ? "" : "+"}{formatCurrency(Math.abs(variance))}
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Budget vs Actual by Project</CardTitle>
            <CardDescription>Top 8 projects by budget</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {mockProjects.filter(p => p.status !== "Completed").slice(0, 8).map((project) => {
              const spentPct = Math.round((project.actualCost / project.budget) * 100);
              const isOverBudget = project.actualCost > project.budget;
              return (
                <div key={project.id} className="space-y-1">
                  <div className="flex items-center justify-between text-sm">
                    <span className="font-medium">{project.name}</span>
                    <div className="flex items-center gap-2">
                      <span>{formatCurrency(project.budget)}</span>
                      <span className="text-muted-foreground">|</span>
                      <span className={isOverBudget ? "text-status-red" : "text-status-green"}>{formatCurrency(project.actualCost)}</span>
                    </div>
                  </div>
                  <Progress value={spentPct} className={`h-2 ${isOverBudget ? "bg-status-red/20" : ""}`} />
                </div>
              );
            })}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Cost Variance</CardTitle>
            <CardDescription>Projects with significant cost deviation</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {[...mockProjects.filter(p => p.status !== "Completed")]
              .sort((a, b) => a.costVariance - b.costVariance)
              .slice(0, 8)
              .map((project) => (
              <div key={project.id} className="flex items-center justify-between border-b pb-2 last:border-0">
                <div className="flex-1">
                  <p className="text-sm font-medium">{project.name}</p>
                  <p className="text-xs text-muted-foreground">{project.code}</p>
                </div>
                <div className="text-right">
                  <p className={`text-sm font-medium ${project.costVariance < 0 ? "text-status-red" : "text-status-green"}`}>
                    {project.costVariance > 0 ? "+" : ""}{project.costVariance}%
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {project.costVariance < 0 ? "Over budget" : "Under budget"}
                  </p>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="by-project">
        <TabsList>
          <TabsTrigger value="by-project">By Project</TabsTrigger>
          <TabsTrigger value="by-portfolio">By Portfolio</TabsTrigger>
        </TabsList>

        <TabsContent value="by-project">
          <div className="rounded-md border">
            <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
              <div className="col-span-3">Project</div>
              <div className="col-span-2">Budget</div>
              <div className="col-span-2">Forecast</div>
              <div className="col-span-2">Actual</div>
              <div className="col-span-2">Variance</div>
              <div className="col-span-1">Status</div>
            </div>
            {mockProjects.filter(p => p.status !== "Completed").map((project) => (
              <div key={project.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <div className="col-span-3 font-medium">{project.name}</div>
                <div className="col-span-2">{formatCurrency(project.budget)}</div>
                <div className="col-span-2">{formatCurrency(project.forecast)}</div>
                <div className="col-span-2">{formatCurrency(project.actualCost)}</div>
                <div className="col-span-2">
                  <span className={`font-medium ${project.costVariance < 0 ? "text-status-red" : "text-status-green"}`}>
                    {project.costVariance > 0 ? "+" : ""}{project.costVariance}%
                  </span>
                </div>
                <div className="col-span-1">
                  <span className={`h-2 w-2 inline-block rounded-full ${project.costVariance >= 0 ? "bg-status-green" : "bg-status-red"}`} />
                </div>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="by-portfolio">
          <Card>
            <CardContent className="pt-6">
              <div className="space-y-4">
                {["Strategic Initiatives", "Digital Transformation", "Infrastructure & Operations", "Business Enablement"].map((name) => {
                  const pProjects = mockProjects.filter(p => p.portfolio === `portfolio-${["Strategic Initiatives", "Digital Transformation", "Infrastructure & Operations", "Business Enablement"].indexOf(name) + 1}`);
                  const pBudget = pProjects.reduce((s, p) => s + p.budget, 0);
                  const pActual = pProjects.reduce((s, p) => s + p.actualCost, 0);
                  return (
                    <div key={name} className="flex items-center justify-between border-b pb-3 last:border-0">
                      <div className="flex-1">
                        <p className="text-sm font-medium">{name}</p>
                        <p className="text-xs text-muted-foreground">{pProjects.length} projects</p>
                      </div>
                      <div className="text-right">
                        <p className="text-sm font-medium">{formatCurrency(pBudget)}</p>
                        <p className="text-xs text-muted-foreground">{formatCurrency(pActual)} spent</p>
                      </div>
                    </div>
                  );
                })}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
