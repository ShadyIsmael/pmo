import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface CapacityMatrixRow { resourceId: string; resourceName: string; jobTitle?: string; departmentName?: string; weeklyCapacityHours: number; currentUtilization: number; cells: { periodKey: string; utilizationPercent: number; availableHours: number; netCapacityHours: number }[]; }
export interface CapacityMatrixDto { rows: CapacityMatrixRow[]; periods: { key: string; label: string }[]; summary: { overallUtilizationPercent: number; totalAvailableHours: number } }
export interface CostUtilizationReport {
  from: string; to: string; canViewCosts: boolean;
  operational: { capacityHours: number; plannedHours: number; actualHours: number; plannedUtilization?: number; actualUtilization?: number; plannedLaborCost?: number; actualLaborCost?: number };
  resources: { resourceId: string; resourceName: string; metrics: { actualHours: number; actualUtilization?: number; actualLaborCost?: number } }[];
  warnings: { code: string; message: string }[];
}

export function useCapacityMatrix(params: Record<string, string | undefined> = {}) {
  return useQuery({
    queryKey: ["capacity", "matrix", params],
    queryFn: () => api.get<CapacityMatrixDto>(endpoints.capacity.matrix, params),
    staleTime: 30_000,
  });
}

export function useCostUtilization(params: Record<string, string | undefined> = {}) {
  return useQuery({
    queryKey: ["resource-analytics", "cost-utilization", params],
    queryFn: () => api.get<CostUtilizationReport>(endpoints.resourceAnalytics.costUtilization, params),
    staleTime: 30_000,
  });
}
