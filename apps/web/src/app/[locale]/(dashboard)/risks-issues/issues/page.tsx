"use client";

import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Plus, Filter, Search, AlertCircle, CheckCircle2, Clock } from "lucide-react";
import { formatDate } from "@/lib/utils";
import { mockIssues, mockUsers, mockProjects } from "@/lib/mock-data";

export default function IssueLogPage() {
  const [search, setSearch] = useState("");

  const filtered = mockIssues.filter(i => !search || i.title.toLowerCase().includes(search.toLowerCase()));
  const openIssues = filtered.filter(i => i.status === "Open" || i.status === "In Progress");
  const resolvedIssues = filtered.filter(i => i.status === "Resolved" || i.status === "Closed");

  return (
    <div className="space-y-6">
      <PageHeader
        title="Issue Log"
        description={`${mockIssues.length} total issues`}
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />New Issue</Button>}
      />

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Open Issues</CardTitle>
            <AlertCircle className="h-4 w-4 text-status-red" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-status-red">{mockIssues.filter(i => i.status === "Open").length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">In Progress</CardTitle>
            <Clock className="h-4 w-4 text-status-amber" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-status-amber">{mockIssues.filter(i => i.status === "In Progress").length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Resolved</CardTitle>
            <CheckCircle2 className="h-4 w-4 text-status-green" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-status-green">{mockIssues.filter(i => i.status === "Resolved").length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Critical</CardTitle>
            <AlertCircle className="h-4 w-4 text-status-red" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-status-red">{mockIssues.filter(i => i.severity === "Critical").length}</div>
          </CardContent>
        </Card>
      </div>

      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input placeholder="Search issues..." className="pl-8" value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
      </div>

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All ({filtered.length})</TabsTrigger>
          <TabsTrigger value="open">Open ({openIssues.length})</TabsTrigger>
          <TabsTrigger value="resolved">Resolved ({resolvedIssues.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="rounded-md border">
            <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
              <div className="col-span-3">Issue</div>
              <div className="col-span-1">Project</div>
              <div className="col-span-1">Severity</div>
              <div className="col-span-2">Owner</div>
              <div className="col-span-2">Created</div>
              <div className="col-span-2">Resolved</div>
              <div className="col-span-1">Status</div>
            </div>
            {filtered.map((issue) => {
              const owner = mockUsers.find(u => u.id === issue.owner);
              const project = mockProjects.find(p => p.id === issue.projectId);
              return (
                <div key={issue.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                  <div className="col-span-3 font-medium">{issue.title}</div>
                  <div className="col-span-1 text-xs text-muted-foreground">{project?.code}</div>
                  <div className="col-span-1">
                    <Badge variant={issue.severity === "Critical" ? "destructive" : issue.severity === "High" ? "destructive" : "secondary"} className="text-[10px]">{issue.severity}</Badge>
                  </div>
                  <div className="col-span-2 text-muted-foreground">{owner?.name}</div>
                  <div className="col-span-2 text-muted-foreground text-xs">{formatDate(issue.createdAt)}</div>
                  <div className="col-span-2 text-xs text-muted-foreground">{issue.resolvedAt ? formatDate(issue.resolvedAt) : "-"}</div>
                  <div className="col-span-1">
                    <Badge variant={issue.status === "Resolved" ? "default" : issue.status === "Closed" ? "outline" : "destructive"} className="text-[10px]">{issue.status}</Badge>
                  </div>
                </div>
              );
            })}
          </div>
        </TabsContent>

        <TabsContent value="open">
          <div className="rounded-md border">
            {openIssues.map((issue) => (
              <div key={issue.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <div className="col-span-4 font-medium">{issue.title}</div>
                <div className="col-span-2"><Badge variant={issue.severity === "Critical" ? "destructive" : "secondary"} className="text-[10px]">{issue.severity}</Badge></div>
                <div className="col-span-3 text-muted-foreground">{formatDate(issue.createdAt)}</div>
                <div className="col-span-3"><Badge variant="destructive" className="text-[10px]">{issue.status}</Badge></div>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="resolved">
          <div className="rounded-md border">
            {resolvedIssues.map((issue) => (
              <div key={issue.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <div className="col-span-4 font-medium">{issue.title}</div>
                <div className="col-span-3 text-muted-foreground text-xs">Resolved: {issue.resolvedAt ? formatDate(issue.resolvedAt) : "-"}</div>
                <div className="col-span-3 text-muted-foreground text-xs">Created: {formatDate(issue.createdAt)}</div>
                <div className="col-span-2"><Badge variant="outline" className="text-[10px]">{issue.status}</Badge></div>
              </div>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
