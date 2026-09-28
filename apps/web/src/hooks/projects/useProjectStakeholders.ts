"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { endpoints } from "@/lib/api/endpoints";
import { stakeholderApi, stakeholderKeys, type AddContactProjectStakeholderDto, type AddInternalProjectStakeholderDto, type UpdateProjectStakeholderDto } from "@/lib/api/stakeholders";
import { userKeys } from "@/lib/api/users";

export function useProjectStakeholders(projectId: string, enabled = true) {
  return useQuery({ queryKey: stakeholderKeys.project(projectId), queryFn: () => stakeholderApi.list(projectId), enabled: !!projectId && enabled, staleTime: 30_000 });
}

function invalidate(queryClient: ReturnType<typeof useQueryClient>, projectId: string) {
  queryClient.invalidateQueries({ queryKey: stakeholderKeys.project(projectId) });
}

export function useAddInternalProjectStakeholder(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (body: AddInternalProjectStakeholderDto) => stakeholderApi.addInternal(projectId, body), onSuccess: () => invalidate(queryClient, projectId) });
}
export function useAddContactProjectStakeholder(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (body: AddContactProjectStakeholderDto) => stakeholderApi.addContact(projectId, body), onSuccess: () => invalidate(queryClient, projectId) });
}
export function useUpdateProjectStakeholder(projectId: string, stakeholderId: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (body: UpdateProjectStakeholderDto) => stakeholderApi.update(projectId, stakeholderId, body), onSuccess: () => invalidate(queryClient, projectId) });
}
export function useRevokeProjectStakeholder(projectId: string, stakeholderId: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: (rowVersion: string) => stakeholderApi.revoke(projectId, stakeholderId, rowVersion), onSuccess: () => invalidate(queryClient, projectId) });
}

export interface UserLookupDto { id: string; fullName: string; email: string; jobTitle?: string; }
export function useStakeholderUserLookup(enabled = true) {
  return useQuery({ queryKey: userKeys.lookup(), queryFn: () => api.get<UserLookupDto[]>(endpoints.users.lookup), enabled, staleTime: 60_000 });
}
