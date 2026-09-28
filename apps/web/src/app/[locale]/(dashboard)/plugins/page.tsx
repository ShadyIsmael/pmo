"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { PuzzleIcon, Star, Download, ExternalLink, CheckCircle2 } from "lucide-react";

const plugins = [
  { id: "plg-1", name: "Advanced Gantt Chart", description: "Interactive Gantt chart with drag-and-drop scheduling", version: "2.1.0", status: "Installed", rating: 4.5, downloads: 1234, verified: true },
  { id: "plg-2", name: "SAP Integration", description: "Bidirectional data sync with SAP S/4HANA", version: "1.0.2", status: "Installed", rating: 4.2, downloads: 567, verified: true },
  { id: "plg-3", name: "Custom Report Builder", description: "Build custom reports with drag-and-drop widgets", version: "3.0.1", status: "Available", rating: 4.8, downloads: 2341, verified: true },
  { id: "plg-4", name: "Portfolio Optimizer", description: "AI-powered portfolio optimization and what-if analysis", version: "1.5.0", status: "Available", rating: 4.6, downloads: 892, verified: true },
  { id: "plg-5", name: "Timesheet Automation", description: "Automated time tracking and timesheet approvals", version: "2.0.0", status: "Installed", rating: 4.3, downloads: 1567, verified: true },
  { id: "plg-6", name: "Resource Forecasting", description: "Predict resource demand using ML algorithms", version: "1.2.0", status: "Available", rating: 4.1, downloads: 345, verified: false },
  { id: "plg-7", name: "BI Connector", description: "Connect to Power BI, Tableau, and Looker", version: "1.0.0", status: "Available", rating: 4.4, downloads: 2345, verified: true },
  { id: "plg-8", name: "Compliance Tracker", description: "Track regulatory compliance across projects", version: "2.3.0", status: "Available", rating: 4.7, downloads: 678, verified: true },
];

export default function PluginsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Plugin Marketplace"
        description="Extend NEXUS PMO with powerful plugins"
        actions={
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <PuzzleIcon className="h-4 w-4" />
            {plugins.filter(p => p.status === "Installed").length} installed
          </div>
        }
      />

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All ({plugins.length})</TabsTrigger>
          <TabsTrigger value="installed">Installed ({plugins.filter(p => p.status === "Installed").length})</TabsTrigger>
          <TabsTrigger value="available">Available ({plugins.filter(p => p.status === "Available").length})</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {plugins.map((plugin) => (
              <Card key={plugin.id}>
                <CardHeader>
                  <div className="flex items-start justify-between">
                    <div className="flex items-start gap-3">
                      <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
                        <PuzzleIcon className="h-5 w-5 text-primary" />
                      </div>
                      <div>
                        <CardTitle className="text-base">{plugin.name}</CardTitle>
                        <CardDescription>v{plugin.version}</CardDescription>
                      </div>
                    </div>
                    <Badge status={plugin.status} className="text-[10px]">
                      {plugin.status === "Installed" ? <CheckCircle2 className="mr-1 h-3 w-3" /> : null}
                      {plugin.status}
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="space-y-3">
                  <p className="text-xs text-muted-foreground">{plugin.description}</p>
                  <div className="flex items-center justify-between text-xs">
                    <div className="flex items-center gap-3">
                      <span className="flex items-center gap-1">
                        <Star className="h-3 w-3 fill-status-amber text-status-amber" />
                        {plugin.rating}
                      </span>
                      <span className="text-muted-foreground">{plugin.downloads} downloads</span>
                    </div>
                    {plugin.verified && <Badge status="Verified" className="text-[10px]">Verified</Badge>}
                  </div>
                  <div className="flex gap-2 pt-1">
                    {plugin.status === "Installed" ? (
                      <>
                        <Button size="sm" variant="outline" className="flex-1"><ExternalLink className="mr-1 h-3 w-3" />Configure</Button>
                        <Button size="sm" variant="outline" className="flex-1">Manage</Button>
                      </>
                    ) : (
                      <Button size="sm" className="w-full"><Download className="mr-1 h-3 w-3" />Install</Button>
                    )}
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="installed">
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {plugins.filter(p => p.status === "Installed").map(p => (
              <Card key={p.id}>
                <CardHeader><CardTitle className="text-base">{p.name}</CardTitle><CardDescription>v{p.version}</CardDescription></CardHeader>
                <CardContent>
                  <Button variant="outline" className="w-full"><ExternalLink className="mr-1 h-4 w-4" />Configure</Button>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="available">
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {plugins.filter(p => p.status === "Available").map(p => (
              <Card key={p.id}>
                <CardHeader><CardTitle className="text-base">{p.name}</CardTitle><CardDescription>{p.description}</CardDescription></CardHeader>
                <CardContent>
                  <div className="flex items-center gap-2 mb-3 text-xs text-muted-foreground">
                    <Star className="h-3 w-3 fill-status-amber text-status-amber" /> {p.rating}
                    <span>· {p.downloads} downloads</span>
                  </div>
                  <Button className="w-full"><Download className="mr-1 h-4 w-4" />Install</Button>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
