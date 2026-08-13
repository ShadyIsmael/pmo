import type { ApiClient } from "./client";
import type { AuditEntry, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createAuditApi(client: ApiClient) {
  return {
    getAuditLog: (params?: PaginationParams & { action?: string; entityType?: string; userId?: string }) =>
      client.getPaged<AuditEntry>("/audit", params),
  };
}
