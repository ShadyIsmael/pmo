"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";

export interface CapacityCellDto {
  periodKey: string;
  periodStart: string;
  periodEnd: string;
  grossCapacityHours: number;
  netCapacityHours: number;
  leaveHours: number;
  confirmedHours: number;
  proposedHours: number;
  availableHours: number;
  utilizationPercent: number;
  heatmapState: string;
  allocations: AllocationBreakdownDto[];
}

export interface AllocationBreakdownDto {
  allocationId: string;
  projectId?: string;
  projectName: string;
  percentage: number;
  hours: number;
  status: string;
  isBillable: boolean;
}

export interface CapacityMatrixRowDto {
  resourceId: string;
  resourceName: string;
  jobTitle?: string;
  departmentName?: string;
  seniority?: string;
  weeklyCapacityHours: number;
  currentUtilization: number;
  earliestAvailableDate?: string;
  cells: CapacityCellDto[];
}

export interface CapacityPeriodDto {
  key: string;
  label: string;
  start: string;
  end: string;
}

export interface CapacitySummaryDto {
  totalGrossCapacityHours: number;
  totalNetCapacityHours: number;
  totalConfirmedHours: number;
  totalProposedHours: number;
  totalAvailableHours: number;
  overallUtilizationPercent: number;
}

export interface CapacityMatrixDto {
  rows: CapacityMatrixRowDto[];
  periods: CapacityPeriodDto[];
  summary: CapacitySummaryDto;
}

export interface ResourceAvailabilityDto {
  resourceId: string;
  resourceName: string;
  jobTitle?: string;
  departmentName?: string;
  weeklyCapacityHours: number;
  currentUtilization: number;
  availableCapacityHours: number;
  earliestAvailableDate?: string;
  conflicts?: AllocationConflictDto[];
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

export interface DemandVsCapacityGroupDto {
  groupType: string;
  groupName: string;
  demandHours: number;
  capacityHours: number;
  gapHours: number;
  status: string;
}

export interface DemandVsCapacityDto {
  groups: DemandVsCapacityGroupDto[];
  total: { totalDemandHours: number; totalCapacityHours: number; totalGapHours: number };
}

export interface CapacityFilterParams {
  periodType?: string;
  startDate?: string;
  endDate?: string;
  departmentId?: string;
  locationId?: string;
  resourceType?: string;
  resourceId?: string;
  includeProposed?: boolean;
}

export interface AvailabilityPage {
  items: ResourceAvailabilityDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

function toParams(filter: CapacityFilterParams): Record<string, string | number | boolean | undefined> {
  return {
    periodType: filter.periodType,
    startDate: filter.startDate,
    endDate: filter.endDate,
    departmentId: filter.departmentId,
    locationId: filter.locationId,
    resourceType: filter.resourceType,
    resourceId: filter.resourceId,
    includeProposed: filter.includeProposed,
  };
}

async function fetchCapacityMatrix(filter: CapacityFilterParams): Promise<CapacityMatrixDto> {
  return api.get<CapacityMatrixDto>("/api/capacity/matrix", toParams(filter));
}

export function useCapacityMatrix(filter: CapacityFilterParams = {}) {
  return useQuery({
    queryKey: ["capacity", "matrix", filter],
    queryFn: () => fetchCapacityMatrix(filter),
    staleTime: 30_000,
  });
}

async function fetchAvailability(filter: CapacityFilterParams): Promise<AvailabilityPage> {
  return api.get<AvailabilityPage>("/api/capacity/availability", toParams(filter));
}

export function useCapacityAvailability(filter: CapacityFilterParams = {}) {
  return useQuery({
    queryKey: ["capacity", "availability", filter],
    queryFn: () => fetchAvailability(filter),
    staleTime: 30_000,
  });
}

async function fetchDemandVsCapacity(filter: CapacityFilterParams): Promise<DemandVsCapacityDto> {
  return api.get<DemandVsCapacityDto>("/api/capacity/demand-vs-capacity", toParams(filter));
}

export function useDemandVsCapacity(filter: CapacityFilterParams = {}) {
  return useQuery({
    queryKey: ["capacity", "demand-vs-capacity", filter],
    queryFn: () => fetchDemandVsCapacity(filter),
    staleTime: 30_000,
  });
}

async function fetchGanttData(filter: CapacityFilterParams): Promise<import("./useAllocations").AllocationDto[]> {
  return api.get<import("./useAllocations").AllocationDto[]>("/api/capacity/gantt", toParams(filter));
}

export function useCapacityGantt(filter: CapacityFilterParams = {}) {
  return useQuery({
    queryKey: ["capacity", "gantt", filter],
    queryFn: () => fetchGanttData(filter),
    staleTime: 30_000,
  });
}

async function fetchResourceAvailability(resourceId: string, startDate: string, endDate: string): Promise<ResourceAvailabilityDto> {
  return api.get<ResourceAvailabilityDto>(`/api/capacity/resource/${resourceId}/availability`, { startDate, endDate });
}

export function useResourceAvailability(resourceId: string, startDate: string, endDate: string) {
  return useQuery({
    queryKey: ["capacity", "resource-availability", resourceId, startDate, endDate],
    queryFn: () => fetchResourceAvailability(resourceId, startDate, endDate),
    enabled: !!resourceId && !!startDate && !!endDate,
    staleTime: 30_000,
  });
}
