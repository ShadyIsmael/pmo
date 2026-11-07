import { MeetingPublicLink } from "@lib/meeting-navigation";
import { useI18n } from "@i18n/messages";
import type { MeetingListDto } from "@api/meetingsFull";
import { formatDashboardMeetingDateTime } from "./dashboard-widget-utils";

export function UpcomingMeetingsWidget({ meetings, maxRecords }: { meetings: readonly MeetingListDto[]; maxRecords: number }) {
  const { t, locale } = useI18n();

  if (meetings.length === 0) {
    return <div className="flex min-h-32 items-center justify-center text-sm text-muted-foreground">{t("dashboard.noMeetings")}</div>;
  }

  return <div className="space-y-1.5">{meetings.slice(0, maxRecords).map((meeting) => <MeetingPublicLink key={meeting.id} publicId={meeting.publicId} className="flex items-center gap-2 rounded px-2 py-1.5 text-xs hover:bg-muted/50">
    <span className="h-6 w-6 shrink-0 rounded bg-primary/10 text-center leading-6 text-primary">◆</span>
    <span className="min-w-0 flex-1"><span className="block truncate font-medium">{meeting.title}</span><span className="block text-[10px] text-muted-foreground">{formatDashboardMeetingDateTime(meeting.startTime, locale)}</span></span>
    <span className="shrink-0 text-[9px] text-muted-foreground">{meeting.projectName || t("dashboard.general")}</span>
  </MeetingPublicLink>)}</div>;
}
