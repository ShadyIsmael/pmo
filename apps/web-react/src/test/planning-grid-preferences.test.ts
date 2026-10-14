import { describe, expect, it } from "vitest";
import { clampPlanningGridWidth, DEFAULT_PLANNING_GRID_COLUMNS, getPlanningGridFilterKind, matchesPlanningGridFilter, normalizePlanningGridColumns, reorderPlanningGridColumn } from "@features/projects/lib/planning-grid-preferences";

describe("planning grid preferences", () => {
  it("keeps required columns and ignores unknown or duplicate saved columns", () => {
    expect(normalizePlanningGridColumns(["progress", "progress", "not-a-column", "description"])).toEqual([
      "schedulingMode", "id", "taskName", "progress", "description",
    ]);
  });

  it("uses source-compatible defaults when no saved column order exists", () => {
    expect(normalizePlanningGridColumns([])).toEqual(DEFAULT_PLANNING_GRID_COLUMNS);
  });

  it("reorders optional columns without moving the required identity columns", () => {
    expect(reorderPlanningGridColumn(DEFAULT_PLANNING_GRID_COLUMNS, "predecessors", "duration")).toEqual([
      "schedulingMode", "id", "taskName", "predecessors", "duration", "start", "finish", "progress",
    ]);
    expect(reorderPlanningGridColumn(DEFAULT_PLANNING_GRID_COLUMNS, "taskName", "duration")).toEqual(DEFAULT_PLANNING_GRID_COLUMNS);
  });

  it("clamps user-resized widths to safe integer bounds", () => {
    expect(clampPlanningGridWidth(101.7, 80, 140)).toBe(102);
    expect(clampPlanningGridWidth(20, 80, 140)).toBe(80);
    expect(clampPlanningGridWidth(200, 80, 140)).toBe(140);
  });

  it("filters text, numbers, dates and enum values by their column type", () => {
    expect(getPlanningGridFilterKind("deadline")).toBe("date");
    expect(getPlanningGridFilterKind("progress")).toBe("number");
    expect(getPlanningGridFilterKind("schedulingMode")).toBe("enum");
    expect(getPlanningGridFilterKind("resources")).toBe("text");
    expect(matchesPlanningGridFilter("Platform Upgrade", { text: "platform" }, "text")).toBe(true);
    expect(matchesPlanningGridFilter(62, { min: "50", max: "80" }, "number")).toBe(true);
    expect(matchesPlanningGridFilter("2026-10-20", { min: "2026-10-01", max: "2026-10-31" }, "date")).toBe(true);
    expect(matchesPlanningGridFilter("Manual", { value: "Automatic" }, "enum")).toBe(false);
  });
});
