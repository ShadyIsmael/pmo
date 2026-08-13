import type { ApiClient } from "./client";
import type { Plugin, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createPluginsApi(client: ApiClient) {
  return {
    getPlugins: (params?: PaginationParams & { status?: string }) =>
      client.getPaged<Plugin>("/plugins", params),

    activatePlugin: (id: string) => client.post<Plugin>(`/plugins/${id}/activate`),

    deactivatePlugin: (id: string) => client.post<Plugin>(`/plugins/${id}/deactivate`),

    getPluginHealth: (id: string) =>
      client.get<{ status: string; uptime: number; lastChecked: string; errors: readonly string[] }>(
        `/plugins/${id}/health`,
      ),
  };
}
