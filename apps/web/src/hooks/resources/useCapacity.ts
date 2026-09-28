"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";
import { capacityKeys } from "@/lib/api/resources";

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
  overallocatedHours: number;
  allocations: AllocationBreakdownDto[];
}

export interface AllocationBreakdownDto {
  allocationId: string;
  projectId?: string;
  projectName: string;
  percentage: number;
  hours: number;
  plannedHours?: number;
  plannedLaborCost?: number;
  status: string;
  isBillable: boolean;
  currency?: string;
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
  costPerHour?: number;
  currency?: string;
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
  costPerHour?: number;
  currency?: string;
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
  return api.get<CapacityMatrixDto>(endpoints.capacity.matrix, toParams(filter));
}

export function useCapacityMatrix(filter: CapacityFilterParams = {}) {
  return useQuery({
    queryKey: capacityKeys.matrix(filter),
    queryFn: () => fetchCapacityMatrix(filter),
    staleTime: 30_000,
  });
}

async function fetchAvailability(filter: CapacityFilterParams): Promise<AvailabilityPage> {
  return api.get<AvailabilityPage>(endpoints.capacity.availability, toParams(filter));
}

export function useCapacityAvailability(filter: CapacityFilterParams = {}) {
  return useQuery({
    queryKey: capacityKeys.availability(filter),
    queryFn: () => fetchAvailability(filter),
    staleTime: 30_000,
  });
}

async function fetchDemandVsCapacity(filter: CapacityFilterParams): Promise<DemandVsCapacityDto> {
  return api.get<DemandVsCapacityDto>(endpoints.capacity.demand, toParams(filter));
}

export function useDemandVsCapacity(filter: CapacityFilterParams = {}) {
  return useQuery({
    queryKey: capacityKeys.demand(filter),
    queryFn: () => fetchDemandVsCapacity(filter),
    staleTime: 30_000,
  });
}

async function fetchGanttData(filter: CapacityFilterParams): Promise<import("./useAllocations").AllocationDto[]> {
  return api.get<import("./useAllocations").AllocationDto[]>(endpoints.capacity.gantt, toParams(filter));
}

export function useCapacityGantt(filter: CapacityFilterParams = {}) {
  return useQuery({
    queryKey: capacityKeys.gantt(filter),
    queryFn: () => fetchGanttData(filter),
    staleTime: 30_000,
  });
}

async function fetchResourceAvailability(resourceId: string, startDate: string, endDate: string): Promise<ResourceAvailabilityDto> {
  return api.get<ResourceAvailabilityDto>(endpoints.capacity.resourceAvailability(resourceId), { startDate, endDate });
}

export function useResourceAvailability(resourceId: string, startDate: string, endDate: string) {
  return useQuery({
    queryKey: capacityKeys.resourceAvailability(resourceId, startDate, endDate),
    queryFn: () => fetchResourceAvailability(resourceId, startDate, endDate),
    enabled: !!resourceId && !!startDate && !!endDate,
    staleTime: 30_000,
  });
}
