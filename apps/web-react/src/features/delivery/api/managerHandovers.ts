import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { api } from "@api/client";

export type ManagerHandoverScope = "programs" | "portfolios";

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
  notes?: string;
  status: string;
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
  respondedBy?: string;
  respondedByName?: string;
  respondedAt?: string;
  rejectionReason?: string;
  snapshotId?: string;
}

export interface ManagerHandoverManagerDto {
  id: string;
  fullName: string;
  email: string;
  jobTitle?: string;
  managerTypes: string[];
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

export const managerHandoverKeys = {
  root: ["managerHandover"] as const,
  managers: (scope: ManagerHandoverScope, id: string, cacheScope: string) => ["managerHandover", "eligibleManagers", cacheScope, scope, id] as const,
  requests: (scope: ManagerHandoverScope, id: string, cacheScope: string) => ["managerHandover", "requests", cacheScope, scope, id] as const,
  history: (scope: ManagerHandoverScope, id: string, cacheScope: string) => ["managerHandover", "history", cacheScope, scope, id] as const,
  snapshots: (scope: ManagerHandoverScope, id: string, cacheScope: string) => ["managerHandover", "snapshots", cacheScope, scope, id] as const,
};

export function invalidateManagerHandoverEntity(queryClient: QueryClient, request: Pick<ManagerHandoverRequestDto, "entityType" | "entityId">) {
  const managementKey = request.entityType === "Program" ? "program-management" : "portfolio-management";
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: managerHandoverKeys.root }),
    queryClient.invalidateQueries({ queryKey: ["managerHandovers"] }),
    queryClient.invalidateQueries({ queryKey: [managementKey] }),
    queryClient.invalidateQueries({ queryKey: ["program-management"] }),
    queryClient.invalidateQueries({ queryKey: ["portfolio-management"] }),
    queryClient.invalidateQueries({ queryKey: ["programs"] }),
    queryClient.invalidateQueries({ queryKey: ["portfolios"] }),
  ]);
}

export function useEligibleManagerHandoverManagers(scope: ManagerHandoverScope, entityId: string, enabled = true, cacheScope = "") {
  return useQuery({
    queryKey: managerHandoverKeys.managers(scope, entityId, cacheScope),
    queryFn: () => api.get<ManagerHandoverManagerDto[]>(`/api/manager-handovers/${scope}/${entityId}/eligible-managers`),
    enabled: Boolean(entityId) && enabled,
    staleTime: 30_000,
  });
}

export function useManagerHandoverRequests(scope: ManagerHandoverScope, entityId: string, enabled = true, cacheScope = "") {
  return useQuery({
    queryKey: managerHandoverKeys.requests(scope, entityId, cacheScope),
    queryFn: () => api.get<ManagerHandoverRequestDto[]>(`/api/manager-handovers/${scope}/${entityId}/requests`),
    enabled: Boolean(entityId) && enabled,
    staleTime: 15_000,
  });
}

export function useManagerAssignmentHistory(scope: ManagerHandoverScope, entityId: string, enabled = true, cacheScope = "") {
  return useQuery({
    queryKey: managerHandoverKeys.history(scope, entityId, cacheScope),
    queryFn: () => api.get<ManagerAssignmentDto[]>(`/api/manager-handovers/${scope}/${entityId}/history`),
    enabled: Boolean(entityId) && enabled,
    staleTime: 30_000,
  });
}

export function useManagerHandoverSnapshots(scope: ManagerHandoverScope, entityId: string, enabled = true, cacheScope = "") {
  return useQuery({
    queryKey: managerHandoverKeys.snapshots(scope, entityId, cacheScope),
    queryFn: () => api.get<ManagerSnapshotDto[]>(`/api/manager-handovers/${scope}/${entityId}/snapshots`),
    enabled: Boolean(entityId) && enabled,
    staleTime: 30_000,
  });
}

export function useCreateManagerHandover(scope: ManagerHandoverScope, entityId: string, cacheScope = "") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (dto: { newManagerId: string; effectiveHandoverDate: string; reason: string; notes?: string }) =>
      api.post<ManagerHandoverRequestDto>(`/api/manager-handovers/${scope}/${entityId}/requests`, dto),
    onSuccess: async (request) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: managerHandoverKeys.requests(scope, entityId, cacheScope) }),
        invalidateManagerHandoverEntity(queryClient, request),
      ]);
    },
  });
}

export function useCancelManagerHandover(scope: ManagerHandoverScope, entityId: string, cacheScope = "") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => api.post<ManagerHandoverRequestDto>(`/api/manager-handovers/${scope}/requests/${requestId}/cancel`, {}),
    onSuccess: async (request) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: managerHandoverKeys.requests(scope, entityId, cacheScope) }),
        invalidateManagerHandoverEntity(queryClient, request),
      ]);
    },
  });
}
