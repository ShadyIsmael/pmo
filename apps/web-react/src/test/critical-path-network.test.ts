import { describe, expect, it } from "vitest";
import { buildCriticalPathNetworkGraph } from "@features/projects/lib/critical-path-network";
import type { PlanActivityDto, PlanDependencyDto, PlanSchedulingAnalysisDto, PlanWbsNodeDto } from "@features/projects/api/planningFull";

const activity = (id: string, type: PlanActivityDto["type"] = "Task", dates = true): PlanActivityDto => ({
  id,
  planVersionId: "version-1",
  code: id.toUpperCase(),
  name: `Task ${id}`,
  type,
  duration: type === "Milestone" ? 0 : 1,
  startDate: dates ? "2026-01-05T08:00:00Z" : undefined,
  endDate: dates ? "2026-01-05T17:00:00Z" : undefined,
  critical: false,
  rowVersion: "version",
});

const dependency = (id: string, predecessorActivityId: string, activityId: string): PlanDependencyDto => ({
  id,
  planVersionId: "version-1",
  predecessorActivityId,
  activityId,
  type: "FinishToStart",
  rowVersion: "version",
});

const labels = {
  task: "Task", milestone: "Milestone", summary: "Summary", duration: "Duration", start: "Start", finish: "Finish",
  totalFloat: "Total Float", progress: "Progress", earlyStart: "Early Start", earlyFinish: "Early Finish",
  lateStart: "Late Start", lateFinish: "Late Finish", days: "days",
};

function build(activities: PlanActivityDto[], dependencies: PlanDependencyDto[], analysis?: PlanSchedulingAnalysisDto, wbs: PlanWbsNodeDto[] = []) {
  return buildCriticalPathNetworkGraph({
    activities,
    dependencies,
    wbs,
    analysis,
    highlightCritical: true,
    highlightSelectedDependencies: true,
    textDirection: "ltr",
    dependencyLabel: (type) => type,
    labels,
    formatDate: (value) => value.slice(0, 10),
    formatNumber: (value) => String(value),
  });
}

describe("critical path network mapping", () => {
  it("maps persisted tasks, milestone diamonds, WBS labels, and only backend-identified critical links", () => {
    const a = activity("a");
    const b = activity("b");
    const milestone = activity("m", "Milestone");
    const short = activity("short");
    const links = [dependency("a-b", "a", "b"), dependency("b-m", "b", "m"), dependency("short-m", "short", "m")];
    const analysis: PlanSchedulingAnalysisDto = {
      planVersionId: "version-1",
      hasCycle: false,
      criticalPathActivityIds: ["a", "b", "m"],
      criticalPathDependencyIds: ["a-b", "b-m"],
      activities: [
        { activityId: "a", earlyStart: "2026-01-05T08:00:00Z", earlyFinish: "2026-01-05T17:00:00Z", lateStart: "2026-01-05T08:00:00Z", lateFinish: "2026-01-05T17:00:00Z", totalSlack: 0, critical: true },
        { activityId: "b", totalSlack: 0, critical: true },
        { activityId: "m", totalSlack: 0, critical: true },
        { activityId: "short", totalSlack: 1, critical: false },
      ],
    };
    const wbs: PlanWbsNodeDto[] = [{ id: "wbs-root", planVersionId: "version-1", sortOrder: 1, code: "1", name: "Delivery", rowVersion: "version", children: [{ id: "wbs-child", planVersionId: "version-1", parentWbsId: "wbs-root", sortOrder: 1, code: "1.2", name: "Build", rowVersion: "version", children: [] }] }];
    a.wbsId = "wbs-child";

    const graph = build([a, b, milestone, short], links, analysis, wbs);

    expect(graph.analysisUsable).toBe(true);
    expect(graph.nodes.find((node) => node.id === "m")?.type).toBe("milestone");
    expect(graph.nodes.find((node) => node.id === "a")?.data.wbsPath).toBe("1 › 1.2");
    expect(graph.nodes.find((node) => node.id === "short")?.data.critical).toBe(false);
    expect(graph.edges.find((edge) => edge.id === "a-b")?.data?.critical).toBe(true);
    expect(graph.edges.find((edge) => edge.id === "short-m")?.data?.critical).toBe(false);
    expect(graph.nodes.find((node) => node.id === "a")!.position.x).toBeLessThan(graph.nodes.find((node) => node.id === "b")!.position.x);
  });

  it("keeps invalid networks visible but suppresses fabricated critical styling and reports diagnostics", () => {
    const first = activity("a");
    const unscheduled = activity("b", "Task", false);
    const cycleAnalysis: PlanSchedulingAnalysisDto = { planVersionId: "version-1", hasCycle: true, criticalPathActivityIds: [], criticalPathDependencyIds: [], activities: [] };

    const cycle = build([first, activity("c")], [dependency("a-c", "a", "c")], cycleAnalysis);
    const incomplete = build([first, unscheduled], [], { ...cycleAnalysis, hasCycle: false });
    const orphan = build([first, activity("c")], [dependency("orphan", "outside", "c")], { ...cycleAnalysis, hasCycle: false });

    expect(cycle.nodes).toHaveLength(2);
    expect(cycle.analysisUsable).toBe(false);
    expect(cycle.nodes.every((node) => !node.data.critical)).toBe(true);
    expect(incomplete.hasUnscheduledActivities).toBe(true);
    expect(incomplete.hasMissingDependencyData).toBe(true);
    expect(incomplete.analysisUsable).toBe(false);
    expect(orphan.orphanDependencyIds).toEqual(["orphan"]);
    expect(orphan.edges).toHaveLength(0);
    expect(orphan.analysisUsable).toBe(false);
  });
});
