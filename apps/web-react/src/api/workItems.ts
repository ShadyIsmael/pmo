import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";

export interface WorkItemDto {
  id: string;
  projectId: string;
  projectName: string;
  projectCode: string;
  title: string;
  description?: string;
  type: string;
  priority: string;
  status: string;
  dueDate?: string;
  assigneeId?: string;
  assigneeName?: string;
  startDate?: string;
  endDate?: string;
  completedAt?: string;
  storyPoints?: number;
  estimatedHours?: number;
  actualHours?: number;
  sortOrder: number;
}

export interface WorkItemStatusDto {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  displayOrder: number;
  isActive: boolean;
  isTerminal: boolean;
}

const workItemKeys = {
  all: ["workItems"] as const,
  lists: () => [...workItemKeys.all, "list"] as const,
  list: (params: any) => [...workItemKeys.lists(), params] as const,
  detail: (id: string) => [...workItemKeys.all, "detail", id] as const,
  statuses: () => [...workItemKeys.all, "statuses"] as const,
};

export function useWorkItems(params: any = {}) {
  return useQuery({
    queryKey: workItemKeys.list(params),
    queryFn: () => api.get<{ items: WorkItemDto[]; totalCount: number; page: number; pageSize: number; totalPages: number }>("/api/work-items", params),
    staleTime: 30_000,
  });
}

export function useWorkItemStatuses(enabled = true) {
  return useQuery({
    queryKey: workItemKeys.statuses(),
    queryFn: () => api.get<WorkItemStatusDto[]>("/api/reference-data/work-item-statuses"),
    enabled,
    staleTime: 60_000,
    gcTime: 300_000,
  });
}