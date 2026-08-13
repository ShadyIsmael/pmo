"use client";

import { useState } from "react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Bell, CheckCheck, AlertTriangle, FileText, CalendarCheck, UserPlus, Settings, Info } from "lucide-react";
import { formatDate, formatRelativeDate, getInitials } from "@/lib/utils";

const mockNotifications = [
  { id: "not-1", title: "Risk Escalated: Core Banking Modernisation", description: "A critical risk with score 20 has been escalated", type: "risk", read: false, createdAt: "2026-07-29" },
  { id: "not-2", title: "Approval Required: Budget Change", description: "ERP Cloud Migration budget revision requires your approval", type: "approval", read: false, createdAt: "2026-07-29" },
  { id: "not-3", title: "Meeting Reminder: Steering Committee", description: "Steering Committee - Core Banking Modernisation starts in 1 hour", type: "meeting", read: false, createdAt: "2026-07-28" },
  { id: "not-4", title: "Resource Allocation Changed", description: "Your resource allocation has been updated for Q3", type: "resource", read: false, createdAt: "2026-07-28" },
  { id: "not-5", title: "Milestone Completed: Architecture Sign-off", description: "Architecture Sign-off milestone has been marked complete", type: "milestone", read: true, createdAt: "2026-07-27" },
  { id: "not-6", title: "New Comment on Risk Register", description: "Daniel O'Brien added a comment to risk R-0042", type: "comment", read: true, createdAt: "2026-07-27" },
  { id: "not-7", title: "Project Health Changed", description: "HR & Payroll Transformation health changed from Amber to Red", type: "health", read: true, createdAt: "2026-07-26" },
  { id: "not-8", title: "New Project Assigned", description: "You have been assigned as Project Manager for ESG Reporting Framework", type: "assignment", read: true, createdAt: "2026-07-25" },
  { id: "not-9", title: "Report Ready: Monthly Dashboard", description: "Monthly Portfolio Dashboard report is ready for download", type: "report", read: true, createdAt: "2026-07-24" },
  { id: "not-10", title: "System: Maintenance Scheduled", description: "NEXUS PMO will be offline for maintenance on Aug 3rd 02:00-04:00", type: "system", read: true, createdAt: "2026-07-23" },
];

const typeIcons: Record<string, { icon: typeof Bell; color: string }> = {
  risk: { icon: AlertTriangle, color: "text-status-red" },
  approval: { icon: FileText, color: "text-status-amber" },
  meeting: { icon: CalendarCheck, color: "text-blue-500" },
  resource: { icon: UserPlus, color: "text-status-green" },
  milestone: { icon: CheckCheck, color: "text-status-green" },
  comment: { icon: Info, color: "text-muted-foreground" },
  health: { icon: AlertTriangle, color: "text-status-red" },
  assignment: { icon: UserPlus, color: "text-primary" },
  report: { icon: FileText, color: "text-muted-foreground" },
  system: { icon: Settings, color: "text-muted-foreground" },
};

export default function NotificationsPage() {
  const [allRead, setAllRead] = useState(false);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Notification Centre"
        description={`${mockNotifications.filter(n => !n.read).length} unread notifications`}
        actions={
          <Button variant="outline" size="sm" onClick={() => setAllRead(true)}>
            <CheckCheck className="mr-2 h-4 w-4" />Mark All Read
          </Button>
        }
      />

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All ({mockNotifications.length})</TabsTrigger>
          <TabsTrigger value="unread">Unread ({mockNotifications.filter(n => !n.read && !allRead).length})</TabsTrigger>
        </TabsList>

        <TabsContent value="all">
          <div className="rounded-md border">
            {mockNotifications.map((notification) => {
              const isUnread = !notification.read && !allRead;
              const iconConfig = typeIcons[notification.type] ?? typeIcons.system!;
              const Icon = iconConfig.icon;
              return (
                <div key={notification.id} className={`flex items-start gap-4 border-b px-4 py-4 last:border-0 hover:bg-muted/30 ${isUnread ? "bg-muted/20" : ""}`}>
                  <div className={`flex h-10 w-10 items-center justify-center rounded-lg ${isUnread ? "bg-primary/10" : "bg-muted"}`}>
                    <Icon className={`h-5 w-5 ${iconConfig.color}`} />
                  </div>
                  <div className="flex-1">
                    <div className="flex items-start justify-between">
                      <div>
                        <p className={`text-sm ${isUnread ? "font-medium" : ""}`}>{notification.title}</p>
                        <p className="text-xs text-muted-foreground mt-0.5">{notification.description}</p>
                      </div>
                      {isUnread && <span className="h-2 w-2 rounded-full bg-primary shrink-0 mt-1.5" />}
                    </div>
                    <p className="mt-1 text-[10px] text-muted-foreground">{formatRelativeDate(notification.createdAt)}</p>
                  </div>
                </div>
              );
            })}
          </div>
        </TabsContent>

        <TabsContent value="unread">
          <div className="rounded-md border">
            {mockNotifications.filter(n => !n.read && !allRead).length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-12 text-muted-foreground">
                <Bell className="h-12 w-12" />
                <p className="text-sm">No unread notifications</p>
              </div>
            ) : (
              mockNotifications.filter(n => !n.read && !allRead).map((notification) => {
                const iconConfig = typeIcons[notification.type] ?? typeIcons.system!;
                const Icon = iconConfig.icon;
                return (
                  <div key={notification.id} className="flex items-start gap-4 border-b px-4 py-4 last:border-0 bg-muted/20">
                    <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10">
                      <Icon className={`h-5 w-5 ${iconConfig.color}`} />
                    </div>
                    <div className="flex-1">
                      <p className="text-sm font-medium">{notification.title}</p>
                      <p className="text-xs text-muted-foreground">{notification.description}</p>
                      <p className="mt-1 text-[10px] text-muted-foreground">{formatRelativeDate(notification.createdAt)}</p>
                    </div>
                    <span className="h-2 w-2 rounded-full bg-primary shrink-0 mt-1.5" />
                  </div>
                );
              })
            )}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
