import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface MeetingDto { id: string; title: string; startTime: string; endTime: string; status?: string; projectName?: string; }
export interface WorkItemDto { id: string; title: string; type: string; status: string; priority?: string; projectName?: string; dueDate?: string; }
export interface DocumentDto {
  id: string;
  name: string;
  description?: string;
  fileName?: string;
  fileType?: string;
  fileSize?: number;
  contentType?: string;
  projectId?: string;
  categoryId?: string;
  categoryName?: string;
  status: string;
  version?: number;
  ownerId?: string;
  ownerName?: string;
  isTemplate?: boolean;
  isArchived?: boolean;
  sourceTemplateId?: string;
  updatedAt?: string;
}
export interface PortfolioDto { id: string; name: string; code?: string; description?: string; status?: string; ownerId?: string; ownerName?: string; currency?: string; createdAt?: string; updatedAt?: string; }
export type SavePortfolioDto = { name: string; description?: string; ownerId: string; status: "Active" | "Archived"; currency?: string };
export interface ProgramDto {
  id: string; name: string; code?: string; description?: string; status?: string;
  portfolioId?: string; portfolioName?: string; programManagerId?: string;
  programManagerName?: string; startDate?: string; endDate?: string;
  currency?: string; budget?: number; health?: string; rowVersion?: number[];
}
export type SaveProgramDto = {
  name: string; description?: string; portfolioId: string; programManagerId?: string;
  status: string; startDate?: string; endDate?: string; currency?: string;
  budget?: number; health?: string; rowVersion?: number[];
};
export interface PagedResult<T> { items: T[]; totalCount: number; page?: number; pageSize?: number; totalPages?: number; }

export interface PortfolioManagementListItem extends PortfolioDto {
  ownerName?: string; health?: string; currency?: string;
  programCount: number; directProjectCount: number; totalProjectCount: number;
  projectsRequiringAttention: number; programsRequiringAttention: number;
  financialIndicator: string; riskIndicator: string; reportingIndicator: string;
}
export interface PortfolioManagementOverview {
  portfolio: PortfolioDto & { description?: string; health?: string; ownerName?: string; currency?: string; strategicObjective?: string; priority?: string; confidenceLevel?: string; startDate?: string; endDate?: string };
  summary: { totalPrograms: number; totalProjects: number; programContainedProjects: number; directPortfolioProjects: number; activeProjects: number; completedProjects: number; atRiskProjects: number; criticalProjects: number; averageProjectProgress?: number; programHealthDistribution: Record<string, number>; projectHealthDistribution: Record<string, number> };
  delivery: { programDeliveryConditions: Record<string, number>; projectScheduleConditions: Record<string, number>; delayedProjects: number; delayedPrograms: number; projectsWithScheduleData: number; totalMilestones: number; overdueMilestones: number; dueSoonMilestones: number; onTrackMilestones: number; completedMilestones: number; projectProgressDistribution: Record<string, number> };
  financials: { baseCurrency: string; hasMixedCurrencies: boolean; groups: { currency: string; isBaseCurrency: boolean; projectCount: number; budget: number; allocated: number; actual: number; committed: number; forecast: number; remaining: number; variance: number; contributions: { projectId: string; projectName: string; programId?: string; programName?: string; isDirectProject: boolean; budget: number; allocated: number; actual: number; committed: number; forecast: number }[] }[] };
  risks: { sourceScope: string; openCount: number; criticalCount: number; highCount: number; overdueCount: number; byProgram: Record<string, number>; byProject: Record<string, number> };
  issues: { openCount: number; criticalCount: number; overdueCount: number; byProgram: Record<string, number>; byProject: Record<string, number> };
  resources: { activeAllocationCount: number; activeResourceCount: number; sharedResourceCount: number; crossProgramResourceCount: number; overAllocatedResourceCount: number; allocatedHoursPerWeek: number; pressure: { resourceId: string; resourceName: string; allocationPercentage: number; programCount: number; projectCount: number; projects: { projectId: string; projectName: string; programId?: string; programName?: string; isDirectProject: boolean; allocationPercentage: number }[] }[] };
  statusReports: { publishedProjectCount: number; currentCount: number; staleCount: number; missingCount: number; freshnessDays: number; programsWithReportingGaps: string[]; projectsWithReportingGaps: string[]; latestPublished: { projectId: string; projectName: string; programId?: string; programName?: string; reportDate: string; health?: string; progress?: number; freshness: string }[] };
  programs: { id: string; name: string; code?: string; programManagerName?: string; health?: string; status?: string; startDate?: string; endDate?: string; currency: string; projectCount: number; projectsAtRisk: number; deliveryIndicator: string; financialIndicator: string; riskIndicator: string; reportingIndicator: string }[];
  projects: { id: string; name: string; code: string; programId?: string; programName?: string; isDirectProject: boolean; projectManagerName?: string; status: string; health: string; startDate?: string; endDate?: string; progress: number; scheduleCondition: string; currency: string; financialCondition: string; riskCondition: string; statusReportFreshness: string; latestStatusReportDate?: string; requiresAttention: boolean }[];
  milestones: { id: string; projectId: string; projectName: string; programId?: string; programName?: string; name: string; dueDate?: string; completedDate?: string; state: string; status?: string }[];
  topRisks: { id: string; projectId: string; projectName: string; programId?: string; programName?: string; title: string; severity: string; status: string; identifiedDate?: string }[];
  topIssues: { id: string; projectId: string; projectName: string; programId?: string; programName?: string; title: string; severity: string; priority: string; status: string; targetResolutionDate?: string }[];
  attentionItems: { level: string; kind: string; entityType: string; entityId: string; entityName: string; programId?: string; projectId?: string; reason: string }[];
  upcomingMeetings: { id: string; title: string; startTime: string; endTime: string; projectId?: string; projectName?: string }[];
  projectsPage: number; projectsTotalPages: number; programsPage: number; programsTotalPages: number;
}
export interface ProgramManagementListItem extends ProgramDto {
  health?: string; programManagerName?: string; portfolioName?: string; startDate?: string; endDate?: string; currency?: string;
  projectCount: number; projectsRequiringAttention: number; scheduleIndicator: string; financialIndicator: string;
}
export interface ProgramManagementOverview {
  program: ProgramDto & { health?: string; programManagerName?: string; portfolioName?: string; currency?: string; startDate?: string; endDate?: string; budget?: number };
  summary: { totalProjects: number; activeProjects: number; completedProjects: number; projectsAtRisk: number; criticalProjects: number; averageProjectProgress?: number; projectHealthDistribution: Record<string, number> };
  delivery: { projectsBehindSchedule: number; projectsWithScheduleData: number; totalMilestones: number; overdueMilestones: number; dueSoonMilestones: number; onTrackMilestones: number; completedMilestones: number };
  financials: { baseCurrency: string; hasMixedCurrencies: boolean; groups: { currency: string; isBaseCurrency: boolean; budget: number; actual: number; committed: number; forecast: number; remaining?: number; projects: { projectId: string; projectName: string; budget: number; actual: number; committed: number; forecast: number }[] }[] };
  risks: { openCount: number; overdueCount: number; bySeverity: Record<string, number> };
  issues: { openCount: number; overdueCount: number; bySeverity: Record<string, number> };
  resources: { activeAllocationCount: number; activeResourceCount: number; sharedResourceCount: number; overAllocatedResourceCount: number; allocatedHoursPerWeek: number; pressure: { resourceId: string; resourceName: string; allocationPercentage: number; projectCount: number; projects: { projectId: string; projectName: string }[] }[] };
  statusReports: { publishedProjectCount: number; currentCount: number; staleCount: number; missingCount: number; freshnessDays: number; latestPublished: { projectId: string; projectName: string; reportDate: string; health?: string; progress?: number; freshness: string }[] };
  projects: { id: string; name: string; code: string; status: string; health: string; projectManagerName?: string; startDate?: string; endDate?: string; progress: number; scheduleCondition: string; forecastEndDate?: string; currency: string; budget?: number; actualCost?: number; requiresAttention: boolean; openRiskCount: number; openIssueCount: number; statusReportState: string; latestStatusReportDate?: string }[];
  milestones: { id: string; projectId: string; projectName: string; name: string; dueDate?: string; completedDate?: string; state: string; status?: string }[];
  topRisks: { id: string; projectId: string; projectName: string; title: string; severity: string; status: string; identifiedDate?: string }[];
  topIssues: { id: string; projectId: string; projectName: string; title: string; severity: string; priority: string; status: string; targetResolutionDate?: string }[];
  attentionItems: { level: string; kind: string; message: string; projectId?: string; itemId?: string }[];
  upcomingMeetings: { id: string; title: string; startTime: string; endTime: string; projectId?: string; projectName?: string }[];
  projectsPage: number; projectsTotalPages: number;
}

export function useMeetings(params: Record<string, string | undefined> = {}, enabled = true) {
  return useQuery({ queryKey: ["meetings", params], queryFn: () => api.get<{ items: MeetingDto[]; totalCount: number }>(endpoints.meetings.root, { pageSize: 50, ...params }), enabled, staleTime: 30_000 });
}
export function useMeeting(id: string, enabled = true) {
  return useQuery({ queryKey: ["meetings", "detail", id], queryFn: () => api.get<MeetingDto>(endpoints.meetings.detail(id)), enabled: Boolean(id) && enabled, staleTime: 30_000 });
}
export function useWorkItemsList(params: Record<string, string | number | undefined> = {}, enabled = true) {
  return useQuery({ queryKey: ["workItems", params], queryFn: () => api.get<{ items: WorkItemDto[]; totalCount: number }>(endpoints.workItems.root, { pageSize: 50, ...params }), enabled, staleTime: 30_000 });
}
export function useWorkItem(id: string, enabled = true) {
  return useQuery({ queryKey: ["workItems", "detail", id], queryFn: () => api.get<WorkItemDto>(endpoints.workItems.detail(id)), enabled: Boolean(id) && enabled, staleTime: 30_000 });
}
export function useDocuments(params: Record<string, string | undefined> = {}, enabled = true) {
  return useQuery({ queryKey: ["documents", params], queryFn: () => api.get<{ items: DocumentDto[]; totalCount: number }>(endpoints.documents.root, { pageSize: 50, ...params }), enabled, staleTime: 30_000 });
}
export function usePortfolios(enabled = true) {
  return useQuery({ queryKey: ["portfolios"], queryFn: () => api.get<{ items: PortfolioDto[] }>(endpoints.portfolios.root, { pageSize: 100 }), enabled, staleTime: 30_000 });
}
export function useManagedPortfolios(search = "", enabled = true, scopeKey = "") {
  return useQuery({ queryKey: ["portfolio-management", "list", scopeKey, search], queryFn: () => api.get<PagedResult<PortfolioManagementListItem>>(endpoints.portfolios.management, { page: 1, pageSize: 50, search: search.trim() || undefined }), enabled, staleTime: 30_000 });
}

function usePortfolioCacheInvalidation() {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({ queryKey: ["portfolio-management"] });
    await queryClient.invalidateQueries({ queryKey: ["portfolios"] });
  };
}

export function useCreatePortfolio() {
  const invalidate = usePortfolioCacheInvalidation();
  return useMutation({ mutationFn: (dto: SavePortfolioDto) => api.post<PortfolioDto>(endpoints.portfolios.root, dto), onSuccess: invalidate });
}

export function useUpdatePortfolio() {
  const invalidate = usePortfolioCacheInvalidation();
  return useMutation({ mutationFn: ({ id, dto }: { id: string; dto: SavePortfolioDto }) => api.put<PortfolioDto>(endpoints.portfolios.detail(id), dto), onSuccess: invalidate });
}

export function useArchivePortfolio() {
  const invalidate = usePortfolioCacheInvalidation();
  return useMutation({ mutationFn: (id: string) => api.post<void>(endpoints.portfolios.archive(id)), onSuccess: invalidate });
}

export function useReactivatePortfolio() {
  const invalidate = usePortfolioCacheInvalidation();
  return useMutation({ mutationFn: (id: string) => api.post<void>(endpoints.portfolios.reactivate(id)), onSuccess: invalidate });
}

export function usePortfolioManagementOverview(id: string, params: { programsPage?: number; projectsPage?: number; pageSize?: number } = {}, enabled = true, scopeKey = "") {
  return useQuery({ queryKey: ["portfolio-management", "overview", scopeKey, id, params], queryFn: () => api.get<PortfolioManagementOverview>(endpoints.portfolios.managementOverview(id), params), enabled: Boolean(id) && enabled, staleTime: 30_000 });
}
export function usePortfolio(id: string, enabled = true, scopeKey = "") {
  return useQuery({ queryKey: ["portfolios", "detail", scopeKey, id], queryFn: () => api.get<PortfolioDto>(endpoints.portfolios.detail(id)), enabled: Boolean(id) && enabled, staleTime: 30_000 });
}
export function usePrograms(enabled = true) {
  return useQuery({ queryKey: ["programs"], queryFn: () => api.get<{ items: ProgramDto[] }>(endpoints.programs.root, { pageSize: 100 }), enabled, staleTime: 30_000 });
}
export function useProgram(id: string, enabled = true, scopeKey = "") {
  return useQuery({ queryKey: ["programs", "detail", scopeKey, id], queryFn: () => api.get<ProgramDto>(endpoints.programs.detail(id)), enabled: Boolean(id) && enabled, staleTime: 30_000 });
}
export function useManagedPrograms(search = "", enabled = true, scopeKey = "") {
  return useQuery({ queryKey: ["program-management", "list", scopeKey, search], queryFn: () => api.get<PagedResult<ProgramManagementListItem>>(endpoints.programs.management, { page: 1, pageSize: 50, search: search.trim() || undefined }), enabled, staleTime: 30_000 });
}
export function useCreateProgram() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (dto: SaveProgramDto) => api.post<ProgramDto>(endpoints.programs.root, dto),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["program-management"] });
      await queryClient.invalidateQueries({ queryKey: ["programs"] });
    },
  });
}
export function useUpdateProgram() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dto }: { id: string; dto: SaveProgramDto }) => api.put<ProgramDto>(endpoints.programs.detail(id), dto),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["program-management"] });
      await queryClient.invalidateQueries({ queryKey: ["programs"] });
    },
  });
}
export function useArchiveProgram() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<void>(endpoints.programs.archive(id)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["program-management"] });
      await queryClient.invalidateQueries({ queryKey: ["programs"] });
    },
  });
}
export function useReactivateProgram() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.post<void>(endpoints.programs.reactivate(id)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["program-management"] });
      await queryClient.invalidateQueries({ queryKey: ["programs"] });
    },
  });
}
export function useProgramManagementOverview(id: string, params: { projectsPage?: number; projectsPageSize?: number } = {}, enabled = true, scopeKey = "") {
  return useQuery({ queryKey: ["program-management", "overview", scopeKey, id, params], queryFn: () => api.get<ProgramManagementOverview>(endpoints.programs.managementOverview(id), params), enabled: Boolean(id) && enabled, staleTime: 30_000 });
}
