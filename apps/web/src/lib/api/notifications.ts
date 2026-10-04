import { api, type PagedResult } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

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
}

export const notificationKeys = { all: ["notifications"] as const };

export const notificationApi = {
  list: (params?: { page?: number; pageSize?: number; unreadOnly?: boolean }) =>
    api.get<PagedResult<NotificationListItem>>(endpoints.notifications.root, params),
  markRead: (notificationId: string) => api.post<void>(endpoints.notifications.markRead(notificationId)),
  markAllRead: () => api.put<void>(endpoints.notifications.markAllRead),
  registerDevice: (request: RegisterDeviceTokenRequest) => api.post<DeviceTokenDto>(endpoints.notifications.devices, request),
  unregisterDevice: (deviceId: string) => api.delete<void>(endpoints.notifications.device(deviceId)),
};
