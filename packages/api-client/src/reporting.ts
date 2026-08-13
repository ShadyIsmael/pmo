import type { ApiClient } from "./client";
import type { PagedResult, PaginationParams } from "@nexus/shared-types";

interface Report {
  id: string;
  name: string;
  category: string;
  createdAt: string;
  createdBy: string;
  format: string;
}

export function createReportingApi(client: ApiClient) {
  return {
    getReports: (params?: PaginationParams & { category?: string }) =>
      client.getPaged<Report>("/reports", params),

    generateReport: (reportId: string, params?: Record<string, string | number | boolean>) =>
      client.post<{ url: string; expiresAt: string }>(`/reports/${reportId}/generate`, params),
  };
}
