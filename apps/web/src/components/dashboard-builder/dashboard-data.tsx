"use client";

import * as React from "react";
import { useLocale, useTranslations } from "next-intl";
import { EmptyState, ErrorState, LoadingSkeleton } from "@/components/ui/state-display";
import { useAuth } from "@/lib/auth";
import { useManagementPerspective } from "@/lib/perspective-context";
import { useProjects, type ProjectSummaryDto } from "@/hooks/projects/useProjects";
import { usePortfolios, type PortfolioDto } from "@/hooks/portfolios/usePortfolios";
import { usePrograms, type ProgramDto } from "@/hooks/portfolios/usePrograms";
import { useProgramManagementOverview, type ProgramManagementOverview, type ProgramProject } from "@/hooks/portfolios/useProgramManagement";
import { usePortfolioManagementOverview, type PortfolioManagementOverview, type PortfolioProject } from "@/hooks/portfolios/usePortfolioManagement";
import { useRisks } from "@/hooks/risks/useRisks";
import { useMeetings } from "@/hooks/meetings/useMeetings";
import { useAllocations, type AllocationDto } from "@/hooks/resources/useAllocations";
import { useCapacityMatrix, type CapacityMatrixDto } from "@/hooks/resources/useCapacity";
import { useResources, type ResourceDto } from "@/hooks/resources/useResources";
import { useWorkItems, type WorkItemDto } from "@/lib/api/work-items";

export const CLOSED_PROJECT_STATUSES = new Set(["completed", "cancelled", "closed"]);

export function isClosedProject(status: string | undefined): boolean {
  return CLOSED_PROJECT_STATUSES.has((status ?? "").trim().toLowerCase());
}

export function isOpenRisk(status: string | undefined): boolean {
  const normalized = (status ?? "").trim().toLowerCase();
  return normalized !== "closed" && normalized !== "mitigated";
}

export function projectHealth(project: Pick<ProjectSummaryDto, "health" | "ragStatus">): "green" | "amber" | "red" | "unknown" {
  const value = `${project.health ?? ""} ${project.ragStatus ?? ""}`.toLowerCase();
  if (value.includes("red") || value.includes("critical")) return "red";
  if (value.includes("amber") || value.includes("yellow") || value.includes("warning") || value.includes("at risk")) return "amber";
  if (value.includes("green") || value.includes("healthy") || value.includes("good")) return "green";
  return "unknown";
}

export function groupMoneyByCurrency<T extends { currency: string }>(items: readonly T[], value: (_item: T) => number | undefined): Map<string, number> {
  const totals = new Map<string, number>();
  for (const item of items) {
    const amount = value(item);
    if (typeof amount !== "number" || !Number.isFinite(amount)) continue;
    totals.set(item.currency, (totals.get(item.currency) ?? 0) + amount);
  }
  return totals;
}

export function formatMoney(value: number, currency: string, locale: string): string {
  return new Intl.NumberFormat(locale, { style: "currency", currency, notation: "compact", maximumFractionDigits: 1 }).format(value);
}

export interface DashboardRuntimeValue {
  filters: { dateRange: string; portfolio: string | null; program: string | null; project: string | null };
  pov: string;
}

const DashboardRuntimeContext = React.createContext<DashboardRuntimeValue>({
  filters: { dateRange: "30d", portfolio: null, program: null, project: null },
  pov: "Portfolio Manager",
});

export function DashboardRuntimeProvider({ value, children }: { value: DashboardRuntimeValue; children: React.ReactNode }) {
  return <DashboardRuntimeContext.Provider value={value}>{children}</DashboardRuntimeContext.Provider>;
}

export function useDashboardRuntime(): DashboardRuntimeValue {
  return React.useContext(DashboardRuntimeContext);
}

function useDashboardEnabled(): boolean {
  return useAuth().isAuthenticated;
}

function filterProjects(items: readonly ProjectSummaryDto[], runtime: DashboardRuntimeValue): ProjectSummaryDto[] {
  return items.filter((project) =>
    (!runtime.filters.project || project.id === runtime.filters.project),
  );
}

export type DashboardProject = ProjectSummaryDto & { programId?: string; portfolioId?: string; isDirectProject?: boolean };
type DashboardProjectPage = { items: DashboardProject[]; page: number; pageSize: number; totalCount: number; totalPages: number };

function fromProgramProject(project: ProgramProject): DashboardProject {
  return {
    id: project.id,
    name: project.name,
    code: project.code,
    status: project.status,
    health: project.health || "Unknown",
    ragStatus: project.health || "Unknown",
    progress: project.progress,
    startDate: project.startDate,
    endDate: project.endDate,
    budget: project.budget,
    actualCost: project.actualCost,
    currency: project.currency,
    projectManagerName: project.projectManagerName,
  };
}

function fromPortfolioProject(project: PortfolioProject): DashboardProject {
  return {
    id: project.id,
    name: project.name,
    code: project.code,
    programId: project.programId,
    health: project.health || "Unknown",
    ragStatus: project.health || "Unknown",
    status: project.status,
    progress: project.progress,
    startDate: project.startDate,
    endDate: project.endDate,
    currency: project.currency,
    projectManagerName: project.projectManagerName,
  };
}

function filterScopedProjects(items: readonly DashboardProject[], runtime: DashboardRuntimeValue, perspective?: string): DashboardProject[] {
  return items.filter((project) =>
    (!runtime.filters.project || project.id === runtime.filters.project) &&
    (!(perspective === "PortfolioManager" && runtime.filters.program) || project.programId === runtime.filters.program),
  );
}

function filterByProjectIds<T>(items: readonly T[], projectIds: ReadonlySet<string>, getProjectId: (_item: T) => string | null | undefined): T[] {
  return items.filter((item) => {
    const projectId = getProjectId(item);
    return Boolean(projectId && projectIds.has(projectId));
  });
}

type DashboardManagementSource =
  | { kind: "program"; data?: ProgramManagementOverview; query: ReturnType<typeof useProgramManagementOverview> }
  | { kind: "portfolio"; data?: PortfolioManagementOverview; query: ReturnType<typeof usePortfolioManagementOverview> }
  | null;

function useDashboardManagementSource(): DashboardManagementSource {
  const { current } = useManagementPerspective();
  const programQuery = useProgramManagementOverview(current?.programId, 1, 100);
  const portfolioQuery = usePortfolioManagementOverview(current?.portfolioId, 1, 1, 100);
  if (current?.perspective === "ProgramManager") return { kind: "program", data: programQuery.data, query: programQuery };
  if (current?.perspective === "PortfolioManager") return { kind: "portfolio", data: portfolioQuery.data, query: portfolioQuery };
  return null;
}

export function useDashboardProjects() {
  const runtime = useDashboardRuntime();
  const { current } = useManagementPerspective();
  const source = useDashboardManagementSource();
  const query = useProjects({ page: 1, pageSize: 200, portfolioId: runtime.filters.portfolio ?? undefined }, useDashboardEnabled() && !source);
  const managementItems = source?.kind === "program" ? source.data?.projects.map(fromProgramProject) : source?.kind === "portfolio" ? source.data?.projects.map(fromPortfolioProject) : undefined;
  const managementTotal = source?.kind === "program" ? source.data?.projectsTotalCount : source?.kind === "portfolio" ? source.data?.projectsTotalCount : undefined;
  const data: DashboardProjectPage | undefined = source
    ? source.data
      ? { items: filterScopedProjects(managementItems ?? [], runtime, current?.perspective), page: 1, pageSize: 100, totalCount: managementTotal ?? 0, totalPages: Math.max(1, Math.ceil((managementTotal ?? 0) / 100)) }
      : undefined
    : query.data
      ? { ...query.data, items: filterProjects(query.data.items, runtime) }
      : undefined;
  const sourceQuery = source?.query ?? query;
  return { ...sourceQuery, data, isLoading: sourceQuery.isLoading, isError: sourceQuery.isError };
}

export function useDashboardPortfolios() {
  return usePortfolios();
}

export function useDashboardPrograms() {
  const runtime = useDashboardRuntime();
  return usePrograms({ portfolioId: runtime.filters.portfolio ?? undefined });
}

export function useDashboardRisks() {
  const projects = useDashboardProjects();
  const query = useRisks(undefined, useDashboardEnabled());
  const projectIds = new Set(projects.data?.items.map((project) => project.id) ?? []);
  return { ...query, data: query.data && projects.data ? { ...query.data, items: filterByProjectIds(query.data.items, projectIds, (item) => item.projectId) } : undefined, isLoading: query.isLoading || projects.isLoading, isError: query.isError || projects.isError };
}

export function useDashboardWorkItems() {
  const runtime = useDashboardRuntime();
  const projects = useDashboardProjects();
  const query = useWorkItems({ page: 1, pageSize: 200, projectId: runtime.filters.project ?? undefined }, useDashboardEnabled());
  const projectIds = new Set(projects.data?.items.map((project) => project.id) ?? []);
  return { ...query, data: query.data && projects.data ? { ...query.data, items: filterByProjectIds(query.data.items, projectIds, (item) => item.projectId) } : undefined, isLoading: query.isLoading || projects.isLoading, isError: query.isError || projects.isError };
}

export function useDashboardMyWorkItems() {
  const runtime = useDashboardRuntime();
  const { user } = useAuth();
  const projects = useDashboardProjects();
  const query = useWorkItems({ page: 1, pageSize: 200, projectId: runtime.filters.project ?? undefined, assigneeId: user?.id }, useDashboardEnabled());
  const projectIds = new Set(projects.data?.items.map((project) => project.id) ?? []);
  return { ...query, data: query.data && projects.data ? { ...query.data, items: filterByProjectIds(query.data.items, projectIds, (item) => item.projectId) } : undefined, isLoading: query.isLoading || projects.isLoading, isError: query.isError || projects.isError };
}

export function useDashboardMeetings() {
  const today = React.useMemo(() => new Date().toISOString().slice(0, 10), []);
  const end = React.useMemo(() => {
    const value = new Date();
    value.setDate(value.getDate() + 90);
    return value.toISOString().slice(0, 10);
  }, []);
  const runtime = useDashboardRuntime();
  const projects = useDashboardProjects();
  const query = useMeetings({ page: 1, pageSize: 50, dateFrom: today, dateTo: end, projectId: runtime.filters.project ?? undefined });
  const projectIds = new Set(projects.data?.items.map((project) => project.id) ?? []);
  return { ...query, data: query.data && projects.data ? { ...query.data, items: filterByProjectIds(query.data.items, projectIds, (item) => item.projectId) } : undefined, isLoading: query.isLoading || projects.isLoading, isError: query.isError || projects.isError };
}

export function useDashboardResources() {
  const source = useDashboardManagementSource();
  const query = useResources({ page: 1, pageSize: 200, isActive: true });
  const resourceIds = new Set(source?.data?.resources.pressure.map((resource) => resource.resourceId) ?? []);
  const data = query.data && source?.data ? { ...query.data, items: query.data.items.filter((resource) => resourceIds.has(resource.id)) } : source ? undefined : query.data;
  return { ...query, data, isLoading: query.isLoading || Boolean(source?.query.isLoading), isError: query.isError || Boolean(source?.query.isError) };
}

export function useDashboardAllocations() {
  const runtime = useDashboardRuntime();
  const projects = useDashboardProjects();
  const query = useAllocations({ page: 1, pageSize: 500, projectId: runtime.filters.project ?? undefined, status: "Active" }, useDashboardEnabled());
  const projectIds = new Set(projects.data?.items.map((project) => project.id) ?? []);
  return { ...query, data: query.data && projects.data ? { ...query.data, items: filterByProjectIds(query.data.items, projectIds, (item) => item.projectId) } : undefined, isLoading: query.isLoading || projects.isLoading, isError: query.isError || projects.isError };
}

export function useDashboardCapacity() {
  return useCapacityMatrix({ periodType: "Week", includeProposed: true });
}

export function useDashboardManagementOverview() {
  const source = useDashboardManagementSource();
  if (source?.kind === "program") return { kind: "program" as const, query: { ...source.query, data: source.data?.statusReports } };
  if (source?.kind === "portfolio") return { kind: "portfolio" as const, query: { ...source.query, data: source.data?.statusReports } };
  return null;
}

export function useDashboardLabels() {
  const t = useTranslations("dashboard");
  const locale = useLocale();
  return { t, locale };
}

export function DashboardDataState<T>({
  query,
  children,
  isEmpty = (data) => Array.isArray(data) ? data.length === 0 : Boolean(data && Array.isArray((data as { items?: unknown }).items) && (data as unknown as { items: unknown[] }).items.length === 0),
}: {
  query: { data?: T; isLoading: boolean; isError: boolean; refetch: () => unknown };
  children: (_data: T) => React.ReactNode;
  isEmpty?: (_data: T) => boolean;
}) {
  const t = useTranslations("dashboard");
  if (query.isLoading) return <LoadingSkeleton rows={3} className="p-2" />;
  if (query.isError) return <ErrorState title={t("loadFailed")} description={t("loadFailedDescription")} onRetry={() => void query.refetch()} className="min-h-32 border-0" />;
  if (query.data === undefined || isEmpty(query.data)) return <EmptyState title={t("noData")} description={t("noDataDescription")} className="min-h-32 border-0" />;
  return <>{children(query.data)}</>;
}

export type DashboardProjectQuery = ReturnType<typeof useDashboardProjects>;
export type DashboardRiskQuery = ReturnType<typeof useDashboardRisks>;
export type DashboardWorkItemQuery = ReturnType<typeof useDashboardWorkItems>;
export type DashboardMeetingQuery = ReturnType<typeof useDashboardMeetings>;
export type DashboardAllocationQuery = ReturnType<typeof useDashboardAllocations>;
export type DashboardCapacityQuery = ReturnType<typeof useDashboardCapacity>;
export type DashboardResourceQuery = ReturnType<typeof useDashboardResources>;
export type DashboardPortfolioQuery = ReturnType<typeof useDashboardPortfolios>;
export type DashboardProgramQuery = ReturnType<typeof useDashboardPrograms>;

export type { AllocationDto, CapacityMatrixDto, PortfolioDto, ProgramDto, ResourceDto, WorkItemDto };

export function useDashboardPerspective() {
  return useManagementPerspective();
}
