import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface PaymentSettlementDto {
  id: string;
  paymentObligationId: string;
  idempotencyKey: string;
  amount: number;
  currency: string | null;
  settlementDate: string;
  paymentReference: string;
  notes?: string;
  createdAt: string;
  rowVersion: string;
}

export interface PaymentObligationDto {
  id: string;
  projectId: string;
  projectName: string;
  milestoneId?: string;
  paymentReference: string;
  purpose: string;
  description: string;
  amount: number;
  currency: string;
  dueDate: string;
  notes?: string;
  settledAmount: number;
  outstandingAmount: number;
  settlementProgressStatus: "Not Paid" | "Partially Paid" | "Fully Paid";
  dueDateStatus: "Upcoming" | "Due" | "Overdue" | "Settled";
  lifecycleStatus: string;
  isDue: boolean;
  isOverdue: boolean;
  settlements: PaymentSettlementDto[];
  createdAt: string;
  updatedAt?: string;
  rowVersion: string;
}

export interface CreatePaymentObligationDto {
  projectId: string;
  milestoneId?: string;
  paymentReference: string;
  purpose: string;
  description?: string;
  amount: number;
  currency: string;
  dueDate: string;
  notes?: string;
}

export interface UpdatePaymentObligationDto extends CreatePaymentObligationDto {
  rowVersion: string;
}

export interface RecordPaymentSettlementDto {
  idempotencyKey: string;
  amount: number;
  settlementDate: string;
  paymentReference: string;
  notes?: string;
}

export interface UpdateCommitmentDto extends Omit<CreateCommitmentDto, "projectId"> { rowVersion: string; }

export interface PaymentSettlementDto {
  id: string;
  paymentObligationId: string;
  idempotencyKey: string;
  amount: number;
  settlementDate: string;
  paymentReference: string;
  notes?: string;
  createdAt: string;
  rowVersion: string;
}

export interface PaymentObligationDto {
  id: string;
  projectId: string;
  projectName: string;
  milestoneId?: string;
  paymentReference: string;
  purpose: string;
  description: string;
  amount: number;
  currency: string;
  dueDate: string;
  notes?: string;
  settledAmount: number;
  outstandingAmount: number;
  settlementProgressStatus: "Not Paid" | "Partially Paid" | "Fully Paid";
  dueDateStatus: "Upcoming" | "Due" | "Overdue" | "Settled";
  lifecycleStatus: string;
  isDue: boolean;
  isOverdue: boolean;
  settlements: PaymentSettlementDto[];
  createdAt: string;
  updatedAt?: string;
  rowVersion: string;
}

export interface CreatePaymentObligationDto {
  projectId: string;
  milestoneId?: string;
  paymentReference: string;
  purpose: string;
  description?: string;
  amount: number;
  currency: string;
  dueDate: string;
  notes?: string;
}

export interface UpdatePaymentObligationDto extends CreatePaymentObligationDto { rowVersion: string; }

export interface RecordPaymentSettlementDto {
  idempotencyKey: string;
  amount: number;
  settlementDate: string;
  paymentReference: string;
  notes?: string;
}

export interface FinancePagedResult<T> {
  items: T[];
  totalCount: number;
  page?: number;
  pageSize?: number;
  totalPages?: number;
}

export type FinanceQueryParams = Record<string, string | number | boolean | undefined>;

export const financeKeys = {
  budgets: (params: FinanceQueryParams = {}) => ["budgets", params] as const,
  costs: (params: FinanceQueryParams = {}) => ["costs", params] as const,
  summary: ["costs", "summary"] as const,
  commitments: (params: FinanceQueryParams = {}) => ["commitments", params] as const,
  paymentObligations: (params: FinanceQueryParams = {}) => ["payment-obligations", params] as const,
  paymentSettlements: (id: string) => ["payment-obligations", id, "settlements"] as const,
};

export function usePaymentObligations(params: FinanceQueryParams = {}, enabled = true) {
  return useQuery({
    queryKey: financeKeys.paymentObligations(params),
    queryFn: () => api.get<FinancePagedResult<PaymentObligationDto>>(
      endpoints.paymentObligations.root,
      { pageSize: 100, ...params },
    ),
    enabled,
    staleTime: 15_000,
  });
}

export function usePaymentSettlements(paymentId: string, enabled = true) {
  return useQuery({
    queryKey: financeKeys.paymentSettlements(paymentId),
    queryFn: () => api.get<FinancePagedResult<PaymentSettlementDto>>(
      endpoints.paymentObligations.settlements(paymentId),
      { pageSize: 100 },
    ),
    enabled: Boolean(paymentId) && enabled,
    staleTime: 15_000,
  });
}

export function useCommitments(params: FinanceQueryParams = {}, enabled = true) {
  return useQuery({
    queryKey: financeKeys.commitments(params),
    queryFn: () => api.get<FinancePagedResult<PurchaseCommitmentDto>>(endpoints.commitments.root, { pageSize: 100, ...params }),
    enabled,
    staleTime: 30_000,
  });
}

export function usePaymentObligations(params: FinanceQueryParams = {}, enabled = true) {
  return useQuery({
    queryKey: financeKeys.paymentObligations(params),
    queryFn: () => api.get<FinancePagedResult<PaymentObligationDto>>(endpoints.paymentObligations.root, { pageSize: 100, ...params }),
    enabled,
    staleTime: 15_000,
  });
}

export function usePaymentSettlements(paymentId: string, enabled = true) {
  return useQuery({
    queryKey: financeKeys.paymentSettlements(paymentId),
    queryFn: () => api.get<FinancePagedResult<PaymentSettlementDto>>(endpoints.paymentObligations.settlements(paymentId), { pageSize: 100 }),
    enabled: Boolean(paymentId) && enabled,
    staleTime: 15_000,
  });
}

export function useProjectBudgets(projectId: string, enabled = true, params: FinanceQueryParams = {}) {
  return useBudgets({ ...params, projectId }, Boolean(projectId) && enabled);
}

export function useProjectCosts(projectId: string, enabled = true, params: FinanceQueryParams = {}) {
  return useCosts({ ...params, projectId }, Boolean(projectId) && enabled);
}

export function useProjectCommitments(projectId: string, enabled = true, params: FinanceQueryParams = {}) {
  return useCommitments({ ...params, projectId }, Boolean(projectId) && enabled);
}

export function useCostSummary(enabled = true) {
  return useQuery({
    queryKey: financeKeys.summary,
    queryFn: () => api.get<{ totalBudget: number; totalActual: number; totalForecast: number; currency: string }>(endpoints.costs.summary),
    enabled,
    staleTime: 30_000,
  });
}

export function useGlobalFinancialSummary(enabled = true) {
  return useQuery({
    queryKey: [...financeKeys.summary, "global"] as const,
    queryFn: () => api.get<GlobalFinancialSummaryDto>(endpoints.costs.summary),
    enabled,
    staleTime: 30_000,
  });
}

export function useCreateBudget() {
  const client = useQueryClient();
  const invalidate = async (paymentId?: string) => {
    await Promise.all([
      client.invalidateQueries({ queryKey: ["payment-obligations"] }),
      paymentId
        ? client.invalidateQueries({ queryKey: financeKeys.paymentSettlements(paymentId) })
        : Promise.resolve(),
    ]);
  };

  return {
    create: useMutation({
      mutationFn: (body: CreatePaymentObligationDto) =>
        api.post<PaymentObligationDto>(endpoints.paymentObligations.root, body),
      onSuccess: () => invalidate(),
    }),
    updateBudget: useMutation({
      mutationFn: ({ id, body }: { id: string; body: CreateBudgetDto & { rowVersion: string } }) => api.put<BudgetDto>(endpoints.budgets.detail(id), body),
      onSuccess: () => invalidate(["budgets"]),
    }),
    deleteBudget: useMutation({
      mutationFn: (id: string) => api.delete<void>(endpoints.budgets.detail(id)),
      onSuccess: () => invalidate(["budgets"]),
    }),
    createCost: useMutation({
      mutationFn: (body: CreateCostEntryDto) => api.post<CostEntryDto>(endpoints.costs.root, body),
      onSuccess: () => invalidate(["costs"]),
    }),
    updateCost: useMutation({
      mutationFn: ({ id, body }: { id: string; body: CreateCostEntryDto & { rowVersion: number[] } }) => api.put<CostEntryDto>(endpoints.costs.detail(id), body),
      onSuccess: () => invalidate(["costs"]),
    }),
    deleteCost: useMutation({
      mutationFn: (id: string) => api.delete<void>(endpoints.costs.detail(id)),
      onSuccess: () => invalidate(["costs"]),
    }),
    createCommitment: useMutation({
      mutationFn: (body: CreateCommitmentDto) => api.post<PurchaseCommitmentDto>(endpoints.commitments.root, body),
      onSuccess: () => invalidate(["commitments"]),
    }),
    updateCommitment: useMutation({
      mutationFn: ({ id, body }: { id: string; body: UpdateCommitmentDto }) => api.put<PurchaseCommitmentDto>(endpoints.commitments.detail(id), body),
      onSuccess: () => invalidate(["commitments"]),
    }),
    closeCommitment: useMutation({
      mutationFn: ({ id, rowVersion }: { id: string; rowVersion: string }) => api.post<void>(`${endpoints.commitments.close(id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
      onSuccess: () => invalidate(["commitments"]),
    }),
    cancelCommitment: useMutation({
      mutationFn: ({ id, rowVersion }: { id: string; rowVersion: string }) => api.post<void>(`${endpoints.commitments.cancel(id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
      onSuccess: () => invalidate(["commitments"]),
    }),
  };
}

export function useCreateCost() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => api.post<CostEntryDto>(endpoints.costs.root, body),
    onSuccess: () => { void client.invalidateQueries({ queryKey: ["costs"] }); void client.invalidateQueries({ queryKey: financeKeys.summary }); },
  });
}

export function usePaymentObligationMutations() {
  const client = useQueryClient();
  const invalidate = async (paymentId?: string) => {
    await Promise.all([
      client.invalidateQueries({ queryKey: ["payment-obligations"] }),
      paymentId ? client.invalidateQueries({ queryKey: financeKeys.paymentSettlements(paymentId) }) : Promise.resolve(),
    ]);
  };

  return {
    create: useMutation({
      mutationFn: (body: CreatePaymentObligationDto) => api.post<PaymentObligationDto>(endpoints.paymentObligations.root, body),
      onSuccess: () => invalidate(),
    }),
    update: useMutation({
      mutationFn: ({ id, body }: { id: string; body: UpdatePaymentObligationDto }) => api.put<PaymentObligationDto>(endpoints.paymentObligations.detail(id), body),
      onSuccess: () => invalidate(),
    }),
    delete: useMutation({
      mutationFn: ({ id, rowVersion }: { id: string; rowVersion: string }) => api.delete<void>(endpoints.paymentObligations.detail(id), { rowVersion }),
      onSuccess: () => invalidate(),
    }),
    settle: useMutation({
      mutationFn: ({ id, body }: { id: string; body: RecordPaymentSettlementDto }) => api.post<PaymentSettlementDto>(endpoints.paymentObligations.settlements(id), body),
      onSuccess: (_data, variables) => invalidate(variables.id),
    }),
  };
}

export interface BudgetRevisionDto {
  id: string;
  projectId: string;
  budgetId: string;
  budgetName: string;
  currency: string;
  previousApprovedAmount: number;
  proposedApprovedAmount: number;
  reason: string;
  justification?: string;
  status: string;
  requestedBy: string;
  requestedByName?: string;
  requestedAt: string;
  reviewedBy?: string;
  reviewedByName?: string;
  reviewedAt?: string;
  reviewComment?: string;
  createdAt: string;
  updatedAt?: string;
  rowVersion: string;
}

export interface CreateBudgetRevisionDto {
  budgetId: string;
  proposedApprovedAmount: number;
  reason: string;
  justification?: string;
}

export interface UpdateBudgetRevisionDto {
  proposedApprovedAmount: number;
  reason: string;
  justification?: string;
  rowVersion: string;
}

export interface ReviewBudgetRevisionDto {
  reviewComment?: string;
  rowVersion: string;
}

export const budgetRevisionKeys = {
  all: ["budget-revisions"] as const,
  byProject: (projectId: string, params: Record<string, string | number | boolean | undefined>) => [...budgetRevisionKeys.all, "project", projectId, params] as const,
};

export const budgetRevisionApi = {
  getByProject: (projectId: string, params: Record<string, string | number | boolean | undefined> = {}) => api.get<{ items: BudgetRevisionDto[]; totalCount: number; page: number; pageSize: number; totalPages: number }>(endpoints.budgetRevisions.byProject(projectId), params),
  create: (body: CreateBudgetRevisionDto) => api.post<BudgetRevisionDto>(endpoints.budgetRevisions.root, body),
  update: (id: string, body: UpdateBudgetRevisionDto) => api.put<BudgetRevisionDto>(endpoints.budgetRevisions.detail(id), body),
  submit: (id: string, body: { rowVersion: string }) => api.post<void>(endpoints.budgetRevisions.submit(id), body),
  approve: (id: string, body: ReviewBudgetRevisionDto) => api.post<void>(endpoints.budgetRevisions.approve(id), body),
  reject: (id: string, body: ReviewBudgetRevisionDto) => api.post<void>(endpoints.budgetRevisions.reject(id), body),
  cancel: (id: string) => api.post<void>(endpoints.budgetRevisions.cancel(id)),
  delete: (id: string, rowVersion: string) => api.delete<void>(`${endpoints.budgetRevisions.detail(id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
};

export function useBudgetRevisionsByProject(projectId: string, params: { page?: number; pageSize?: number } = {}, enabled = true) {
  return useQuery({
    queryKey: budgetRevisionKeys.byProject(projectId, params),
    queryFn: () => budgetRevisionApi.getByProject(projectId, params),
    staleTime: 30_000,
    enabled: Boolean(projectId) && enabled,
  });
}

export function useBudgetRevisionMutations(projectId: string) {
  const client = useQueryClient();
  const invalidate = () => {
    void client.invalidateQueries({ queryKey: ["budgets"] });
    void client.invalidateQueries({ queryKey: financeKeys.summary });
    void client.invalidateQueries({ queryKey: budgetRevisionKeys.all });
    void client.invalidateQueries({ queryKey: projectKeys.overview(projectId) });
  };
  return {
    create: useMutation({ mutationFn: budgetRevisionApi.create, onSuccess: invalidate }),
    update: useMutation({
      mutationFn: ({ id, body }: { id: string; body: UpdatePaymentObligationDto }) =>
        api.put<PaymentObligationDto>(endpoints.paymentObligations.detail(id), body),
      onSuccess: () => invalidate(),
    }),
    delete: useMutation({
      mutationFn: ({ id, rowVersion }: { id: string; rowVersion: string }) =>
        api.delete<void>(endpoints.paymentObligations.detail(id), { rowVersion }),
      onSuccess: () => invalidate(),
    }),
    settle: useMutation({
      mutationFn: ({ id, body }: { id: string; body: RecordPaymentSettlementDto }) =>
        api.post<PaymentSettlementDto>(endpoints.paymentObligations.settlements(id), body),
      onSuccess: (_data, variables) => invalidate(variables.id),
    }),
  };
}
