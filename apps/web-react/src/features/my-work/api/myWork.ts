import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";

export interface TimeEntryDto {
  id: string;
  projectId: string;
  projectName: string;
  projectCode: string;
  workDate: string;
  startTime: string;
  endTime: string;
  actualHours: number;
  description?: string;
  rowVersion: number[];
}

export interface TimeEntrySummary {
  actualHours: number;
  plannedHours: number;
  workVarianceHours: number;
  actualUtilization: number;
  byTeam: { id: string; label: string; actualHours: number }[];
}

export interface TimeEntryParams {
  page?: number;
  pageSize?: number;
  from?: string;
  to?: string;
  projectId?: string;
  [key: string]: string | number | boolean | undefined;
}

const timeEntryKeys = {
  all: ["timeEntries"] as const,
  lists: () => [...timeEntryKeys.all, "list"] as const,
  list: (params: TimeEntryParams) => [...timeEntryKeys.lists(), params] as const,
  summary: (params: { from: string; to: string }) => [...timeEntryKeys.all, "summary", params] as const,
};

export function useTimeEntries(params: TimeEntryParams = {}) {
  return useQuery({
    queryKey: timeEntryKeys.list(params),
    queryFn: () => api.get<{ items: TimeEntryDto[]; totalCount: number; page: number; pageSize: number; totalPages: number }>("/api/time-entries", params),
    staleTime: 30_000,
  });
}

export function useTimeEntrySummary(params: { from: string; to: string }) {
  return useQuery({
    queryKey: timeEntryKeys.summary(params),
    queryFn: () => api.get<TimeEntrySummary>("/api/time-entries/summary", params),
    staleTime: 30_000,
  });
}

export function useCreateTimeEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { projectId: string; workDate: string; startTime: string; endTime: string; description?: string }) =>
      api.post<TimeEntryDto>("/api/time-entries", data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.all });
    },
  });
}

export function useUpdateTimeEntry(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { projectId: string; workDate: string; startTime: string; endTime: string; description?: string; rowVersion: number[] }) =>
      api.put<TimeEntryDto>(`/api/time-entries/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.all });
    },
  });
}

export function useDeleteTimeEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/api/time-entries/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.all });
    },
  });
}

export interface HandoverRequestDto {
  id: string;
  projectId: string;
  projectName: string;
  projectCode: string;
  currentProjectManagerId: string;
  currentProjectManagerName: string;
  newProjectManagerId: string;
  newProjectManagerName: string;
  effectiveHandoverDate: string;
  reason: string;
  notes?: string;
  status: string;
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
  respondedBy?: string;
  respondedByName?: string;
  respondedAt?: string;
  rejectionReason?: string;
}

export interface ManagerHandoverRequestDto {
  id: string;
  entityType: "Program" | "Portfolio";
  entityId: string;
  entityName: string;
  currentManagerId: string;
  currentManagerName: string;
  newManagerId: string;
  newManagerName: string;
  effectiveHandoverDate: string;
  reason: string;
  status: string;
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
}

export interface ProjectChangeRequestDto {
  id: string;
  projectId: string;
  projectName: string;
  number: string;
  type: string;
  status: string;
  reason: string;
  justification?: string;
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
}

export function usePendingHandoverRequestsForMe() {
  return useQuery({
    queryKey: ["handovers", "pendingForMe"],
    queryFn: () => api.get<HandoverRequestDto[]>("/api/handover-requests/pending-for-me"),
    staleTime: 15_000,
  });
}

export function usePendingManagerHandovers() {
  return useQuery({
    queryKey: ["managerHandovers", "pendingForMe"],
    queryFn: () => api.get<ManagerHandoverRequestDto[]>("/api/manager-handovers/pending-for-me"),
    staleTime: 15_000,
  });
}

export function usePendingProjectChangeRequests() {
  return useQuery({
    queryKey: ["projectChangeRequests", "pendingForMe"],
    queryFn: () => api.get<ProjectChangeRequestDto[]>("/api/project-change-requests/pending-for-me"),
    staleTime: 15_000,
  });
}

export function useAcceptHandoverRequest() {
  return useMutation({
    mutationFn: (requestId: string) => api.post<void>(`/api/handover-requests/${requestId}/accept`, {}),
  });
}

export function useRejectHandoverRequest() {
  return useMutation({
    mutationFn: ({ requestId, rejectionReason }: { requestId: string; rejectionReason: string }) =>
      api.post<void>(`/api/handover-requests/${requestId}/reject`, { rejectionReason }),
  });
}

export function useAcceptManagerHandover(type: "programs" | "portfolios") {
  return useMutation({
    mutationFn: (requestId: string) => api.post<void>(`/api/manager-handovers/${type}/requests/${requestId}/accept`, {}),
  });
}

export function useRejectManagerHandover(type: "programs" | "portfolios") {
  return useMutation({
    mutationFn: ({ requestId, rejectionReason }: { requestId: string; rejectionReason: string }) =>
      api.post<void>(`/api/manager-handovers/${type}/requests/${requestId}/reject`, { rejectionReason }),
  });
}

export function useApproveProjectChangeRequest() {
  return useMutation({
    mutationFn: ({ id, dto }: { id: string; dto: Record<string, unknown> }) =>
      api.post<void>(`/api/project-change-requests/${id}/approve`, dto),
  });
}

export function useRejectProjectChangeRequest() {
  return useMutation({
    mutationFn: ({ id, decisionNotes }: { id: string; decisionNotes: string }) =>
      api.post<void>(`/api/project-change-requests/${id}/reject`, { decisionNotes }),
  });
}