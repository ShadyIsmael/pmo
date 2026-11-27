import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@auth/AuthProvider";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { documentKeys } from "./documents";
import { projectKeys } from "./projects";

export type ProjectPhaseGateReadinessStatus = "Ready" | "Incomplete" | "NotConfigured" | "NotApplicable";
export type ProjectPhaseGateTransitionStatus = "AwaitingTransition" | "Completed" | "NotApplicable";
export type ProjectPhaseGateRequirementType = "Mandatory" | "Optional";
export type ProjectPhaseGateRequirementStatus = "Missing" | "Satisfied" | "OptionalNotProvided" | "InvalidOrUnavailable";
export type ProjectDocumentComplianceStatus = "Compliant" | "Mismatch" | "NotVerified" | "PendingValidation" | "Missing";

export interface ProjectDocumentComplianceFinding {
  code: string;
  messageEn: string;
  messageAr: string;
  expected?: string | null;
  detected?: string | null;
}

export interface ProjectPhaseGateRequirementStatusDto {
  requirementSnapshotId: string;
  sourceRequirementId: string;
  templateId: string;
  templateVersionId: string;
  templateName: string;
  category?: string | null;
  templateVersionLabel: string;
  requirementType: ProjectPhaseGateRequirementType;
  status: ProjectPhaseGateRequirementStatus;
  documentId?: string | null;
  documentVersionId?: string | null;
  documentName?: string | null;
  originalFileName?: string | null;
  documentVersionNumber?: number | null;
  statusMessage?: string | null;
  complianceStatus: ProjectDocumentComplianceStatus;
  expectedFileType?: string | null;
  detectedFileType?: string | null;
  comparedAt?: string | null;
  validatorVersion?: string | null;
  complianceFindings?: ProjectDocumentComplianceFinding[] | null;
  templateVersionNumber?: number | null;
  templateFileName?: string | null;
  supportsEvidenceLinking?: boolean;
}

export interface ProjectPhaseGateReadinessDto {
  projectId: string;
  organizationId?: string | null;
  currentPhaseId?: string | null;
  currentPhase: string;
  currentPhaseLabelEn?: string | null;
  currentPhaseLabelAr?: string | null;
  targetPhaseId?: string | null;
  targetPhase?: string | null;
  targetPhaseLabelEn?: string | null;
  targetPhaseLabelAr?: string | null;
  snapshotId?: string | null;
  configurationId?: string | null;
  configurationVersionId?: string | null;
  configurationVersionNumber?: number | null;
  readinessStatus: ProjectPhaseGateReadinessStatus;
  phaseTransitionStatus: ProjectPhaseGateTransitionStatus;
  mandatoryRequirementCount: number;
  satisfiedMandatoryCount: number;
  missingMandatoryCount: number;
  optionalRequirementCount: number;
  satisfiedOptionalCount: number;
  readinessPercentage?: number | null;
  projectRowVersion?: string | null;
  availableTransitions: ProjectPhaseGateTransitionOptionDto[];
  requirements: ProjectPhaseGateRequirementStatusDto[];
}

export interface ProjectPhaseGateTransitionOptionDto {
  targetPhaseId: string;
  targetPhase: string;
  targetPhaseLabelEn?: string | null;
  targetPhaseLabelAr?: string | null;
  configurationId?: string | null;
  configurationVersionId?: string | null;
  configurationVersionNumber?: number | null;
  readinessStatus: ProjectPhaseGateReadinessStatus;
  readinessEvaluated: boolean;
}

export interface ProjectPhaseTransitionResult {
  projectId: string;
  previousPhaseId: string;
  newPhaseId: string;
  previousPhase: string;
  newPhase: string;
  configurationVersionId?: string | null;
  configurationVersionNumber?: number | null;
  projectRowVersion?: string | null;
  transitionedAt: string;
}

export interface LinkPhaseGateDocumentRequest {
  requirementSnapshotId: string;
  documentId: string;
  documentVersionId: string;
}

export interface ChangeProjectPhaseRequest {
  targetPhaseId: string;
  expectedProjectVersionToken: string;
}

export interface ProjectPhaseHistoryRequirement {
  requirementSnapshotId: string;
  templateId: string;
  templateVersionId: string;
  templateName: string;
  category?: string | null;
  templateVersionLabel: string;
  requirementType: ProjectPhaseGateRequirementType;
  status: ProjectPhaseGateRequirementStatus;
  complianceStatus: ProjectDocumentComplianceStatus;
  expectedFileType?: string | null;
  detectedFileType?: string | null;
  comparedAt?: string | null;
  validatorVersion?: string | null;
  /** Backend contract exposes the immutable snapshot findings as `findings`. */
  findings?: ProjectDocumentComplianceFinding[] | null;
  /** Tolerate the audit snapshot field name when a legacy record is projected verbatim. */
  complianceFindings?: ProjectDocumentComplianceFinding[] | null;
}

export interface ProjectPhaseHistoryItem {
  id: string;
  projectId: string;
  previousPhaseId?: string | null;
  newPhaseId?: string | null;
  previousPhase?: string | null;
  newPhase?: string | null;
  previousPhaseLabelEn?: string | null;
  previousPhaseLabelAr?: string | null;
  newPhaseLabelEn?: string | null;
  newPhaseLabelAr?: string | null;
  configurationVersionId?: string | null;
  configurationVersionNumber?: number | null;
  evaluationOutcome?: string | null;
  mandatoryRequirementCount?: number | null;
  satisfiedMandatoryCount?: number | null;
  missingMandatoryCount?: number | null;
  optionalRequirementCount?: number | null;
  satisfiedOptionalCount?: number | null;
  actorId?: string | null;
  actorName?: string | null;
  transitionedAt: string;
  requirements?: ProjectPhaseHistoryRequirement[] | null;
}

export const projectPhaseGateKeys = {
  all: (tenantId?: string) => ["projects", "phase-gate", tenantId] as const,
  readiness: (projectId: string, targetPhaseId?: string, tenantId?: string) =>
    [...projectPhaseGateKeys.all(tenantId), projectId, "readiness", targetPhaseId ?? ""] as const,
  history: (projectId: string, tenantId?: string) =>
    [...projectPhaseGateKeys.all(tenantId), projectId, "history"] as const,
};

export function useProjectPhaseGateReadiness(projectId: string, targetPhaseId: string | undefined, enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: projectPhaseGateKeys.readiness(projectId, targetPhaseId, user?.tenantId),
    queryFn: () => api.get<ProjectPhaseGateReadinessDto>(endpoints.projects.phaseGateReadiness(projectId, targetPhaseId)),
    enabled: Boolean(projectId) && enabled,
    staleTime: 10_000,
  });
}

/**
 * Immutable phase-change history for a project, newest first as returned by the API.
 * The endpoint path lives here because `endpoints.ts` is outside this change's write scope.
 */
export function useProjectPhaseHistory(projectId: string, enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: projectPhaseGateKeys.history(projectId, user?.tenantId),
    queryFn: () => api.get<ProjectPhaseHistoryItem[]>(`/api/projects/${encodeURIComponent(projectId)}/phase-gate/history`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 10_000,
  });
}

export function useLinkPhaseGateDocument(projectId: string) {
  const { user } = useAuth();
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ requirementSnapshotId, documentId, documentVersionId }: LinkPhaseGateDocumentRequest) =>
      api.post<ProjectPhaseGateReadinessDto>(endpoints.projects.phaseGateRequirementDocument(projectId, requirementSnapshotId), { documentId, documentVersionId }),
    onSuccess: (readiness) => {
      client.setQueryData(projectPhaseGateKeys.readiness(projectId, readiness.targetPhaseId ?? undefined, user?.tenantId), readiness);
      void client.invalidateQueries({ queryKey: projectPhaseGateKeys.all(user?.tenantId) });
      void client.invalidateQueries({ queryKey: projectKeys.all });
      void client.invalidateQueries({ queryKey: projectKeys.shell(projectId) });
      void client.invalidateQueries({ queryKey: documentKeys.all });
    },
  });
}

export function useUnlinkPhaseGateDocument(projectId: string) {
  const { user } = useAuth();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (requirementSnapshotId: string) =>
      api.delete<ProjectPhaseGateReadinessDto>(endpoints.projects.phaseGateRequirementDocument(projectId, requirementSnapshotId)),
    onSuccess: (readiness) => {
      client.setQueryData(projectPhaseGateKeys.readiness(projectId, readiness.targetPhaseId ?? undefined, user?.tenantId), readiness);
      void client.invalidateQueries({ queryKey: projectPhaseGateKeys.all(user?.tenantId) });
      void client.invalidateQueries({ queryKey: projectKeys.all });
      void client.invalidateQueries({ queryKey: projectKeys.shell(projectId) });
      void client.invalidateQueries({ queryKey: documentKeys.all });
    },
  });
}

export function useChangeProjectPhase(projectId: string) {
  const { user } = useAuth();
  const client = useQueryClient();
  return useMutation({
    mutationFn: (request: ChangeProjectPhaseRequest) =>
      api.post<ProjectPhaseTransitionResult>(endpoints.projects.phaseGateChange(projectId), request),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: projectPhaseGateKeys.all(user?.tenantId) });
      void client.invalidateQueries({ queryKey: projectKeys.all });
      void client.invalidateQueries({ queryKey: projectKeys.shell(projectId) });
    },
  });
}
