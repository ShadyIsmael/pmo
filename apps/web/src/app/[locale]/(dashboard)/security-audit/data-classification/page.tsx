"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ShieldCheck, Lock, Eye, Users } from "lucide-react";

const classifications = [
  { level: "Public", icon: Eye, description: "Information that can be freely disclosed", color: "bg-status-green", examples: ["Project names", "Team members", "Meeting schedules"] },
  { level: "Internal", icon: Users, description: "Information for internal use only", color: "bg-blue-500", examples: ["Project status reports", "Resource allocation", "Budget summaries"] },
  { level: "Confidential", icon: Lock, description: "Sensitive information with restricted access", color: "bg-status-amber", examples: ["Financial details", "Vendor contracts", "Risk assessments"] },
  { level: "Restricted", icon: ShieldCheck, description: "Highly sensitive information with strict controls", color: "bg-status-red", examples: ["Security credentials", "Audit logs", "Personal data"] },
];

export default function DataClassificationPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Data Classification" description="Information classification framework" />

      <div className="grid gap-4 md:grid-cols-2">
        {classifications.map((c) => {
          const Icon = c.icon;
          return (
            <Card key={c.level}>
              <CardHeader>
                <div className="flex items-center gap-3">
                  <div className={`flex h-10 w-10 items-center justify-center rounded-lg ${c.color}/10`}>
                    <Icon className={`h-5 w-5 ${c.color} text-white`} />
                  </div>
                  <div>
                    <CardTitle className="text-base">{c.level}</CardTitle>
                    <p className="text-xs text-muted-foreground">{c.description}</p>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <p className="text-xs font-medium text-muted-foreground mb-2">Examples:</p>
                <ul className="space-y-1">
                  {c.examples.map((ex) => (
                    <li key={ex} className="flex items-center gap-2 text-xs text-muted-foreground">
                      <span className={`h-1.5 w-1.5 rounded-full ${c.color}`} />
                      {ex}
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
