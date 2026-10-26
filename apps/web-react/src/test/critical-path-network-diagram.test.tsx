import * as React from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { CriticalPathNetworkDiagram } from "@features/projects/components/CriticalPathNetworkDiagram";
import type { PlanActivityDto, PlanDependencyDto, PlanSchedulingAnalysisDto } from "@features/projects/api/planningFull";

vi.mock("@xyflow/react", async () => {
  const ReactModule = await import("react");
  return {
    Background: () => null,
    Controls: () => null,
    Handle: () => null,
    MarkerType: { ArrowClosed: "arrowclosed" },
    Position: { Left: "left", Right: "right" },
    getSmoothStepPath: ({ sourceX, sourceY, targetX, targetY }: { sourceX: number; sourceY: number; targetX: number; targetY: number }) => {
      const middle = (sourceX + targetX) / 2;
      return [`M ${sourceX},${sourceY} H ${middle} V ${targetY} H ${targetX}`, middle, (sourceY + targetY) / 2];
    },
    ReactFlowProvider: ({ children }: { children: React.ReactNode }) => ReactModule.createElement(ReactModule.Fragment, null, children),
    useNodesState: (initial: unknown[]) => {
      const [nodes, setNodes] = ReactModule.useState(initial);
      return [nodes, setNodes, () => undefined];
    },
    useEdgesState: (initial: unknown[]) => {
      const [edges, setEdges] = ReactModule.useState(initial);
      return [edges, setEdges, () => undefined];
    },
    useReactFlow: () => ({ fitView: vi.fn(), setCenter: vi.fn(), zoomIn: vi.fn(), zoomOut: vi.fn() }),
    ReactFlow: ({ nodes = [], edges = [], nodeTypes = {}, onNodeClick }: {
      nodes?: Array<{ id: string; type: string; position: { x: number; y: number }; data: Record<string, unknown> }>;
      edges?: Array<{ id: string; label?: string }>;
      nodeTypes?: Record<string, React.ComponentType<Record<string, unknown>>>;
      onNodeClick?: (event: unknown, node: { id: string }) => void;
    }) => <div data-testid="critical-path-canvas">
      {nodes.map((node) => {
        const Renderer = nodeTypes[node.type];
        return <div key={node.id} data-testid={`flow-node-${node.id}`}>
          <button type="button" onClick={() => onNodeClick?.({}, node)}>{node.id}</button>
          {Renderer ? <Renderer {...node} selected={false} dragging={false} zIndex={1} isConnectable={false} width={208} height={176} /> : null}
        </div>;
      })}
      <div data-testid="flow-edges">{edges.map((edge) => <span key={edge.id}>{edge.label}</span>)}</div>
    </div>,
  };
});

const activity = (id: string, type: PlanActivityDto["type"] = "Task"): PlanActivityDto => ({
  id, planVersionId: "version-1", code: id.toUpperCase(), name: `${id} activity`, type,
  duration: type === "Milestone" ? 0 : 1, startDate: "2026-01-05T08:00:00Z", endDate: "2026-01-05T17:00:00Z",
  progress: 45, critical: false, rowVersion: "version",
});

describe("mounted critical path network view", () => {
  it("renders API-backed task/milestone nodes and directional dependency labels with task actions", async () => {
    const tasks = [activity("task-a"), activity("mile-1", "Milestone")];
    const dependencies: PlanDependencyDto[] = [{ id: "link-a-m", planVersionId: "version-1", predecessorActivityId: "task-a", activityId: "mile-1", type: "FinishToStart", lag: 1, rowVersion: "version" }];
    const analysis: PlanSchedulingAnalysisDto = {
      planVersionId: "version-1", hasCycle: false, criticalPathActivityIds: ["task-a", "mile-1"], criticalPathDependencyIds: ["link-a-m"],
      activities: tasks.map((task) => ({ activityId: task.id, totalSlack: 0, critical: true, earlyStart: task.startDate, earlyFinish: task.endDate, lateStart: task.startDate, lateFinish: task.endDate })),
    };
    const onOpenDetails = vi.fn();
    const onOpenInGrid = vi.fn();
    const t = (key: string, params?: Record<string, string | number>) => params ? `${key} ${params.count ?? ""}` : key;

    render(<I18nProvider><CriticalPathNetworkDiagram projectId="project-1" projectName="Test Project" projectCode="PRJ-1" planId="plan-1" planName="Working Plan" workingVersionId="version-1" workingVersionNumber={1} activities={tasks} dependencies={dependencies} wbs={[]} analysis={analysis} isLoading={false} isError={false} onRetry={vi.fn()} onOpenDetails={onOpenDetails} onOpenInGrid={onOpenInGrid} t={t} /></I18nProvider>);

    expect(await screen.findByTestId("critical-path-milestone-node")).toHaveTextContent("mile-1 activity");
    expect(screen.getByTestId("critical-path-milestone-shape").firstElementChild?.className).toContain("rotate-45");
    expect(within(screen.getByTestId("flow-edges")).getByText("FinishToStart +1")).toBeInTheDocument();
    fireEvent.click(within(screen.getByTestId("flow-node-task-a")).getByRole("button", { name: "task-a" }));
    fireEvent.click(await screen.findByRole("button", { name: "projects.planning.network.openDetails" }));
    expect(onOpenDetails).toHaveBeenCalledWith(tasks[0]);
    fireEvent.click(screen.getByRole("button", { name: "projects.planning.network.openInGrid" }));
    expect(onOpenInGrid).toHaveBeenCalledWith("task-a");
  });

  it("opens the mounted full-graph export configuration from the Critical Path toolbar", async () => {
    const tasks = [activity("task-a"), activity("task-b")];
    const links: PlanDependencyDto[] = [{ id: "link-a-b", planVersionId: "version-1", predecessorActivityId: "task-a", activityId: "task-b", type: "FinishToStart", lag: 0, rowVersion: "version" }];
    const analysis: PlanSchedulingAnalysisDto = {
      planVersionId: "version-1", hasCycle: false, criticalPathActivityIds: ["task-a", "task-b"], criticalPathDependencyIds: ["link-a-b"],
      activities: tasks.map((task) => ({ activityId: task.id, totalSlack: 0, critical: true })),
    };
    const t = (key: string) => key.startsWith("planning.") ? `projects.${key}` : key;
    render(<I18nProvider><CriticalPathNetworkDiagram projectId="project-1" projectName="Test Project" projectCode="PRJ-1" planId="plan-1" planName="Working Plan" workingVersionId="version-1" workingVersionNumber={1} activities={tasks} dependencies={links} wbs={[]} analysis={analysis} isLoading={false} isError={false} onRetry={vi.fn()} onOpenDetails={vi.fn()} onOpenInGrid={vi.fn()} t={t} /></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: "projects.planning.exportDiagram.open" }));
    const dialog = await screen.findByRole("dialog", { name: "projects.planning.exportDiagram.title" });
    expect(dialog.querySelector(".absolute.inset-y-0.end-0")).toBeInTheDocument();
    expect(within(dialog).getByText("projects.planning.exportDiagram.fullNetwork")).toBeInTheDocument();
    expect(within(dialog).getByText("projects.planning.exportDiagram.criticalOnly")).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "projects.planning.exportDiagram.export" })).toBeEnabled();
    expect(within(dialog).getByRole("radio", { name: /projects\.planning\.exportDiagram\.pdf/ })).toBeChecked();
    expect(within(dialog).getByLabelText("projects.planning.exportDiagram.showDependencyLabels")).toBeChecked();
    expect(within(dialog).getByLabelText("projects.planning.exportDiagram.pageSize")).toHaveValue("AUTO");
    expect(within(dialog).getByLabelText("projects.planning.exportDiagram.pdfLayout")).toHaveValue("fit");
    expect(within(dialog).getByRole("option", { name: "A0" })).toBeInTheDocument();
  });
});
