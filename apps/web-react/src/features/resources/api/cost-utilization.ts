import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface CostUtilizationFilter {
  from?: string;
  to?: string;
  resourceId?: string;
  projectId?: string;
  vendorId?: string;
  projectRoleId?: string;
  departmentId?: string;
  teamId?: string;
  planActivityId?: string;
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

export interface CostUtilizationResource {
  resourceId: string;
  resourceName: string;
  resourceType: string;
  departmentName?: string;
  vendorId?: string;
  vendorName?: string;
  metrics: CostUtilizationMetric;
}

export interface CostUtilizationDepartmentOption {
  departmentId: string;
  name: string;
}

export interface CostUtilizationAttribution {
  id?: string;
  projectPublicId?: string;
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

export interface CostUtilizationDaily extends CostUtilizationMetric {
  resourceId: string;
  resourceName: string;
  workDate: string;
  activities?: CostUtilizationActivity[];
}

export interface CostUtilizationActivity {
  planActivityId?: string;
  activityName?: string;
  workItemTitles: string[];
  projectId?: string;
  projectName?: string;
  plannedHours: number;
  actualHours: number;
}

export interface CostUtilizationReport {
  from: string;
  to: string;
  canViewCosts: boolean;
  operational: CostUtilizationMetric;
  byCurrency: CostUtilizationMetric[];
  resources: CostUtilizationResource[];
  departments?: CostUtilizationDepartmentOption[];
  projects: CostUtilizationAttribution[];
  vendors: CostUtilizationAttribution[];
  roles: CostUtilizationAttribution[];
  daily: CostUtilizationDaily[];
  warnings: Array<{ code: string; message: string; resourceId?: string; resourceName?: string }>;
}

function toParams(filter: CostUtilizationFilter): Record<string, string | undefined> {
  return {
    from: filter.from,
    to: filter.to,
    resourceId: filter.resourceId,
    projectId: filter.projectId,
    vendorId: filter.vendorId,
    projectRoleId: filter.projectRoleId,
    departmentId: filter.departmentId,
    teamId: filter.teamId,
    planActivityId: filter.planActivityId,
  };
}

export function useCostUtilization(filter: CostUtilizationFilter = {}, enabled = true) {
  return useQuery({
    queryKey: ["resource-analytics", "cost-utilization", filter],
    queryFn: () => api.get<CostUtilizationReport>(endpoints.resourceAnalytics.costUtilization, toParams(filter)),
    staleTime: 30_000,
    enabled,
  });
}

export function useCostUtilizationExportData(filter: CostUtilizationFilter = {}, enabled = true) {
  return useQuery({
    queryKey: ["resource-analytics", "cost-utilization-export-data", filter],
    queryFn: () => api.get<CostUtilizationReport>(endpoints.resourceAnalytics.costUtilizationExportData, toParams(filter)),
    staleTime: 30_000,
    enabled,
  });
}
