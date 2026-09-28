import { describe, expect, it } from "vitest";
import type { TimerSegmentDto, TimerSessionDto } from "@/hooks/time-tracking/useTimer";
import {
  deriveTimerTotals,
  formatDurationSeconds,
  segmentDurationSeconds,
  sortSegmentsChronologically,
} from "@/lib/timer/timer-totals";

function seg(id: string, type: TimerSegmentDto["segmentType"], startedAt: string, endedAt?: string): TimerSegmentDto {
  return { id, segmentType: type, startedAt, endedAt: endedAt ?? null, durationSeconds: 0 };
}

const T0 = Date.parse("2026-09-22T09:00:00.000Z");
const iso = (offsetSeconds: number) => new Date(T0 + offsetSeconds * 1000).toISOString();

function makeSession(segments: TimerSegmentDto[], serverNowOffsetSeconds = 7325): TimerSessionDto {
  return {
    id: "session-1",
    resourceId: "resource-1",
    status: "AwaitingFinalization",
    startedAt: iso(0),
    endedAt: iso(serverNowOffsetSeconds),
    serverNow: iso(serverNowOffsetSeconds),
    timezone: "UTC",
    metrics: { workSeconds: 0, breakSeconds: 0, pauseSeconds: 0, idleSeconds: 0, breakCount: 0, workSessionCount: 0, longestWorkSeconds: 0, averageWorkSeconds: 0 },
    segments,
  };
}

const fullHistory: TimerSegmentDto[] = [
  seg("w1", "Work", iso(0), iso(3600)),
  seg("b1", "Break", iso(3600), iso(3660)),
  seg("w2", "Work", iso(3660), iso(5460)),
  seg("p1", "Pause", iso(5460), iso(5500)),
  seg("b2", "Break", iso(5500), iso(5520)),
  seg("i1", "Idle", iso(5520), iso(5620)),
  seg("p2", "Pause", iso(5620), iso(5680)),
  seg("i2", "Idle", iso(5680), iso(5700)),
  seg("i3", "Idle", iso(5700), iso(5720)),
];

const fullTotals = deriveTimerTotals(makeSession(fullHistory), T0 + 5725 * 1000);

describe("timer totals — derived from TimerSegment authority (tests 1-8)", () => {
  it("1. Multiple Work segments sum correctly", () => {
    expect(fullTotals.workSeconds).toBe(3600 + 1800);
    expect(fullTotals.work.count).toBe(2);
  });

  it("2. Two Break segments → count = 2", () => {
    expect(fullTotals.breakCount).toBe(2);
  });

  it("3. Break total equals individual Break durations", () => {
    const individual = fullTotals.break.segments.reduce(
      (sum, segment) => sum + segmentDurationSeconds(segment, T0 + 5725 * 1000),
      0
    );
    expect(fullTotals.breakSeconds).toBe(60 + 20);
    expect(fullTotals.breakSeconds).toBe(individual);
  });

  it("4. Manual Pause count is correct (idle excluded)", () => {
    expect(fullTotals.pauseCount).toBe(2);
  });

  it("5. Pause total excludes Idle", () => {
    expect(fullTotals.pauseSeconds).toBe(40 + 60);
    expect(fullTotals.pauseSeconds).not.toBe(40 + 60 + 100 + 20 + 20);
  });

  it("6. Idle count is correct", () => {
    expect(fullTotals.idleCount).toBe(3);
  });

  it("7. Idle total is correct", () => {
    expect(fullTotals.idleSeconds).toBe(100 + 20 + 20);
  });

  it("8. Session total = Work + Break + Pause + Idle", () => {
    expect(fullTotals.sessionSeconds).toBe(
      fullTotals.workSeconds + fullTotals.breakSeconds + fullTotals.pauseSeconds + fullTotals.idleSeconds
    );
    expect(fullTotals.sessionSeconds).toBe(5720);
  });
});

describe("floating card counter behavior (tests 9-15)", () => {
  const runningSession = makeSession(
    [seg("w1", "Work", iso(0), iso(3600)), seg("w2", "Work", iso(3610))],
    3620
  );
  const onBreakSession = makeSession(
    [seg("w1", "Work", iso(0), iso(3600)), seg("b1", "Break", iso(3600))],
    3660
  );
  const pausedSession = makeSession(
    [seg("w1", "Work", iso(0), iso(3600)), seg("p1", "Pause", iso(3610))],
    3620
  );
  const idleSession = makeSession(
    [seg("w1", "Work", iso(0), iso(3600)), seg("p1", "Pause", iso(3605), iso(3610)), seg("i1", "Idle", iso(3610))],
    3620
  );

  it("9. Work counter advances only while Working", () => {
    const serverNow = T0 + 3620 * 1000;
    const later = serverNow + 5_000;
    const runningNow = deriveTimerTotals(runningSession, serverNow);
    const runningLater = deriveTimerTotals(runningSession, later);
    expect(runningLater.workSeconds).toBe(runningNow.workSeconds + 5);

    const frozenNow = deriveTimerTotals(onBreakSession, serverNow);
    const frozenLater = deriveTimerTotals(onBreakSession, later);
    expect(frozenLater.workSeconds).toBe(frozenNow.workSeconds);
    expect(frozenNow.workSeconds).toBe(3600);
  });

  it("10. Break counter advances only during Break", () => {
    const serverNow = T0 + 3660 * 1000;
    const later = serverNow + 5_000;
    const breakNow = deriveTimerTotals(onBreakSession, serverNow);
    const breakLater = deriveTimerTotals(onBreakSession, later);
    expect(breakLater.breakSeconds).toBe(breakNow.breakSeconds + 5);
    expect(breakNow.breakSeconds).toBe(60);

    const runningBreak = deriveTimerTotals(runningSession, serverNow);
    expect(runningBreak.breakSeconds).toBe(0);
  });

  it("11. Pause counter advances only during manual Pause", () => {
    const serverNow = T0 + 3620 * 1000;
    const later = serverNow + 5_000;
    const pauseNow = deriveTimerTotals(pausedSession, serverNow);
    const pauseLater = deriveTimerTotals(pausedSession, later);
    expect(pauseLater.pauseSeconds).toBe(pauseNow.pauseSeconds + 5);
    expect(pauseNow.pauseSeconds).toBe(10);

    const runningPause = deriveTimerTotals(runningSession, serverNow);
    expect(runningPause.pauseSeconds).toBe(0);
  });

  it("12. Idle does not inflate the Pause counter", () => {
    const serverNow = T0 + 3620 * 1000;
    const totals = deriveTimerTotals(idleSession, serverNow);
    expect(totals.pauseSeconds).toBe(5);
    expect(totals.idleSeconds).toBe(10);
    const later = deriveTimerTotals(idleSession, serverNow + 5_000);
    expect(later.pauseSeconds).toBe(5);
    expect(later.idleSeconds).toBe(15);
  });

  it("13. Resume continues accumulated Work across a Break", () => {
    const resumed = deriveTimerTotals(
      makeSession([seg("w1", "Work", iso(0), iso(1800)), seg("b1", "Break", iso(1800), iso(2100)), seg("w2", "Work", iso(2100))], 2110),
      T0 + 2110 * 1000
    );
    expect(resumed.workSeconds).toBe(1800 + 10);
    expect(resumed.breakSeconds).toBe(300);
  });

  it("14. Navigation (remount) does not reset counters", () => {
    const first = deriveTimerTotals(runningSession, T0 + 3620 * 1000);
    const remountCopy = makeSession([...runningSession.segments], 3620);
    const second = deriveTimerTotals(remountCopy, T0 + 3620 * 1000);
    expect(second).toEqual(first);
  });

  it("15. Refresh reconstructs counters from persisted server segments", () => {
    const refreshed = deriveTimerTotals(makeSession(fullHistory), T0 + 5725 * 1000);
    expect(refreshed.workSeconds).toBe(5400);
    expect(refreshed.breakSeconds).toBe(80);
    expect(refreshed.pauseSeconds).toBe(100);
    expect(refreshed.idleSeconds).toBe(140);
    expect(refreshed.sessionSeconds).toBe(5720);
  });
});

describe("finalize consistency — Work is the only Actual Work source (tests 27-31)", () => {
  it("27. Work total equals the sum of Work segment durations (TimeEntry Actual basis)", () => {
    const workSum = fullHistory
      .filter((segment) => segment.segmentType === "Work")
      .reduce((sum, segment) => sum + (Date.parse(segment.endedAt!) - Date.parse(segment.startedAt)) / 1000, 0);
    expect(fullTotals.workSeconds).toBe(workSum);
    expect(fullTotals.workSeconds).toBe(5400);
  });

  it("28. Break does not inflate Work", () => {
    const withBreak = deriveTimerTotals(makeSession([seg("w1", "Work", iso(0), iso(600)), seg("b1", "Break", iso(600), iso(900))], 900), T0 + 900 * 1000);
    const withoutBreak = deriveTimerTotals(makeSession([seg("w1", "Work", iso(0), iso(600))], 600), T0 + 600 * 1000);
    expect(withBreak.workSeconds).toBe(withoutBreak.workSeconds);
    expect(withBreak.workSeconds).toBe(600);
  });

  it("29. Pause does not inflate Work", () => {
    const withPause = deriveTimerTotals(makeSession([seg("w1", "Work", iso(0), iso(600)), seg("p1", "Pause", iso(600), iso(660))], 660), T0 + 660 * 1000);
    expect(withPause.workSeconds).toBe(600);
  });

  it("30. Idle does not inflate Work", () => {
    const withIdle = deriveTimerTotals(makeSession([seg("w1", "Work", iso(0), iso(600)), seg("i1", "Idle", iso(600), iso(720))], 720), T0 + 720 * 1000);
    expect(withIdle.workSeconds).toBe(600);
    expect(withIdle.idleSeconds).toBe(120);
  });

  it("31. Cross-midnight Work segment sums correctly", () => {
    const start = Date.parse("2026-09-22T23:30:00.000Z");
    const end = Date.parse("2026-09-23T00:30:00.000Z");
    const totals = deriveTimerTotals(
      makeSession([seg("w-cross", "Work", new Date(start).toISOString(), new Date(end).toISOString())], 0),
      end
    );
    expect(totals.workSeconds).toBe(3600);
    expect(totals.sessionSeconds).toBe(3600);
  });
});

describe("formatting and ordering", () => {
  it("sorts events chronologically with SegmentId tie-break", () => {
    const sorted = sortSegmentsChronologically([
      seg("z", "Work", iso(100), iso(200)),
      seg("a", "Break", iso(50), iso(60)),
      seg("b", "Break", iso(100), iso(110)),
      seg("a2", "Break", iso(100), iso(120)),
    ]);
    expect(sorted.map((segment) => segment.id)).toEqual(["a", "a2", "b", "z"]);
  });

  it("renders Western digits for AR locale", () => {
    expect(formatDurationSeconds(5077, "en")).toBe("01:24:37");
    expect(formatDurationSeconds(5077, "ar")).toBe("01:24:37");
    expect(formatDurationSeconds(5077, "ar")).not.toMatch(/[٠-٩]/);
  });
});
