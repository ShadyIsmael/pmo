"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";
import { resourceAnalyticsKeys } from "@/lib/api/resources";

export interface CostUtilizationFilter {
  from?: string;
  to?: string;
  resourceId?: string;
  projectId?: string;
  vendorId?: string;
  projectRoleId?: string;
  departmentId?: string;
}

export interface CostUtilizationMetric {
  currency?: string;
  capacityHours: number;
  plannedHours: number;
  actualHours: number;
  regularHours: number;
  planningGapHours: number;
  executionGapHours: number;
  unusedCapacityHours: number;
  excessHours: number;
  overtimeHours: number;
  plannedUtilization?: number;
  actualUtilization?: number;
  plannedLaborCost?: number;
  actualLaborCost?: number;
  laborCostVariance?: number;
  overtimeCost?: number;
  unusedCapacityCost?: number;
  committedCapacityCost?: number;
  totalResourceCost?: number;
  costProfileMissing: boolean;
  costCoverageComplete: boolean;
  uncostedActualHours: number;
  uncostedPlannedHours: number;
}

export interface CostUtilizationAttribution {
  id?: string;
  name: string;
  currency?: string;
  plannedHours: number;
  actualHours: number;
  regularHours: number;
  excessHours: number;
  overtimeHours: number;
  plannedLaborCost?: number;
  actualLaborCost?: number;
  overtimeCost?: number;
  laborCostVariance?: number;
  costProfileMissing: boolean;
  costCoverageComplete: boolean;
}

export interface CostUtilizationReport {
  from: string;
  to: string;
  canViewCosts: boolean;
  operational: CostUtilizationMetric;
  byCurrency: CostUtilizationMetric[];
  resources: Array<{ resourceId: string; resourceName: string; resourceType: string; departmentName?: string; vendorId?: string; vendorName?: string; metrics: CostUtilizationMetric }>;
  projects: CostUtilizationAttribution[];
  vendors: CostUtilizationAttribution[];
  roles: CostUtilizationAttribution[];
  daily: Array<CostUtilizationMetric & { resourceId: string; resourceName: string; workDate: string }>;
  warnings: Array<{ code: string; message: string; resourceId?: string; resourceName?: string }>;
}

export function useCostUtilization(filters: CostUtilizationFilter = {}) {
  return useQuery({
    queryKey: resourceAnalyticsKeys.costUtilization(filters),
    queryFn: () => api.get<CostUtilizationReport>(endpoints.resourceAnalytics.costUtilization, filters as Record<string, string | undefined>),
    staleTime: 30_000,
  });
}
