import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { stakeholderInvitationApi } from "@/lib/api/stakeholder-invitations";
import { projectKeys } from "@/lib/api/projects";

export const stakeholderInvitationKeys = {
  all: ["stakeholder-invitations"] as const,
  detail: (projectId: string, stakeholderId: string) => ["stakeholder-invitations", projectId, stakeholderId] as const,
};

export function useStakeholderInvitation(projectId: string, stakeholderId: string, enabled = true) {
  return useQuery({
    queryKey: stakeholderInvitationKeys.detail(projectId, stakeholderId),
    queryFn: () => stakeholderInvitationApi.get(projectId, stakeholderId),
    enabled: enabled && !!projectId && !!stakeholderId,
    staleTime: 10_000,
  });
}

export function useSendStakeholderInvitation(projectId: string, stakeholderId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => stakeholderInvitationApi.send(projectId, stakeholderId),
    onSuccess: (data) => {
      queryClient.setQueryData(stakeholderInvitationKeys.detail(projectId, stakeholderId), data);
      queryClient.invalidateQueries({ queryKey: projectKeys.people(projectId) });
    },
  });
}

export function useRevokeStakeholderInvitation(projectId: string, stakeholderId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => stakeholderInvitationApi.revoke(projectId, stakeholderId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: stakeholderInvitationKeys.detail(projectId, stakeholderId) });
      queryClient.invalidateQueries({ queryKey: projectKeys.people(projectId) });
    },
  });
}
