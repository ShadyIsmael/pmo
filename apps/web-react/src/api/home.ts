import { useQuery } from "@tanstack/react-query";
import { api } from "./client";

export interface HomeBudgetSummary { currency: string; budget: number; actualCost: number; }
export interface HomeProjectSummary { id: string; name: string; progress: number; health: string; ragStatus: string; }
export interface HomeRiskSummary { id: string; title: string; projectName: string; riskLevel: string; status: string; }
export interface HomeSummary { activeProjectsCount: number; projectsAtRiskCount: number; openRisksCount: number; criticalOpenRisksCount: number; projectHealthCounts: Record<string, number>; budgetByCurrency: HomeBudgetSummary[]; attentionProjects: HomeProjectSummary[]; topRisks: HomeRiskSummary[]; }
export interface HomeSummaryFilters extends Record<string, string | undefined> { portfolioId?: string; programId?: string; projectId?: string; }

export const homeKeys = { summary: (filters: HomeSummaryFilters = {}) => ["home", "summary", filters] as const };

export function useHomeSummary(filtersOrEnabled: HomeSummaryFilters | boolean = {}, enabledArgument = true) {
  const filters = typeof filtersOrEnabled === "boolean" ? {} : filtersOrEnabled;
  const enabled = typeof filtersOrEnabled === "boolean" ? filtersOrEnabled : enabledArgument;
  return useQuery({
    queryKey: homeKeys.summary(filters),
    queryFn: () => api.get<HomeSummary>("/api/home/summary", filters),
    enabled,
    staleTime: 30_000,
    gcTime: 300_000,
    refetchOnWindowFocus: false,
  });
}
