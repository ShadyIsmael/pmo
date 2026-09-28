"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";
import { resourceKeys } from "@/lib/api/resources";
import type { CreateResourceInput, UpdateResourceInput } from "@/lib/resources/resource-form";

export interface ResourceBaseCalendarDto {
  id: string;
  name: string;
  isDefault: boolean;
}

export interface ResourceDto {
  id: string;
  userId?: string | null;
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
  businessUnitName?: string;
  teamName?: string;
  locationId?: string;
  locationName?: string;
  vendorId?: string;
  vendorName?: string;
  baseCalendarId?: string;
  baseCalendarName?: string;
  timezone?: string;
  utilizationTarget?: number;
  currentUtilization?: number;
  notes?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
  skills: ResourceSkillDto[];
  allocations: ResourceAllocationDto[];
  costProfiles: ResourceCostProfileDto[];
}

export interface ResourceSkillDto {
  id: string;
  skillId: string;
  skillName: string;
  skillCategory?: string;
  proficiency: string;
  yearsOfExperience: number;
}

export interface ResourceAllocationDto {
  id: string;
  projectId?: string;
  projectName?: string;
  role: string;
  allocationPercentage: number;
  plannedHours?: number;
  actualHours?: number;
  startDate: string;
  endDate: string;
  status?: string;
}

export interface ResourceFilterParams {
  search?: string;
  status?: string;
  type?: string;
  userId?: string;
  departmentId?: string;
  locationId?: string;
  vendorId?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: string;
}

export interface ResourceCostProfileDto {
  id: string;
  resourceId: string;
  effectiveFrom: string;
  effectiveTo?: string;
  compensationType: string;
  amount: number;
  currency: string;
  overtimeEligible: boolean;
  overtimeRateType?: string;
  overtimeRateValue?: number;
  createdAt: string;
  updatedAt?: string;
  rowVersion: number[];
}

export interface ProjectRoleDto {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  description?: string;
  displayOrder: number;
  isActive: boolean;
  color?: string;
}

function toParams(filter: ResourceFilterParams): Record<string, string | number | boolean | undefined> {
  return {
    search: filter.search,
    status: filter.status,
    type: filter.type,
    userId: filter.userId,
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
  return api.get<PagedResult<ResourceDto>>(endpoints.resources.root, toParams(filter));
}

async function fetchResource(id: string): Promise<ResourceDto> {
  return api.get<ResourceDto>(endpoints.resources.detail(id));
}

export function useResources(filter: ResourceFilterParams = {}, enabled = true) {
  return useQuery({
    queryKey: resourceKeys.list(filter),
    queryFn: () => fetchResources(filter),
    staleTime: 30_000,
    enabled,
  });
}

export function useBaseCalendars(enabled = true) {
  return useQuery({
    queryKey: resourceKeys.baseCalendars,
    queryFn: () => api.get<ResourceBaseCalendarDto[]>(endpoints.resources.baseCalendars),
    staleTime: 60_000,
    enabled,
  });
}

export function useResource(id: string) {
  return useQuery({
    queryKey: resourceKeys.detail(id),
    queryFn: () => fetchResource(id),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useCreateResource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: CreateResourceInput | Record<string, unknown>) =>
      api.post<ResourceDto>(endpoints.resources.root, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: resourceKeys.all });
    },
  });
}

export function useUpdateResource(defaultId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: { id?: string; data: UpdateResourceInput | Record<string, unknown> }) => api.put<ResourceDto>(endpoints.resources.detail(input.id ?? defaultId ?? ""), input.data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: resourceKeys.all });
    },
  });
}

export function useDeleteResource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete<void>(endpoints.resources.detail(id));
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: resourceKeys.all });
    },
  });
}

export function useCostProfiles(resourceId: string, enabled = true) {
  return useQuery({
    queryKey: [...resourceKeys.all, "cost-profiles", resourceId],
    queryFn: () => api.get<ResourceCostProfileDto[]>(`${endpoints.resources.detail(resourceId)}/cost-profiles`),
    enabled: !!resourceId && enabled,
    staleTime: 30_000,
  });
}

export function useCreateCostProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { resourceId: string; body: Record<string, unknown> }) =>
      api.post<ResourceCostProfileDto>(`${endpoints.resources.detail(data.resourceId)}/cost-profiles`, data.body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: resourceKeys.all });
    },
  });
}

export function useUpdateCostProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { resourceId: string; profileId: string; body: Record<string, unknown> }) =>
      api.put<ResourceCostProfileDto>(`${endpoints.resources.detail(data.resourceId)}/cost-profiles/${data.profileId}`, data.body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: resourceKeys.all });
    },
  });
}

export function useDeleteCostProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { resourceId: string; profileId: string }) =>
      api.delete<void>(`${endpoints.resources.detail(data.resourceId)}/cost-profiles/${data.profileId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: resourceKeys.all });
    },
  });
}

export function useProjectRoles(activeOnly = true) {
  return useQuery({
    queryKey: [...resourceKeys.all, "project-roles", activeOnly],
    queryFn: () => api.get<ProjectRoleDto[]>("/api/project-roles", { activeOnly }),
    staleTime: 60_000,
  });
}

export function useCreateProjectRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: Record<string, unknown>) => api.post<ProjectRoleDto>("/api/project-roles", data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...resourceKeys.all, "project-roles"] });
    },
  });
}

export function useUpdateProjectRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { id: string; body: Record<string, unknown> }) =>
      api.put<ProjectRoleDto>(`/api/project-roles/${data.id}`, data.body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...resourceKeys.all, "project-roles"] });
    },
  });
}

export function useDeactivateProjectRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.put<void>(`/api/project-roles/${id}/deactivate`, {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...resourceKeys.all, "project-roles"] });
    },
  });
}
