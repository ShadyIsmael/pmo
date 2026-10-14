import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { projectKeys } from "@features/projects/api/projects";
import { invalidateManagerHandoverEntity } from "@features/delivery/api/managerHandovers";
import type { ManagerHandoverRequestDto } from "@features/delivery/api/managerHandovers";
export type { ManagerHandoverRequestDto } from "@features/delivery/api/managerHandovers";

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

export function useTimeEntries(params: TimeEntryParams = {}, enabled = true) {
  return useQuery({
    queryKey: timeEntryKeys.list(params),
    queryFn: () => api.get<{ items: TimeEntryDto[]; totalCount: number; page: number; pageSize: number; totalPages: number }>("/api/time-entries", params),
    enabled,
    staleTime: 30_000,
  });
}

export function useTimeEntrySummary(params: { from: string; to: string }, enabled = true, staleTime = 30_000) {
  return useQuery({
    queryKey: timeEntryKeys.summary(params),
    queryFn: () => api.get<TimeEntrySummary>("/api/time-entries/summary", params),
    enabled,
    staleTime,
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

export interface ProjectChangeRequestDto {
  id: string;
  projectId: string;
  projectName: string;
  number: number;
  type: "Administrative" | "Baseline";
  status: "Pending" | "Approved" | "Rejected" | "Cancelled";
  reason: string;
  justification?: string | null;
  impact?: string | null;
  budgetBeforeChange?: number | null;
  budgetChangeAmount?: number | null;
  proposedProjectBudget?: number | null;
  attachments?: string | null;
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
  reviewedBy?: string | null;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  decisionNotes?: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export function usePendingHandoverRequestsForMe(enabled = true) {
  return useQuery({
    queryKey: ["handovers", "pendingForMe"],
    queryFn: () => api.get<HandoverRequestDto[]>("/api/handover-requests/pending-for-me"),
    enabled,
    staleTime: 15_000,
  });
}

export function usePendingManagerHandovers(enabled = true) {
  return useQuery({
    queryKey: ["managerHandovers", "pendingForMe"],
    queryFn: () => api.get<ManagerHandoverRequestDto[]>("/api/manager-handovers/pending-for-me"),
    enabled,
    staleTime: 15_000,
  });
}

export function usePendingProjectChangeRequests(enabled = true) {
  return useQuery({
    queryKey: ["projectChangeRequests", "pendingForMe"],
    queryFn: () => api.get<ProjectChangeRequestDto[]>("/api/project-change-requests/pending-for-me"),
    enabled,
    staleTime: 15_000,
  });
}

export function useAcceptHandoverRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => api.post<HandoverRequestDto>(`/api/handover-requests/${requestId}/accept`, {}),
    onSuccess: (request) => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["projects", "detail", request.projectId], exact: true }),
      queryClient.invalidateQueries({ queryKey: ["projects", "shell", request.projectId], exact: true }),
      queryClient.invalidateQueries({ queryKey: ["projects", "overview", request.projectId], exact: true }),
      queryClient.invalidateQueries({ queryKey: ["projects", "list"] }),
      queryClient.invalidateQueries({ queryKey: ["projects", "people", request.projectId] }),
      queryClient.invalidateQueries({ queryKey: ["projects", "team", request.projectId] }),
      queryClient.invalidateQueries({ queryKey: ["projects", "history", request.projectId] }),
      queryClient.invalidateQueries({ queryKey: ["projects", "snapshots", request.projectId] }),
      queryClient.invalidateQueries({ queryKey: ["projects", "handovers", request.projectId] }),
      queryClient.invalidateQueries({ queryKey: projectKeys.handoverManagers(request.projectId) }),
    ]),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["handovers", "pendingForMe"] }),
  });
}

export function useRejectHandoverRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ requestId, rejectionReason }: { requestId: string; rejectionReason: string }) =>
      api.post<HandoverRequestDto>(`/api/handover-requests/${requestId}/reject`, { rejectionReason }),
    onSuccess: (request) => queryClient.invalidateQueries({ queryKey: ["projects", "handovers", request.projectId] }),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["handovers", "pendingForMe"] }),
  });
}

export function useAcceptManagerHandover(type: "programs" | "portfolios") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => api.post<ManagerHandoverRequestDto>(`/api/manager-handovers/${type}/requests/${requestId}/accept`, {}),
    onSuccess: (request) => invalidateManagerHandoverEntity(queryClient, request),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["managerHandovers"] }),
  });
}

export function useRejectManagerHandover(type: "programs" | "portfolios") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ requestId, rejectionReason }: { requestId: string; rejectionReason: string }) =>
      api.post<ManagerHandoverRequestDto>(`/api/manager-handovers/${type}/requests/${requestId}/reject`, { rejectionReason }),
    onSuccess: (request) => invalidateManagerHandoverEntity(queryClient, request),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["managerHandovers"] }),
  });
}

export function useApproveProjectChangeRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: string; dto: Record<string, unknown> }) =>
      api.post<ProjectChangeRequestDto>(`/api/project-change-requests/${id}/approve`, dto),
    onSuccess: (request) => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["projects", "change-requests", request.projectId] }),
      queryClient.invalidateQueries({ queryKey: ["projects", "detail", request.projectId], exact: true }),
      queryClient.invalidateQueries({ queryKey: ["projects", "overview", request.projectId], exact: true }),
    ]),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["projectChangeRequests"] }),
  });
}

export function useRejectProjectChangeRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, decisionNotes }: { id: string; decisionNotes: string }) =>
      api.post<ProjectChangeRequestDto>(`/api/project-change-requests/${id}/reject`, { decisionNotes }),
    onSuccess: (request) => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["projects", "change-requests", request.projectId] }),
      queryClient.invalidateQueries({ queryKey: ["projects", "detail", request.projectId], exact: true }),
      queryClient.invalidateQueries({ queryKey: ["projects", "overview", request.projectId], exact: true }),
    ]),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["projectChangeRequests"] }),
  });
}
