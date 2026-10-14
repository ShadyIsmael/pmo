import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";

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
  primaryOrganizationalUnitId?: string;
  primaryOrganizationalUnitName?: string;
  primaryOrganizationalUnitCode?: string;
  primaryManagerResourceId?: string;
  primaryManagerResourceName?: string;
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
  organizationalUnitId?: string;
  locationId?: string;
  vendorId?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: string;
  [key: string]: string | number | boolean | undefined;
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

export interface PagedResult<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ResourceFinderQuery {
  role?: string;
  skillIds?: string[];
  seniority?: string;
  startDate?: string;
  endDate?: string;
  requiredAllocation?: number;
  departmentId?: string;
  locationId?: string;
  vendorId?: string;
}

export interface ResourceFinderItem {
  resourceId: string;
  resourceName: string;
  jobTitle?: string;
  seniority?: string;
  departmentName?: string;
  locationName?: string;
  email?: string;
  matchScore: number;
  availableCapacityHours: number;
  currentLoadPercent: number;
  earliestAvailableDate?: string;
  conflictingAllocations: Array<{ allocationId: string; projectName: string; role: string; allocationPercentage: number; startDate: string; endDate: string }>;
  skillMatches: Array<{ skillName: string; proficiency: string; isMatch: boolean }>;
  resourceType: string;
}

export type ResourceFinderResult = PagedResult<ResourceFinderItem>;
export interface ResourceSkillLookup { id: string; name: string; category?: string; isActive: boolean }

const resourceKeysAll = ["resources"] as const;
const resourceKeysLists = () => [...resourceKeysAll, "list"] as const;

const resourceKeys = {
  all: resourceKeysAll,
  lists: resourceKeysLists,
  list: (params: ResourceFilterParams) => [...resourceKeysLists(), params] as const,
  detail: (id: string) => [...resourceKeysAll, "detail", id] as const,
  baseCalendars: [...resourceKeysAll, "baseCalendars"] as const,
};

export function useResources(filter: ResourceFilterParams = {}, enabled = true) {
  return useQuery({
    queryKey: resourceKeys.list(filter),
    queryFn: () => api.get<PagedResult<ResourceDto>>("/api/resources", filter),
    staleTime: 30_000,
    enabled,
  });
}

export function useResourceFinder(query: ResourceFinderQuery, enabled = true) {
  return useQuery({
    queryKey: [...resourceKeys.all, "finder", query],
    queryFn: () => api.post<ResourceFinderResult>("/api/resource-finder/find", query),
    enabled,
    staleTime: 15_000,
  });
}

export function useResourceSkills(enabled = true) {
  return useQuery({
    queryKey: [...resourceKeys.all, "skills"],
    queryFn: async () => (await api.get<PagedResult<ResourceSkillLookup>>("/api/skills", { pageSize: 200 })).items ?? [],
    enabled,
    staleTime: 60_000,
  });
}

export function useBaseCalendars(enabled = true) {
  return useQuery({
    queryKey: resourceKeys.baseCalendars,
    queryFn: () => api.get<ResourceBaseCalendarDto[]>("/api/resources/base-calendars"),
    staleTime: 60_000,
    enabled,
  });
}

export function useResource(id: string) {
  return useQuery({
    queryKey: resourceKeys.detail(id),
    queryFn: () => api.get<ResourceDto>(`/api/resources/${id}`),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useCreateResource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: Record<string, unknown>) => api.post<ResourceDto>("/api/resources", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: resourceKeys.all }),
  });
}

export function useUpdateResource(defaultId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: { id?: string; data: Record<string, unknown> }) => api.put<ResourceDto>(`/api/resources/${input.id ?? defaultId ?? ""}`, input.data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: resourceKeys.all }),
  });
}

export function useDeleteResource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.delete<void>(`/api/resources/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: resourceKeys.all }),
  });
}

export function useCostProfiles(resourceId: string, enabled = true) {
  return useQuery({
    queryKey: [...resourceKeys.all, "cost-profiles", resourceId],
    queryFn: () => api.get<ResourceCostProfileDto[]>(`/api/resources/${resourceId}/cost-profiles`),
    enabled: !!resourceId && enabled,
    staleTime: 30_000,
  });
}

export function useProjectRoles(activeOnly = true, enabled = true) {
  return useQuery({
    queryKey: [...resourceKeys.all, "project-roles", activeOnly],
    queryFn: () => api.get<ProjectRoleDto[]>("/api/project-roles", { activeOnly }),
    enabled,
    staleTime: 60_000,
  });
}

export function useProjectRoleNextCode(enabled = true, requestKey: number | string = 0) {
  return useQuery({
    queryKey: [...resourceKeys.all, "project-role-next-code", requestKey],
    queryFn: () => api.get<{ code: string }>("/api/project-roles/next-code"),
    enabled,
    staleTime: 0,
  });
}

export function useCreateProjectRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: Record<string, unknown>) => api.post<ProjectRoleDto>("/api/project-roles", data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...resourceKeys.all, "project-roles"] }),
  });
}

export function useUpdateProjectRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { id: string; body: Record<string, unknown> }) => api.put<ProjectRoleDto>(`/api/project-roles/${data.id}`, data.body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...resourceKeys.all, "project-roles"] }),
  });
}

export function useDeactivateProjectRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.put<void>(`/api/project-roles/${id}/deactivate`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...resourceKeys.all, "project-roles"] }),
  });
}
