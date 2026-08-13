import type { ApiClient } from "./client";
import type { Budget, CostEntry, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createCostsApi(client: ApiClient) {
  return {
    getBudgets: (params?: PaginationParams & { projectId?: string; portfolioId?: string }) =>
      client.getPaged<Budget>("/budgets", params),

    getCosts: (projectId: string, params?: PaginationParams & { costType?: string }) =>
      client.getPaged<CostEntry>(`/projects/${projectId}/costs`, params),

    getCostVariance: (projectId: string) =>
      client.get<{ budget: number; actual: number; variance: number; variancePercentage: number }>(
        `/projects/${projectId}/costs/variance`,
      ),
  };
}
