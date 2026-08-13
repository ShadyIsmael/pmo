"use client";

import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Plus, Filter, Search, AlertTriangle, TrendingUp, Shield } from "lucide-react";
import { formatDate } from "@/lib/utils";
import { mockRisks, mockUsers, mockProjects } from "@/lib/mock-data";

const riskCategories = [...new Set(mockRisks.map(r => r.category))];

export default function RiskRegisterPage() {
  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("All");

  const filtered = mockRisks.filter(r => {
    if (categoryFilter !== "All" && r.category !== categoryFilter) return false;
    if (search && !r.title.toLowerCase().includes(search.toLowerCase())) return false;
    return true;
  });

  const highRisk = mockRisks.filter(r => r.score >= 15);
  const mediumRisk = mockRisks.filter(r => r.score >= 8 && r.score < 15);
  const lowRisk = mockRisks.filter(r => r.score < 8);

  const heatMapData: number[][] = Array.from({ length: 5 }, () => Array(5).fill(0));
  mockRisks.forEach(r => {
    const idx = r.impact - 1;
    const jdx = r.probability - 1;
    if (idx >= 0 && idx < 5 && jdx >= 0 && jdx < 5) heatMapData[idx]![jdx]!++;
  });

  const getHeatColor = (count: number) => {
    if (count === 0) return "bg-muted";
    if (count <= 2) return "bg-status-green/30";
    if (count <= 4) return "bg-status-amber/40";
    return "bg-status-red/50";
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="Risk Register"
        description={`${mockRisks.length} total risks across all projects`}
        actions={
          <div className="flex gap-2">
            <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Filter</Button>
            <Button size="sm"><Plus className="mr-2 h-4 w-4" />New Risk</Button>
          </div>
        }
      />

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Total Risks</CardTitle>
            <AlertTriangle className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{mockRisks.length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">High (Score 15-25)</CardTitle>
            <AlertTriangle className="h-4 w-4 text-status-red" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-status-red">{highRisk.length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Medium (Score 8-14)</CardTitle>
            <TrendingUp className="h-4 w-4 text-status-amber" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-status-amber">{mediumRisk.length}</div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">Low (Score 1-7)</CardTitle>
            <Shield className="h-4 w-4 text-status-green" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-status-green">{lowRisk.length}</div>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-1">
          <CardHeader>
            <CardTitle className="text-base">Risk Heat Map</CardTitle>
            <CardDescription>Impact vs Probability</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-1">
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                <div className="w-16 text-right">Impact</div>
                <div className="flex-1" />
                <div className="text-center text-[10px]" style={{ width: "140px" }}>Probability →</div>
              </div>
              {heatMapData.map((row, i) => (
                <div key={i} className="flex items-center gap-2">
                  <div className="w-16 text-right text-xs text-muted-foreground">{5 - i}</div>
                  <div className="flex flex-1 gap-1">
                    {row.map((count, j) => (
                      <div
                        key={j}
                        className={`flex h-8 flex-1 items-center justify-center rounded text-xs font-medium ${getHeatColor(count)}`}
                      >
                        {count > 0 ? count : ""}
                      </div>
                    ))}
                  </div>
                </div>
              ))}
              <div className="flex items-center gap-2 pt-1">
                <div className="w-16" />
                <div className="flex flex-1 gap-1">
                  {[1, 2, 3, 4, 5].map((v) => (
                    <div key={v} className="flex-1 text-center text-[10px] text-muted-foreground">{v}</div>
                  ))}
                </div>
              </div>
              <div className="mt-3 flex items-center gap-3 text-xs text-muted-foreground">
                <span className="flex items-center gap-1"><span className="h-2.5 w-2.5 rounded bg-status-green/30" /> Low</span>
                <span className="flex items-center gap-1"><span className="h-2.5 w-2.5 rounded bg-status-amber/40" /> Medium</span>
                <span className="flex items-center gap-1"><span className="h-2.5 w-2.5 rounded bg-status-red/50" /> High</span>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Risk Distribution</CardTitle>
            <CardDescription>By category</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {riskCategories.map((cat) => {
                const count = mockRisks.filter(r => r.category === cat).length;
                const maxCount = Math.max(...riskCategories.map(c => mockRisks.filter(r => r.category === c).length));
                return (
                  <div key={cat} className="flex items-center gap-3">
                    <span className="w-24 text-xs font-medium">{cat}</span>
                    <div className="flex h-5 flex-1 rounded-full bg-muted">
                      <div
                        className="h-full rounded-full bg-primary"
                        style={{ width: `${(count / maxCount) * 100}%` }}
                      />
                    </div>
                    <span className="w-6 text-right text-xs text-muted-foreground">{count}</span>
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input placeholder="Search risks..." className="pl-8" value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <Select value={categoryFilter} onValueChange={setCategoryFilter}>
          <SelectTrigger className="w-[160px]">
            <SelectValue placeholder="Category" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="All">All Categories</SelectItem>
            {riskCategories.map(c => <SelectItem key={c} value={c}>{c}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All Risks</TabsTrigger>
          <TabsTrigger value="open">Open</TabsTrigger>
          <TabsTrigger value="mitigating">Mitigating</TabsTrigger>
          <TabsTrigger value="closed">Closed</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="rounded-md border">
            <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
              <div className="col-span-3">Risk</div>
              <div className="col-span-1">Project</div>
              <div className="col-span-2">Category</div>
              <div className="col-span-1">P/I</div>
              <div className="col-span-1">Score</div>
              <div className="col-span-2">Owner</div>
              <div className="col-span-1">Updated</div>
              <div className="col-span-1">Status</div>
            </div>
            {filtered.map((risk) => {
              const owner = mockUsers.find(u => u.id === risk.owner);
              const project = mockProjects.find(p => p.id === risk.projectId);
              return (
                <div key={risk.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                  <div className="col-span-3 font-medium">{risk.title}</div>
                  <div className="col-span-1 text-muted-foreground text-xs">{project?.code}</div>
                  <div className="col-span-2 text-muted-foreground">{risk.category}</div>
                  <div className="col-span-1 text-muted-foreground">{risk.probability}/{risk.impact}</div>
                  <div className="col-span-1">
                    <span className={`font-medium ${risk.score >= 15 ? "text-status-red" : risk.score >= 8 ? "text-status-amber" : "text-status-green"}`}>{risk.score}</span>
                  </div>
                  <div className="col-span-2 text-muted-foreground">{owner?.name}</div>
                  <div className="col-span-1 text-xs text-muted-foreground">{formatDate(risk.updatedAt)}</div>
                  <div className="col-span-1">
                    <Badge variant={risk.status === "Open" ? "destructive" : risk.status === "Mitigating" ? "secondary" : "outline"} className="text-[10px]">{risk.status}</Badge>
                  </div>
                </div>
              );
            })}
          </div>
        </TabsContent>

        <TabsContent value="open">
          <div className="rounded-md border">
            {mockRisks.filter(r => r.status === "Open").map((risk) => (
              <div key={risk.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <div className="col-span-4 font-medium">{risk.title}</div>
                <div className="col-span-2 text-muted-foreground">{risk.category}</div>
                <div className="col-span-2">
                  <span className={`font-medium ${risk.score >= 15 ? "text-status-red" : "text-status-amber"}`}>{risk.score}</span>
                </div>
                <div className="col-span-4 text-xs text-muted-foreground">{risk.mitigationPlan}</div>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="mitigating">
          <div className="rounded-md border">
            {mockRisks.filter(r => r.status === "Mitigating").map((risk) => (
              <div key={risk.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <div className="col-span-4 font-medium">{risk.title}</div>
                <div className="col-span-3 text-muted-foreground">{risk.category}</div>
                <div className="col-span-3 text-xs text-muted-foreground">{risk.mitigationPlan}</div>
                <div className="col-span-2">
                  <span className={`font-medium ${risk.score >= 15 ? "text-status-red" : "text-status-amber"}`}>Score: {risk.score}</span>
                </div>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="closed">
          <div className="rounded-md border">
            {mockRisks.filter(r => r.status === "Closed").map((risk) => (
              <div key={risk.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                <div className="col-span-5 font-medium">{risk.title}</div>
                <div className="col-span-3 text-muted-foreground">{risk.category}</div>
                <div className="col-span-2 text-xs text-muted-foreground">Closed: {formatDate(risk.updatedAt)}</div>
                <div className="col-span-2"><Badge variant="outline" className="text-[10px]">Closed</Badge></div>
              </div>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
