import { useQuery } from "@tanstack/react-query";
import { api } from "@api/client";
import { endpoints } from "@api/endpoints";

export interface UserLookupDto {
  id: string;
  fullName: string;
  email: string;
  jobTitle?: string;
}

export interface WorkflowUserLookupDto {
  id: string;
  fullName: string;
  email?: string;
}

export interface UserSelectOption {
  value: string;
  label: string;
  keywords: string[];
}

export function toUserSelectOptions(users: Array<UserLookupDto | WorkflowUserLookupDto> | undefined): UserSelectOption[] {
  return (users ?? []).map((u) => ({
    value: u.id,
    label: "email" in u && u.email ? `${u.fullName} · ${u.email}` : u.fullName,
    keywords: ["jobTitle" in u ? u.jobTitle ?? "" : ""],
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

export function useProjectWorkflowUserLookup(enabled = true) {
  return useQuery({
    queryKey: ["workflow-lookups", "project", "users", "all"],
    queryFn: () => api.get<WorkflowUserLookupDto[]>(endpoints.users.projectWorkflowLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function useProjectManagerCandidateLookup(enabled = true) {
  return useQuery({
    queryKey: ["workflow-lookups", "project", "managers"],
    queryFn: () => api.get<WorkflowUserLookupDto[]>(endpoints.users.projectManagerWorkflowLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function useMeetingWorkflowUserLookup(enabled = true) {
  return useQuery({
    queryKey: ["workflow-lookups", "meeting", "users"],
    queryFn: () => api.get<WorkflowUserLookupDto[]>(endpoints.users.meetingWorkflowLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function useWorkItemWorkflowUserLookup(enabled = true) {
  return useQuery({
    queryKey: ["workflow-lookups", "work-items", "users"],
    queryFn: () => api.get<WorkflowUserLookupDto[]>(endpoints.users.workItemWorkflowLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function useRiskWorkflowUserLookup(enabled = true) {
  return useQuery({
    queryKey: ["workflow-lookups", "risks", "users"],
    queryFn: () => api.get<WorkflowUserLookupDto[]>(endpoints.users.riskWorkflowLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function useResourceWorkflowUserLookup(enabled = true, resourceId?: string) {
  return useQuery({
    queryKey: ["workflow-lookups", "resources", "users", resourceId ?? "all"],
    queryFn: () => resourceId
      ? api.get<WorkflowUserLookupDto[]>(endpoints.users.resourceWorkflowLookup, { resourceId })
      : api.get<WorkflowUserLookupDto[]>(endpoints.users.resourceWorkflowLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function useManagerAssignmentUserLookup(role?: string, enabled = true) {
  return useQuery({
    queryKey: ["workflow-lookups", "handover", "manager-candidates", role ?? "all"],
    queryFn: () => api.get<WorkflowUserLookupDto[]>(
      endpoints.users.managerAssignmentWorkflowLookup,
      role ? { role } : undefined,
    ),
    staleTime: 60_000,
    enabled,
  });
}

export function usePortfolioWorkflowManagerLookup(enabled = true) {
  return useQuery({
    queryKey: ["workflow-lookups", "portfolio", "managers"],
    queryFn: () => api.get<WorkflowUserLookupDto[]>(endpoints.users.portfolioManagerWorkflowLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function useProgramWorkflowManagerLookup(enabled = true) {
  return useQuery({
    queryKey: ["workflow-lookups", "program", "managers"],
    queryFn: () => api.get<WorkflowUserLookupDto[]>(endpoints.users.programManagerWorkflowLookup),
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

export interface ProjectCurrencyLookupDto {
  id: string;
  code: string;
  name: string;
  symbol: string;
  isBase: boolean;
}

export function usePortfolioLookup(enabled = true, scopeKey = "") {
  return useQuery({
    queryKey: ["portfolios", "lookup", scopeKey],
    queryFn: () => api.get<{ items: PortfolioLookupDto[] }>(endpoints.portfolios.projectLookup, { pageSize: 200 }),
    staleTime: 60_000,
    enabled,
  });
}

export function useProgramLookup(enabled = true, portfolioId = "") {
  return useQuery({
    queryKey: ["programs", "lookup", portfolioId],
    queryFn: () => api.get<{ items: ProgramLookupDto[] }>(endpoints.programs.projectLookup, { pageSize: 200, ...(portfolioId ? { portfolioId } : {}) }),
    staleTime: 60_000,
    enabled,
  });
}

export function useDepartmentLookup(enabled = true) {
  return useQuery({
    queryKey: ["departments", "lookup"],
    queryFn: () => api.get<DepartmentLookupDto[]>(endpoints.organization.projectUnitLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function useProjectCurrencyLookup(enabled = true) {
  return useQuery({
    queryKey: ["currencies", "project-lookup"],
    queryFn: () => api.get<ProjectCurrencyLookupDto[]>(endpoints.currencies.projectLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function useWorkflowCurrencyLookup(enabled = true) {
  return useQuery({
    queryKey: ["currencies", "workflow-lookup"],
    queryFn: () => api.get<ProjectCurrencyLookupDto[]>(endpoints.currencies.workflowLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function useResourceUnitLookup(enabled = true) {
  return useQuery({
    queryKey: ["organization", "units", "resource-lookup"],
    queryFn: () => api.get<DepartmentLookupDto[]>(endpoints.organization.resourceUnitLookup),
    staleTime: 60_000,
    enabled,
  });
}

export function shouldLoadProjectLookup(open: boolean): boolean {
  return open;
}
