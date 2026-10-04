import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

export type WorkItemType = "Task" | "Issue" | "Subtask" | "Bug" | "Feature" | "Epic" | "Story" | "Ticket" | "TestCase";
export type WorkItemPriority = "Critical" | "High" | "Medium" | "Low" | "Backlog";
export type WorkItemStatus = string;
export type WorkflowRowVersion = string | number[];

export interface WorkItemDto {
  id: string;
  title: string;
  description?: string;
  type: WorkItemType;
  priority: WorkItemPriority;
  status: WorkItemStatus;
  source: string;
  projectId?: string;
  projectName?: string;
  assignedResourceId?: string;
  assignedResourceName?: string;
  assignedTeamId?: string;
  assignedTeamName?: string;
  reporterId?: string;
  parentId?: string;
  storyPoints?: number;
  estimatedHours?: number;
  actualHours?: number;
  startDate?: string;
  dueDate?: string;
  completedAt?: string;
  severity?: string;
  identifiedDate?: string;
  targetResolutionDate?: string;
  resolvedDate?: string;
  resolution?: string;
  tags?: string;
  labels?: string;
  createdAt: string;
  updatedAt?: string;
  createdBy: string;
  updatedBy?: string;
  rowVersion?: number[];
}

export interface WorkItemAssigneeDto {
  targetType: "Resource" | "Team";
  targetId: string;
  resourceId?: string;
  teamId?: string;
  name: string;
  email?: string;
  jobTitle?: string;
}

export interface WorkItemActivityDto {
  id: string;
  action: string;
  changes?: string;
  userId?: string;
  userName?: string;
  timestamp: string;
}

export interface WorkItemInsightBucketDto {
  key: string;
  count: number;
}

export interface WorkItemInsightsDto {
  total: number;
  open: number;
  overdue: number;
  unassigned: number;
  dueThisWeek: number;
  byStatus: WorkItemInsightBucketDto[];
  byPriority: WorkItemInsightBucketDto[];
}

export interface WorkItemCommentDto {
  id: string;
  workItemId: string;
  parentCommentId?: string;
  commentText: string;
  createdBy: string;
  authorName?: string;
  createdAt: string;
}

export interface CreateWorkItemCommentDto {
  commentText: string;
  parentCommentId?: string;
}

export interface WorkItemAttachmentDto {
  id: string;
  workItemId: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  createdBy: string;
  createdAt: string;
}

export interface SaveWorkItemDto {
  title: string;
  description?: string;
  type: WorkItemType;
  priority: WorkItemPriority;
  projectId?: string;
  assignedResourceId?: string;
  assignedTeamId?: string;
  parentId?: string;
  storyPoints?: number;
  estimatedHours?: number;
  actualHours?: number;
  startDate?: string;
  dueDate?: string;
  severity?: string;
  targetResolutionDate?: string;
  resolvedDate?: string;
  resolution?: string;
  tags?: string;
  labels?: string;
  rowVersion?: number[];
}

export interface WorkflowTransitionOption { code: string; nameEn: string; nameAr: string; targetStateCode: string; targetStateNameEn: string; targetStateNameAr: string; displayOrder: number; canExecute: boolean; denialReason?: string; }
export interface WorkflowTransitionsResponse { currentStateCode: string; currentStateNameEn: string; currentStateNameAr: string; isFinalState: boolean; transitions: WorkflowTransitionOption[]; currentRowVersion?: WorkflowRowVersion; }
export interface WorkflowTransitionResult { success: boolean; instanceId?: string; fromStateCode?: string; fromStateNameEn?: string; fromStateNameAr?: string; toStateCode?: string; toStateNameEn?: string; toStateNameAr?: string; denialCode?: string; denialMessage?: string; newRowVersion?: WorkflowRowVersion; }

export const workItemKeys = {
  all: ["work-items"] as const,
  list: (params: Record<string, unknown>) => ["work-items", "list", params] as const,
  detail: (id: string) => ["work-items", "detail", id] as const,
  activity: (id: string, page = 1, pageSize = 20) => ["work-items", "activity", id, page, pageSize] as const,
  comments: (id: string, page = 1, pageSize = 20) => ["work-items", "comments", id, page, pageSize] as const,
  attachments: (id: string) => ["work-items", "attachments", id] as const,
};

export const workItemsApi = {
  list: (params?: Record<string, string | number | boolean | undefined>) => api.get<PagedResult<WorkItemDto>>(endpoints.workItems.root, params),
  assignees: (projectId: string) => api.get<WorkItemAssigneeDto[]>(endpoints.workItems.assignees(projectId)),
  insights: (params?: Record<string, string | number | boolean | undefined>) => api.get<WorkItemInsightsDto>(`${endpoints.workItems.root}/insights`, params),
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
  delete: (id: string) => api.delete<void>(endpoints.workItems.detail(id)),
  workflowTransitions: (id: string) => api.get<WorkflowTransitionsResponse>(endpoints.workItems.workflowTransitions(id)),
  executeWorkflowTransition: (id: string, transitionCode: string, body: { comment?: string; expectedStateCode?: string; expectedRowVersion?: WorkflowRowVersion }) => api.post<WorkflowTransitionResult>(endpoints.workItems.workflowTransition(id, transitionCode), body),
};

export function useWorkItems(params: Record<string, string | number | boolean | undefined> = {}, enabled = true) {
  return useQuery({ queryKey: workItemKeys.list(params), queryFn: () => workItemsApi.list(params), enabled, staleTime: 15_000 });
}

export function useWorkItemAssignees(projectId?: string, enabled = true) {
  return useQuery({
    queryKey: ["work-items", "assignees", projectId ?? ""],
    queryFn: () => workItemsApi.assignees(projectId!),
    enabled: Boolean(projectId) && enabled,
    staleTime: 15_000,
  });
}

export function useWorkItemInsights(params: Record<string, string | number | boolean | undefined> = {}, enabled = true) {
  return useQuery({ queryKey: ["work-items", "insights", params], queryFn: () => workItemsApi.insights(params), enabled, staleTime: 15_000 });
}

export function useWorkItem(id?: string) {
  return useQuery({ queryKey: workItemKeys.detail(id ?? ""), queryFn: () => workItemsApi.detail(id!), enabled: !!id });
}

export function useWorkItemWorkflow(id?: string, enabled = true) {
  const queryClient = useQueryClient();
  const transitions = useQuery({ queryKey: ["work-items", "workflow", id ?? ""], queryFn: () => workItemsApi.workflowTransitions(id!), enabled: Boolean(id) && enabled, staleTime: 5_000 });
  const execute = useMutation({
    mutationFn: ({ transitionCode, comment }: { transitionCode: string; comment?: string }) => workItemsApi.executeWorkflowTransition(id!, transitionCode, { comment, expectedStateCode: transitions.data?.currentStateCode, expectedRowVersion: transitions.data?.currentRowVersion }),
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: workItemKeys.all }); await queryClient.invalidateQueries({ queryKey: ["work-items", "workflow", id ?? ""] }); },
  });
  return { ...transitions, execute, currentState: transitions.data?.currentStateCode, availableTransitions: transitions.data?.transitions ?? [] };
}

export function useWorkItemWorkflowTransition() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ itemId, targetStateCode, transitionCode }: { itemId: string; targetStateCode: string; transitionCode?: string }) => {
      const available = await workItemsApi.workflowTransitions(itemId);
      const candidates = available.transitions.filter((transition) => transition.targetStateCode === targetStateCode && transition.canExecute);
      if (candidates.length === 0) throw new Error(available.transitions.find((transition) => transition.targetStateCode === targetStateCode)?.denialReason || "Transition unavailable");
      const selected = transitionCode ? candidates.find((candidate) => candidate.code === transitionCode) : candidates.length === 1 ? candidates[0] : undefined;
      if (!selected) throw new Error("Multiple transitions are available for this target state. Select a transition.");
      return workItemsApi.executeWorkflowTransition(itemId, selected.code, { expectedStateCode: available.currentStateCode, expectedRowVersion: available.currentRowVersion });
    },
    onSuccess: async (_result, variables) => { await queryClient.invalidateQueries({ queryKey: workItemKeys.all }); await queryClient.invalidateQueries({ queryKey: ["work-items", "workflow", variables.itemId] }); },
  });
}

export function useWorkItemActivity(id?: string, page = 1, pageSize = 20, enabled = true) {
  return useQuery({ queryKey: workItemKeys.activity(id ?? "", page, pageSize), queryFn: () => workItemsApi.activity(id!, page, pageSize), enabled: !!id && enabled, placeholderData: (previousData) => previousData });
}

export function useWorkItemComments(id?: string, enabled = true, page = 1, pageSize = 20) {
  return useQuery({ queryKey: workItemKeys.comments(id ?? "", page, pageSize), queryFn: () => workItemsApi.comments(id!, page, pageSize), enabled: !!id && enabled, placeholderData: (previousData) => previousData, staleTime: 15_000 });
}

export function useWorkItemCommentMutation(id: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (body: CreateWorkItemCommentDto) => workItemsApi.addComment(id, body), onSuccess: () => queryClient.invalidateQueries({ queryKey: ["work-items", "comments", id] }) });
}

export function useWorkItemAttachments(id?: string, enabled = true) {
  return useQuery({ queryKey: workItemKeys.attachments(id ?? ""), queryFn: () => workItemsApi.attachments(id!), enabled: !!id && enabled, staleTime: 15_000 });
}

export function useWorkItemAttachmentMutation(id: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (files: File[]) => workItemsApi.uploadAttachments(id, files), onSuccess: () => queryClient.invalidateQueries({ queryKey: workItemKeys.attachments(id), exact: true }) });
}

export function useDeleteWorkItemAttachment(id: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (attachmentId: string) => workItemsApi.deleteAttachment(id, attachmentId), onSuccess: () => queryClient.invalidateQueries({ queryKey: workItemKeys.attachments(id), exact: true }) });
}

export function useWorkItemMutations() {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: workItemKeys.all });
  return {
    create: useMutation({ mutationFn: workItemsApi.create, onSuccess: invalidate }),
    update: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkItemDto }) => workItemsApi.update(id, body), onSuccess: invalidate }),
    remove: useMutation({ mutationFn: workItemsApi.delete, onSuccess: invalidate }),
  };
}
