"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";
import { useAuth } from "@/lib/auth";

export type ManagerHandoverEntity = "programs" | "portfolios";

export interface ManagerHandoverRequestDto {
  id: string;
  entityType: string;
  entityId: string;
  entityName: string;
  currentManagerId: string;
  currentManagerName: string;
  newManagerId: string;
  newManagerName: string;
  effectiveHandoverDate: string;
  reason: string;
  notes?: string;
  status: "Pending" | "Accepted" | "Rejected" | "Cancelled" | string;
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
  respondedBy?: string;
  respondedByName?: string;
  respondedAt?: string;
  rejectionReason?: string;
  snapshotId?: string;
}

export interface ManagerAssignmentDto {
  id: string;
  entityId: string;
  managerId: string;
  managerName: string;
  startDate: string;
  endDate?: string;
  isCurrent: boolean;
  handoverDate?: string;
  handoverReason?: string;
  handoverNotes?: string;
}

export interface ManagerSnapshotDto {
  id: string;
  entityId: string;
  snapshotNumber: number;
  name: string;
  snapshotDate: string;
  effectiveEndDate: string;
  previousManagerId: string;
  previousManagerName: string;
  newManagerId: string;
  newManagerName: string;
  handoverReason: string;
  handoverNotes?: string;
  data?: string;
}

export interface CreateManagerHandoverRequestDto {
  newManagerId: string;
  effectiveHandoverDate: string;
  reason: string;
  notes?: string;
}

export const managerHandoverKeys = {
  all: ["manager-handovers"] as const,
  pending: (userId: string) => ["manager-handovers", "pending-for-me", userId] as const,
  byEntity: (type: ManagerHandoverEntity, id: string) => ["manager-handovers", type, id] as const,
  requests: (type: ManagerHandoverEntity, id: string) => [...managerHandoverKeys.byEntity(type, id), "requests"] as const,
  history: (type: ManagerHandoverEntity, id: string) => [...managerHandoverKeys.byEntity(type, id), "history"] as const,
  snapshots: (type: ManagerHandoverEntity, id: string) => [...managerHandoverKeys.byEntity(type, id), "snapshots"] as const,
};

export function useManagerHandoverRequests(type: ManagerHandoverEntity, id: string) {
  return useQuery({ queryKey: managerHandoverKeys.requests(type, id), queryFn: () => api.get<ManagerHandoverRequestDto[]>(endpoints.managerHandovers.requests(type, id)), enabled: !!id, staleTime: 15_000 });
}

export function useManagerHistory(type: ManagerHandoverEntity, id: string) {
  return useQuery({ queryKey: managerHandoverKeys.history(type, id), queryFn: () => api.get<ManagerAssignmentDto[]>(endpoints.managerHandovers.history(type, id)), enabled: !!id, staleTime: 30_000 });
}

export function useManagerSnapshots(type: ManagerHandoverEntity, id: string) {
  return useQuery({ queryKey: managerHandoverKeys.snapshots(type, id), queryFn: () => api.get<ManagerSnapshotDto[]>(endpoints.managerHandovers.snapshots(type, id)), enabled: !!id, staleTime: 30_000 });
}

export function usePendingManagerHandovers() {
  const { user } = useAuth();
  return useQuery({ queryKey: managerHandoverKeys.pending(user?.id ?? ""), queryFn: () => api.get<ManagerHandoverRequestDto[]>(endpoints.managerHandovers.pending), enabled: !!user?.id, staleTime: 15_000 });
}

function useHandoverMutation<TVariables>(mutationFn: (_variables: TVariables) => Promise<ManagerHandoverRequestDto>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn, onSuccess: (_data, _variables) => { queryClient.invalidateQueries({ queryKey: managerHandoverKeys.all }); } });
}

export function useCreateManagerHandover(type: ManagerHandoverEntity, id: string) {
  return useHandoverMutation((body: CreateManagerHandoverRequestDto) => api.post<ManagerHandoverRequestDto>(endpoints.managerHandovers.create(type, id), body));
}

export function useAcceptManagerHandover(type: ManagerHandoverEntity) {
  return useHandoverMutation((requestId: string) => api.post<ManagerHandoverRequestDto>(endpoints.managerHandovers.accept(type, requestId), {}));
}

export function useRejectManagerHandover(type: ManagerHandoverEntity) {
  return useHandoverMutation(({ requestId, rejectionReason }: { requestId: string; rejectionReason: string }) => api.post<ManagerHandoverRequestDto>(endpoints.managerHandovers.reject(type, requestId), { rejectionReason }));
}

export function useCancelManagerHandover(type: ManagerHandoverEntity) {
  return useHandoverMutation((requestId: string) => api.post<ManagerHandoverRequestDto>(endpoints.managerHandovers.cancel(type, requestId), {}));
}
