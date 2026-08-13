import type { ApiClient } from "./client";
import type { Notification, NotificationPreference, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createNotificationsApi(client: ApiClient) {
  return {
    getNotifications: (params?: PaginationParams & { type?: string; isRead?: boolean }) =>
      client.getPaged<Notification>("/notifications", params),

    markAsRead: (id: string) => client.put<void>(`/notifications/${id}/read`),

    markAllAsRead: () => client.put<void>("/notifications/read-all"),

    getPreferences: () => client.get<readonly NotificationPreference[]>("/notifications/preferences"),
  };
}
