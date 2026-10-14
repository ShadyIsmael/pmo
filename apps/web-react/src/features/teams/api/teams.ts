import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";

export interface TeamMemberDto {
  id: string;
  teamId: string;
  resourceId: string;
  resourceName?: string;
  resourceType?: string;
  effectiveFrom: string;
  effectiveTo?: string;
  isActive: boolean;
}

export interface TeamDto {
  id: string;
  name: string;
  code?: string;
  description?: string;
  departmentId?: string;
  departmentName?: string;
  teamLeadId?: string;
  teamLeadName?: string;
  isActive: boolean;
  emailAlias?: string;
  externalDirectoryId?: string;
  memberCount: number;
  members: TeamMemberDto[];
}

export interface TeamLookupDto {
  id: string;
  code?: string;
  name: string;
  isActive: boolean;
}

export interface CreateTeamInput {
  name: string;
  description?: string;
  departmentId?: string;
  teamLeadId?: string;
  emailAlias?: string;
  memberResourceIds?: string[];
}

export interface UpdateTeamInput {
  name?: string;
  description?: string;
  departmentId?: string;
  teamLeadId?: string;
  emailAlias?: string;
  isActive?: boolean;
}

export interface TeamFilterParams {
  page?: number;
  pageSize?: number;
  search?: string;
  departmentId?: string;
  isActive?: boolean;
  [key: string]: string | number | boolean | undefined;
}

export interface DepartmentOption {
  id: string;
  name: string;
  code?: string;
  isActive: boolean;
}

export interface PagedResult<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

const teamKeys = {
  all: ["teams"] as const,
  lists: () => [...teamKeys.all, "list"] as const,
  list: (filter: TeamFilterParams) => [...teamKeys.lists(), filter] as const,
  detail: (id: string) => [...teamKeys.all, "detail", id] as const,
  lookup: (activeOnly: boolean) => [...teamKeys.all, "lookup", activeOnly] as const,
  members: (teamId: string) => [...teamKeys.all, "members", teamId] as const,
};

export function useTeams(filter: TeamFilterParams = {}, enabled = true) {
  return useQuery({
    queryKey: teamKeys.list(filter),
    queryFn: () => api.get<PagedResult<TeamDto>>("/api/teams", {
      page: filter.page,
      pageSize: filter.pageSize,
      search: filter.search || undefined,
      departmentId: filter.departmentId || undefined,
      isActive: filter.isActive,
    }),
    staleTime: 30_000,
    enabled,
  });
}

export function useActiveTeams() {
  return useQuery({
    queryKey: teamKeys.lookup(true),
    queryFn: () => api.get<TeamLookupDto[]>("/api/teams/lookup", { activeOnly: true }),
    staleTime: 60_000,
  });
}

export function useTeam(id: string | null | undefined, includeMembers = false) {
  return useQuery({
    queryKey: [...teamKeys.detail(id ?? ""), { includeMembers }] as const,
    queryFn: () => api.get<TeamDto>(`/api/teams/${id!}`, { includeMembers }),
    enabled: Boolean(id),
    staleTime: 30_000,
  });
}

export function useTeamMembers(teamId: string | null | undefined, currentOnly = true) {
  return useQuery({
    queryKey: [...teamKeys.members(teamId ?? ""), { currentOnly }] as const,
    queryFn: () => api.get<TeamMemberDto[]>(`/api/teams/${teamId!}/members`, { currentOnly }),
    enabled: Boolean(teamId),
    staleTime: 30_000,
  });
}

function useInvalidateTeams() {
  const queryClient = useQueryClient();
  return (id?: string) => {
    const invalidations = [queryClient.invalidateQueries({ queryKey: teamKeys.lists() })];
    if (id) {
      invalidations.push(
        queryClient.invalidateQueries({ queryKey: teamKeys.detail(id) }),
        queryClient.invalidateQueries({ queryKey: teamKeys.members(id) }),
      );
    }
    return Promise.all(invalidations).then(() => undefined);
  };
}

export function useCreateTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: (input: CreateTeamInput) => api.post<TeamDto>("/api/teams", input),
    onSuccess: (created) => invalidate(created?.id),
  });
}

export function useUpdateTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateTeamInput }) =>
      api.put<TeamDto>(`/api/teams/${id}`, input),
    onSuccess: (_data, variables) => invalidate(variables.id),
  });
}

export function useDeactivateTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: (id: string) => api.put<TeamDto>(`/api/teams/${id}/deactivate`, {}),
    onSuccess: (_data, id) => invalidate(id),
  });
}

export function useReactivateTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: (id: string) => api.put<TeamDto>(`/api/teams/${id}/reactivate`, {}),
    onSuccess: (_data, id) => invalidate(id),
  });
}

export function useDeleteTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/api/teams/${id}`),
    onSuccess: () => invalidate(),
  });
}

export function useAddTeamMembers() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ teamId, resourceIds, effectiveFrom }: { teamId: string; resourceIds: string[]; effectiveFrom?: string }) =>
      api.post<TeamMemberDto[]>(`/api/teams/${teamId}/members`, {
        resourceIds,
        effectiveFrom: effectiveFrom || undefined,
      }),
    onSuccess: (_data, variables) => invalidate(variables.teamId),
  });
}

export function useSetTeamMembers() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ teamId, resourceIds, effectiveFrom }: { teamId: string; resourceIds: string[]; effectiveFrom?: string }) =>
      api.put<TeamMemberDto[]>(`/api/teams/${teamId}/members`, {
        resourceIds,
        effectiveFrom: effectiveFrom || undefined,
      }),
    onSuccess: (_data, variables) => invalidate(variables.teamId),
  });
}

export function useRemoveTeamMember() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ teamId, resourceId, effectiveTo }: { teamId: string; resourceId: string; effectiveTo?: string }) =>
      api.delete<TeamMemberDto[]>(`/api/teams/${teamId}/members/${resourceId}`, undefined, {
        effectiveTo: effectiveTo || undefined,
      }),
    onSuccess: (_data, variables) => invalidate(variables.teamId),
  });
}

export function useDepartments(activeOnly = true, enabled = true) {
  return useQuery({
    queryKey: ["departments", "list", activeOnly] as const,
    queryFn: () => api.get<PagedResult<DepartmentOption>>("/api/departments", { page: 1, pageSize: 200 }),
    staleTime: 300_000,
    enabled,
    select: (data) => (data.items ?? []).filter((department) => !activeOnly || department.isActive !== false),
  });
}