import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { useAuth } from "@auth/AuthProvider";

export type EmailNotificationScope = "Platform" | "Tenant" | "Organization";
export type EmailNotificationLogScope = Exclude<EmailNotificationScope, "Platform">;
export type SmtpSecurityMode = "StartTlsRequired" | "ImplicitTls";
export type SmtpAuthenticationMode = "UsernamePassword" | "TrustedRelay" | "OAuth2";

export type EmailNotificationSettingsAccess = {
  canReadTenant: boolean;
  canManageTenant: boolean;
  canTestTenant: boolean;
  canReadTenantLogs: boolean;
  canReadPlatform: boolean;
  canManagePlatform: boolean;
  canTestPlatform: boolean;
  oauth2Available: false;
};

export type EmailNotificationProfileView = {
  enabled: boolean;
  host: string;
  port: number;
  securityMode: SmtpSecurityMode;
  authenticationMode: SmtpAuthenticationMode;
  userName: string | null;
  hasPassword: boolean;
  fromAddress: string;
  fromName: string;
  replyToAddress: string | null;
  version: string;
};

export type EmailNotificationSettings = {
  scope: EmailNotificationScope;
  ownerId: string | null;
  persistenceAvailable: boolean;
  hasOverride: boolean;
  overrideProfile: EmailNotificationProfileView | null;
  effectiveProfile: EmailNotificationProfileView | null;
  effectiveSourceScope: EmailNotificationScope | null;
  canManage: boolean;
  canTest: boolean;
  canViewLogs: boolean;
  trustedRelayAvailableForEffectiveHost: boolean;
  oauth2Available: false;
};

export type UpdateEmailNotificationProfile = {
  enabled: boolean;
  host: string;
  port: number;
  securityMode: SmtpSecurityMode;
  authenticationMode: Exclude<SmtpAuthenticationMode, "OAuth2">;
  userName: string | null;
  password: string | null;
  clearPassword: boolean;
  fromAddress: string;
  fromName: string;
  replyToAddress: string | null;
  expectedVersion: string | null;
};

export type EmailNotificationOperationResult = {
  succeeded: boolean;
  failureCode: string | null;
  summary: string;
  accepted: boolean;
  isTransient: boolean;
};

export type EmailNotificationDeliveryLog = {
  id: string;
  createdAt: string;
  eventType: string;
  status: string;
  attemptCount: number;
  lastAttemptAt: string | null;
  failureCategory: string | null;
  organizationId: string | null;
  isTest: boolean;
};

const keys = {
  access: ["email-notification-settings", "access"] as const,
  settings: (scope: EmailNotificationScope, organizationId?: string, actorId?: string, tenantId?: string) =>
    ["email-notification-settings", scope, organizationId, actorId, tenantId] as const,
  logs: (scope: EmailNotificationLogScope, organizationId?: string, actorId?: string, tenantId?: string) =>
    ["email-notification-settings", "logs", scope, organizationId, actorId, tenantId] as const,
};

export function useEmailNotificationSettingsAccess() {
  const { user } = useAuth();
  return useQuery({
    queryKey: [...keys.access, user?.id, user?.tenantId],
    queryFn: () => api.get<EmailNotificationSettingsAccess>(endpoints.emailNotificationSettings.access),
    staleTime: 20_000,
  });
}

export function useEmailNotificationSettings(scope: EmailNotificationScope, organizationId?: string, enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: keys.settings(scope, organizationId, user?.id, user?.tenantId),
    queryFn: () => api.get<EmailNotificationSettings>(endpoints.emailNotificationSettings.profile(scope, organizationId)),
    enabled: enabled && (scope !== "Organization" || Boolean(organizationId)),
    staleTime: 15_000,
  });
}

export function useUpdateEmailNotificationSettings(scope: EmailNotificationScope, organizationId?: string) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (profile: UpdateEmailNotificationProfile) =>
      api.put<EmailNotificationSettings>(endpoints.emailNotificationSettings.profile(scope, organizationId), profile),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.settings(scope, organizationId, user?.id, user?.tenantId) }),
  });
}

export function useResetEmailNotificationSettings(scope: EmailNotificationScope, organizationId?: string) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (expectedVersion: string) =>
      api.post<EmailNotificationSettings>(endpoints.emailNotificationSettings.reset(scope, organizationId), { expectedVersion }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.settings(scope, organizationId, user?.id, user?.tenantId) }),
  });
}

export function useTestEmailNotificationConnection(scope: EmailNotificationScope, organizationId?: string) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<EmailNotificationOperationResult>(endpoints.emailNotificationSettings.testConnection(scope, organizationId), {}),
    onSettled: () => scope !== "Platform" && queryClient.invalidateQueries({
      queryKey: keys.logs(scope, organizationId, user?.id, user?.tenantId),
    }),
  });
}

export function useSendEmailNotificationTest(scope: EmailNotificationScope, organizationId?: string) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<EmailNotificationOperationResult>(endpoints.emailNotificationSettings.sendTest(scope, organizationId), {}),
    onSettled: () => scope !== "Platform" && queryClient.invalidateQueries({
      queryKey: keys.logs(scope, organizationId, user?.id, user?.tenantId),
    }),
  });
}

export function useCheckTrustedRelay(scope: EmailNotificationScope, organizationId?: string) {
  return useMutation({
    mutationFn: (host: string) => api.post<{ isAuthorized: boolean }>(
      endpoints.emailNotificationSettings.trustedRelayCheck(scope, organizationId), { host }),
  });
}

export function useEmailNotificationDeliveryLogs(scope: EmailNotificationLogScope, organizationId?: string, enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: keys.logs(scope, organizationId, user?.id, user?.tenantId),
    queryFn: () => api.get<EmailNotificationDeliveryLog[]>(endpoints.emailNotificationSettings.logs(scope, organizationId)),
    enabled: enabled && (scope !== "Organization" || Boolean(organizationId)),
    staleTime: 10_000,
  });
}

