import { describe, expect, it } from "vitest";
import {
  MONTH_GRID_DAYS,
  calendarRange,
  groupMeetingsByDay,
  layoutDayMeetings,
  meetingEnd,
  meetingsOnDay,
  monthGridDays,
  parseCalendarDate,
  projectColor,
  projectColorKey,
  shiftCalendarAnchor,
  sortMeetings,
  toCalendarDateValue,
  weekDays,
} from "@features/delivery/meetings-calendar";

function localIso(year: number, monthIndex: number, day: number, hours = 0, minutes = 0) {
  return new Date(year, monthIndex, day, hours, minutes, 0, 0).toISOString();
}

function meeting(id: string, start: string, end: string, title = id, projectPublicId = "public-1") {
  return { id, title, startTime: start, endTime: end, projectPublicId, projectId: `internal-${projectPublicId}`, projectName: "Project" };
}

describe("meetings calendar ranges", () => {
  it("builds a 42-day month grid starting on Monday", () => {
    const anchor = new Date(2026, 9, 15);
    const range = calendarRange("month", anchor);
    const days = monthGridDays(anchor);

    expect(days).toHaveLength(MONTH_GRID_DAYS);
    expect(range.from.getDay()).toBe(1);
    expect(toCalendarDateValue(days[0])).toBe("2026-09-28");
    expect(toCalendarDateValue(days[days.length - 1])).toBe("2026-11-08");
    expect(range.to.getHours()).toBe(23);
  });

  it("builds Monday-to-Sunday week and single-day ranges", () => {
    const anchor = new Date(2026, 9, 15, 12);
    const week = calendarRange("week", anchor);
    const days = weekDays(anchor);
    const day = calendarRange("day", anchor);

    expect(days).toHaveLength(7);
    expect(toCalendarDateValue(days[0])).toBe("2026-10-12");
    expect(toCalendarDateValue(days[6])).toBe("2026-10-18");
    expect(toCalendarDateValue(week.from)).toBe("2026-10-12");
    expect(toCalendarDateValue(day.from)).toBe("2026-10-15");
    expect(toCalendarDateValue(day.to)).toBe("2026-10-15");
  });

  it("shifts anchors by day, week and month", () => {
    const anchor = new Date(2026, 9, 15);

    expect(toCalendarDateValue(shiftCalendarAnchor("day", anchor, 1))).toBe("2026-10-16");
    expect(toCalendarDateValue(shiftCalendarAnchor("week", anchor, -1))).toBe("2026-10-08");
    expect(toCalendarDateValue(shiftCalendarAnchor("month", anchor, 1))).toBe("2026-11-15");
  });

  it("parses only valid calendar date values", () => {
    expect(toCalendarDateValue(parseCalendarDate("2026-10-15")!)).toBe("2026-10-15");
    expect(parseCalendarDate("not-a-date")).toBeNull();
    expect(parseCalendarDate(null)).toBeNull();
  });
});

describe("meetings calendar grouping", () => {
  it("includes meetings that overlap the day and sorts them by start time", () => {
    const day = new Date(2026, 9, 15);
    const early = meeting("early", localIso(2026, 9, 15, 9), localIso(2026, 9, 15, 10));
    const late = meeting("late", localIso(2026, 9, 15, 14), localIso(2026, 9, 15, 15));
    const overnight = meeting("overnight", localIso(2026, 9, 14, 23), localIso(2026, 9, 15, 1));
    const outside = meeting("outside", localIso(2026, 9, 16, 9), localIso(2026, 9, 16, 10));

    const result = meetingsOnDay([late, outside, overnight, early], day);

    expect(result.map((item) => item.id)).toEqual(["overnight", "early", "late"]);
  });

  it("splits a meeting across every day it covers", () => {
    const overnight = meeting("overnight", localIso(2026, 9, 15, 23), localIso(2026, 9, 16, 1));
    const single = meeting("single", localIso(2026, 9, 15, 9), localIso(2026, 9, 15, 10));

    const grouped = groupMeetingsByDay([overnight, single]);

    expect(grouped.get("2026-10-15")?.map((item) => item.id)).toEqual(["single", "overnight"]);
    expect(grouped.get("2026-10-16")?.map((item) => item.id)).toEqual(["overnight"]);
    expect(grouped.has("2026-10-17")).toBe(false);
  });

  it("falls back to a 30 minute block when the end time is missing or invalid", () => {
    const start = localIso(2026, 9, 15, 9);
    expect(meetingEnd({ startTime: start, endTime: start })).toEqual(new Date(new Date(start).getTime() + 30 * 60_000));
    expect(meetingEnd({ startTime: start, endTime: "invalid" })).toEqual(new Date(new Date(start).getTime() + 30 * 60_000));
  });

  it("sorts by start time and then title", () => {
    const later = meeting("later", localIso(2026, 9, 15, 10), localIso(2026, 9, 15, 11), "Alpha");
    const firstB = meeting("first-b", localIso(2026, 9, 15, 9), localIso(2026, 9, 15, 10), "Bravo");
    const firstA = meeting("first-a", localIso(2026, 9, 15, 9), localIso(2026, 9, 15, 10), "Alpha");

    expect(sortMeetings([later, firstB, firstA]).map((item) => item.id)).toEqual(["first-a", "first-b", "later"]);
  });
});

describe("meetings calendar overlap layout", () => {
  it("gives overlapping meetings side-by-side columns and leaves isolated meetings full width", () => {
    const day = new Date(2026, 9, 15);
    const first = meeting("first", localIso(2026, 9, 15, 9), localIso(2026, 9, 15, 10));
    const overlapping = meeting("overlapping", localIso(2026, 9, 15, 9, 30), localIso(2026, 9, 15, 10, 30));
    const isolated = meeting("isolated", localIso(2026, 9, 15, 11), localIso(2026, 9, 15, 12));

    const positioned = layoutDayMeetings([isolated, overlapping, first], day);
    const byId = new Map(positioned.map((item) => [item.meeting.id, item]));

    expect(byId.get("first")).toMatchObject({ column: 0, columnCount: 2 });
    expect(byId.get("overlapping")).toMatchObject({ column: 1, columnCount: 2 });
    expect(byId.get("isolated")).toMatchObject({ column: 0, columnCount: 1 });
  });

  it("clamps events that start before the day and keeps a minimum visible height", () => {
    const day = new Date(2026, 9, 15);
    const overnight = meeting("overnight", localIso(2026, 9, 14, 22), localIso(2026, 9, 15, 1));
    const tiny = meeting("tiny", localIso(2026, 9, 15, 9), localIso(2026, 9, 15, 9, 2));

    const positioned = layoutDayMeetings([overnight, tiny], day);
    const byId = new Map(positioned.map((item) => [item.meeting.id, item]));

    expect(byId.get("overnight")?.startMinutes).toBe(0);
    expect(byId.get("overnight")?.endMinutes).toBe(60);
    expect(byId.get("tiny")!.endMinutes - byId.get("tiny")!.startMinutes).toBeGreaterThanOrEqual(15);
  });
});

describe("meetings calendar project colors", () => {
  it("assigns the same palette entry for the same project key", () => {
    expect(projectColor("public-project-1")).toEqual(projectColor("public-project-1"));
    expect(projectColor("")).toEqual(projectColor("unassigned"));
    expect(projectColor(null)).toEqual(projectColor("unassigned"));
  });

  it("prefers the public id, then internal id, then name when choosing a color key", () => {
    expect(projectColorKey({ projectPublicId: "public-1", projectId: "internal-1", projectName: "Name" })).toBe("public-1");
    expect(projectColorKey({ projectPublicId: null, projectId: "internal-1", projectName: "Name" })).toBe("internal-1");
    expect(projectColorKey({ projectPublicId: null, projectId: null, projectName: "Name" })).toBe("Name");
    expect(projectColorKey({ projectPublicId: null, projectId: null, projectName: null })).toBe("unassigned");
  });
});
