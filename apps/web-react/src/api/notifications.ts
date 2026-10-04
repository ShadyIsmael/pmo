import { useQuery } from "@tanstack/react-query";
import { api } from "./client";

interface NotificationItem { isRead: boolean; }
interface PagedNotifications { items: NotificationItem[]; totalCount: number; }

export function useUnreadNotificationCount(enabled: boolean) {
  return useQuery({
    queryKey: ["notifications", "unread-count"],
    queryFn: async () => {
      const result = await api.get<PagedNotifications>("/api/notifications", { page: 1, pageSize: 1, unreadOnly: true });
      return result.totalCount ?? result.items.filter((item) => !item.isRead).length;
    },
    enabled,
    staleTime: 30_000,
    gcTime: 300_000,
    refetchOnWindowFocus: false,
  });
}
