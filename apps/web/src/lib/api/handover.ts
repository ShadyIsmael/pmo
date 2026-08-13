import { api } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";
import { projectKeys } from "@/lib/api/projects";

export const handoverKeys = {
  all: ["handover-requests"] as const,
  pending: (userId: string) => ["handover-requests", "pending-for-me", userId] as const,
  pendingForMe: ["handover-requests", "pending-for-me"] as const,
  project: (id: string) => projectKeys.handovers(id),
};

export const handoverApi = {
  project: <T>(id: string) => api.get<T>(endpoints.projects.handovers(id)),
  pending: <T>() => api.get<T>(endpoints.handover.pending),
  create: <T>(id: string, body: unknown) => api.post<T>(endpoints.projects.handovers(id), body),
  accept: <T>(id: string) => api.post<T>(endpoints.handover.accept(id), {}),
  reject: <T>(id: string, body: unknown) => api.post<T>(endpoints.handover.reject(id), body),
  cancel: <T>(id: string) => api.post<T>(endpoints.handover.cancel(id), {}),
};
