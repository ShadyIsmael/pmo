import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import type { AllocationDto, AllocationConflictDto } from "./allocations";

export interface CapacityCell { periodKey: string; periodStart?: string; periodEnd?: string; grossCapacityHours?: number; netCapacityHours: number; leaveHours?: number; confirmedHours?: number; proposedHours?: number; availableHours: number; utilizationPercent: number; heatmapState?: string; overallocatedHours?: number; allocations?: { allocationId: string; projectId?: string; projectName: string; percentage: number; hours: number; plannedHours?: number; plannedLaborCost?: number; status: string; isBillable: boolean; currency?: string }[]; }
export interface CapacityMatrixRow { resourceId: string; resourceName: string; jobTitle?: string; departmentName?: string; seniority?: string; weeklyCapacityHours: number; currentUtilization: number; earliestAvailableDate?: string; costPerHour?: number; currency?: string; cells: CapacityCell[]; }
export interface CapacityPeriod { key: string; label: string; start?: string; end?: string; }
export interface CapacitySummary { totalGrossCapacityHours?: number; totalNetCapacityHours?: number; totalConfirmedHours?: number; totalProposedHours?: number; totalAvailableHours: number; overallUtilizationPercent: number; }
export interface CapacityMatrixDto { rows: CapacityMatrixRow[]; periods: CapacityPeriod[]; summary: CapacitySummary }
export interface ResourceAvailabilityDto { resourceId: string; resourceName: string; jobTitle?: string; departmentName?: string; weeklyCapacityHours: number; currentUtilization: number; availableCapacityHours: number; earliestAvailableDate?: string; costPerHour?: number; currency?: string; conflicts?: AllocationConflictDto[]; }
export interface DemandVsCapacityGroupDto { groupType: string; groupName: string; demandHours: number; capacityHours: number; gapHours: number; status: string; }
export interface DemandVsCapacityDto { groups: DemandVsCapacityGroupDto[]; total: { totalDemandHours: number; totalCapacityHours: number; totalGapHours: number }; }
export interface CapacityFilterParams { periodType?: string; startDate?: string; endDate?: string; departmentId?: string; locationId?: string; resourceType?: string; resourceId?: string; includeProposed?: boolean; }

function toParams(filter: CapacityFilterParams): Record<string, string | number | boolean | undefined> {
  return { periodType: filter.periodType, startDate: filter.startDate, endDate: filter.endDate, departmentId: filter.departmentId, locationId: filter.locationId, resourceType: filter.resourceType, resourceId: filter.resourceId, includeProposed: filter.includeProposed };
}

const capacityKeys = {
  matrix: (filter: CapacityFilterParams) => ["capacity", "matrix", filter] as const,
  availability: (filter: CapacityFilterParams) => ["capacity", "availability", filter] as const,
  demand: (filter: CapacityFilterParams) => ["capacity", "demand-vs-capacity", filter] as const,
  gantt: (filter: CapacityFilterParams) => ["capacity", "gantt", filter] as const,
  resourceAvailability: (resourceId: string, startDate: string, endDate: string) => ["capacity", "resource-availability", resourceId, startDate, endDate] as const,
};
export interface AvailabilityPage { items: ResourceAvailabilityDto[]; page: number; pageSize: number; totalCount: number; totalPages: number; }

export function useCapacityMatrix(params: CapacityFilterParams = {}, enabled = true) {
  return useQuery({
    queryKey: capacityKeys.matrix(params),
    queryFn: () => api.get<CapacityMatrixDto>(endpoints.capacity.matrix, toParams(params)),
    staleTime: 30_000,
    enabled,
  });
}

export function useCapacityAvailability(params: CapacityFilterParams = {}) {
  return useQuery({
    queryKey: capacityKeys.availability(params),
    queryFn: () => api.get<AvailabilityPage>(endpoints.capacity.availability, toParams(params)),
    staleTime: 30_000,
  });
}

export function useDemandVsCapacity(params: CapacityFilterParams = {}) {
  return useQuery({
    queryKey: capacityKeys.demand(params),
    queryFn: () => api.get<DemandVsCapacityDto>(endpoints.capacity.demand, toParams(params)),
    staleTime: 30_000,
  });
}

export function useCapacityGantt(params: CapacityFilterParams = {}) {
  return useQuery({
    queryKey: capacityKeys.gantt(params),
    queryFn: () => api.get<AllocationDto[]>(endpoints.capacity.gantt, toParams(params)),
    staleTime: 30_000,
  });
}

export function useResourceAvailability(resourceId: string, startDate: string, endDate: string) {
  return useQuery({
    queryKey: capacityKeys.resourceAvailability(resourceId, startDate, endDate),
    queryFn: () => api.get<ResourceAvailabilityDto>(endpoints.capacity.resourceAvailability(resourceId), { startDate, endDate }),
    enabled: Boolean(resourceId && startDate && endDate),
    staleTime: 30_000,
  });
}
