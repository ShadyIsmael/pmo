import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface ResourceTypeOptionDto {
  code: string;
  nameEn: string;
  nameAr: string;
  displayOrder: number;
  isDefault: boolean;
  requiresVendor: boolean;
}

export interface ResourceSeniorityOptionDto {
  code: string;
  nameEn: string;
  nameAr: string;
  displayOrder: number;
}

export interface ResourceReferenceDataDto {
  resourceTypes: ResourceTypeOptionDto[];
  seniorityLevels: ResourceSeniorityOptionDto[];
  compensationTypes: string[];
}

export interface ResourceBaseCalendarDto {
  id: string;
  name: string;
  isDefault: boolean;
  isConfigured: boolean;
  workingDaysPerWeek: number;
}

export interface ResourceVendorLookupDto {
  id: string;
  name: string;
  code?: string;
  isActive: boolean;
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
  hoursPerDay?: number | null;
  hoursPerWeek?: number | null;
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

export interface CreateResourceCostProfileInput {
  effectiveFrom: string;
  effectiveTo?: string;
  compensationType: string;
  amount: number;
  currency: string;
  overtimeEligible: false;
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
  hasCapacityData: boolean;
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
  costProfiles: (id: string) => [...resourceKeysAll, "cost-profiles", id] as const,
  baseCalendars: [...resourceKeysAll, "baseCalendars"] as const,
  referenceData: [...resourceKeysAll, "referenceData"] as const,
};

export function useResources(filter: ResourceFilterParams = {}, enabled = true) {
  return useQuery({
    queryKey: resourceKeys.list(filter),
    queryFn: () => api.get<PagedResult<ResourceDto>>(endpoints.resources.root, filter),
    staleTime: 30_000,
    enabled,
  });
}

export function useResourceFinder(query: ResourceFinderQuery, enabled = true) {
  return useQuery({
    queryKey: [...resourceKeys.all, "finder", query],
    queryFn: () => api.post<ResourceFinderResult>(endpoints.resourceFinder.find, query),
    enabled,
    staleTime: 15_000,
  });
}

export function useResourceSkills(enabled = true) {
  return useQuery({
    queryKey: [...resourceKeys.all, "skills"],
    queryFn: async () => (await api.get<PagedResult<ResourceSkillLookup>>(endpoints.skills.root, { pageSize: 200 })).items ?? [],
    enabled,
    staleTime: 60_000,
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

export function useResourceVendorLookup(enabled = true, resourceId?: string) {
  return useQuery({
    queryKey: [...resourceKeys.all, "vendor-options", resourceId],
    queryFn: async () => ({
      items: await api.get<ResourceVendorLookupDto[]>(endpoints.vendors.resourceLookup, resourceId ? { resourceId } : undefined),
    }),
    enabled,
    staleTime: 0,
    refetchOnMount: "always",
  });
}

export function useResourceReferenceData(enabled = true) {
  return useQuery({
    queryKey: resourceKeys.referenceData,
    queryFn: () => api.get<ResourceReferenceDataDto>(endpoints.resources.referenceData),
    enabled,
    staleTime: 300_000,
  });
}

export function useResource(id: string) {
  return useQuery({
    queryKey: resourceKeys.detail(id),
    queryFn: () => api.get<ResourceDto>(endpoints.resources.detail(id)),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useCreateResource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: Record<string, unknown>) => api.post<ResourceDto>(endpoints.resources.root, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: resourceKeys.all }),
  });
}

export function useUpdateResource(defaultId?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: { id?: string; data: Record<string, unknown> }) => api.put<ResourceDto>(endpoints.resources.detail(input.id ?? defaultId ?? ""), input.data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: resourceKeys.all }),
  });
}

export function useDeleteResource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.delete<void>(endpoints.resources.detail(id)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: resourceKeys.all }),
  });
}

export function useCostProfiles(resourceId: string, enabled = true) {
  return useQuery({
    queryKey: resourceKeys.costProfiles(resourceId),
    queryFn: () => api.get<ResourceCostProfileDto[]>(endpoints.resources.costProfiles(resourceId)),
    enabled: !!resourceId && enabled,
    staleTime: 30_000,
  });
}

export function useCreateCostProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ resourceId, body }: { resourceId: string; body: CreateResourceCostProfileInput }) =>
      api.post<ResourceCostProfileDto>(endpoints.resources.costProfiles(resourceId), body),
    onSuccess: (_result, input) => {
      void queryClient.invalidateQueries({ queryKey: resourceKeys.costProfiles(input.resourceId) });
      void queryClient.invalidateQueries({ queryKey: resourceKeys.detail(input.resourceId) });
      void queryClient.invalidateQueries({ queryKey: resourceKeys.lists() });
    },
  });
}

export function useProjectRoles(activeOnly = true, enabled = true) {
  return useQuery({
    queryKey: [...resourceKeys.all, "project-roles", activeOnly],
    queryFn: () => api.get<ProjectRoleDto[]>(endpoints.projectRoles.root, { activeOnly }),
    enabled,
    staleTime: 60_000,
  });
}

export function useProjectRoleNextCode(enabled = true, requestKey: number | string = 0) {
  return useQuery({
    queryKey: [...resourceKeys.all, "project-role-next-code", requestKey],
    queryFn: () => api.get<{ code: string }>(endpoints.projectRoles.nextCode),
    enabled,
    staleTime: 0,
  });
}

export function useCreateProjectRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: Record<string, unknown>) => api.post<ProjectRoleDto>(endpoints.projectRoles.root, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...resourceKeys.all, "project-roles"] }),
  });
}

export function useUpdateProjectRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { id: string; body: Record<string, unknown> }) => api.put<ProjectRoleDto>(endpoints.projectRoles.detail(data.id), data.body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...resourceKeys.all, "project-roles"] }),
  });
}

export function useDeactivateProjectRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.put<void>(`${endpoints.projectRoles.detail(id)}/deactivate`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...resourceKeys.all, "project-roles"] }),
  });
}
