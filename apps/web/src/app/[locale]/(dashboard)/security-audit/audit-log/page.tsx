"use client";

import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Filter, Search, Download, ShieldCheck, UserCheck, AlertTriangle, Info } from "lucide-react";
import { formatDate } from "@/lib/utils";

const auditLogs = [
  { id: "aud-1", user: "Daniel O'Brien", action: "Project.Update", resource: "Core Banking Modernisation", details: "Updated project status to Active", timestamp: "2026-07-29T14:32:00", severity: "info" },
  { id: "aud-2", user: "Sophie Turner", action: "Budget.Modify", resource: "ERP Cloud Migration", details: "Modified budget from £30M to £32M", timestamp: "2026-07-29T11:15:00", severity: "warning" },
  { id: "aud-3", user: "Rachel Green", action: "User.Permission.Change", resource: "User: user-22", details: "Changed role from Team Member to Team Lead", timestamp: "2026-07-28T16:45:00", severity: "warning" },
  { id: "aud-4", user: "Raj Kapoor", action: "Risk.Create", resource: "AI-Powered Risk Analytics", details: "Created risk: AI model accuracy below threshold", timestamp: "2026-07-28T14:20:00", severity: "info" },
  { id: "aud-5", user: "Liam O'Connor", action: "Task.Assign", resource: "Omni-Channel Customer Hub", details: "Assigned task to user-24", timestamp: "2026-07-28T10:30:00", severity: "info" },
  { id: "aud-6", user: "System", action: "Integration.Sync", resource: "Jira", details: "Synchronized 24 issues from Jira project OCH-2024", timestamp: "2026-07-28T09:00:00", severity: "info" },
  { id: "aud-7", user: "Admin", action: "User.Login.Failed", resource: "user-45@nexus.com", details: "Failed login attempt from IP 192.168.1.100", timestamp: "2026-07-27T23:15:00", severity: "critical" },
  { id: "aud-8", user: "Nadia Khan", action: "Document.Upload", resource: "Data Centre Consolidation", details: "Uploaded: Deployment Runbook v1.0", timestamp: "2026-07-27T15:45:00", severity: "info" },
  { id: "aud-9", user: "Admin", action: "Settings.Modify", resource: "Global Settings", details: "Modified email notification settings", timestamp: "2026-07-27T14:00:00", severity: "info" },
  { id: "aud-10", user: "System", action: "Backup.Completed", resource: "Database", details: "Automated daily backup completed successfully", timestamp: "2026-07-27T03:00:00", severity: "info" },
  { id: "aud-11", user: "Carlos Mendez", action: "Meeting.Create", resource: "Regulatory Compliance Suite", details: "Created meeting: Regulatory Compliance Review", timestamp: "2026-07-26T11:30:00", severity: "info" },
  { id: "aud-12", user: "Ahmed Hassan", action: "Project.Delete.Attempt", resource: "Network Security Upgrade", details: "Attempted to delete project - denied: insufficient permissions", timestamp: "2026-07-26T09:20:00", severity: "critical" },
];

const severityIcons: Record<string, typeof Info> = {
  info: Info,
  warning: AlertTriangle,
  critical: AlertTriangle,
};

const severityColors: Record<string, string> = {
  info: "text-blue-500",
  warning: "text-status-amber",
  critical: "text-status-red",
};

export default function AuditLogPage() {
  const [search, setSearch] = useState("");
  const [severityFilter, setSeverityFilter] = useState("All");

  const filtered = auditLogs.filter(l => {
    if (severityFilter !== "All" && l.severity !== severityFilter) return false;
    if (search && !l.user.toLowerCase().includes(search.toLowerCase()) && !l.action.toLowerCase().includes(search.toLowerCase()) && !l.resource.toLowerCase().includes(search.toLowerCase())) return false;
    return true;
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Audit Log"
        description={`${auditLogs.length} recorded events`}
        actions={
          <div className="flex gap-2">
            <Button variant="outline" size="sm"><Download className="mr-2 h-4 w-4" />Export</Button>
            <Button variant="outline" size="sm"><Filter className="mr-2 h-4 w-4" />Advanced Filter</Button>
          </div>
        }
      />

      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input placeholder="Search audit log by user, action, or resource..." className="pl-8" value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <Select value={severityFilter} onValueChange={setSeverityFilter}>
          <SelectTrigger className="w-[140px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="All">All Severities</SelectItem>
            <SelectItem value="info">Info</SelectItem>
            <SelectItem value="warning">Warning</SelectItem>
            <SelectItem value="critical">Critical</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All ({filtered.length})</TabsTrigger>
          <TabsTrigger value="critical">Critical ({auditLogs.filter(l => l.severity === "critical").length})</TabsTrigger>
          <TabsTrigger value="warning">Warning ({auditLogs.filter(l => l.severity === "warning").length})</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="rounded-md border">
            <div className="grid grid-cols-12 gap-2 border-b bg-muted/50 px-4 py-3 text-xs font-medium text-muted-foreground">
              <div className="col-span-2">Timestamp</div>
              <div className="col-span-2">User</div>
              <div className="col-span-2">Action</div>
              <div className="col-span-3">Resource / Details</div>
              <div className="col-span-2">Details</div>
              <div className="col-span-1">Severity</div>
            </div>
            {filtered.map((log) => {
              const Icon = severityIcons[log.severity] ?? Info;
              return (
                <div key={log.id} className="grid grid-cols-12 gap-2 border-b px-4 py-3 text-sm hover:bg-muted/30">
                  <div className="col-span-2 text-xs text-muted-foreground">{formatDate(log.timestamp)}</div>
                  <div className="col-span-2 font-medium">{log.user}</div>
                  <div className="col-span-2 text-muted-foreground">{log.action}</div>
                  <div className="col-span-3 text-muted-foreground">{log.resource}</div>
                  <div className="col-span-2 text-xs text-muted-foreground truncate">{log.details}</div>
                  <div className="col-span-1">
                    <Icon className={`h-4 w-4 ${severityColors[log.severity] ?? "text-muted-foreground"}`} />
                  </div>
                </div>
              );
            })}
          </div>
        </TabsContent>

        <TabsContent value="critical">
          <div className="rounded-md border">
            {auditLogs.filter(l => l.severity === "critical").map((log) => (
              <div key={log.id} className="flex items-start gap-4 border-b px-4 py-3 last:border-0 hover:bg-muted/30">
                <AlertTriangle className="mt-1 h-5 w-5 text-status-red shrink-0" />
                <div className="flex-1">
                  <p className="text-sm font-medium">{log.action}</p>
                  <p className="text-xs text-muted-foreground">{log.resource} · {log.details}</p>
                  <p className="text-xs text-muted-foreground mt-1">{log.user} · {formatDate(log.timestamp)}</p>
                </div>
                <Badge variant="destructive" className="text-[10px]">Critical</Badge>
              </div>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="warning">
          <div className="rounded-md border">
            {auditLogs.filter(l => l.severity === "warning").map((log) => (
              <div key={log.id} className="flex items-start gap-4 border-b px-4 py-3 last:border-0 hover:bg-muted/30">
                <AlertTriangle className="mt-1 h-5 w-5 text-status-amber shrink-0" />
                <div className="flex-1">
                  <p className="text-sm font-medium">{log.action}</p>
                  <p className="text-xs text-muted-foreground">{log.resource}</p>
                  <p className="text-xs text-muted-foreground">{log.user} · {formatDate(log.timestamp)}</p>
                </div>
                <Badge variant="secondary" className="text-[10px]">Warning</Badge>
              </div>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
