import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "@api/client";
import { projectKeys } from "./projects";

export interface StakeholderInvitationDto {
  id: string;
  projectId: string;
  projectStakeholderId: string;
  resourceId: string;
  email: string;
  status: "Pending" | "Accepted" | "Declined" | "Revoked" | "Expired" | string;
  expiresAt: string;
  acceptedAt?: string | null;
  declinedAt?: string | null;
  revokedAt?: string | null;
  reportsConsent: boolean;
  notificationsConsent: boolean;
  emailDeliveryStatus: string;
  emailSentAt?: string | null;
  invitationUrl?: string | null;
  emailDeliveryDeferred: boolean;
}

export { ApiError };

const invitationKeys = {
  all: ["stakeholder-invitations"] as const,
  detail: (projectId: string, stakeholderId: string) => ["stakeholder-invitations", projectId, stakeholderId] as const,
};

const invitationApi = {
  get: (projectId: string, stakeholderId: string) =>
    api.get<StakeholderInvitationDto>(`/api/projects/${encodeURIComponent(projectId)}/stakeholders/${encodeURIComponent(stakeholderId)}/invitation`),
  send: (projectId: string, stakeholderId: string) =>
    api.post<StakeholderInvitationDto>(`/api/projects/${encodeURIComponent(projectId)}/stakeholders/${encodeURIComponent(stakeholderId)}/invitation`, {}),
  revoke: (projectId: string, stakeholderId: string) =>
    api.post<StakeholderInvitationDto>(`/api/projects/${encodeURIComponent(projectId)}/stakeholders/${encodeURIComponent(stakeholderId)}/invitation/revoke`, {}),
};

export function useStakeholderInvitation(projectId: string, stakeholderId: string, enabled = true) {
  return useQuery({
    queryKey: invitationKeys.detail(projectId, stakeholderId),
    queryFn: () => invitationApi.get(projectId, stakeholderId),
    enabled: enabled && !!projectId && !!stakeholderId,
    staleTime: 10_000,
  });
}

export function useSendStakeholderInvitation(projectId: string, stakeholderId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => invitationApi.send(projectId, stakeholderId),
    onSuccess: (data) => {
      queryClient.setQueryData(invitationKeys.detail(projectId, stakeholderId), data);
      void queryClient.invalidateQueries({ queryKey: projectKeys.people(projectId) });
    },
  });
}

export function useRevokeStakeholderInvitation(projectId: string, stakeholderId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => invitationApi.revoke(projectId, stakeholderId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: invitationKeys.detail(projectId, stakeholderId) });
      void queryClient.invalidateQueries({ queryKey: projectKeys.people(projectId) });
    },
  });
}
