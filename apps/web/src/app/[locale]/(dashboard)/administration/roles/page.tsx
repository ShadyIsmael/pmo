"use client";

import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Plus, Shield, CheckCircle2, XCircle } from "lucide-react";

const roles = [
  { name: "Executive", users: 4, permissions: 42, description: "Full access to all portfolios and projects" },
  { name: "Portfolio Manager", users: 4, permissions: 36, description: "Manage portfolios and programs" },
  { name: "Project Manager", users: 11, permissions: 28, description: "Manage assigned projects and resources" },
  { name: "Team Lead", users: 8, permissions: 20, description: "Manage team tasks and reporting" },
  { name: "Team Member", users: 45, permissions: 14, description: "View assigned tasks and timesheets" },
  { name: "PMO Director", users: 1, permissions: 38, description: "Oversee PMO operations and standards" },
  { name: "PMO Analyst", users: 3, permissions: 24, description: "Support PMO reporting and analytics" },
  { name: "Resource Manager", users: 2, permissions: 22, description: "Manage resource allocation" },
  { name: "Financial Controller", users: 2, permissions: 18, description: "Financial oversight and budgets" },
  { name: "Risk Manager", users: 2, permissions: 20, description: "Risk management and mitigation" },
  { name: "Administrator", users: 2, permissions: 48, description: "Full system administration access" },
  { name: "Auditor", users: 1, permissions: 16, description: "Read-only audit access" },
];

const allPermissions = [
  { category: "Projects", perms: ["View", "Create", "Edit", "Delete", "Approve", "Close", "View Financials", "Edit Financials"] },
  { category: "Portfolios", perms: ["View", "Create", "Edit", "Delete", "Manage"] },
  { category: "Resources", perms: ["View", "Allocate", "Deallocate", "Manage Rates", "View Capacity"] },
  { category: "Risks", perms: ["View", "Create", "Edit", "Delete", "Mitigate", "Escalate"] },
];

export default function AdminRolesPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Roles & Permissions"
        description="Manage roles and access control"
        actions={<Button size="sm"><Plus className="mr-2 h-4 w-4" />New Role</Button>}
      />

      <Tabs defaultValue="roles">
        <TabsList>
          <TabsTrigger value="roles">Roles ({roles.length})</TabsTrigger>
          <TabsTrigger value="permissions">Permissions</TabsTrigger>
        </TabsList>

        <TabsContent value="roles">
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {roles.map((role) => (
              <Card key={role.name}>
                <CardHeader>
                  <div className="flex items-start justify-between">
                    <div>
                      <CardTitle className="text-base">{role.name}</CardTitle>
                      <CardDescription>{role.description}</CardDescription>
                    </div>
                    <Shield className="h-5 w-5 text-muted-foreground" />
                  </div>
                </CardHeader>
                <CardContent className="space-y-2">
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Users</span><span className="font-medium">{role.users}</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Permissions</span><span className="font-medium">{role.permissions}</span>
                  </div>
                  <Button variant="outline" size="sm" className="w-full mt-2">Edit Role</Button>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="permissions">
          <div className="space-y-4">
            {allPermissions.map((group) => (
              <Card key={group.category}>
                <CardHeader><CardTitle className="text-base">{group.category}</CardTitle></CardHeader>
                <CardContent>
                  <div className="flex flex-wrap gap-2">
                    {group.perms.map((perm) => (
                      <Badge key={perm} variant="secondary" className="text-[10px]">{perm}</Badge>
                    ))}
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
