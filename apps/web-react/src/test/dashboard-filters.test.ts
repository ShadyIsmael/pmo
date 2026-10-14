import { describe, expect, it } from "vitest";
import { dashboardSummaryFilters, DEFAULT_DASHBOARD_DATE_RANGE, normalizeDashboardFilters } from "../features/home/dashboardFilters";

describe("dashboard filters", () => {
  it("defaults the date range for new and older saved views", () => {
    expect(normalizeDashboardFilters()).toEqual({
      portfolioId: undefined,
      programId: undefined,
      projectId: undefined,
      dateRange: DEFAULT_DASHBOARD_DATE_RANGE,
    });
    expect(normalizeDashboardFilters({ projectId: "project-1" })).toMatchObject({
      projectId: "project-1",
      dateRange: "30d",
    });
  });

  it("preserves supported date ranges and scope filters", () => {
    expect(normalizeDashboardFilters({ dateRange: "ytd", portfolioId: "portfolio-1" })).toMatchObject({
      dateRange: "ytd",
      portfolioId: "portfolio-1",
    });
  });

  it("falls back to the default for unsupported saved ranges", () => {
    expect(normalizeDashboardFilters({ dateRange: "invalid" })).toMatchObject({ dateRange: "30d" });
  });

  it("keeps the display-only date range out of Home summary requests, like the source app", () => {
    expect(dashboardSummaryFilters(normalizeDashboardFilters({ dateRange: "7d", projectId: "project-1" }))).toEqual({
      portfolioId: undefined,
      programId: undefined,
      projectId: "project-1",
    });
  });
});
