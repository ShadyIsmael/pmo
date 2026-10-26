import { fireEvent, render, screen } from "@testing-library/react";
import type * as React from "react";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import type { PlanWorkspaceRow } from "@features/projects/lib/plan-workspace-rows";
import { PlanTimelineGantt } from "@features/projects/components/PlanTimelineGantt";

const row = (overrides: Partial<PlanWorkspaceRow> & Pick<PlanWorkspaceRow, "id">): PlanWorkspaceRow => ({
  rowNumber: 1,
  depth: 0,
  hasChildren: false,
  isSummary: false,
  ...overrides,
});

const activityRow = (id: string, startDate: string, endDate: string, overrides: Partial<PlanWorkspaceRow> = {}) => row({
  id: `activity:${id}`,
  activity: { id, planVersionId: "plan-1", code: id, name: `Task ${id}`, type: "Task", duration: 2, critical: false, rowVersion: `v-${id}`, startDate, endDate, progress: 40 },
  ...overrides,
});

describe("plan timeline Gantt", () => {
  const baseProps = { dependencies: [], selectedId: "", onSelect: vi.fn() };
  const renderGantt = (ui: React.ReactElement) => render(ui, { wrapper: ({ children }: { children: React.ReactNode }) => <I18nProvider>{children}</I18nProvider> });

  it("renders summary rows as MS Project-style bracket bars with fallback descendant dates", () => {
    const summary = row({
      id: "wbs:parent",
      depth: 0,
      hasChildren: true,
      isSummary: true,
      wbs: { id: "parent", planVersionId: "plan-1", sortOrder: 0, code: "1", name: "Phase", rowVersion: "w1", children: [] },
    });
    const child = activityRow("child-1", "2026-03-02", "2026-03-06", { depth: 1 });

    renderGantt(<PlanTimelineGantt {...baseProps} rows={[summary, child]} />);

    const bar = screen.getByTestId("gantt-summary-bar");
    expect(bar).toBeInTheDocument();
    expect(bar.querySelector("span")?.className).toContain("w-[3px]");
    const rowEl = screen.getAllByTestId("planning-gantt-row").find((el) => el.getAttribute("data-row-kind") === "summary");
    expect(rowEl).toBeDefined();
  });

  it("centers milestone diamonds on their start day", () => {
    const milestone = row({
      id: "activity:m1",
      activity: { id: "m1", planVersionId: "plan-1", code: "M1", name: "Kickoff", type: "Milestone", duration: 0, critical: false, rowVersion: "v1", startDate: "2026-03-05" },
    });

    renderGantt(<PlanTimelineGantt {...baseProps} rows={[milestone]} />);

    expect(screen.getByTestId("gantt-milestone")).toBeInTheDocument();
    expect(screen.getAllByTestId("planning-gantt-row").some((el) => el.getAttribute("data-row-kind") === "milestone")).toBe(true);
  });

  it("draws an inner progress fill on task bars", () => {
    renderGantt(<PlanTimelineGantt {...baseProps} rows={[activityRow("t1", "2026-03-02", "2026-03-05")]} />);

    const bar = screen.getByTestId("gantt-task-bar");
    expect(bar.querySelector("span")).toHaveStyle({ width: "40%" });
  });

  it("hides the identity panel in split view but keeps it by default", () => {
    const task = activityRow("t1", "2026-03-02", "2026-03-05");

    const { rerender } = renderGantt(<PlanTimelineGantt {...baseProps} rows={[task]} />);
    expect(screen.getAllByTestId("planning-gantt-row").length).toBe(2);

    rerender(<PlanTimelineGantt {...baseProps} rows={[task]} showIdentityPanel={false} />);
    expect(screen.getAllByTestId("planning-gantt-row").length).toBe(1);
  });

  it("selects a row when its timeline cell is clicked", () => {
    const onSelect = vi.fn();
    const task = activityRow("t1", "2026-03-02", "2026-03-05");

    renderGantt(<PlanTimelineGantt {...baseProps} rows={[task]} selectedId="" onSelect={onSelect} />);

    fireEvent.click(screen.getByTestId("gantt-task-bar").closest("button")!);
    expect(onSelect).toHaveBeenCalledWith("activity:t1");
  });
});
