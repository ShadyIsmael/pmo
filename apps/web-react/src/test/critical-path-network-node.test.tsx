import * as React from "react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ActivityNodeContent } from "@features/projects/components/CriticalPathNetworkDiagram";
import type { CriticalPathActivityNodeData } from "@features/projects/lib/critical-path-network";
import type { PlanActivityDto } from "@features/projects/api/planningFull";

vi.mock("@xyflow/react", async () => {
  const ReactModule = await import("react");
  return {
    Background: () => null,
    Controls: () => null,
    Handle: () => null,
    Position: { Left: "left", Right: "right" },
    ReactFlow: () => null,
    ReactFlowProvider: ({ children }: { children: React.ReactNode }) => ReactModule.createElement(ReactModule.Fragment, null, children),
    useEdgesState: (edges: unknown) => [edges, vi.fn(), vi.fn()],
    useNodesState: (nodes: unknown) => [nodes, vi.fn(), vi.fn()],
    useReactFlow: () => ({ fitView: vi.fn(), setCenter: vi.fn(), zoomIn: vi.fn(), zoomOut: vi.fn() }),
  };
});

describe("critical path network nodes", () => {
  it("renders a milestone as a diamond and exposes its saved schedule metrics", () => {
    const activity: PlanActivityDto = {
      id: "milestone-1", planVersionId: "version-1", code: "M-01", name: "Go live", type: "Milestone",
      duration: 0, startDate: "2026-01-07", endDate: "2026-01-07", progress: 100, critical: true, rowVersion: "version",
    };
    const data: CriticalPathActivityNodeData = {
      activity,
      metrics: { activityId: activity.id, totalSlack: 0, freeSlack: 0, critical: true, earlyStart: "2026-01-07", earlyFinish: "2026-01-07", lateStart: "2026-01-07", lateFinish: "2026-01-07" },
      critical: true,
      highlightCritical: true,
      selectedDependencies: false,
      scheduleTooltip: "ES: Jan 7 · EF: Jan 7 · LS: Jan 7 · LF: Jan 7",
      textDirection: "ltr",
      labels: {
        task: "Task", milestone: "Milestone", summary: "Summary", duration: "Duration", start: "Start", finish: "Finish",
        totalFloat: "Total Float", progress: "Progress", earlyStart: "Early Start", earlyFinish: "Early Finish",
        lateStart: "Late Start", lateFinish: "Late Finish", days: "days",
      },
      formatDate: (value) => value,
      formatNumber: String,
    };
    const props = {
      id: activity.id, type: "milestone", position: { x: 0, y: 0 }, data, selected: false, dragging: false,
      zIndex: 1, isConnectable: false, width: 208, height: 176,
    } as unknown as React.ComponentProps<typeof ActivityNodeContent>;

    render(<ActivityNodeContent {...props} milestone />);

    expect(screen.getByTestId("critical-path-milestone-node")).toHaveTextContent("Milestone");
    expect(screen.getByTestId("critical-path-milestone-node")).toHaveTextContent("Go live");
    expect(screen.getByTestId("critical-path-milestone-node")).toHaveTextContent("Total Float");
    expect(screen.getByTestId("critical-path-milestone-shape").firstElementChild?.className).toContain("rotate-45");
    expect(screen.getByTestId("critical-path-milestone-node")).toHaveAttribute("title", expect.stringContaining("ES: Jan 7"));
  });
});
