import type { ApiClient } from "./client";
import type { Risk, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createRisksApi(client: ApiClient) {
  return {
    getRisks: (projectId: string, params?: PaginationParams & { status?: string; level?: string }) =>
      client.getPaged<Risk>(`/projects/${projectId}/risks`, params),

    createRisk: (projectId: string, data: Partial<Risk>) =>
      client.post<Risk>(`/projects/${projectId}/risks`, data),

    updateRisk: (id: string, data: Partial<Risk>) =>
      client.put<Risk>(`/risks/${id}`, data),
  };
}
