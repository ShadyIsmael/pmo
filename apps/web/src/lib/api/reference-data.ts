import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { endpoints } from "@/lib/api/endpoints";

export interface WorkItemStatusReference {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  displayOrder: number;
  isActive: boolean;
  color?: string;
  isDefault: boolean;
}

export interface SaveWorkItemStatusRequest {
  code: string;
  nameEn: string;
  nameAr: string;
  displayOrder: number;
  isActive: boolean;
  color?: string;
  isDefault: boolean;
}

export const referenceDataKeys = {
  workItemStatuses: (includeInactive = true) => ["reference-data", "work-item-statuses", includeInactive] as const,
  vendorCategories: (activeOnly = false) => ["reference-data", "vendor-categories", activeOnly] as const,
};

export interface VendorCategoryReference {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  description?: string;
  displayOrder: number;
  isActive: boolean;
  color?: string;
}

export interface SaveVendorCategoryRequest {
  code: string;
  nameEn: string;
  nameAr: string;
  displayOrder: number;
  isActive: boolean;
  description?: string;
  color?: string;
}

export const referenceDataApi = {
  workItemStatuses: (includeInactive = true) => api.get<WorkItemStatusReference[]>(endpoints.referenceData.workItemStatuses, { includeInactive }),
  createWorkItemStatus: (body: SaveWorkItemStatusRequest) => api.post<WorkItemStatusReference>(endpoints.referenceData.workItemStatuses, body),
  updateWorkItemStatus: (id: string, body: SaveWorkItemStatusRequest) => api.put<WorkItemStatusReference>(`${endpoints.referenceData.workItemStatuses}/${encodeURIComponent(id)}`, body),
};

export function useWorkItemStatuses(includeInactive = true) {
  return useQuery({
    queryKey: referenceDataKeys.workItemStatuses(includeInactive),
    queryFn: () => referenceDataApi.workItemStatuses(includeInactive),
    staleTime: 60_000,
  });
}

export function useWorkItemStatusMutations() {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["reference-data", "work-item-statuses"] });
  return {
    create: useMutation({ mutationFn: referenceDataApi.createWorkItemStatus, onSuccess: invalidate }),
    update: useMutation({ mutationFn: ({ id, body }: { id: string; body: SaveWorkItemStatusRequest }) => referenceDataApi.updateWorkItemStatus(id, body), onSuccess: invalidate }),
  };
}

export function getWorkItemStatusName(code: string, statuses: readonly WorkItemStatusReference[], locale: string, fallback = code) {
  const status = statuses.find((candidate) => candidate.code === code);
  return status ? (locale === "ar" ? status.nameAr : status.nameEn) : fallback;
}

export async function fetchVendorCategories(activeOnly = false): Promise<VendorCategoryReference[]> {
  return api.get<VendorCategoryReference[]>(endpoints.referenceData.vendorCategories, { activeOnly });
}

export function useVendorCategories(activeOnly = false) {
  return useQuery({
    queryKey: referenceDataKeys.vendorCategories(activeOnly),
    queryFn: () => fetchVendorCategories(activeOnly),
    staleTime: 60_000,
  });
}

export function useVendorCategoryMutations() {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["reference-data", "vendor-categories"] });
  return {
    create: useMutation({
      mutationFn: (body: SaveVendorCategoryRequest) =>
        api.post<VendorCategoryReference>(endpoints.referenceData.vendorCategories, body),
      onSuccess: invalidate,
    }),
    update: useMutation({
      mutationFn: ({ id, body }: { id: string; body: Partial<SaveVendorCategoryRequest> }) =>
        api.put<VendorCategoryReference>(`${endpoints.referenceData.vendorCategories}/${encodeURIComponent(id)}`, body),
      onSuccess: invalidate,
    }),
    deactivate: useMutation({
      mutationFn: (id: string) =>
        api.put<void>(`${endpoints.referenceData.vendorCategories}/${encodeURIComponent(id)}/deactivate`, {}),
      onSuccess: invalidate,
    }),
  };
}
