"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult, type PaginationParams } from "@/lib/api";
import { endpoints } from "@/lib/api/endpoints";
import { projectApi, projectKeys, type ProjectOverviewDto } from "@/lib/api/projects";
import { handoverKeys } from "@/lib/api/handover";
import { allocationKeys } from "@/lib/api/resources";
import { useAuth } from "@/lib/auth";

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

export interface CreateProjectMemberDto {
  projectId: string;
  resourceId: string;
  role: string;
  allocationPercentage: number;
  hoursPerWeek?: number;
  startDate: string;
  endDate: string;
  status?: string;
  isBillable?: boolean;
  notes?: string;
}

export interface UpdateProjectMemberDto {
  role?: string;
  allocationPercentage?: number;
  hoursPerWeek?: number;
  startDate?: string;
  endDate?: string;
  status?: string;
  isBillable?: boolean;
  notes?: string;
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

export interface ProjectManagerHistoryDto {
  id: string;
  projectId: string;
  projectManagerId: string;
  projectManagerName: string;
  projectManagerEmail?: string;
  startDate: string;
  endDate?: string;
  isCurrent: boolean;
  handoverFromProjectManagerId?: string;
  handoverFromProjectManagerName?: string;
  handoverDate?: string;
  handoverReason?: string;
  handoverNotes?: string;
  createdAt: string;
  createdBy: string;
}

export interface HandoverProjectRequestDto {
  newProjectManagerId: string;
  effectiveHandoverDate: string;
  reason: string;
  notes?: string;
}

export interface ProjectSnapshotSummaryDto {
  id: string;
  projectId: string;
  snapshotNumber: number;
  snapshotName: string;
  snapshotType: string;
  snapshotDate: string;
  effectiveEndDate: string;
  previousProjectManagerId?: string;
  previousProjectManagerName?: string;
  newProjectManagerId?: string;
  newProjectManagerName?: string;
  handoverReason?: string;
  handoverNotes?: string;
  createdAt: string;
  createdBy: string;
}

export interface ProjectSnapshotDataDto {
  members: SnapshotMemberDto[];
  allocations: SnapshotAllocationDto[];
  phases: SnapshotPhaseDto[];
  milestones: SnapshotMilestoneDto[];
  deliverables: SnapshotDeliverableDto[];
  stakeholders: SnapshotStakeholderDto[];
  schedules: SnapshotScheduleDto[];
  wbsItems: SnapshotWbsDto[];
  baselines: SnapshotBaselineDto[];
  statusReports: SnapshotStatusReportDto[];
  workItems: SnapshotWorkItemDto[];
  risks: SnapshotRiskDto[];
  issues: SnapshotIssueDto[];
  budgets: SnapshotBudgetDto[];
  costEntries: SnapshotCostEntryDto[];
  meetings: SnapshotMeetingDto[];
  documents: SnapshotDocumentDto[];
}

export interface SnapshotMemberDto {
  originalId: string;
  resourceId: string;
  resourceName?: string;
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

export interface SnapshotAllocationDto {
  originalId: string;
  resourceId: string;
  resourceName?: string;
  role: string;
  allocationPercentage: number;
  hoursPerWeek?: number;
  startDate: string;
  endDate: string;
  status: string;
  isBillable: boolean;
  notes?: string;
}

export interface SnapshotPhaseDto {
  originalId: string;
  name: string;
  description?: string;
  sortOrder: number;
  status?: string;
  startDate?: string;
  endDate?: string;
  progress?: number;
}

export interface SnapshotMilestoneDto {
  originalId: string;
  name: string;
  description?: string;
  dueDate?: string;
  completedDate?: string;
  status?: string;
  progress?: number;
  category?: string;
  assigneeId?: string;
  assigneeName?: string;
}

export interface SnapshotDeliverableDto {
  originalId: string;
  name: string;
  description?: string;
  status?: string;
  dueDate?: string;
  deliveredDate?: string;
  ownerId?: string;
  ownerName?: string;
  qualityCriteria?: string;
}

export interface SnapshotStakeholderDto {
  originalId: string;
  userId?: string;
  name: string;
  email?: string;
  role: string;
  interest?: string;
  influence?: string;
}

export interface SnapshotScheduleDto {
  originalId: string;
  name: string;
  description?: string;
  baselineStartDate?: string;
  baselineEndDate?: string;
  forecastStartDate?: string;
  forecastEndDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  totalDays?: number;
  remainingDays?: number;
}

export interface SnapshotWbsDto {
  originalId: string;
  parentId?: string;
  name: string;
  code?: string;
  description?: string;
  deliverable?: string;
  plannedHours?: number;
  actualHours?: number;
  progress?: number;
  sortOrder: number;
  status?: string;
}

export interface SnapshotBaselineDto {
  originalId: string;
  name: string;
  description?: string;
  version: number;
  baselineDate?: string;
  isCurrent: boolean;
}

export interface SnapshotStatusReportDto {
  originalId: string;
  status?: string;
  summary?: string;
  accomplishments?: string;
  nextSteps?: string;
  blockers?: string;
  scheduleVariance?: number;
  costVariance?: number;
  periodStart: string;
  periodEnd: string;
  reportedAt: string;
  reportedBy?: string;
  reportedByName?: string;
}

export interface SnapshotWorkItemDto {
  originalId: string;
  title: string;
  description?: string;
  type: string;
  priority: string;
  status: string;
  assigneeId?: string;
  assigneeName?: string;
  parentId?: string;
  storyPoints?: number;
  estimatedHours?: number;
  actualHours?: number;
  startDate?: string;
  dueDate?: string;
  completedAt?: string;
  sortOrder: number;
}

export interface SnapshotRiskDto {
  originalId: string;
  title: string;
  description?: string;
  impact: string;
  likelihood: string;
  riskLevel: string;
  status: string;
  potentialCost?: number;
  potentialDelay?: number;
  riskResponse?: string;
  ownerId?: string;
  ownerName?: string;
  identifiedDate?: string;
  targetResolutionDate?: string;
  closedDate?: string;
}

export interface SnapshotIssueDto {
  originalId: string;
  title: string;
  description?: string;
  severity: string;
  priority: string;
  status: string;
  assigneeId?: string;
  assigneeName?: string;
  identifiedDate?: string;
  targetResolutionDate?: string;
  resolvedDate?: string;
  resolution?: string;
}

export interface SnapshotBudgetDto {
  originalId: string;
  name: string;
  description?: string;
  category: string;
  plannedAmount: number;
  approvedAmount: number;
  actualAmount: number;
  committedAmount: number;
  forecastAmount: number;
  currency: string;
  fiscalYear: number;
  fiscalQuarter?: number;
  status: string;
  startDate?: string;
  endDate?: string;
}

export interface SnapshotCostEntryDto {
  originalId: string;
  budgetId?: string;
  resourceId?: string;
  category: string;
  subCategory?: string;
  description?: string;
  amount: number;
  currency: string;
  taxAmount?: number;
  incurredDate: string;
  type: string;
  reference?: string;
  vendorName?: string;
  status: string;
}

export interface SnapshotMeetingDto {
  originalId: string;
  title: string;
  description?: string;
  meetingType?: string;
  location?: string;
  startTime: string;
  endTime: string;
  status?: string;
  notes?: string;
  organizerId?: string;
  organizerName?: string;
  recurrence?: string;
}

export interface SnapshotDocumentDto {
  originalId: string;
  name: string;
  description?: string;
  fileName?: string;
  fileType?: string;
  fileSize?: number;
  tags?: string;
  status: string;
  version: number;
  ownerId?: string;
  ownerName?: string;
}

export interface ProjectSnapshotDto extends ProjectSnapshotSummaryDto {
  name: string;
  code: string;
  description?: string;
  programId?: string;
  programName?: string;
  status: string;
  priority: string;
  category?: string;
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
  data?: ProjectSnapshotDataDto;
}

type ProjectPage = PagedResult<ProjectSummaryDto>;

export function useProjects(params: PaginationParams & { status?: string; priority?: string; portfolioId?: string } = {}, enabled = true) {
  return useQuery({
    queryKey: projectKeys.list(params),
    queryFn: () => api.get<ProjectPage>(endpoints.projects.root, params),
    staleTime: 30_000,
    enabled,
  });
}

export function useProject(id: string) {
  return useQuery({
    queryKey: projectKeys.detail(id),
    queryFn: () => api.get<ProjectDto>(endpoints.projects.detail(id)),
    enabled: !!id,
    staleTime: 30_000,
  });
}

export function useProjectOverview(id: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.overview(id),
    queryFn: () => projectApi.overview<ProjectOverviewDto>(id),
    enabled: !!id && enabled,
    staleTime: 30_000,
  });
}

export function useProjectTeam(projectId: string) {
  return useQuery({
    queryKey: projectKeys.team(projectId),
    queryFn: () => api.get<ProjectMemberDto[]>(endpoints.projects.team(projectId)),
    enabled: !!projectId,
    staleTime: 30_000,
  });
}

export function useProjectPeople(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.people(projectId),
    queryFn: () => api.get<ProjectPersonDto[]>(endpoints.projects.people(projectId)),
    enabled: !!projectId && enabled,
    staleTime: 30_000,
  });
}

function invalidateProjectPeople(queryClient: ReturnType<typeof useQueryClient>, projectId: string) {
  queryClient.invalidateQueries({ queryKey: projectKeys.people(projectId) });
  queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId), exact: true });
  queryClient.invalidateQueries({ queryKey: projectKeys.team(projectId) });
  queryClient.invalidateQueries({ queryKey: allocationKeys.all });
}

export function useAddProjectPerson(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateProjectPersonDto) => api.post<ProjectPersonDto>(endpoints.projects.people(projectId), data),
    onSuccess: () => invalidateProjectPeople(queryClient, projectId),
  });
}

export function useUpdateProjectPerson(projectId: string, personId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateProjectPersonDto) => api.put<ProjectPersonDto>(`${endpoints.projects.people(projectId)}/${encodeURIComponent(personId)}`, data),
    onSuccess: () => invalidateProjectPeople(queryClient, projectId),
  });
}

export function useRemoveProjectPerson(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ personId, source, rowVersion }: { personId: string; source: string; rowVersion?: string }) => api.post<ProjectPersonDto>(`${endpoints.projects.people(projectId)}/${encodeURIComponent(personId)}/remove`, { source, rowVersion }),
    onSuccess: () => invalidateProjectPeople(queryClient, projectId),
  });
}

export function useCreateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: CreateProjectDto) => api.post<ProjectDto>(endpoints.projects.root, data),
    retry: false,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.list() });
    },
  });
}

export function useUpdateProject(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: UpdateProjectDto) => api.put<ProjectDto>(endpoints.projects.detail(id), data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.list() });
      queryClient.invalidateQueries({ queryKey: projectKeys.people(id) });
      queryClient.invalidateQueries({ queryKey: projectKeys.team(id) });
    },
  });
}

export function useDeleteProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.delete<void>(endpoints.projects.detail(id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.list() });
    },
  });
}

export function useArchiveProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.post<void>(endpoints.projects.archive(id), {}),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.list() });
    },
  });
}

export function useReactivateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => api.post<void>(endpoints.projects.reactivate(id), {}),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(id), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.list() });
    },
  });
}

export function useAddTeamMember(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: CreateProjectMemberDto) => api.post<ProjectMemberDto>(endpoints.projects.team(projectId), data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.team(projectId) });
      queryClient.invalidateQueries({ queryKey: allocationKeys.all });
    },
  });
}

export function useUpdateTeamMember(projectId: string, memberId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: UpdateProjectMemberDto) => api.put<ProjectMemberDto>(endpoints.projects.member(projectId, memberId), data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.team(projectId) });
      queryClient.invalidateQueries({ queryKey: allocationKeys.all });
    },
  });
}

export function useRemoveTeamMember(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (memberId: string) => api.delete<void>(endpoints.projects.member(projectId, memberId)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.team(projectId) });
      queryClient.invalidateQueries({ queryKey: allocationKeys.all });
    },
  });
}

export function useProjectManagerHistory(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.history(projectId),
    queryFn: () => api.get<ProjectManagerHistoryDto[]>(endpoints.projects.history(projectId)),
    enabled: !!projectId && enabled,
    staleTime: 30_000,
  });
}

export function useProjectSnapshots(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.snapshots(projectId),
    queryFn: () => api.get<ProjectSnapshotSummaryDto[]>(endpoints.projects.snapshots(projectId)),
    enabled: !!projectId && enabled,
    staleTime: 30_000,
  });
}

export function useProjectSnapshot(projectId: string, snapshotId: string) {
  return useQuery({
    queryKey: projectKeys.snapshot(projectId, snapshotId),
    queryFn: () => api.get<ProjectSnapshotDto>(endpoints.projects.snapshot(projectId, snapshotId)),
    enabled: !!projectId && !!snapshotId,
    staleTime: 30_000,
  });
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
  requestedBy: string;
  requestedByName: string;
  requestedAt: string;
  respondedBy?: string;
  respondedByName?: string;
  respondedAt?: string;
  rejectionReason?: string;
  snapshotId?: string;
  snapshotName?: string;
  createdAt: string;
  createdBy: string;
}

export function useProjectHandoverRequests(projectId: string, enabled = true) {
  return useQuery({
    queryKey: projectKeys.handovers(projectId),
    queryFn: () => api.get<ProjectHandoverRequestDto[]>(endpoints.projects.handovers(projectId)),
    enabled: !!projectId && enabled,
    staleTime: 15_000,
  });
}

export function usePendingHandoverRequestsForMe() {
  const { user } = useAuth();
  return useQuery({
    queryKey: handoverKeys.pending(user?.id ?? ""),
    queryFn: () => api.get<ProjectHandoverRequestDto[]>(endpoints.handover.pending),
    enabled: !!user?.id,
    staleTime: 15_000,
  });
}

export function useCreateProjectHandoverRequest(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: HandoverProjectRequestDto) =>
      api.post<ProjectHandoverRequestDto>(endpoints.projects.handovers(projectId), data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.handovers(projectId) });
      queryClient.invalidateQueries({ queryKey: handoverKeys.pendingForMe });
    },
  });
}

export function useAcceptHandoverRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (requestId: string) => api.post<ProjectHandoverRequestDto>(endpoints.handover.accept(requestId), {}),
    onSuccess: (request) => {
      queryClient.invalidateQueries({ queryKey: handoverKeys.pendingForMe });
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(request.projectId), exact: true });
      queryClient.invalidateQueries({ queryKey: projectKeys.list() });
      queryClient.invalidateQueries({ queryKey: projectKeys.people(request.projectId) });
      queryClient.invalidateQueries({ queryKey: projectKeys.team(request.projectId) });
      queryClient.invalidateQueries({ queryKey: projectKeys.history(request.projectId) });
      queryClient.invalidateQueries({ queryKey: projectKeys.snapshots(request.projectId) });
      queryClient.invalidateQueries({ queryKey: projectKeys.handovers(request.projectId) });
    },
  });
}

export function useRejectHandoverRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ requestId, rejectionReason }: { requestId: string; rejectionReason: string }) =>
      api.post<ProjectHandoverRequestDto>(endpoints.handover.reject(requestId), { rejectionReason }),
    onSuccess: (request) => {
      queryClient.invalidateQueries({ queryKey: handoverKeys.pendingForMe });
      queryClient.invalidateQueries({ queryKey: projectKeys.handovers(request.projectId) });
    },
  });
}

export function useCancelHandoverRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (requestId: string) => api.post<ProjectHandoverRequestDto>(endpoints.handover.cancel(requestId), {}),
    onSuccess: (request) => {
      queryClient.invalidateQueries({ queryKey: handoverKeys.pendingForMe });
      queryClient.invalidateQueries({ queryKey: projectKeys.handovers(request.projectId) });
    },
  });
}

export function useCheckResourceConflicts(
  projectId: string,
  resourceId: string,
  startDate: string,
  endDate: string,
  allocationPercentage: number,
  excludeMemberId?: string,
  enabled = true
) {
  return useQuery({
    queryKey: projectKeys.conflicts(projectId, resourceId, startDate, endDate, allocationPercentage, excludeMemberId),
    queryFn: () => api.get<ProjectTeamMemberAllocationDto[]>(endpoints.projects.conflicts(projectId), {
      resourceId,
      startDate,
      endDate,
      allocationPercentage,
      excludeMemberId,
    }),
    enabled: enabled && !!resourceId && !!startDate && !!endDate,
    staleTime: 10_000,
  });
}
