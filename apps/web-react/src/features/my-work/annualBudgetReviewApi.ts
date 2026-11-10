import { useQuery } from "@tanstack/react-query";
import { z } from "zod";
import { api } from "@api/client";

export interface AnnualBudgetApprovalReviewerDto {
  assignmentId: string;
  userId: string;
  displayName: string | null;
  position: number | null;
  requirement: string;
  state: string;
  decision: string | null;
  decidedAtUtc: string | null;
  comment: string | null;
  isCurrentUser: boolean;
  isActionable: boolean;
  workflowState: string;
}

export interface AnnualBudgetApprovalReviewDetailDto {
  requestId: string;
  budgetId: string;
  projectPublicId: string;
  projectName: string;
  projectCode: string;
  requestType: string;
  requestTitle: string;
  requestDescription: string | null;
  status: string;
  budgetStatus: string;
  fiscalYear: number;
  fiscalYearStartDate: string;
  fiscalYearEndDate: string;
  requestedByName: string | null;
  requestedAtUtc: string;
  submissionComment: string | null;
  reason: string | null;
  newFunding: number;
  approvedCarryForward: number;
  approvedAdjustments: number;
  totalAnnualBudget: number;
  currency: string;
  projectBudget: number | null;
  projectCurrency: string | null;
  approvalMode: "Sequential" | "Parallel";
  steps: AnnualBudgetApprovalReviewerDto[];
  budgetRowVersion: string;
  approvalRowVersion: string;
  canDecide: boolean;
  disabledReason: string | null;
}

export interface AssignedAnnualBudgetApprovalHistoryItem {
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
  requestedByName: string | null;
  status: string;
  requestType: string;
  canDecide: boolean;
  assignedStepState: string;
}

const reviewerSchema = z.looseObject({
  assignmentId: z.string().min(1),
  userId: z.string().min(1),
  displayName: z.string().nullable(),
  position: z.number().int().nullable(),
  requirement: z.string(),
  state: z.string(),
  decision: z.string().nullable(),
  decidedAtUtc: z.string().nullable(),
  comment: z.string().nullable(),
  isCurrentUser: z.boolean(),
  isActionable: z.boolean(),
  workflowState: z.string(),
});

const reviewDetailSchema = z.looseObject({
  requestId: z.string().min(1),
  budgetId: z.string().min(1),
  projectPublicId: z.string().uuid(),
  projectName: z.string().min(1),
  projectCode: z.string(),
  requestType: z.string(),
  requestTitle: z.string(),
  requestDescription: z.string().nullable(),
  status: z.string(),
  budgetStatus: z.string(),
  fiscalYear: z.number().int().positive(),
  fiscalYearStartDate: z.string().min(1),
  fiscalYearEndDate: z.string().min(1),
  requestedByName: z.string().nullable(),
  requestedAtUtc: z.string().min(1),
  submissionComment: z.string().nullable(),
  reason: z.string().nullable(),
  newFunding: z.number().finite(),
  approvedCarryForward: z.number().finite(),
  approvedAdjustments: z.number().finite(),
  totalAnnualBudget: z.number().finite(),
  currency: z.string().min(1),
  projectBudget: z.number().finite().nullable(),
  projectCurrency: z.string().nullable(),
  approvalMode: z.enum(["Sequential", "Parallel"]),
  steps: z.array(reviewerSchema),
  budgetRowVersion: z.string().min(1),
  approvalRowVersion: z.string().min(1),
  canDecide: z.boolean(),
  disabledReason: z.string().nullable(),
});

const historyItemSchema = z.looseObject({
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
  requestedByName: z.string().nullable(),
  status: z.string(),
  requestType: z.string(),
  canDecide: z.boolean(),
  assignedStepState: z.string(),
});

export const annualBudgetApprovalDetailKey = (requestId: string) =>
  ["annualBudgetApprovals", "detail", requestId] as const;

export function useAnnualBudgetApprovalReviewDetail(requestId: string, enabled = true) {
  return useQuery({
    queryKey: annualBudgetApprovalDetailKey(requestId),
    queryFn: async () => reviewDetailSchema.parse(
      await api.get<unknown>(`/api/my-work/annual-budget-approvals/${encodeURIComponent(requestId)}`),
    ) as AnnualBudgetApprovalReviewDetailDto,
    enabled: Boolean(requestId) && enabled,
    retry: false,
    staleTime: 0,
  });
}

export function useAssignedAnnualBudgetApprovalHistory(enabled = true) {
  return useQuery({
    queryKey: ["annualBudgetApprovals", "history"],
    queryFn: async () => z.array(historyItemSchema).parse(
      await api.get<unknown>("/api/my-work/annual-budget-approvals/history"),
    ) as AssignedAnnualBudgetApprovalHistoryItem[],
    enabled,
    retry: false,
    staleTime: 15_000,
  });
}
