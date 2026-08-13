import type { ApiClient } from "./client";
import type { ApprovalRequest, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createApprovalsApi(client: ApiClient) {
  return {
    getApprovals: (params?: PaginationParams & { status?: string }) =>
      client.getPaged<ApprovalRequest>("/approvals", params),

    approve: (id: string, comments?: string) =>
      client.post<ApprovalRequest>(`/approvals/${id}/approve`, { comments }),

    reject: (id: string, reason: string) =>
      client.post<ApprovalRequest>(`/approvals/${id}/reject`, { reason }),

    delegate: (id: string, delegateId: string, reason?: string) =>
      client.post<ApprovalRequest>(`/approvals/${id}/delegate`, { delegateId, reason }),
  };
}
