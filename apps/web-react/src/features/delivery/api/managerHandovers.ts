import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { api } from "@api/client";

export type ManagerHandoverScope = "programs" | "portfolios";
export type ManagerHandoverInitiationType = "ManagerInitiated" | "AdminInitiated" | "InitialAssignment";

export interface ManagerHandoverRequestDto {
  id: string;
  entityType: "Program" | "Portfolio";
  entityId: string;
  entityName: string;
  currentManagerId: string | null;
  currentManagerName: string;
  newManagerId: string;
  newManagerName: string;
  effectiveHandoverDate: string;
  reason: string;
  notes?: string;
  status: string;
  initiationType: ManagerHandoverInitiationType;
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
  previousManagerId: string | null;
  previousManagerName: string;
  newManagerId: string;
  newManagerName: string;
  handoverReason: string;
  handoverNotes?: string;
  data?: string;
}

export interface ManagerHandoverCapabilitiesDto {
  currentManagerId: string | null;
  currentManagerName?: string | null;
  canViewRequests: boolean;
  canInitiate: boolean;
  canAdminInitiate: boolean;
  canAssignInitial: boolean;
  canApproveIncoming: boolean;
  canCancelOwnPending: boolean;
  hasPendingRequest: boolean;
}

export const managerHandoverKeys = {
  root: ["managerHandover"] as const,
  managers: (scope: ManagerHandoverScope, id: string, cacheScope: string) => ["managerHandover", "eligibleManagers", cacheScope, scope, id] as const,
  capabilities: (scope: ManagerHandoverScope, id: string, cacheScope: string) => ["managerHandover", "capabilities", cacheScope, scope, id] as const,
  requests: (scope: ManagerHandoverScope, id: string, cacheScope: string) => ["managerHandover", "requests", cacheScope, scope, id] as const,
  selfPending: (scope: ManagerHandoverScope, id: string, cacheScope: string) => ["managerHandover", "selfPending", cacheScope, scope, id] as const,
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

export function useManagerHandoverCapabilities(scope: ManagerHandoverScope, entityId: string, enabled = true, cacheScope = "") {
  return useQuery({
    queryKey: managerHandoverKeys.capabilities(scope, entityId, cacheScope),
    queryFn: () => api.get<ManagerHandoverCapabilitiesDto>(`/api/manager-handovers/${scope}/${entityId}/capabilities`),
    enabled: Boolean(entityId) && enabled,
    staleTime: 10_000,
  });
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

export function useSelfPendingManagerHandover(scope: ManagerHandoverScope, entityId: string, enabled = true, cacheScope = "") {
  return useQuery({
    queryKey: managerHandoverKeys.selfPending(scope, entityId, cacheScope),
    queryFn: () => api.get<ManagerHandoverRequestDto[]>(`/api/manager-handovers/${scope}/${entityId}/pending-for-me`),
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
    mutationFn: ({ initiationType, ...dto }: { initiationType: ManagerHandoverInitiationType; newManagerId: string; effectiveHandoverDate: string; reason: string; notes?: string }) => {
      const route = initiationType === "ManagerInitiated" ? "requests" : initiationType === "AdminInitiated" ? "admin-requests" : "initial-assignment-requests";
      return api.post<ManagerHandoverRequestDto>(`/api/manager-handovers/${scope}/${entityId}/${route}`, dto);
    },
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
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["managerHandovers"] }),
  });
}

export function useAcceptManagerHandover(scope: ManagerHandoverScope, entityId: string, cacheScope = "") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => api.post<ManagerHandoverRequestDto>(`/api/manager-handovers/${scope}/requests/${requestId}/accept`, {}),
    onSuccess: async (request) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: managerHandoverKeys.requests(scope, entityId, cacheScope) }),
        invalidateManagerHandoverEntity(queryClient, request),
      ]);
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["managerHandovers"] }),
  });
}

export function useRejectManagerHandover(scope: ManagerHandoverScope, entityId: string, cacheScope = "") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ requestId, rejectionReason }: { requestId: string; rejectionReason: string }) =>
      api.post<ManagerHandoverRequestDto>(`/api/manager-handovers/${scope}/requests/${requestId}/reject`, { rejectionReason }),
    onSuccess: async (request) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: managerHandoverKeys.requests(scope, entityId, cacheScope) }),
        invalidateManagerHandoverEntity(queryClient, request),
      ]);
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["managerHandovers"] }),
  });
}
