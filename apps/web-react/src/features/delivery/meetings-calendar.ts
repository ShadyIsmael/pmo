import {
  addDays,
  addMonths,
  addWeeks,
  endOfDay,
  endOfWeek,
  format,
  startOfDay,
  startOfMonth,
  startOfWeek,
} from "date-fns";
import type { MeetingListDto } from "@api/meetingsFull";

export type MeetingsView = "calendar" | "list";
export type CalendarMode = "month" | "week" | "day";

export const WEEK_STARTS_ON = 1 as const;
export const MONTH_GRID_DAYS = 42;
export const CALENDAR_HOUR_HEIGHT = 48;

export interface CalendarRange {
  from: Date;
  to: Date;
}

export function calendarRange(mode: CalendarMode, anchor: Date): CalendarRange {
  if (mode === "day") return { from: startOfDay(anchor), to: endOfDay(anchor) };
  if (mode === "week") {
    return {
      from: startOfWeek(anchor, { weekStartsOn: WEEK_STARTS_ON }),
      to: endOfWeek(anchor, { weekStartsOn: WEEK_STARTS_ON }),
    };
  }
  const gridStart = startOfWeek(startOfMonth(anchor), { weekStartsOn: WEEK_STARTS_ON });
  return { from: gridStart, to: endOfDay(addDays(gridStart, MONTH_GRID_DAYS - 1)) };
}

export function monthGridDays(anchor: Date): Date[] {
  const { from } = calendarRange("month", anchor);
  return Array.from({ length: MONTH_GRID_DAYS }, (_, index) => addDays(from, index));
}

export function weekDays(anchor: Date): Date[] {
  const { from } = calendarRange("week", anchor);
  return Array.from({ length: 7 }, (_, index) => addDays(from, index));
}

export function shiftCalendarAnchor(mode: CalendarMode, anchor: Date, direction: -1 | 1): Date {
  if (mode === "day") return addDays(anchor, direction);
  if (mode === "week") return addWeeks(anchor, direction);
  return addMonths(anchor, direction);
}

export function parseCalendarDate(value: string | null | undefined): Date | null {
  if (!value) return null;
  const parsed = new Date(`${value}T00:00:00`);
  return Number.isFinite(parsed.getTime()) ? parsed : null;
}

export function toCalendarDateValue(date: Date): string {
  return format(date, "yyyy-MM-dd");
}

export function meetingStart(meeting: Pick<MeetingListDto, "startTime">): Date {
  return new Date(meeting.startTime);
}

export function meetingEnd(meeting: Pick<MeetingListDto, "startTime" | "endTime">): Date {
  const start = meetingStart(meeting);
  const end = new Date(meeting.endTime);
  if (!Number.isFinite(end.getTime()) || end <= start) return new Date(start.getTime() + 30 * 60_000);
  return end;
}

export function sortMeetings<T extends Pick<MeetingListDto, "startTime" | "endTime" | "title">>(meetings: readonly T[]): T[] {
  return [...meetings].sort((left, right) => {
    const startDelta = meetingStart(left).getTime() - meetingStart(right).getTime();
    if (startDelta !== 0) return startDelta;
    return left.title.localeCompare(right.title);
  });
}

export function meetingsOnDay<T extends Pick<MeetingListDto, "startTime" | "endTime" | "title">>(meetings: readonly T[], day: Date): T[] {
  const dayStart = startOfDay(day);
  const dayEnd = endOfDay(day);
  return sortMeetings(meetings.filter((meeting) => {
    const start = meetingStart(meeting);
    const end = meetingEnd(meeting);
    if (!Number.isFinite(start.getTime())) return false;
    return start < dayEnd && end > dayStart;
  }));
}

export function groupMeetingsByDay<T extends Pick<MeetingListDto, "startTime" | "endTime" | "title">>(meetings: readonly T[]): Map<string, T[]> {
  const grouped = new Map<string, T[]>();
  for (const meeting of sortMeetings(meetings)) {
    const start = meetingStart(meeting);
    const end = meetingEnd(meeting);
    if (!Number.isFinite(start.getTime())) continue;
    let cursor = startOfDay(start);
    const lastDay = startOfDay(end.getTime() > start.getTime() ? new Date(end.getTime() - 1) : end);
    while (cursor <= lastDay) {
      const key = toCalendarDateValue(cursor);
      const bucket = grouped.get(key);
      if (bucket) bucket.push(meeting);
      else grouped.set(key, [meeting]);
      cursor = addDays(cursor, 1);
    }
  }
  return grouped;
}

export interface PositionedMeeting<T extends Pick<MeetingListDto, "startTime" | "endTime" | "title">> {
  meeting: T;
  column: number;
  columnCount: number;
  startMinutes: number;
  endMinutes: number;
}

interface TimedMeeting<T> {
  meeting: T;
  start: number;
  end: number;
}

/**
 * Assigns overlapping meetings to side-by-side columns. Each contiguous overlap
 * cluster shares a column count so events never cover one another.
 */
export function layoutDayMeetings<T extends Pick<MeetingListDto, "startTime" | "endTime" | "title">>(meetings: readonly T[], day: Date): PositionedMeeting<T>[] {
  const dayStart = startOfDay(day).getTime();
  const dayEnd = endOfDay(day).getTime();
  const timed: TimedMeeting<T>[] = sortMeetings(meetings).map((meeting) => {
    const start = Math.max(meetingStart(meeting).getTime(), dayStart);
    const end = Math.min(meetingEnd(meeting).getTime(), dayEnd);
    return {
      meeting,
      start: Math.max(0, start - dayStart),
      end: Math.max(0, end - dayStart),
    };
  });

  const positioned: PositionedMeeting<T>[] = [];
  let cluster: TimedMeeting<T>[] = [];
  let clusterEnd = -1;

  const flush = () => {
    if (cluster.length === 0) return;
    const columns: TimedMeeting<T>[][] = [];
    for (const item of cluster) {
      let column = columns.findIndex((entries) => {
        const last = entries[entries.length - 1];
        return last ? last.end <= item.start : true;
      });
      if (column === -1) {
        columns.push([item]);
        column = columns.length - 1;
      } else {
        columns[column].push(item);
      }
    }
    columns.forEach((entries, column) => {
      for (const item of entries) {
        positioned.push({
          meeting: item.meeting,
          column,
          columnCount: columns.length,
          startMinutes: item.start / 60_000,
          endMinutes: Math.max(item.end / 60_000, item.start / 60_000 + 15),
        });
      }
    });
    cluster = [];
  };

  for (const item of timed) {
    if (cluster.length === 0 || item.start < clusterEnd) {
      cluster.push(item);
      clusterEnd = Math.max(clusterEnd, item.end);
    } else {
      flush();
      cluster.push(item);
      clusterEnd = item.end;
    }
  }
  flush();

  return positioned.sort((left, right) => left.startMinutes - right.startMinutes);
}

export interface ProjectColor {
  dot: string;
  border: string;
  soft: string;
  text: string;
  swatch: string;
}

/**
 * A fixed palette keeps project colours deterministic across calendar, legend,
 * and chips without storing presentation data on the server.
 */
export const PROJECT_COLORS: readonly ProjectColor[] = [
  { dot: "bg-sky-500", border: "border-sky-500", soft: "bg-sky-500/10", text: "text-sky-700 dark:text-sky-300", swatch: "bg-sky-500" },
  { dot: "bg-violet-500", border: "border-violet-500", soft: "bg-violet-500/10", text: "text-violet-700 dark:text-violet-300", swatch: "bg-violet-500" },
  { dot: "bg-emerald-500", border: "border-emerald-500", soft: "bg-emerald-500/10", text: "text-emerald-700 dark:text-emerald-300", swatch: "bg-emerald-500" },
  { dot: "bg-amber-500", border: "border-amber-500", soft: "bg-amber-500/10", text: "text-amber-700 dark:text-amber-300", swatch: "bg-amber-500" },
  { dot: "bg-rose-500", border: "border-rose-500", soft: "bg-rose-500/10", text: "text-rose-700 dark:text-rose-300", swatch: "bg-rose-500" },
  { dot: "bg-cyan-500", border: "border-cyan-500", soft: "bg-cyan-500/10", text: "text-cyan-700 dark:text-cyan-300", swatch: "bg-cyan-500" },
  { dot: "bg-indigo-500", border: "border-indigo-500", soft: "bg-indigo-500/10", text: "text-indigo-700 dark:text-indigo-300", swatch: "bg-indigo-500" },
  { dot: "bg-orange-500", border: "border-orange-500", soft: "bg-orange-500/10", text: "text-orange-700 dark:text-orange-300", swatch: "bg-orange-500" },
];

export function projectColorKey(meeting: Pick<MeetingListDto, "projectPublicId" | "projectId" | "projectName">): string {
  return meeting.projectPublicId || meeting.projectId || meeting.projectName || "unassigned";
}

export function projectColor(key: string | null | undefined): ProjectColor {
  const value = key?.trim() || "unassigned";
  let hash = 0;
  for (let index = 0; index < value.length; index += 1) {
    hash = (hash * 31 + value.charCodeAt(index)) | 0;
  }
  return PROJECT_COLORS[Math.abs(hash) % PROJECT_COLORS.length];
}
