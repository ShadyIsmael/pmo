"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";

export interface HomeSummary {
  activeProjectsCount: number;
  projectsAtRiskCount: number;
  openRisksCount: number;
  criticalOpenRisksCount: number;
  projectHealthCounts: Record<string, number>;
  budgetByCurrency: Array<{ currency: string; budget: number; actualCost: number }>;
  attentionProjects: Array<{ id: string; name: string; progress: number; health: string; ragStatus: string }>;
  topRisks: Array<{ id: string; title: string; projectName: string; riskLevel: string; status: string }>;
}

export const homeSummaryKeys = {
  all: ["home", "summary"] as const,
};

export function useHomeSummary(params: { portfolioId?: string | null; programId?: string | null; projectId?: string | null } = {}, enabled = true) {
  return useQuery({
    queryKey: [...homeSummaryKeys.all, params],
    queryFn: () => api.get<HomeSummary>(endpoints.home.summary, {
      portfolioId: params.portfolioId ?? undefined,
      programId: params.programId ?? undefined,
      projectId: params.projectId ?? undefined,
    }),
    enabled,
    staleTime: 30_000,
    gcTime: 5 * 60_000,
  });
}
