import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { documentKeys } from "./documents";
import { notificationKeys } from "@api/notifications";

export type TemplateDeletionInboxStatus = "PendingApproval" | "History";

export interface TemplateDeletionRequestDto {
  id: string;
  templateId: string;
  templateName: string;
  ownerOrganizationId?: string | null;
  ownerScope: string;
  requesterId: string;
  requesterName?: string | null;
  reason: string;
  status: string;
  requestedAt: string;
  approverId?: string | null;
  approverName?: string | null;
  decidedAt?: string | null;
  decisionComments?: string | null;
  deletionType?: string | null;
}

export interface TemplateDeletionRequestPage {
  items: TemplateDeletionRequestDto[];
  page?: number;
  pageSize?: number;
  totalCount?: number;
  totalPages?: number;
}

type RequestListResponse = TemplateDeletionRequestDto[] | TemplateDeletionRequestPage;

export const templateDeletionKeys = {
  all: ["templateDeletionRequests"] as const,
  byTemplate: (templateId: string) => ["templateDeletionRequests", "template", templateId] as const,
  inbox: (status: TemplateDeletionInboxStatus) => ["templateDeletionRequests", "inbox", status] as const,
};

function requestPage(response: RequestListResponse): TemplateDeletionRequestPage {
  if (Array.isArray(response)) {
    return { items: response, page: 1, pageSize: response.length, totalCount: response.length, totalPages: 1 };
  }
  const pageSize = response.pageSize || Math.max(response.items.length, 1);
  const totalCount = response.totalCount ?? response.items.length;
  return { ...response, totalPages: response.totalPages ?? Math.max(1, Math.ceil(totalCount / pageSize)) };
}

export const templateDeletionApi = {
  create: (input: { templateId: string; reason: string }) =>
    api.post<TemplateDeletionRequestDto>(endpoints.templateDeletionRequests.root, input),
  byTemplate: async (templateId: string, page = 1, pageSize = 20) => requestPage(await api.get<RequestListResponse>(endpoints.templateDeletionRequests.root, { templateId, page, pageSize })),
  inbox: async (status: TemplateDeletionInboxStatus, page = 1, pageSize = 100) => requestPage(await api.get<RequestListResponse>(endpoints.templateDeletionRequests.inbox, { status, page, pageSize })),
  approve: ({ requestId, comments }: { requestId: string; comments: string }) =>
    api.post<TemplateDeletionRequestDto>(endpoints.templateDeletionRequests.approve(requestId), { comments }),
  reject: ({ requestId, comments }: { requestId: string; comments: string }) =>
    api.post<TemplateDeletionRequestDto>(endpoints.templateDeletionRequests.reject(requestId), { comments }),
};

function invalidateTemplateDeletionContext(client: ReturnType<typeof useQueryClient>) {
  void client.invalidateQueries({ queryKey: templateDeletionKeys.all });
  void client.invalidateQueries({ queryKey: documentKeys.all });
  void client.invalidateQueries({ queryKey: ["projects", "phase-gate"] });
  void client.invalidateQueries({ queryKey: notificationKeys.all });
}

export function useTemplateDeletionRequests(templateId: string | null, enabled = true, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: [...templateDeletionKeys.byTemplate(templateId ?? ""), page, pageSize],
    queryFn: () => templateDeletionApi.byTemplate(templateId!, page, pageSize),
    enabled: enabled && Boolean(templateId),
    staleTime: 15_000,
  });
}

export function useTemplateDeletionInbox(status: TemplateDeletionInboxStatus, enabled = true, page = 1, pageSize = 100) {
  return useQuery({
    queryKey: [...templateDeletionKeys.inbox(status), page, pageSize],
    queryFn: () => templateDeletionApi.inbox(status, page, pageSize),
    enabled,
    staleTime: 15_000,
  });
}

export function useRequestTemplateDeletion() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: templateDeletionApi.create,
    onSuccess: (_request, input) => {
      invalidateTemplateDeletionContext(client);
      void client.invalidateQueries({ queryKey: documentKeys.detail(input.templateId) });
    },
  });
}

export function useApproveTemplateDeletion() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: templateDeletionApi.approve,
    onSuccess: () => invalidateTemplateDeletionContext(client),
  });
}

export function useRejectTemplateDeletion() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: templateDeletionApi.reject,
    onSuccess: () => invalidateTemplateDeletionContext(client),
  });
}
