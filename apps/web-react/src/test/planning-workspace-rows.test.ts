import { describe, expect, it } from "vitest";
import type { PlanActivityDto, PlanWbsNodeDto } from "@features/projects/api/planningFull";
import { buildPlanWorkspaceRows, collapsePlanWorkspaceToOutlineLevel, getPlanWorkspaceOutlineLevel, visiblePlanWorkspaceRows } from "@features/projects/lib/plan-workspace-rows";

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

  it("assigns Microsoft Project-style hierarchical outline numbers", () => {
    const root = baseWbs({ id: "wbs-1", code: "T-AAA", sortOrder: 0, activityId: "activity-1", children: [
      baseWbs({ id: "wbs-2", code: "T-BBB", parentWbsId: "wbs-1", sortOrder: 0, activityId: "activity-2" }),
      baseWbs({ id: "wbs-3", code: "T-CCC", parentWbsId: "wbs-1", sortOrder: 1, activityId: "activity-3", children: [
        baseWbs({ id: "wbs-4", code: "T-DDD", parentWbsId: "wbs-3", sortOrder: 0, activityId: "activity-4" }),
      ] }),
    ] });
    const second = baseWbs({ id: "wbs-5", code: "T-EEE", sortOrder: 1, activityId: "activity-5" });
    const rows = buildPlanWorkspaceRows([root, second], ["activity-1", "activity-2", "activity-3", "activity-4", "activity-5"].map((id) => baseActivity({ id, wbsId: `wbs-${Number(id.slice(-1))}` })));

    const byId = new Map(rows.map((row) => [row.activity?.id ?? row.wbs?.id, row]));
    expect(byId.get("activity-1")?.outlineNumber).toBe("1");
    expect(byId.get("activity-2")?.outlineNumber).toBe("1.1");
    expect(byId.get("activity-3")?.outlineNumber).toBe("1.2");
    expect(byId.get("activity-4")?.outlineNumber).toBe("1.2.1");
    expect(byId.get("activity-5")?.outlineNumber).toBe("2");
  });

  it("collapses WBS summaries below the selected outline level", () => {
    const root = baseWbs({ id: "wbs-1", activityId: "activity-1", children: [
      baseWbs({ id: "wbs-2", parentWbsId: "wbs-1", activityId: "activity-2", children: [
        baseWbs({ id: "wbs-3", parentWbsId: "wbs-2", activityId: "activity-3" }),
      ] }),
    ] });
    const rows = buildPlanWorkspaceRows([root], [
      baseActivity({ id: "activity-1", wbsId: "wbs-1" }),
      baseActivity({ id: "activity-2", wbsId: "wbs-2" }),
      baseActivity({ id: "activity-3", wbsId: "wbs-3" }),
    ]);

    const levelTwo = collapsePlanWorkspaceToOutlineLevel(rows, 2);
    expect([...levelTwo]).toEqual(["wbs-2"]);
    expect(getPlanWorkspaceOutlineLevel(rows, levelTwo)).toBe("2");
    expect(visiblePlanWorkspaceRows(rows, levelTwo).map((row) => row.wbs?.id)).toEqual(["wbs-1", "wbs-2"]);
  });

  it("marks rows with children as summary rows and extra linked activities as nested outline numbers", () => {
    const parent = baseWbs({ id: "wbs-1", activityId: "activity-1", children: [baseWbs({ id: "wbs-2", parentWbsId: "wbs-1", activityId: "activity-2" })] });
    const extra = baseActivity({ id: "activity-3", wbsId: "wbs-1" });
    const summaryTask = baseActivity({ id: "activity-4", isSummaryTask: true });
    const rows = buildPlanWorkspaceRows([parent], [baseActivity({ id: "activity-1", wbsId: "wbs-1" }), baseActivity({ id: "activity-2", wbsId: "wbs-2" }), extra, summaryTask]);

    expect(rows.find((row) => row.wbs?.id === "wbs-1")?.isSummary).toBe(true);
    expect(rows.find((row) => row.wbs?.id === "wbs-2")?.isSummary).toBe(false);
    expect(rows.find((row) => row.activity?.id === "activity-4")?.isSummary).toBe(true);
    const extraRow = rows.find((row) => row.activity?.id === "activity-3");
    expect(extraRow?.outlineNumber).toBe("1.1");
    expect(extraRow?.depth).toBe(1);
  });
});
