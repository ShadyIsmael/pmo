import type { ApiClient } from "./client";
import type { WorkItem, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createWorkItemsApi(client: ApiClient) {
  return {
    getWorkItems: (projectId: string, params?: PaginationParams & { status?: string; type?: string; assigneeId?: string }) =>
      client.getPaged<WorkItem>(`/projects/${projectId}/work-items`, params),

    getMyWorkItems: (params?: PaginationParams & { status?: string; projectId?: string }) =>
      client.getPaged<WorkItem>("/work-items/my", params),

    createWorkItem: (projectId: string, data: Partial<WorkItem>) =>
      client.post<WorkItem>(`/projects/${projectId}/work-items`, data),

    updateWorkItemStatus: (id: string, status: string) =>
      client.put<WorkItem>(`/work-items/${id}/status`, { status }),
  };
}
