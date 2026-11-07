import { describe, expect, it } from "vitest";
import enMessages from "../i18n/messages/en.json";
import arMessages from "../i18n/messages/ar.json";
import { endpoints } from "../api/endpoints";
import { hasAnyPermission, Permissions } from "../lib/permissions";
import { CapacityPage } from "../features/resources/CapacityPage";
import { AllocationDialog } from "../features/resources/AllocationDialog";
import { ResourceGanttView } from "../features/resources/ResourceGanttView";
import { allocationKeys } from "../features/resources/api/allocations";
import { hasDashboardResourceReadAccess } from "../features/home/dashboard-widget-utils";

describe("resource capacity parity", () => {
  it("exposes the canonical allocation endpoints and capacity views", () => {
    expect(endpoints.allocations.root).toBe("/api/allocations");
    expect(endpoints.allocations.conflicts).toBe("/api/allocations/conflicts");
    expect(endpoints.capacity.gantt).toBe("/api/capacity/gantt");
    expect(typeof CapacityPage).toBe("function");
    expect(typeof AllocationDialog).toBe("function");
    expect(typeof ResourceGanttView).toBe("function");
    expect(allocationKeys.conflicts("resource", "2026-01-01", "2026-01-31")).toEqual(["allocations", "conflicts", "resource", "2026-01-01", "2026-01-31", undefined]);
  });

  it("keeps allocation mutations permission-aware", () => {
    expect(hasAnyPermission([], [Permissions.Resources.Assign])).toBe(false);
    expect(hasAnyPermission([Permissions.Resources.Assign], [Permissions.Resources.Assign])).toBe(true);
    expect(hasAnyPermission(["Resource.Allocate"], [Permissions.Resources.Assign])).toBe(false);
    expect(hasDashboardResourceReadAccess([Permissions.Resources.View])).toBe(true);
    expect(hasDashboardResourceReadAccess(["Resource.Allocate"])).toBe(false);
  });

  it("keeps Capacity allocation copy in EN and AR", () => {
    const en = (enMessages.resources as typeof enMessages.resources).capacity as Record<string, string>;
    const ar = (arMessages.resources as typeof arMessages.resources).capacity as Record<string, string>;
    for (const key of ["allocationTable", "allocationTableDescription", "emptyAllocations", "editAllocation", "deleteAllocation", "deleteConfirm"]) {
      expect(typeof en[key], `missing EN Capacity key: ${key}`).toBe("string");
      expect(typeof ar[key], `missing AR Capacity key: ${key}`).toBe("string");
    }
  });
});
