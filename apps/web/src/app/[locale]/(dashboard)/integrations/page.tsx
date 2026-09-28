"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { CheckCircle2, RefreshCw, Settings } from "lucide-react";

const integrations = [
  { id: "int-1", name: "Microsoft Project", description: "Import and sync project plans from Microsoft Project", status: "Connected", type: "Project Management", icon: "📊" },
  { id: "int-2", name: "Jira", description: "Sync issues, sprints, and release data from Jira", status: "Connected", type: "Development", icon: "🔧" },
  { id: "int-3", name: "ServiceNow", description: "Connect with ServiceNow for IT service management", status: "Available", type: "ITSM", icon: "🔄" },
  { id: "int-4", name: "SAP", description: "Financial and resource data integration with SAP", status: "Available", type: "ERP", icon: "💰" },
  { id: "int-5", name: "Confluence", description: "Link project documentation from Confluence spaces", status: "Connected", type: "Documentation", icon: "📝" },
  { id: "int-6", name: "Slack", description: "Receive notifications and updates in Slack channels", status: "Available", type: "Communication", icon: "💬" },
  { id: "int-7", name: "GitHub", description: "Track commits, PRs, and code reviews", status: "Connected", type: "Development", icon: "🐙" },
  { id: "int-8", name: "Power BI", description: "Export reports and dashboards to Power BI", status: "Available", type: "Reporting", icon: "📈" },
  { id: "int-9", name: "Azure DevOps", description: "End-to-end DevOps integration with Azure", status: "Available", type: "DevOps", icon: "☁️" },
  { id: "int-10", name: "Salesforce", description: "CRM project integration with Salesforce", status: "Available", type: "CRM", icon: "🏆" },
];

export default function IntegrationsPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Integration Hub" description="Connect NEXUS PMO with your enterprise tools" />

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All ({integrations.length})</TabsTrigger>
          <TabsTrigger value="connected">Connected ({integrations.filter(i => i.status === "Connected").length})</TabsTrigger>
          <TabsTrigger value="available">Available ({integrations.filter(i => i.status === "Available").length})</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="grid gap-4 md:grid-cols-2">
            {integrations.map((integration) => (
              <Card key={integration.id}>
                <CardHeader>
                  <div className="flex items-start justify-between">
                    <div className="flex items-start gap-3">
                      <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted text-lg">
                        {integration.icon}
                      </div>
                      <div>
                        <CardTitle className="text-base">{integration.name}</CardTitle>
                        <CardDescription>{integration.description}</CardDescription>
                      </div>
                    </div>
                    <Badge status={integration.status} className="text-[10px]">
                      {integration.status === "Connected" ? <CheckCircle2 className="mr-1 h-3 w-3" /> : null}
                      {integration.status}
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent>
                  <div className="flex items-center justify-between">
                    <Badge variant="outline" className="text-[10px]">{integration.type}</Badge>
                    <div className="flex gap-2">
                      {integration.status === "Connected" ? (
                        <>
                          <Button size="sm" variant="outline"><RefreshCw className="mr-1 h-3 w-3" />Sync</Button>
                          <Button size="sm" variant="outline"><Settings className="mr-1 h-3 w-3" />Configure</Button>
                        </>
                      ) : (
                        <Button size="sm">Connect</Button>
                      )}
                    </div>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="connected">
          <div className="grid gap-4 md:grid-cols-2">
            {integrations.filter(i => i.status === "Connected").map((integration) => (
              <Card key={integration.id}>
                <CardHeader>
                  <div className="flex items-center gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted text-lg">{integration.icon}</div>
                    <div>
                      <CardTitle className="text-base">{integration.name}</CardTitle>
                      <CardDescription>{integration.type}</CardDescription>
                    </div>
                  </div>
                </CardHeader>
                <CardContent>
                  <Button size="sm" variant="outline" className="w-full"><Settings className="mr-1 h-4 w-4" />Configure</Button>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="available">
          <div className="grid gap-4 md:grid-cols-2">
            {integrations.filter(i => i.status === "Available").map((integration) => (
              <Card key={integration.id}>
                <CardHeader>
                  <div className="flex items-center gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted text-lg">{integration.icon}</div>
                    <div>
                      <CardTitle className="text-base">{integration.name}</CardTitle>
                      <CardDescription>{integration.description}</CardDescription>
                    </div>
                  </div>
                </CardHeader>
                <CardContent>
                  <Button size="sm" className="w-full">Connect Now</Button>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
