"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Plus, FileText, Copy, CheckCircle2 } from "lucide-react";

const templates = [
  { id: "tpl-1", name: "Software Development (Agile)", category: "Technology", phases: 6, tasks: 24, used: 12 },
  { id: "tpl-2", name: "Infrastructure Project (Waterfall)", category: "Infrastructure", phases: 8, tasks: 32, used: 8 },
  { id: "tpl-3", name: "Business Transformation (Hybrid)", category: "Business", phases: 7, tasks: 28, used: 5 },
  { id: "tpl-4", name: "Regulatory Implementation", category: "Compliance", phases: 5, tasks: 20, used: 3 },
  { id: "tpl-5", name: "Data & Analytics Project", category: "Technology", phases: 6, tasks: 22, used: 7 },
  { id: "tpl-6", name: "Cloud Migration", category: "Infrastructure", phases: 7, tasks: 30, used: 9 },
];

export default function ProjectTemplatesPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Project Templates"
        description="Pre-configured project plans to accelerate project setup"
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />Create Template</Button>}
      />

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All Templates</TabsTrigger>
          <TabsTrigger value="technology">Technology</TabsTrigger>
          <TabsTrigger value="infrastructure">Infrastructure</TabsTrigger>
          <TabsTrigger value="business">Business</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {templates.map((tpl) => (
              <Card key={tpl.id}>
                <CardHeader>
                  <div className="flex items-start justify-between">
                    <div>
                      <CardTitle className="text-base">{tpl.name}</CardTitle>
                      <CardDescription>{tpl.category}</CardDescription>
                    </div>
                    <FileText className="h-5 w-5 text-muted-foreground" />
                  </div>
                </CardHeader>
                <CardContent className="space-y-3">
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Phases</span><span className="font-medium">{tpl.phases}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Tasks</span><span className="font-medium">{tpl.tasks}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Times Used</span><span className="font-medium">{tpl.used}</span>
                  </div>
                  <div className="flex gap-2 pt-2">
                    <Button size="sm" variant="default" className="flex-1"><Copy className="mr-1 h-4 w-4" />Use Template</Button>
                    <Button size="sm" variant="outline">Preview</Button>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="technology">
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {templates.filter(t => t.category === "Technology").map((tpl) => (
              <Card key={tpl.id}>
                <CardHeader>
                  <CardTitle className="text-base">{tpl.name}</CardTitle>
                  <CardDescription>{tpl.phases} phases, {tpl.tasks} tasks</CardDescription>
                </CardHeader>
                <CardContent>
                  <Button size="sm" className="w-full"><Copy className="mr-1 h-4 w-4" />Use Template</Button>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="infrastructure">
          <div className="grid gap-4 md:grid-cols-2">
            {templates.filter(t => t.category === "Infrastructure").map((tpl) => (
              <Card key={tpl.id}><CardHeader><CardTitle className="text-base">{tpl.name}</CardTitle></CardHeader>
                <CardContent><Button size="sm" className="w-full"><Copy className="mr-1 h-4 w-4" />Use Template</Button></CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="business">
          <div className="grid gap-4 md:grid-cols-2">
            {templates.filter(t => t.category === "Business").map((tpl) => (
              <Card key={tpl.id}><CardHeader><CardTitle className="text-base">{tpl.name}</CardTitle></CardHeader>
                <CardContent><Button size="sm" className="w-full"><Copy className="mr-1 h-4 w-4" />Use Template</Button></CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
