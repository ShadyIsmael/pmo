"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";
import { resourceFinderKeys } from "@/lib/api/resources";

export interface ResourceFinderQueryDto {
  projectId?: string;
  role?: string;
  skillIds?: string[];
  seniority?: string;
  startDate?: string;
  endDate?: string;
  requiredAllocation?: number;
  resourceType?: string;
  departmentId?: string;
  locationId?: string;
  vendorId?: string;
  page?: number;
  pageSize?: number;
}

export interface ResourceFinderConflictDto {
  allocationId: string;
  projectName: string;
  role: string;
  allocationPercentage: number;
  startDate: string;
  endDate: string;
}

export interface ResourceFinderSkillMatchDto {
  skillName: string;
  proficiency: string;
  isMatch: boolean;
}

export interface ResourceFinderResultDto {
  resourceId: string;
  resourceName: string;
  jobTitle?: string;
  seniority?: string;
  departmentName?: string;
  locationName?: string;
  email?: string;
  phone?: string;
  matchScore: number;
  availableCapacityHours: number;
  currentLoadPercent: number;
  earliestAvailableDate?: string;
  conflictingAllocations: ResourceFinderConflictDto[];
  skillMatches: ResourceFinderSkillMatchDto[];
  resourceType: string;
  avatarUrl?: string;
}

export interface ResourceFinderResponse {
  items: ResourceFinderResultDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

async function findResources(query: ResourceFinderQueryDto): Promise<ResourceFinderResponse> {
  return api.post<ResourceFinderResponse>(endpoints.resourceFinder, query);
}

export function useResourceFinder(query: ResourceFinderQueryDto, enabled = true) {
  return useQuery({
    queryKey: resourceFinderKeys.find(query),
    queryFn: () => findResources(query),
    enabled,
    staleTime: 15_000,
  });
}
