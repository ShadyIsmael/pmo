import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { I18nProvider } from "../i18n/messages";
import { CapacityPage } from "../features/resources/CapacityPage";
import { ResourceUtilizationPage } from "../features/resources/ResourceUtilizationPage";

const mocks = vi.hoisted(() => ({
  useDepartments: vi.fn(),
  useAvailability: vi.fn(),
  useMatrix: vi.fn(),
  useDemand: vi.fn(),
  useGantt: vi.fn(),
  useAllocations: vi.fn(),
  useDeleteAllocation: vi.fn(),
}));

vi.mock("@features/teams/api/teams", () => ({ useDepartments: mocks.useDepartments }));
vi.mock("../features/resources/api/capacity", () => ({
  useCapacityAvailability: mocks.useAvailability,
  useCapacityMatrix: mocks.useMatrix,
  useDemandVsCapacity: mocks.useDemand,
  useCapacityGantt: mocks.useGantt,
}));
vi.mock("../features/resources/api/allocations", () => ({
  useAllocations: mocks.useAllocations,
  useDeleteAllocation: mocks.useDeleteAllocation,
}));
vi.mock("../features/resources/AllocationDialog", () => ({ AllocationDialog: () => null }));
vi.mock("../features/resources/ResourceGanttView", () => ({ ResourceGanttView: () => null }));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: ["Resource.View"] } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: false, effectiveReadOnly: false }) }));

const departments = [{ id: "department-17", name: "Technology", isActive: true }];

function renderPage(page: ReactNode) {
  window.localStorage.setItem("nexus-locale", "en");
  return render(<I18nProvider>{page}</I18nProvider>);
}

describe("resource analytics department filters", () => {
  beforeEach(() => {
    mocks.useDepartments.mockReset().mockReturnValue({ data: departments, isLoading: false, isError: false });
    mocks.useAvailability.mockReset().mockReturnValue({ data: { items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 }, isLoading: false, isError: false });
    mocks.useMatrix.mockReset().mockReturnValue({ data: { rows: [], periods: [], summary: { totalNetCapacityHours: 0, totalAvailableHours: 0, overallUtilizationPercent: 0 } }, isLoading: false, isError: false });
    mocks.useDemand.mockReset().mockReturnValue({ data: { groups: [], total: { totalDemandHours: 0, totalCapacityHours: 0, totalGapHours: 0 } }, isLoading: false, isError: false });
    mocks.useGantt.mockReset().mockReturnValue({ data: [], isLoading: false, isError: false });
    mocks.useAllocations.mockReset().mockReturnValue({ data: { items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 }, isLoading: false, isError: false });
    mocks.useDeleteAllocation.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
  });

  it("passes the selected department ID to all Capacity views", () => {
    renderPage(<CapacityPage />);
    const filter = screen.getByRole("combobox", { name: "Dept" });
    expect(screen.getByRole("option", { name: "Technology" })).toHaveValue("department-17");
    fireEvent.change(filter, { target: { value: "department-17" } });

    expect(mocks.useMatrix).toHaveBeenLastCalledWith(expect.objectContaining({ departmentId: "department-17" }));
    expect(mocks.useDemand).toHaveBeenLastCalledWith(expect.objectContaining({ departmentId: "department-17" }));
    expect(mocks.useAvailability).toHaveBeenLastCalledWith(expect.objectContaining({ departmentId: "department-17" }));
    expect(mocks.useGantt).toHaveBeenLastCalledWith(expect.objectContaining({ departmentId: "department-17" }));
  });

  it("passes the selected department ID to resource utilization analytics", () => {
    renderPage(<ResourceUtilizationPage />);
    const filter = screen.getByRole("combobox", { name: "Department" });
    expect(screen.getByRole("option", { name: "Technology" })).toHaveValue("department-17");
    fireEvent.change(filter, { target: { value: "department-17" } });

    expect(mocks.useAvailability).toHaveBeenLastCalledWith({ departmentId: "department-17" });
  });

  it("matches the source's sorted top-15 resource rows and utilization indicators", () => {
    const items = Array.from({ length: 20 }, (_, index) => ({
      resourceId: "resource-" + (index + 1),
      resourceName: "Resource " + String(index + 1).padStart(2, "0"),
      jobTitle: "Analyst",
      departmentName: "Technology",
      weeklyCapacityHours: 40,
      currentUtilization: 100 - index * 5,
      availableCapacityHours: 30,
      earliestAvailableDate: "2026-10-01",
    }));
    mocks.useAvailability.mockReturnValue({ data: { items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1 }, isLoading: false, isError: false });

    renderPage(<ResourceUtilizationPage />);

    expect(screen.getAllByRole("row")).toHaveLength(16);
    expect(screen.getByText("Resource 01")).toBeInTheDocument();
    expect(screen.getByText("Resource 15")).toBeInTheDocument();
    expect(screen.queryByText("Resource 16")).not.toBeInTheDocument();
    expect(screen.getAllByText("R0").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Over").length).toBeGreaterThan(0);
    expect(screen.getAllByText("At risk").length).toBeGreaterThan(0);
    expect(screen.getAllByText("OK").length).toBeGreaterThan(0);
    expect(screen.getAllByRole("progressbar")).toHaveLength(31);
  });

  it("shows source-shaped loading skeletons instead of zero-valued utilization metrics", () => {
    mocks.useAvailability.mockReturnValue({ data: undefined, isLoading: true, isError: false });

    renderPage(<ResourceUtilizationPage />);

    expect(screen.getAllByRole("row")).toHaveLength(9);
    expect(document.querySelectorAll(".animate-pulse").length).toBeGreaterThan(3);
    expect(screen.queryByText("0%")).not.toBeInTheDocument();
  });
});
