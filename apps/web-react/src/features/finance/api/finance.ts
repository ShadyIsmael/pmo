import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { projectKeys } from "@features/projects/api/projects";

export interface BudgetDto {
  id: string;
  projectId?: string;
  projectPublicId?: string;
  name: string;
  description?: string;
  category: string;
  plannedAmount: number;
  approvedAmount: number;
  forecastAmount: number;
  currency: string;
  fiscalYear: number;
  fiscalQuarter?: number;
  status: string;
  startDate?: string;
  endDate?: string;
  createdAt?: string;
  updatedAt?: string;
  rowVersion: string;
}

export interface CreateBudgetDto {
  projectId: string;
  name: string;
  description?: string;
  category: string;
  plannedAmount: number;
  approvedAmount: number;
  forecastAmount: number;
  currency: string;
  fiscalYear: number;
  fiscalQuarter?: number;
  status: string;
  startDate?: string;
  endDate?: string;
}

export interface CostEntryDto {
  id: string;
  budgetId?: string;
  projectId: string;
  resourceId?: string;
  category: string;
  subCategory?: string;
  description?: string;
  amount: number;
  currency: string;
  taxAmount?: number;
  incurredDate: string;
  type: string;
  reference?: string;
  status: string;
  vendorName?: string;
  incurredBy?: string;
  createdAt?: string;
  updatedAt?: string;
  rowVersion: number[];
}

export interface CreateCostEntryDto {
  projectId: string;
  budgetId?: string;
  category: string;
  subCategory?: string;
  description?: string;
  amount: number;
  currency: string;
  taxAmount?: number;
  incurredDate: string;
  type: string;
  reference?: string;
  vendorName?: string;
  status: string;
}

export interface PurchaseCommitmentDto {
  id: string;
  purchaseOrderNumber: string;
  description?: string;
  vendorName?: string;
  amount: number;
  invoicedAmount?: number;
  currency: string;
  commitmentDate: string;
  expectedDeliveryDate?: string;
  status: string;
  budgetId?: string;
  projectId?: string;
  createdAt?: string;
  updatedAt?: string;
  rowVersion: string;
}

export interface CreateCommitmentDto {
  projectId: string;
  budgetId?: string;
  purchaseOrderNumber: string;
  description?: string;
  vendorName?: string;
  amount: number;
  commitmentDate: string;
  expectedDeliveryDate?: string;
}

export interface UpdateCommitmentDto extends Omit<CreateCommitmentDto, "projectId"> { rowVersion: string; }

export interface FinancePagedResult<T> {
  items: T[];
  totalCount: number;
  page?: number;
  pageSize?: number;
  totalPages?: number;
}

export interface CurrencyFinancialSummary {
  currency: string;
  originalApprovedBudget: number;
  approvedBudget: number;
  allocatedBudget: number;
  unallocatedBudget: number;
  availableBudget: number;
  actualCost: number;
  committedCost: number;
  forecastAtCompletion: number;
  forecastVariance: number;
  budgetUtilizationPercentage?: number | null;
  projectCount: number;
}

export interface ProjectFinancialBreakdown {
  projectId: string;
  projectPublicId: string;
  projectName: string;
  currency: string;
  originalApprovedBudget: number;
  approvedBudget: number;
  allocatedBudget: number;
  unallocatedBudget: number;
  availableBudget: number;
  actualCost: number;
  committedCost: number;
  forecastAtCompletion: number;
  forecastVariance: number;
  budgetUtilizationPercentage?: number | null;
}

export interface GlobalFinancialSummaryDto {
  byCurrency: CurrencyFinancialSummary[];
  byProject: ProjectFinancialBreakdown[];
}

export type FinanceQueryParams = Record<string, string | number | boolean | undefined>;

export const financeKeys = {
  budgets: (params: FinanceQueryParams = {}) => ["budgets", params] as const,
  costs: (params: FinanceQueryParams = {}) => ["costs", params] as const,
  summary: ["costs", "summary"] as const,
  commitments: (params: FinanceQueryParams = {}) => ["commitments", params] as const,
};

export function useBudgets(params: FinanceQueryParams = {}, enabled = true) {
  return useQuery({
    queryKey: financeKeys.budgets(params),
    queryFn: () => api.get<FinancePagedResult<BudgetDto>>(endpoints.budgets.root, { pageSize: 100, ...params }),
    enabled,
    staleTime: 30_000,
  });
}

export function useCosts(params: FinanceQueryParams = {}, enabled = true) {
  return useQuery({
    queryKey: financeKeys.costs(params),
    queryFn: () => api.get<FinancePagedResult<CostEntryDto>>(endpoints.costs.root, { pageSize: 100, ...params }),
    enabled,
    staleTime: 30_000,
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
  return useMutation({
    mutationFn: (body: CreateBudgetDto) => api.post<BudgetDto>(endpoints.budgets.root, body),
    onSuccess: () => { void client.invalidateQueries({ queryKey: ["budgets"] }); void client.invalidateQueries({ queryKey: financeKeys.summary }); },
  });
}

export function useCostManagementMutations(projectId: string) {
  const client = useQueryClient();
  const invalidate = async (...keys: readonly unknown[][]) => {
    await Promise.all([
      ...keys.map((queryKey) => client.invalidateQueries({ queryKey })),
      client.invalidateQueries({ queryKey: financeKeys.summary }),
      client.invalidateQueries({ queryKey: projectKeys.overview(projectId) }),
    ]);
  };
  return {
    createBudget: useMutation({
      mutationFn: (body: CreateBudgetDto) => api.post<BudgetDto>(endpoints.budgets.root, body),
      onSuccess: () => invalidate(["budgets"]),
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
      mutationFn: ({ id, body }: { id: string; body: UpdateBudgetRevisionDto }) => budgetRevisionApi.update(id, body),
      onSuccess: invalidate,
    }),
    submit: useMutation({
      mutationFn: ({ id, body }: { id: string; body: { rowVersion: string } }) => budgetRevisionApi.submit(id, body),
      onSuccess: invalidate,
    }),
    approve: useMutation({
      mutationFn: ({ id, body }: { id: string; body: ReviewBudgetRevisionDto }) => budgetRevisionApi.approve(id, body),
      onSuccess: invalidate,
    }),
    reject: useMutation({
      mutationFn: ({ id, body }: { id: string; body: ReviewBudgetRevisionDto }) => budgetRevisionApi.reject(id, body),
      onSuccess: invalidate,
    }),
    cancel: useMutation({ mutationFn: (id: string) => budgetRevisionApi.cancel(id), onSuccess: invalidate }),
    delete: useMutation({
      mutationFn: ({ id, rowVersion }: { id: string; rowVersion: string }) => budgetRevisionApi.delete(id, rowVersion),
      onSuccess: invalidate,
    }),
  };
}
