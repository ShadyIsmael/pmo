import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { I18nProvider } from "../i18n/messages";
import { CostUtilizationPage } from "../features/resources/CostUtilizationPage";

const mocks = vi.hoisted(() => ({
  useCostUtilization: vi.fn(),
  useCostUtilizationExportData: vi.fn(),
  permissions: [] as string[],
  management: { canMutate: true, effectiveReadOnly: false },
}));

vi.mock("../features/resources/api/cost-utilization", () => ({
  useCostUtilization: mocks.useCostUtilization,
  useCostUtilizationExportData: mocks.useCostUtilizationExportData,
}));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: mocks.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => mocks.management }));
vi.mock("@features/teams/api/teams", () => ({
  useActiveTeams: () => ({ data: [], isLoading: false, isError: false }),
}));

const metric = {
  currency: "USD", capacityHours: 160, plannedHours: 120, actualHours: 110, regularHours: 100,
  planningGapHours: 10, executionGapHours: 10, unusedCapacityHours: 50, excessHours: 0, overtimeHours: 10,
  plannedUtilization: 75, actualUtilization: 68.75, plannedLaborCost: 6000, actualLaborCost: 5500,
  laborCostVariance: -500, overtimeCost: 500, unusedCapacityCost: 2500, committedCapacityCost: 8000,
  totalResourceCost: 8000, costProfileMissing: false, costCoverageComplete: true, uncostedActualHours: 0, uncostedPlannedHours: 0,
};

const report = {
  from: "2026-09-01", to: "2026-09-30", canViewCosts: true, operational: metric, byCurrency: [metric],
  resources: [{ resourceId: "resource-1", resourceName: "Taylor Resource", resourceType: "Staff", departmentName: "Platform", vendorId: "vendor-1", vendorName: "Vendor One", metrics: metric }],
  departments: [{ departmentId: "department-uuid", name: "Platform" }],
  projects: [{ id: "project-1", projectPublicId: "public-project-1", name: "Project Atlas", ...metric, plannedHours: 120, actualHours: 110 }],
  vendors: [{ id: "vendor-1", name: "Vendor One", ...metric, plannedHours: 120, actualHours: 110 }],
  roles: [{ id: "role-1", name: "Delivery Lead", ...metric, plannedHours: 120, actualHours: 110 }],
  daily: [{ ...metric, resourceId: "resource-1", resourceName: "Taylor Resource", workDate: "2026-09-30", activities: [{ planActivityId: "activity-1", activityName: "Activity Alpha", workItemTitles: ["Release notes"], projectId: "project-1", projectName: "Project Atlas", plannedHours: 4, actualHours: 3 }] }],
  warnings: [],
};

function renderPage() {
  return render(<I18nProvider><CostUtilizationPage /></I18nProvider>);
}

function expectReportFilter(expected: Record<string, string>) {
  const calls = mocks.useCostUtilization.mock.calls as Array<[Record<string, unknown>]>;
  expect(calls.some(([filter]) => Object.entries(expected).every(([key, value]) => filter[key] === value))).toBe(true);
}

describe("cost utilization React parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.useCostUtilization.mockReset().mockReturnValue({ data: structuredClone(report), isLoading: false, isFetching: false, isError: false });
    mocks.useCostUtilizationExportData.mockReset().mockReturnValue({ data: structuredClone(report), isLoading: false, isFetching: false, isError: false });
    mocks.permissions = [];
    mocks.management = { canMutate: true, effectiveReadOnly: false };
  });

  it("renders the source report sections, filters, currency costs, and Western digits", () => {
    renderPage();
    expect(screen.getByRole("heading", { level: 1, name: "Cost & Utilization" })).toBeInTheDocument();
    expect(screen.getByText("Costs by currency")).toBeInTheDocument();
    expect(screen.queryByText("Resource breakdown")).not.toBeInTheDocument();
    expect(screen.getByText("Project Atlas")).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Daily detail" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Calendar view" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getAllByText("160h").length).toBeGreaterThan(0);
    expect((screen.getByLabelText("From") as HTMLInputElement).value).toMatch(/^\d{2}\/\d{2}\/\d{4}$/);
    expect(screen.getByRole("button", { name: "All departments" })).toBeInTheDocument();
  });

  it("keeps ordinary reads available while requiring the dedicated export permission", () => {
    renderPage();

    expect(mocks.useCostUtilization).toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: "Export" })).not.toBeInTheDocument();
    expect(mocks.useCostUtilizationExportData).not.toHaveBeenCalled();
  });

  it("hides export for read-only management contexts even when the permission is assigned", () => {
    mocks.permissions = ["Resource.View", "CostUtilization.Export"];
    mocks.management = { canMutate: false, effectiveReadOnly: true };
    renderPage();

    expect(mocks.useCostUtilization).toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: "Export" })).not.toBeInTheDocument();
    expect(mocks.useCostUtilizationExportData).not.toHaveBeenCalled();
  });

  it("loads export data only through the dedicated export query when authorized", () => {
    mocks.permissions = ["Resource.View", "CostUtilization.Export"];
    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "Export" }));

    const exportCalls = mocks.useCostUtilizationExportData.mock.calls as Array<[Record<string, unknown>, boolean]>;
    expect(exportCalls.some(([filter, enabled]) => enabled && Object.keys(filter).length > 0)).toBe(true);
    expect(mocks.useCostUtilization).toHaveBeenCalled();
  });

  it("uses report-scoped department options and carries the selected filter into export data", () => {
    mocks.permissions = ["Resource.View", "CostUtilization.Export"];
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Export" }));
    const exportSheet = screen.getByRole("dialog", { name: "Export settings" });
    fireEvent.click(within(exportSheet).getByRole("button", { name: "All departments" }));
    fireEvent.click(screen.getByRole("option", { name: "Platform" }));

    const exportCalls = mocks.useCostUtilizationExportData.mock.calls as Array<[Record<string, unknown>, boolean]>;
    expect(exportCalls.some(([filter, enabled]) => enabled && filter.departmentId === "department-uuid")).toBe(true);
  });

  it("keeps cost filters in ISO dates while the field only shows a masked value", () => {
    renderPage();
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-09-15" } });
    expectReportFilter({ from: "2026-09-15" });
    expect((screen.getByLabelText("From") as HTMLInputElement).value).toBe("15/09/2026");
  });

  it("passes real department identifiers and selected report dimensions to the query", () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "All departments" }));
    fireEvent.click(screen.getByRole("option", { name: "Platform" }));
    expectReportFilter({ departmentId: "department-uuid" });

    fireEvent.click(screen.getByRole("button", { name: "All projects" }));
    fireEvent.click(screen.getByRole("option", { name: "Project Atlas" }));
    expectReportFilter({ departmentId: "department-uuid", projectId: "public-project-1" });
  });

  it("keeps Western digits in Arabic for operational hour values", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderPage();
    expect(screen.getByRole("heading", { level: 1, name: "التكلفة والاستغلال" })).toBeInTheDocument();
    expect(screen.getAllByText("160h").length).toBeGreaterThan(0);
    expect(document.documentElement.dir).toBe("rtl");
    fireEvent.click(screen.getByRole("tab", { name: "التفصيل اليومي" }));
    const expandDay = screen.getByRole("button", { name: /عرض موارد هذا اليوم/ });
    fireEvent.click(expandDay);
    expect(screen.getByRole("heading", { name: "تفصيل الموارد" })).toBeInTheDocument();
  });

  it("keeps daily totals visible and expands the resource breakdown inline for each day", () => {
    renderPage();
    fireEvent.click(screen.getByRole("tab", { name: "Daily detail" }));

    const expandDay = screen.getByRole("button", { name: /Show resources for this day/ });
    const daySummary = expandDay.closest("tr");
    expect(daySummary).toHaveTextContent("160h");
    expect(daySummary).toHaveTextContent("120h");
    expect(daySummary).toHaveTextContent("110h");

    fireEvent.click(expandDay);
    expect(expandDay).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("heading", { name: "Resource breakdown" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Type" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Department" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Vendor" })).toBeInTheDocument();
    for (const name of ["Resource", "Type", "Department", "Vendor", "Capacity", "Planned work", "Actual work", "Planned utilization", "Actual utilization", "Excess", "Overtime", "Actual labor cost"]) {
      expect(screen.getAllByRole("columnheader", { name })).not.toHaveLength(0);
    }
    expect(screen.getByText("Staff")).toBeInTheDocument();
    expect(screen.getByText("Platform")).toBeInTheDocument();
    expect(screen.getAllByText("Vendor One")).toHaveLength(2);

    fireEvent.click(expandDay);
    expect(expandDay).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("heading", { name: "Resource breakdown" })).not.toBeInTheDocument();
  });

  it("does not render monetary values when the API denies cost visibility", () => {
    const restrictedReport = structuredClone(report);
    restrictedReport.canViewCosts = false;
    restrictedReport.operational.actualLaborCost = 888888;
    restrictedReport.resources[0].metrics.actualLaborCost = 888888;
    restrictedReport.projects[0].actualLaborCost = 888888;
    restrictedReport.daily[0].actualLaborCost = 888888;
    mocks.useCostUtilization.mockReturnValue({ data: restrictedReport, isLoading: false, isError: false });

    renderPage();
    expect(screen.getAllByText(/monetary values require Resource cost-rate permission/i).length).toBeGreaterThan(0);
    expect(screen.queryByText(/888,888/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "Daily detail" }));
    fireEvent.click(screen.getByRole("button", { name: /Show resources for this day/ }));
    expect(screen.queryByText(/888,888/)).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Actual labor cost" })).not.toBeInTheDocument();
    expect(screen.queryByText("Costs by currency")).not.toBeInTheDocument();
  });

  it("shows an explicit empty state when the report has no daily rows", () => {
    const emptyDailyReport = structuredClone(report);
    emptyDailyReport.daily = [];
    mocks.useCostUtilization.mockReturnValue({ data: emptyDailyReport, isLoading: false, isError: false });
    renderPage();
    fireEvent.click(screen.getByRole("tab", { name: "Daily detail" }));
    expect(screen.getByText("No daily utilization data")).toBeInTheDocument();
  });

  it("keeps day-level actual costs separate when resources use different currencies", () => {
    const multiCurrencyReport = structuredClone(report);
    multiCurrencyReport.resources.push({ resourceId: "resource-2", resourceName: "Mona Resource", resourceType: "Staff", departmentName: "Delivery", vendorId: "vendor-2", vendorName: "Vendor Two", metrics: { ...metric, currency: "EGP", actualLaborCost: 350 } });
    multiCurrencyReport.daily.push({ ...metric, resourceId: "resource-2", resourceName: "Mona Resource", workDate: "2026-09-30", currency: "EGP", actualLaborCost: 350, activities: [] });
    mocks.useCostUtilization.mockReturnValue({ data: multiCurrencyReport, isLoading: false, isError: false });

    renderPage();
    fireEvent.click(screen.getByRole("tab", { name: "Daily detail" }));
    const daySummary = screen.getByRole("button", { name: /Show resources for this day/ }).closest("tr");
    expect(daySummary).toHaveTextContent("USD:");
    expect(daySummary).toHaveTextContent("EGP:");
  });

  it("loads the selected month and opens day details with matching resources and activities", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-15T12:00:00"));
    try {
      renderPage();
      fireEvent.click(screen.getByRole("tab", { name: "Calendar view" }));

      expectReportFilter({ from: "2026-09-01", to: "2026-09-30" });
      expect(screen.getByRole("heading", { name: "September 2026" })).toBeInTheDocument();
      fireEvent.click(screen.getByRole("gridcell", { name: /September 30, 2026/ }));

      const details = screen.getByRole("dialog", { name: "Day details" });
      expect(within(details).getAllByText("Taylor Resource").length).toBeGreaterThan(0);
      expect(within(details).getByText("Activity Alpha")).toBeInTheDocument();
      expect(within(details).getByText("Release notes")).toBeInTheDocument();
      expect(within(details).getAllByText("Project Atlas").length).toBeGreaterThan(0);
      expect(within(details).getAllByText("Planned labor cost").length).toBeGreaterThan(0);
    } finally {
      vi.useRealTimers();
    }
  });

  it("opens empty-day details and keeps the requested month in the shared report range", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-15T12:00:00"));
    try {
      renderPage();
      fireEvent.click(screen.getByRole("tab", { name: "Calendar view" }));
      fireEvent.click(screen.getByRole("gridcell", { name: /September 29, 2026/ }));

      const details = screen.getByRole("dialog", { name: "Day details" });
      expect(within(details).getByText("No daily utilization data")).toBeInTheDocument();
      expectReportFilter({ from: "2026-09-01", to: "2026-09-30" });
    } finally {
      vi.useRealTimers();
    }
  });

  it("shows a calendar loading state while the same Cost Utilization query is pending", () => {
    mocks.useCostUtilization.mockReturnValue({ data: undefined, isLoading: true, isFetching: true, isError: false });
    renderPage();
    fireEvent.click(screen.getByRole("tab", { name: "Calendar view" }));
    expect(screen.getByRole("heading", { name: "November 2026" })).toBeInTheDocument();
    expect(screen.getAllByRole("gridcell")).toHaveLength(42);
  });
});
