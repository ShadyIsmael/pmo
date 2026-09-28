"use client";

import { useQuery } from "@tanstack/react-query";
import { api, type PagedResult } from "@/lib/api-client";
import { endpoints } from "@/lib/api/endpoints";
import { userKeys } from "@/lib/api/users";

export interface UserLookupDto {
  id: string;
  fullName: string;
  email: string;
  jobTitle?: string;
}

export interface DepartmentDto {
  id: string;
  name: string;
  code?: string;
  description?: string;
}

export interface PortfolioDto {
  id: string;
  name: string;
  code?: string;
  description?: string;
  status?: string;
  currency?: string;
  health?: string;
  priority?: string;
}

export interface ProgramDto {
  id: string;
  name: string;
  code?: string;
  description?: string;
  portfolioId: string;
  status?: string;
  health?: string;
  currency?: string;
}

export function useUserLookup(role?: string, enabled = true) {
  return useQuery({
    queryKey: userKeys.lookup(role),
    queryFn: () => api.get<UserLookupDto[]>(endpoints.users.lookup, role ? { role } : undefined),
    staleTime: 60_000,
    enabled,
  });
}

export interface UserSelectOption {
  value: string;
  label: string;
  keywords: string[];
}

export function toUserSelectOptions(users: UserLookupDto[] | undefined): UserSelectOption[] {
  return (users ?? []).map((u) => ({
    value: u.id,
    label: `${u.fullName} · ${u.email}`,
    keywords: [u.jobTitle ?? ""],
  }));
}

export function usePortfolioLookup() {
  return useQuery({
    queryKey: ["portfolios", "lookup"],
    queryFn: () => api.get<PagedResult<PortfolioDto>>(endpoints.portfolios.root, { pageSize: 200, status: "Active" }),
    staleTime: 60_000,
  });
}

export function useProgramLookup(portfolioId?: string) {
  return useQuery({
    queryKey: ["programs", "lookup", portfolioId],
    queryFn: () =>
      api.get<PagedResult<ProgramDto>>(endpoints.programs.root, {
        portfolioId: portfolioId || undefined,
        pageSize: 200,
      }),
    staleTime: 60_000,
  });
}

export function useDepartmentLookup() {
  return useQuery({
    queryKey: ["departments", "lookup"],
    queryFn: () => api.get<PagedResult<DepartmentDto>>(endpoints.departments, { pageSize: 200 }),
    staleTime: 60_000,
  });
}
