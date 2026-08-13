"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api-client";

export interface ResourceDto {
  id: string;
  userId: string;
  name: string;
  employeeId?: string;
  jobTitle?: string;
  email?: string;
  phone?: string;
  avatarUrl?: string;
  type: string;
  status: string;
  costPerHour?: number;
  billableRate?: number;
  currency?: string;
  employmentType?: string;
  seniority?: string;
  dateOfJoining?: string;
  departmentId?: string;
  departmentName?: string;
  locationId?: string;
  locationName?: string;
  vendorId?: string;
  vendorName?: string;
  timezone?: string;
  utilizationTarget?: number;
  currentUtilization?: number;
  notes?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
  skills: ResourceSkillDto[];
}

export interface ResourceSkillDto {
  id: string;
  skillId: string;
  skillName: string;
  skillCategory?: string;
  proficiency: string;
  yearsOfExperience: number;
}

export interface ResourceFilterParams {
  search?: string;
  status?: string;
  type?: string;
  departmentId?: string;
  locationId?: string;
  vendorId?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: string;
}

function toParams(filter: ResourceFilterParams): Record<string, string | number | boolean | undefined> {
  return {
    search: filter.search,
    status: filter.status,
    type: filter.type,
    departmentId: filter.departmentId,
    locationId: filter.locationId,
    vendorId: filter.vendorId,
    isActive: filter.isActive,
    page: filter.page,
    pageSize: filter.pageSize,
    sortBy: filter.sortBy,
    sortDirection: filter.sortDirection,
  };
}

async function fetchResources(filter: ResourceFilterParams): Promise<PagedResult<ResourceDto>> {
  return api.get<PagedResult<ResourceDto>>("/api/resources", toParams(filter));
}

async function fetchResource(id: string): Promise<ResourceDto> {
  return api.get<ResourceDto>(`/api/resources/${id}`);
}

export function useResources(filter: ResourceFilterParams = {}) {
  return useQuery({
    queryKey: ["resources", filter],
    queryFn: () => fetchResources(filter),
    staleTime: 30_000,
  });
}

export function useResource(id: string) {
  return useQuery({
    queryKey: ["resource", id],
    queryFn: () => fetchResource(id),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useCreateResource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: Record<string, unknown>) => api.post<ResourceDto>("/api/resources", data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["resources"] });
    },
  });
}

export function useUpdateResource(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: Record<string, unknown>) => api.put<ResourceDto>(`/api/resources/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["resources"] });
      queryClient.invalidateQueries({ queryKey: ["resource", id] });
    },
  });
}

export function useDeleteResource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete<void>(`/api/resources/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["resources"] });
    },
  });
}
