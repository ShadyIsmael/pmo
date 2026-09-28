"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";
import { allocationKeys } from "@/lib/api/resources";

export interface AllocationDto {
  id: string;
  resourceId: string;
  resourceName: string;
  resourceJobTitle?: string;
  resourceType?: string;
  projectId?: string;
  projectName?: string;
  workItemId?: string;
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

type AllocationPage = PagedResult<AllocationDto>;

export function useAllocations(params: {
  page?: number;
  pageSize?: number;
  resourceId?: string;
  projectId?: string;
  status?: string;
} = {}, enabled = true) {
  return useQuery({
    queryKey: allocationKeys.list(params),
    queryFn: () => api.get<AllocationPage>(endpoints.allocations.root, params),
    staleTime: 30_000,
    enabled,
  });
}

export function useAllocation(id: string) {
  return useQuery({
    queryKey: allocationKeys.detail(id),
    queryFn: () => api.get<AllocationDto>(endpoints.allocations.detail(id)),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useAllocationsByResource(resourceId: string, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: allocationKeys.byResource(resourceId, page, pageSize),
    queryFn: () => api.get<AllocationPage>(endpoints.allocations.byResource(resourceId), { page, pageSize }),
    enabled: !!resourceId,
    staleTime: 30_000,
  });
}

export function useAllocationsByProject(projectId: string, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: allocationKeys.byProject(projectId, page, pageSize),
    queryFn: () => api.get<AllocationPage>(endpoints.allocations.byProject(projectId), { page, pageSize }),
    enabled: !!projectId,
    staleTime: 30_000,
  });
}

export function useAllocationConflicts(resourceId: string, startDate: string, endDate: string, excludeAllocationId?: string) {
  return useQuery({
    queryKey: allocationKeys.conflicts(resourceId, startDate, endDate, excludeAllocationId),
    queryFn: () => api.get<AllocationConflictDto[]>(endpoints.allocations.conflicts, { resourceId, startDate, endDate, excludeAllocationId }),
    enabled: !!resourceId && !!startDate && !!endDate,
    staleTime: 10_000,
  });
}

export function useCreateAllocation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: CreateAllocationDto) => api.post<AllocationDto>(endpoints.allocations.root, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: allocationKeys.all });
    },
  });
}

export function useUpdateAllocation(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: UpdateAllocationDto) => api.put<AllocationDto>(endpoints.allocations.detail(id), data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: allocationKeys.all });
    },
  });
}

export function useDeleteAllocation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete<void>(endpoints.allocations.detail(id));
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: allocationKeys.all });
    },
  });
}
