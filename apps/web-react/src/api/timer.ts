import { useQuery } from "@tanstack/react-query";
import { api } from "./client";

export type TimerStatus = "Running" | "OnBreak" | "Paused" | "IdlePaused" | "AwaitingFinalization" | "Finished" | "Cancelled";
export interface TimerSession { id: string; status: TimerStatus; startedAt: string; serverNow: string; timezone: string; metrics: { workSeconds: number; breakSeconds: number; pauseSeconds: number; idleSeconds: number; }; }

export function useCurrentTimer(enabled: boolean) {
  return useQuery({
    queryKey: ["timer", "current"],
    queryFn: () => api.get<TimerSession | null>("/api/timer/current"),
    enabled,
    staleTime: 60_000,
    gcTime: 300_000,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    refetchInterval: false,
  });
}
