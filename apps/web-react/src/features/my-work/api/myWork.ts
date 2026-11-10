import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { useManagement } from "@api/management";
import { api } from "@api/client";
import { projectKeys } from "@features/projects/api/projects";
import { invalidateManagerHandoverEntity } from "@features/delivery/api/managerHandovers";
import type { ManagerHandoverRequestDto } from "@features/delivery/api/managerHandovers";
import { APPROVAL_REVIEW_CONTRACTS, getApprovalReviewApiPath, type ApprovalReviewType } from "@lib/approval-navigation";
import { MANAGEMENT_PERSPECTIVES } from "@lib/perspective";
export type { ManagerHandoverRequestDto } from "@features/delivery/api/managerHandovers";

export interface TimeEntryDto {
  id: string;
  projectId: string;
  projectName: string;
  projectCode: string;
  workDate: string;
  startTime: string;
  endTime: string;
  actualHours: number;
  description?: string;
  rowVersion: number[];
}

export interface TimeEntrySummary {
  actualHours: number;
  plannedHours: number;
  workVarianceHours: number;
  actualUtilization: number;
  byTeam: { id: string; label: string; actualHours: number }[];
}

export interface TimeEntryParams {
  page?: number;
  pageSize?: number;
  from?: string;
  to?: string;
  projectId?: string;
  [key: string]: string | number | boolean | undefined;
}

const timeEntryKeys = {
  all: ["timeEntries"] as const,
  lists: () => [...timeEntryKeys.all, "list"] as const,
  list: (params: TimeEntryParams) => [...timeEntryKeys.lists(), params] as const,
  summary: (params: { from: string; to: string }) => [...timeEntryKeys.all, "summary", params] as const,
};

export function useTimeEntries(params: TimeEntryParams = {}, enabled = true) {
  return useQuery({
    queryKey: timeEntryKeys.list(params),
    queryFn: () => api.get<{ items: TimeEntryDto[]; totalCount: number; page: number; pageSize: number; totalPages: number }>("/api/time-entries", params),
    enabled,
    staleTime: 30_000,
  });
}

export function useTimeEntrySummary(params: { from: string; to: string }, enabled = true, staleTime = 30_000) {
  return useQuery({
    queryKey: timeEntryKeys.summary(params),
    queryFn: () => api.get<TimeEntrySummary>("/api/time-entries/summary", params),
    enabled,
    staleTime,
  });
}

export function useCreateTimeEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { projectId: string; workDate: string; startTime: string; endTime: string; description?: string }) =>
      api.post<TimeEntryDto>("/api/time-entries", data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.all });
    },
  });
}

export function useUpdateTimeEntry(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { projectId: string; workDate: string; startTime: string; endTime: string; description?: string; rowVersion: number[] }) =>
      api.put<TimeEntryDto>(`/api/time-entries/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.all });
    },
  });
}

export function useDeleteTimeEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/api/time-entries/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: timeEntryKeys.all });
    },
  });
}

export interface HandoverRequestDto {
  id: string;
  projectId: string;
  projectPublicId?: string | null;
  projectName: string;
  projectCode: string;
  currentProjectManagerId: string | null;
  currentProjectManagerName: string;
  newProjectManagerId: string;
  newProjectManagerName: string;
  effectiveHandoverDate: string;
  reason: string;
  notes?: string;
  status: string;
  initiationType: "ManagerInitiated" | "AdminInitiated" | "InitialAssignment";
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
  respondedBy?: string;
  respondedByName?: string;
  respondedAt?: string;
  rejectionReason?: string;
}

export type { ProjectChangeRequestDto } from "@features/projects/api/projects";

export interface PendingProjectChangeApprovalDto {
  id: string;
  status: string;
  projectName: string;
  number: number;
  type: "Baseline" | "Administrative";
  reason: string;
  requestedByName: string;
  requestedAt: string;
  approvalCount: number;
  requiredApprovalCount: number;
}

export interface PendingAnnualBudgetApprovalDto {
  requestId: string;
  budgetId: string;
  projectPublicId: string;
  projectName: string;
  projectCode: string;
  fiscalYear: number;
  financialReference: string;
  amount: number;
  currency: string;
  approvalMode: "Sequential" | "Parallel";
  requestedAtUtc: string;
  actionableAtUtc: string;
  requestedByName: string;
}

const pendingAnnualBudgetApprovalSchema = z.array(z.object({
  requestId: z.string().min(1),
  budgetId: z.string().min(1),
  projectPublicId: z.string().uuid(),
  projectName: z.string().min(1),
  projectCode: z.string(),
  fiscalYear: z.number().int().positive(),
  financialReference: z.string(),
  amount: z.number().finite(),
  currency: z.string().min(1),
  approvalMode: z.enum(["Sequential", "Parallel"]),
  requestedAtUtc: z.string().min(1),
  actionableAtUtc: z.string().min(1),
  requestedByName: z.string(),
}));

export interface ProjectChangeRequestDecisionDto {
  id: string;
  number: number;
  status: string;
}

export interface ApprovalReviewDto {
  approvalId: string;
  approvalType: string;
  businessObjectType: string;
  businessObjectPublicId?: string | null;
  businessObjectName: string;
  businessObjectCode?: string | null;
  businessObjectStatus?: string | null;
  title: string;
  reason?: string | null;
  notes?: string | null;
  requestedByName: string;
  requestedAt: string;
  status: string;
  effectiveDate?: string | null;
  currentAssigneeName?: string | null;
  proposedAssigneeName?: string | null;
  changeNumber?: number | null;
  changeType?: string | null;
  justification?: string | null;
  impact?: string | null;
  currentAmount?: number | null;
  amountChange?: number | null;
  proposedAmount?: number | null;
  currency?: string | null;
  isReadOnly: boolean;
  canDecide: boolean;
}

const approvalReviewDtoSchema = z.looseObject({
  approvalId: z.string().min(1),
  approvalType: z.enum(["ProjectHandover", "ProgramHandover", "PortfolioHandover", "ChangeRequest"]),
  businessObjectType: z.enum(["Project", "Program", "Portfolio"]),
  businessObjectPublicId: z.string().nullable().optional(),
  businessObjectName: z.string(),
  businessObjectCode: z.string().nullable().optional(),
  businessObjectStatus: z.string().nullable().optional(),
  title: z.string(),
  reason: z.string().nullable().optional(),
  notes: z.string().nullable().optional(),
  requestedByName: z.string(),
  requestedAt: z.string().min(1),
  status: z.string(),
  effectiveDate: z.string().nullable().optional(),
  currentAssigneeName: z.string().nullable().optional(),
  proposedAssigneeName: z.string().nullable().optional(),
  changeNumber: z.number().int().nullable().optional(),
  changeType: z.string().nullable().optional(),
  justification: z.string().nullable().optional(),
  impact: z.string().nullable().optional(),
  currentAmount: z.number().nullable().optional(),
  amountChange: z.number().nullable().optional(),
  proposedAmount: z.number().nullable().optional(),
  currency: z.string().nullable().optional(),
  isReadOnly: z.boolean(),
  canDecide: z.boolean(),
});

const managerHandoverRoutingSchema = z.array(z.looseObject({
  id: z.string().uuid(),
  entityType: z.enum(["Program", "Portfolio"]),
})).max(1000);

export function useApprovalReview(type: ApprovalReviewType, approvalId: string) {
  return useQuery({
    queryKey: ["approvalReview", type, approvalId],
    queryFn: async () => {
      const response = await api.get<unknown>(getApprovalReviewApiPath(type, approvalId));
      const review = approvalReviewDtoSchema.parse(response);
      const contract = APPROVAL_REVIEW_CONTRACTS[type];
      if (review.approvalId.toLowerCase() !== approvalId.toLowerCase()
        || review.approvalType !== contract.approvalType
        || review.businessObjectType !== contract.businessObjectType) {
        throw new Error("Approval review response does not match the requested approval type.");
      }
      return review as ApprovalReviewDto;
    },
    enabled: Boolean(approvalId),
    retry: false,
    staleTime: 0,
  });
}

export function usePendingHandoverRequestsForMe(enabled = true) {
  return useQuery({
    queryKey: ["handovers", "pendingForMe"],
    queryFn: () => api.get<HandoverRequestDto[]>("/api/handover-requests/pending-for-me"),
    enabled,
    staleTime: 15_000,
  });
}

export function usePendingManagerHandovers(enabled = true) {
  return useQuery({
    queryKey: ["managerHandovers", "pendingForMe"],
    queryFn: async () => {
      const response = await api.get<unknown>("/api/manager-handovers/pending-for-me");
      return managerHandoverRoutingSchema.parse(response) as unknown as ManagerHandoverRequestDto[];
    },
    enabled,
    staleTime: 15_000,
  });
}

export function usePendingProjectChangeRequests(enabled = true) {
  return useQuery({
    queryKey: ["projectChangeRequests", "pendingForMe"],
    queryFn: () => api.get<PendingProjectChangeApprovalDto[]>("/api/project-change-requests/pending-for-me"),
    enabled,
    staleTime: 15_000,
  });
}

export function usePendingAnnualBudgetApprovals(enabled = true) {
  return useQuery({
    queryKey: ["annualBudgetApprovals", "pendingForMe"],
    queryFn: async () => pendingAnnualBudgetApprovalSchema.parse(
      await api.get<unknown>("/api/my-work/annual-budget-approvals"),
    ) as PendingAnnualBudgetApprovalDto[],
    enabled,
    staleTime: 15_000,
  });
}

export function useAcceptHandoverRequest() {
  const queryClient = useQueryClient();
  const management = useManagement();
  return useMutation({
    mutationFn: (requestId: string) => api.post<HandoverRequestDto>(`/api/handover-requests/${requestId}/accept`, {}),
    onSuccess: async (request) => {
      // Acceptance may add a project outside the currently selected PM context.
      // Drop that single-project context so the refreshed native scope includes
      // every project assigned to this manager, including future-effective work
      // once the scheduler applies it.
      if (management.context?.perspective === MANAGEMENT_PERSPECTIVES.ProjectManager
        && management.context.viewMode === "Native") {
        management.setContext({ perspective: MANAGEMENT_PERSPECTIVES.ProjectManager, viewMode: "Native" });
      }
      await management.refreshSnapshot();

      // Project endpoints use PublicId in their route/query keys; projectId on
      // the handover DTO is the internal relationship key.
      const projectRouteId = request.projectPublicId || request.projectId;
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["projects", "detail", projectRouteId], exact: true }),
        queryClient.invalidateQueries({ queryKey: ["projects", "shell", projectRouteId], exact: true }),
        queryClient.invalidateQueries({ queryKey: ["projects", "overview", projectRouteId], exact: true }),
        queryClient.invalidateQueries({ queryKey: ["projects", "list"] }),
        queryClient.invalidateQueries({ queryKey: ["projects", "people", projectRouteId] }),
        queryClient.invalidateQueries({ queryKey: ["projects", "team", projectRouteId] }),
        queryClient.invalidateQueries({ queryKey: ["projects", "history", projectRouteId] }),
        queryClient.invalidateQueries({ queryKey: ["projects", "snapshots", projectRouteId] }),
        queryClient.invalidateQueries({ queryKey: ["projects", "handovers", projectRouteId] }),
        queryClient.invalidateQueries({ queryKey: projectKeys.handoverManagers(projectRouteId) }),
      ]);
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["handovers", "pendingForMe"] }),
  });
}

export function useRejectHandoverRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ requestId, rejectionReason }: { requestId: string; rejectionReason: string }) =>
      api.post<HandoverRequestDto>(`/api/handover-requests/${requestId}/reject`, { rejectionReason }),
    onSuccess: (request) => queryClient.invalidateQueries({ queryKey: ["projects", "handovers", request.projectId] }),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["handovers", "pendingForMe"] }),
  });
}

export function useAcceptManagerHandover(type: "programs" | "portfolios") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => api.post<ManagerHandoverRequestDto>(`/api/manager-handovers/${type}/requests/${requestId}/accept`, {}),
    onSuccess: (request) => invalidateManagerHandoverEntity(queryClient, request),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["managerHandovers"] }),
  });
}

export function useRejectManagerHandover(type: "programs" | "portfolios") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ requestId, rejectionReason }: { requestId: string; rejectionReason: string }) =>
      api.post<ManagerHandoverRequestDto>(`/api/manager-handovers/${type}/requests/${requestId}/reject`, { rejectionReason }),
    onSuccess: (request) => invalidateManagerHandoverEntity(queryClient, request),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["managerHandovers"] }),
  });
}

export function useApproveProjectChangeRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: string; dto: Record<string, unknown> }) =>
      api.post<ProjectChangeRequestDecisionDto>(`/api/project-change-requests/${id}/approve`, dto),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["projects", "change-requests"] }),
      queryClient.invalidateQueries({ queryKey: ["projects", "detail"] }),
      queryClient.invalidateQueries({ queryKey: ["projects", "overview"] }),
      queryClient.invalidateQueries({ queryKey: ["approvalReview"] }),
    ]),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["projectChangeRequests"] }),
  });
}

export function useRejectProjectChangeRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, decisionNotes }: { id: string; decisionNotes: string }) =>
      api.post<ProjectChangeRequestDecisionDto>(`/api/project-change-requests/${id}/reject`, { decisionNotes }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["projects", "change-requests"] }),
      queryClient.invalidateQueries({ queryKey: ["projects", "detail"] }),
      queryClient.invalidateQueries({ queryKey: ["projects", "overview"] }),
      queryClient.invalidateQueries({ queryKey: ["approvalReview"] }),
    ]),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["projectChangeRequests"] }),
  });
}
