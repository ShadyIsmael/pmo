import type { ApiClient } from "./client";
import type {
  Resource,
  ResourceAllocation,
  Skill,
  Vendor,
  PagedResult,
  PaginationParams,
} from "@nexus/shared-types";

export interface ResourceFilterParams extends PaginationParams {
  status?: string;
  type?: string;
  departmentId?: string;
  locationId?: string;
  businessUnitId?: string;
  isActive?: boolean;
}

export interface CreateResourceData {
  name: string;
  employeeId?: string;
  jobTitle?: string;
  email?: string;
  phone?: string;
  type?: string;
  status?: string;
  costPerHour?: number;
  billableRate?: number;
  currency?: string;
  employmentType?: string;
  seniority?: string;
  departmentId?: string;
  locationId?: string;
  skills?: { skillId: string; proficiency: string; yearsOfExperience: number }[];
}

export function createResourcesApi(client: ApiClient) {
  return {
    getResources: (params?: ResourceFilterParams) =>
      client.getPaged<Resource>("/api/resources", params as Record<string, string | number | boolean | undefined>),

    getResource: (id: string) =>
      client.get<Resource>(`/api/resources/${id}`),

    createResource: (data: CreateResourceData) =>
      client.post<Resource>("/api/resources", data),

    updateResource: (id: string, data: Partial<CreateResourceData>) =>
      client.put<Resource>(`/api/resources/${id}`, data),

    deleteResource: (id: string) =>
      client.delete<void>(`/api/resources/${id}`),

    getResourceAllocations: (resourceId: string, params?: PaginationParams) =>
      client.getPaged<ResourceAllocation>(`/api/allocations/by-resource/${resourceId}`, params),

    getSkills: (params?: PaginationParams & { search?: string }) =>
      client.getPaged<Skill>("/api/skills", params as Record<string, string | number | boolean | undefined>),

    createSkill: (data: { name: string; category?: string; description?: string }) =>
      client.post<Skill>("/api/skills", data),

    getVendors: (params?: PaginationParams & { search?: string }) =>
      client.getPaged<Vendor>("/api/vendors", params as Record<string, string | number | boolean | undefined>),

    createVendor: (data: { name: string; contactName?: string; contactEmail?: string; category?: string }) =>
      client.post<Vendor>("/api/vendors", data),
  };
}
