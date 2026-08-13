"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api-client";

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
} = {}) {
  return useQuery({
    queryKey: ["allocations", params],
    queryFn: () => api.get<AllocationPage>("/api/allocations", params),
    staleTime: 30_000,
  });
}

export function useAllocation(id: string) {
  return useQuery({
    queryKey: ["allocation", id],
    queryFn: () => api.get<AllocationDto>(`/api/allocations/${id}`),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useAllocationsByResource(resourceId: string, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: ["allocations", "by-resource", resourceId, page, pageSize],
    queryFn: () => api.get<AllocationPage>("/api/allocations/by-resource/" + resourceId, { page, pageSize }),
    enabled: !!resourceId,
    staleTime: 30_000,
  });
}

export function useAllocationsByProject(projectId: string, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: ["allocations", "by-project", projectId, page, pageSize],
    queryFn: () => api.get<AllocationPage>("/api/allocations/by-project/" + projectId, { page, pageSize }),
    enabled: !!projectId,
    staleTime: 30_000,
  });
}

export function useAllocationConflicts(resourceId: string, startDate: string, endDate: string, excludeAllocationId?: string) {
  return useQuery({
    queryKey: ["allocations", "conflicts", resourceId, startDate, endDate, excludeAllocationId],
    queryFn: () => api.get<AllocationConflictDto[]>("/api/allocations/conflicts", { resourceId, startDate, endDate, excludeAllocationId }),
    enabled: !!resourceId && !!startDate && !!endDate,
    staleTime: 10_000,
  });
}

export function useCreateAllocation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: CreateAllocationDto) => api.post<AllocationDto>("/api/allocations", data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["allocations"] });
    },
  });
}

export function useUpdateAllocation(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: UpdateAllocationDto) => api.put<AllocationDto>(`/api/allocations/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["allocations"] });
      queryClient.invalidateQueries({ queryKey: ["allocation", id] });
    },
  });
}

export function useDeleteAllocation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete<void>(`/api/allocations/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["allocations"] });
    },
  });
}
