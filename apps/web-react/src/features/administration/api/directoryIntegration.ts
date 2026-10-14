import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export type DirectoryProviderType = "Entra" | "Ldap";
export interface DirectoryConnectionDto {
  id: string; name: string; providerType: DirectoryProviderType; isActive: boolean; isDefault: boolean;
  directoryTenantId?: string | null; clientId?: string | null; credentialReference?: string | null; credentialConfigured: boolean;
  host?: string | null; port?: number | null; useSsl: boolean; baseDn?: string | null; bindUser?: string | null;
  bindCredentialReference?: string | null; bindCredentialConfigured: boolean; domain?: string | null; createdAt: string; updatedAt?: string | null;
}
export interface SaveDirectoryConnectionInput {
  name: string; providerType: DirectoryProviderType; isActive: boolean; isDefault: boolean;
  directoryTenantId?: string; clientId?: string; credentialReference?: string;
  host?: string; port?: number; useSsl: boolean; baseDn?: string; bindUser?: string; bindCredentialReference?: string; domain?: string;
}
export interface DirectorySyncScopeDto {
  scopeType: "SelectedGroups" | "SelectedUsers" | "OrganizationalUnits" | "AllUsers";
  selectedGroupIds: string[]; selectedUserIds: string[]; organizationalUnits: string[];
  userFilter?: string | null; groupFilter?: string | null; maxItems: number;
}
export interface DirectoryFieldMappingDto { targetField: string; sourceAttribute: string; updatePolicy: "Ignore" | "Review" | "AutoUpdate"; transform?: string | null; }
export interface DirectoryGroupDto { externalDirectoryId: string; name: string; description?: string | null; isEnabled: boolean; }
export interface DirectoryGroupMappingDto { externalGroupId: string; externalGroupName: string; roleId?: string | null; isActive: boolean; }
export interface DirectoryOrganizationMappingDto { directoryValue: string; organizationalUnitId: string; }
export interface DirectoryProvisioningPolicyDto {
  missingUserAction: string; missingGroupAction: string; disabledUserAction: string; outOfScopeUserAction: string;
  changedGroupAction: string; missingGroupReferenceAction: string; createResource: boolean;
  defaultOrganizationalUnitId?: string | null; defaultBaseCalendarId?: string | null; defaultEmploymentType?: string | null; defaultSeniority?: string | null;
}
export interface DirectoryPreviewItemDto {
  itemType: "User" | "Group"; externalDirectoryId: string; displayName: string; classification: string; proposedAction: string;
  policyReason: string; matchedUserId?: string | null; matchedUserName?: string | null; changes: Record<string, string | null>; selectable: boolean;
}
export interface DirectoryPreviewResultDto { connectionId: string; page: number; pageSize: number; hasMore: boolean; items: DirectoryPreviewItemDto[]; providerStatus: { connected: boolean; provider: string; directoryName?: string | null; latencyMilliseconds: number; errorCode?: string | null; message?: string | null }; }
export interface SelectedDirectorySyncItem { itemType: "User" | "Group"; externalDirectoryId: string; action: string; }
export interface DirectorySyncRunDto {
  id: string; connectionId: string; connectionName: string; startedAt: string; completedAt?: string | null; mode: string; scope: string; status: string;
  usersFetched: number; usersCreated: number; usersUpdated: number; usersDeactivated: number; usersSkipped: number;
  groupsCreated: number; groupsUpdated: number; groupsDeactivated: number; conflicts: number; failures: number;
}
export interface DirectorySyncItemDto {
  id: string; itemType: string; externalDirectoryId: string; displayName?: string | null; classification: string;
  proposedAction: string; matchedUserId?: string | null; status: string; errorCode?: string | null; details: Record<string, string | null>;
}
export interface DirectorySyncRunDetailDto { run: DirectorySyncRunDto; items: DirectorySyncItemDto[]; }
export interface DirectoryReferenceOptionDto { id: string; name: string; code?: string | null; }
export interface DirectoryRoleOptionDto { id: string; name: string; description?: string | null; category?: string | null; }
export interface SaveDirectoryConnectionVariables { id?: string; input: SaveDirectoryConnectionInput; }

const keys = {
  connections: ["directory-integration", "connections"] as const,
  scope: (id: string) => ["directory-integration", "scope", id] as const,
  fieldMappings: (id: string) => ["directory-integration", "field-mappings", id] as const,
  groupMappings: (id: string) => ["directory-integration", "group-mappings", id] as const,
  organizationMappings: (id: string) => ["directory-integration", "organization-mappings", id] as const,
  policy: (id: string) => ["directory-integration", "provisioning-policy", id] as const,
  history: ["directory-integration", "history"] as const,
};

export function useDirectoryConnections(enabled = true) {
  return useQuery({ queryKey: keys.connections, queryFn: () => api.get<DirectoryConnectionDto[]>(endpoints.directoryIntegrations.connections), enabled, staleTime: 15_000 });
}
export function useSaveDirectoryConnection() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: SaveDirectoryConnectionVariables) => id
      ? api.put<DirectoryConnectionDto>(endpoints.directoryIntegrations.connection(id), input)
      : api.post<DirectoryConnectionDto>(endpoints.directoryIntegrations.connections, input),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.connections }),
  });
}
export function useTestDirectoryConnection() {
  return useMutation({ mutationFn: (id: string) => api.post<DirectoryPreviewResultDto["providerStatus"]>(endpoints.directoryIntegrations.test(id)) });
}
export function useDirectoryScope(connectionId: string | null, enabled = true) {
  return useQuery({ queryKey: connectionId ? keys.scope(connectionId) : ["directory-integration", "scope", "none"], queryFn: () => api.get<DirectorySyncScopeDto>(endpoints.directoryIntegrations.scope(connectionId!)), enabled: enabled && Boolean(connectionId), staleTime: 0 });
}
export function useSaveDirectoryScope() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ id, input }: { id: string; input: DirectorySyncScopeDto }) => api.put<DirectorySyncScopeDto>(endpoints.directoryIntegrations.scope(id), input), onSuccess: (data, vars) => client.setQueryData(keys.scope(vars.id), data) });
}
export function useDirectoryFieldMappings(connectionId: string | null, enabled = true) {
  return useQuery({ queryKey: connectionId ? keys.fieldMappings(connectionId) : ["directory-integration", "field-mappings", "none"], queryFn: () => api.get<DirectoryFieldMappingDto[]>(endpoints.directoryIntegrations.fieldMappings(connectionId!)), enabled: enabled && Boolean(connectionId), staleTime: 0 });
}
export function useSaveDirectoryFieldMappings() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ id, mappings }: { id: string; mappings: DirectoryFieldMappingDto[] }) => api.put<DirectoryFieldMappingDto[]>(endpoints.directoryIntegrations.fieldMappings(id), { mappings }), onSuccess: (data, vars) => client.setQueryData(keys.fieldMappings(vars.id), data) });
}
export function useDirectoryGroups(connectionId: string | null, enabled = true, search = "", page = 1) {
  return useQuery({ queryKey: ["directory-integration", "groups", connectionId, search, page], queryFn: () => api.get<{ items: DirectoryGroupDto[]; page: number; pageSize: number; hasMore: boolean }>(endpoints.directoryIntegrations.groups(connectionId!), { page, pageSize: 50, search: search || undefined }), enabled: enabled && Boolean(connectionId), staleTime: 60_000 });
}
export function useDirectoryGroupMappings(connectionId: string | null, enabled = true) {
  return useQuery({ queryKey: connectionId ? keys.groupMappings(connectionId) : ["directory-integration", "group-mappings", "none"], queryFn: () => api.get<DirectoryGroupMappingDto[]>(endpoints.directoryIntegrations.groupMappings(connectionId!)), enabled: enabled && Boolean(connectionId), staleTime: 0 });
}
export function useSaveDirectoryGroupMappings() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ id, mappings }: { id: string; mappings: DirectoryGroupMappingDto[] }) => api.put<DirectoryGroupMappingDto[]>(endpoints.directoryIntegrations.groupMappings(id), { mappings }), onSuccess: (data, vars) => client.setQueryData(keys.groupMappings(vars.id), data) });
}
export function useDirectoryOrganizationMappings(connectionId: string | null, enabled = true) {
  return useQuery({ queryKey: connectionId ? keys.organizationMappings(connectionId) : ["directory-integration", "organization-mappings", "none"], queryFn: () => api.get<DirectoryOrganizationMappingDto[]>(endpoints.directoryIntegrations.organizationMappings(connectionId!)), enabled: enabled && Boolean(connectionId), staleTime: 0 });
}
export function useSaveDirectoryOrganizationMappings() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ id, mappings }: { id: string; mappings: DirectoryOrganizationMappingDto[] }) => api.put<DirectoryOrganizationMappingDto[]>(endpoints.directoryIntegrations.organizationMappings(id), { mappings }), onSuccess: (data, vars) => client.setQueryData(keys.organizationMappings(vars.id), data) });
}
export function useDirectoryPolicy(connectionId: string | null, enabled = true) {
  return useQuery({ queryKey: connectionId ? keys.policy(connectionId) : ["directory-integration", "provisioning-policy", "none"], queryFn: () => api.get<DirectoryProvisioningPolicyDto>(endpoints.directoryIntegrations.provisioningPolicy(connectionId!)), enabled: enabled && Boolean(connectionId), staleTime: 0 });
}
export function useSaveDirectoryPolicy() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ id, input }: { id: string; input: DirectoryProvisioningPolicyDto }) => api.put<DirectoryProvisioningPolicyDto>(endpoints.directoryIntegrations.provisioningPolicy(id), input), onSuccess: (data, vars) => client.setQueryData(keys.policy(vars.id), data) });
}
export function useDirectoryPreview(connectionId: string | null, itemType: "Users" | "Groups", page: number, pageSize: number, search: string, enabled = true) {
  return useQuery({ queryKey: ["directory-integration", "preview", connectionId, itemType, page, pageSize, search], queryFn: () => api.get<DirectoryPreviewResultDto>(endpoints.directoryIntegrations.preview(connectionId!, itemType), { page, pageSize, search: search || undefined }), enabled: enabled && Boolean(connectionId), staleTime: 0 });
}
export function useExecuteDirectorySync() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, items }: { id: string; items: SelectedDirectorySyncItem[] }) => api.post<{ run: DirectorySyncRunDto; items: DirectorySyncItemDto[] }>(endpoints.directoryIntegrations.sync(id), { items }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        client.invalidateQueries({ queryKey: ["directory-integration", "preview", variables.id] }),
        client.invalidateQueries({ queryKey: keys.history }),
        client.invalidateQueries({ queryKey: keys.connections }),
      ]);
    },
  });
}
export function useDirectoryHistory(enabled = true) {
  return useQuery({ queryKey: keys.history, queryFn: () => api.get<DirectorySyncRunDto[]>(endpoints.directoryIntegrations.history, { page: 1, pageSize: 50 }), enabled, staleTime: 0 });
}
export function useDirectoryHistoryDetail(runId: string | null, enabled = true) {
  return useQuery({ queryKey: ["directory-integration", "history-detail", runId], queryFn: () => api.get<DirectorySyncRunDetailDto>(endpoints.directoryIntegrations.historyDetail(runId!)), enabled: enabled && Boolean(runId), staleTime: 0 });
}
export function useDirectoryReferenceData(enabled = true) {
  const units = useQuery({ queryKey: ["directory-integration", "reference", "units"], queryFn: () => api.get<DirectoryReferenceOptionDto[]>(endpoints.directoryIntegrations.referenceUnits), enabled, staleTime: 60_000 });
  const calendars = useQuery({ queryKey: ["directory-integration", "reference", "calendars"], queryFn: () => api.get<DirectoryReferenceOptionDto[]>(endpoints.directoryIntegrations.referenceCalendars), enabled, staleTime: 60_000 });
  const roles = useQuery({ queryKey: ["directory-integration", "reference", "roles"], queryFn: () => api.get<DirectoryRoleOptionDto[]>(endpoints.users.roles), enabled, staleTime: 60_000 });
  return { units, calendars, roles };
}
