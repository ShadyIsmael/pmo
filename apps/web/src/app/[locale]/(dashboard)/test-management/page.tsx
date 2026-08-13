"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Plus, Filter, Search, Beaker, CheckCircle2, XCircle, AlertCircle } from "lucide-react";
import { formatDate } from "@/lib/utils";
import { mockTestCases } from "@/lib/mock-data";

export default function TestManagementPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Test Management"
        description={`${mockTestCases.length} test cases`}
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />New Test Case</Button>}
      />

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Passed</CardTitle>
            <CheckCircle2 className="h-4 w-4 text-status-green" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold text-status-green">{mockTestCases.filter(t => t.status === "Passed").length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Failed</CardTitle>
            <XCircle className="h-4 w-4 text-status-red" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold text-status-red">{mockTestCases.filter(t => t.status === "Failed").length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Blocked</CardTitle>
            <AlertCircle className="h-4 w-4 text-status-amber" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold text-status-amber">{mockTestCases.filter(t => t.status === "Blocked").length}</div></CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Draft / Ready</CardTitle>
            <Beaker className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent><div className="text-2xl font-bold">{mockTestCases.filter(t => t.status === "Draft" || t.status === "Ready").length}</div></CardContent>
        </Card>
      </div>

      <div className="flex items-center gap-2">
        <div className="relative flex-1"><Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" /><Input placeholder="Search test cases..." className="pl-8" /></div>
        <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
      </div>

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All</TabsTrigger>
          <TabsTrigger value="passed">Passed</TabsTrigger>
          <TabsTrigger value="failed">Failed</TabsTrigger>
          <TabsTrigger value="blocked">Blocked</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="rounded-md border">
            <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
              <div className="col-span-4">Test Case</div>
              <div className="col-span-2">Priority</div>
              <div className="col-span-2">Assigned To</div>
              <div className="col-span-2">Last Run</div>
              <div className="col-span-2">Status</div>
            </div>
            {mockTestCases.map((tc) => (
              <div key={tc.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <div className="col-span-4 font-medium">{tc.title}</div>
                <div className="col-span-2">
                  <Badge variant={tc.priority === "Critical" ? "destructive" : "secondary"} className="text-[10px]">{tc.priority}</Badge>
                </div>
                <div className="col-span-2 text-muted-foreground">{tc.assignedTo}</div>
                <div className="col-span-2 text-muted-foreground text-xs">{formatDate(tc.lastRun)}</div>
                <div className="col-span-2">
                  <Badge variant={tc.status === "Passed" ? "default" : tc.status === "Failed" ? "destructive" : tc.status === "Blocked" ? "destructive" : "secondary"} className="text-[10px]">{tc.status}</Badge>
                </div>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="passed">
          <div className="rounded-md border">
            {mockTestCases.filter(t => t.status === "Passed").map(tc => (
              <div key={tc.id} className="flex items-center gap-4 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <CheckCircle2 className="h-4 w-4 text-status-green shrink-0" />
                <span className="flex-1 font-medium">{tc.title}</span>
                <span className="text-xs text-muted-foreground">{tc.assignedTo}</span>
                <Badge variant="default" className="text-[10px]">Passed</Badge>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="failed">
          <div className="rounded-md border">
            {mockTestCases.filter(t => t.status === "Failed").map(tc => (
              <div key={tc.id} className="flex items-center gap-4 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <XCircle className="h-4 w-4 text-status-red shrink-0" />
                <span className="flex-1 font-medium">{tc.title}</span>
                <Badge variant="destructive" className="text-[10px]">Failed</Badge>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="blocked">
          <div className="rounded-md border">
            {mockTestCases.filter(t => t.status === "Blocked").map(tc => (
              <div key={tc.id} className="flex items-center gap-4 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <AlertCircle className="h-4 w-4 text-status-amber shrink-0" />
                <span className="flex-1 font-medium">{tc.title}</span>
                <Badge variant="destructive" className="text-[10px]">Blocked</Badge>
              </div>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
