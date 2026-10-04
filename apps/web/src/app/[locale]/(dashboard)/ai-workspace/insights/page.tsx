"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { BarChart3, Lightbulb, TrendingUp, AlertTriangle, Sparkles } from "lucide-react";
import { getLookupTextClass } from "@/lib/status-presentation";

const insights = [
  { id: "ins-1", title: "Resource Overallocation Detected", description: "Engineering department is overallocated by 120 hours this month. Consider redistributing tasks or adjusting deadlines.", type: "warning", category: "Resources", date: "2026-07-29" },
  { id: "ins-2", title: "Cost Saving Opportunity", description: "Projects using outsourced resources in Infrastructure show potential savings of £45K by converting 3 roles to internal.", type: "opportunity", category: "Costs", date: "2026-07-28" },
  { id: "ins-3", title: "Schedule Risk: Core Banking", description: "Core Banking Modernisation has 2 critical risks with score > 15. Schedule variance is -2.0%. Recommend escalation review.", type: "warning", category: "Risk", date: "2026-07-28" },
  { id: "ins-4", title: "Portfolio Health Improving", description: "Overall portfolio health score increased by 5% this month. Green-rated projects now represent 62% of active projects.", type: "positive", category: "Portfolio", date: "2026-07-27" },
  { id: "ins-5", title: "Compliance Milestone Approaching", description: "Regulatory Compliance Suite has a key milestone due in 14 days. Current progress is 52% vs planned 60%.", type: "info", category: "Milestone", date: "2026-07-27" },
  { id: "ins-6", title: "AI Model Accuracy Update", description: "AI-Powered Risk Analytics model accuracy improved to 93.5%, approaching the 95% target.", type: "positive", category: "AI", date: "2026-07-26" },
];

export default function AIInsightsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="AI Insights"
        description="Automated intelligence and recommendations"
        actions={
          <Button size="sm" variant="outline">
            <Sparkles className="mr-2 h-4 w-4" />
            Refresh Insights
          </Button>
        }
      />

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Total Insights</CardTitle>
            <Lightbulb className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold">{insights.length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Warnings</CardTitle>
            <AlertTriangle className="h-4 w-4 text-status-amber" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold text-status-amber">{insights.filter(i => i.type === "warning").length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Opportunities</CardTitle>
            <TrendingUp className="h-4 w-4 text-status-green" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold text-status-green">{insights.filter(i => i.type === "opportunity" || i.type === "positive").length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">AI Confidence</CardTitle>
            <BarChart3 className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold">87%</div></CardContent>
        </Card>
      </div>

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All</TabsTrigger>
          <TabsTrigger value="warnings">Warnings</TabsTrigger>
          <TabsTrigger value="opportunities">Opportunities</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="space-y-3">
            {insights.map((insight) => (
              <Card key={insight.id}>
                <CardContent className="flex items-start gap-4 pt-6">
                  <div className={`flex h-10 w-10 items-center justify-center rounded-lg ${
                    insight.type === "warning" ? "bg-status-amber/10" :
                    insight.type === "positive" ? "bg-status-green/10" :
                    insight.type === "opportunity" ? "bg-blue-500/10" : "bg-muted"
                  }`}>
                    {insight.type === "warning" ? <AlertTriangle className="h-5 w-5 text-status-amber" /> :
                     insight.type === "positive" ? <TrendingUp className="h-5 w-5 text-status-green" /> :
                     insight.type === "opportunity" ? <Lightbulb className="h-5 w-5 text-blue-500" /> :
                     <BarChart3 className="h-5 w-5" />}
                  </div>
                  <div className="flex-1">
                    <div className="flex items-start justify-between">
                      <div>
                        <h3 className="font-medium">{insight.title}</h3>
                        <p className="text-sm text-muted-foreground mt-1">{insight.description}</p>
                      </div>
                      <Badge status={insight.type === "warning" ? "Warning" : insight.type === "positive" ? "Completed" : "Info"} className="text-[10px] shrink-0">
                        {insight.type}
                      </Badge>
                    </div>
                    <div className="mt-2 flex items-center gap-2 text-xs text-muted-foreground">
                      <span className={`text-xs font-medium ${getLookupTextClass(insight.category)}`}>{insight.category}</span>
                      <span>{insight.date}</span>
                    </div>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="warnings">
          <div className="space-y-3">
            {insights.filter(i => i.type === "warning").map((insight) => (
              <Card key={insight.id}>
                <CardContent className="flex items-start gap-4 pt-6">
                  <AlertTriangle className="mt-1 h-5 w-5 text-status-amber shrink-0" />
                  <div className="flex-1">
                    <h3 className="font-medium">{insight.title}</h3>
                    <p className="text-sm text-muted-foreground">{insight.description}</p>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="opportunities">
          <div className="space-y-3">
            {insights.filter(i => i.type === "opportunity" || i.type === "positive").map((insight) => (
              <Card key={insight.id}>
                <CardContent className="flex items-start gap-4 pt-6">
                  <TrendingUp className="mt-1 h-5 w-5 text-status-green shrink-0" />
                  <div className="flex-1">
                    <h3 className="font-medium">{insight.title}</h3>
                    <p className="text-sm text-muted-foreground">{insight.description}</p>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
