import { api } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

export const projectKeys = {
  all: ["projects"] as const,
  list: (filters: unknown = {}) => ["projects", "list", filters] as const,
  detail: (id: string) => ["projects", "detail", id] as const,
  team: (id: string) => ["projects", "detail", id, "team"] as const,
  history: (id: string) => ["projects", "detail", id, "manager-history"] as const,
  snapshots: (id: string) => ["projects", "detail", id, "snapshots"] as const,
  snapshot: (projectId: string, snapshotId: string) => ["projects", "detail", projectId, "snapshots", snapshotId] as const,
  handovers: (id: string) => ["projects", "detail", id, "handover-requests"] as const,
  conflicts: (
    projectId: string,
    resourceId: string,
    startDate: string,
    endDate: string,
    allocationPercentage: number,
    excludeMemberId?: string
  ) => ["projects", "detail", projectId, "team", "conflicts", resourceId, startDate, endDate, allocationPercentage, excludeMemberId] as const,
};

export const projectApi = {
  list: <T>(params?: Record<string, unknown>) => api.get<T>(endpoints.projects.root, params as never),
  detail: <T>(id: string) => api.get<T>(endpoints.projects.detail(id)),
  create: <T>(body: unknown) => api.post<T>(endpoints.projects.root, body),
  update: <T>(id: string, body: unknown) => api.put<T>(endpoints.projects.detail(id), body),
  remove: <T>(id: string) => api.delete<T>(endpoints.projects.detail(id)),
};
