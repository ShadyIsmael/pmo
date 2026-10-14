import { describe, expect, it } from "vitest";
import { canPerformPlanningWrites, getPlanningBaselineUnavailableReason } from "../features/projects/lib/planning-access";

describe("planning write access", () => {
  it("requires the planning manage permission", () => {
    expect(canPerformPlanningWrites(false, true, false)).toBe(false);
  });

  it("blocks writes outside a mutable management context", () => {
    expect(canPerformPlanningWrites(true, false, false)).toBe(false);
  });

  it("blocks writes in an effective read-only view", () => {
    expect(canPerformPlanningWrites(true, true, true)).toBe(false);
  });

  it("allows planning writes for an authorized mutable context", () => {
    expect(canPerformPlanningWrites(true, true, false)).toBe(true);
  });

  it("explains that baseline lifecycle permission is required", () => {
    expect(getPlanningBaselineUnavailableReason(false, true, false)).toBe("permission-required");
  });

  it("explains when baseline actions need an editable context", () => {
    expect(getPlanningBaselineUnavailableReason(true, false, false)).toBe("editable-context-required");
    expect(getPlanningBaselineUnavailableReason(true, true, true)).toBe("editable-context-required");
  });

  it("shows no baseline-access warning when lifecycle permission and context allow it", () => {
    expect(getPlanningBaselineUnavailableReason(true, true, false)).toBeNull();
  });
});
