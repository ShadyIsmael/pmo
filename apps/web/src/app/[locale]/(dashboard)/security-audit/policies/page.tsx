"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { ShieldCheck, Plus } from "lucide-react";
import { getLookupTextClass } from "@/lib/status-presentation";

const policies = [
  { name: "Password Complexity", description: "Minimum 12 characters, mixed case, numbers, special chars", status: "Active", scope: "Global" },
  { name: "MFA Enforcement", description: "Multi-factor authentication required for all users", status: "Active", scope: "Global" },
  { name: "Session Timeout", description: "Automatic logout after 30 minutes of inactivity", status: "Active", scope: "Global" },
  { name: "IP Whitelisting", description: "Access restricted to corporate VPN IP ranges", status: "Inactive", scope: "Global" },
  { name: "Data Export Controls", description: "Export of project data requires admin approval", status: "Active", scope: "Project" },
  { name: "Audit Log Retention", description: "Audit logs retained for minimum 365 days", status: "Active", scope: "System" },
];

export default function SecurityPoliciesPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Security Policies"
        description="Manage security and compliance policies"
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />New Policy</Button>}
      />

      <div className="space-y-3">
        {policies.map((policy) => (
          <Card key={policy.name}>
            <CardContent className="flex items-center gap-4 py-4">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
                <ShieldCheck className="h-5 w-5" />
              </div>
              <div className="flex-1">
                <div className="flex items-center gap-2">
                  <p className="font-medium">{policy.name}</p>
                  <Badge status={policy.status} className="text-[10px]">{policy.status}</Badge>
                  <span className={`text-xs font-medium ${getLookupTextClass(policy.scope)}`}>{policy.scope}</span>
                </div>
                <p className="text-xs text-muted-foreground mt-1">{policy.description}</p>
              </div>
              <Button size="sm" variant="outline">Edit</Button>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
