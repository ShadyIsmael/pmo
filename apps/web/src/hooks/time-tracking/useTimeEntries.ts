"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api";
import { endpoints } from "@/lib/api/endpoints";

export interface TimeEntryDto {
  id: string;
  resourceId: string;
  resourceName: string;
  projectId: string;
  projectName: string;
  planActivityId?: string | null;
  planActivityName?: string | null;
  workItemId?: string | null;
  projectRoleId?: string | null;
  timerSessionId?: string | null;
  workDate: string;
  startTime: string;
  endTime: string;
  elapsedSeconds: number;
  actualSeconds: number;
  breakSeconds: number;
  pauseSeconds: number;
  elapsedHours: number;
  actualHours: number;
  description?: string | null;
  entrySource: string;
  rowVersion: number[];
}

export interface TimeEntrySummaryDto {
  from: string;
  to: string;
  actualHours: number;
  plannedHours: number;
  workVarianceHours: number;
  capacityHours: number;
  plannedUtilization: number;
  actualUtilization: number;
  byTeam: ActualWorkBreakdownDto[];
}

export interface ActualWorkBreakdownDto {
  id?: string | null;
  label: string;
  actualHours: number;
}

export interface TimeEntryFilter extends Record<string, string | number | boolean | undefined> {
  page?: number;
  pageSize?: number;
  resourceId?: string;
  projectId?: string;
  from?: string;
  to?: string;
}

export interface CreateTimeEntryInput {
  projectId: string;
  workDate: string;
  startTime?: string;
  endTime?: string;
  actualSeconds?: number;
  breakSeconds?: number;
  pauseSeconds?: number;
  description?: string;
}

export interface UpdateTimeEntryInput {
  projectId?: string;
  workDate?: string;
  startTime?: string;
  endTime?: string;
  description?: string;
  rowVersion: number[];
}

export const timeEntryKeys = {
  all: ["time-entries"] as const,
  list: (filter: TimeEntryFilter) => ["time-entries", "list", filter] as const,
  summary: (filter: TimeEntryFilter) => ["time-entries", "summary", filter] as const,
};

export function useTimeEntries(filter: TimeEntryFilter = {}) {
  return useQuery({
    queryKey: timeEntryKeys.list(filter),
    queryFn: () => api.get<PagedResult<TimeEntryDto>>(endpoints.timeEntries.root, filter),
    staleTime: 15_000,
  });
}

export function useTimeEntrySummary(filter: TimeEntryFilter = {}) {
  return useQuery({
    queryKey: timeEntryKeys.summary(filter),
    queryFn: () => api.get<TimeEntrySummaryDto>(endpoints.timeEntries.summary, filter),
    staleTime: 15_000,
  });
}

export function useCreateTimeEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateTimeEntryInput) => api.post<TimeEntryDto>(endpoints.timeEntries.root, input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: timeEntryKeys.all }),
  });
}

export function useDeleteTimeEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.timeEntries.detail(id)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: timeEntryKeys.all }),
  });
}

export function useUpdateTimeEntry(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateTimeEntryInput) => api.put<TimeEntryDto>(endpoints.timeEntries.detail(id), input),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: timeEntryKeys.all }),
  });
}
