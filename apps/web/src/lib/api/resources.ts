import { api } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

export const resourceKeys = {
  all: ["resources"] as const,
  list: (filters: unknown = {}) => ["resources", "list", filters] as const,
  detail: (id: string) => ["resources", "detail", id] as const,
  baseCalendars: ["resources", "base-calendars"] as const,
};

export const allocationKeys = {
  all: ["allocations"] as const,
  list: (params: unknown = {}) => ["allocations", "list", params] as const,
  detail: (id: string) => ["allocations", "detail", id] as const,
  byResource: (resourceId: string, page: number, pageSize: number) => ["allocations", "by-resource", resourceId, page, pageSize] as const,
  byProject: (projectId: string, page: number, pageSize: number) => ["allocations", "by-project", projectId, page, pageSize] as const,
  conflicts: (resourceId: string, startDate: string, endDate: string, excludeAllocationId?: string) => ["allocations", "conflicts", resourceId, startDate, endDate, excludeAllocationId] as const,
};

export const capacityKeys = {
  all: ["capacity"] as const,
  matrix: (filters: unknown = {}) => ["capacity", "matrix", filters] as const,
  availability: (filters: unknown = {}) => ["capacity", "availability", filters] as const,
  demand: (filters: unknown = {}) => ["capacity", "demand-vs-capacity", filters] as const,
  gantt: (filters: unknown = {}) => ["capacity", "gantt", filters] as const,
  resourceAvailability: (resourceId: string, startDate: string, endDate: string) => ["capacity", "resource-availability", resourceId, startDate, endDate] as const,
};

export type VendorStatusFilter = "all" | "active" | "inactive";

export const vendorKeys = {
  all: ["vendors"] as const,
  list: (search?: string, categoryId?: string, status?: VendorStatusFilter) =>
    ["vendors", "list", search ?? "", categoryId ?? "", status ?? "all"] as const,
  detail: (id: string) => ["vendors", "detail", id] as const,
};

export const skillKeys = {
  all: ["skills"] as const,
  list: (search?: string) => ["skills", search] as const,
};

export const resourceFinderKeys = {
  all: ["resource-finder"] as const,
  find: (query: unknown) => ["resource-finder", query] as const,
};

export const resourceApi = {
  list: <T>(p?: Record<string, unknown>) => api.get<T>(endpoints.resources.root, p as never),
  detail: <T>(id: string) => api.get<T>(endpoints.resources.detail(id)),
  create: <T>(b: unknown) => api.post<T>(endpoints.resources.root, b),
  update: <T>(id: string, b: unknown) => api.put<T>(endpoints.resources.detail(id), b),
  remove: <T>(id: string) => api.delete<T>(endpoints.resources.detail(id)),
};

export const resourceAnalyticsKeys = {
  all: ["resource-analytics"] as const,
  costUtilization: (filters: unknown = {}) => ["resource-analytics", "cost-utilization", filters] as const,
};
