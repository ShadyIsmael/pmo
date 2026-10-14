import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface ProjectDto {
  id: string;
  tenantId: string;
  name: string;
  code: string;
  description?: string;
  programId?: string;
  status: string;
  priority: string;
  projectManagerId?: string;
  projectManagerName?: string;
  sponsorId?: string;
  sponsorName?: string;
  portfolioId?: string;
  portfolioName?: string;
  departmentId?: string;
  departmentName?: string;
  startDate?: string;
  endDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  budget?: number;
  currency: string;
  actualCost?: number;
  plannedValue?: number;
  earnedValue?: number;
  progress: number;
  health: string;
  ragStatus: string;
  confidenceLevel: string;
  phase?: string;
  riskRating?: string;
  businessCase?: string;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
  createdBy: string;
  updatedBy?: string;
  isDeleted: boolean;
  members: ProjectMemberDto[];
}

export interface ProjectSummaryDto {
  id: string;
  name: string;
  code: string;
  status: string;
  health: string;
  ragStatus: string;
  progress: number;
  startDate?: string;
  endDate?: string;
  budget?: number;
  actualCost?: number;
  currency: string;
  projectManagerId?: string;
  projectManagerName?: string;
}

export interface ProjectMemberDto {
  id: string;
  projectId: string;
  resourceId: string;
  resourceName: string;
  resourceJobTitle?: string;
  role: string;
  allocationPercentage: number;
  hoursPerWeek?: number;
  startDate: string;
  endDate: string;
  status: string;
  isBillable: boolean;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface ProjectPersonDto {
  id: string;
  projectId: string;
  resourceId?: string | null;
  name: string;
  email?: string | null;
  jobTitle?: string | null;
  resourceType: string;
  userId?: string | null;
  hasSystemUser: boolean;
  source: string;
  relationshipType: "ProjectManager" | "TeamMember" | "Sponsor" | "Stakeholder" | "Other" | string;
  status: string;
  allocationPercentage?: number | null;
  hoursPerWeek?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  isBillable?: boolean | null;
  role?: string | null;
  interest?: string | null;
  influence?: string | null;
  isLegacy: boolean;
  calendarPolicy?: string;
  functionalRoles?: ProjectRoleRefDto[];
  rowVersion: string;
  createdAt: string;
  updatedAt?: string | null;
}

export interface ProjectRoleRefDto {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  isPrimary: boolean;
}

export interface ProjectTeamMemberAllocationDto {
  allocationId: string;
  resourceId: string;
  resourceName: string;
  resourceJobTitle?: string;
  role: string;
  allocationPercentage: number;
  hoursPerWeek?: number;
  startDate: string;
  endDate: string;
  status: string;
  isBillable: boolean;
  notes?: string;
}

export interface CreateProjectPersonDto {
  resourceId: string;
  relationshipType: string;
  role?: string;
  allocationPercentage?: number;
  hoursPerWeek?: number;
  startDate?: string;
  endDate?: string;
  isBillable?: boolean;
  interest?: string;
  influence?: string;
  projectRoleIds?: string[];
  calendarPolicy?: string;
}

export interface UpdateProjectPersonDto {
  source: string;
  relationshipType?: string;
  role?: string;
  allocationPercentage?: number;
  hoursPerWeek?: number;
  startDate?: string;
  endDate?: string;
  isBillable?: boolean;
  interest?: string;
  influence?: string;
  rowVersion?: string;
  projectRoleIds?: string[];
  calendarPolicy?: string;
}

export interface CreateProjectDto {
  name: string;
  code?: string;
  description?: string;
  programId?: string;
  status?: string;
  priority?: string;
  category?: string;
  projectManagerId?: string;
  sponsorId?: string;
  portfolioId?: string;
  departmentId?: string;
  startDate?: string;
  endDate?: string;
  budget?: number;
  currency?: string;
  businessCase?: string;
  notes?: string;
  confidenceLevel?: string;
}

export interface UpdateProjectDto {
  name?: string;
  code?: string;
  description?: string;
  programId?: string;
  status?: string;
  priority?: string;
  category?: string;
  projectManagerId?: string;
  sponsorId?: string;
  portfolioId?: string;
  departmentId?: string;
  startDate?: string;
  endDate?: string;
  budget?: number;
  currency?: string;
  plannedValue?: number;
  earnedValue?: number;
  actualCost?: number;
  businessCase?: string;
  notes?: string;
  confidenceLevel?: string;
}

export interface ProjectOverviewRisk {
  id: string;
  title: string;
  riskLevel: string;
  status: string;
}

export interface ProjectOverviewIssue {
  id: string;
  title: string;
  status: string;
  priority: string;
  dueDate?: string;
}

export interface ProjectOverviewMilestone {
  id: string;
  name: string;
  dueDate?: string;
  completedDate?: string;
  status?: string;
  progress?: number;
}

export interface ProjectOverviewDto {
  progress?: number | null;
  health?: string | null;
  program?: { id: string; name: string; code?: string };
  risks: { openCount: number; highCriticalOpenCount: number; topRisks: ProjectOverviewRisk[] };
  issues: { openIssueCount: number; overdueOpenIssueCount: number; topIssues: ProjectOverviewIssue[] };
  milestones: ProjectOverviewMilestone[];
  currentBaseline?: { id: string; name: string; version: number; baselineDate?: string; isCurrent: boolean };
  pendingChangeRequestCount: number;
  pendingHandoverCount: number;
  allocations: { activeAllocationCount: number; activeResourceCount: number; allocatedHoursPerWeek: number };
  meetings: { meetingCount: number; finalMinutesCount: number };
  planProgress?: number | null;
  statusDate?: string;
  budgetAtCompletion?: number | null;
  plannedValue?: number | null;
  earnedValue?: number | null;
  actualCost?: number | null;
  currency?: string | null;
  scheduleVariance?: number | null;
  costVariance?: number | null;
  schedulePerformanceIndex?: number | null;
  costPerformanceIndex?: number | null;
  plannedValueUnavailableReason?: string | null;
  earnedValueUnavailableReason?: string | null;
  actualCostUnavailableReason?: string | null;
  schedulePerformanceIndexUnavailableReason?: string | null;
  costPerformanceIndexUnavailableReason?: string | null;
  costVarianceUnavailableReason?: string | null;
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
  statusReports: { totalCount: number; publishedCount: number; latestPublishedReport?: { id: string; reportDate: string; periodFrom: string; periodTo: string; reportState?: "Draft" | "Published" | string; projectLifecycleStatusAtReport?: string; overallHealth?: string; overallProgressPercent?: number; executiveSummary?: string; publishedAt?: string } };
}

export interface PagedResult<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PaginationParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: "asc" | "desc";
  search?: string;
  [key: string]: string | number | boolean | undefined;
}

export const projectKeys = {
  all: ["projects"] as const,
  lists: () => [...projectKeys.all, "list"] as const,
  list: (params: PaginationParams) => [...projectKeys.lists(), params] as const,
  detail: (id: string) => [...projectKeys.all, "detail", id] as const,
  shell: (id: string) => [...projectKeys.all, "shell", id] as const,
  overview: (id: string) => [...projectKeys.all, "overview", id] as const,
  team: (projectId: string) => [...projectKeys.all, "team", projectId] as const,
  people: (projectId: string) => [...projectKeys.all, "people", projectId] as const,
  history: (projectId: string) => [...projectKeys.all, "history", projectId] as const,
  snapshots: (projectId: string) => [...projectKeys.all, "snapshots", projectId] as const,
  snapshot: (projectId: string, snapshotId: string) => [...projectKeys.all, "snapshot", projectId, snapshotId] as const,
  handovers: (projectId: string) => [...projectKeys.all, "handovers", projectId] as const,
  handoverManagers: (projectId: string) => [...projectKeys.all, "handover-managers", projectId] as const,
  changeRequests: (projectId: string) => [...projectKeys.all, "change-requests", projectId] as const,
  statusReports: (projectId: string) => [...projectKeys.all, "status-reports", projectId] as const,
  statusReport: (projectId: string, reportId: string) => [...projectKeys.all, "status-report", projectId, reportId] as const,
  conflicts: (projectId: string, resourceId: string, startDate: string, endDate: string, allocationPercentage: number, excludeMemberId?: string) =>
    [...projectKeys.all, "conflicts", projectId, resourceId, startDate, endDate, allocationPercentage, excludeMemberId] as const,
};

export interface ProjectManagerHistoryDto {
  id: string;
  projectId: string;
  projectManagerId: string;
  projectManagerName: string;
  projectManagerEmail?: string;
  startDate: string;
  endDate?: string;
  isCurrent: boolean;
  handoverFromProjectManagerName?: string;
  handoverDate?: string;
  handoverReason?: string;
  handoverNotes?: string;
  createdAt: string;
}

export interface ProjectSnapshotSummaryDto {
  id: string;
  projectId: string;
  snapshotNumber: number;
  snapshotName: string;
  snapshotType: string;
  snapshotDate: string;
  effectiveEndDate: string;
  previousProjectManagerName?: string;
  newProjectManagerName?: string;
  handoverReason?: string;
  handoverNotes?: string;
  createdAt: string;
}

export interface ProjectSnapshotCollectionItem {
  originalId: string;
  name?: string;
  title?: string;
  description?: string;
  status?: string;
  role?: string;
  resourceName?: string;
  assigneeName?: string;
  ownerName?: string;
  category?: string;
  amount?: number;
  approvedAmount?: number;
  currency?: string;
  startDate?: string;
  endDate?: string;
  dueDate?: string;
  reportedAt?: string;
  [key: string]: string | number | boolean | undefined;
}

export interface ProjectSnapshotDataDto {
  members: ProjectSnapshotCollectionItem[];
  allocations: ProjectSnapshotCollectionItem[];
  phases: ProjectSnapshotCollectionItem[];
  milestones: ProjectSnapshotCollectionItem[];
  deliverables: ProjectSnapshotCollectionItem[];
  stakeholders: ProjectSnapshotCollectionItem[];
  schedules: ProjectSnapshotCollectionItem[];
  wbsItems: ProjectSnapshotCollectionItem[];
  baselines: ProjectSnapshotCollectionItem[];
  statusReports: ProjectSnapshotCollectionItem[];
  workItems: ProjectSnapshotCollectionItem[];
  risks: ProjectSnapshotCollectionItem[];
  issues: ProjectSnapshotCollectionItem[];
  budgets: ProjectSnapshotCollectionItem[];
  costEntries: ProjectSnapshotCollectionItem[];
  meetings: ProjectSnapshotCollectionItem[];
  documents: ProjectSnapshotCollectionItem[];
}

export interface ProjectSnapshotDto extends ProjectSnapshotSummaryDto {
  name: string;
  code: string;
  description?: string;
  programName?: string;
  portfolioName?: string;
  departmentName?: string;
  sponsorName?: string;
  status: string;
  priority: string;
  phase?: string;
  health: string;
  confidenceLevel: string;
  riskRating?: string;
  startDate?: string;
  endDate?: string;
  budget?: number;
  currency: string;
  actualCost?: number;
  progress: number;
  businessCase?: string;
  notes?: string;
  data?: ProjectSnapshotDataDto;
}

export interface ProjectHandoverRequestDto {
  id: string;
  projectId: string;
  projectName: string;
  projectCode: string;
  currentProjectManagerId: string;
  currentProjectManagerName: string;
  newProjectManagerId: string;
  newProjectManagerName: string;
  effectiveHandoverDate: string;
  reason: string;
  notes?: string;
  status: string;
  requestedByName: string;
  requestedAt: string;
  respondedByName?: string;
  respondedAt?: string;
  rejectionReason?: string;
  snapshotId?: string;
  snapshotName?: string;
  createdAt: string;
}

export interface ProjectStatusReportListDto {
  id: string;
  projectId: string;
  reportDate: string;
  periodFrom: string;
  periodTo: string;
  reportState: "Draft" | "Published" | string;
  projectLifecycleStatusAtReport?: string;
  overallHealth?: string;
  overallProgressPercent?: number;
  preparedByNameSnapshot?: string;
  publishedAt?: string;
}

export interface ProjectStatusReportDto extends ProjectStatusReportListDto {
  projectNameSnapshot?: string;
  projectCodeSnapshot?: string;
  projectManagerIdAtReport?: string;
  projectManagerNameSnapshot?: string;
  scheduleHealth?: string;
  costHealth?: string;
  scopeHealth?: string;
  qualityHealth?: string;
  resourceHealth?: string;
  executiveSummary?: string;
  keyAchievements?: string;
  challengesConcerns?: string;
  nextPeriodPriorities?: string;
  sourceDataCapturedAt?: string;
  preparedByUserId?: string;
  publishedByUserId?: string;
  publishedByNameSnapshot?: string;
  preparedByName?: string;
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
  risks: ProjectStatusReportRiskSnapshotDto[];
  issues: ProjectStatusReportIssueSnapshotDto[];
  changeRequests: ProjectStatusReportChangeRequestSnapshotDto[];
  rowVersion?: string;
}

export interface ProjectStatusReportRiskSnapshotDto { sourceRiskId: string; rank: number; title: string; description?: string; impact?: string; likelihood?: string; riskLevel: string; riskStatus: string; potentialCost?: number; potentialDelay?: number; riskResponse?: string; ownerId?: string; ownerNameSnapshot?: string; }
export interface ProjectStatusReportIssueSnapshotDto { sourceIssueId: string; rank: number; title: string; description?: string; severity: string; priority: string; issueStatus: string; assigneeId?: string; assigneeNameSnapshot?: string; resolution?: string; }
export interface ProjectStatusReportChangeRequestSnapshotDto { sourceChangeRequestId: string; rank: number; number: number; changeType: string; reason: string; justification?: string; impact?: string; changeRequestStatus: string; requestedBy: string; requestedByNameSnapshot?: string; requestedAt: string; reviewedAt?: string; decisionNotes?: string; }

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
  rowVersion?: string;
}

export interface ProjectChangeRequestDto {
  id: string;
  projectId: string;
  projectName: string;
  number: number;
  type: "Administrative" | "Baseline";
  reason: string;
  justification?: string | null;
  impact?: string | null;
  budgetBeforeChange?: number | null;
  budgetChangeAmount?: number | null;
  proposedProjectBudget?: number | null;
  attachments?: string | null;
  status: "Pending" | "Approved" | "Rejected" | "Cancelled";
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
  reviewedBy?: string | null;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  decisionNotes?: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface CreateProjectChangeRequestDto {
  projectId: string;
  type: "Administrative" | "Baseline";
  reason: string;
  justification?: string | null;
  impact?: string | null;
  budgetChangeAmount?: number | null;
  attachments?: string | null;
}

function invalidateProjectPeople(queryClient: ReturnType<typeof useQueryClient>, projectId: string) {
  queryClient.invalidateQueries({ queryKey: projectKeys.people(projectId) });
  queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId), exact: true });
  queryClient.invalidateQueries({ queryKey: projectKeys.team(projectId) });
}

export function useProjects(params: PaginationParams & { status?: string; priority?: string; portfolioId?: string } = {}, enabled = true) {
  return useQuery({
    queryKey: projectKeys.list(params),
    queryFn: () => api.get<PagedResult<ProjectSummaryDto>>("/api/projects", params),
    staleTime: 30_000,
    enabled,
  });
}

export function useProject(id: string) {
  return useQuery({
    queryKey: projectKeys.detail(id),
    queryFn: () => api.get<ProjectDto>(`/api/projects/${id}`),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useProjectShell(id: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.shell(id),
    queryFn: () => api.get<ProjectDto>(`/api/projects/${id}/shell`),
    enabled: !!id && enabled,
    staleTime: 30_000,
  });
}

export function useProjectOverview(id: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.overview(id),
    queryFn: () => api.get<ProjectOverviewDto>(`/api/projects/${id}/overview`),
    enabled: !!id && enabled,
    staleTime: 30_000,
  });
}

export function useProjectTeam(projectId: string) {
  return useQuery({
    queryKey: projectKeys.team(projectId),
    queryFn: () => api.get<ProjectMemberDto[]>(`/api/projects/${projectId}/team`),
    enabled: !!projectId,
    staleTime: 30_000,
  });
}

export function useProjectPeople(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.people(projectId),
    queryFn: () => api.get<ProjectPersonDto[]>(`/api/projects/${projectId}/people`),
    enabled: !!projectId && enabled,
    staleTime: 30_000,
  });
}

export function useCheckResourceConflicts(
  projectId: string,
  resourceId: string,
  startDate: string,
  endDate: string,
  allocationPercentage: number,
  excludeMemberId?: string,
  enabled = true,
) {
  return useQuery({
    queryKey: projectKeys.conflicts(projectId, resourceId, startDate, endDate, allocationPercentage, excludeMemberId),
    queryFn: () => api.get<ProjectTeamMemberAllocationDto[]>(`/api/projects/${projectId}/team/conflicts`, {
      resourceId,
      startDate,
      endDate,
      allocationPercentage,
      excludeMemberId,
    }),
    enabled: enabled && Boolean(projectId && resourceId && startDate && endDate && allocationPercentage > 0),
    staleTime: 10_000,
  });
}

export function useProjectManagerHistory(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.history(projectId),
    queryFn: () => api.get<ProjectManagerHistoryDto[]>(`/api/projects/${projectId}/manager-history`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useProjectSnapshots(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.snapshots(projectId),
    queryFn: () => api.get<ProjectSnapshotSummaryDto[]>(`/api/projects/${projectId}/snapshots`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useProjectSnapshot(projectId: string, snapshotId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.snapshot(projectId, snapshotId),
    queryFn: () => api.get<ProjectSnapshotDto>(`/api/projects/${projectId}/snapshots/${encodeURIComponent(snapshotId)}`),
    enabled: Boolean(projectId && snapshotId) && enabled,
    staleTime: 60_000,
  });
}

export interface ProjectHandoverManagerDto {
  id: string;
  fullName: string;
  email: string;
  jobTitle?: string;
  managerTypes: Array<"ProjectManager" | "PortfolioManager" | "ProgramManager">;
}

export function useProjectHandoverManagers(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.handoverManagers(projectId),
    queryFn: () => api.get<ProjectHandoverManagerDto[]>(endpoints.projects.handoverManagers(projectId)),
    enabled: Boolean(projectId) && enabled,
    staleTime: 60_000,
  });
}

export function useProjectHandoverRequests(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.handovers(projectId),
    queryFn: () => api.get<ProjectHandoverRequestDto[]>(`/api/projects/${projectId}/handover-requests`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 15_000,
  });
}

export function useCreateProjectHandoverRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { newProjectManagerId: string; effectiveHandoverDate: string; reason: string; notes?: string }) =>
      api.post<ProjectHandoverRequestDto>(`/api/projects/${projectId}/handover-requests`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.handovers(projectId) });
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(projectId) });
    },
  });
}

export function useProjectStatusReports(projectId: string, enabled = true) {
  return useQuery({
    queryKey: [...projectKeys.statusReports(projectId)],
    queryFn: () => api.get<PagedResult<ProjectStatusReportListDto>>(`/api/projects/${projectId}/status-reports`, { page: 1, pageSize: 50 }),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export interface ProjectStatusReportCurrentProgressDto { projectPlanProgressPercent?: number; }

export function useProjectStatusReportCurrentProgress(projectId: string, enabled = true) {
  return useQuery({
    queryKey: [...projectKeys.statusReports(projectId), "current-progress"],
    queryFn: () => api.get<ProjectStatusReportCurrentProgressDto>(`/api/projects/${projectId}/status-reports/current-progress`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useCreateProjectStatusReport(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ProjectStatusReportInput) => api.post<ProjectStatusReportListDto>(`/api/projects/${projectId}/status-reports`, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.statusReports(projectId) }),
  });
}

export function useProjectStatusReport(projectId: string, reportId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.statusReport(projectId, reportId),
    queryFn: () => api.get<ProjectStatusReportDto>(`/api/projects/${projectId}/status-reports/${encodeURIComponent(reportId)}`),
    enabled: Boolean(projectId && reportId) && enabled,
    staleTime: 30_000,
  });
}

export function useUpdateProjectStatusReport(projectId: string, reportId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ProjectStatusReportInput) => api.put<ProjectStatusReportDto>(`/api/projects/${projectId}/status-reports/${encodeURIComponent(reportId)}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.statusReports(projectId) });
      queryClient.invalidateQueries({ queryKey: projectKeys.statusReport(projectId, reportId) });
    },
  });
}

export function usePublishProjectStatusReport(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (reportId: string) => api.post<ProjectStatusReportListDto>(`/api/projects/${projectId}/status-reports/${encodeURIComponent(reportId)}/publish`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.statusReports(projectId) }),
  });
}

export function useDeleteProjectStatusReport(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (reportId: string) => api.delete<void>(`/api/projects/${projectId}/status-reports/${encodeURIComponent(reportId)}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.statusReports(projectId) }),
  });
}

export function useProjectChangeRequests(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.changeRequests(projectId),
    queryFn: () => api.get<ProjectChangeRequestDto[]>(`/api/projects/${projectId}/change-requests`),
    enabled: Boolean(projectId) && enabled,
    staleTime: 30_000,
  });
}

export function useCreateProjectChangeRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateProjectChangeRequestDto) => api.post<ProjectChangeRequestDto>(`/api/projects/${projectId}/change-requests`, data),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: projectKeys.changeRequests(projectId) }),
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId), exact: true }),
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(projectId), exact: true }),
      queryClient.invalidateQueries({ queryKey: ["projectChangeRequests"] }),
    ]),
  });
}

export function useCancelProjectChangeRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (requestId: string) => api.post<ProjectChangeRequestDto>(`/api/project-change-requests/${encodeURIComponent(requestId)}/cancel`, {}),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: projectKeys.changeRequests(projectId) }),
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId), exact: true }),
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(projectId), exact: true }),
      queryClient.invalidateQueries({ queryKey: ["projectChangeRequests"] }),
    ]),
  });
}

export function useAddProjectPerson(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateProjectPersonDto) => api.post<ProjectPersonDto>(`/api/projects/${projectId}/people`, data),
    onSuccess: () => invalidateProjectPeople(queryClient, projectId),
  });
}

export function useUpdateProjectPerson(projectId: string, personId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateProjectPersonDto) => api.put<ProjectPersonDto>(`/api/projects/${projectId}/people/${encodeURIComponent(personId)}`, data),
    onSuccess: () => invalidateProjectPeople(queryClient, projectId),
  });
}

export function useRemoveProjectPerson(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ personId, source, rowVersion }: { personId: string; source: string; rowVersion?: string }) =>
      api.post<ProjectPersonDto>(`/api/projects/${projectId}/people/${encodeURIComponent(personId)}/remove`, { source, rowVersion }),
    onSuccess: () => invalidateProjectPeople(queryClient, projectId),
  });
}

export function useCreateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: CreateProjectDto) => api.post<ProjectDto>("/api/projects", data),
    retry: false,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });
}

export function useUpdateProject(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: UpdateProjectDto) => api.put<ProjectDto>(`/api/projects/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.shell(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
      queryClient.invalidateQueries({ queryKey: projectKeys.people(id) });
      queryClient.invalidateQueries({ queryKey: projectKeys.team(id) });
    },
  });
}

export function useDeleteProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.delete<void>(`/api/projects/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });
}

export function useArchiveProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.post<void>(`/api/projects/${id}/archive`, {}),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.shell(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });
}

export function useReactivateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.post<void>(`/api/projects/${id}/reactivate`, {}),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.shell(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.overview(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });
}
