import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export type WorkflowLifecycleStatus = "Draft" | "Published" | "Retired";
export type WorkflowDomain = "Task" | "Risk" | "Finance";
export type WorkflowRowVersion = string | number[];
export interface WorkflowDefinitionListItem { id: string; name: string; code: string; domain: WorkflowDomain; entityType: string; isActive: boolean; activePublishedVersionId?: string; activePublishedVersionNumber?: number; activePublishedLifecycleStatus?: WorkflowLifecycleStatus; versionCount: number; updatedAt?: string; rowVersion?: WorkflowRowVersion; }
export interface WorkflowVersionListItem { id: string; versionNumber: number; lifecycleStatus: WorkflowLifecycleStatus; changeLog?: string; createdAt: string; createdBy: string; publishedAt?: string; publishedBy?: string; stateCount: number; transitionCount: number; guardCount: number; actionCount: number; rowVersion?: WorkflowRowVersion; }
export interface WorkflowDefinitionDetails { id: string; name: string; code: string; description?: string; domain: WorkflowDomain; entityType: string; isActive: boolean; activePublishedVersionId?: string; rowVersion?: WorkflowRowVersion; versions: WorkflowVersionListItem[]; }
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

export const workflowKeysFull = { all: ["workflows-full"] as const, definitions: (params: Record<string, unknown>) => ["workflows-full", "definitions", params] as const, definition: (id: string) => ["workflows-full", "definition", id] as const, version: (id: string) => ["workflows-full", "version", id] as const, metadata: ["workflows-full", "metadata"] as const, preview: (id: string) => ["workflows-full", "preview", id] as const };
export const workflowsApiFull = {
  list: (params?: { search?: string; isActive?: boolean; entityType?: string; domain?: WorkflowDomain }) => api.get<WorkflowDefinitionListItem[]>(endpoints.workflows.root, params),
  metadata: () => api.get<WorkflowManagementMetadata>(endpoints.workflows.metadata),
  definition: (id: string) => api.get<WorkflowDefinitionDetails>(endpoints.workflows.definition(id)),
  version: (id: string) => api.get<WorkflowVersionDetails>(endpoints.workflows.version(id)),
  create: (body: { name: string; code: string; description?: string; domain: WorkflowDomain; entityType?: string }) => api.post<WorkflowDefinitionDetails>(endpoints.workflows.root, body),
  createDraft: (id: string, body?: { changeLog?: string; rowVersion?: WorkflowRowVersion }) => api.post<WorkflowVersionDetails>(endpoints.workflows.drafts(id), body ?? {}),
  deleteDraft: (id: string, rowVersion?: WorkflowRowVersion) => api.delete<void>(endpoints.workflows.deleteDraft(id), undefined, rowVersion === undefined ? undefined : { rowVersion: JSON.stringify(rowVersion) }),
  validate: (id: string) => api.post<WorkflowValidationReport>(endpoints.workflows.validate(id), {}),
  publish: (id: string, rowVersion?: WorkflowRowVersion) => api.post<WorkflowVersionDetails>(endpoints.workflows.publish(id), { rowVersion }),
  preview: (id: string) => api.get<WorkflowPreviewState[]>(endpoints.workflows.preview(id)),
  createState: (id: string, body: SaveWorkflowState) => api.post<WorkflowState>(endpoints.workflows.states(id), body),
  updateState: (id: string, body: SaveWorkflowState) => api.patch<WorkflowState>(endpoints.workflows.state(id), body),
  deleteState: (id: string, rowVersion?: WorkflowRowVersion) => api.delete<void>(endpoints.workflows.state(id), undefined, rowVersion === undefined ? undefined : { rowVersion: JSON.stringify(rowVersion) }),
  createTransition: (id: string, body: SaveWorkflowTransition) => api.post<WorkflowTransition>(endpoints.workflows.transitions(id), body),
  updateTransition: (id: string, body: SaveWorkflowTransition) => api.patch<WorkflowTransition>(endpoints.workflows.transition(id), body),
  deleteTransition: (id: string, rowVersion?: WorkflowRowVersion) => api.delete<void>(endpoints.workflows.transition(id), undefined, rowVersion === undefined ? undefined : { rowVersion: JSON.stringify(rowVersion) }),
  createGuard: (id: string, body: SaveWorkflowRule) => api.post<WorkflowGuard>(endpoints.workflows.guards(id), body),
  deleteGuard: (id: string, rowVersion?: WorkflowRowVersion) => api.delete<void>(endpoints.workflows.guard(id), undefined, rowVersion === undefined ? undefined : { rowVersion: JSON.stringify(rowVersion) }),
  createAction: (id: string, body: SaveWorkflowRule) => api.post<WorkflowAction>(endpoints.workflows.actions(id), body),
  deleteAction: (id: string, rowVersion?: WorkflowRowVersion) => api.delete<void>(endpoints.workflows.action(id), undefined, rowVersion === undefined ? undefined : { rowVersion: JSON.stringify(rowVersion) }),
};

export function useFullWorkflowDefinitions(params: { search?: string; isActive?: boolean; domain?: WorkflowDomain } = {}) { return useQuery({ queryKey: workflowKeysFull.definitions(params), queryFn: () => workflowsApiFull.list(params), staleTime: 15_000 }); }
export function useFullWorkflowDefinition(id?: string) { return useQuery({ queryKey: workflowKeysFull.definition(id ?? ""), queryFn: () => workflowsApiFull.definition(id!), enabled: Boolean(id), staleTime: 10_000 }); }
export function useFullWorkflowVersion(id?: string) { return useQuery({ queryKey: workflowKeysFull.version(id ?? ""), queryFn: () => workflowsApiFull.version(id!), enabled: Boolean(id), staleTime: 5_000 }); }
export function useFullWorkflowMetadata(enabled = true) { return useQuery({ queryKey: workflowKeysFull.metadata, queryFn: workflowsApiFull.metadata, enabled, staleTime: 60_000 }); }
export function useFullWorkflowPreview(id?: string) { return useQuery({ queryKey: workflowKeysFull.preview(id ?? ""), queryFn: () => workflowsApiFull.preview(id!), enabled: Boolean(id), staleTime: 5_000 }); }

export function useFullWorkflowMutations() {
  const client = useQueryClient();
  const invalidate = () => { void client.invalidateQueries({ queryKey: workflowKeysFull.all }); };
  return {
    create: useMutation({ mutationFn: workflowsApiFull.create, onSuccess: invalidate }),
    createDraft: useMutation({ mutationFn: ({ id, body }: { id: string; body?: { changeLog?: string; rowVersion?: WorkflowRowVersion } }) => workflowsApiFull.createDraft(id, body), onSuccess: invalidate }),
    deleteDraft: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: WorkflowRowVersion }) => workflowsApiFull.deleteDraft(id, rowVersion), onSuccess: invalidate }),
    validate: useMutation({ mutationFn: workflowsApiFull.validate }),
    publish: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: WorkflowRowVersion }) => workflowsApiFull.publish(id, rowVersion), onSuccess: invalidate }),
    createState: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowState }) => workflowsApiFull.createState(id, body), onSuccess: invalidate }),
    updateState: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowState }) => workflowsApiFull.updateState(id, body), onSuccess: invalidate }),
    deleteState: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: WorkflowRowVersion }) => workflowsApiFull.deleteState(id, rowVersion), onSuccess: invalidate }),
    createTransition: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowTransition }) => workflowsApiFull.createTransition(id, body), onSuccess: invalidate }),
    updateTransition: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowTransition }) => workflowsApiFull.updateTransition(id, body), onSuccess: invalidate }),
    deleteTransition: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: WorkflowRowVersion }) => workflowsApiFull.deleteTransition(id, rowVersion), onSuccess: invalidate }),
    createGuard: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowRule }) => workflowsApiFull.createGuard(id, body), onSuccess: invalidate }),
    deleteGuard: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: WorkflowRowVersion }) => workflowsApiFull.deleteGuard(id, rowVersion), onSuccess: invalidate }),
    createAction: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkflowRule }) => workflowsApiFull.createAction(id, body), onSuccess: invalidate }),
    deleteAction: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion?: WorkflowRowVersion }) => workflowsApiFull.deleteAction(id, rowVersion), onSuccess: invalidate }),
  };
}
