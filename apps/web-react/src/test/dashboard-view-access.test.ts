import { describe, expect, it } from "vitest";
import {
  canDuplicateDashboardView,
  canEditDashboardView,
  canMutateDashboard,
  dashboardViewsForPov,
  defaultDashboardViewIdForPov,
  type DashboardViewAccessDefinition,
} from "../features/home/dashboard-view-access";
import { defaultDashboardViewsForPov, ensureDashboardSharedTemplates } from "../features/home/DashboardBuilder";

describe("dashboard view access and POV-scoped fallback", () => {
  it("uses the view access level for both personal and shared view actions", () => {
    const accessLevels: DashboardViewAccessDefinition["access"][] = ["view", "duplicate", "edit", "manage-sharing"];
    expect(accessLevels.map((access) => canEditDashboardView({ access }))).toEqual([false, false, true, true]);
    expect(accessLevels.map((access) => canDuplicateDashboardView({ access }))).toEqual([false, true, true, true]);
  });

  it("allows personal dashboard editing without a selected management context but protects read-only scopes", () => {
    expect(canMutateDashboard(true, false, false)).toBe(true);
    expect(canMutateDashboard(false, false, true)).toBe(false);
    expect(canMutateDashboard(true, true, false)).toBe(false);
  });

  it("chooses the default or first surviving view only within the active POV", () => {
    const remaining = [
      { id: "other-pov-default", pov: "Team Member", isDefault: true },
      { id: "active-pov-custom", pov: "Project Manager", isDefault: false },
      { id: "active-pov-default", pov: "Project Manager", isDefault: true },
    ];
    expect(defaultDashboardViewIdForPov(remaining, "Project Manager")).toBe("active-pov-default");
    expect(defaultDashboardViewIdForPov(remaining.slice(0, 1), "Project Manager")).toBeUndefined();
    expect(dashboardViewsForPov(remaining, "Project Manager").map(({ id }) => id)).toEqual(["active-pov-custom", "active-pov-default"]);
  });

  it("starts each new point of view with its personal dashboard and the standard shared template", () => {
    const views = defaultDashboardViewsForPov("Team Member", "user-1", "Team Member");
    expect(views.map(({ type }) => type)).toEqual(["personal", "shared"]);
    expect(views[0]).toMatchObject({ id: "default-team-member", pov: "Team Member", ownerId: "user-1", access: "manage-sharing", isDefault: true });
    expect(views[1]).toMatchObject({ id: "shared-team-member", name: "Team Member Standard", ownerId: "pmo-admin", ownerName: "PMO Office", access: "duplicate", sharedWith: ["Team Member"], isDefault: false });
  });

  it("adds missing standard templates to older saved POVs without duplicating an existing one", () => {
    const oldSavedViews = defaultDashboardViewsForPov("Project Manager", "user-1", "PM").filter(({ type }) => type === "personal");
    const migrated = ensureDashboardSharedTemplates(oldSavedViews);
    expect(migrated.map(({ type }) => type)).toEqual(["personal", "shared"]);
    expect(ensureDashboardSharedTemplates(migrated)).toHaveLength(2);
  });
});
