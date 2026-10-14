import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { WorkflowVersionDetails } from "../features/workflow/api/workflowsFull";
import { WorkflowFlowDesignerFull } from "../features/workflow/WorkflowFlowDesignerFull";

type MockNode = { id: string; data: { state: { nameEn: string } } };
type MockFlowProps = {
  nodes: MockNode[];
  nodesConnectable?: boolean;
  onNodeClick?: (event: React.MouseEvent<HTMLButtonElement>, node: MockNode) => void;
  onConnect?: (connection: { source: string | null; target: string | null }) => void;
};

vi.mock("@xyflow/react", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@xyflow/react")>();
  return {
    ...actual,
    ReactFlow: (props: MockFlowProps) => (
      <div data-testid="workflow-flow" data-connectable={props.nodesConnectable ? "true" : "false"}>
        {props.nodes.map((node) => (
          <button key={node.id} type="button" onClick={(event) => props.onNodeClick?.(event, node)}>
            {node.data.state.nameEn}
          </button>
        ))}
        <button type="button" onClick={() => props.onConnect?.({ source: "state-open", target: "state-closed" })}>Create connection</button>
      </div>
    ),
  };
});

const version: WorkflowVersionDetails = {
  id: "version-1",
  workflowDefinitionId: "workflow-1",
  versionNumber: 1,
  lifecycleStatus: "Draft",
  createdAt: "2026-10-08T00:00:00Z",
  createdBy: "user-1",
  states: [
    { id: "state-open", code: "OPEN", nameEn: "Open", nameAr: "مفتوحة", displayOrder: 1, isInitial: true, isFinal: false, isActive: true },
    { id: "state-closed", code: "CLOSED", nameEn: "Closed", nameAr: "مغلقة", displayOrder: 2, isInitial: false, isFinal: true, isActive: true },
  ],
  transitions: [],
};

const labels = {
  title: "Workflow Designer",
  hint: "Drag states and connect them visually.",
  connect: "Connect states",
  autoLayout: "Auto layout",
  fitView: "Fit view",
  resetLayout: "Reset layout",
  connectHint: "Drag from a source handle to the target state.",
  connectFailed: "Could not create this transition. Check the workflow states and try again.",
  initial: "Initial",
  terminal: "Terminal",
  normalState: "Normal state",
};

function renderDesigner(onSelectState = vi.fn(), onConnectStates = vi.fn().mockResolvedValue(undefined)) {
  return render(
    <WorkflowFlowDesignerFull
      version={version}
      locale="en"
      editable
      labels={labels}
      onSelectState={onSelectState}
      onSelectTransition={vi.fn()}
      onConnectStates={onConnectStates}
    />,
  );
}

describe("workflow flow designer interactions", () => {
  it("does not open a state when clicking it in connect mode", () => {
    const onSelectState = vi.fn();
    renderDesigner(onSelectState);

    fireEvent.click(screen.getByRole("button", { name: "Connect states" }));
    expect(screen.getByTestId("workflow-flow")).toHaveAttribute("data-connectable", "true");
    fireEvent.click(screen.getByRole("button", { name: "Open" }));

    expect(onSelectState).not.toHaveBeenCalled();
  });

  it("opens the state normally when connect mode is off", () => {
    const onSelectState = vi.fn();
    renderDesigner(onSelectState);

    fireEvent.click(screen.getByRole("button", { name: "Open" }));

    expect(onSelectState).toHaveBeenCalledWith("state-open");
    expect(screen.getByTestId("workflow-flow")).toHaveAttribute("data-connectable", "false");
  });

  it("creates the transition and exits connect mode after a successful connection", async () => {
    const onConnectStates = vi.fn().mockResolvedValue(undefined);
    renderDesigner(vi.fn(), onConnectStates);

    fireEvent.click(screen.getByRole("button", { name: "Connect states" }));
    fireEvent.click(screen.getByRole("button", { name: "Create connection" }));

    await waitFor(() => expect(onConnectStates).toHaveBeenCalledWith("state-open", "state-closed"));
    await waitFor(() => expect(screen.getByTestId("workflow-flow")).toHaveAttribute("data-connectable", "false"));
  });

  it("shows a localized recoverable error when saving the transition fails", async () => {
    const onConnectStates = vi.fn().mockRejectedValue(new Error("request failed"));
    renderDesigner(vi.fn(), onConnectStates);

    fireEvent.click(screen.getByRole("button", { name: "Connect states" }));
    fireEvent.click(screen.getByRole("button", { name: "Create connection" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(labels.connectFailed);
    expect(screen.getByTestId("workflow-flow")).toHaveAttribute("data-connectable", "true");
  });
});
