import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import { endpoints } from "./endpoints";

export interface AccountProfile {
  userId: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  authenticationSource: "Local" | "EntraId" | "ActiveDirectoryLdap";
  passwordChangedAt?: string | null;
  lastLoginAt?: string | null;
}
export interface AccountProfessionalProfile {
  name: string;
  employeeId?: string | null;
  jobTitle?: string | null;
  email?: string | null;
  phone?: string | null;
  avatarUrl?: string | null;
  type: string;
  status: string;
  employmentType?: string | null;
  seniority?: string | null;
  dateOfJoining?: string | null;
  location?: string | null;
}
export interface AccountSession {
  id: string;
  createdAt: string;
  expiresAt: string;
  ipAddress?: string | null;
  userAgent?: string | null;
  isCurrent: boolean;
}
export interface AccountProjectRole { id: string; code: string; nameEn: string; nameAr: string; isPrimary: boolean; }
export interface AccountProjectMembership {
  projectId: string;
  projectName: string;
  projectCode: string;
  projectStatus: string;
  assignmentSource: string;
  role: string;
  allocationPercentage: number;
  startDate: string;
  endDate: string;
  functionalRoles: AccountProjectRole[];
}
export interface AccountData {
  profile: AccountProfile;
  professionalProfile: AccountProfessionalProfile | null;
  tenant: { tenantId: string; code: string; name: string; status: string } | null;
  organizationAccess: { organizationId: string; name: string; code?: string | null; canReadSettings: boolean; canManageSettings: boolean }[];
  organizationalUnits: { organizationalUnitId: string; name: string; type: string; isPrimary: boolean }[];
  teams: { teamId: string; name: string; code?: string | null; effectiveFrom: string }[];
  projects: AccountProjectMembership[];
  security: {
    canChangePassword: boolean;
    mustChangePassword: boolean;
    activeSessionCount: number;
    currentSessionId: string;
    passwordChangedAt?: string | null;
    lastLoginAt?: string | null;
  };
  sessions: AccountSession[];
}
export interface UpdateAccountProfileRequest { firstName: string; lastName: string; }
export interface ChangeAccountPasswordRequest { currentPassword: string; newPassword: string; }

export const accountKeys = { all: ["account"] as const, profile: ["account", "self"] as const, sessions: ["account", "sessions"] as const };

export const accountApi = {
  get: () => api.get<AccountData>(endpoints.account.root),
  updateProfile: (request: UpdateAccountProfileRequest) => api.put<AccountProfile>(endpoints.account.profile, request),
  changePassword: (request: ChangeAccountPasswordRequest) => api.post<{ reauthenticationRequired: boolean }>(endpoints.account.changePassword, request),
  getSessions: () => api.get<AccountSession[]>(endpoints.account.sessions),
  revokeSession: (sessionId: string) => api.delete<void>(endpoints.account.session(sessionId)),
  revokeOthers: () => api.post<{ revokedCount: number }>(endpoints.account.revokeOthers),
};

export function useAccount() {
  return useQuery({ queryKey: accountKeys.profile, queryFn: accountApi.get, staleTime: 30_000, refetchOnWindowFocus: true });
}

export function useUpdateAccountProfile() {
  const client = useQueryClient();
  return useMutation({ mutationFn: accountApi.updateProfile, onSuccess: () => client.invalidateQueries({ queryKey: accountKeys.all }) });
}

export function useChangeAccountPassword() {
  return useMutation({ mutationFn: accountApi.changePassword });
}

export function useRevokeAccountSession() {
  const client = useQueryClient();
  return useMutation({ mutationFn: accountApi.revokeSession, onSuccess: () => { void client.invalidateQueries({ queryKey: accountKeys.all }); } });
}

export function useRevokeOtherAccountSessions() {
  const client = useQueryClient();
  return useMutation({ mutationFn: accountApi.revokeOthers, onSuccess: () => { void client.invalidateQueries({ queryKey: accountKeys.all }); } });
}
