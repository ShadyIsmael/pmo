import type { ApiClient } from "./client";
import type { LoginRequest, LoginResponse, CurrentUser } from "@nexus/shared-types";

export function createAuthApi(client: ApiClient) {
  return {
    login: (data: LoginRequest) => client.post<LoginResponse>("/auth/login", data),

    logout: () => client.post<void>("/auth/logout"),

    getMe: () => client.get<CurrentUser>("/auth/me"),

    switchRole: (roleName: string) => client.post<LoginResponse>("/auth/switch-role", { roleName }),

    refreshToken: (refreshToken: string) => client.post<LoginResponse>("/auth/refresh", { refreshToken }),
  };
}
