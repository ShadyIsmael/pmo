"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "@/lib/api";
import { endpoints } from "@/lib/api/endpoints";
import { clearSessionReceivedStamp, stampSessionReceived } from "@/lib/timer/timer-totals";
import { timeEntryKeys } from "./useTimeEntries";

export type TimerStatus = "Running" | "OnBreak" | "Paused" | "IdlePaused" | "AwaitingFinalization" | "Finished" | "Cancelled";

export interface TimerMetricsDto { workSeconds: number; breakSeconds: number; pauseSeconds: number; idleSeconds: number; breakCount: number; workSessionCount: number; longestWorkSeconds: number; averageWorkSeconds: number; }
export interface TimerSegmentDto { id: string; segmentType: "Work" | "Break" | "Pause" | "Idle"; startedAt: string; endedAt?: string | null; durationSeconds: number; }
export interface TimerSessionDto { id: string; resourceId: string; status: TimerStatus; startedAt: string; endedAt?: string | null; serverNow: string; timezone: string; metrics: TimerMetricsDto; segments: TimerSegmentDto[]; }
export interface FinalizeTimerInput { projectId: string; planActivityId?: string; workItemId?: string; projectRoleId?: string; description?: string; }

export const timerKeys = { all: ["timer"] as const, current: ["timer", "current"] as const, detail: (id: string) => ["timer", id] as const };

function isFullSession(data: unknown): data is TimerSessionDto {
  if (!data || typeof data !== "object" || Array.isArray(data)) return false;
  const value = data as Partial<TimerSessionDto>;
  return (
    typeof value.id === "string" &&
    typeof value.status === "string" &&
    typeof value.serverNow === "string" &&
    Array.isArray(value.segments) &&
    Boolean(value.metrics) &&
    typeof value.metrics === "object" &&
    typeof (value.metrics as TimerMetricsDto).workSeconds === "number"
  );
}

function cacheSession(client: ReturnType<typeof useQueryClient>, data: TimerSessionDto | TimerSessionDto[] | undefined | null): void {
  if (!isFullSession(data)) return;
  stampSessionReceived(data);
  if (data.status === "Cancelled" || data.status === "Finished") {
    client.removeQueries({ queryKey: timerKeys.current });
    clearSessionReceivedStamp(data.id);
    return;
  }
  client.setQueryData(timerKeys.current, data);
}

export function useCurrentTimer(enabled = true) {
  return useQuery({
    queryKey: timerKeys.current,
    queryFn: async (): Promise<TimerSessionDto | null> => {
      const result = await api.get<TimerSessionDto | null | undefined>(endpoints.timer.current);
      if (result && isFullSession(result)) stampSessionReceived(result);
      return result ?? null;
    },
    enabled,
    placeholderData: null,
    refetchInterval: 30_000,
    refetchIntervalInBackground: true,
    staleTime: 5_000,
    retry: 2,
  });
}

function useTimerMutation<T>(mutationFn: (_input: T) => Promise<TimerSessionDto | TimerSessionDto[] | undefined>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: (data) => {
      cacheSession(client, data);
      client.invalidateQueries({ queryKey: timerKeys.current });
      client.invalidateQueries({ queryKey: timeEntryKeys.all });
    },
    onError: (error: unknown) => { console.error("Timer action failed:", error); },
  });
}

export function useStartTimer() { return useTimerMutation<void>((_input) => api.post<TimerSessionDto>(endpoints.timer.start)); }
export function useTimerAction(action: "break" | "pause" | "resume" | "finish" | "cancel") { return useTimerMutation<string>((id) => api.post<TimerSessionDto>(endpoints.timer[action](id))); }
export function useTimerHeartbeat() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: { id: string; isIdle: boolean; lastActivityAt?: string }) => api.post<TimerSessionDto>(endpoints.timer.heartbeat(input.id), { isIdle: input.isIdle, lastActivityAt: input.lastActivityAt }),
    onSuccess: (data) => { cacheSession(client, data); },
    onError: (error: unknown) => { console.error("Timer action failed:", error); },
  });
}
export function useFinalizeTimer() { return useTimerMutation<{ id: string; input: FinalizeTimerInput }>((value) => api.post<TimerSessionDto[]>(endpoints.timer.finalize(value.id), value.input)); }

export function useEligibleProjects(id: string, status?: TimerStatus) {
  return useQuery({ queryKey: ["timer", id, "eligible-projects"], queryFn: async (): Promise<EligibleProjectDto[]> => { if (!id) return []; const result = await api.get<EligibleProjectDto[]>(endpoints.timer.eligibleProjects(id)); return result ?? []; }, enabled: Boolean(id) && status === "AwaitingFinalization", retry: (failureCount, error: unknown) => { if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false; return failureCount < 2; } });
}

export interface EligibleProjectDto { id: string; code: string; name: string; }
