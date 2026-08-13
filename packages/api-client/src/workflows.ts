import type { ApiClient } from "./client";
import type { WorkflowTemplate, WorkflowInstance, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createWorkflowsApi(client: ApiClient) {
  return {
    getWorkflowTemplates: (params?: PaginationParams) =>
      client.getPaged<WorkflowTemplate>("/workflows/templates", params),

    getWorkflowInstances: (templateId: string, params?: PaginationParams & { status?: string }) =>
      client.getPaged<WorkflowInstance>(`/workflows/templates/${templateId}/instances`, params),
  };
}
