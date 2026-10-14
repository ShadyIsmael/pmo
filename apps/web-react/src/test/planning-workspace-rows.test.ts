import { describe, expect, it } from "vitest";
import type { PlanActivityDto, PlanWbsNodeDto } from "@features/projects/api/planningFull";
import { buildPlanWorkspaceRows } from "@features/projects/lib/plan-workspace-rows";

const baseWbs = (overrides: Partial<PlanWbsNodeDto> = {}): PlanWbsNodeDto => ({
  id: "wbs-1", planVersionId: "plan-1", sortOrder: 0, code: "1", name: "Phase", rowVersion: "w1", children: [], ...overrides,
});

const baseActivity = (overrides: Partial<PlanActivityDto> = {}): PlanActivityDto => ({
  id: "activity-1", planVersionId: "plan-1", code: "T-1", name: "Task", type: "Task", duration: 1, critical: false, rowVersion: "a1", ...overrides,
});

describe("planning workspace row formatting", () => {
  it("preserves the task's saved formatting for the grid and ribbon", () => {
    const format = { isBold: true, textColorToken: "Blue" };
    const rows = buildPlanWorkspaceRows([baseWbs({ activityId: "activity-1" })], [baseActivity({ textFormat: format })]);

    expect(rows[0].textFormat).toEqual(format);
  });

  it("uses WBS formatting when its linked task has no row formatting", () => {
    const format = { isItalic: true, highlightColorToken: "Yellow" };
    const rows = buildPlanWorkspaceRows([baseWbs({ activityId: "activity-1", textFormat: format })], [baseActivity()]);

    expect(rows[0].textFormat).toEqual(format);
  });

  it("preserves formatting on child and unplaced task rows", () => {
    const childFormat = { isUnderline: true };
    const unplacedFormat = { fontSizeToken: "Large" };
    const child = baseActivity({ id: "activity-2", code: "T-2", wbsId: "wbs-1", textFormat: childFormat });
    const unplaced = baseActivity({ id: "activity-3", code: "T-3", textFormat: unplacedFormat });
    const rows = buildPlanWorkspaceRows([baseWbs({ activityId: "activity-1" })], [baseActivity(), child, unplaced]);

    expect(rows.find((row) => row.activity?.id === "activity-2")?.textFormat).toEqual(childFormat);
    expect(rows.find((row) => row.activity?.id === "activity-3")?.textFormat).toEqual(unplacedFormat);
  });
});
