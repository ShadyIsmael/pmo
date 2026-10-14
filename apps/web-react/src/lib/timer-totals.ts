import type { TimerSegmentDto, TimerSessionDto, TimerStatus } from "../api/timer";

export function westernDigits(value: number): string {
  return new Intl.NumberFormat("en-US", { minimumIntegerDigits: 2, useGrouping: false }).format(value);
}

export function formatDurationSeconds(totalSeconds: number): string {
  const seconds = Math.max(0, Math.floor(totalSeconds));
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const remainder = seconds % 60;
  return `${westernDigits(hours)}:${westernDigits(minutes)}:${westernDigits(remainder)}`;
}

export function estimateServerNowMs(session: TimerSessionDto, receivedAtLocalMs: number, localNowMs: number): number {
  return new Date(session.serverNow).getTime() + Math.max(0, localNowMs - receivedAtLocalMs);
}

const receiveStampsById = new Map<string, number>();

export function stampSessionReceived(session: TimerSessionDto): void {
  receiveStampsById.set(session.id, Date.now());
}

export function sessionReceivedAtMs(session: TimerSessionDto): number {
  const existing = receiveStampsById.get(session.id);
  if (existing !== undefined) return existing;
  const stamp = Date.now();
  receiveStampsById.set(session.id, stamp);
  return stamp;
}

export type TimerSegmentType = TimerSegmentDto["segmentType"];

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

export function sortSegmentsChronologically(segments: readonly TimerSegmentDto[]): TimerSegmentDto[] {
  return [...segments].sort((left, right) => {
    const leftStart = new Date(left.startedAt).getTime();
    const rightStart = new Date(right.startedAt).getTime();
    if (leftStart !== rightStart) return leftStart - rightStart;
    if (left.id === right.id) return 0;
    return left.id < right.id ? -1 : 1;
  });
}

export function segmentDurationSeconds(segment: TimerSegmentDto, serverNowMs: number): number {
  const startMs = new Date(segment.startedAt).getTime();
  const endMs = segment.endedAt ? new Date(segment.endedAt).getTime() : serverNowMs;
  return Math.max(0, Math.round((endMs - startMs) / 1000));
}

function groupSegments(sorted: readonly TimerSegmentDto[], type: TimerSegmentType, serverNowMs: number): TimerSegmentGroup {
  const segments = sorted.filter((segment) => segment.segmentType === type);
  return {
    segments,
    totalSeconds: segments.reduce((sum, segment) => sum + segmentDurationSeconds(segment, serverNowMs), 0),
    count: segments.length,
  };
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

export function isTimerActive(status?: TimerStatus | null): boolean {
  return status === "Running" || status === "OnBreak" || status === "Paused" || status === "IdlePaused";
}

export function deriveWorkSeconds(session: TimerSessionDto, serverNowMs: number): number {
  return deriveTimerTotals(session, serverNowMs).workSeconds;
}

export type { TimerSegmentDto, TimerSessionDto, TimerStatus };
