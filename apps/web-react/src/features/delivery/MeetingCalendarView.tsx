import * as React from "react";
import { MeetingPublicLink } from "@lib/meeting-navigation";
import { addDays, format, isSameDay, startOfDay } from "date-fns";
import { CalendarDays, CheckCircle2, ChevronLeft, ChevronRight, Clock3, PlayCircle, Repeat, XCircle } from "lucide-react";
import { Button } from "@components/ui/Button";
import { Card, CardContent } from "@components/ui/Card";
import { Tabs, TabsList, TabsTrigger } from "@components/ui/Tabs";
import { useI18n } from "@i18n/messages";
import {
  formatCalendarDayValue,
  formatCalendarMonthValue,
  formatCalendarTimeValue,
  formatCalendarWeekdayValue,
  formatCalendarWeekRangeValue,
} from "@lib/formatting/date";
import { cn } from "@lib/utils";
import type { MeetingListDto } from "@api/meetingsFull";
import {
  CALENDAR_HOUR_HEIGHT,
  calendarRange,
  layoutDayMeetings,
  meetingEnd,
  meetingStart,
  meetingsOnDay,
  monthGridDays,
  projectColor,
  projectColorKey,
  weekDays,
  type CalendarMode,
} from "./meetings-calendar";
import { getMeetingStatusLabel } from "./meetings-labels";

interface MeetingCalendarViewProps {
  mode: CalendarMode;
  anchor: Date;
  meetings: readonly MeetingListDto[];
  isLoading: boolean;
  isError: boolean;
  errorText?: string;
  isTruncated?: boolean;
  totalCount?: number;
  filtersPanel?: React.ReactNode;
  onModeChange: (mode: CalendarMode) => void;
  onAnchorChange: (date: Date) => void;
  onDayClick: (date: Date) => void;
  onRetry: () => void;
}

const STATUS_ICONS = {
  scheduled: Clock3,
  inprogress: PlayCircle,
  completed: CheckCircle2,
  cancelled: XCircle,
} as const;

function statusKey(status: string | null | undefined): keyof typeof STATUS_ICONS {
  const normalized = (status ?? "").trim().toLowerCase().replace(/[\s_-]/g, "");
  if (normalized === "inprogress") return "inprogress";
  if (normalized === "completed") return "completed";
  if (normalized === "cancelled" || normalized === "canceled") return "cancelled";
  return "scheduled";
}

function StatusIcon({ status, className }: { status: string | null | undefined; className?: string }) {
  const Icon = STATUS_ICONS[statusKey(status)];
  return <Icon className={className} aria-hidden="true" />;
}

function statusIconClass(status: string | null | undefined): string {
  switch (statusKey(status)) {
    case "inprogress": return "text-primary";
    case "completed": return "text-success";
    case "cancelled": return "text-destructive";
    default: return "text-info";
  }
}

function MeetingMonthChip({ meeting }: { meeting: MeetingListDto }) {
  const { t, locale } = useI18n();
  const color = projectColor(projectColorKey(meeting));
  const statusLabel = getMeetingStatusLabel(meeting.status, t);
  const projectLabel = meeting.projectName || t("meetings.noProject");
  const timeLabel = formatCalendarTimeValue(meeting.startTime, locale);
  return (
    <MeetingPublicLink
      publicId={meeting.publicId}
      title={`${meeting.title} · ${projectLabel} · ${timeLabel} · ${statusLabel}`}
      className={cn(
        "block rounded border-s-[3px] px-1.5 py-1 text-[11px] leading-tight transition-colors hover:bg-muted/70 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
        color.border,
        color.soft,
        statusKey(meeting.status) === "cancelled" && "opacity-70",
      )}
    >
      <span className="flex min-w-0 items-center gap-1">
        <StatusIcon status={meeting.status} className={cn("h-3 w-3 shrink-0", statusIconClass(meeting.status))} />
        {meeting.isRecurring ? <span title={t("meetings.recurrence.indicator")}><Repeat className="h-3 w-3 shrink-0 text-primary" aria-label={t("meetings.recurrence.indicator")} /></span> : null}
        <span className={cn("truncate font-medium", statusKey(meeting.status) === "cancelled" && "line-through")}>{meeting.title}</span>
        <span className="sr-only">{statusLabel}</span>
      </span>
      <span className="mt-0.5 flex min-w-0 items-center gap-1 text-muted-foreground">
        <span className={cn("h-1.5 w-1.5 shrink-0 rounded-full", color.dot)} aria-hidden="true" />
        <span className="truncate">{projectLabel}</span>
        <span className="ms-auto shrink-0 tabular-nums">{timeLabel}</span>
      </span>
    </MeetingPublicLink>
  );
}

function MonthGrid({ anchor, meetings, onDayClick }: { anchor: Date; meetings: readonly MeetingListDto[]; onDayClick: (date: Date) => void }) {
  const { t, locale } = useI18n();
  const days = React.useMemo(() => monthGridDays(anchor), [anchor]);
  const weeks = React.useMemo(() => Array.from({ length: 6 }, (_, index) => days.slice(index * 7, index * 7 + 7)), [days]);
  const today = React.useMemo(() => new Date(), []);

  return (
    <div className="overflow-x-auto">
      <div className="min-w-[760px]" role="grid" aria-label={formatCalendarMonthValue(anchor, locale)}>
        <div className="grid grid-cols-7 border-b" role="row">
          {weeks[0].map((day) => (
            <div key={day.toISOString()} role="columnheader" className="px-2 py-2 text-start text-xs font-medium text-muted-foreground">
              {formatCalendarWeekdayValue(day, locale, "short")}
            </div>
          ))}
        </div>
        {weeks.map((week, weekIndex) => (
          <div key={weekIndex} className="grid grid-cols-7" role="row">
            {week.map((day) => {
              const dayMeetings = meetingsOnDay(meetings, day);
              const outside = day.getMonth() !== anchor.getMonth();
              const label = `${formatCalendarDayValue(day, locale)} · ${t("meetings.calendar.eventCount", { count: dayMeetings.length })}`;
              const visible = dayMeetings.slice(0, 2);
              return (
                <div
                  key={day.toISOString()}
                  role="gridcell"
                  onClick={(event) => {
                    if ((event.target as HTMLElement).closest("a,button")) return;
                    onDayClick(day);
                  }}
                  className={cn(
                    "min-h-[7.5rem] border-b border-e p-1.5 transition-colors last:border-e-0",
                    outside ? "bg-muted/20" : "bg-background",
                    isSameDay(day, today) && "bg-primary/5",
                  )}
                >
                  <button
                    type="button"
                    onClick={() => onDayClick(day)}
                    aria-label={label}
                    className={cn(
                      "mb-1 flex h-6 w-6 items-center justify-center rounded-full text-xs font-medium hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                      outside ? "text-muted-foreground/70" : "text-foreground",
                      isSameDay(day, today) && "bg-primary text-primary-foreground hover:bg-primary",
                    )}
                  >
                    {format(day, "d")}
                  </button>
                  <div className="space-y-1">
                    {visible.map((meeting) => <MeetingMonthChip key={`${meeting.id}-${day.toISOString()}`} meeting={meeting} />)}
                    {dayMeetings.length > visible.length ? (
                      <button
                        type="button"
                        onClick={() => onDayClick(day)}
                        className="w-full rounded px-1 py-0.5 text-start text-[11px] font-medium text-primary hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                      >
                        {t("meetings.calendar.more", { count: dayMeetings.length - visible.length })}
                      </button>
                    ) : null}
                  </div>
                </div>
              );
            })}
          </div>
        ))}
      </div>
    </div>
  );
}

function TimeGridEvent({ meeting, day, column, columnCount, startMinutes, endMinutes }: { meeting: MeetingListDto; day: Date; column: number; columnCount: number; startMinutes: number; endMinutes: number }) {
  const { t, locale } = useI18n();
  const color = projectColor(projectColorKey(meeting));
  const statusLabel = getMeetingStatusLabel(meeting.status, t);
  const projectLabel = meeting.projectName || t("meetings.noProject");
  const timeLabel = `${formatCalendarTimeValue(meeting.startTime, locale)} – ${formatCalendarTimeValue(meeting.endTime, locale)}`;
  const dayStart = startOfDay(day).getTime();
  const startsBefore = meetingStart(meeting).getTime() < dayStart;
  const endsAfter = meetingEnd(meeting).getTime() > dayStart + 24 * 60 * 60_000;
  const top = (startMinutes / 60) * CALENDAR_HOUR_HEIGHT;
  const height = Math.max(24, ((endMinutes - startMinutes) / 60) * CALENDAR_HOUR_HEIGHT - 2);

  return (
    <MeetingPublicLink
      publicId={meeting.publicId}
      style={{ insetInlineStart: `${(column / columnCount) * 100}%`, width: `${100 / columnCount}%`, top, height }}
      title={`${meeting.title} · ${projectLabel} · ${timeLabel} · ${statusLabel}`}
      className="absolute p-0.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <div className={cn(
        "flex h-full min-w-0 flex-col overflow-hidden rounded border-s-[3px] px-1.5 py-1 text-[11px] leading-tight",
        color.border,
        color.soft,
        statusKey(meeting.status) === "cancelled" && "opacity-70",
      )}>
        <span className="flex min-w-0 items-center gap-1">
          <StatusIcon status={meeting.status} className={cn("h-3 w-3 shrink-0", statusIconClass(meeting.status))} />
          {meeting.isRecurring ? <span title={t("meetings.recurrence.indicator")}><Repeat className="h-3 w-3 shrink-0 text-primary" aria-label={t("meetings.recurrence.indicator")} /></span> : null}
          <span className={cn("truncate font-semibold", statusKey(meeting.status) === "cancelled" && "line-through")}>{meeting.title}</span>
        </span>
        <span className="mt-0.5 truncate text-muted-foreground">{timeLabel}{startsBefore ? ` · ${t("meetings.calendar.startsBefore")}` : ""}{endsAfter ? ` · ${t("meetings.calendar.continues")}` : ""}</span>
        <span className="mt-0.5 flex min-w-0 items-center gap-1">
          <span className={cn("h-1.5 w-1.5 shrink-0 rounded-full", color.dot)} aria-hidden="true" />
          <span className={cn("truncate", color.text)}>{projectLabel}</span>
        </span>
        <span className="mt-0.5 flex items-center gap-1 text-muted-foreground">
          <StatusIcon status={meeting.status} className={cn("h-3 w-3 shrink-0", statusIconClass(meeting.status))} />
          <span className="truncate">{statusLabel}</span>
        </span>
      </div>
    </MeetingPublicLink>
  );
}

function TimeGrid({ days, meetings, onDayClick }: { days: Date[]; meetings: readonly MeetingListDto[]; onDayClick: (date: Date) => void }) {
  const { locale } = useI18n();
  const today = React.useMemo(() => new Date(), []);
  const totalHeight = 24 * CALENDAR_HOUR_HEIGHT;
  const hourLabels = React.useMemo(() => Array.from({ length: 24 }, (_, hour) => formatCalendarTimeValue(new Date(2000, 0, 1, hour), locale)), [locale]);

  return (
    <div className="overflow-auto" style={{ maxHeight: 620 }}>
      <div className={cn("min-w-[720px]", days.length === 1 && "min-w-0")}>
        <div className="sticky top-0 z-20 flex border-b bg-background">
          <div className="w-14 shrink-0 border-e" aria-hidden="true" />
          <div className="grid flex-1" style={{ gridTemplateColumns: `repeat(${days.length}, minmax(0, 1fr))` }}>
            {days.map((day) => (
              <button
                key={day.toISOString()}
                type="button"
                onClick={() => onDayClick(day)}
                className={cn(
                  "border-e px-2 py-2 text-center text-xs last:border-e-0 hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                  isSameDay(day, today) && "bg-primary/5",
                )}
              >
                <span className="block font-medium text-muted-foreground">{formatCalendarWeekdayValue(day, locale, "short")}</span>
                <span className={cn("mt-0.5 inline-flex h-6 min-w-6 items-center justify-center rounded-full px-1 text-sm font-semibold", isSameDay(day, today) && "bg-primary text-primary-foreground")}>{format(day, "d")}</span>
              </button>
            ))}
          </div>
        </div>
        <div className="flex">
          <div className="w-14 shrink-0 border-e" style={{ height: totalHeight }}>
            {hourLabels.map((label, hour) => (
              <div key={hour} className="relative -top-1 pe-2 text-end text-[10px] text-muted-foreground" style={{ height: CALENDAR_HOUR_HEIGHT }}>
                {hour === 0 ? "" : label}
              </div>
            ))}
          </div>
          <div className="grid flex-1" style={{ gridTemplateColumns: `repeat(${days.length}, minmax(0, 1fr))` }}>
            {days.map((day) => {
              const dayMeetings = meetingsOnDay(meetings, day);
              const positioned = layoutDayMeetings(dayMeetings, day);
              const now = new Date();
              const nowMinutes = now.getHours() * 60 + now.getMinutes();
              return (
                <div key={day.toISOString()} className="relative border-e last:border-e-0" style={{ height: totalHeight }}>
                  {Array.from({ length: 24 }, (_, hour) => (
                    <div key={hour} className="absolute inset-x-0 border-t border-border/60" style={{ top: hour * CALENDAR_HOUR_HEIGHT }} aria-hidden="true" />
                  ))}
                  {isSameDay(day, now) ? (
                    <div className="absolute inset-x-0 z-10 border-t-2 border-destructive" style={{ top: (nowMinutes / 60) * CALENDAR_HOUR_HEIGHT }} aria-hidden="true">
                      <span className="absolute -top-1 h-2 w-2 rounded-full bg-destructive" style={{ insetInlineStart: -4 }} />
                    </div>
                  ) : null}
                  {positioned.map(({ meeting, column, columnCount, startMinutes, endMinutes }) => (
                    <TimeGridEvent key={meeting.id} meeting={meeting} day={day} column={column} columnCount={columnCount} startMinutes={startMinutes} endMinutes={endMinutes} />
                  ))}
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}

function CalendarLegend({ meetings }: { meetings: readonly MeetingListDto[] }) {
  const { t } = useI18n();
  const projects = React.useMemo(() => {
    const seen = new Map<string, string>();
    for (const meeting of meetings) {
      const key = projectColorKey(meeting);
      if (!seen.has(key)) seen.set(key, meeting.projectName || t("meetings.noProject"));
    }
    return [...seen.entries()].slice(0, 10);
  }, [meetings, t]);

  return (
    <div className="flex flex-col gap-3 border-t pt-3 text-xs sm:flex-row sm:flex-wrap sm:items-start sm:gap-6">
      <div className="min-w-0">
        <p className="mb-1.5 font-medium text-muted-foreground">{t("meetings.calendar.projectLegend")}</p>
        <div className="flex flex-wrap gap-x-3 gap-y-1">
          {projects.length === 0 ? <span className="text-muted-foreground">{t("meetings.calendar.noProjects")}</span> : projects.map(([key, name]) => {
            const color = projectColor(key);
            return <span key={key} className="inline-flex min-w-0 items-center gap-1.5"><span className={cn("h-2.5 w-2.5 shrink-0 rounded-full", color.dot)} aria-hidden="true" /><span className="truncate">{name}</span></span>;
          })}
        </div>
      </div>
      <div>
        <p className="mb-1.5 font-medium text-muted-foreground">{t("meetings.calendar.statusLegend")}</p>
        <div className="flex flex-wrap gap-x-3 gap-y-1">
          {(["Scheduled", "InProgress", "Completed", "Cancelled"] as const).map((status) => (
            <span key={status} className="inline-flex items-center gap-1.5">
              <StatusIcon status={status} className={cn("h-3 w-3", statusIconClass(status))} />
              <span>{getMeetingStatusLabel(status, t)}</span>
            </span>
          ))}
        </div>
      </div>
    </div>
  );
}

export function MeetingCalendarView({ mode, anchor, meetings, isLoading, isError, errorText, isTruncated, totalCount, filtersPanel, onModeChange, onAnchorChange, onDayClick, onRetry }: MeetingCalendarViewProps) {
  const { t, locale } = useI18n();
  const range = React.useMemo(() => calendarRange(mode, anchor), [anchor, mode]);
  const days = React.useMemo(() => (mode === "week" ? weekDays(anchor) : mode === "day" ? [startOfDay(anchor)] : []), [anchor, mode]);
  const heading = mode === "month"
    ? formatCalendarMonthValue(anchor, locale)
    : mode === "week"
      ? formatCalendarWeekRangeValue(range.from, range.to, locale)
      : formatCalendarDayValue(anchor, locale);

  return (
    <div className={cn("grid min-w-0 gap-3", filtersPanel && "lg:grid-cols-[17rem_minmax(0,1fr)]")}>
      {filtersPanel}
      <Card className="min-w-0">
        <CardContent className="space-y-3 p-3 sm:p-4">
        {isLoading ? (
          <div className="space-y-3" role="status" aria-label={t("common.loading")}>{[0, 1, 2, 3, 4, 5].map((row) => <div key={row} className="h-14 animate-pulse rounded bg-muted" />)}</div>
        ) : isError ? (
          <div className="flex flex-col items-center gap-3 p-8 text-center"><CalendarDays className="h-8 w-8 text-muted-foreground/60" aria-hidden="true" /><p className="text-sm text-destructive">{errorText ?? t("meetings.calendar.loadError")}</p><Button type="button" variant="outline" size="sm" onClick={onRetry}>{t("common.retry")}</Button></div>
        ) : <>
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex min-w-0 items-center gap-1">
            <Button type="button" size="icon" variant="ghost" aria-label={t("meetings.calendar.previous")} onClick={() => onAnchorChange(addDays(range.from, -1))}>
              <ChevronLeft className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />
            </Button>
            <Button type="button" size="icon" variant="ghost" aria-label={t("meetings.calendar.next")} onClick={() => onAnchorChange(addDays(range.to, 1))}>
              <ChevronRight className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={() => onAnchorChange(new Date())}>{t("meetings.calendar.today")}</Button>
            <h3 className="ms-1 truncate text-sm font-semibold sm:text-base" aria-live="polite">{heading}</h3>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {typeof totalCount === "number" ? <span className="text-xs text-muted-foreground">{t("meetings.calendar.eventCount", { count: totalCount })}</span> : null}
            <Tabs value={mode} onValueChange={(value) => { if (value === "month" || value === "week" || value === "day") onModeChange(value); }}>
              <TabsList aria-label={t("meetings.calendar.mode")}>
                <TabsTrigger value="month">{t("meetings.calendar.month")}</TabsTrigger>
                <TabsTrigger value="week">{t("meetings.calendar.week")}</TabsTrigger>
                <TabsTrigger value="day">{t("meetings.calendar.day")}</TabsTrigger>
              </TabsList>
            </Tabs>
          </div>
        </div>

        {isTruncated ? (
          <p role="status" className="rounded-md border border-warning/40 bg-warning/10 px-3 py-2 text-xs text-warning">{t("meetings.calendar.truncated")}</p>
        ) : null}

        {meetings.length === 0 ? (
          <div className="rounded-md border border-dashed p-6 text-center">
            <CalendarDays className="mx-auto mb-2 h-7 w-7 text-muted-foreground/60" aria-hidden="true" />
            <p className="text-sm font-medium">{t("meetings.calendar.emptyTitle")}</p>
            <p className="mt-1 text-xs text-muted-foreground">{t("meetings.calendar.emptyDescription")}</p>
          </div>
        ) : null}

        {mode === "month" ? <MonthGrid anchor={anchor} meetings={meetings} onDayClick={onDayClick} /> : <TimeGrid days={days} meetings={meetings} onDayClick={onDayClick} />}

        <CalendarLegend meetings={meetings} />
        </>}
      </CardContent>
      </Card>
    </div>
  );
}
