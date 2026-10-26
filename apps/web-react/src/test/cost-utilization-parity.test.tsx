import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../i18n/messages";
import { CostUtilizationPage } from "../features/resources/CostUtilizationPage";

const mocks = vi.hoisted(() => ({ useCostUtilization: vi.fn(), useDepartments: vi.fn() }));

vi.mock("../features/resources/api/cost-utilization", () => ({ useCostUtilization: mocks.useCostUtilization }));
vi.mock("@features/teams/api/teams", () => ({ useDepartments: mocks.useDepartments }));

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
  projects: [{ id: "project-1", projectPublicId: "public-project-1", name: "Project Atlas", ...metric, plannedHours: 120, actualHours: 110 }],
  vendors: [{ id: "vendor-1", name: "Vendor One", ...metric, plannedHours: 120, actualHours: 110 }],
  roles: [{ id: "role-1", name: "Delivery Lead", ...metric, plannedHours: 120, actualHours: 110 }],
  daily: [{ ...metric, resourceId: "resource-1", resourceName: "Taylor Resource", workDate: "2026-09-30" }],
  warnings: [],
};

function renderPage() {
  return render(<I18nProvider><CostUtilizationPage /></I18nProvider>);
}

describe("cost utilization React parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.useCostUtilization.mockReset().mockReturnValue({ data: structuredClone(report), isLoading: false, isError: false });
    mocks.useDepartments.mockReset().mockReturnValue({ data: [{ id: "department-uuid", name: "Platform", isActive: true }], isLoading: false });
  });

  it("renders the source report sections, filters, currency costs, and Western digits", () => {
    renderPage();
    expect(screen.getByRole("heading", { level: 1, name: "Cost & Utilization" })).toBeInTheDocument();
    expect(screen.getByText("Costs by currency")).toBeInTheDocument();
    expect(screen.getByText("Resource breakdown")).toBeInTheDocument();
    expect(screen.getByText("Project Atlas")).toBeInTheDocument();
    expect(screen.getByText("Daily detail")).toBeInTheDocument();
    expect(screen.getAllByText("160h").length).toBeGreaterThan(0);
    expect((screen.getByLabelText("From") as HTMLInputElement).value).toMatch(/^\d{2}\/\d{2}\/\d{4}$/);
    expect(screen.getByRole("button", { name: "All departments" })).toBeInTheDocument();
  });

  it("keeps cost filters in ISO dates while the field only shows a masked value", () => {
    renderPage();
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-09-15" } });
    expect(mocks.useCostUtilization).toHaveBeenLastCalledWith(expect.objectContaining({ from: "2026-09-15" }));
    expect((screen.getByLabelText("From") as HTMLInputElement).value).toBe("15/09/2026");
  });

  it("passes real department identifiers and selected report dimensions to the query", () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "All departments" }));
    fireEvent.click(screen.getByRole("option", { name: "Platform" }));
    expect(mocks.useCostUtilization).toHaveBeenLastCalledWith(expect.objectContaining({ departmentId: "department-uuid" }));

    fireEvent.click(screen.getByRole("button", { name: "All projects" }));
    fireEvent.click(screen.getByRole("option", { name: "Project Atlas" }));
    expect(mocks.useCostUtilization).toHaveBeenLastCalledWith(expect.objectContaining({ departmentId: "department-uuid", projectId: "public-project-1" }));
  });

  it("keeps Western digits in Arabic for operational hour values", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderPage();
    expect(screen.getByRole("heading", { level: 1, name: "التكلفة والاستغلال" })).toBeInTheDocument();
    expect(screen.getAllByText("160h").length).toBeGreaterThan(0);
  });

  it("does not render monetary values when the API denies cost visibility", () => {
    const restrictedReport = structuredClone(report);
    restrictedReport.canViewCosts = false;
    restrictedReport.operational.actualLaborCost = 888888;
    restrictedReport.resources[0].metrics.actualLaborCost = 888888;
    restrictedReport.projects[0].actualLaborCost = 888888;
    mocks.useCostUtilization.mockReturnValue({ data: restrictedReport, isLoading: false, isError: false });

    renderPage();
    expect(screen.getByText(/monetary values require Resource cost-rate permission/i)).toBeInTheDocument();
    expect(screen.queryByText(/888,888/)).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Actual labor cost" })).not.toBeInTheDocument();
    expect(screen.queryByText("Costs by currency")).not.toBeInTheDocument();
  });
});
