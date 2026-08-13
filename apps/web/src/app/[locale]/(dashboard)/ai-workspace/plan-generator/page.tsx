"use client";

import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Progress } from "@/components/ui/progress";
import { Sparkles, FileText, CheckCircle2, Loader2 } from "lucide-react";

export default function PlanGeneratorPage() {
  const [step, setStep] = useState<"form" | "generating" | "result">("form");

  return (
    <div className="space-y-6">
      <PageHeader
        title="AI Plan Generator"
        description="Generate comprehensive project plans using AI"
        actions={
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Sparkles className="h-4 w-4 text-primary" />
            Powered by NEXUS AI
          </div>
        }
      />

      {step === "form" && (
        <div className="grid gap-6 lg:grid-cols-3">
          <div className="lg:col-span-2 space-y-4">
            <Card>
              <CardHeader><CardTitle className="text-base">Project Details</CardTitle></CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <label className="text-sm font-medium">Project Name</label>
                  <Input placeholder="e.g. Cloud Migration Initiative" />
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">Project Description</label>
                  <Textarea placeholder="Describe the project objectives, scope, and key deliverables..." className="h-24" />
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Methodology</label>
                    <Select defaultValue="agile">
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="agile">Agile</SelectItem>
                        <SelectItem value="waterfall">Waterfall</SelectItem>
                        <SelectItem value="hybrid">Hybrid</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Priority</label>
                    <Select defaultValue="high">
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="critical">Critical</SelectItem>
                        <SelectItem value="high">High</SelectItem>
                        <SelectItem value="medium">Medium</SelectItem>
                        <SelectItem value="low">Low</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Estimated Duration</label>
                    <Select defaultValue="6">
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {[3, 6, 9, 12, 18, 24].map(m => <SelectItem key={m} value={String(m)}>{m} months</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Team Size</label>
                    <Select defaultValue="10">
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {[5, 10, 15, 20, 30, 50].map(s => <SelectItem key={s} value={String(s)}>{s} people</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                </div>
                <div className="space-y-2">
                  <label className="text-sm font-medium">Key Constraints or Requirements (optional)</label>
                  <Textarea placeholder="e.g. Must comply with ISO 27001, integration with legacy systems..." className="h-20" />
                </div>
              </CardContent>
            </Card>
          </div>

          <div className="space-y-4">
            <Card>
              <CardHeader><CardTitle className="text-base">Template</CardTitle></CardHeader>
              <CardContent className="space-y-2">
                {["Software Development (Agile)", "Infrastructure (Waterfall)", "Business Transformation (Hybrid)"].map(t => (
                  <div key={t} className="flex items-center gap-2 rounded-md border p-3 text-sm hover:bg-muted/50 cursor-pointer">
                    <FileText className="h-4 w-4 text-muted-foreground" />
                    {t}
                  </div>
                ))}
              </CardContent>
            </Card>
            <Card>
              <CardHeader><CardTitle className="text-base">What you'll get</CardTitle></CardHeader>
              <CardContent className="space-y-2 text-sm text-muted-foreground">
                <div className="flex items-center gap-2"><CheckCircle2 className="h-4 w-4 text-status-green" /> Phases & Milestones</div>
                <div className="flex items-center gap-2"><CheckCircle2 className="h-4 w-4 text-status-green" /> Task breakdown</div>
                <div className="flex items-center gap-2"><CheckCircle2 className="h-4 w-4 text-status-green" /> Resource estimates</div>
                <div className="flex items-center gap-2"><CheckCircle2 className="h-4 w-4 text-status-green" /> Risk identification</div>
                <div className="flex items-center gap-2"><CheckCircle2 className="h-4 w-4 text-status-green" /> Timeline & dependencies</div>
              </CardContent>
            </Card>
            <Button className="w-full" size="lg" onClick={() => setStep("generating")}>
              <Sparkles className="mr-2 h-5 w-5" />
              Generate Plan
            </Button>
          </div>
        </div>
      )}

      {step === "generating" && (
        <Card className="py-12">
          <CardContent className="flex flex-col items-center gap-4">
            <Loader2 className="h-12 w-12 animate-spin text-primary" />
            <h3 className="text-lg font-medium">Generating your project plan...</h3>
            <p className="text-sm text-muted-foreground">Analysing best practices and historical data for Cloud Migration Initiative</p>
            <Progress value={65} className="h-2 w-64" />
            <p className="text-xs text-muted-foreground">Analysing similar projects...</p>
            <Button variant="outline" size="sm" className="mt-4" onClick={() => setStep("result")}>Skip to Results (Demo)</Button>
          </CardContent>
        </Card>
      )}

      {step === "result" && (
        <div className="space-y-4">
          <Card>
            <CardHeader>
              <div className="flex items-center gap-2">
                <Sparkles className="h-5 w-5 text-primary" />
                <CardTitle className="text-lg">Generated Plan: Cloud Migration Initiative</CardTitle>
              </div>
              <CardDescription>AI-generated project plan based on your inputs</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                <div className="rounded-lg border p-4">
                  <h4 className="font-medium mb-2">Phase 1: Discovery & Assessment (Weeks 1-4)</h4>
                  <div className="space-y-1 text-sm text-muted-foreground">
                    <p>• Current state assessment and dependency mapping</p>
                    <p>• Cloud readiness evaluation</p>
                    <p>• Security and compliance requirements gathering</p>
                    <p className="flex items-center gap-1"><Badge variant="outline" className="text-[10px]">Milestone</Badge> Assessment Complete</p>
                  </div>
                </div>
                <div className="rounded-lg border p-4">
                  <h4 className="font-medium mb-2">Phase 2: Planning & Design (Weeks 5-10)</h4>
                  <div className="space-y-1 text-sm text-muted-foreground">
                    <p>• Target architecture design</p>
                    <p>• Migration strategy and cutover plan</p>
                    <p>• Resource planning and team allocation</p>
                    <p className="flex items-center gap-1"><Badge variant="outline" className="text-[10px]">Milestone</Badge> Design Sign-off</p>
                  </div>
                </div>
                <div className="rounded-lg border p-4">
                  <h4 className="font-medium mb-2">Phase 3: Execution (Weeks 11-22)</h4>
                  <div className="space-y-1 text-sm text-muted-foreground">
                    <p>• Environment provisioning and configuration</p>
                    <p>• Workload migration in defined waves</p>
                    <p>• Testing and validation per wave</p>
                  </div>
                </div>
                <div className="rounded-lg border p-4">
                  <h4 className="font-medium mb-2">Phase 4: Transition & Close (Weeks 23-26)</h4>
                  <div className="space-y-1 text-sm text-muted-foreground">
                    <p>• Cutover and go-live</p>
                    <p>• Hypercare support and issue resolution</p>
                    <p>• Documentation and lessons learned</p>
                    <p className="flex items-center gap-1"><Badge variant="outline" className="text-[10px]">Milestone</Badge> Project Complete</p>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
          <div className="flex gap-2">
            <Button variant="outline">Edit Plan</Button>
            <Button variant="outline">Export as PDF</Button>
            <Button>Create Project from Plan</Button>
          </div>
        </div>
      )}
    </div>
  );
}
