import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface BudgetDto { id: string; projectId?: string; name: string; category: string; plannedAmount: number; approvedAmount: number; forecastAmount: number; currency: string; fiscalYear: number; fiscalQuarter?: number; status: string; }
export interface CostEntryDto { id: string; projectId: string; description: string; amount: number; currency: string; incurredDate: string; status: string; vendorName?: string; }

export const financeKeys = {
  budgets: (params: Record<string, string | undefined> = {}) => ["budgets", params] as const,
  costs: (params: Record<string, string | undefined> = {}) => ["costs", params] as const,
  summary: ["costs", "summary"] as const,
};

export function useBudgets(params: Record<string, string | undefined> = {}, enabled = true) {
  return useQuery({
    queryKey: financeKeys.budgets(params),
    queryFn: () => api.get<{ items: BudgetDto[]; totalCount: number }>(endpoints.budgets.root, { pageSize: 100, ...params }),
    enabled,
    staleTime: 30_000,
  });
}

export function useCosts(params: Record<string, string | undefined> = {}, enabled = true) {
  return useQuery({
    queryKey: financeKeys.costs(params),
    queryFn: () => api.get<{ items: CostEntryDto[]; totalCount: number }>(endpoints.costs.root, { pageSize: 100, ...params }),
    enabled,
    staleTime: 30_000,
  });
}

export function useCostSummary(enabled = true) {
  return useQuery({
    queryKey: financeKeys.summary,
    queryFn: () => api.get<{ totalBudget: number; totalActual: number; totalForecast: number; currency: string }>(endpoints.costs.summary),
    enabled,
    staleTime: 30_000,
  });
}

export function useCreateBudget() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => api.post<BudgetDto>(endpoints.budgets.root, body),
    onSuccess: () => { void client.invalidateQueries({ queryKey: ["budgets"] }); void client.invalidateQueries({ queryKey: financeKeys.summary }); },
  });
}

export function useCreateCost() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: Record<string, unknown>) => api.post<CostEntryDto>(endpoints.costs.root, body),
    onSuccess: () => { void client.invalidateQueries({ queryKey: ["costs"] }); void client.invalidateQueries({ queryKey: financeKeys.summary }); },
  });
}
