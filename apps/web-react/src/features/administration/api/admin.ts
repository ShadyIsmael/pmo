import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type AuthenticationSource } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface AdminUserDto { id: string; email: string; firstName: string; lastName: string; fullName: string; jobTitle?: string | null; phone?: string | null; isActive: boolean; authenticationSource: AuthenticationSource; mustChangePassword: boolean; temporaryPasswordExpiresAt?: string | null; roles: string[]; }
export interface UpdateAdminUserInput { email: string; firstName: string; lastName: string; jobTitle?: string | null; phone?: string | null; isActive: boolean; roles: string[]; }
export interface CreateAdminUserInput extends UpdateAdminUserInput { authenticationSource: AuthenticationSource; }
export interface CreateAdminUserResponse { user: AdminUserDto; temporaryPassword?: string | null; }
export interface TemporaryPasswordResetDto { userId: string; temporaryPassword: string; expiresAt: string; }
export interface RoleDto { id: string; name: string; description?: string; category?: string; }
export interface ManagedRoleSummaryDto extends RoleDto {
  isSystem: boolean;
  permissionCount: number;
  assignedUserCount: number;
}
export interface PermissionCatalogItemDto { id: string; name: string; resource: string; action: string; description?: string; }
export interface ManagedRoleDto extends ManagedRoleSummaryDto { permissions: PermissionCatalogItemDto[]; }
export interface SaveRoleInput { name: string; description: string; category: string; permissionIds: string[]; }
export type PermissionOverrideEffect = "Inherit" | "Allow" | "Deny";
export interface UserPermissionAssignmentDto {
  id: string;
  name: string;
  resource: string;
  action: string;
  description?: string;
  isRoleGranted: boolean;
  overrideEffect: Exclude<PermissionOverrideEffect, "Inherit"> | null;
  isEffective: boolean;
}
export interface CurrencyDto { id: string; code: string; name: string; symbol: string; exchangeRate: number; isBase: boolean; }
export interface SaveCurrencyInput { code: string; name: string; symbol: string; isBase: boolean; }
export interface ReferenceItem { id: string; code: string; nameEn: string; nameAr: string; isActive: boolean; displayOrder: number; }
export interface WorkItemStatusDto extends ReferenceItem { color?: string; isDefault: boolean; }
export interface SaveWorkItemStatusInput { code: string; nameEn: string; nameAr: string; displayOrder: number; isActive: boolean; color: string; isDefault: boolean; }
export interface VendorCategoryDto extends ReferenceItem { description?: string; color?: string; }
export interface SaveVendorCategoryInput { code: string; nameEn: string; nameAr: string; description?: string; displayOrder: number; isActive: boolean; color?: string; }

export function useAdminUsers(enabled = true, options: { page: number; pageSize: number; search: string }) {
  return useQuery({ queryKey: ["admin", "users", options], queryFn: () => api.get<{ items: AdminUserDto[]; page: number; pageSize: number; totalCount: number; totalPages: number }>(endpoints.users.root, options), enabled, staleTime: 30_000 });
}
export function useCreateAdminUser() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateAdminUserInput) => api.post<CreateAdminUserResponse>(endpoints.users.root, input),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ["admin", "users"] });
      void client.invalidateQueries({ queryKey: ["admin", "roles", "manage"] });
    },
  });
}
export function useResetAdminUserPassword() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => api.post<TemporaryPasswordResetDto>(endpoints.users.resetPassword(userId)),
    onSuccess: () => void client.invalidateQueries({ queryKey: ["admin", "users"] }),
  });
}
export function useUpdateAdminUser() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, input }: { userId: string; input: UpdateAdminUserInput }) => api.put<AdminUserDto>(endpoints.users.detail(userId), input),
    onSuccess: (_user, variables) => {
      void client.invalidateQueries({ queryKey: ["admin", "users"] });
      void client.invalidateQueries({ queryKey: ["admin", "user-permissions", variables.userId] });
      void client.invalidateQueries({ queryKey: ["admin", "roles", "manage"] });
    },
  });
}
export function useRoles(enabled = true) {
  return useQuery({ queryKey: ["admin", "roles"], queryFn: () => api.get<RoleDto[]>(endpoints.users.roles), enabled, staleTime: 60_000 });
}
export function useManagedRoles(enabled = true) {
  return useQuery({ queryKey: ["admin", "roles", "manage"], queryFn: () => api.get<ManagedRoleSummaryDto[]>(endpoints.roleManagement.manageable), enabled, staleTime: 30_000 });
}
export function useAssignableRoles(enabled = true) {
  return useManagedRoles(enabled);
}
export function useManagedRole(roleId: string | null, enabled = true) {
  return useQuery({
    queryKey: ["admin", "roles", "detail", roleId],
    queryFn: () => api.get<ManagedRoleDto>(endpoints.roleManagement.detail(roleId!)),
    enabled: enabled && Boolean(roleId),
    staleTime: 0,
  });
}
export function useAssignablePermissions(enabled = true) {
  return useQuery({ queryKey: ["admin", "roles", "permission-catalog"], queryFn: () => api.get<PermissionCatalogItemDto[]>(endpoints.roleManagement.permissionCatalog), enabled, staleTime: 60_000 });
}
export function useSaveManagedRole() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ roleId, input }: { roleId: string | null; input: SaveRoleInput }) => roleId
      ? api.put<ManagedRoleDto>(endpoints.roleManagement.detail(roleId), input)
      : api.post<ManagedRoleDto>(endpoints.roleManagement.root, input),
    onSuccess: (role, variables) => {
      client.setQueryData(["admin", "roles", "detail", role.id], role);
      void client.invalidateQueries({ queryKey: ["admin", "roles", "manage"] });
      void client.invalidateQueries({ queryKey: ["admin", "roles"] });
      if (variables.roleId) void client.invalidateQueries({ queryKey: ["admin", "roles", "detail", variables.roleId] });
    },
  });
}
export function useDeleteManagedRole() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (roleId: string) => api.delete<void>(endpoints.roleManagement.detail(roleId)),
    onSuccess: (_result, roleId) => {
      client.removeQueries({ queryKey: ["admin", "roles", "detail", roleId] });
      void client.invalidateQueries({ queryKey: ["admin", "roles", "manage"] });
      void client.invalidateQueries({ queryKey: ["admin", "roles"] });
    },
  });
}
export function useUserPermissions(userId: string | null, enabled = true) {
  return useQuery({
    queryKey: ["admin", "user-permissions", userId],
    queryFn: () => api.get<UserPermissionAssignmentDto[]>(endpoints.users.permissions(userId!)),
    enabled: enabled && Boolean(userId),
    staleTime: 0,
  });
}
export function useSaveUserPermissions() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, overrides }: { userId: string; overrides: { permissionId: string; effect: "Allow" | "Deny" }[] }) =>
      api.put<UserPermissionAssignmentDto[]>(endpoints.users.permissions(userId), { overrides }),
    onSuccess: (permissions, variables) => {
      client.setQueryData(["admin", "user-permissions", variables.userId], permissions);
      void client.invalidateQueries({ queryKey: ["admin", "users"] });
    },
  });
}
export function toUserPermissionOverrides(draft: Record<string, PermissionOverrideEffect>) {
  return Object.entries(draft)
    .filter(([, effect]) => effect !== "Inherit")
    .map(([permissionId, effect]) => ({ permissionId, effect: effect as "Allow" | "Deny" }));
}
export function useCurrencies(enabled = true) {
  return useQuery({ queryKey: ["admin", "currencies"], queryFn: () => api.get<CurrencyDto[]>(endpoints.currencies.root), enabled, staleTime: 300_000 });
}
export function useCreateCurrency() {
  const client = useQueryClient();
  return useMutation({ mutationFn: (input: SaveCurrencyInput) => api.post<CurrencyDto>(endpoints.currencies.root, input), onSuccess: () => { void client.invalidateQueries({ queryKey: ["admin", "currencies"] }); } });
}
export function useUpdateCurrency() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ id, input }: { id: string; input: SaveCurrencyInput }) => api.put<CurrencyDto>(endpoints.currencies.detail(id), input), onSuccess: () => { void client.invalidateQueries({ queryKey: ["admin", "currencies"] }); } });
}
export function useDeleteCurrency() {
  const client = useQueryClient();
  return useMutation({ mutationFn: (id: string) => api.delete<void>(endpoints.currencies.detail(id)), onSuccess: () => { void client.invalidateQueries({ queryKey: ["admin", "currencies"] }); } });
}
export function useVendorCategories(enabled = true) {
  return useQuery({ queryKey: ["reference", "vendor-categories"], queryFn: () => api.get<VendorCategoryDto[]>(endpoints.referenceData.vendorCategories), enabled, staleTime: 300_000 });
}
export function useCreateVendorCategory() {
  const client = useQueryClient();
  return useMutation({ mutationFn: (input: SaveVendorCategoryInput) => api.post<VendorCategoryDto>(endpoints.referenceData.vendorCategories, input), onSuccess: () => { void client.invalidateQueries({ queryKey: ["reference", "vendor-categories"] }); } });
}
export function useUpdateVendorCategory() {
  const client = useQueryClient();
  return useMutation({ mutationFn: ({ id, input }: { id: string; input: SaveVendorCategoryInput }) => api.put<VendorCategoryDto>(`${endpoints.referenceData.vendorCategories}/${encodeURIComponent(id)}`, input), onSuccess: () => { void client.invalidateQueries({ queryKey: ["reference", "vendor-categories"] }); } });
}
export function useWorkItemStatuses(enabled = true) {
  return useQuery({ queryKey: ["reference", "work-item-statuses"], queryFn: () => api.get<WorkItemStatusDto[]>(endpoints.referenceData.workItemStatuses, { includeInactive: true }), enabled, staleTime: 300_000 });
}
export function useCreateWorkItemStatus() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: SaveWorkItemStatusInput) => api.post<WorkItemStatusDto>(endpoints.referenceData.workItemStatuses, input),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ["reference", "work-item-statuses"] });
      void client.invalidateQueries({ queryKey: ["workItems", "statuses"] });
    },
  });
}
export function useUpdateWorkItemStatus() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: SaveWorkItemStatusInput }) => api.put<WorkItemStatusDto>(`${endpoints.referenceData.workItemStatuses}/${encodeURIComponent(id)}`, input),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ["reference", "work-item-statuses"] });
      void client.invalidateQueries({ queryKey: ["workItems", "statuses"] });
    },
  });
}
export function useProjectRoles(enabled = true) {
  return useQuery({ queryKey: ["reference", "project-roles"], queryFn: () => api.get<ReferenceItem[]>(endpoints.referenceData.projectRoles), enabled, staleTime: 300_000 });
}
export function usePlatformSettings(enabled = true) {
  return useQuery({ queryKey: ["admin", "settings"], queryFn: () => api.get<Record<string, string>>("/api/settings"), enabled, staleTime: 60_000 });
}
export function useSavePlatformSettings() {
  const client = useQueryClient();
  return useMutation({ mutationFn: (body: Record<string, string>) => api.put<Record<string, string>>("/api/settings", body), onSuccess: () => { void client.invalidateQueries({ queryKey: ["admin", "settings"] }); } });
}
