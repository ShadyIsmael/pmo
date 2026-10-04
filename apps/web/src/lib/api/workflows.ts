import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

export type WorkflowLifecycleStatus = "Draft" | "Published" | "Retired";
export type WorkflowDomain = "Task" | "Risk" | "Finance";
export type WorkflowRowVersion = string | number[];

export interface WorkflowDefinitionListItem { id: string; name: string; code: string; domain: WorkflowDomain; entityType: string; isActive: boolean; activePublishedVersionId?: string; activePublishedVersionNumber?: number; activePublishedLifecycleStatus?: WorkflowLifecycleStatus; versionCount: number; updatedAt?: string; rowVersion?: WorkflowRowVersion; }
export interface WorkflowVersionListItem { id: string; versionNumber: number; lifecycleStatus: WorkflowLifecycleStatus; changeLog?: string; createdAt: string; createdBy: string; publishedAt?: string; publishedBy?: string; stateCount: number; transitionCount: number; guardCount: number; actionCount: number; rowVersion?: WorkflowRowVersion; }
export interface WorkflowDefinitionDetails { id: string; name: string; code: string; description?: string; domain: WorkflowDomain; entityType: string; isActive: boolean; activePublishedVersionId?: string; createdAt: string; createdBy: string; updatedAt?: string; updatedBy?: string; rowVersion?: WorkflowRowVersion; versions: WorkflowVersionListItem[]; }
export interface WorkflowState { id: string; code: string; nameEn: string; nameAr: string; description?: string; displayOrder: number; isInitial: boolean; isFinal: boolean; isActive: boolean; rowVersion?: WorkflowRowVersion; }
export interface WorkflowGuard { id: string; guardType: string; configurationJson: string; executionOrder: number; isActive: boolean; rowVersion?: WorkflowRowVersion; }
export interface WorkflowAction { id: string; actionType: string; configurationJson: string; executionOrder: number; isActive: boolean; rowVersion?: WorkflowRowVersion; }
export interface WorkflowTransition { id: string; code: string; nameEn: string; nameAr: string; fromStateId: string; toStateId: string; fromStateCode: string; toStateCode: string; displayOrder: number; isActive: boolean; guards: WorkflowGuard[]; actions: WorkflowAction[]; rowVersion?: WorkflowRowVersion; }
export interface WorkflowVersionDetails { id: string; workflowDefinitionId: string; versionNumber: number; lifecycleStatus: WorkflowLifecycleStatus; changeLog?: string; createdAt: string; createdBy: string; publishedAt?: string; publishedBy?: string; rowVersion?: WorkflowRowVersion; states: WorkflowState[]; transitions: WorkflowTransition[]; }
export interface WorkflowFactMetadata { code: string; nameEn: string; nameAr: string; valueType: string; }
export interface WorkflowEntityTypeMetadata { domain: WorkflowDomain; entityType: string; nameEn: string; nameAr: string; facts: WorkflowFactMetadata[]; }
export interface WorkflowManagementMetadata { entityTypes: WorkflowEntityTypeMetadata[]; guardTypes: string[]; actionTypes: string[]; }
export interface WorkflowValidationReport { isValid: boolean; errors: string[]; warnings: string[]; }
export interface WorkflowPreviewTransition { code: string; nameEn: string; nameAr: string; toStateCode: string; toStateNameEn: string; toStateNameAr: string; }
export interface WorkflowPreviewState { code: string; nameEn: string; nameAr: string; isInitial: boolean; isFinal: boolean; transitions: WorkflowPreviewTransition[]; }

export interface SaveWorkflowState { code: string; nameEn: string; nameAr: string; description?: string; displayOrder: number; isInitial: boolean; isFinal: boolean; isActive: boolean; rowVersion?: WorkflowRowVersion; }
export interface SaveWorkflowTransition { code: string; nameEn: string; nameAr: string; fromStateId: string; toStateId: string; displayOrder: number; isActive: boolean; rowVersion?: WorkflowRowVersion; }
export interface SaveWorkflowRule { guardType?: string; actionType?: string; configuration: Record<string, unknown>; executionOrder: number; isActive: boolean; rowVersion?: WorkflowRowVersion; }

export const workflowKeys = { all: ["workflows"] as const, definitions: (params?: Record<string, unknown>) => ["workflows", "definitions", params ?? {}] as const, definition: (id: string) => ["workflows", "definition", id] as const, version: (id: string) => ["workflows", "version", id] as const, metadata: ["workflows", "metadata"] as const, preview: (id: string) => ["workflows", "preview", id] as const, project: (id: string) => ["workflows", "project", id] as const };

export interface ProjectWorkflowConfiguration { id: string; projectId: string; domain: WorkflowDomain; mode: "Standard" | "Custom"; workflowDefinitionId?: string; workflowDefinitionName?: string; effectiveWorkflowVersionId?: string; effectiveWorkflowVersionNumber?: number; isPublished: boolean; effectiveBehavior?: string; configuredAt: string; configuredBy: string; rowVersion?: WorkflowRowVersion; }
export interface ProjectWorkflowSettings { projectId: string; domains: ProjectWorkflowConfiguration[]; }

export const workflowsApi = {
  list: (params?: { search?: string; isActive?: boolean; entityType?: string; domain?: WorkflowDomain }) => api.get<WorkflowDefinitionListItem[]>(endpoints.workflows.root, params),
  metadata: () => api.get<WorkflowManagementMetadata>(endpoints.workflows.metadata),
  definition: (id: string) => api.get<WorkflowDefinitionDetails>(endpoints.workflows.definition(id)),
  version: (id: string) => api.get<WorkflowVersionDetails>(endpoints.workflows.version(id)),
  create: (body: { name: string; code: string; description?: string; domain: WorkflowDomain; entityType?: string }) => api.post<WorkflowDefinitionDetails>(endpoints.workflows.root, body),
  update: (id: string, body: { name: string; description?: string; isActive: boolean; rowVersion?: WorkflowRowVersion }) => api.patch<WorkflowDefinitionDetails>(endpoints.workflows.definition(id), body),
  createDraft: (id: string, body?: { changeLog?: string; rowVersion?: WorkflowRowVersion }) => api.post<WorkflowVersionDetails>(endpoints.workflows.drafts(id), body ?? {}),
  deleteDraft: (id: string, rowVersion?: WorkflowRowVersion) => api.delete<void>(endpoints.workflows.deleteDraft(id), undefined, { rowVersion }),
  validate: (id: string) => api.post<WorkflowValidationReport>(endpoints.workflows.validate(id), {}),
  publish: (id: string, rowVersion?: WorkflowRowVersion) => api.post<WorkflowVersionDetails>(endpoints.workflows.publish(id), { rowVersion }),
  preview: (id: string) => api.get<WorkflowPreviewState[]>(endpoints.workflows.preview(id)),
  createState: (id: string, body: SaveWorkflowState) => api.post<WorkflowState>(endpoints.workflows.states(id), body),
  updateState: (id: string, body: SaveWorkflowState) => api.patch<WorkflowState>(endpoints.workflows.state(id), body),
  deleteState: (id: string, rowVersion?: WorkflowRowVersion) => api.delete<void>(endpoints.workflows.state(id), undefined, { rowVersion }),
  createTransition: (id: string, body: SaveWorkflowTransition) => api.post<WorkflowTransition>(endpoints.workflows.transitions(id), body),
  updateTransition: (id: string, body: SaveWorkflowTransition) => api.patch<WorkflowTransition>(endpoints.workflows.transition(id), body),
  deleteTransition: (id: string, rowVersion?: WorkflowRowVersion) => api.delete<void>(endpoints.workflows.transition(id), undefined, { rowVersion }),
  createGuard: (id: string, body: SaveWorkflowRule) => api.post<WorkflowGuard>(endpoints.workflows.guards(id), body),
  updateGuard: (id: string, body: SaveWorkflowRule) => api.patch<WorkflowGuard>(endpoints.workflows.guard(id), body),
  deleteGuard: (id: string, rowVersion?: WorkflowRowVersion) => api.delete<void>(endpoints.workflows.guard(id), undefined, { rowVersion }),
  createAction: (id: string, body: SaveWorkflowRule) => api.post<WorkflowAction>(endpoints.workflows.actions(id), body),
  updateAction: (id: string, body: SaveWorkflowRule) => api.patch<WorkflowAction>(endpoints.workflows.action(id), body),
  deleteAction: (id: string, rowVersion?: WorkflowRowVersion) => api.delete<void>(endpoints.workflows.action(id), undefined, { rowVersion }),
  projectSettings: (projectId: string) => api.get<ProjectWorkflowSettings>(endpoints.projects.workflows(projectId)),
  saveProjectConfiguration: (projectId: string, domain: WorkflowDomain, body: { mode: "Standard" | "Custom"; workflowDefinitionId?: string; rowVersion?: WorkflowRowVersion }) => api.put<ProjectWorkflowConfiguration>(endpoints.projects.workflow(projectId, domain), body),
};

export function useWorkflowDefinitions(params?: { search?: string; isActive?: boolean; entityType?: string; domain?: WorkflowDomain }) { return useQuery({ queryKey: workflowKeys.definitions(params), queryFn: () => workflowsApi.list(params), staleTime: 15_000 }); }
export function useWorkflowDefinition(id?: string) { return useQuery({ queryKey: workflowKeys.definition(id ?? ""), queryFn: () => workflowsApi.definition(id!), enabled: Boolean(id), staleTime: 10_000 }); }
export function useWorkflowVersion(id?: string) { return useQuery({ queryKey: workflowKeys.version(id ?? ""), queryFn: () => workflowsApi.version(id!), enabled: Boolean(id), staleTime: 5_000 }); }
export function useWorkflowMetadata() { return useQuery({ queryKey: workflowKeys.metadata, queryFn: workflowsApi.metadata, staleTime: 60_000 }); }
export function useWorkflowPreview(id?: string) { return useQuery({ queryKey: workflowKeys.preview(id ?? ""), queryFn: () => workflowsApi.preview(id!), enabled: Boolean(id), staleTime: 5_000 }); }
export function useProjectWorkflowSettings(projectId?: string) { return useQuery({ queryKey: workflowKeys.project(projectId ?? ""), queryFn: () => workflowsApi.projectSettings(projectId!), enabled: Boolean(projectId), staleTime: 10_000 }); }

export function useWorkflowManagementMutations() {
  const queryClient = useQueryClient();
  const invalidate = async (versionId?: string, definitionId?: string) => { await queryClient.invalidateQueries({ queryKey: workflowKeys.all }); if (versionId) await queryClient.invalidateQueries({ queryKey: workflowKeys.version(versionId) }); if (definitionId) await queryClient.invalidateQueries({ queryKey: workflowKeys.definition(definitionId) }); };
  return {
    create: useMutation({ mutationFn: workflowsApi.create, onSuccess: () => invalidate() }),
    update: useMutation({ mutationFn: ({ id, body }: { id: string; body: Parameters<typeof workflowsApi.update>[1] }) => workflowsApi.update(id, body), onSuccess: (_data, variables) => invalidate(undefined, variables.id) }),
    createDraft: useMutation({ mutationFn: ({ id, body }: { id: string; body?: { changeLog?: string; rowVersion?: WorkflowRowVersion } }) => workflowsApi.createDraft(id, body), onSuccess: (_data, variables) => invalidate(undefined, variables.id) }),
    validate: useMutation({ mutationFn: workflowsApi.validate }),
    publish: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: WorkflowRowVersion }) => workflowsApi.publish(id, rowVersion), onSuccess: (_data, variables) => invalidate(variables.id) }),
    createState: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowState }) => workflowsApi.createState(id, body), onSuccess: (_data, variables) => invalidate(variables.id) }),
    updateState: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowState }) => workflowsApi.updateState(id, body), onSuccess: () => invalidate() }),
    deleteState: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: WorkflowRowVersion }) => workflowsApi.deleteState(id, rowVersion), onSuccess: () => invalidate() }),
    createTransition: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowTransition }) => workflowsApi.createTransition(id, body), onSuccess: (_data, variables) => invalidate(variables.id) }),
    updateTransition: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowTransition }) => workflowsApi.updateTransition(id, body), onSuccess: () => invalidate() }),
    deleteTransition: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: WorkflowRowVersion }) => workflowsApi.deleteTransition(id, rowVersion), onSuccess: () => invalidate() }),
    createGuard: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowRule }) => workflowsApi.createGuard(id, body), onSuccess: () => invalidate() }),
    updateGuard: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowRule }) => workflowsApi.updateGuard(id, body), onSuccess: () => invalidate() }),
    deleteGuard: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: WorkflowRowVersion }) => workflowsApi.deleteGuard(id, rowVersion), onSuccess: () => invalidate() }),
    createAction: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowRule }) => workflowsApi.createAction(id, body), onSuccess: () => invalidate() }),
    updateAction: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowRule }) => workflowsApi.updateAction(id, body), onSuccess: () => invalidate() }),
    deleteAction: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: WorkflowRowVersion }) => workflowsApi.deleteAction(id, rowVersion), onSuccess: () => invalidate() }),
    saveProjectConfiguration: useMutation({ mutationFn: ({ projectId, domain, body }: { projectId: string; domain: WorkflowDomain; body: { mode: "Standard" | "Custom"; workflowDefinitionId?: string; rowVersion?: WorkflowRowVersion } }) => workflowsApi.saveProjectConfiguration(projectId, domain, body), onSuccess: (_data, variables) => queryClient.invalidateQueries({ queryKey: workflowKeys.project(variables.projectId) }) }),
  };
}
