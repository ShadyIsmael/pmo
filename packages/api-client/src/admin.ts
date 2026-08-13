import type { ApiClient } from "./client";
import type { User, Role, Permission, PagedResult, PaginationParams } from "@nexus/shared-types";

export function createAdminApi(client: ApiClient) {
  return {
    getUsers: (params?: PaginationParams & { isActive?: boolean; roleId?: string }) =>
      client.getPaged<User>("/admin/users", params),

    getRoles: (params?: PaginationParams) =>
      client.getPaged<Role>("/admin/roles", params),

    getPermissions: () => client.get<readonly Permission[]>("/admin/permissions"),
  };
}
