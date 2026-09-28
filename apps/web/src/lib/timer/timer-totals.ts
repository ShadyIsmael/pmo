import type { TimerSegmentDto, TimerSessionDto, TimerStatus } from "@/hooks/time-tracking/useTimer";

export type TimerSegmentType = TimerSegmentDto["segmentType"];

export function westernNumberLocale(locale: string): string {
  return locale === "ar" ? "ar-EG-u-nu-latn" : locale;
}

export function formatDurationSeconds(value: number, locale: string): string {
  const seconds = Math.max(0, Math.floor(value));
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const remainder = seconds % 60;
  const fmt = (n: number) =>
    new Intl.NumberFormat(westernNumberLocale(locale), { minimumIntegerDigits: 2, useGrouping: false }).format(n);
  return `${fmt(hours)}:${fmt(minutes)}:${fmt(remainder)}`;
}

export function estimateServerNowMs(session: TimerSessionDto, receivedAtLocalMs: number, localNowMs: number): number {
  return new Date(session.serverNow).getTime() + Math.max(0, localNowMs - receivedAtLocalMs);
}

const receiveStamps = new WeakMap<object, number>();
const receiveStampsById = new Map<string, number>();

export function stampSessionReceived(session: TimerSessionDto): void {
  const stamp = Date.now();
  receiveStamps.set(session, stamp);
  receiveStampsById.set(session.id, stamp);
}

export function sessionReceivedAtMs(session: TimerSessionDto): number {
  const byObject = receiveStamps.get(session);
  if (byObject !== undefined) return byObject;
  const byId = receiveStampsById.get(session.id);
  if (byId !== undefined) {
    receiveStamps.set(session, byId);
    return byId;
  }
  const stamp = Date.now();
  receiveStamps.set(session, stamp);
  receiveStampsById.set(session.id, stamp);
  return stamp;
}

export function clearSessionReceivedStamp(sessionId: string): void {
  receiveStampsById.delete(sessionId);
}

export function isTimerActive(status?: TimerStatus): boolean {
  return status === "Running" || status === "OnBreak" || status === "Paused" || status === "IdlePaused";
}

export function sortSegmentsChronologically(segments: readonly TimerSegmentDto[]): TimerSegmentDto[] {
  return [...segments].sort((a, b) => {
    const at = new Date(a.startedAt).getTime();
    const bt = new Date(b.startedAt).getTime();
    if (at !== bt) return at - bt;
    if (a.id === b.id) return 0;
    return a.id < b.id ? -1 : 1;
  });
}

export function segmentDurationSeconds(segment: TimerSegmentDto, serverNowMs: number): number {
  const startMs = new Date(segment.startedAt).getTime();
  const endMs = segment.endedAt ? new Date(segment.endedAt).getTime() : serverNowMs;
  return Math.max(0, Math.round((endMs - startMs) / 1000));
}

export interface TimerSegmentGroup {
  segments: TimerSegmentDto[];
  totalSeconds: number;
  count: number;
}

export interface TimerTotals {
  work: TimerSegmentGroup;
  break: TimerSegmentGroup;
  pause: TimerSegmentGroup;
  idle: TimerSegmentGroup;
  workSeconds: number;
  breakSeconds: number;
  pauseSeconds: number;
  idleSeconds: number;
  sessionSeconds: number;
  breakCount: number;
  pauseCount: number;
  idleCount: number;
}

function groupSegments(sorted: readonly TimerSegmentDto[], type: TimerSegmentType, serverNowMs: number): TimerSegmentGroup {
  const segments = sorted.filter((segment) => segment.segmentType === type);
  const totalSeconds = segments.reduce((sum, segment) => sum + segmentDurationSeconds(segment, serverNowMs), 0);
  return { segments, totalSeconds, count: segments.length };
}

export function deriveTimerTotals(
  session: Pick<TimerSessionDto, "segments"> | null | undefined,
  serverNowMs: number
): TimerTotals {
  const sorted = sortSegmentsChronologically(session?.segments ?? []);
  const work = groupSegments(sorted, "Work", serverNowMs);
  const breakGroup = groupSegments(sorted, "Break", serverNowMs);
  const pause = groupSegments(sorted, "Pause", serverNowMs);
  const idle = groupSegments(sorted, "Idle", serverNowMs);
  return {
    work,
    break: breakGroup,
    pause,
    idle,
    workSeconds: work.totalSeconds,
    breakSeconds: breakGroup.totalSeconds,
    pauseSeconds: pause.totalSeconds,
    idleSeconds: idle.totalSeconds,
    sessionSeconds: work.totalSeconds + breakGroup.totalSeconds + pause.totalSeconds + idle.totalSeconds,
    breakCount: breakGroup.count,
    pauseCount: pause.count,
    idleCount: idle.count,
  };
}
