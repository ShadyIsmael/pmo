import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { I18nProvider } from "../i18n/messages";
import { CapacityPage } from "../features/resources/CapacityPage";

const mocks = vi.hoisted(() => ({
  useCapacityMatrix: vi.fn(),
  useDemandVsCapacity: vi.fn(),
  useCapacityAvailability: vi.fn(),
  useCapacityGantt: vi.fn(),
  useAllocations: vi.fn(),
  useDeleteAllocation: vi.fn(),
  useDepartments: vi.fn(),
}));

vi.mock("@features/resources/api/capacity", () => ({
  useCapacityMatrix: mocks.useCapacityMatrix,
  useDemandVsCapacity: mocks.useDemandVsCapacity,
  useCapacityAvailability: mocks.useCapacityAvailability,
  useCapacityGantt: mocks.useCapacityGantt,
}));
vi.mock("@features/resources/api/allocations", () => ({
  useAllocations: mocks.useAllocations,
  useDeleteAllocation: mocks.useDeleteAllocation,
}));
vi.mock("@features/teams/api/teams", () => ({ useDepartments: mocks.useDepartments }));
vi.mock("../features/resources/AllocationDialog", () => ({ AllocationDialog: () => null }));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: [] } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: false, effectiveReadOnly: false }) }));

const matrix = {
  periods: [{ key: "2026-10", label: "October" }],
  summary: { totalNetCapacityHours: 160, totalAvailableHours: 100, overallUtilizationPercent: 70 },
  rows: [
    { resourceId: "res-1", resourceName: "Morgan Resource", departmentName: "Technology", weeklyCapacityHours: 40, currentUtilization: 80, cells: [{ periodKey: "2026-10", netCapacityHours: 40, availableHours: 10, utilizationPercent: 80 }] },
    { resourceId: "res-2", resourceName: "Jamie Resource", departmentName: "Technology", weeklyCapacityHours: 30, currentUtilization: 90, cells: [{ periodKey: "2026-10", netCapacityHours: 30, availableHours: 20, utilizationPercent: 90 }] },
    { resourceId: "res-3", resourceName: "Casey Resource", departmentName: "Operations", weeklyCapacityHours: 25, currentUtilization: 40, cells: [{ periodKey: "2026-10", netCapacityHours: 25, availableHours: 30, utilizationPercent: 40 }] },
  ],
};

function renderPage() {
  return render(<I18nProvider><CapacityPage /></I18nProvider>);
}

describe("capacity page source parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    window.localStorage.setItem("nexus_capacity_view", "matrix");
    mocks.useCapacityMatrix.mockReset().mockReturnValue({ data: matrix, isLoading: false, isError: false });
    mocks.useDemandVsCapacity.mockReset().mockReturnValue({
      data: { groups: [], total: { totalDemandHours: 80, totalCapacityHours: 100, totalGapHours: 20 } },
      isLoading: false,
      isError: false,
    });
    mocks.useCapacityAvailability.mockReset().mockReturnValue({ data: { items: [], totalCount: 0 }, isLoading: false, isError: false });
    mocks.useCapacityGantt.mockReset().mockReturnValue({ data: [], isLoading: false, isError: false });
    mocks.useAllocations.mockReset().mockReturnValue({ data: { items: [], totalCount: 0 }, isLoading: false, isError: false });
    mocks.useDeleteAllocation.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.useDepartments.mockReset().mockReturnValue({ data: [] });
  });

  it("shows source-style department summaries instead of ungrouped resource rows", () => {
    renderPage();

    expect(screen.getByText("Department Capacity Overview")).toBeInTheDocument();
    expect(screen.getByText("Technology")).toBeInTheDocument();
    expect(screen.getByText("Operations")).toBeInTheDocument();
    expect(screen.getByText(/2 resources/)).toBeInTheDocument();
    expect(document.body.textContent).toContain("70 hrs");
    expect(screen.getByText("85%")).toBeInTheDocument();
    expect(screen.getByText("Over")).toBeInTheDocument();
    expect(screen.queryByText("Morgan Resource")).not.toBeInTheDocument();
  });

  it("uses the source three-month default window", () => {
    renderPage();

    const today = new Date();
    const expectedStart = new Date(today.getFullYear(), today.getMonth(), 1).toISOString().slice(0, 10);
    const sourceEnd = new Date(today.getFullYear(), today.getMonth(), 1);
    sourceEnd.setMonth(sourceEnd.getMonth() + 3);
    const expectedEnd = sourceEnd.toISOString().slice(0, 10);

    expect(mocks.useCapacityMatrix).toHaveBeenCalledWith(expect.objectContaining({
      periodType: "month",
      startDate: expectedStart,
      endDate: expectedEnd,
    }));
  });
});
