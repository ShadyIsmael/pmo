import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@api/client";

export interface VendorDto {
  id: string;
  name: string;
  code?: string;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  category?: string;
  vendorCategoryId?: string;
  categoryName?: string;
  status?: string;
  contractValue?: number;
  contractStart?: string;
  contractEnd?: string;
  isActive: boolean;
}

export interface CreateVendorInput {
  name: string;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  vendorCategoryId: string;
  status?: string;
  contractValue?: number;
  contractStart?: string;
  contractEnd?: string;
}

export interface UpdateVendorInput {
  name?: string;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  vendorCategoryId?: string;
  status?: string;
  contractValue?: number;
  contractStart?: string;
  contractEnd?: string;
  isActive?: boolean;
}

export interface VendorCategoryDto {
  id: string;
  code: string;
  name: string;
  nameEn: string;
  nameAr: string;
  isActive: boolean;
}

const vendorKeysAll = ["vendors"] as const;
const vendorKeysLists = () => [...vendorKeysAll, "list"] as const;

const vendorKeys = {
  all: vendorKeysAll,
  lists: vendorKeysLists,
  list: (search?: string, categoryId?: string, status?: string) => [...vendorKeysLists(), search, categoryId, status] as const,
  detail: (id: string) => [...vendorKeysAll, "detail", id] as const,
  categories: [...vendorKeysAll, "categories"] as const,
};

export type VendorStatusFilter = "all" | "active" | "inactive";

export function useVendors(search?: string, categoryId?: string, status: VendorStatusFilter = "all", enabled = true) {
  return useQuery({
    queryKey: vendorKeys.list(search, categoryId, status),
    queryFn: () => api.get<{ items: VendorDto[] }>("/api/vendors", {
      pageSize: 200,
      search: search || undefined,
      categoryId: categoryId || undefined,
      isActive: status === "active" ? true : status === "inactive" ? false : undefined,
    }),
    staleTime: 60_000,
    enabled,
  });
}

export function useActiveVendors(search?: string) {
  return useVendors(search, undefined, "active");
}

export function useVendor(id: string | null | undefined) {
  return useQuery({
    queryKey: vendorKeys.detail(id ?? ""),
    queryFn: () => api.get<VendorDto>(`/api/vendors/${id!}`),
    enabled: Boolean(id),
    staleTime: 60_000,
  });
}

export function useVendorCategories(enabled = true) {
  return useQuery({
    queryKey: vendorKeys.categories,
    queryFn: () => api.get<VendorCategoryDto[]>("/api/reference-data/vendor-categories"),
    enabled,
    staleTime: 60_000,
  });
}

export function useCreateVendor() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateVendorInput) => api.post<VendorDto>("/api/vendors", input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: vendorKeys.lists() });
    },
  });
}

export function useUpdateVendor() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateVendorInput }) =>
      api.put<VendorDto>(`/api/vendors/${id}`, input),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: vendorKeys.lists() });
      queryClient.invalidateQueries({ queryKey: vendorKeys.detail(variables.id) });
    },
  });
}

export function useDeactivateVendor() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.put<VendorDto>(`/api/vendors/${id}/deactivate`, {}),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: vendorKeys.lists() });
      queryClient.invalidateQueries({ queryKey: vendorKeys.detail(id) });
    },
  });
}

export function useReactivateVendor() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.put<VendorDto>(`/api/vendors/${id}/reactivate`, {}),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: vendorKeys.lists() });
      queryClient.invalidateQueries({ queryKey: vendorKeys.detail(id) });
    },
  });
}

export function useDeleteVendor() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(`/api/vendors/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: vendorKeys.lists() });
    },
  });
}
