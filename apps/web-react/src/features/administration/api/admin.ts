import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface AdminUserDto { id: string; email: string; fullName: string; isActive: boolean; roles: { id: string; name: string }[]; }
export interface RoleDto { id: string; name: string; description?: string; permissions: string[]; }
export interface CurrencyDto { id: string; code: string; nameEn: string; nameAr: string; isActive: boolean; }
export interface ReferenceItem { id: string; code: string; nameEn: string; nameAr: string; isActive: boolean; displayOrder: number; }

export function useAdminUsers(enabled = true) {
  return useQuery({ queryKey: ["admin", "users"], queryFn: () => api.get<{ items: AdminUserDto[] }>(endpoints.users.root, { pageSize: 200 }), enabled, staleTime: 30_000 });
}
export function useRoles(enabled = true) {
  return useQuery({ queryKey: ["admin", "roles"], queryFn: () => api.get<RoleDto[]>(endpoints.users.roles), enabled, staleTime: 60_000 });
}
export function useCurrencies(enabled = true) {
  return useQuery({ queryKey: ["admin", "currencies"], queryFn: () => api.get<CurrencyDto[]>(endpoints.currencies.root), enabled, staleTime: 300_000 });
}
export function useVendorCategories(enabled = true) {
  return useQuery({ queryKey: ["reference", "vendor-categories"], queryFn: () => api.get<ReferenceItem[]>(endpoints.referenceData.vendorCategories), enabled, staleTime: 300_000 });
}
export function useWorkItemStatuses(enabled = true) {
  return useQuery({ queryKey: ["reference", "work-item-statuses"], queryFn: () => api.get<ReferenceItem[]>(endpoints.referenceData.workItemStatuses), enabled, staleTime: 300_000 });
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
