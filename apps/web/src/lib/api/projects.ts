import { api } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

export const projectKeys = {
  all: ["projects"] as const,
  list: (filters: unknown = {}) => ["projects", "list", filters] as const,
  detail: (id: string) => ["projects", "detail", id] as const,
  shell: (id: string) => ["projects", "detail", id, "shell"] as const,
  overview: (id: string) => ["projects", "detail", id, "overview"] as const,
  team: (id: string) => ["projects", "detail", id, "team"] as const,
  people: (id: string) => ["projects", "detail", id, "people"] as const,
  history: (id: string) => ["projects", "detail", id, "manager-history"] as const,
  snapshots: (id: string) => ["projects", "detail", id, "snapshots"] as const,
  snapshot: (projectId: string, snapshotId: string) => ["projects", "detail", projectId, "snapshots", snapshotId] as const,
  handovers: (id: string) => ["projects", "detail", id, "handover-requests"] as const,
  statusReports: (id: string) => ["projects", "detail", id, "status-reports"] as const,
  statusReport: (projectId: string, reportId: string) => ["projects", "detail", projectId, "status-reports", reportId] as const,
  conflicts: (
    projectId: string,
    resourceId: string,
    startDate: string,
    endDate: string,
    allocationPercentage: number,
    excludeMemberId?: string
  ) => ["projects", "detail", projectId, "team", "conflicts", resourceId, startDate, endDate, allocationPercentage, excludeMemberId] as const,
};

export interface ProjectOverviewDto {
  program?: { id: string; name: string; code?: string };
  statusReports: {
    totalCount: number;
    publishedCount: number;
    latestPublishedReport?: {
      id: string;
      reportDate: string;
      periodFrom: string;
      periodTo: string;
      reportState: "Draft" | "Published";
      projectLifecycleStatusAtReport?: string;
      overallHealth?: string;
      overallProgressPercent?: number;
      executiveSummary?: string;
      publishedAt?: string;
    };
  };
  risks: {
    openCount: number;
    highCriticalOpenCount: number;
    statusCounts: Array<{ status: string; count: number }>;
    levelCounts: Array<{ riskLevel: string; count: number; displayOrder?: number }>;
    topRisks: Array<{ id: string; title: string; riskLevel: string; status: string }>;
  };
  issues: {
    openIssueCount: number;
    overdueOpenIssueCount: number;
    statusCounts: Array<{ status: string; count: number; nameEn?: string; nameAr?: string; displayOrder?: number }>;
    topIssues: Array<{ id: string; title: string; status: string; priority: string; dueDate?: string }>;
  };
  milestones: Array<{ id: string; name: string; dueDate?: string; completedDate?: string; status?: string; progress?: number }>;
  currentBaseline?: { id: string; name: string; version: number; baselineDate?: string; isCurrent: boolean };
  pendingChangeRequestCount: number | null;
  pendingHandoverCount: number | null;
  allocations: { activeAllocationCount: number; activeResourceCount: number; allocatedHoursPerWeek: number | null };
  meetings: { meetingCount: number; finalMinutesCount: number };
  planProgress?: number | null;
  statusDate?: string;
  plannedValue?: number | null;
  earnedValue?: number | null;
  actualCost?: number | null;
  currency?: string | null;
  scheduleVariance?: number | null;
  costVariance?: number | null;
  schedulePerformanceIndex?: number | null;
  costPerformanceIndex?: number | null;
  approvedBudget?: number | null;
  allocatedBudget?: number | null;
  unallocatedBudget?: number | null;
  originalApprovedBudget?: number | null;
  revisedBudget?: number | null;
  committedCost?: number | null;
  forecastAtCompletion?: number | null;
  remainingBudget?: number | null;
  budgetUtilizationPercentage?: number | null;
  forecastVariance?: number | null;
  hasBudgetData?: boolean;
  hasCostEntryData?: boolean;
  hasCommitmentData?: boolean;
  hasCostManagementData?: boolean;
  hasPendingRevision?: boolean;
}

export const projectApi = {
  list: <T>(params?: Record<string, unknown>) => api.get<T>(endpoints.projects.root, params as never),
  detail: <T>(id: string) => api.get<T>(endpoints.projects.detail(id)),
  overview: <T>(id: string) => api.get<T>(endpoints.projects.overview(id)),
  create: <T>(body: unknown) => api.post<T>(endpoints.projects.root, body),
  update: <T>(id: string, body: unknown) => api.put<T>(endpoints.projects.detail(id), body),
  remove: <T>(id: string) => api.delete<T>(endpoints.projects.detail(id)),
  statusReports: <T>(projectId: string, params?: Record<string, unknown>) => api.get<T>(endpoints.projects.statusReports(projectId), params as never),
  currentStatusReportProgress: <T>(projectId: string) => api.get<T>(endpoints.projects.currentStatusReportProgress(projectId)),
  statusReport: <T>(projectId: string, reportId: string) => api.get<T>(endpoints.projects.statusReport(projectId, reportId)),
  createStatusReport: <T>(projectId: string, body: unknown) => api.post<T>(endpoints.projects.statusReports(projectId), body),
  updateStatusReport: <T>(projectId: string, reportId: string, body: unknown) => api.put<T>(endpoints.projects.statusReport(projectId, reportId), body),
  publishStatusReport: <T>(projectId: string, reportId: string) => api.post<T>(endpoints.projects.publishStatusReport(projectId, reportId)),
  deleteStatusReport: <T>(projectId: string, reportId: string) => api.delete<T>(endpoints.projects.statusReport(projectId, reportId)),
};
