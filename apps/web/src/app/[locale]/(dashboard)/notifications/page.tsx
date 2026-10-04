"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  Bell,
  CalendarCheck,
  CheckCheck,
  FileText,
  Info,
  Settings,
  UserPlus,
} from "lucide-react";
import { PageHeader } from "@/components/layout/PageHeader";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useToast } from "@/hooks/use-toast";
import { notificationApi, notificationKeys, type NotificationListItem } from "@/lib/api/notifications";
import {
  enablePushNotifications,
  isFirebaseMessagingConfigured,
  type PushRegistrationStatus,
} from "@/lib/firebase-messaging";
import { formatRelativeDate } from "@/lib/utils";

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
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [pushStatus, setPushStatus] = useState<PushRegistrationStatus | null>(null);

  const notificationsQuery = useQuery({
    queryKey: notificationKeys.all,
    queryFn: () => notificationApi.list({ page: 1, pageSize: 50 }),
    refetchInterval: 10_000,
    refetchOnWindowFocus: true,
  });

  const markReadMutation = useMutation({
    mutationFn: (notificationId: string) => notificationApi.markRead(notificationId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
    onError: () => toast({ title: "Could not update notification", variant: "destructive" }),
  });

  const markAllReadMutation = useMutation({
    mutationFn: () => notificationApi.markAllRead(),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all });
      toast({ title: "All notifications marked as read" });
    },
    onError: () => toast({ title: "Could not update notifications", variant: "destructive" }),
  });

  const notifications = notificationsQuery.data?.items ?? [];
  const unreadNotifications = useMemo(
    () => notifications.filter((notification) => !notification.isRead),
    [notifications]
  );

  const handleEnablePush = async () => {
    const result = await enablePushNotifications();
    setPushStatus(result.status);

    const messages: Record<PushRegistrationStatus, string> = {
      enabled: "Browser notifications are enabled on this device.",
      denied: "Browser notification permission was denied. Enable it from the browser settings.",
      unsupported: "This browser does not support push notifications.",
      unconfigured: "Firebase web configuration is not present in the deployed web application.",
      unavailable: "Push registration could not be completed. Please try again.",
    };
    toast({
      title: result.status === "enabled" ? "Notifications enabled" : "Notifications not enabled",
      description: messages[result.status],
      variant: result.status === "enabled" ? "success" : "destructive",
    });
  };

  const renderNotification = (notification: NotificationListItem) => {
    const iconConfig = typeIcons[notification.type.toLowerCase()] ?? typeIcons.system!;
    const Icon = iconConfig.icon;
    const isUnread = !notification.isRead;

    return (
      <button
        key={notification.id}
        type="button"
        className={`nexus-table-row flex w-full items-start gap-4 px-4 py-4 text-left last:border-0 ${isUnread ? "bg-muted/20" : ""}`}
        onClick={() => {
          if (isUnread && !markReadMutation.isPending) {
            markReadMutation.mutate(notification.id);
          }
        }}
      >
        <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${isUnread ? "bg-primary/10" : "bg-muted"}`}>
          <Icon className={`h-5 w-5 ${iconConfig.color}`} />
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className={`text-sm ${isUnread ? "font-medium" : ""}`}>{notification.title}</p>
              {notification.body && <p className="mt-0.5 text-xs text-muted-foreground">{notification.body}</p>}
            </div>
            {isUnread && <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-primary" />}
          </div>
          <p className="mt-1 text-[10px] text-muted-foreground">{formatRelativeDate(notification.sentAt)}</p>
        </div>
      </button>
    );
  };

  const content = notificationsQuery.isLoading ? (
    <div className="nexus-table-surface px-4 py-12 text-center text-sm text-muted-foreground">Loading notifications…</div>
  ) : notificationsQuery.isError ? (
    <div className="nexus-table-surface px-4 py-12 text-center text-sm text-destructive">
      Notifications could not be loaded. Please refresh and try again.
    </div>
  ) : notifications.length === 0 ? (
    <div className="nexus-table-surface flex flex-col items-center gap-2 py-12 text-muted-foreground">
      <Bell className="h-12 w-12" />
      <p className="text-sm">No notifications yet</p>
    </div>
  ) : (
    <div className="nexus-table-surface">{notifications.map(renderNotification)}</div>
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="Notification Centre"
        description={`${unreadNotifications.length} unread notifications`}
        actions={
          <div className="flex flex-wrap gap-2">
            {pushStatus !== "enabled" && (
              <Button variant="outline" size="sm" onClick={() => void handleEnablePush()}>
                <Bell className="mr-2 h-4 w-4" />
                Enable Browser Notifications
              </Button>
            )}
            <Button
              variant="outline"
              size="sm"
              disabled={unreadNotifications.length === 0 || markAllReadMutation.isPending}
              onClick={() => markAllReadMutation.mutate()}
            >
              <CheckCheck className="mr-2 h-4 w-4" />Mark All Read
            </Button>
          </div>
        }
      />

      {!isFirebaseMessagingConfigured() && (
        <p className="text-xs text-muted-foreground">
          Browser push is available after the Firebase web settings are supplied to this deployment. In-app notifications below are loaded from the NEXUS database.
        </p>
      )}

      <Tabs defaultValue="all">
        <TabsList>
          <TabsTrigger value="all">All ({notifications.length})</TabsTrigger>
          <TabsTrigger value="unread">Unread ({unreadNotifications.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="all">{content}</TabsContent>
        <TabsContent value="unread">
          {unreadNotifications.length === 0 ? (
            <div className="nexus-table-surface flex flex-col items-center gap-2 py-12 text-muted-foreground">
              <Bell className="h-12 w-12" />
              <p className="text-sm">No unread notifications</p>
            </div>
          ) : (
            <div className="nexus-table-surface">{unreadNotifications.map(renderNotification)}</div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
