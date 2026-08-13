import type { ApiClient } from "./client";
import type { IntegrationConnection, IntegrationSync, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createIntegrationsApi(client: ApiClient) {
  return {
    getIntegrations: (params?: PaginationParams) =>
      client.getPaged<IntegrationConnection>("/integrations", params),

    getSyncHistory: (connectionId: string, params?: PaginationParams) =>
      client.getPaged<IntegrationSync>(`/integrations/${connectionId}/syncs`, params),

    triggerSync: (connectionId: string) =>
      client.post<IntegrationSync>(`/integrations/${connectionId}/sync`),
  };
}
