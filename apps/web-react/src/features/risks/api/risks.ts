import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export type RiskLevel = "Low" | "Medium" | "High" | "Critical";
export type RiskStatus = "Identified" | "ActionRequired" | "FollowUp" | "Mitigated" | "Closed";
export type RiskActionStatus = "Planned" | "InProgress" | "Completed" | "Cancelled";
export type RiskImpactType = "Schedule" | "Budget" | "Scope" | "Quality" | "Resources" | "Procurement" | "Compliance" | "Reputation" | "Operations" | "Other";
export interface RiskDto { id: string; projectId: string; projectName: string; title: string; description: string; riskLevel: RiskLevel; impact?: string; likelihood?: string; status: RiskStatus; ownerResourceId?: string; ownerResourceName?: string; createdAt: string; updatedAt?: string; lastActionUpdateAt?: string; lastActionUpdateText?: string; impactTypes?: string[]; rowVersion: number[]; }
export interface CreateRiskDto { projectId: string; title: string; description: string; riskLevel: RiskLevel; impact?: string; likelihood?: string; ownerResourceId?: string; impactAssessment?: { impactTypes: RiskImpactType[]; description: string }; }
export interface UpdateRiskDto { title: string; description: string; riskLevel: RiskLevel; ownerResourceId?: string; rowVersion?: number[]; }
export interface RiskImpactAssessmentDto { id: string; riskId: string; impactTypes: RiskImpactType[]; description: string; createdAt: string; rowVersion: number[]; }
export interface RiskActionDto { id: string; riskId: string; title: string; description: string; ownerResourceId?: string; ownerResourceName?: string; targetDate?: string; nextFollowUpDate?: string; status: RiskActionStatus; sortOrder: number; createdAt: string; updatedAt?: string; rowVersion: number[]; updateCount: number; }
export interface RiskActionWriteDto { title: string; description: string; ownerResourceId?: string; targetDate?: string; nextFollowUpDate?: string; status: RiskActionStatus; sortOrder?: number; rowVersion?: number[]; }
export interface RiskActionUpdateDto { id: string; riskActionId: string; updateText: string; createdBy: string; createdAt: string; entryType: "Comment" | "SystemChange"; changes?: Array<{ field: string; oldValue?: string | null; newValue?: string | null }>; }

export const riskKeys = {
  all: ["risks"] as const,
  list: (filters: RiskListFilters = {}) => ["risks", "list", filters] as const,
  detail: (id: string) => ["risks", "detail", id] as const,
  impacts: (id: string) => ["risks", "impacts", id] as const,
  actions: (id: string) => ["risks", "actions", id] as const,
  updates: (riskId: string, actionId: string) => ["risks", "updates", riskId, actionId] as const,
};

interface PagedRisksDto {
  items: RiskDto[];
  totalCount: number;
  totalPages: number;
}

export interface RiskListFilters {
  projectId?: string;
  ownerResourceId?: string;
  status?: string;
  search?: string;
  pageSize?: number;
}

async function fetchRiskCollection(filters: RiskListFilters): Promise<{ items: RiskDto[]; totalCount: number }> {
  const pageSize = filters.pageSize ?? 100;
  const getPage = (page: number) => api.get<PagedRisksDto>(endpoints.risks.root, { ...filters, page, pageSize });
  const firstPage = await getPage(1);
  if ((filters.pageSize != null && filters.pageSize < 100) || firstPage.totalPages <= 1) {
    return { items: [...firstPage.items], totalCount: firstPage.totalCount };
  }

  const remainingPages = await Promise.all(
    Array.from({ length: firstPage.totalPages - 1 }, (_, index) => getPage(index + 2))
  );
  return {
    items: [firstPage.items, ...remainingPages.map((page) => page.items)].flat(),
    totalCount: firstPage.totalCount,
  };
}

export function useRisks(filters: RiskListFilters = {}, enabled = true) {
  return useQuery({
    queryKey: riskKeys.list(filters),
    queryFn: () => fetchRiskCollection(filters),
    enabled,
    staleTime: 30_000,
  });
}

export function useRiskDashboard(enabled = true) {
  return useRisks({}, enabled);
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

export function useUpdateRisk() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateRiskDto }) => api.put<RiskDto>(endpoints.risks.detail(id), data),
    onSuccess: (_value, variables) => { void client.invalidateQueries({ queryKey: riskKeys.all }); void client.invalidateQueries({ queryKey: riskKeys.detail(variables.id) }); },
  });
}

export function useUpdateRiskStatus() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status, rowVersion }: { id: string; status: RiskStatus; rowVersion?: number[] }) =>
      api.put<RiskDto>(endpoints.risks.status(id), { status, rowVersion }),
    onSuccess: (_d, v) => {
      void client.invalidateQueries({ queryKey: riskKeys.detail(v.id) });
      void client.invalidateQueries({ queryKey: riskKeys.all });
    },
  });
}

export function useRiskImpacts(id: string, enabled = true) {
  return useQuery({ queryKey: riskKeys.impacts(id), queryFn: () => api.get<RiskImpactAssessmentDto[]>(endpoints.risks.impacts(id)), enabled: Boolean(id) && enabled, staleTime: 30_000 });
}
export function useRiskActions(id: string, enabled = true) {
  return useQuery({ queryKey: riskKeys.actions(id), queryFn: () => api.get<RiskActionDto[]>(endpoints.risks.actions(id)), enabled: Boolean(id) && enabled, staleTime: 30_000 });
}
export function useRiskActionUpdates(riskId: string, actionId: string, enabled = true) {
  return useQuery({ queryKey: riskKeys.updates(riskId, actionId), queryFn: () => api.get<RiskActionUpdateDto[]>(endpoints.risks.updates(riskId, actionId)), enabled: Boolean(riskId && actionId) && enabled, staleTime: 30_000 });
}
export function useCreateRiskImpact() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, data }: { riskId: string; data: { impactTypes: RiskImpactType[]; description: string } }) => api.post<RiskImpactAssessmentDto>(endpoints.risks.impacts(riskId), data), onSuccess: (_value, variables) => { void client.invalidateQueries({ queryKey: riskKeys.impacts(variables.riskId) }); void client.invalidateQueries({ queryKey: riskKeys.detail(variables.riskId) }); } });
}
export function useCreateRiskAction() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, data }: { riskId: string; data: RiskActionWriteDto }) => api.post<RiskActionDto>(endpoints.risks.actions(riskId), data), onSuccess: (_value, variables) => { void client.invalidateQueries({ queryKey: riskKeys.actions(variables.riskId) }); void client.invalidateQueries({ queryKey: riskKeys.detail(variables.riskId) }); } });
}
export function useUpdateRiskAction() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, actionId, data }: { riskId: string; actionId: string; data: RiskActionWriteDto }) => api.put<RiskActionDto>(endpoints.risks.action(riskId, actionId), data), onSuccess: (_value, variables) => { void client.invalidateQueries({ queryKey: riskKeys.actions(variables.riskId) }); void client.invalidateQueries({ queryKey: riskKeys.updates(variables.riskId, variables.actionId) }); void client.invalidateQueries({ queryKey: riskKeys.detail(variables.riskId) }); void client.invalidateQueries({ queryKey: riskKeys.all }); } });
}
export function useDeleteRiskAction() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, actionId }: { riskId: string; actionId: string }) => api.delete<void>(endpoints.risks.action(riskId, actionId)), onSuccess: (_value, variables) => { void client.invalidateQueries({ queryKey: riskKeys.actions(variables.riskId) }); void client.invalidateQueries({ queryKey: riskKeys.detail(variables.riskId) }); void client.invalidateQueries({ queryKey: riskKeys.all }); } });
}
export function useAddRiskActionUpdate() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, actionId, updateText }: { riskId: string; actionId: string; updateText: string }) => api.post<RiskActionUpdateDto>(endpoints.risks.updates(riskId, actionId), { updateText }), onSuccess: (_value, variables) => { void client.invalidateQueries({ queryKey: riskKeys.updates(variables.riskId, variables.actionId) }); void client.invalidateQueries({ queryKey: riskKeys.detail(variables.riskId) }); } });
}
