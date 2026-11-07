import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({ useQuery: vi.fn(), get: vi.fn() }));

vi.mock("@tanstack/react-query", () => ({ useQuery: mocks.useQuery }));
vi.mock("@api/client", () => ({ api: { get: mocks.get } }));

import { useCostUtilization, useCostUtilizationExportData } from "../features/resources/api/cost-utilization";

describe("Cost Utilization report API separation", () => {
  beforeEach(() => {
    mocks.useQuery.mockReset().mockImplementation((options) => options);
    mocks.get.mockReset().mockResolvedValue({ from: "2026-10-01", to: "2026-10-02" });
  });

  it("uses the dedicated export-data endpoint and preserves all selected filters", async () => {
    const filters = {
      from: "2026-10-01",
      to: "2026-10-02",
      resourceId: "resource-1",
      projectId: "project-public-1",
      vendorId: "vendor-1",
      projectRoleId: "role-1",
      departmentId: "department-1",
      teamId: "team-1",
      planActivityId: "activity-1",
    };

    useCostUtilizationExportData(filters, true);
    const exportOptions = mocks.useQuery.mock.calls[0][0] as { enabled: boolean; queryFn: () => Promise<unknown> };

    expect(exportOptions.enabled).toBe(true);
    await exportOptions.queryFn();
    expect(mocks.get).toHaveBeenCalledWith("/api/resource-analytics/cost-utilization/export-data", filters);
  });

  it("keeps ordinary screen reads on the original endpoint", async () => {
    useCostUtilization({ from: "2026-10-01", to: "2026-10-02" }, true);
    const readOptions = mocks.useQuery.mock.calls[0][0] as { queryFn: () => Promise<unknown> };

    await readOptions.queryFn();
    expect(mocks.get).toHaveBeenCalledWith("/api/resource-analytics/cost-utilization", {
      from: "2026-10-01",
      to: "2026-10-02",
      resourceId: undefined,
      projectId: undefined,
      vendorId: undefined,
      projectRoleId: undefined,
      departmentId: undefined,
      teamId: undefined,
      planActivityId: undefined,
    });
  });
});
