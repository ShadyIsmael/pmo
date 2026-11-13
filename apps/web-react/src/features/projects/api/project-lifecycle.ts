import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@auth/AuthProvider";
import { api } from "@api/client";

export type ProjectStatusReportingCategory = "InProgress" | "Completed" | "Excluded";
export type ProjectStatusAttentionLevel = "None" | "NeedsAttention" | "Critical";

export interface ProjectLifecycleOption {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  descriptionEn?: string | null;
  descriptionAr?: string | null;
  displayOrder: number;
  isDefault: boolean;
  isActive?: boolean;
  isContinuousActivity?: boolean;
  reportingCategory?: ProjectStatusReportingCategory;
  attentionLevel?: ProjectStatusAttentionLevel;
}

export interface ProjectLifecycleOptions {
  phases: ProjectLifecycleOption[];
  statuses: ProjectLifecycleOption[];
}

export interface ChangeProjectStatusRequest {
  targetStatusId: string;
  reason?: string;
}

export interface ProjectStatusTransitionResult {
  projectId: string;
  previousStatusId: string;
  newStatusId: string;
  previousStatus: string;
  newStatus: string;
  transitionedAt: string;
}

export interface ProjectLifecycleOptionsScope {
  purpose?: "create";
  portfolioId?: string;
  departmentId?: string;
}

export const projectLifecycleKeys = {
  options: (
    tenantId: string | undefined,
    projectId?: string,
    organizationId?: string,
    scope?: ProjectLifecycleOptionsScope,
  ) => [
    "projectLifecycle",
    tenantId,
    "options",
    projectId ?? "",
    organizationId ?? "",
    scope?.purpose ?? "",
    scope?.portfolioId ?? "",
    scope?.departmentId ?? "",
  ] as const,
};

export function useProjectLifecycleOptions({
  enabled = true,
  projectId,
  organizationId,
  purpose,
  portfolioId,
  departmentId,
}: {
  enabled?: boolean;
  projectId?: string;
  organizationId?: string;
  purpose?: "create";
  portfolioId?: string;
  departmentId?: string;
} = {}) {
  const { user } = useAuth();
  // The create lookup resolves the Organization from the portfolio/department scope.
  // It must never send a project or Organization identifier, and its scope must be
  // part of the query key so switching scope cannot reuse another scope's options.
  const createMode = purpose === "create";
  const scopedProjectId = createMode ? undefined : projectId;
  const scopedOrganizationId = createMode ? undefined : organizationId;
  return useQuery({
    queryKey: projectLifecycleKeys.options(user?.tenantId, scopedProjectId, scopedOrganizationId, {
      purpose,
      portfolioId: createMode ? portfolioId : undefined,
      departmentId: createMode ? departmentId : undefined,
    }),
    queryFn: () => api.get<ProjectLifecycleOptions>("/api/project-lifecycle/options", {
      ...(createMode ? { purpose: "create" } : {}),
      ...(createMode && portfolioId ? { portfolioId } : {}),
      ...(createMode && departmentId ? { departmentId } : {}),
      ...(!createMode && projectId ? { projectId } : {}),
      ...(!createMode && organizationId ? { organizationId } : {}),
    }),
    enabled: Boolean(enabled && user),
    staleTime: 30_000,
  });
}

export function useChangeProjectStatus(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ChangeProjectStatusRequest) =>
      api.post<ProjectStatusTransitionResult>(`/api/projects/${encodeURIComponent(projectId)}/status`, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["projects"] });
      void queryClient.invalidateQueries({ queryKey: ["projectLifecycle"] });
    },
  });
}

export function projectLifecycleLabel(option: ProjectLifecycleOption | undefined, locale: string, fallback?: string | null) {
  if (!option) return fallback?.trim() || "";
  return locale === "ar" ? option.nameAr || option.nameEn || option.code : option.nameEn || option.nameAr || option.code;
}

export function projectLifecycleDescription(option: ProjectLifecycleOption | undefined, locale: string) {
  if (!option) return "";
  return locale === "ar" ? option.descriptionAr || option.descriptionEn || "" : option.descriptionEn || option.descriptionAr || "";
}
