import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";
import { useAuth } from "@auth/AuthProvider";

export type SettingsAccess = {
  canReadTenant: boolean;
  canWriteTenant: boolean;
  canReadPlatform: boolean;
  canWritePlatform: boolean;
};

export type EffectiveSetting = {
  key: string;
  type: "String" | "Integer" | "Decimal" | "Boolean";
  defaultValue: unknown;
  value: unknown;
  valueSource: "TenantOverride" | "OrganizationOverride" | "PlatformDefault" | "PlatformOverride" | "DefinitionDefault";
  isInherited: boolean;
  canOverride: boolean;
  version?: string | null;
  validationDescription: string;
  allowedScopes: Array<"Platform" | "Tenant" | "Organization" | "Project" | "User">;
  overrideBehavior: "TenantOverride" | "PlatformOnly" | "InheritOnly" | "ConstrainedTenantOverride" | "TenantAndOrganizationOverride" | "ConstrainedTenantAndOrganizationOverride" | "HierarchicalOverride" | "ConstrainedHierarchicalOverride";
  securityClassification: "Standard" | "Sensitive" | "Secret";
};

export type SettingsReadResponse = {
  scope: "Platform" | "Tenant" | "Organization";
  persistenceAvailable: boolean;
  settings: EffectiveSetting[];
  ownerId?: string | null;
};

export type TenantFiscalYearPreview = {
  startMonth: number;
  timeZoneId: string;
  fiscalYear: number;
  label: string;
  startDate: string;
  endDate: string;
  endDateExclusive: string;
};

export type OrganizationProfile = {
  id: string;
  name: string;
  description?: string | null;
  website?: string | null;
  email?: string | null;
  phone?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  country?: string | null;
  postcode?: string | null;
  updatedAt?: string | null;
  canManageSettings: boolean;
};

export type OrganizationProfileUpdate = Omit<OrganizationProfile, "id" | "updatedAt" | "canManageSettings"> & {
  expectedUpdatedAt?: string | null;
};

export type OrganizationDirectoryItem = { id: string; name: string };
export type OrganizationSettingsGrantTarget = { id: string; fullName: string; email: string };
export type OrganizationSettingsGrant = {
  id: string; organizationId: string; userId: string; userName: string; userEmail: string;
  canRead: boolean; canManage: boolean; version: string;
};
export type SettingCurrencyOption = { code: string; name: string; symbol: string };

const keys = {
  access: ["settings", "access"] as const,
  platform: ["settings", "platform"] as const,
  tenant: ["settings", "tenant"] as const,
  runtime: ["settings", "runtime"] as const,
  organizations: ["settings", "organization-profiles"] as const,
  organization: (organizationId: string) => ["settings", "organization", organizationId] as const,
  grants: (organizationId: string) => ["settings", "organization-grants", organizationId] as const,
  grantTargets: (tenantId?: string) => ["settings", "organization-grant-targets", tenantId] as const,
  organizationDirectory: (tenantId?: string) => ["settings", "organization-directory", tenantId] as const,
};

export function useSettingsAccess() {
  const { user } = useAuth();
  return useQuery({
    queryKey: [...keys.access, user?.id, user?.tenantId],
    queryFn: () => api.get<SettingsAccess>(endpoints.settings.access),
    staleTime: 30_000,
  });
}

export function usePlatformSettings(enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: [...keys.platform, user?.id],
    queryFn: () => api.get<SettingsReadResponse>(endpoints.settings.platform),
    enabled,
    staleTime: 30_000,
  });
}

export function useTenantSettings(enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: [...keys.tenant, user?.tenantId],
    queryFn: () => api.get<SettingsReadResponse>(endpoints.settings.tenant),
    enabled,
    staleTime: 30_000,
  });
}

export function useTenantFiscalYearPreview(startMonth: number, enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: ["settings", "tenant-fiscal-year", user?.tenantId, startMonth],
    queryFn: () => api.get<TenantFiscalYearPreview>(endpoints.settings.tenantFiscalYearPreview(startMonth)),
    enabled: enabled && Boolean(user?.tenantId) && startMonth >= 1 && startMonth <= 12,
    staleTime: 0,
  });
}

export function useTenantRuntimeSettings(enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: [...keys.runtime, user?.tenantId],
    queryFn: () => api.get<SettingsReadResponse>(endpoints.settings.runtime),
    enabled: enabled && Boolean(user?.tenantId),
    staleTime: 5 * 60_000,
  });
}

export function useSettingCurrencies(enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: ["settings", "active-currencies", user?.tenantId],
    queryFn: () => api.get<SettingCurrencyOption[]>(endpoints.currencies.root),
    enabled,
    staleTime: 60_000,
  });
}

export function useOrganizationProfiles(enabled = true, refreshAuthorization = false) {
  const { user } = useAuth();
  return useQuery({
    queryKey: [...keys.organizations, user?.tenantId],
    queryFn: () => api.get<OrganizationProfile[]>(endpoints.settings.organizationProfiles),
    enabled,
    staleTime: refreshAuthorization ? 0 : 30_000,
    refetchInterval: refreshAuthorization ? 30_000 : false,
    refetchIntervalInBackground: false,
  });
}

export function useOrganizationSettings(organizationId: string, enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: [...keys.organization(organizationId), user?.tenantId],
    queryFn: () => api.get<SettingsReadResponse>(endpoints.settings.organizationSettings(organizationId)),
    enabled: enabled && Boolean(organizationId),
    staleTime: 30_000,
  });
}

export function useOrganizationDirectory(enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: keys.organizationDirectory(user?.tenantId),
    queryFn: () => api.get<{ items: OrganizationDirectoryItem[] }>(endpoints.organization.directory, { page: 1, pageSize: 100 }),
    enabled,
    staleTime: 30_000,
  });
}

export function useOrganizationGrantTargets(enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: keys.grantTargets(user?.tenantId),
    queryFn: () => api.get<OrganizationSettingsGrantTarget[]>(endpoints.organization.settingsGrantTargets),
    enabled,
    staleTime: 60_000,
  });
}

export function useOrganizationSettingsGrants(organizationId: string, enabled = true) {
  const { user } = useAuth();
  return useQuery({
    queryKey: [...keys.grants(organizationId), user?.tenantId],
    queryFn: () => api.get<OrganizationSettingsGrant[]>(endpoints.organization.settingsGrants(organizationId)),
    enabled: enabled && Boolean(organizationId),
    staleTime: 30_000,
  });
}

export function useSaveOrganizationSettingsGrant() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ organizationId, userId, canRead, canManage, expectedVersion }: {
      organizationId: string; userId: string; canRead: boolean; canManage: boolean; expectedVersion?: string | null;
    }) => api.put<OrganizationSettingsGrant>(endpoints.organization.settingsGrants(organizationId), {
      userId, canRead, canManage, expectedVersion: expectedVersion ?? null,
    }),
    onSuccess: (_grant, variables) => {
      void queryClient.invalidateQueries({ queryKey: keys.grants(variables.organizationId) });
      void queryClient.invalidateQueries({ queryKey: keys.organizations });
    },
  });
}

export function useRevokeOrganizationSettingsGrant() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ organizationId, userId, version }: { organizationId: string; userId: string; version: string }) =>
      api.post<void>(endpoints.organization.revokeSettingsGrant(organizationId, userId), { expectedVersion: version }),
    onSuccess: (_result, variables) => {
      void queryClient.invalidateQueries({ queryKey: keys.grants(variables.organizationId) });
      void queryClient.invalidateQueries({ queryKey: keys.organizations });
    },
  });
}

export type SettingsScope = "Platform" | "Tenant" | "Organization";
export type SettingUpdate = { key: string; value: unknown; expectedVersion?: string | null };
export type SettingReset = { key: string; expectedVersion: string };

export function useUpdateSetting(scope: SettingsScope, organizationId?: string) {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  return useMutation({
    mutationFn: ({ key, value, expectedVersion }: SettingUpdate) => {
      const body = { value, expectedVersion: expectedVersion ?? null };
      if (scope === "Platform") return api.put<EffectiveSetting>(endpoints.settings.platformValue(key), body);
      if (scope === "Tenant") return api.put<EffectiveSetting>(endpoints.settings.tenantValue(key), body);
      if (!organizationId) throw new Error("Organization context is required.");
      return api.put<EffectiveSetting>(endpoints.settings.organizationValue(organizationId, key), body);
    },
    onSuccess: () => {
      if (scope === "Platform") void queryClient.invalidateQueries({ queryKey: keys.platform });
      else if (scope === "Tenant") {
        void queryClient.invalidateQueries({ queryKey: [...keys.tenant, user?.tenantId] });
        void queryClient.invalidateQueries({ queryKey: [...keys.runtime, user?.tenantId] });
        void queryClient.invalidateQueries({ queryKey: ["settings", "tenant-fiscal-year", user?.tenantId] });
      } else if (organizationId) void queryClient.invalidateQueries({ queryKey: keys.organization(organizationId) });
    },
  });
}

export function useResetSetting(scope: SettingsScope, organizationId?: string) {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  return useMutation({
    mutationFn: ({ key, expectedVersion }: SettingReset) => {
      const body = { expectedVersion };
      if (scope === "Platform") return api.post<EffectiveSetting>(endpoints.settings.resetPlatformValue(key), body);
      if (scope === "Tenant") return api.post<EffectiveSetting>(endpoints.settings.resetTenantValue(key), body);
      if (!organizationId) throw new Error("Organization context is required.");
      return api.post<EffectiveSetting>(endpoints.settings.resetOrganizationValue(organizationId, key), body);
    },
    onSuccess: () => {
      if (scope === "Platform") void queryClient.invalidateQueries({ queryKey: keys.platform });
      else if (scope === "Tenant") {
        void queryClient.invalidateQueries({ queryKey: [...keys.tenant, user?.tenantId] });
        void queryClient.invalidateQueries({ queryKey: [...keys.runtime, user?.tenantId] });
        void queryClient.invalidateQueries({ queryKey: ["settings", "tenant-fiscal-year", user?.tenantId] });
      } else if (organizationId) void queryClient.invalidateQueries({ queryKey: keys.organization(organizationId) });
    },
  });
}

export function useUpdateOrganizationProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, update }: { id: string; update: OrganizationProfileUpdate }) =>
      api.put<OrganizationProfile>(endpoints.settings.organizationProfile(id), update),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: keys.organizations }),
  });
}
