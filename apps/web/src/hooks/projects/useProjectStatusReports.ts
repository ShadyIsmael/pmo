"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { type PagedResult } from "@/lib/api";
import { projectApi, projectKeys } from "@/lib/api/projects";

export type ProjectStatusReportState = "Draft" | "Published";
export type ProjectHealth = "Green" | "Amber" | "Red";

export interface ProjectStatusReportCurrentProgressDto {
  projectPlanProgressPercent?: number;
}

export interface ProjectStatusReportListDto {
  id: string;
  projectId: string;
  reportDate: string;
  periodFrom: string;
  periodTo: string;
  reportState: ProjectStatusReportState;
  projectLifecycleStatusAtReport?: string;
  overallHealth?: ProjectHealth;
  overallProgressPercent?: number;
  preparedByNameSnapshot?: string;
  publishedAt?: string;
}

export interface ProjectStatusReport extends ProjectStatusReportListDto {
  projectNameSnapshot?: string;
  projectCodeSnapshot?: string;
  projectManagerIdAtReport?: string;
  projectManagerNameSnapshot?: string;
  scheduleHealth?: ProjectHealth;
  costHealth?: ProjectHealth;
  scopeHealth?: ProjectHealth;
  qualityHealth?: ProjectHealth;
  resourceHealth?: ProjectHealth;
  executiveSummary?: string;
  keyAchievements?: string;
  challengesConcerns?: string;
  nextPeriodPriorities?: string;
  preparedByUserId?: string;
  publishedByUserId?: string;
  publishedByNameSnapshot?: string;
  sourceDataCapturedAt?: string;
  currencyCodeSnapshot?: string;
  budgetAmountSnapshot?: number;
  actualCostAmountSnapshot?: number;
  committedCostAmountSnapshot?: number;
  forecastCostAmountSnapshot?: number;
  costVarianceAmountSnapshot?: number;
  scheduleVarianceDaysSnapshot?: number;
  scheduleVariancePercentSnapshot?: number;
  baselineIdAtReport?: string;
  baselineNameSnapshot?: string;
  baselineVersionSnapshot?: number;
  risks: StatusReportRiskSnapshot[];
  issues: StatusReportIssueSnapshot[];
  changeRequests: StatusReportChangeRequestSnapshot[];
  rowVersion: number[];
}

export interface StatusReportRiskSnapshot { sourceRiskId: string; rank: number; title: string; description?: string; impact?: string; likelihood?: string; riskLevel: string; riskStatus: string; potentialCost?: number; potentialDelay?: number; riskResponse?: string; ownerId?: string; ownerNameSnapshot?: string; }
export interface StatusReportIssueSnapshot { sourceIssueId: string; rank: number; title: string; description?: string; severity: string; priority: string; issueStatus: string; assigneeId?: string; assigneeNameSnapshot?: string; resolution?: string; }
export interface StatusReportChangeRequestSnapshot { sourceChangeRequestId: string; rank: number; number: number; changeType: string; reason: string; justification?: string; impact?: string; changeRequestStatus: string; requestedBy: string; requestedByNameSnapshot?: string; requestedAt: string; reviewedAt?: string; decisionNotes?: string; }

export interface ProjectStatusReportInput {
  reportDate: string;
  periodFrom: string;
  periodTo: string;
  projectLifecycleStatusAtReport?: string;
  overallHealth?: string;
  overallProgressPercent?: number;
  scheduleHealth?: string;
  costHealth?: string;
  scopeHealth?: string;
  qualityHealth?: string;
  resourceHealth?: string;
  executiveSummary?: string;
  keyAchievements?: string;
  challengesConcerns?: string;
  nextPeriodPriorities?: string;
}

export function useProjectStatusReports(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.statusReports(projectId),
    queryFn: () => projectApi.statusReports<PagedResult<ProjectStatusReportListDto>>(projectId, { page: 1, pageSize: 50 }),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useProjectStatusReportCurrentProgress(projectId: string, enabled = true) {
  return useQuery({
    queryKey: [...projectKeys.statusReports(projectId), "current-progress"],
    queryFn: () => projectApi.currentStatusReportProgress<ProjectStatusReportCurrentProgressDto>(projectId),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useProjectStatusReport(projectId: string, reportId: string | null, enabled = true) {
  return useQuery({
    queryKey: projectKeys.statusReport(projectId, reportId ?? ""),
    queryFn: () => projectApi.statusReport<ProjectStatusReport>(projectId, reportId!),
    enabled: Boolean(projectId && reportId) && enabled,
    staleTime: 30_000,
  });
}

function invalidateReportQueries(queryClient: ReturnType<typeof useQueryClient>, projectId: string, reportId?: string) {
  queryClient.invalidateQueries({ queryKey: projectKeys.statusReports(projectId) });
  if (reportId) queryClient.invalidateQueries({ queryKey: projectKeys.statusReport(projectId, reportId), exact: true });
}

export function useCreateProjectStatusReport(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (input: ProjectStatusReportInput) => projectApi.createStatusReport<ProjectStatusReport>(projectId, input), onSuccess: () => invalidateReportQueries(queryClient, projectId) });
}

export function useUpdateProjectStatusReport(projectId: string, reportId: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (input: ProjectStatusReportInput & { rowVersion?: number[] }) => projectApi.updateStatusReport<ProjectStatusReport>(projectId, reportId, input), onSuccess: () => invalidateReportQueries(queryClient, projectId, reportId) });
}

export function usePublishProjectStatusReport(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (reportId: string) => projectApi.publishStatusReport<ProjectStatusReport>(projectId, reportId), onSuccess: (_data, reportId) => invalidateReportQueries(queryClient, projectId, reportId) });
}

export function useDeleteProjectStatusReport(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (reportId: string) => projectApi.deleteStatusReport<void>(projectId, reportId), onSuccess: (_data, reportId) => invalidateReportQueries(queryClient, projectId, reportId) });
}
