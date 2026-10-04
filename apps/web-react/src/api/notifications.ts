import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import { endpoints } from "./endpoints";

export interface NotificationListItem {
  id: string;
  title: string;
  body?: string;
  type: string;
  channel: string;
  isRead: boolean;
  readAt?: string;
  sentAt: string;
  referenceType?: string;
  referenceId?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const notificationKeys = { all: ["notifications"] as const };

/** Header: lightweight unread count only. Full center loads lazily when opened. */
export function useUnreadNotificationCount(enabled: boolean) {
  return useQuery({
    queryKey: [...notificationKeys.all, "unread-count"],
    queryFn: async () => {
      const result = await api.get<PagedResult<NotificationListItem>>(endpoints.notifications.root, { page: 1, pageSize: 1, unreadOnly: true });
      return result.totalCount ?? result.items.filter((i) => !i.isRead).length;
    },
    enabled,
    staleTime: 30_000,
    gcTime: 300_000,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    refetchInterval: false,
  });
}

export function useNotifications(params?: { page?: number; pageSize?: number; unreadOnly?: boolean }, enabled = true) {
  return useQuery({
    queryKey: [...notificationKeys.all, "list", params ?? {}],
    queryFn: () => api.get<PagedResult<NotificationListItem>>(endpoints.notifications.root, params),
    enabled,
    staleTime: 15_000,
  });
}

export function useMarkNotificationRead() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<void>(endpoints.notifications.markRead(id)),
    onSuccess: () => { void client.invalidateQueries({ queryKey: notificationKeys.all }); },
  });
}

export function useMarkAllNotificationsRead() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => api.put<void>(endpoints.notifications.markAllRead),
    onSuccess: () => { void client.invalidateQueries({ queryKey: notificationKeys.all }); },
  });
}
