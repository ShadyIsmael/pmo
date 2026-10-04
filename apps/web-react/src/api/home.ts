import { useQuery } from "@tanstack/react-query";
import { api } from "./client";

export interface HomeBudgetSummary { currency: string; budget: number; actualCost: number; }
export interface HomeProjectSummary { id: string; name: string; progress: number; health: string; ragStatus: string; }
export interface HomeRiskSummary { id: string; title: string; projectName: string; riskLevel: string; status: string; }
export interface HomeSummary { activeProjectsCount: number; projectsAtRiskCount: number; openRisksCount: number; criticalOpenRisksCount: number; projectHealthCounts: Record<string, number>; budgetByCurrency: HomeBudgetSummary[]; attentionProjects: HomeProjectSummary[]; topRisks: HomeRiskSummary[]; }

export const homeKeys = { summary: ["home", "summary"] as const };

export function useHomeSummary(enabled: boolean) {
  return useQuery({
    queryKey: homeKeys.summary,
    queryFn: () => api.get<HomeSummary>("/api/home/summary"),
    enabled,
    staleTime: 30_000,
    gcTime: 300_000,
    refetchOnWindowFocus: false,
  });
}
