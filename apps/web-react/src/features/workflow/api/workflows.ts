import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export type WorkflowDomain = "Task" | "Risk" | "Finance" | "ChangeRequest";
export type WorkflowRowVersion = string | number[];
export interface WorkflowDefinitionListItem { id: string; name: string; code: string; domain: WorkflowDomain; entityType: string; isActive: boolean; activePublishedVersionId?: string; activePublishedVersionNumber?: number; activePublishedLifecycleStatus?: "Draft" | "Published" | "Retired"; versionCount: number; updatedAt?: string; rowVersion?: WorkflowRowVersion; }
export interface WorkflowState { id: string; code: string; name: string; isInitial?: boolean; isTerminal?: boolean; }
export interface WorkflowTransition { id: string; code: string; name: string; fromStateId: string; toStateId: string; }
export interface WorkflowVersionDetails { id: string; definitionId: string; version: number; status: string; states: WorkflowState[]; transitions: WorkflowTransition[]; rowVersion?: number[]; }
export interface WorkflowDefinitionDetails { id: string; name: string; code: string; domain: WorkflowDomain; description?: string; isActive: boolean; versions: { id: string; version: number; status: string }[]; }
export interface ProjectWorkflowConfiguration { id: string; projectId: string; domain: WorkflowDomain; mode: "Standard" | "Custom"; workflowDefinitionId?: string; workflowDefinitionName?: string; effectiveWorkflowVersionId?: string; effectiveWorkflowVersionNumber?: number; isPublished: boolean; effectiveBehavior?: string; configuredAt: string; configuredBy: string; rowVersion?: WorkflowRowVersion; sponsorApprovalRequired: boolean; allowAnyOneOptionalApprover: boolean; allowSequentialApprovers: boolean; }
export interface ProjectWorkflowSettings { projectId: string; domains: ProjectWorkflowConfiguration[]; }

export const workflowKeys = {
  all: ["workflows"] as const,
  definitions: (params: Record<string, string | undefined> = {}) => ["workflows", "definitions", params] as const,
  definition: (id: string) => ["workflows", "definition", id] as const,
  version: (id: string) => ["workflows", "version", id] as const,
  project: (projectId: string) => ["workflows", "project", projectId] as const,
};

export function useWorkflowDefinitions(params: Record<string, string | undefined> = {}, enabled = true) {
  return useQuery({ queryKey: workflowKeys.definitions(params), queryFn: () => api.get<WorkflowDefinitionListItem[]>(endpoints.workflows.root, params), enabled, staleTime: 15_000 });
}
export function useWorkflowDefinition(id?: string) {
  return useQuery({ queryKey: workflowKeys.definition(id ?? ""), queryFn: () => api.get<WorkflowDefinitionDetails>(endpoints.workflows.definition(id!)), enabled: Boolean(id), staleTime: 10_000 });
}
export function useWorkflowVersion(id?: string) {
  return useQuery({ queryKey: workflowKeys.version(id ?? ""), queryFn: () => api.get<WorkflowVersionDetails>(endpoints.workflows.version(id!)), enabled: Boolean(id), staleTime: 5_000 });
}
export function useProjectWorkflowSettings(projectId?: string, enabled = true) {
  return useQuery({
    queryKey: workflowKeys.project(projectId ?? ""),
    queryFn: () => api.get<ProjectWorkflowSettings>(endpoints.projects.workflows(projectId!)),
    enabled: Boolean(projectId) && enabled,
    staleTime: 10_000,
  });
}

export function useWorkflowMutations() {
  const client = useQueryClient();
  const invalidate = () => { void client.invalidateQueries({ queryKey: workflowKeys.all }); };
  return {
    create: useMutation({ mutationFn: (body: Record<string, unknown>) => api.post<WorkflowDefinitionDetails>(endpoints.workflows.root, body), onSuccess: invalidate }),
    createDraft: useMutation({ mutationFn: (id: string) => api.post<WorkflowVersionDetails>(endpoints.workflows.drafts(id), {}), onSuccess: invalidate }),
    validate: useMutation({ mutationFn: (versionId: string) => api.post<{ valid: boolean; errors: string[] }>(endpoints.workflows.validate(versionId), {}) }),
    publish: useMutation({ mutationFn: (versionId: string) => api.post<WorkflowVersionDetails>(endpoints.workflows.publish(versionId), {}), onSuccess: invalidate }),
    saveProjectConfiguration: useMutation({
      mutationFn: ({ projectId, domain, body }: { projectId: string; domain: WorkflowDomain; body: { mode: "Standard" | "Custom"; workflowDefinitionId?: string; rowVersion?: WorkflowRowVersion; sponsorApprovalRequired?: boolean; allowAnyOneOptionalApprover?: boolean; allowSequentialApprovers?: boolean } }) =>
        api.put<ProjectWorkflowConfiguration>(endpoints.projects.workflow(projectId, domain), body),
      onSuccess: (_d, v) => { void client.invalidateQueries({ queryKey: workflowKeys.project(v.projectId) }); },
    }),
  };
}
