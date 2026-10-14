import { useQuery, type UseQueryResult } from "@tanstack/react-query";
import { api } from "./client";
import { endpoints } from "./endpoints";

export interface AuditEntryDto {
  id: string;
  entityType: string;
  entityId: string;
  action: string;
  changes?: string | null;
  originalValues?: string | null;
  newValues?: string | null;
  userId?: string | null;
  userName?: string | null;
  timestamp: string;
}

export interface AuditPageDto {
  items: AuditEntryDto[];
  totalCount: number;
  page?: number;
  pageSize?: number;
  totalPages?: number;
}

export interface AuditQueryParams {
  page?: number;
  pageSize?: number;
  entityType?: string;
  action?: string;
  entityId?: string;
  entityIds?: string[];
  search?: string;
}

export const auditKeys = {
  all: ["audit"] as const,
  entries: (params: AuditQueryParams) => ["audit", "entries", params] as const,
  project: (projectId: string) => ["audit", "project", projectId] as const,
};

export function useAuditEntries(params: AuditQueryParams, enabled = true): UseQueryResult<AuditPageDto> {
  return useQuery({
    queryKey: auditKeys.entries(params),
    queryFn: () =>
      api.get<AuditPageDto>(endpoints.audit.root, {
        page: params.page,
        pageSize: params.pageSize,
        entityType: params.entityType,
        action: params.action,
        entityId: params.entityId,
        entityIds: params.entityIds?.length ? params.entityIds.join(",") : undefined,
        search: params.search,
      }),
    enabled,
    staleTime: 15_000,
  });
}

export function useProjectAuditEntries(projectId: string, pageSize = 50, memberIds: string[] = []): UseQueryResult<AuditPageDto> {
  return useAuditEntries(
    { page: 1, pageSize, entityIds: [projectId, ...memberIds] },
    Boolean(projectId),
  );
}
