import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import type { PagedResult } from "./resources";

export interface AllocationDto {
  id: string;
  resourceId: string;
  resourceName: string;
  resourceJobTitle?: string;
  resourceType?: string;
  projectId?: string;
  projectName?: string;
  workItemId?: string;
  projectRoleId?: string;
  role: string;
  allocationPercentage: number;
  hoursPerWeek?: number;
  plannedHours?: number;
  actualHours?: number;
  startDate: string;
  endDate: string;
  status: string;
  isBillable: boolean;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateAllocationDto {
  resourceId: string;
  projectId?: string;
  projectName?: string;
  workItemId?: string;
  projectRoleId?: string;
  role: string;
  allocationPercentage: number;
  hoursPerWeek?: number;
  startDate: string;
  endDate: string;
  status?: string;
  isBillable?: boolean;
  notes?: string;
}

export interface UpdateAllocationDto {
  projectId?: string;
  projectName?: string;
  workItemId?: string;
  projectRoleId?: string;
  role?: string;
  allocationPercentage?: number;
  hoursPerWeek?: number;
  startDate?: string;
  endDate?: string;
  status?: string;
  isBillable?: boolean;
  notes?: string;
}

export interface AllocationConflictDto {
  existingAllocationId: string;
  resourceName: string;
  existingProjectName: string;
  existingRole: string;
  existingPercentage: number;
  newTotalPercentage: number;
  existingStart: string;
  existingEnd: string;
  severity: string;
  description: string;
}

export const allocationKeys = {
  all: ["allocations"] as const,
  list: (params: unknown = {}) => [...allocationKeys.all, "list", params] as const,
  detail: (id: string) => [...allocationKeys.all, "detail", id] as const,
  byResource: (resourceId: string, page: number, pageSize: number) => [...allocationKeys.all, "by-resource", resourceId, page, pageSize] as const,
  byProject: (projectId: string, page: number, pageSize: number) => [...allocationKeys.all, "by-project", projectId, page, pageSize] as const,
  conflicts: (resourceId: string, startDate: string, endDate: string, excludeAllocationId?: string) => [...allocationKeys.all, "conflicts", resourceId, startDate, endDate, excludeAllocationId] as const,
};

type AllocationPage = PagedResult<AllocationDto>;

export function useAllocations(params: { page?: number; pageSize?: number; resourceId?: string; projectId?: string; status?: string } = {}, enabled = true) {
  return useQuery({
    queryKey: allocationKeys.list(params),
    queryFn: () => api.get<AllocationPage>(endpoints.allocations.root, params),
    staleTime: 30_000,
    enabled,
  });
}

export function useAllocation(id: string, enabled = true) {
  return useQuery({
    queryKey: allocationKeys.detail(id),
    queryFn: () => api.get<AllocationDto>(endpoints.allocations.detail(id)),
    enabled: Boolean(id) && enabled,
    staleTime: 30_000,
  });
}

export function useAllocationsByResource(resourceId: string, page = 1, pageSize = 50, enabled = true) {
  return useQuery({
    queryKey: allocationKeys.byResource(resourceId, page, pageSize),
    queryFn: () => api.get<AllocationPage>(endpoints.allocations.byResource(resourceId), { page, pageSize }),
    enabled: Boolean(resourceId) && enabled,
    staleTime: 30_000,
  });
}

export function useAllocationsByProject(projectId: string, page = 1, pageSize = 50, enabled = true) {
  return useQuery({
    queryKey: allocationKeys.byProject(projectId, page, pageSize),
    queryFn: () => api.get<AllocationPage>(endpoints.allocations.byProject(projectId), { page, pageSize }),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useAllocationConflicts(resourceId: string, startDate: string, endDate: string, excludeAllocationId?: string, enabled = true) {
  return useQuery({
    queryKey: allocationKeys.conflicts(resourceId, startDate, endDate, excludeAllocationId),
    queryFn: () => api.get<AllocationDto[]>(endpoints.allocations.conflicts, { resourceId, startDate, endDate, excludeAllocationId }),
    enabled: enabled && Boolean(resourceId && startDate && endDate),
    staleTime: 10_000,
  });
}

function invalidateAllocations(queryClient: ReturnType<typeof useQueryClient>) {
  return queryClient.invalidateQueries({ queryKey: allocationKeys.all });
}

export function useCreateAllocation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateAllocationDto) => api.post<AllocationDto>(endpoints.allocations.root, data),
    onSuccess: () => invalidateAllocations(queryClient),
  });
}

export function useUpdateAllocation(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateAllocationDto) => api.put<AllocationDto>(endpoints.allocations.detail(id), data),
    onSuccess: () => invalidateAllocations(queryClient),
  });
}

export function useDeleteAllocation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.allocations.detail(id)),
    onSuccess: () => invalidateAllocations(queryClient),
  });
}
