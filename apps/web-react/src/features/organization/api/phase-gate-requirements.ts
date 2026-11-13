import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { useAuth } from "@auth/AuthProvider";

export type PhaseGateRequirementType = "Mandatory" | "Optional";

export interface PhaseGateTransition {
  sourcePhaseId: string;
  targetPhaseId: string;
  /** Stable catalog codes retained for historical display compatibility. */
  sourcePhase: string;
  sourceLabelEn: string;
  sourceLabelAr: string;
  targetPhase: string;
  targetLabelEn: string;
  targetLabelAr: string;
}

export interface PhaseGateRequirement {
  id: string;
  templateId: string;
  templateVersionId: string;
  templateName: string;
  category?: string | null;
  version: string;
  requirementType: PhaseGateRequirementType;
}

export interface PhaseGateRequirementsConfiguration {
  configurationId: string | null;
  organizationId: string;
  sourcePhaseId: string;
  targetPhaseId: string;
  /** Stable catalog codes retained for historical display compatibility. */
  sourcePhase: string;
  targetPhase: string;
  configurationVersionId: string | null;
  configurationVersion: number;
  versionToken: string | null;
  createdAt?: string | null;
  createdBy?: string | null;
  updatedAt?: string | null;
  updatedBy?: string | null;
  requirements: PhaseGateRequirement[];
}

export interface SavePhaseGateRequirementsRequest {
  expectedVersionToken: string | null;
  requirements: Array<{ templateId: string; templateVersionId: string; requirementType: PhaseGateRequirementType }>;
}

export interface PhaseGateConfigurationHistoryVersion {
  configurationVersionId: string;
  configurationVersion: number;
  createdAt: string;
  createdBy: string;
  requirements: PhaseGateRequirement[];
}

export interface PhaseGateConfigurationHistory {
  configurationId: string;
  organizationId: string;
  sourcePhaseId: string;
  targetPhaseId: string;
  sourcePhase: string;
  targetPhase: string;
  versions: PhaseGateConfigurationHistoryVersion[];
}

export interface TemplateCategoryGroup {
  categoryId: string;
  categoryName: string;
  categoryNameAr?: string | null;
  templates: PhaseGateRequirement[];
}

const phaseGateKeys = {
  transitions: (organizationId: string, tenantId?: string) => ["organization", "phase-gate", "transitions", tenantId, organizationId] as const,
  configuration: (organizationId: string, sourcePhaseId: string, targetPhaseId: string, tenantId?: string) =>
    ["organization", "phase-gate", "configuration", tenantId, organizationId, sourcePhaseId, targetPhaseId] as const,
  templatesByCategory: (organizationId: string, sourcePhaseId: string, categoryId?: string, tenantId?: string) =>
    ["organization", "phase-gate", "templates-by-category", tenantId, organizationId, sourcePhaseId, categoryId ?? "all"] as const,
};

export function usePhaseGateTransitions(organizationId: string, enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: phaseGateKeys.transitions(organizationId, user?.tenantId),
    queryFn: () => api.get<PhaseGateTransition[]>(endpoints.organization.phaseGateTransitions(organizationId)),
    enabled: enabled && Boolean(organizationId),
    staleTime: 60_000,
  });
}

export function usePhaseGateRequirements(organizationId: string, transition: Pick<PhaseGateTransition, "sourcePhaseId" | "targetPhaseId"> | null, enabled = true) {
  const { user } = useAuth();
  const sourcePhaseId = transition?.sourcePhaseId ?? "";
  const targetPhaseId = transition?.targetPhaseId ?? "";
  return useQuery({
    queryKey: phaseGateKeys.configuration(organizationId, sourcePhaseId, targetPhaseId, user?.tenantId),
    queryFn: () => api.get<PhaseGateRequirementsConfiguration>(
      endpoints.organization.phaseGateRequirements(organizationId, sourcePhaseId, targetPhaseId),
    ),
    enabled: enabled && Boolean(organizationId && sourcePhaseId && targetPhaseId),
    staleTime: 15_000,
  });
}

export function usePhaseGateRequirementHistory(organizationId: string, transition: Pick<PhaseGateTransition, "sourcePhaseId" | "targetPhaseId"> | null, enabled = true) {
  const { user } = useAuth();
  const sourcePhaseId = transition?.sourcePhaseId ?? "";
  const targetPhaseId = transition?.targetPhaseId ?? "";
  return useQuery({
    queryKey: ["organization", "phase-gate", "history", user?.tenantId, organizationId, sourcePhaseId, targetPhaseId],
    queryFn: () => api.get<PhaseGateConfigurationHistory>(
      endpoints.organization.phaseGateRequirementHistory(organizationId, sourcePhaseId, targetPhaseId),
    ),
    enabled: enabled && Boolean(organizationId && sourcePhaseId && targetPhaseId),
    staleTime: 30_000,
  });
}

export function usePhaseGateTemplatesByCategory(organizationId: string, sourcePhaseId: string, categoryId?: string, enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: phaseGateKeys.templatesByCategory(organizationId, sourcePhaseId, categoryId, user?.tenantId),
    queryFn: () => api.get<TemplateCategoryGroup[]>(
      endpoints.organization.phaseGateTemplatesByCategory(organizationId, sourcePhaseId, categoryId),
    ),
    enabled: enabled && Boolean(organizationId && sourcePhaseId),
    staleTime: 60_000,
  });
}

export function useSavePhaseGateRequirements(organizationId: string, transition: Pick<PhaseGateTransition, "sourcePhaseId" | "targetPhaseId"> | null) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const sourcePhaseId = transition?.sourcePhaseId ?? "";
  const targetPhaseId = transition?.targetPhaseId ?? "";
  const queryKey = phaseGateKeys.configuration(organizationId, sourcePhaseId, targetPhaseId, user?.tenantId);
  return useMutation({
    mutationFn: (body: SavePhaseGateRequirementsRequest) => api.put<PhaseGateRequirementsConfiguration>(
      endpoints.organization.phaseGateRequirements(organizationId, sourcePhaseId, targetPhaseId),
      body,
    ),
    onSuccess: (configuration) => {
      queryClient.setQueryData(queryKey, configuration);
      void queryClient.invalidateQueries({ queryKey: ["organization", "phase-gate", "configuration", user?.tenantId, organizationId] });
    },
  });
}
