export type DashboardFilterKey = "portfolio" | "program" | "project";

export function isDashboardFilterOpen(openFilter: DashboardFilterKey | null, filter: DashboardFilterKey): boolean {
  return openFilter === filter;
}
