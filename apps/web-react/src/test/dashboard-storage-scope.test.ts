import { beforeEach, describe, expect, it } from "vitest";
import { dashboardViewsStorageKey, loadDashboardViews, saveDashboardViews } from "../features/home/dashboardStorage";

describe("dashboard view storage scope", () => {
  beforeEach(() => window.localStorage.clear());

  it("isolates saved layouts by tenant and user", () => {
    const firstTenantViews = { name: "Tenant A dashboard" };
    const secondTenantViews = { name: "Tenant B dashboard" };

    saveDashboardViews("tenant/a", "user-1", firstTenantViews);
    saveDashboardViews("tenant-b", "user-1", secondTenantViews);

    expect(loadDashboardViews("tenant/a", "user-1")).toEqual(firstTenantViews);
    expect(loadDashboardViews("tenant-b", "user-1")).toEqual(secondTenantViews);
    expect(loadDashboardViews("tenant/a", "user-2")).toBeNull();
    expect(dashboardViewsStorageKey("tenant/a", "user-1")).not.toBe(dashboardViewsStorageKey("tenant-b", "user-1"));
  });

  it("does not load the previous unscoped dashboard key", () => {
    window.localStorage.setItem("nexus-react-dashboard-views", JSON.stringify({ views: [{ name: "Unscoped" }] }));

    expect(loadDashboardViews("tenant-a", "user-1")).toBeNull();
  });
});
