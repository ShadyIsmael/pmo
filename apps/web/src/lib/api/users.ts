import { api } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

export const userKeys = {
  all: ["users"] as const,
  admin: (params: unknown = {}) => ["users", "admin", params] as const,
  lookup: (role?: string) => ["users", "lookup", role] as const,
  roles: ["roles"] as const,
};

export const userApi = {
  list: <T>(p?: Record<string, unknown>) => api.get<T>(endpoints.users.root, p as never),
  create: <T>(b: unknown) => api.post<T>(endpoints.users.root, b),
  update: <T>(id: string, b: unknown) => api.put<T>(endpoints.users.detail(id), b),
  lookup: <T>(role?: string) => api.get<T>(endpoints.users.lookup, role ? { role } : undefined),
  roles: <T>() => api.get<T>(endpoints.users.roles),
};
