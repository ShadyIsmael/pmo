import type { NotificationListItem } from "@api/notifications";
import enMessages from "../i18n/messages/en.json";
import arMessages from "../i18n/messages/ar.json";

const templateDeletionNotificationKeys = {
  templatedeletionrequested: "requested",
  templatedeletionapproved: "approved",
  templatedeletionrejected: "rejected",
} as const;

const templateDeletionTranslations = {
  requested: {
    enTitle: enMessages.notifications.templateDeletion.requestedTitle,
    arTitle: arMessages.notifications.templateDeletion.requestedTitle,
    enBody: enMessages.notifications.templateDeletion.requestedBody,
    arBody: arMessages.notifications.templateDeletion.requestedBody,
  },
  approved: {
    enTitle: enMessages.notifications.templateDeletion.approvedTitle,
    arTitle: arMessages.notifications.templateDeletion.approvedTitle,
    enBody: enMessages.notifications.templateDeletion.approvedBody,
    arBody: arMessages.notifications.templateDeletion.approvedBody,
  },
  rejected: {
    enTitle: enMessages.notifications.templateDeletion.rejectedTitle,
    arTitle: arMessages.notifications.templateDeletion.rejectedTitle,
    enBody: enMessages.notifications.templateDeletion.rejectedBody,
    arBody: arMessages.notifications.templateDeletion.rejectedBody,
  },
} as const;

const bilingualNotificationTypes = new Set([
  "meetingreminder",
  "meetinginvitation",
  "meetingstarted",
  "meetingminutespublished",
  "meetingminutescomment",
  "meetingminutescommentreply",
  "projectannualbudgetapproval",
  "projectpaymentrequestapproval",
]);

/** Selects the locale half of supported bilingual notification payloads. */
export function getLocalizedNotificationContent(notification: Pick<NotificationListItem, "type" | "title" | "body">, locale: string) {
  const deletionKind = templateDeletionNotificationKeys[notification.type.toLowerCase() as keyof typeof templateDeletionNotificationKeys];
  if (deletionKind) {
    const translations = templateDeletionTranslations[deletionKind];
    const isArabic = locale === "ar";
    return {
      title: replaceNotificationValues(isArabic ? translations.arTitle : translations.enTitle, { templateName: notification.title }),
      body: replaceNotificationValues(isArabic ? translations.arBody : translations.enBody, { detail: notification.body ?? "" }),
    };
  }
  if (!bilingualNotificationTypes.has(notification.type.toLowerCase())) {
    return { title: notification.title, body: notification.body };
  }
  return {
    title: selectLocale(notification.title, locale, "\n"),
    body: notification.body ? selectLocale(notification.body, locale, "\n\n") : notification.body,
  };
}

function replaceNotificationValues(template: string, values: Record<string, string>): string {
  return template.replace(/\{(\w+)\}/g, (_match, key: string) => values[key] ?? "");
}

function selectLocale(content: string, locale: string, separator: string): string {
  const normalized = content.replace(/\r\n/g, "\n");
  const separatorIndex = normalized.indexOf(separator);
  if (separatorIndex < 0) return content;
  const english = normalized.slice(0, separatorIndex).trim();
  const arabic = normalized.slice(separatorIndex + separator.length).trim();
  return locale === "ar" ? (arabic || english) : (english || arabic);
}
