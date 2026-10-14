import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface UserLookupDto {
  id: string;
  fullName: string;
  email: string;
  jobTitle?: string;
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

export function mergeUserLookups(...lookups: Array<UserLookupDto[] | undefined>): UserLookupDto[] {
  const usersById = new Map<string, UserLookupDto>();
  for (const user of lookups.flatMap((items) => items ?? [])) {
    usersById.set(user.id, user);
  }
  return Array.from(usersById.values());
}

export function useUserLookup(role?: string, enabled = true) {
  return useQuery({
    queryKey: ["users", "lookup", role ?? "all"],
    queryFn: () => api.get<UserLookupDto[]>(endpoints.users.lookup, role ? { role } : undefined),
    staleTime: 60_000,
    enabled,
  });
}

export interface PortfolioLookupDto {
  id: string;
  name: string;
  code?: string;
  status?: string;
  currency?: string;
}

export interface ProgramLookupDto {
  id: string;
  name: string;
  portfolioId: string;
  currency?: string;
}

export interface DepartmentLookupDto {
  id: string;
  name: string;
}

export function usePortfolioLookup(enabled = true) {
  return useQuery({
    queryKey: ["portfolios", "lookup"],
    queryFn: () => api.get<{ items: PortfolioLookupDto[] }>(endpoints.portfolios.root, { pageSize: 200 }),
    staleTime: 60_000,
    enabled,
  });
}

export function useProgramLookup(enabled = true) {
  return useQuery({
    queryKey: ["programs", "lookup"],
    queryFn: () => api.get<{ items: ProgramLookupDto[] }>(endpoints.programs.root, { pageSize: 200 }),
    staleTime: 60_000,
    enabled,
  });
}

export function useDepartmentLookup(enabled = true) {
  return useQuery({
    queryKey: ["departments", "lookup"],
    queryFn: () => api.get<{ items: DepartmentLookupDto[] }>("/api/organization/units", { pageSize: 200 }),
    staleTime: 60_000,
    enabled,
  });
}

export function shouldLoadProjectLookup(open: boolean): boolean {
  return open;
}
