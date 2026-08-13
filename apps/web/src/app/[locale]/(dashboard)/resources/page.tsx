"use client";

import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { Input } from "@/components/ui/input";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Plus, Filter, Search, Users, UserCheck, Clock, Banknote, Loader2 } from "lucide-react";
import { formatCurrency, getInitials } from "@/lib/utils";
import { useResources } from "@/hooks/resources/useResources";
import { ResourceFinderDialog } from "@/components/resources/ResourceFinderDialog";

export default function ResourcesPage() {
  const [search, setSearch] = useState("");
  const [activeTab, setActiveTab] = useState("all");
  const { data, isLoading, error } = useResources({ search: search || undefined, pageSize: 50 });

  const resources = data?.items ?? [];
  const internal = resources.filter(r => r.type === "Employee");
  const outsourced = resources.filter(r => r.type === "Outsourced");
  const avgRate = resources.length > 0
    ? Math.round(resources.reduce((s, r) => s + (r.billableRate ?? 0), 0) / resources.length)
    : 0;

  const filtered = activeTab === "internal" ? internal : activeTab === "outsourced" ? outsourced : resources;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Resource Directory"
        description={data ? `${data.totalCount} resources available` : "Loading..."}
        actions={<div className="flex items-center gap-2"><ResourceFinderDialog /><Button size="sm"><Plus className="mr-2 h-4 w-4" />Add Resource</Button></div>}
      />

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Total Resources</CardTitle>
            <Users className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{data?.totalCount ?? resources.length}</div>
            <p className="text-xs text-muted-foreground">{internal.length} Internal · {outsourced.length} Outsourced</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Avg Rate</CardTitle>
            <Banknote className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{formatCurrency(avgRate)}</div>
            <p className="text-xs text-muted-foreground">Per hour</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Active</CardTitle>
            <UserCheck className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{resources.filter(r => r.isActive).length}</div>
            <p className="text-xs text-muted-foreground">{resources.filter(r => !r.isActive).length} inactive</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Avg Utilization</CardTitle>
            <Clock className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {resources.length > 0
                ? `${Math.round(resources.reduce((s, r) => s + (r.currentUtilization ?? 0), 0) / resources.length)}%`
                : "—"}
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input placeholder="Search resources by name, role, skills..." className="pl-8" value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      ) : error ? (
        <div className="rounded-md border bg-destructive/10 p-4 text-sm text-destructive">
          Failed to load resources: {(error as Error).message}
        </div>
      ) : (
        <Tabs defaultValue="all" onValueChange={setActiveTab}>
          <TabsList>
            <TabsTrigger value="all">All ({resources.length})</TabsTrigger>
            <TabsTrigger value="internal">Internal ({internal.length})</TabsTrigger>
            <TabsTrigger value="outsourced">Outsourced ({outsourced.length})</TabsTrigger>
          </TabsList>

          <TabsContent value="all">
            <div className="rounded-md border">
              <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
                <div className="col-span-3">Name</div>
                <div className="col-span-2">Role</div>
                <div className="col-span-2">Skills</div>
                <div className="col-span-1">Rate</div>
                <div className="col-span-1">Seniority</div>
                <div className="col-span-1">Type</div>
                <div className="col-span-1">Status</div>
                <div className="col-span-1">Location</div>
              </div>
              {filtered.map((res) => (
                <div key={res.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                  <div className="col-span-3 flex items-center gap-2 font-medium">
                    <Avatar className="h-6 w-6">
                      <AvatarFallback className="text-[8px]">{getInitials(res.name)}</AvatarFallback>
                    </Avatar>
                    {res.name}
                  </div>
                  <div className="col-span-2 text-muted-foreground">{res.jobTitle ?? "—"}</div>
                  <div className="col-span-2 flex flex-wrap gap-1">
                    {res.skills.slice(0, 2).map((skill) => (
                      <Badge key={skill.skillId} variant="secondary" className="text-[10px]">{skill.skillName}</Badge>
                    ))}
                    {res.skills.length > 2 && <span className="text-[10px] text-muted-foreground">+{res.skills.length - 2}</span>}
                  </div>
                  <div className="col-span-1 text-muted-foreground">
                    {res.billableRate ? formatCurrency(res.billableRate) + "/hr" : "—"}
                  </div>
                  <div className="col-span-1 text-muted-foreground">{res.seniority ?? "—"}</div>
                  <div className="col-span-1">
                    <Badge variant={res.type === "Employee" ? "default" : "secondary"} className="text-[10px]">
                      {res.type}
                    </Badge>
                  </div>
                  <div className="col-span-1">
                    <Badge variant={res.isActive ? "default" : "secondary"} className="text-[10px]">
                      {res.isActive ? "Active" : "Inactive"}
                    </Badge>
                  </div>
                  <div className="col-span-1 text-xs text-muted-foreground">{res.locationName ?? "—"}</div>
                </div>
              ))}
            </div>
          </TabsContent>

          <TabsContent value="internal">
            <div className="rounded-md border">
              <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
                <div className="col-span-3">Name</div>
                <div className="col-span-2">Role</div>
                <div className="col-span-2">Department</div>
                <div className="col-span-1">Rate</div>
                <div className="col-span-2">Skills</div>
                <div className="col-span-2">Utilization</div>
              </div>
              {filtered.map((res) => (
                <div key={res.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                  <div className="col-span-3 flex items-center gap-2 font-medium">
                    <Avatar className="h-6 w-6"><AvatarFallback className="text-[8px]">{getInitials(res.name)}</AvatarFallback></Avatar>
                    {res.name}
                  </div>
                  <div className="col-span-2 text-muted-foreground">{res.jobTitle ?? "—"}</div>
                  <div className="col-span-2 text-muted-foreground">{res.departmentName ?? "—"}</div>
                  <div className="col-span-1 text-muted-foreground">
                    {res.billableRate ? formatCurrency(res.billableRate) + "/hr" : "—"}
                  </div>
                  <div className="col-span-2 flex flex-wrap gap-1">
                    {res.skills.slice(0, 2).map((s) => <Badge key={s.skillId} variant="secondary" className="text-[10px]">{s.skillName}</Badge>)}
                  </div>
                  <div className="col-span-2">
                    <div className="flex items-center gap-2">
                      <Progress value={res.currentUtilization ?? 0} className="h-1.5 w-12" />
                      <span className="text-xs">{res.currentUtilization ?? 0}%</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </TabsContent>

          <TabsContent value="outsourced">
            <div className="rounded-md border">
              <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
                <div className="col-span-3">Name</div>
                <div className="col-span-2">Role</div>
                <div className="col-span-2">Employment</div>
                <div className="col-span-1">Rate</div>
                <div className="col-span-2">Location</div>
                <div className="col-span-2">Status</div>
              </div>
              {filtered.map((res) => (
                <div key={res.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                  <div className="col-span-3 flex items-center gap-2 font-medium">
                    <Avatar className="h-6 w-6"><AvatarFallback className="text-[8px]">{getInitials(res.name)}</AvatarFallback></Avatar>
                    {res.name}
                  </div>
                  <div className="col-span-2 text-muted-foreground">{res.jobTitle ?? "—"}</div>
                  <div className="col-span-2"><Badge variant="secondary" className="text-[10px]">{res.employmentType ?? "Contractor"}</Badge></div>
                  <div className="col-span-1 text-muted-foreground">
                    {res.billableRate ? formatCurrency(res.billableRate) + "/hr" : "—"}
                  </div>
                  <div className="col-span-2 text-xs text-muted-foreground">{res.locationName ?? "—"}</div>
                  <div className="col-span-2">
                    <Badge variant={res.isActive ? "default" : "secondary"} className="text-[10px]">
                      {res.isActive ? "Active" : "Inactive"}
                    </Badge>
                  </div>
                </div>
              ))}
            </div>
          </TabsContent>
        </Tabs>
      )}
    </div>
  );
}
