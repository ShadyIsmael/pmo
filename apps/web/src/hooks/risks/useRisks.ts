"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { riskApi, riskKeys, type CreateRiskActionDto, type CreateRiskActionUpdateDto, type CreateRiskDto, type CreateRiskImpactAssessmentDto, type RiskDto, type RiskStatus, type UpdateRiskActionDto, type UpdateRiskDto, type UpdateRiskImpactAssessmentDto } from "@/lib/api/risks";

type RiskCollection = { items: readonly RiskDto[]; totalCount: number };

async function fetchAllRisks(projectId?: string, ownerResourceId?: string): Promise<RiskCollection> {
  const pageSize = 100;
  const firstPage = await riskApi.list({ page: 1, pageSize, projectId, ownerResourceId });
  if (firstPage.totalPages <= 1) return { items: [...firstPage.items], totalCount: firstPage.totalCount };

  const remainingPages = await Promise.all(
    Array.from({ length: firstPage.totalPages - 1 }, (_, index) => riskApi.list({ page: index + 2, pageSize, projectId, ownerResourceId }))
  );

  return {
    items: [firstPage.items, ...remainingPages.map((page) => page.items)].flat(),
    totalCount: firstPage.totalCount,
  };
}

export function useRisks(projectId?: string, enabled = true, ownerResourceId?: string) {
  return useQuery({
    queryKey: riskKeys.list({ ...(projectId ? { projectId } : {}), ...(ownerResourceId ? { ownerResourceId } : {}) }),
    queryFn: () => fetchAllRisks(projectId, ownerResourceId),
    staleTime: 30_000,
    enabled,
  });
}

export function useRiskDashboard() {
  return useRisks();
}

export function useRisk(id: string) {
  return useQuery({ queryKey: riskKeys.detail(id), queryFn: () => riskApi.detail(id), enabled: Boolean(id), retry: false, staleTime: 30_000 });
}

export function useRiskActions(riskId: string, enabled = true) {
  return useQuery({ queryKey: riskKeys.actions(riskId), queryFn: () => riskApi.actions(riskId), enabled: Boolean(riskId) && enabled, retry: false, staleTime: 30_000 });
}

export function useRiskImpacts(riskId: string, enabled = true) {
  return useQuery({ queryKey: riskKeys.impacts(riskId), queryFn: () => riskApi.impacts(riskId), enabled: Boolean(riskId) && enabled, retry: false, staleTime: 30_000 });
}

export function useRiskActionUpdates(riskId: string, actionId: string, enabled = true) {
  return useQuery({ queryKey: riskKeys.updates(riskId, actionId), queryFn: () => riskApi.updates(riskId, actionId), enabled: Boolean(riskId && actionId) && enabled, staleTime: 30_000 });
}

export function useProjectRisks(projectId: string, pageSize = 100, enabled = true) {
  return useQuery({
    queryKey: riskKeys.project(projectId),
    queryFn: () => riskApi.project(projectId, { page: 1, pageSize }),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useCreateRisk() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateRiskDto) => riskApi.create(data),
    retry: false,
    onSuccess: (_risk, variables) => {
      queryClient.invalidateQueries({ queryKey: riskKeys.list() });
      queryClient.invalidateQueries({ queryKey: riskKeys.dashboard() });
      queryClient.invalidateQueries({ queryKey: riskKeys.project(variables.projectId), exact: true });
    },
  });
}

function invalidateRisk(queryClient: ReturnType<typeof useQueryClient>, riskId: string) {
  queryClient.invalidateQueries({ queryKey: riskKeys.detail(riskId) });
  queryClient.invalidateQueries({ queryKey: riskKeys.actions(riskId) });
  queryClient.invalidateQueries({ queryKey: riskKeys.impacts(riskId) });
  queryClient.invalidateQueries({ queryKey: riskKeys.list() });
  queryClient.invalidateQueries({ queryKey: riskKeys.dashboard() });
  queryClient.invalidateQueries({ queryKey: riskKeys.project("") });
}

export function useCreateRiskImpact() {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, data }: { riskId: string; data: CreateRiskImpactAssessmentDto }) => riskApi.createImpact(riskId, data), retry: false, onSuccess: (_value, variables) => queryClient.invalidateQueries({ queryKey: riskKeys.impacts(variables.riskId) }) });
}

export function useUpdateRiskImpact() {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, impactId, data }: { riskId: string; impactId: string; data: UpdateRiskImpactAssessmentDto }) => riskApi.updateImpact(riskId, impactId, data), retry: false, onSuccess: (_value, variables) => queryClient.invalidateQueries({ queryKey: riskKeys.impacts(variables.riskId) }) });
}

export function useDeleteRiskImpact() {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, impactId }: { riskId: string; impactId: string }) => riskApi.deleteImpact(riskId, impactId), retry: false, onSuccess: (_value, variables) => queryClient.invalidateQueries({ queryKey: riskKeys.impacts(variables.riskId) }) });
}

export function useUpdateRisk() {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: ({ id, data }: { id: string; data: UpdateRiskDto }) => riskApi.update(id, data), retry: false, onSuccess: (_value, variables) => invalidateRisk(queryClient, variables.id) });
}

export function useUpdateRiskStatus() {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: ({ id, status, rowVersion }: { id: string; status: RiskStatus; rowVersion?: number[] }) => riskApi.updateStatus(id, { status, rowVersion }), retry: false, onSuccess: (_value, variables) => invalidateRisk(queryClient, variables.id) });
}

export function useCreateRiskAction() {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, data }: { riskId: string; data: CreateRiskActionDto }) => riskApi.createAction(riskId, data), retry: false, onSuccess: (_value, variables) => invalidateRisk(queryClient, variables.riskId) });
}

export function useUpdateRiskAction() {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, actionId, data }: { riskId: string; actionId: string; data: UpdateRiskActionDto }) => riskApi.updateAction(riskId, actionId, data), retry: false, onSuccess: (_value, variables) => { invalidateRisk(queryClient, variables.riskId); queryClient.invalidateQueries({ queryKey: riskKeys.updates(variables.riskId, variables.actionId) }); } });
}

export function useDeleteRiskAction() {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, actionId }: { riskId: string; actionId: string }) => riskApi.deleteAction(riskId, actionId), retry: false, onSuccess: (_value, variables) => invalidateRisk(queryClient, variables.riskId) });
}

export function useAddRiskActionUpdate() {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: ({ riskId, actionId, data }: { riskId: string; actionId: string; data: CreateRiskActionUpdateDto }) => riskApi.addUpdate(riskId, actionId, data), retry: false, onSuccess: (_value, variables) => { queryClient.invalidateQueries({ queryKey: riskKeys.updates(variables.riskId, variables.actionId) }); queryClient.invalidateQueries({ queryKey: riskKeys.actions(variables.riskId) }); queryClient.invalidateQueries({ queryKey: riskKeys.detail(variables.riskId) }); queryClient.invalidateQueries({ queryKey: riskKeys.list() }); queryClient.invalidateQueries({ queryKey: riskKeys.dashboard() }); } });
}
