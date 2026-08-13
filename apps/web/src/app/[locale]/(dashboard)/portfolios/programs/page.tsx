"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { Plus, Filter, FolderKanban } from "lucide-react";
import { formatCurrency } from "@/lib/utils";
import { mockPortfolios, mockProjects } from "@/lib/mock-data";

const mockPrograms = [
  { id: "program-1", name: "Core Banking Transformation", portfolio: "portfolio-1", manager: "user-5", progress: 65, projects: 4 },
  { id: "program-2", name: "Digital Customer Experience", portfolio: "portfolio-2", manager: "user-6", progress: 45, projects: 4 },
  { id: "program-3", name: "Infrastructure Modernisation", portfolio: "portfolio-3", manager: "user-7", progress: 72, projects: 4 },
  { id: "program-4", name: "Business Process Optimisation", portfolio: "portfolio-4", manager: "user-8", progress: 50, projects: 4 },
  { id: "program-5", name: "Digital Lending & Channels", portfolio: "portfolio-2", manager: "user-6", progress: 55, projects: 3 },
  { id: "program-6", name: "Strategic Data & Analytics", portfolio: "portfolio-1", manager: "user-5", progress: 42, projects: 3 },
  { id: "program-7", name: "Security & Resilience", portfolio: "portfolio-3", manager: "user-7", progress: 58, projects: 3 },
];

export default function ProgramsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Programs"
        description="Programmes within each portfolio"
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />New Program</Button>}
      />

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {mockPrograms.map((program) => {
          const portfolio = mockPortfolios.find(p => p.id === program.portfolio);
          return (
            <Card key={program.id}>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle className="text-base">{program.name}</CardTitle>
                  <FolderKanban className="h-4 w-4 text-muted-foreground" />
                </div>
                <CardDescription>{portfolio?.name}</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Manager</span>
                  <span className="font-medium">{program.manager}</span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Projects</span>
                  <span className="font-medium">{program.projects}</span>
                </div>
                <div className="space-y-1">
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Progress</span>
                    <span>{program.progress}%</span>
                  </div>
                  <Progress value={program.progress} className="h-2" />
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
