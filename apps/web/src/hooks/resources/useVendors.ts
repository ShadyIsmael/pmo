"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";
import { vendorKeys, type VendorStatusFilter } from "@/lib/api/resources";

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

async function fetchVendors(search?: string, categoryId?: string, status: VendorStatusFilter = "all"): Promise<VendorDto[]> {
  const data = await api.get<{ items: VendorDto[] }>(endpoints.vendors.root, {
    pageSize: 200,
    search: search || undefined,
    categoryId: categoryId || undefined,
    isActive: status === "active" ? true : status === "inactive" ? false : undefined,
  });
  return data.items ?? [];
}

export function useVendors(search?: string, categoryId?: string, status: VendorStatusFilter = "all") {
  return useQuery({
    queryKey: vendorKeys.list(search, categoryId, status),
    queryFn: () => fetchVendors(search, categoryId, status),
    staleTime: 60_000,
  });
}

/** Active-only vendor lookup for NEW relationships (selectors). Management listing uses useVendors (all by default). */
export function useActiveVendors(search?: string) {
  return useVendors(search, undefined, "active");
}

export function useVendor(id: string | null | undefined) {
  return useQuery({
    queryKey: vendorKeys.detail(id ?? ""),
    queryFn: () => api.get<VendorDto>(endpoints.vendors.detail(id!)),
    enabled: Boolean(id),
    staleTime: 60_000,
  });
}

export function useCreateVendor() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateVendorInput) => api.post<VendorDto>(endpoints.vendors.root, input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: vendorKeys.all });
    },
  });
}

export function useUpdateVendor() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateVendorInput }) =>
      api.put<VendorDto>(endpoints.vendors.detail(id), input),
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({ queryKey: vendorKeys.all });
      void queryClient.invalidateQueries({ queryKey: vendorKeys.detail(variables.id) });
    },
  });
}

export function useDeleteVendor() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.vendors.detail(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: vendorKeys.all });
    },
  });
}

export function useDeactivateVendor() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.put<VendorDto>(endpoints.vendors.deactivate(id), {}),
    onSuccess: (_data, id) => {
      void queryClient.invalidateQueries({ queryKey: vendorKeys.all });
      void queryClient.invalidateQueries({ queryKey: vendorKeys.detail(id) });
    },
  });
}

export function useReactivateVendor() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.put<VendorDto>(endpoints.vendors.reactivate(id), {}),
    onSuccess: (_data, id) => {
      void queryClient.invalidateQueries({ queryKey: vendorKeys.all });
      void queryClient.invalidateQueries({ queryKey: vendorKeys.detail(id) });
    },
  });
}
