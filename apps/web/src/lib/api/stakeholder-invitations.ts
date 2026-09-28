import { api } from "@/lib/api";
import { endpoints } from "@/lib/api/endpoints";

export interface StakeholderInvitationDto {
  id: string;
  projectId: string;
  projectStakeholderId: string;
  resourceId: string;
  email: string;
  status: "Pending" | "Accepted" | "Revoked" | "Expired" | string;
  expiresAt: string;
  acceptedAt?: string | null;
  revokedAt?: string | null;
  invitationUrl?: string | null;
  emailDeliveryDeferred: boolean;
}

export interface StakeholderInvitationValidationDto {
  projectId: string;
  resourceId: string;
  resourceName: string;
  email: string;
  projectName: string;
  relationshipType: string;
  expiresAt: string;
}

export interface AcceptStakeholderInvitationDto { password: string; confirmPassword: string; }
export interface StakeholderInvitationAcceptanceDto { userId: string; resourceId: string; email: string; status: string; }

export const stakeholderInvitationApi = {
  get: (projectId: string, stakeholderId: string) => api.get<StakeholderInvitationDto>(endpoints.stakeholderInvitations.manage(projectId, stakeholderId)),
  send: (projectId: string, stakeholderId: string) => api.post<StakeholderInvitationDto>(endpoints.stakeholderInvitations.manage(projectId, stakeholderId), {}),
  revoke: (projectId: string, stakeholderId: string) => api.post<StakeholderInvitationDto>(endpoints.stakeholderInvitations.revoke(projectId, stakeholderId), {}),
  validate: (token: string) => api.get<StakeholderInvitationValidationDto>(endpoints.stakeholderInvitations.validate, { token }),
  accept: (token: string, body: AcceptStakeholderInvitationDto) => api.post<StakeholderInvitationAcceptanceDto>(`${endpoints.stakeholderInvitations.accept}?token=${encodeURIComponent(token)}`, body),
};
