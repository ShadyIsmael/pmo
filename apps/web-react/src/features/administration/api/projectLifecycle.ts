import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";

export type ProjectLifecycleCatalogKind = "phases" | "statuses";
export type ProjectStatusReportingCategory = "InProgress" | "Completed" | "Excluded";
export type ProjectStatusAttentionLevel = "None" | "NeedsAttention" | "Critical";

export interface ProjectLifecycleCatalogItem {
  id: string;
  tenantId: string;
  organizationId: string | null;
  code: string;
  nameEn: string;
  nameAr: string;
  descriptionEn: string | null;
  descriptionAr: string | null;
  displayOrder: number;
  isDefault: boolean;
  isActive: boolean;
  isContinuousActivity?: boolean;
  reportingCategory?: ProjectStatusReportingCategory;
  attentionLevel?: ProjectStatusAttentionLevel;
  referencedProjectCount: number | null;
  rowVersion: string;
}

export type SaveProjectLifecycleCatalogItem = Pick<
  ProjectLifecycleCatalogItem,
  "code" | "nameEn" | "nameAr" | "displayOrder"
> & Partial<Pick<ProjectLifecycleCatalogItem, "descriptionEn" | "descriptionAr" | "isContinuousActivity" | "reportingCategory" | "attentionLevel">> & { organizationId?: string | null };

export interface ProjectLifecycleOption {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  displayOrder: number;
  isDefault: boolean;
  isActive: boolean;
  isContinuousActivity?: boolean;
  reportingCategory?: ProjectStatusReportingCategory;
  attentionLevel?: ProjectStatusAttentionLevel;
}

export interface ProjectLifecycleOptions {
  phases: ProjectLifecycleOption[];
  statuses: ProjectLifecycleOption[];
}

const catalogPath = (kind: ProjectLifecycleCatalogKind) => `/api/project-lifecycle/${kind}`;

function invalidateCatalogs(client: ReturnType<typeof useQueryClient>) {
  void client.invalidateQueries({ queryKey: ["projectLifecycle"] });
}

export function useProjectLifecycleCatalog(kind: ProjectLifecycleCatalogKind, organizationId?: string | null, includeInactive = true, enabled = true) {
  return useQuery({
    queryKey: ["projectLifecycle", kind, "catalog", organizationId ?? null, includeInactive],
    queryFn: () => api.get<ProjectLifecycleCatalogItem[]>(catalogPath(kind), { organizationId: organizationId ?? undefined, includeInactive }),
    enabled,
    staleTime: 30_000,
  });
}

export function useProjectLifecycleOptions(organizationId?: string | null, projectId?: string | null, enabled = true) {
  return useQuery({
    queryKey: ["projectLifecycle", "options", organizationId ?? null, projectId ?? null],
    queryFn: () => api.get<ProjectLifecycleOptions>("/api/project-lifecycle/options", { organizationId: organizationId ?? undefined, projectId: projectId ?? undefined }),
    enabled: Boolean(enabled && (organizationId || projectId)),
    staleTime: 30_000,
  });
}

export function useCreateProjectLifecycleItem(kind: ProjectLifecycleCatalogKind) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: SaveProjectLifecycleCatalogItem & { organizationId?: string | null }) => api.post<ProjectLifecycleCatalogItem>(catalogPath(kind), input),
    onSuccess: () => invalidateCatalogs(client),
  });
}

export function useUpdateProjectLifecycleItem(kind: ProjectLifecycleCatalogKind) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: SaveProjectLifecycleCatalogItem & { rowVersion: string } }) => api.put<ProjectLifecycleCatalogItem>(`${catalogPath(kind)}/${encodeURIComponent(id)}`, input),
    onSuccess: () => invalidateCatalogs(client),
  });
}

export function useProjectLifecycleItemAction(kind: ProjectLifecycleCatalogKind, action: "activate" | "deactivate" | "default") {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, rowVersion }: { id: string; rowVersion: string }) => api.post<ProjectLifecycleCatalogItem>(`${catalogPath(kind)}/${encodeURIComponent(id)}/${action}`, { rowVersion }),
    onSuccess: () => invalidateCatalogs(client),
  });
}
