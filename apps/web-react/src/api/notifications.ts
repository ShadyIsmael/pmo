import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import { endpoints } from "./endpoints";

export interface NotificationListItem {
  id: string;
  title: string;
  body?: string;
  type: string;
  isRead: boolean;
  readAt?: string;
  sentAt: string;
  referenceType?: string;
  referenceId?: string;
}

export interface DeviceTokenDto {
  id: string;
  platform: string;
  deviceName?: string;
  isActive: boolean;
  lastSeenAt: string;
}

export interface RegisterDeviceTokenRequest {
  token: string;
  platform: string;
  deviceName?: string;
  previousDeviceId?: string;
}

export interface NotificationPreference {
  type: string;
  inAppEnabled: boolean;
  pushEnabled: boolean;
  emailEnabled: boolean;
}

export interface NotificationPreferencesResponse {
  items: NotificationPreference[];
  pushAvailable: boolean;
  emailAvailable: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const notificationKeys = { all: ["notifications"] as const };

export const notificationApi = {
  registerDevice: (request: RegisterDeviceTokenRequest) => api.post<DeviceTokenDto>(endpoints.notifications.devices, request),
  unregisterDevice: (deviceId: string) => api.delete<void>(endpoints.notifications.device(deviceId)),
  getPreferences: () => api.get<NotificationPreferencesResponse>(endpoints.notifications.preferences),
  updatePreferences: (items: NotificationPreference[]) => api.put<void>(endpoints.notifications.preferences, { items }),
};

/** Header: lightweight unread count only. Full center loads lazily when opened. */
export function useUnreadNotificationCount(enabled: boolean) {
  return useQuery({
    queryKey: [...notificationKeys.all, "unread-count"],
    queryFn: async () => {
      const result = await api.get<{ count: number }>(endpoints.notifications.unreadCount);
      return result.count;
    },
    enabled,
    staleTime: 30_000,
    gcTime: 300_000,
    refetchOnWindowFocus: true,
    refetchOnReconnect: true,
  });
}

export function useNotifications(params?: { page?: number; pageSize?: number; unreadOnly?: boolean }, enabled = true, realtime = false) {
  return useQuery({
    queryKey: [...notificationKeys.all, "list", params ?? {}],
    queryFn: () => api.get<PagedResult<NotificationListItem>>(endpoints.notifications.root, params),
    enabled,
    staleTime: 15_000,
    refetchInterval: realtime ? 30_000 : false,
    refetchOnWindowFocus: realtime,
  });
}

export const notificationPreferenceKeys = { all: ["notification-preferences"] as const };

export function useNotificationPreferences(enabled = true) {
  return useQuery({
    queryKey: notificationPreferenceKeys.all,
    queryFn: notificationApi.getPreferences,
    enabled,
    staleTime: 30_000,
    refetchOnWindowFocus: true,
  });
}

export function useUpdateNotificationPreferences() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: notificationApi.updatePreferences,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: notificationPreferenceKeys.all });
      void client.invalidateQueries({ queryKey: notificationKeys.all });
    },
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
