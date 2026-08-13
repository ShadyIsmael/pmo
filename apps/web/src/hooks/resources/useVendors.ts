"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api-client";

export interface VendorDto {
  id: string;
  name: string;
  code?: string;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  category?: string;
  status?: string;
  isActive: boolean;
}

async function fetchVendors(search?: string): Promise<VendorDto[]> {
  const data = await api.get<{ items: VendorDto[] }>("/api/vendors", { pageSize: 200, search });
  return data.items ?? [];
}

export function useVendors(search?: string) {
  return useQuery({
    queryKey: ["vendors", search],
    queryFn: () => fetchVendors(search),
    staleTime: 60_000,
  });
}
