import type { HomeSummaryFilters } from "@api/home";

export const DASHBOARD_DATE_RANGES = ["7d", "30d", "90d", "ytd", "1y"] as const;
export type DashboardDateRange = (typeof DASHBOARD_DATE_RANGES)[number];
export type DashboardFilters = HomeSummaryFilters & { dateRange: DashboardDateRange };

export const DEFAULT_DASHBOARD_DATE_RANGE: DashboardDateRange = "30d";

export function normalizeDashboardFilters(
  filters?: Partial<HomeSummaryFilters> & { dateRange?: unknown },
): DashboardFilters {
  const dateRange = DASHBOARD_DATE_RANGES.includes(filters?.dateRange as DashboardDateRange)
    ? (filters!.dateRange as DashboardDateRange)
    : DEFAULT_DASHBOARD_DATE_RANGE;

  return {
    portfolioId: filters?.portfolioId,
    programId: filters?.programId,
    projectId: filters?.projectId,
    dateRange,
  };
}

export function dashboardSummaryFilters(filters: DashboardFilters): HomeSummaryFilters {
  return {
    portfolioId: filters.portfolioId,
    programId: filters.programId,
    projectId: filters.projectId,
  };
}
