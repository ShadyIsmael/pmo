import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface OrganizationUnit { id: string; code: string; name: string; typeCode: string; typeNameEn?: string; typeNameAr?: string; description?: string; parentOrganizationalUnitId?: string | null; parentName?: string; isActive: boolean; sortOrder: number; childCount: number; currentMemberCount: number; }
export interface OrganizationUnitType { id: string; code: string; nameEn: string; nameAr: string; displayOrder: number; isActive: boolean; }
export interface OrganizationMembership { id: string; resourceId: string; resourceName: string; organizationalUnitId: string; organizationalUnitName: string; isPrimary: boolean; effectiveFrom: string; effectiveTo?: string; isActive: boolean; }
export interface OrganizationReportingLine { id: string; resourceId: string; resourceName: string; managerResourceId: string; managerResourceName: string; reportingType: string; isPrimary: boolean; effectiveFrom: string; effectiveTo?: string; isActive: boolean; }

export const organizationKeys = {
  all: ["organization"] as const,
  units: ["organization", "units"] as const,
  types: ["organization", "types"] as const,
  memberships: ["organization", "memberships"] as const,
  reporting: ["organization", "reporting"] as const,
};

export function useOrganizationUnits(enabled = true) {
  return useQuery({ queryKey: organizationKeys.units, queryFn: () => api.get<OrganizationUnit[]>(endpoints.organization.units, { includeInactive: true }), enabled, staleTime: 30_000 });
}
export function useOrganizationTypes(enabled = true) {
  return useQuery({ queryKey: organizationKeys.types, queryFn: () => api.get<OrganizationUnitType[]>(endpoints.organization.types), enabled, staleTime: 300_000 });
}
export function useOrganizationMemberships(enabled = true) {
  return useQuery({ queryKey: organizationKeys.memberships, queryFn: () => api.get<OrganizationMembership[]>(endpoints.organization.memberships), enabled, staleTime: 30_000 });
}
export function useOrganizationReporting(enabled = true) {
  return useQuery({ queryKey: organizationKeys.reporting, queryFn: () => api.get<OrganizationReportingLine[]>(endpoints.organization.reportingLines), enabled, staleTime: 30_000 });
}

export function useInvalidateOrganization() {
  const client = useQueryClient();
  return () => { void client.invalidateQueries({ queryKey: organizationKeys.all }); };
}

export function useCreateUnit() {
  const invalidate = useInvalidateOrganization();
  return useMutation({ mutationFn: (body: Record<string, unknown>) => api.post<OrganizationUnit>(endpoints.organization.units, body), onSuccess: () => invalidate() });
}
export function useUpdateUnit(invalidateOnSuccess = true) {
  const invalidate = useInvalidateOrganization();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: Record<string, unknown> }) => api.put<OrganizationUnit>(endpoints.organization.unit(id), body),
    onSuccess: () => { if (invalidateOnSuccess) invalidate(); },
  });
}
export function useDeleteUnit() {
  const invalidate = useInvalidateOrganization();
  return useMutation({ mutationFn: (id: string) => api.delete<void>(endpoints.organization.unit(id)), onSuccess: () => invalidate() });
}
export function useChangeUnitStatus() {
  const invalidate = useInvalidateOrganization();
  return useMutation({
    mutationFn: ({ unit, active }: { unit: OrganizationUnit; active: boolean }) =>
      api.post<void>(active ? endpoints.organization.reactivateUnit(unit.id) : endpoints.organization.deactivateUnit(unit.id)),
    onSuccess: () => invalidate(),
  });
}
