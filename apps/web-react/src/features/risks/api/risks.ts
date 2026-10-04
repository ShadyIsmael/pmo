import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export type RiskLevel = "Low" | "Medium" | "High" | "Critical";
export type RiskStatus = "Identified" | "ActionRequired" | "FollowUp" | "Mitigated" | "Closed";
export interface RiskDto { id: string; projectId: string; projectName: string; title: string; description: string; riskLevel: RiskLevel; status: RiskStatus; ownerResourceId?: string; ownerResourceName?: string; createdAt: string; rowVersion: number[]; }
export interface CreateRiskDto { projectId: string; title: string; description: string; riskLevel: RiskLevel; ownerResourceId?: string; }

export const riskKeys = {
  all: ["risks"] as const,
  list: (filters: Record<string, string | undefined> = {}) => ["risks", "list", filters] as const,
  detail: (id: string) => ["risks", "detail", id] as const,
};

export function useRisks(filters: { projectId?: string; status?: string; search?: string } = {}, enabled = true) {
  return useQuery({
    queryKey: riskKeys.list(filters as Record<string, string | undefined>),
    queryFn: () => api.get<{ items: RiskDto[]; totalCount: number }>(endpoints.risks.root, { ...filters, pageSize: 100 }),
    enabled,
    staleTime: 30_000,
  });
}

export function useRisk(id: string, enabled = true) {
  return useQuery({
    queryKey: riskKeys.detail(id),
    queryFn: () => api.get<RiskDto>(endpoints.risks.detail(id)),
    enabled: Boolean(id) && enabled,
    staleTime: 30_000,
  });
}

export function useCreateRisk() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateRiskDto) => api.post<RiskDto>(endpoints.risks.root, data),
    onSuccess: () => { void client.invalidateQueries({ queryKey: riskKeys.all }); },
  });
}

export function useUpdateRiskStatus() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status, rowVersion }: { id: string; status: RiskStatus; rowVersion?: number[] }) =>
      api.put<RiskDto>(`/api/risks/${encodeURIComponent(id)}/status`, { status, rowVersion }),
    onSuccess: (_d, v) => {
      void client.invalidateQueries({ queryKey: riskKeys.detail(v.id) });
      void client.invalidateQueries({ queryKey: riskKeys.all });
    },
  });
}
