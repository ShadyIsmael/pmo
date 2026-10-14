import { useQueryClient, useMutation, useQuery } from "@tanstack/react-query";
import { api } from "./client";
import { endpoints } from "./endpoints";
import { stampSessionReceived } from "../lib/timer-totals";
import type { ManagementRequestContext } from "./client";
import { MANAGEMENT_PERSPECTIVES, type ManagementPerspective, type PerspectiveSnapshot } from "../lib/perspective";

export type TimerStatus = "Running" | "OnBreak" | "Paused" | "IdlePaused" | "AwaitingFinalization" | "Finished" | "Cancelled";
export interface TimerMetricsDto { workSeconds: number; breakSeconds: number; pauseSeconds: number; idleSeconds: number; breakCount: number; workSessionCount: number; longestWorkSeconds: number; averageWorkSeconds: number; }
export interface TimerSegmentDto { id: string; segmentType: "Work" | "Break" | "Pause" | "Idle"; startedAt: string; endedAt?: string | null; durationSeconds: number; }
export interface TimerSessionDto { id: string; resourceId: string; status: TimerStatus; startedAt: string; endedAt?: string | null; serverNow: string; timezone: string; metrics: TimerMetricsDto; segments: TimerSegmentDto[]; }
export type TimerSession = TimerSessionDto;
export interface FinalizeTimerInput { projectId: string; workItemId?: string; description?: string; }
export interface EligibleProjectDto { id: string; code: string; name: string; }

export const timerKeys = { all: ["timer"] as const, current: ["timer", "current"] as const, detail: (id: string) => ["timer", id] as const };

function isFullSession(data: unknown): data is TimerSessionDto {
  if (!data || typeof data !== "object" || Array.isArray(data)) return false;
  const v = data as Partial<TimerSessionDto>;
  return typeof v.id === "string" && typeof v.status === "string" && typeof v.serverNow === "string";
}

function cacheSession(client: ReturnType<typeof useQueryClient>, data: TimerSessionDto | TimerSessionDto[] | undefined | null): boolean {
  if (Array.isArray(data) || !isFullSession(data)) return false;
  stampSessionReceived(data);
  if (data.status === "Cancelled" || data.status === "Finished") {
    client.setQueryData(timerKeys.current, null);
    return true;
  }
  client.setQueryData(timerKeys.current, data);
  return true;
}

/**
 * VISIBLE-FIRST + NO POLLING: single GET on startup. If no active timer, query stays idle.
 * If active, elapsed is computed locally; 30s heartbeat only while Running/IdlePaused.
 */
export function useCurrentTimer(enabled: boolean) {
  return useQuery({
    queryKey: timerKeys.current,
    queryFn: async (): Promise<TimerSessionDto | null> => {
      const result = await api.get<TimerSessionDto | null>(endpoints.timer.current);
      if (result && isFullSession(result)) stampSessionReceived(result);
      return result ?? null;
    },
    enabled,
    placeholderData: null,
    staleTime: 60_000,
    gcTime: 300_000,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    refetchInterval: false,
  });
}

function useTimerMutation<T>(mutationFn: (input: T) => Promise<TimerSessionDto | TimerSessionDto[] | undefined>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: (data) => {
      if (!cacheSession(client, data)) void client.invalidateQueries({ queryKey: timerKeys.current });
      void client.invalidateQueries({ queryKey: ["timeEntries"] });
    },
  });
}

export function useStartTimer() { return useTimerMutation<void>(() => api.post<TimerSessionDto>(endpoints.timer.start)); }
export function useTimerAction(action: "break" | "pause" | "resume" | "finish" | "cancel") {
  return useTimerMutation<string>((sessionId) => api.post<TimerSessionDto>(endpoints.timer[action](sessionId)));
}
export function useTimerHeartbeat() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; isIdle: boolean; lastActivityAt?: string }) =>
      api.post<TimerSessionDto>(`/api/timer/${encodeURIComponent(input.id)}/heartbeat`, { isIdle: input.isIdle, lastActivityAt: input.lastActivityAt }),
    onSuccess: (data) => { cacheSession(client, data); },
  });
}
export function useFinalizeTimer() {
  return useTimerMutation<{ id: string; input: FinalizeTimerInput }>((v) => api.post<TimerSessionDto[]>(endpoints.timer.finalize(v.id), v.input));
}
export function useEligibleProjects(sessionId: string, status?: TimerStatus) {
  return useQuery({
    queryKey: ["timer", sessionId, "eligible-projects"],
    queryFn: () => api.get<EligibleProjectDto[]>(`/api/timer/${encodeURIComponent(sessionId)}/eligible-projects`),
    enabled: Boolean(sessionId) && status === "AwaitingFinalization",
    staleTime: 30_000,
  });
}

// Re-export management context helpers for HeaderTimer convenience
export type { ManagementRequestContext };
export { MANAGEMENT_PERSPECTIVES };
export type { ManagementPerspective, PerspectiveSnapshot };
