import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

type PagedResult<T> = { items: T[]; totalCount: number; page?: number; pageSize?: number; totalPages?: number };

export type WorkItemType = "Task" | "Issue" | "Subtask" | "Bug" | "Feature" | "Epic" | "Story" | "Ticket" | "TestCase";
export type WorkItemPriority = "Critical" | "High" | "Medium" | "Low" | "Backlog";
export type WorkflowRowVersion = string | number[];
export interface WorkItemDto { id: string; title: string; description?: string; type: WorkItemType; priority: WorkItemPriority; status: string; source: string; projectId?: string; projectName?: string; assignedResourceId?: string; assignedResourceName?: string; assignedTeamId?: string; assignedTeamName?: string; reporterId?: string; parentId?: string; storyPoints?: number; estimatedHours?: number; actualHours?: number; startDate?: string; dueDate?: string; completedAt?: string; severity?: string; identifiedDate?: string; targetResolutionDate?: string; resolvedDate?: string; resolution?: string; tags?: string; labels?: string; createdAt: string; updatedAt?: string; createdBy: string; updatedBy?: string; rowVersion?: number[]; }
export interface WorkItemAssigneeDto { targetType: "Resource" | "Team"; targetId: string; resourceId?: string; teamId?: string; name: string; email?: string; jobTitle?: string; }
export interface WorkItemActivityDto { id: string; action: string; changes?: string; userId?: string; userName?: string; timestamp: string; }
export interface WorkItemInsightBucketDto { key: string; count: number; }
export interface WorkItemInsightsDto { total: number; open: number; overdue: number; unassigned: number; dueThisWeek: number; byStatus: WorkItemInsightBucketDto[]; byPriority: WorkItemInsightBucketDto[]; }
export interface WorkItemCommentDto { id: string; workItemId: string; parentCommentId?: string; commentText: string; createdBy: string; authorName?: string; createdAt: string; }
export interface CreateWorkItemCommentDto { commentText: string; parentCommentId?: string; }
export interface WorkItemAttachmentDto { id: string; workItemId: string; fileName: string; contentType: string; fileSize: number; createdBy: string; createdAt: string; }
export interface SaveWorkItemDto { title: string; description?: string; type: WorkItemType; priority: WorkItemPriority; projectId?: string; assignedResourceId?: string; assignedTeamId?: string; parentId?: string; storyPoints?: number; estimatedHours?: number; actualHours?: number; startDate?: string; dueDate?: string; severity?: string; targetResolutionDate?: string; resolvedDate?: string; resolution?: string; tags?: string; labels?: string; rowVersion?: number[]; }
export interface WorkflowTransitionOption { code: string; nameEn: string; nameAr: string; targetStateCode: string; targetStateNameEn: string; targetStateNameAr: string; displayOrder: number; canExecute: boolean; denialReason?: string; }
export interface WorkflowTransitionsResponse { currentStateCode: string; currentStateNameEn: string; currentStateNameAr: string; isFinalState: boolean; transitions: WorkflowTransitionOption[]; currentRowVersion?: WorkflowRowVersion; }
export interface WorkflowTransitionResult { success: boolean; instanceId?: string; fromStateCode?: string; fromStateNameEn?: string; fromStateNameAr?: string; toStateCode?: string; toStateNameEn?: string; toStateNameAr?: string; denialCode?: string; denialMessage?: string; newRowVersion?: WorkflowRowVersion; }

export const workItemKeys = {
  all: ["work-items"] as const,
  list: (params: Record<string, unknown>) => ["work-items", "list", params] as const,
  insights: (params: Record<string, unknown>) => ["work-items", "insights", params] as const,
  detail: (id: string) => ["work-items", "detail", id] as const,
  activity: (id: string, page = 1, pageSize = 20) => ["work-items", "activity", id, page, pageSize] as const,
  comments: (id: string, page = 1, pageSize = 20) => ["work-items", "comments", id, page, pageSize] as const,
  attachments: (id: string) => ["work-items", "attachments", id] as const,
};

export const workItemsApiFull = {
  list: (params?: Record<string, string | number | boolean | undefined>) => api.get<PagedResult<WorkItemDto>>(endpoints.workItems.root, params),
  insights: (params?: Record<string, string | number | boolean | undefined>) => api.get<WorkItemInsightsDto>(`${endpoints.workItems.root}/insights`, params),
  assignees: (projectId: string) => api.get<WorkItemAssigneeDto[]>(endpoints.workItems.assignees(projectId)),
  detail: (id: string) => api.get<WorkItemDto>(endpoints.workItems.detail(id)),
  activity: (id: string, page = 1, pageSize = 20) => api.get<PagedResult<WorkItemActivityDto>>(endpoints.workItems.activity(id), { page, pageSize }),
  comments: (id: string, page = 1, pageSize = 20) => api.get<PagedResult<WorkItemCommentDto>>(endpoints.workItems.comments(id), { page, pageSize }),
  addComment: (id: string, body: CreateWorkItemCommentDto) => api.post<WorkItemCommentDto>(endpoints.workItems.comments(id), body),
  attachments: (id: string) => api.get<WorkItemAttachmentDto[]>(endpoints.workItems.attachments(id)),
  uploadAttachments: (id: string, files: File[]) => { const form = new FormData(); files.forEach((file) => form.append("files", file, file.name)); return api.upload<WorkItemAttachmentDto[]>(endpoints.workItems.attachments(id), form); },
  downloadAttachment: (id: string, attachmentId: string) => api.download<Blob>(endpoints.workItems.attachmentContent(id, attachmentId)),
  deleteAttachment: (id: string, attachmentId: string) => api.delete<void>(endpoints.workItems.attachment(id, attachmentId)),
  create: (body: SaveWorkItemDto) => api.post<WorkItemDto>(endpoints.workItems.root, body),
  update: (id: string, body: SaveWorkItemDto) => api.put<WorkItemDto>(endpoints.workItems.detail(id), body),
  remove: (id: string) => api.delete<void>(endpoints.workItems.detail(id)),
  workflowTransitions: (id: string) => api.get<WorkflowTransitionsResponse>(endpoints.workItems.workflowTransitions(id)),
  executeWorkflowTransition: (id: string, transitionCode: string, body: { comment?: string; expectedStateCode?: string; expectedRowVersion?: WorkflowRowVersion }) => api.post<WorkflowTransitionResult>(endpoints.workItems.workflowTransition(id, transitionCode), body),
};

export function useFullWorkItems(params: Record<string, string | number | boolean | undefined> = {}, enabled = true) { return useQuery({ queryKey: workItemKeys.list(params), queryFn: () => workItemsApiFull.list(params), enabled, staleTime: 15_000 }); }
export function useFullWorkItemInsights(params: Record<string, string | number | boolean | undefined> = {}, enabled = true) { return useQuery({ queryKey: workItemKeys.insights(params), queryFn: () => workItemsApiFull.insights(params), enabled, staleTime: 15_000 }); }
export function useFullWorkItem(id?: string, enabled = true) { return useQuery({ queryKey: workItemKeys.detail(id ?? ""), queryFn: () => workItemsApiFull.detail(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useFullWorkItemAssignees(projectId?: string, enabled = true) { return useQuery({ queryKey: ["work-items", "assignees", projectId ?? ""], queryFn: () => workItemsApiFull.assignees(projectId!), enabled: Boolean(projectId) && enabled, staleTime: 15_000 }); }
export function useFullWorkItemActivity(id?: string, enabled = true, page = 1, pageSize = 50) { return useQuery({ queryKey: workItemKeys.activity(id ?? "", page, pageSize), queryFn: () => workItemsApiFull.activity(id!, page, pageSize), enabled: Boolean(id) && enabled, placeholderData: (previousData) => previousData }); }
export function useFullWorkItemComments(id?: string, enabled = true, page = 1, pageSize = 50) { return useQuery({ queryKey: workItemKeys.comments(id ?? "", page, pageSize), queryFn: () => workItemsApiFull.comments(id!, page, pageSize), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useFullWorkItemAttachments(id?: string, enabled = true) { return useQuery({ queryKey: workItemKeys.attachments(id ?? ""), queryFn: () => workItemsApiFull.attachments(id!), enabled: Boolean(id) && enabled, staleTime: 15_000 }); }
export function useFullWorkItemWorkflow(id?: string, enabled = true) {
  const client = useQueryClient();
  const query = useQuery({ queryKey: ["work-items", "workflow", id ?? ""], queryFn: () => workItemsApiFull.workflowTransitions(id!), enabled: Boolean(id) && enabled, staleTime: 5_000 });
  const execute = useMutation({ mutationFn: ({ transitionCode, comment }: { transitionCode: string; comment?: string }) => workItemsApiFull.executeWorkflowTransition(id!, transitionCode, { comment, expectedStateCode: query.data?.currentStateCode, expectedRowVersion: query.data?.currentRowVersion }), onSuccess: async () => { await client.invalidateQueries({ queryKey: workItemKeys.all }); await client.invalidateQueries({ queryKey: ["work-items", "workflow", id ?? ""] }); } });
  return { ...query, execute, currentState: query.data?.currentStateCode, availableTransitions: query.data?.transitions ?? [] };
}
export function useFullWorkItemMutations() {
  const client = useQueryClient();
  return {
    create: useMutation({ mutationFn: workItemsApiFull.create, onSuccess: () => client.invalidateQueries({ queryKey: workItemKeys.all }) }),
    update: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkItemDto }) => workItemsApiFull.update(id, body), onSuccess: (_data, variables) => { void client.invalidateQueries({ queryKey: workItemKeys.all }); void client.invalidateQueries({ queryKey: workItemKeys.detail(variables.id) }); } }),
    remove: useMutation({ mutationFn: workItemsApiFull.remove, onSuccess: () => client.invalidateQueries({ queryKey: workItemKeys.all }) }),
  };
}

export function useFullWorkItemCommentMutation(id: string) { const client = useQueryClient(); return useMutation({ mutationFn: (body: CreateWorkItemCommentDto) => workItemsApiFull.addComment(id, body), onSuccess: () => client.invalidateQueries({ queryKey: ["work-items", "comments", id] }) }); }
export function useFullWorkItemUploadMutation(id: string) { const client = useQueryClient(); return useMutation({ mutationFn: (files: File[]) => workItemsApiFull.uploadAttachments(id, files), onSuccess: () => client.invalidateQueries({ queryKey: workItemKeys.attachments(id) }) }); }
export function useFullWorkItemDeleteAttachment(id: string) { const client = useQueryClient(); return useMutation({ mutationFn: (attachmentId: string) => workItemsApiFull.deleteAttachment(id, attachmentId), onSuccess: () => client.invalidateQueries({ queryKey: workItemKeys.attachments(id) }) }); }
