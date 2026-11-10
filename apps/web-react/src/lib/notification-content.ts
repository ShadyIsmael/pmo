import type { NotificationListItem } from "@api/notifications";

const bilingualNotificationTypes = new Set([
  "meetingreminder",
  "meetinginvitation",
  "meetingstarted",
  "meetingminutespublished",
  "meetingminutescomment",
  "meetingminutescommentreply",
  "projectannualbudgetapproval",
]);

/** Selects the locale half of supported bilingual notification payloads. */
export function getLocalizedNotificationContent(notification: Pick<NotificationListItem, "type" | "title" | "body">, locale: string) {
  if (!bilingualNotificationTypes.has(notification.type.toLowerCase())) {
    return { title: notification.title, body: notification.body };
  }
  return {
    title: selectLocale(notification.title, locale, "\n"),
    body: notification.body ? selectLocale(notification.body, locale, "\n\n") : notification.body,
  };
}

function selectLocale(content: string, locale: string, separator: string): string {
  const normalized = content.replace(/\r\n/g, "\n");
  const separatorIndex = normalized.indexOf(separator);
  if (separatorIndex < 0) return content;
  const english = normalized.slice(0, separatorIndex).trim();
  const arabic = normalized.slice(separatorIndex + separator.length).trim();
  return locale === "ar" ? (arabic || english) : (english || arabic);
}
