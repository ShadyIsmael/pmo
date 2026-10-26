import { describe, expect, it } from "vitest";
import type { PlanActivityDto, PlanDependencyDto, PlanWbsNodeDto } from "@features/projects/api/planningFull";
import { buildPlanningExportRows, type PlanningExportInput } from "@features/projects/lib/planning-export";

const first: PlanActivityDto = {
  id: "activity-secret-guid-a",
  planVersionId: "version-1",
  code: "ACT-01",
  name: "Design",
  type: "Task",
  schedulingMode: "Manual",
  startDate: "2026-02-03T00:00:00Z",
  endDate: "2026-02-03T00:00:00Z",
  duration: 1,
  progress: 25,
  critical: false,
  rowVersion: "row-a",
  wbsId: "wbs-a",
};
const second: PlanActivityDto = {
  id: "activity-secret-guid-b",
  planVersionId: "version-1",
  code: "ACT-02",
  name: "Build",
  type: "Milestone",
  constraintType: "StartNoEarlierThan",
  constraintDate: "2026-02-06T00:00:00Z",
  deadline: "2026-02-10T00:00:00Z",
  duration: 0,
  progress: 0,
  critical: true,
  rowVersion: "row-b",
  wbsId: "wbs-b",
};
const nodes: PlanWbsNodeDto[] = [
  { id: "wbs-a", planVersionId: "version-1", sortOrder: 0, code: "1", name: "Design phase", activityId: first.id, rowVersion: "wbs-a-v1", children: [] },
  { id: "wbs-b", planVersionId: "version-1", sortOrder: 1, code: "2", name: "Build phase", activityId: second.id, rowVersion: "wbs-b-v1", children: [] },
];
const dependencies: PlanDependencyDto[] = [
  { id: "dependency-secret-guid", planVersionId: "version-1", activityId: second.id, predecessorActivityId: first.id, type: "FinishToStart", lag: 2, rowVersion: "dependency-v1" },
];
const labels: PlanningExportInput["labels"] = {
  planInfoSheet: "planInfoSheet", tasksSheet: "tasksSheet", project: "project", projectCode: "projectCode", plan: "plan", planType: "planType", workingVersion: "workingVersion",
  outline: "outline", taskId: "taskId", taskName: "taskName", taskType: "taskType", schedulingMode: "schedulingMode", start: "start", finish: "finish", duration: "duration",
  progress: "progress", predecessors: "predecessors", constraint: "constraint", constraintDate: "constraintDate", deadline: "deadline", workPackage: "workPackage", task: "task", milestone: "milestone", summary: "summary",
};

describe("planning workbook export rows", () => {
  it("exports human-readable task codes and uses the grid row numbers for predecessors", () => {
    const rows = buildPlanningExportRows({
      project: { name: "NEXUS", code: "PRJ-1001" },
      plan: { name: "Scenario", planType: "Scenario" },
      workingVersionNumber: 3,
      wbs: nodes,
      activities: [first, second],
      dependencies,
      labels,
      localizeValue: (_group, value) => `localized:${value}`,
    });

    expect(rows).toHaveLength(2);
    expect(rows[0]).toMatchObject({ outline: "1", taskId: "ACT-01", taskName: "Design", schedulingMode: "localized:Manual", progress: 25 });
    expect(rows[0]?.start?.toISOString()).toBe("2026-02-03T00:00:00.000Z");
    expect(rows[1]).toMatchObject({ outline: "2", taskId: "ACT-02", taskName: "Build", taskType: "localized:Milestone", predecessors: "1FS+2d", constraint: "localized:StartNoEarlierThan" });
    expect(rows[1]?.constraintDate?.toISOString()).toBe("2026-02-06T00:00:00.000Z");
    expect(JSON.stringify(rows)).not.toContain("secret-guid");
  });

  it("keeps date fields blank when source values are absent or invalid", () => {
    const invalidDateActivity = { ...first, id: "activity-c", startDate: "not-a-date", endDate: undefined };
    const rows = buildPlanningExportRows({
      project: { name: "NEXUS", code: "PRJ-1001" },
      plan: { name: "Scenario", planType: "Scenario" },
      workingVersionNumber: 1,
      wbs: [],
      activities: [invalidDateActivity],
      dependencies: [],
      labels,
      localizeValue: (_group, value) => value,
    });

    expect(rows[0]?.start).toBeUndefined();
    expect(rows[0]?.finish).toBeUndefined();
  });
});
