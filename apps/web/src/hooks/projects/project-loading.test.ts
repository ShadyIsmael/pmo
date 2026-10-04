import { describe, expect, it } from "vitest";
import { isProjectTabVisible, shouldLoadProjectLookup, shouldLoadProjectOverview } from "./project-loading";

describe("project visible-first loading", () => {
  it("loads overview data only for Overview and Finance", () => {
    expect(shouldLoadProjectOverview("overview")).toBe(true);
    expect(shouldLoadProjectOverview("financials")).toBe(true);
    expect(shouldLoadProjectOverview("risks-issues")).toBe(false);
    expect(shouldLoadProjectOverview("team")).toBe(false);
  });

  it("keeps tab-owned data disabled until its tab is active", () => {
    expect(isProjectTabVisible("overview", "risks-issues")).toBe(false);
    expect(isProjectTabVisible("risks-issues", "risks-issues")).toBe(true);
    expect(isProjectTabVisible("overview", "financials")).toBe(false);
    expect(isProjectTabVisible("financials", "financials")).toBe(true);
  });

  it("does not load form lookups for closed side sheets", () => {
    expect(shouldLoadProjectLookup(false)).toBe(false);
    expect(shouldLoadProjectLookup(true)).toBe(true);
  });
});
