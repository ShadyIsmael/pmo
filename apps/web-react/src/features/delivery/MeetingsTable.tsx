import * as React from "react";
import { Link } from "react-router-dom";
import { CalendarCheck, Eye, Repeat, Users } from "lucide-react";
import { Badge } from "@components/ui/Badge";
import { Button } from "@components/ui/Button";
import { Card, CardContent } from "@components/ui/Card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@components/ui/Table";
import { useI18n } from "@i18n/messages";
import type { MeetingListDto } from "@api/meetingsFull";
import { getMeetingMinutesStateLabel, getMeetingStatusLabel } from "./meetings-labels";
import { MeetingPublicLink, meetingPublicPath } from "@lib/meeting-navigation";

export const MEETINGS_PAGE_SIZE = 20;
export const MEETING_STATUSES = ["Scheduled", "InProgress", "Completed", "Cancelled"] as const;
export type MeetingStatus = (typeof MEETING_STATUSES)[number];

export function isMeetingStatus(value: string): value is MeetingStatus | "all" {
  return value === "all" || (MEETING_STATUSES as readonly string[]).includes(value);
}

export function MeetingStatusBadge({ status, label }: { status?: string | null; label: string }) {
  return status?.trim()
    ? <Badge status={status}>{label}</Badge>
    : <span className="text-xs font-medium text-muted-foreground">{label}</span>;
}

interface MeetingsTableProps {
  meetings: readonly MeetingListDto[];
  showProject: boolean;
  canManageMinutes: boolean;
  formatDate: (date: Date | string, options?: Intl.DateTimeFormatOptions) => string;
}

export function MeetingsTable({ meetings, showProject, canManageMinutes, formatDate }: MeetingsTableProps) {
  const { t } = useI18n();
  return <Card><CardContent className="p-0"><Table><TableHeader><TableRow><TableHead>{t("meetings.columns.meeting")}</TableHead>{showProject ? <TableHead>{t("common.project")}</TableHead> : null}<TableHead>{t("meetings.organizer")}</TableHead><TableHead>{t("meetings.dateTime")}</TableHead><TableHead>{t("meetings.attendees")}</TableHead><TableHead>{t("common.status")}</TableHead><TableHead>{t("meetings.minutesTitle")}</TableHead><TableHead className="text-end">{t("common.actions")}</TableHead></TableRow></TableHeader><TableBody>{meetings.map((meeting) => {
    const hasMinutes = Boolean(meeting.minutesState);
    const minutesActionLabel = hasMinutes || !canManageMinutes ? t("meetings.viewMinutes") : t("meetings.enterMinutes");
    const meetingPath = meetingPublicPath(meeting.publicId);
    const minutesPath = meetingPath ? `${meetingPath}/minutes` : null;
    return <TableRow key={meeting.id} className="group"><TableCell className="min-w-[16rem]"><MeetingPublicLink publicId={meeting.publicId} className="block min-w-0"><p className="flex items-center gap-1.5 truncate font-semibold underline-offset-4 group-hover:text-primary group-hover:underline">{meeting.isRecurring ? <span title={t("meetings.recurrence.indicator")}><Repeat className="h-3.5 w-3.5 shrink-0 text-primary" aria-label={t("meetings.recurrence.indicator")} /></span> : null}<span className="truncate">{meeting.title}</span></p><p className="mt-1 truncate text-[11px] text-muted-foreground">{hasMinutes ? getMeetingMinutesStateLabel(meeting.minutesState, t) : t("meetings.minutes.notAvailable")}</p></MeetingPublicLink></TableCell>{showProject ? <TableCell className="max-w-[14rem]"><span className="block truncate text-muted-foreground">{meeting.projectName ?? t("meetings.notAvailable")}</span></TableCell> : null}<TableCell className="max-w-[12rem]"><span className="block truncate text-muted-foreground">{meeting.organizerName ?? t("meetings.notAvailable")}</span></TableCell><TableCell className="whitespace-nowrap"><span className="flex items-center gap-1.5 text-muted-foreground"><CalendarCheck className="h-3.5 w-3.5 shrink-0" />{formatDate(meeting.startTime, { timeStyle: "short" })}</span></TableCell><TableCell><span className="inline-flex items-center gap-1.5 text-muted-foreground"><Users className="h-4 w-4" />{meeting.attendeeCount}</span></TableCell><TableCell><MeetingStatusBadge status={meeting.status} label={getMeetingStatusLabel(meeting.status, t)} /></TableCell><TableCell><MeetingStatusBadge status={meeting.minutesState} label={hasMinutes ? getMeetingMinutesStateLabel(meeting.minutesState, t) : t("meetings.minutes.notAvailable")} /></TableCell><TableCell className="text-end"><div className="nexus-table-actions flex justify-end gap-1">{meetingPath ? <Button asChild size="icon" variant="ghost" aria-label={t("meetings.viewMeeting")} title={t("meetings.viewMeeting")}><Link to={meetingPath}><Eye className="h-4 w-4" /></Link></Button> : <Button size="icon" variant="ghost" aria-label={t("meetings.viewMeeting")} title={t("meetings.viewMeeting")} disabled><Eye className="h-4 w-4" /></Button>}{minutesPath ? <Button asChild size="sm" variant={canManageMinutes && !hasMinutes ? "default" : "outline"} className="gap-1.5"><Link to={minutesPath}>{minutesActionLabel}</Link></Button> : <Button size="sm" variant={canManageMinutes && !hasMinutes ? "default" : "outline"} className="gap-1.5" disabled>{minutesActionLabel}</Button>}</div></TableCell></TableRow>;
  })}</TableBody></Table></CardContent></Card>;
}
