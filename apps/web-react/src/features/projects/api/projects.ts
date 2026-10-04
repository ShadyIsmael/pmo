import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";

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

const projectKeys = {
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
  conflicts: (projectId: string, resourceId: string, startDate: string, endDate: string, allocationPercentage: number, excludeMemberId?: string) =>
    [...projectKeys.all, "conflicts", projectId, resourceId, startDate, endDate, allocationPercentage, excludeMemberId] as const,
};

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
    queryFn: () => api.get<ProjectDto>(`/api/projects/${id}/overview`),
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