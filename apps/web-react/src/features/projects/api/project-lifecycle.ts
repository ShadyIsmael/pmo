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

export interface ProjectStatusTransitionOption {
  targetStatusId: string;
  requiresReason: boolean;
}

export interface ProjectLifecycleOptions {
  phases: ProjectLifecycleOption[];
  statuses: ProjectLifecycleOption[];
  // Null means no catalog transition rules are configured, so all active scoped statuses are eligible.
  statusTransitions?: ProjectStatusTransitionOption[] | null;
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
  programId?: string;
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
    scope?.programId ?? "",
  ] as const,
};

export function useProjectLifecycleOptions({
  enabled = true,
  projectId,
  organizationId,
  purpose,
  portfolioId,
  programId,
}: {
  enabled?: boolean;
  projectId?: string;
  organizationId?: string;
  purpose?: "create";
  portfolioId?: string;
  programId?: string;
} = {}) {
  const { user } = useAuth();
  // The create lookup resolves the Organization from the selected Portfolio.
  // It must never send a project or Organization identifier, and its scope must be
  // part of the query key so switching scope cannot reuse another scope's options.
  const createMode = purpose === "create";
  const scopedProjectId = createMode ? undefined : projectId;
  const scopedOrganizationId = createMode ? undefined : organizationId;
  return useQuery({
    queryKey: projectLifecycleKeys.options(user?.tenantId, scopedProjectId, scopedOrganizationId, {
      purpose,
      portfolioId: createMode ? portfolioId : undefined,
      programId: createMode ? programId : undefined,
    }),
    queryFn: () => api.get<ProjectLifecycleOptions>("/api/project-lifecycle/options", {
      ...(createMode ? { purpose: "create" } : {}),
      ...(createMode && portfolioId ? { portfolioId } : {}),
      ...(createMode && programId ? { programId } : {}),
      ...(!createMode && projectId ? { projectId } : {}),
      ...(!createMode && organizationId ? { organizationId } : {}),
    }),
    // Non-create lookups must be scoped to an authorized Organization or Project.
    // Create mode is allowed before Portfolio selection because it returns no
    // Organization phases until the selected Portfolio resolves that scope.
    enabled: Boolean(enabled && user && (createMode || scopedProjectId || scopedOrganizationId)),
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
