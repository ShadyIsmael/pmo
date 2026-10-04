"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";
import { teamKeys, type DepartmentOption, type TeamFilterParams, type TeamStatusFilter } from "@/lib/api/teams";

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

function toParams(filter: TeamFilterParams) {
  return {
    page: filter.page,
    pageSize: filter.pageSize,
    search: filter.search || undefined,
    departmentId: filter.departmentId || undefined,
    isActive: filter.isActive,
  };
}

export function useTeams(filter: TeamFilterParams = {}, enabled = true) {
  return useQuery({
    queryKey: teamKeys.list(filter),
    queryFn: () => api.get<PagedResult<TeamDto>>(endpoints.teams.root, toParams(filter)),
    staleTime: 30_000,
    enabled,
  });
}

/** Active-only team lookup for NEW relationships (selectors). Management listing uses useTeams (all by default). */
export function useActiveTeams() {
  return useQuery({
    queryKey: teamKeys.lookup(true),
    queryFn: () => api.get<TeamLookupDto[]>(endpoints.teams.lookup, { activeOnly: true }),
    staleTime: 60_000,
  });
}

export function useTeam(id: string | null | undefined, includeMembers = false) {
  return useQuery({
    queryKey: [...teamKeys.detail(id ?? ""), { includeMembers }] as const,
    queryFn: () => api.get<TeamDto>(endpoints.teams.detail(id!), { includeMembers }),
    enabled: Boolean(id),
    staleTime: 30_000,
  });
}

export function useTeamMembers(teamId: string | null | undefined, currentOnly = true) {
  return useQuery({
    queryKey: [...teamKeys.members(teamId ?? ""), { currentOnly }] as const,
    queryFn: () => api.get<TeamMemberDto[]>(endpoints.teams.members(teamId!), { currentOnly }),
    enabled: Boolean(teamId),
    staleTime: 30_000,
  });
}

function useInvalidateTeams() {
  const queryClient = useQueryClient();
  return (id?: string) => {
    const invalidations = [queryClient.invalidateQueries({ queryKey: teamKeys.all })];
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
    mutationFn: (input: CreateTeamInput) => api.post<TeamDto>(endpoints.teams.root, input),
    onSuccess: (created) => invalidate(created?.id),
  });
}

export function useUpdateTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateTeamInput }) =>
      api.put<TeamDto>(endpoints.teams.detail(id), input),
    onSuccess: (_data, variables) => invalidate(variables.id),
  });
}

export function useDeactivateTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: (id: string) => api.put<TeamDto>(endpoints.teams.deactivate(id), {}),
    onSuccess: (_data, id) => invalidate(id),
  });
}

export function useReactivateTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: (id: string) => api.put<TeamDto>(endpoints.teams.reactivate(id), {}),
    onSuccess: (_data, id) => invalidate(id),
  });
}

export function useDeleteTeam() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.teams.detail(id)),
    onSuccess: () => invalidate(),
  });
}

export function useAddTeamMembers() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ teamId, resourceIds, effectiveFrom }: { teamId: string; resourceIds: string[]; effectiveFrom?: string }) =>
      api.post<TeamMemberDto[]>(endpoints.teams.members(teamId), {
        resourceIds,
        effectiveFrom: effectiveFrom || undefined,
      }),
    onSuccess: (_data, variables) => invalidate(variables.teamId),
  });
}

/** Replaces the current membership set; members left out are closed with an EffectiveTo. */
export function useSetTeamMembers() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ teamId, resourceIds, effectiveFrom }: { teamId: string; resourceIds: string[]; effectiveFrom?: string }) =>
      api.put<TeamMemberDto[]>(endpoints.teams.members(teamId), {
        resourceIds,
        effectiveFrom: effectiveFrom || undefined,
      }),
    onSuccess: (_data, variables) => invalidate(variables.teamId),
  });
}

/** Closes a single membership. Membership rows are never hard-deleted, to preserve history. */
export function useRemoveTeamMember() {
  const invalidate = useInvalidateTeams();
  return useMutation({
    mutationFn: ({ teamId, resourceId, effectiveTo }: { teamId: string; resourceId: string; effectiveTo?: string }) =>
      api.delete<TeamMemberDto[]>(endpoints.teams.member(teamId, resourceId), undefined, {
        effectiveTo: effectiveTo || undefined,
      }),
    onSuccess: (_data, variables) => invalidate(variables.teamId),
  });
}

export type { TeamFilterParams, TeamStatusFilter };

/** Departments are reference data shared with the Team form's optional Department link. */
export function useDepartments(activeOnly = true, enabled = true) {
  return useQuery({
    queryKey: ["departments", "list", activeOnly] as const,
    queryFn: () => api.get<PagedResult<DepartmentOption>>(endpoints.departments, { page: 1, pageSize: 200 }),
    staleTime: 300_000,
    enabled,
    select: (data) => (data.items ?? []).filter((department) => !activeOnly || department.isActive !== false),
  });
}
