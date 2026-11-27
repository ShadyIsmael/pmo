import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";

export interface PlannedPaymentInstallmentDto {
  id: string;
  sequence: number;
  amount: number;
  dueDateUtc: string | null;
  planActivityId: string | null;
  planActivityName: string | null;
  requestedAmount: number;
  settledAmount: number;
  actualPaid: number;
  outstandingBalance: number;
  remainingRequestableAmount: number;
  rowVersion: string;
}

export interface PlannedPaymentDto {
  id: string;
  projectId: string;
  reference: string;
  description: string;
  currency: string;
  totalAmount: number;
  state: string;
  isLocked: boolean;
  paymentStatus: string;
  reservedAmount: number;
  releasedAmount: number;
  outstandingReservedAmount: number;
  settledAmount: number;
  registeredAtUtc: string | null;
  firstRequestAtUtc: string | null;
  replacesPlannedPaymentId: string | null;
  replacedByPlannedPaymentId: string | null;
  installments: PlannedPaymentInstallmentDto[];
  rowVersion: string;
  canEdit: boolean;
}

export interface PlannedPaymentRequestContext {
  installmentId: string;
  installmentSequence: number;
  installmentAmount: number;
  remainingRequestableAmount: number;
  currency: string;
  planReference: string;
  planDescription: string;
  dueDateUtc: string | null;
  planActivityId: string | null;
  planActivityName: string | null;
}

export interface PlannedPaymentBudgetDto {
  projectId: string;
  currency: string;
  lifetimeCeiling: number | null;
  lifetimeConsumed: number;
  lifetimeReserved: number;
  availableLifetimeBudget: number;
  canRegister: boolean;
}

export interface PlannedPaymentPageDto {
  items: PlannedPaymentDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface PlannedPaymentMilestoneOption {
  id: string;
  name: string;
  endDate: string | null;
}

export interface CreatePlannedPaymentInstallmentInput {
  amount: number;
  dueDateUtc: string;
  planActivityId?: string;
  description?: string;
}

export interface CreatePlannedPaymentInput {
  description: string;
  installments: CreatePlannedPaymentInstallmentInput[];
  idempotencyKey: string;
}

export interface ReplacePlannedPaymentInstallmentInput {
  amount: number;
  dueDateUtc: string;
  planActivityId?: string;
  description?: string;
}

export interface ReplaceRemainingCommitmentInput {
  newRemainingCommitment: number;
  installments: ReplacePlannedPaymentInstallmentInput[];
  reason: string;
  idempotencyKey: string;
}

export interface PlannedPaymentReplacementPreviewBlocker {
  paymentRequestId: string;
  paymentRequestReference: string;
  status: string;
  outstandingAmount: number;
  approvedReversalAmount: number;
  pendingReversalAmount: number;
}

export interface PlannedPaymentReplacementPreview {
  plannedPaymentId: string;
  currency: string;
  currentCommitment: number;
  historicalPaid: number;
  currentOutstandingCommitment: number;
  newRemainingCommitment: number;
  netLifetimeBudgetChange: number;
  availableLifetimeBudgetBefore: number;
  availableLifetimeBudgetAfter: number;
  hasSufficientLifetimeBudget: boolean;
  blockers: PlannedPaymentReplacementPreviewBlocker[];
}

const plannedPaymentsKey = (projectId: string) => ["project-planned-payments", projectId] as const;
const plannedPaymentBudgetKey = (projectId: string) => ["project-planned-payment-budget", projectId] as const;
const plannedPaymentMilestonesKey = (projectId: string) => ["project-payment-request-milestones", projectId] as const;
const replacementPreviewKey = (projectId: string, plannedPaymentId: string, amount: number | null) =>
  ["planned-payment-replacement-preview", projectId, plannedPaymentId, amount] as const;

function invalidatePlannedPayments(queryClient: ReturnType<typeof useQueryClient>, projectId: string) {
  void queryClient.invalidateQueries({ queryKey: plannedPaymentsKey(projectId) });
  void queryClient.invalidateQueries({ queryKey: plannedPaymentBudgetKey(projectId) });
  void queryClient.invalidateQueries({ queryKey: ["project-financial-activity", projectId] });
  void queryClient.invalidateQueries({ queryKey: ["project-financial-foundation", projectId] });
}

export function usePlannedPaymentBudget(projectId: string, enabled = true) {
  return useQuery({
    queryKey: plannedPaymentBudgetKey(projectId),
    queryFn: () => api.get<PlannedPaymentBudgetDto>(`/api/projects/${encodeURIComponent(projectId)}/planned-payments/budget`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 10_000,
  });
}

export function useProjectPlannedPayments(projectId: string, page: number, enabled = true) {
  return useQuery({
    queryKey: [...plannedPaymentsKey(projectId), page] as const,
    queryFn: () => api.get<PlannedPaymentPageDto>(
      `/api/projects/${encodeURIComponent(projectId)}/planned-payments`,
      { page, pageSize: 5 }),
    enabled: Boolean(projectId) && enabled,
    staleTime: 10_000,
  });
}

export function usePlannedPaymentMilestones(projectId: string, enabled = true) {
  return useQuery({
    queryKey: plannedPaymentMilestonesKey(projectId),
    queryFn: () => api.get<PlannedPaymentMilestoneOption[]>(
      `/api/projects/${encodeURIComponent(projectId)}/payment-requests/milestones`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useRegisterPlannedPayment(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreatePlannedPaymentInput) => api.post<PlannedPaymentDto>(
      `/api/projects/${encodeURIComponent(projectId)}/planned-payments`, input),
    onSuccess: () => invalidatePlannedPayments(queryClient, projectId),
  });
}

export function useReplacePlannedPayment(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { plannedPaymentId: string; body: ReplaceRemainingCommitmentInput }) => api.post<PlannedPaymentDto>(
      `/api/projects/${encodeURIComponent(projectId)}/planned-payments/${encodeURIComponent(input.plannedPaymentId)}/replace`, input.body),
    onSuccess: () => invalidatePlannedPayments(queryClient, projectId),
  });
}

export function usePlannedPaymentReplacementPreview(
  projectId: string,
  plannedPaymentId: string | null,
  newRemainingCommitment: number | null,
  enabled = true,
) {
  return useQuery({
    queryKey: plannedPaymentId ? replacementPreviewKey(projectId, plannedPaymentId, newRemainingCommitment) : ["planned-payment-replacement-preview", projectId],
    queryFn: () => api.get<PlannedPaymentReplacementPreview>(
      `/api/projects/${encodeURIComponent(projectId)}/planned-payments/${encodeURIComponent(plannedPaymentId!)}/replacement-preview`,
      { newRemainingCommitment: newRemainingCommitment! }),
    enabled: Boolean(projectId && plannedPaymentId && newRemainingCommitment !== null && newRemainingCommitment > 0) && enabled,
    staleTime: 0,
  });
}
