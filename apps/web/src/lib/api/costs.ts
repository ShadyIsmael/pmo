"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult, type PaginationParams } from "@/lib/api";
import { endpoints } from "@/lib/api/endpoints";
import { projectKeys } from "@/lib/api/projects";

export interface BudgetDto {
  id: string;
  projectId?: string;
  portfolioId?: string;
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
  createdAt: string;
  updatedAt?: string;
  /** ASP.NET Core serializes the API byte[] concurrency token as Base64. */
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
  projectId?: string;
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
  vendorName?: string;
  status: string;
  incurredBy?: string;
  createdAt: string;
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
  createdAt: string;
  updatedAt?: string;
  /** ASP.NET Core serializes the API byte[] concurrency token as Base64. */
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

export interface UpdateCommitmentDto {
  purchaseOrderNumber: string;
  description?: string;
  vendorName?: string;
  amount: number;
  commitmentDate: string;
  expectedDeliveryDate?: string;
  /** Send the backend-provided Base64 value back unchanged for optimistic concurrency. */
  rowVersion: string;
}

export interface CurrencyFinancialSummary {
  currency: string;
  originalApprovedBudget: number;
  /** Current approved project budget, used as the project budget ceiling. */
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
  projectName: string;
  currency: string;
  originalApprovedBudget: number;
  /** Current approved project budget, used as the project budget ceiling. */
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

export interface BudgetListParams extends PaginationParams {
  projectId?: string;
  portfolioId?: string;
  hasForecast?: boolean;
}

export interface CostEntryListParams extends PaginationParams {
  projectId?: string;
}

export interface CommitmentListParams extends PaginationParams {
  projectId?: string;
}

export const costKeys = {
  all: ["cost-management"] as const,
  summary: () => [...costKeys.all, "summary"] as const,
  budgets: (params: BudgetListParams) => [...costKeys.all, "budgets", params] as const,
  costs: (params: CostEntryListParams) => [...costKeys.all, "costs", params] as const,
  commitments: (params: CommitmentListParams) => [...costKeys.all, "commitments", params] as const,
  projectBudgets: (projectId: string) => [...costKeys.all, "budgets", { projectId }] as const,
  projectCosts: (projectId: string) => [...costKeys.all, "costs", { projectId }] as const,
  projectCommitments: (projectId: string) => [...costKeys.all, "commitments", { projectId }] as const,
};

export const costApi = {
  summary: () => api.get<GlobalFinancialSummaryDto>(endpoints.costs.summary),
  budgets: (params: BudgetListParams = {}) => api.get<PagedResult<BudgetDto>>(endpoints.budgets.root, params),
  costs: (params: CostEntryListParams = {}) => api.get<PagedResult<CostEntryDto>>(endpoints.costs.root, params),
  commitments: (params: CommitmentListParams = {}) => api.get<PagedResult<PurchaseCommitmentDto>>(endpoints.commitments.root, params),
  createBudget: (body: CreateBudgetDto) => api.post<BudgetDto>(endpoints.budgets.root, body),
  updateBudget: (id: string, body: CreateBudgetDto & { rowVersion: string }) => api.put<BudgetDto>(endpoints.budgets.detail(id), body),
  deleteBudget: (id: string) => api.delete<void>(endpoints.budgets.detail(id)),
  createCost: (body: CreateCostEntryDto) => api.post<CostEntryDto>(endpoints.costs.root, body),
  updateCost: (id: string, body: CreateCostEntryDto & { rowVersion: number[] }) => api.put<CostEntryDto>(endpoints.costs.detail(id), body),
  deleteCost: (id: string) => api.delete<void>(endpoints.costs.detail(id)),
  createCommitment: (body: CreateCommitmentDto) => api.post<PurchaseCommitmentDto>(endpoints.commitments.root, body),
  updateCommitment: (id: string, body: UpdateCommitmentDto) => api.put<PurchaseCommitmentDto>(endpoints.commitments.detail(id), body),
  closeCommitment: (id: string, rowVersion: string) => api.post<void>(`${endpoints.commitments.close(id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
  cancelCommitment: (id: string, rowVersion: string) => api.post<void>(`${endpoints.commitments.cancel(id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
  deleteCommitment: (id: string, rowVersion: string) => api.delete<void>(`${endpoints.commitments.detail(id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
};

export function useGlobalFinancialSummary(enabled = true) {
  return useQuery({
    queryKey: costKeys.summary(),
    queryFn: () => costApi.summary(),
    staleTime: 30_000,
    enabled,
  });
}

export function useBudgets(params: BudgetListParams = {}, enabled = true) {
  return useQuery({
    queryKey: costKeys.budgets(params),
    queryFn: () => costApi.budgets(params),
    staleTime: 30_000,
    enabled,
  });
}

export function useCostEntries(params: CostEntryListParams = {}, enabled = true) {
  return useQuery({
    queryKey: costKeys.costs(params),
    queryFn: () => costApi.costs(params),
    staleTime: 30_000,
    enabled,
  });
}

export function useCommitments(params: CommitmentListParams = {}, enabled = true) {
  return useQuery({
    queryKey: costKeys.commitments(params),
    queryFn: () => costApi.commitments(params),
    staleTime: 30_000,
    enabled,
  });
}

export function useProjectBudgets(projectId: string, enabled = true, params: PaginationParams = { page: 1, pageSize: 200 }) {
  return useQuery({
    queryKey: costKeys.budgets({ ...params, projectId }),
    queryFn: () => costApi.budgets({ ...params, projectId }),
    enabled: Boolean(projectId) && enabled,
  });
}

export function useProjectCosts(projectId: string, enabled = true, params: PaginationParams = { page: 1, pageSize: 200 }) {
  return useQuery({
    queryKey: costKeys.costs({ ...params, projectId }),
    queryFn: () => costApi.costs({ ...params, projectId }),
    enabled: Boolean(projectId) && enabled,
  });
}

export function useProjectCommitments(projectId: string, enabled = true, params: PaginationParams = { page: 1, pageSize: 200 }) {
  return useQuery({
    queryKey: costKeys.commitments({ ...params, projectId }),
    queryFn: () => costApi.commitments({ ...params, projectId }),
    enabled: Boolean(projectId) && enabled,
  });
}

export function useCostManagementMutations(projectId: string) {
  const queryClient = useQueryClient();
  const invalidateBudget = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: [...costKeys.all, "budgets"] }),
      queryClient.invalidateQueries({ queryKey: costKeys.summary() }),
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(projectId) }),
    ]);
  };
  const invalidateCost = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: [...costKeys.all, "costs"] }),
      queryClient.invalidateQueries({ queryKey: costKeys.summary() }),
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(projectId) }),
    ]);
  };
  const invalidateCommitment = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: [...costKeys.all, "commitments"] }),
      queryClient.invalidateQueries({ queryKey: costKeys.summary() }),
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(projectId) }),
    ]);
  };
  const replaceCommitmentInCaches = (updated: PurchaseCommitmentDto) => {
    queryClient.setQueriesData<PagedResult<PurchaseCommitmentDto>>({ queryKey: [...costKeys.all, "commitments"] }, (current) => current ? {
      ...current,
      items: current.items.map((item) => item.id === updated.id ? updated : item),
    } : current);
  };
  return {
    createBudget: useMutation({ mutationFn: costApi.createBudget, onSuccess: invalidateBudget }),
    updateBudget: useMutation({
      mutationFn: ({ id, body }: { id: string; body: CreateBudgetDto & { rowVersion: string } }) => costApi.updateBudget(id, body),
      onSuccess: invalidateBudget,
    }),
    deleteBudget: useMutation({ mutationFn: costApi.deleteBudget, onSuccess: invalidateBudget }),
    createCost: useMutation({ mutationFn: costApi.createCost, onSuccess: invalidateCost }),
    updateCost: useMutation({
      mutationFn: ({ id, body }: { id: string; body: CreateCostEntryDto & { rowVersion: number[] } }) => costApi.updateCost(id, body),
      onSuccess: invalidateCost,
    }),
    deleteCost: useMutation({ mutationFn: costApi.deleteCost, onSuccess: invalidateCost }),
    createCommitment: useMutation({ mutationFn: costApi.createCommitment, onSuccess: invalidateCommitment }),
    updateCommitment: useMutation({
      mutationFn: ({ id, body }: { id: string; body: UpdateCommitmentDto }) => costApi.updateCommitment(id, body),
      onSuccess: async (updated) => {
        replaceCommitmentInCaches(updated);
        await invalidateCommitment();
      },
    }),
    closeCommitment: useMutation({
      mutationFn: ({ id, rowVersion }: { id: string; rowVersion: string }) => costApi.closeCommitment(id, rowVersion),
      onSuccess: invalidateCommitment,
    }),
    cancelCommitment: useMutation({
      mutationFn: ({ id, rowVersion }: { id: string; rowVersion: string }) => costApi.cancelCommitment(id, rowVersion),
      onSuccess: invalidateCommitment,
    }),
    deleteCommitment: useMutation({
      mutationFn: ({ id, rowVersion }: { id: string; rowVersion: string }) => costApi.deleteCommitment(id, rowVersion),
      onSuccess: invalidateCommitment,
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

export interface BudgetRevisionListParams extends PaginationParams {
  projectId?: string;
  status?: string;
}

export const budgetRevisionKeys = {
  all: ["budget-revisions"] as const,
  byBudget: (budgetId: string, params: PaginationParams) => [...budgetRevisionKeys.all, "budget", budgetId, params] as const,
  byProject: (projectId: string, params: BudgetRevisionListParams) => [...budgetRevisionKeys.all, "project", projectId, params] as const,
  detail: (id: string) => [...budgetRevisionKeys.all, "detail", id] as const,
};

export const budgetRevisionApi = {
  getById: (id: string) => api.get<BudgetRevisionDto>(endpoints.budgetRevisions.detail(id)),
  getByBudget: (budgetId: string, params: PaginationParams = {}) => api.get<PagedResult<BudgetRevisionDto>>(endpoints.budgetRevisions.byBudget(budgetId), params),
  getByProject: (projectId: string, params: BudgetRevisionListParams = {}) => api.get<PagedResult<BudgetRevisionDto>>(endpoints.budgetRevisions.byProject(projectId), params),
  create: (body: CreateBudgetRevisionDto) => api.post<BudgetRevisionDto>(endpoints.budgetRevisions.root, body),
  update: (id: string, body: UpdateBudgetRevisionDto) => api.put<BudgetRevisionDto>(endpoints.budgetRevisions.detail(id), body),
  submit: (id: string, body: { rowVersion: string }) => api.post<void>(endpoints.budgetRevisions.submit(id), body),
  approve: (id: string, body: ReviewBudgetRevisionDto) => api.post<void>(endpoints.budgetRevisions.approve(id), body),
  reject: (id: string, body: ReviewBudgetRevisionDto) => api.post<void>(endpoints.budgetRevisions.reject(id), body),
  cancel: (id: string) => api.post<void>(endpoints.budgetRevisions.cancel(id)),
  delete: (id: string, rowVersion: string) => api.delete<void>(`${endpoints.budgetRevisions.detail(id)}?rowVersion=${encodeURIComponent(rowVersion)}`),
};

export function useBudgetRevisionsByBudget(budgetId: string, params: PaginationParams = {}, enabled = true) {
  return useQuery({
    queryKey: budgetRevisionKeys.byBudget(budgetId, params),
    queryFn: () => budgetRevisionApi.getByBudget(budgetId, params),
    staleTime: 30_000,
    enabled: Boolean(budgetId) && enabled,
  });
}

export function useBudgetRevisionsByProject(projectId: string, params: BudgetRevisionListParams = {}, enabled = true) {
  return useQuery({
    queryKey: budgetRevisionKeys.byProject(projectId, params),
    queryFn: () => budgetRevisionApi.getByProject(projectId, params),
    staleTime: 30_000,
    enabled: Boolean(projectId) && enabled,
  });
}

export function useBudgetRevisionMutations(projectId: string) {
  const queryClient = useQueryClient();
  const invalidate = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: [...costKeys.all, "budgets"] }),
      queryClient.invalidateQueries({ queryKey: costKeys.summary() }),
      queryClient.invalidateQueries({ queryKey: budgetRevisionKeys.all }),
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(projectId) }),
    ]);
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
    delete: useMutation({ mutationFn: ({ id, rowVersion }: { id: string; rowVersion: string }) => budgetRevisionApi.delete(id, rowVersion), onSuccess: invalidate }),
  };
}
