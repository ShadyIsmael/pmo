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

export function isTimerActive(status?: TimerStatus | null): boolean {
  return status === "Running" || status === "OnBreak" || status === "Paused" || status === "IdlePaused";
}

export function deriveWorkSeconds(session: TimerSessionDto, serverNowMs: number): number {
  // Backend metrics.workSeconds is authoritative up to serverNow; add local delta only while active.
  const base = session.metrics.workSeconds;
  if (!isTimerActive(session.status)) return base;
  const serverNow = new Date(session.serverNow).getTime();
  const delta = Math.max(0, Math.floor((serverNowMs - serverNow) / 1000));
  // Only Running accrues work time locally; Break/Pause/Idle accrue separately and are shown via segments on sync.
  if (session.status !== "Running") return base;
  return base + delta;
}

export type { TimerSegmentDto, TimerSessionDto, TimerStatus };
