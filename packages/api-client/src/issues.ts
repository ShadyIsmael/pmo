import type { ApiClient } from "./client";
import type { Issue, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createIssuesApi(client: ApiClient) {
  return {
    getIssues: (projectId: string, params?: PaginationParams & { status?: string; severity?: string }) =>
      client.getPaged<Issue>(`/projects/${projectId}/issues`, params),

    createIssue: (projectId: string, data: Partial<Issue>) =>
      client.post<Issue>(`/projects/${projectId}/issues`, data),

    updateIssue: (id: string, data: Partial<Issue>) =>
      client.put<Issue>(`/issues/${id}`, data),
  };
}
