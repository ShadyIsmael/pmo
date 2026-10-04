import { describe, expect, it } from "vitest";
import { isDashboardFilterOpen } from "./dashboard-loading";

describe("dashboard filter loading", () => {
  it("keeps every filter query disabled until its own menu opens", () => {
    expect(isDashboardFilterOpen(null, "portfolio")).toBe(false);
    expect(isDashboardFilterOpen("portfolio", "portfolio")).toBe(true);
    expect(isDashboardFilterOpen("portfolio", "program")).toBe(false);
    expect(isDashboardFilterOpen("project", "project")).toBe(true);
  });
});
