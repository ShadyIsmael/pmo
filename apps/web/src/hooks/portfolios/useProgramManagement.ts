"use client";

import { useQuery } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";

export interface ProgramManagementListItem {
  id: string;
  name: string;
  code?: string;
  health?: string;
  status?: string;
  programManagerName?: string;
  portfolioName?: string;
  startDate?: string;
  endDate?: string;
  currency: string;
  projectCount: number;
  projectsRequiringAttention: number;
  scheduleIndicator: string;
  financialIndicator: string;
}

export interface ProgramManagementOverview {
  program: ProgramIdentity;
  summary: ProgramSummary;
  delivery: ProgramDelivery;
  financials: ProgramFinancials;
  risks: ProgramRiskSummary;
  issues: ProgramIssueSummary;
  resources: ProgramResourceSummary;
  statusReports: ProgramStatusReports;
  projects: ProgramProject[];
  projectsPage: number;
  projectsPageSize: number;
  projectsTotalCount: number;
  projectsTotalPages: number;
  milestones: ProgramMilestone[];
  topRisks: ProgramRisk[];
  topIssues: ProgramIssue[];
  attentionItems: ProgramAttention[];
  upcomingMeetings: ProgramMeeting[];
}

export interface ProgramIdentity { id: string; name: string; code?: string; status?: string; health?: string; programManagerName?: string; portfolioId?: string; portfolioName?: string; currency: string; startDate?: string; endDate?: string; budget?: number; }
export interface ProgramSummary { totalProjects: number; activeProjects: number; completedProjects: number; projectsAtRisk: number; criticalProjects: number; averageProjectProgress?: number; projectHealthDistribution: Record<string, number>; }
export interface ProgramDelivery { projectsBehindSchedule: number; projectsWithScheduleData: number; totalMilestones: number; overdueMilestones: number; dueSoonMilestones: number; onTrackMilestones: number; completedMilestones: number; }
export interface ProgramProject { id: string; name: string; code: string; status: string; health: string; projectManagerName?: string; startDate?: string; endDate?: string; progress: number; scheduleCondition: string; forecastEndDate?: string; currency: string; budget?: number; actualCost?: number; requiresAttention: boolean; openRiskCount: number; openIssueCount: number; statusReportState: string; latestStatusReportDate?: string; }
export interface ProgramFinancials { baseCurrency: string; hasMixedCurrencies: boolean; groups: ProgramFinancialGroup[]; }
export interface ProgramFinancialGroup { currency: string; isBaseCurrency: boolean; budget: number; actual: number; committed: number; forecast: number; remaining?: number; projects: ProgramFinancialProject[]; }
export interface ProgramFinancialProject { projectId: string; projectName: string; budget: number; actual: number; committed: number; forecast: number; }
export interface ProgramRiskSummary { openCount: number; overdueCount: number; bySeverity: Record<string, number>; }
export interface ProgramRisk { id: string; projectId: string; projectName: string; title: string; severity: string; status: string; identifiedDate?: string; }
export interface ProgramIssueSummary { openCount: number; overdueCount: number; bySeverity: Record<string, number>; }
export interface ProgramIssue { id: string; projectId: string; projectName: string; title: string; severity: string; priority: string; status: string; targetResolutionDate?: string; }
export interface ProgramResourceSummary { activeAllocationCount: number; activeResourceCount: number; sharedResourceCount: number; overAllocatedResourceCount: number; allocatedHoursPerWeek: number; pressure: ProgramResourcePressure[]; }
export interface ProgramResourcePressure { resourceId: string; resourceName: string; allocationPercentage: number; projectCount: number; projects: ProgramResourceProject[]; }
export interface ProgramResourceProject { projectId: string; projectName: string; allocationPercentage: number; }
export interface ProgramStatusReports { publishedProjectCount: number; currentCount: number; staleCount: number; missingCount: number; freshnessDays: number; latestPublished: ProgramStatusReport[]; }
export interface ProgramStatusReport { projectId: string; projectName: string; reportDate: string; health?: string; progress?: number; freshness: string; }
export interface ProgramMilestone { id: string; projectId: string; projectName: string; name: string; dueDate?: string; completedDate?: string; state: string; status?: string; }
export interface ProgramAttention { level: string; kind: string; message: string; projectId?: string; itemId?: string; }
export interface ProgramMeeting { id: string; projectId?: string; projectName?: string; title: string; startTime: string; endTime: string; }

export function useManagedPrograms(search?: string) {
  return useQuery({
    queryKey: ["program-management", "programs", search ?? ""],
    queryFn: () => api.get<PagedResult<ProgramManagementListItem>>(endpoints.programs.management, { page: 1, pageSize: 50, search: search?.trim() || undefined }),
    staleTime: 30_000,
  });
}

export function useProgramManagementOverview(programId?: string, projectsPage = 1, projectsPageSize = 25, enabled = true) {
  return useQuery({
    queryKey: ["program-management", "overview", programId ?? "", projectsPage, projectsPageSize],
    queryFn: () => api.get<ProgramManagementOverview>(endpoints.programs.managementOverview(programId!), { projectsPage, projectsPageSize }),
    enabled: Boolean(programId) && enabled,
    staleTime: 30_000,
  });
}
