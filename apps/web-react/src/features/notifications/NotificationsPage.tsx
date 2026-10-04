import * as React from "react";
import { PageHeader } from "@components/layout/PageHeader";
import { Card, CardContent } from "@components/ui/Card";
import { Button } from "@components/ui/Button";
import { Pagination } from "@components/ui/Pagination";
import { useI18n } from "@i18n/messages";
import { useNotifications, useMarkNotificationRead, useMarkAllNotificationsRead } from "@api/notifications";

const PAGE_SIZE = 20;

export function NotificationsPage() {
  const { t, formatDate } = useI18n();
  const [page, setPage] = React.useState(1);
  const [unreadOnly, setUnreadOnly] = React.useState(false);
  // Full Notification Center data loads only when opened (visible-first).
  const query = useNotifications({ page, pageSize: PAGE_SIZE, unreadOnly }, true);
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllNotificationsRead();
  const items = query.data?.items ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("notifications.title")}
        description={t("notifications.description")}
        actions={<Button size="sm" variant="outline" disabled={markAll.isPending} onClick={() => markAll.mutate()}>{t("notifications.markAllRead")}</Button>}
      />
      <div className="flex items-center gap-2">
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={unreadOnly} onChange={(e) => { setUnreadOnly(e.target.checked); setPage(1); }} />
          {t("notifications.unreadOnly")}
        </label>
      </div>
      <Card>
        <CardContent className="p-0">
          {query.isLoading ? <p className="p-6 text-center text-sm text-muted-foreground">{t("common.loading")}</p>
            : query.isError ? <p className="p-6 text-center text-sm text-destructive">{t("common.errorLoading")}</p>
            : items.length === 0 ? <p className="p-6 text-center text-sm text-muted-foreground">{t("notifications.empty")}</p>
            : (
              <ul className="divide-y">
                {items.map((n) => (
                  <li key={n.id} className="flex items-start justify-between gap-3 px-4 py-3">
                    <div className="min-w-0">
                      <p className="text-sm font-semibold">{n.title}</p>
                      {n.body ? <p className="mt-0.5 truncate text-sm text-muted-foreground">{n.body}</p> : null}
                      <p className="mt-1 text-xs text-muted-foreground">{formatDate(n.sentAt)}</p>
                    </div>
                    {!n.isRead ? <Button size="sm" variant="ghost" disabled={markRead.isPending} onClick={() => markRead.mutate(n.id)}>{t("notifications.markRead")}</Button> : null}
                  </li>
                ))}
              </ul>
            )}
        </CardContent>
      </Card>
      <Pagination page={page} totalPages={query.data?.totalPages ?? 1} total={query.data?.totalCount} pageSize={PAGE_SIZE} onChange={setPage} />
    </div>
  );
}
