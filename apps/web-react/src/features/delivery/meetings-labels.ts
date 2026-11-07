export type MeetingTranslate = (key: string, params?: Record<string, string | number>) => string;

export function getMeetingStatusLabel(status: string | null | undefined, t: MeetingTranslate): string {
  switch (status?.toLowerCase()) {
    case "scheduled": return t("meetings.status.scheduled");
    case "inprogress":
    case "in progress": return t("meetings.status.inprogress");
    case "completed": return t("meetings.status.completed");
    case "cancelled":
    case "canceled": return t("meetings.status.cancelled");
    default: return t("meetings.status.unknown");
  }
}

export function getMeetingMinutesStateLabel(state: string | null | undefined, t: MeetingTranslate): string {
  const normalized = state?.trim().toLowerCase();
  if (normalized === "draft") return t("meetings.minutes.draft");
  if (normalized === "final") return t("meetings.minutes.final");
  return t("meetings.minutes.notAvailable");
}
