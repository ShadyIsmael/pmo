import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type * as React from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { planningApiFull, usePlanningAssignmentsFull, type PlanActivityDto, type PlanAssignmentDto, type PlanDependencyDto, type PlanWbsNodeDto } from "@features/projects/api/planningFull";
import { PlanningGrid } from "@features/projects/components/PlanningGrid";
import { ConfirmProvider } from "@components/ui/ConfirmProvider";
import { I18nProvider } from "@i18n/messages";
import { PLAN_ROW_HEIGHT } from "@features/projects/lib/gantt-constants";

vi.mock("@features/projects/api/planningFull", () => ({
  usePlanningAssignmentsFull: vi.fn(() => ({ data: [], isSuccess: true, isError: false })),
  planningApiFull: { activities: vi.fn(async () => []) },
  usePlanningResourcesFull: () => ({ data: [] }),
  usePlanningCalendarsFull: () => ({ data: [] }),
  usePlanningTablePreferencesFull: () => ({ data: undefined }),
  useSavePlanningTablePreferencesFull: () => ({ mutateAsync: vi.fn().mockResolvedValue(undefined) }),
  useResetPlanningTablePreferencesFull: () => ({ isPending: false, mutateAsync: vi.fn().mockResolvedValue(undefined) }),
}));

const t = (key: string) => key;
const activity = (id: string, wbsId: string): PlanActivityDto => ({ id, planVersionId: "plan-1", code: id, name: `Task ${id}`, type: "Task", duration: 1, critical: false, rowVersion: `row-${id}`, wbsId });
const wbsNode = (id: string, activityId: string, sortOrder: number): PlanWbsNodeDto => ({ id, planVersionId: "plan-1", parentWbsId: undefined, sortOrder, code: id, name: `WBS ${id}`, activityId, rowVersion: `wbs-${id}`, children: [] });
const makeMutations = () => ({
  updateActivity: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, bulkUpdateActivities: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, deleteActivity: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, deleteAssignment: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, createActivity: { mutateAsync: vi.fn().mockImplementation(async () => activity("created-activity", "created-wbs")) },
  updateWbs: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, bulkUpdateWbsPlacements: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, createWbs: { mutateAsync: vi.fn().mockImplementation(async () => wbsNode("created-wbs", "created-activity", 0)) }, deleteWbs: { mutateAsync: vi.fn().mockResolvedValue(undefined) },
  createDependency: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, deleteDependency: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, replacePredecessorsBulk: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, moveActivity: { mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false },
});

describe("planning grid bulk dependency commands", () => {
  it("links selected activity rows in order through one atomic predecessor update", async () => {
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-first", first.id, 0), wbsNode("wbs-second", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);

    const rows = screen.getAllByTestId("planning-grid-row");
    fireEvent.click(rows[0]!);
    fireEvent.click(rows[1]!, { ctrlKey: true });
    fireEvent.click(screen.getByRole("button", { name: "planning.linkSelected" }));

    await waitFor(() => expect(mutations.replacePredecessorsBulk.mutateAsync).toHaveBeenCalledWith([
      { activityId: second.id, predecessors: [{ predecessorActivityId: first.id, type: "FinishToStart", lag: 0 }] },
    ]));
  });

  it("unlinks only dependencies internal to the selection and retains external predecessors", async () => {
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    const external = activity("external", "wbs-external");
    const mutations = makeMutations();
    const dependencies: PlanDependencyDto[] = [
      { id: "internal", planVersionId: "plan-1", activityId: second.id, predecessorActivityId: first.id, type: "FinishToStart", rowVersion: "v1" },
      { id: "external-edge", planVersionId: "plan-1", activityId: second.id, predecessorActivityId: external.id, type: "StartToStart", rowVersion: "v2" },
    ];
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-first", first.id, 0), wbsNode("wbs-second", second.id, 1), wbsNode("wbs-external", external.id, 2)]} activities={[first, second, external]} dependencies={dependencies} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);

    const rows = screen.getAllByTestId("planning-grid-row");
    fireEvent.click(rows[0]!);
    fireEvent.click(rows[1]!, { ctrlKey: true });
    fireEvent.click(screen.getByRole("button", { name: "planning.unlinkSelected" }));

    await waitFor(() => expect(mutations.replacePredecessorsBulk.mutateAsync).toHaveBeenCalledWith([
      { activityId: second.id, predecessors: [{ predecessorActivityId: external.id, type: "StartToStart", lag: undefined }] },
    ]));
  });
});

describe("planning grid bulk hierarchy commands", () => {
  it("indents multiple selected task WBS nodes in one atomic placement request", async () => {
    const parent = activity("parent", "wbs-parent");
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[
      wbsNode("wbs-parent", parent.id, 0),
      wbsNode("wbs-first", first.id, 1),
      wbsNode("wbs-second", second.id, 2),
    ]} activities={[parent, first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);

    const rows = screen.getAllByTestId("planning-grid-row");
    fireEvent.click(rows[1]!);
    fireEvent.click(rows[2]!, { ctrlKey: true });
    fireEvent.click(screen.getByRole("button", { name: "planning.indent" }));

    await waitFor(() => expect(mutations.bulkUpdateWbsPlacements.mutateAsync).toHaveBeenCalledWith([
      { nodeId: "wbs-first", parentWbsId: "wbs-parent", sortOrder: 0, rowVersion: "wbs-wbs-first" },
      { nodeId: "wbs-second", parentWbsId: "wbs-parent", sortOrder: 1, rowVersion: "wbs-wbs-second" },
    ]));
    expect(mutations.updateWbs.mutateAsync).not.toHaveBeenCalled();
  });

  it("keeps a multi-row selection when a structural context-menu command is used", async () => {
    const parent = activity("parent", "wbs-parent");
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[
      wbsNode("wbs-parent", parent.id, 0),
      wbsNode("wbs-first", first.id, 1),
      wbsNode("wbs-second", second.id, 2),
    ]} activities={[parent, first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);

    const rows = screen.getAllByTestId("planning-grid-row");
    fireEvent.click(rows[1]!);
    fireEvent.click(rows[2]!, { ctrlKey: true });
    fireEvent.contextMenu(rows[2]!, { clientX: 280, clientY: 300 });
    fireEvent.click(screen.getByRole("menuitem", { name: "planning.indent" }));

    await waitFor(() => expect(mutations.bulkUpdateWbsPlacements.mutateAsync).toHaveBeenCalledWith([
      { nodeId: "wbs-first", parentWbsId: "wbs-parent", sortOrder: 0, rowVersion: "wbs-wbs-first" },
      { nodeId: "wbs-second", parentWbsId: "wbs-parent", sortOrder: 1, rowVersion: "wbs-wbs-second" },
    ]));
    expect(mutations.updateWbs.mutateAsync).not.toHaveBeenCalled();
  });

  it("outdents selected sibling WBS nodes in one atomic placement request", async () => {
    const parent = activity("parent", "wbs-parent");
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    const parentNode: PlanWbsNodeDto = {
      ...wbsNode("wbs-parent", parent.id, 0),
      children: [
        { ...wbsNode("wbs-first", first.id, 0), parentWbsId: "wbs-parent" },
        { ...wbsNode("wbs-second", second.id, 1), parentWbsId: "wbs-parent" },
      ],
    };
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[parentNode]} activities={[parent, first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);

    const rows = screen.getAllByTestId("planning-grid-row");
    fireEvent.click(rows[1]!);
    fireEvent.click(rows[2]!, { ctrlKey: true });
    fireEvent.click(screen.getByRole("button", { name: "planning.outdent" }));

    await waitFor(() => expect(mutations.bulkUpdateWbsPlacements.mutateAsync).toHaveBeenCalledWith([
      { nodeId: "wbs-first", parentWbsId: null, sortOrder: 1, rowVersion: "wbs-wbs-first" },
      { nodeId: "wbs-second", parentWbsId: null, sortOrder: 2, rowVersion: "wbs-wbs-second" },
    ]));
  });
});

describe("planning grid bulk scheduling commands", () => {
  it("changes scheduling mode for selected leaf activities through one atomic update", async () => {
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-first", first.id, 0), wbsNode("wbs-second", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);

    const rows = screen.getAllByTestId("planning-grid-row");
    fireEvent.click(rows[0]!);
    fireEvent.click(rows[1]!, { ctrlKey: true });
    fireEvent.click(screen.getByRole("button", { name: "planning.manualSelected" }));

    await waitFor(() => expect(mutations.bulkUpdateActivities.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({
      items: expect.arrayContaining([
        expect.objectContaining({ activityId: "first", update: expect.objectContaining({ schedulingMode: "Manual", rowVersion: "row-first" }) }),
        expect.objectContaining({ activityId: "second", update: expect.objectContaining({ schedulingMode: "Manual", rowVersion: "row-second" }) }),
      ]),
    })));
  });

  it("applies one selected progress value to all selected leaf activities", async () => {
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-first", first.id, 0), wbsNode("wbs-second", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);

    const rows = screen.getAllByTestId("planning-grid-row");
    fireEvent.click(rows[0]!);
    fireEvent.click(rows[1]!, { ctrlKey: true });
    fireEvent.click(screen.getByLabelText("planning.bulkProgress"));
    fireEvent.click(within(screen.getByRole("listbox")).getAllByRole("option")[2]!);
    fireEvent.click(screen.getByRole("button", { name: "planning.applyProgress" }));

    await waitFor(() => expect(mutations.bulkUpdateActivities.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({
      items: expect.arrayContaining([
        expect.objectContaining({ activityId: "first", update: expect.objectContaining({ progress: 50, rowVersion: "row-first" }) }),
        expect.objectContaining({ activityId: "second", update: expect.objectContaining({ progress: 50, rowVersion: "row-second" }) }),
      ]),
    })));
  });
});

beforeEach(() => {
  vi.mocked(usePlanningAssignmentsFull).mockReturnValue({ data: [], isSuccess: true, isError: false } as unknown as ReturnType<typeof usePlanningAssignmentsFull>);
  vi.mocked(planningApiFull.activities).mockResolvedValue([]);
});
afterEach(() => {
  cleanup();
  vi.mocked(usePlanningAssignmentsFull).mockReturnValue({ data: [], isSuccess: true, isError: false } as unknown as ReturnType<typeof usePlanningAssignmentsFull>);
  vi.mocked(planningApiFull.activities).mockResolvedValue([]);
});

function renderWithProviders(ui: React.ReactElement) {
  return render(ui, { wrapper: ({ children }: { children: React.ReactNode }) => <I18nProvider><ConfirmProvider translate={t}>{children}</ConfirmProvider></I18nProvider> });
}

describe("planning grid row and ribbon behavior", () => {
  it("keeps the task row intact when undo/redo shortcuts are unavailable", () => {
    const task = activity("task-1", "wbs-1");
    const { container } = renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);
    const grid = container.firstElementChild as HTMLElement;

    fireEvent.keyDown(grid, { key: "z", ctrlKey: true });
    fireEvent.keyDown(grid, { key: "y", ctrlKey: true });
    fireEvent.keyDown(grid, { key: "z", ctrlKey: true, shiftKey: true });

    expect(screen.getByText("Task task-1")).toBeInTheDocument();
  });

  it("selects an entire column and row with Ctrl+Space and Shift+Space", () => {
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    const { container } = renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-first", first.id, 0), wbsNode("wbs-second", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);
    const grid = container.firstElementChild as HTMLElement;
    const firstTaskName = screen.getByText("Task first").closest("td");
    fireEvent.mouseDown(firstTaskName!);
    fireEvent.keyDown(grid, { ctrlKey: true, code: "Space", key: " " });

    expect(screen.getAllByTestId("planning-grid-row")).toHaveLength(2);
    for (const row of screen.getAllByTestId("planning-grid-row")) expect(row).toHaveAttribute("aria-selected", "true");
    expect(container.querySelectorAll('[data-cell-selected="true"]')).toHaveLength(2);

    fireEvent.keyDown(grid, { shiftKey: true, code: "Space", key: " " });
    const visibleColumnCount = container.querySelectorAll("thead th").length - 1;
    expect(screen.getAllByTestId("planning-grid-row")[0]).toHaveAttribute("aria-selected", "true");
    expect(screen.getAllByTestId("planning-grid-row")[1]).toHaveAttribute("aria-selected", "false");
    expect(container.querySelectorAll('[data-cell-selected="true"]')).toHaveLength(visibleColumnCount);
  });

  it("selects cell ranges with Shift and extends a row selection from the keyboard", () => {
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-first", first.id, 0), wbsNode("wbs-second", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);
    const rows = screen.getAllByTestId("planning-grid-row");
    const firstCells = within(rows[0]!).getAllByRole("cell");
    const secondCells = within(rows[1]!).getAllByRole("cell");

    fireEvent.mouseDown(firstCells[3]!);
    expect(firstCells[3]).toHaveAttribute("aria-selected", "true");
    fireEvent.mouseDown(secondCells[5]!, { shiftKey: true });
    expect(firstCells[0]).toHaveAttribute("aria-selected", "false");
    expect(firstCells[3]).toHaveAttribute("aria-selected", "true");
    expect(firstCells[5]).toHaveAttribute("aria-selected", "true");
    expect(secondCells[5]).toHaveAttribute("aria-selected", "true");
    expect(rows[0]).toHaveAttribute("aria-selected", "true");
    expect(rows[1]).toHaveAttribute("aria-selected", "true");

    fireEvent.mouseDown(firstCells[5]!);
    const grid = screen.getAllByTestId("planning-grid-row")[0]!.closest("div[tabindex='0']");
    expect(grid).not.toBeNull();
    fireEvent.keyDown(grid!, { key: "ArrowDown", shiftKey: true });
    expect(secondCells[5]).toHaveAttribute("aria-selected", "true");
  });

  it("adds and removes discontiguous cells and rows with Control selection", () => {
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-first", first.id, 0), wbsNode("wbs-second", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);
    const rows = screen.getAllByTestId("planning-grid-row");
    const firstCells = within(rows[0]!).getAllByRole("cell");
    const secondCells = within(rows[1]!).getAllByRole("cell");
    fireEvent.mouseDown(firstCells[3]!);
    fireEvent.mouseDown(secondCells[5]!, { ctrlKey: true });
    expect(firstCells[3]).toHaveAttribute("aria-selected", "true");
    expect(secondCells[5]).toHaveAttribute("aria-selected", "true");
    expect(rows[0]).toHaveAttribute("aria-selected", "true");
    expect(rows[1]).toHaveAttribute("aria-selected", "true");
    fireEvent.mouseDown(secondCells[5]!, { ctrlKey: true });
    expect(secondCells[5]).toHaveAttribute("aria-selected", "false");
    expect(rows[1]).toHaveAttribute("aria-selected", "false");
  });

  it("extends a cell range while dragging across the grid", () => {
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-first", first.id, 0), wbsNode("wbs-second", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);
    const rows = screen.getAllByTestId("planning-grid-row");
    const firstCells = within(rows[0]!).getAllByRole("cell");
    const secondCells = within(rows[1]!).getAllByRole("cell");
    fireEvent.mouseDown(firstCells[3]!);
    fireEvent.mouseEnter(secondCells[5]!);
    expect(firstCells[3]).toHaveAttribute("aria-selected", "true");
    expect(secondCells[5]).toHaveAttribute("aria-selected", "true");
    expect(rows[0]).toHaveAttribute("aria-selected", "true");
    expect(rows[1]).toHaveAttribute("aria-selected", "true");
    fireEvent.mouseUp(window);
  });

  it("copies selected cells as tab-separated values and pastes a range through the bulk API", async () => {
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-first", first.id, 0), wbsNode("wbs-second", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);
    const rows = screen.getAllByTestId("planning-grid-row");
    const firstCells = within(rows[0]!).getAllByRole("cell");
    const secondCells = within(rows[1]!).getAllByRole("cell");
    fireEvent.mouseDown(firstCells[3]!);
    fireEvent.mouseDown(secondCells[3]!, { shiftKey: true });
    const grid = rows[0]!.closest("div[tabindex='0']")!;
    const setData = vi.fn();
    fireEvent.copy(grid, { clipboardData: { setData } });
    expect(setData).toHaveBeenCalledWith("text/plain", "Task first\nTask second");

    fireEvent.mouseDown(firstCells[4]!);
    fireEvent.paste(grid, { clipboardData: { getData: () => "3\n4" } });
    await waitFor(() => expect(mutations.bulkUpdateActivities.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ items: expect.arrayContaining([
      expect.objectContaining({ activityId: first.id, update: expect.objectContaining({ duration: 3 }) }),
      expect.objectContaining({ activityId: second.id, update: expect.objectContaining({ duration: 4 }) }),
    ]) })));
  });

  it("fills the selected duration range down through the bulk API", async () => {
    const first: PlanActivityDto = { ...activity("first", "wbs-first"), duration: 3 };
    const second: PlanActivityDto = { ...activity("second", "wbs-second"), duration: 1 };
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-first", first.id, 0), wbsNode("wbs-second", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);
    const rows = screen.getAllByTestId("planning-grid-row");
    const firstDuration = within(rows[0]!).getAllByRole("cell")[4]!;
    const secondDuration = within(rows[1]!).getAllByRole("cell")[4]!;
    fireEvent.mouseDown(firstDuration);
    fireEvent.mouseDown(secondDuration, { shiftKey: true });
    const grid = rows[0]!.closest("div[tabindex='0']")!;
    fireEvent.keyDown(grid, { key: "d", ctrlKey: true });
    await waitFor(() => expect(mutations.bulkUpdateActivities.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ items: expect.arrayContaining([
      expect.objectContaining({ activityId: second.id, update: expect.objectContaining({ duration: 3 }) }),
    ]) })));
  });

  it.each(["Automatic", "Manual"] as const)("validates a delayed Start according to %s scheduling", async (mode) => {
    const task: PlanActivityDto = { ...activity("date-task", "date-wbs"), schedulingMode: mode, startDate: "2026-01-15", endDate: "2026-01-15" };
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("date-wbs", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);
    const row = screen.getByText(task.name).closest("tr")!;
    fireEvent.doubleClick(within(row).getAllByText("2026-01-15")[0]);
    const editor = screen.getByPlaceholderText("dd/mm/yyyy");
    fireEvent.change(editor, { target: { value: "02/11/2026" } });
    fireEvent.keyDown(editor, { key: "Enter" });
    if (mode === "Automatic") {
      await waitFor(() => expect(mutations.updateActivity.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ body: expect.objectContaining({ startDate: "2026-11-02", schedulingMode: mode }) })));
    } else {
      expect(await screen.findByRole("alert")).toHaveTextContent("planning.invalidDateRange");
      expect(mutations.updateActivity.mutateAsync).not.toHaveBeenCalled();
    }
  });

  it("keeps the original planning row height and spacing", () => {
    const task = activity("task-1", "wbs-1");
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    const row = screen.getByText("Task task-1").closest("tr");
    expect(row).not.toBeNull();
    expect(row).toHaveStyle({ height: `${PLAN_ROW_HEIGHT}px` });
    expect(screen.getByText("Task task-1").closest("td")).toHaveClass("py-1.5");
  });

  it("renders the row context menu outside the clipped planning frame", () => {
    const task = activity("task-1", "wbs-1");
    const { getByTestId } = renderWithProviders(
      <div data-testid="planning-frame" className="w-32 overflow-hidden">
        <PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />
      </div>
    );

    fireEvent.contextMenu(screen.getByText("Task task-1"), { clientX: 280, clientY: 300 });

    const menu = screen.getByRole("menu");
    expect(menu.parentElement).toBe(document.body);
    expect(getByTestId("planning-frame")).not.toContainElement(menu);

    fireEvent.mouseDown(menu);
    expect(screen.getByRole("menu")).toBeInTheDocument();
    fireEvent.mouseDown(document.body);
    expect(screen.queryByRole("menu")).not.toBeInTheDocument();
  });

  it("applies Format ribbon changes to the selected table row and keeps the saved state visible", async () => {
    const updateActivity = vi.fn().mockResolvedValue(undefined);
    const mutations = { ...makeMutations(), updateActivity: { mutateAsync: updateActivity } };
    const task = activity("task-1", "wbs-1");
    const wbs = [wbsNode("wbs-1", task.id, 0)];
    const props = { projectId: "project-1", planId: "plan-1", wbs, activities: [task], dependencies: [], mutations, editable: true, showBaseline: false, ribbonTab: "task" as const, t, onEdit: vi.fn(), onDelete: vi.fn() };
    const { rerender } = renderWithProviders(<PlanningGrid {...props} />);

    fireEvent.click(screen.getByText("Task task-1"));
    fireEvent.click(screen.getByRole("button", { name: "planning.formatBold" }));
    await waitFor(() => expect(updateActivity).toHaveBeenCalledWith(expect.objectContaining({ id: task.id, body: expect.objectContaining({ textFormat: { isBold: true, isItalic: false, isUnderline: false } }) })));

    rerender(<PlanningGrid {...props} activities={[{ ...task, textFormat: { isBold: true, isItalic: false, isUnderline: false } }]} />);
    expect(screen.getByText("Task task-1").closest("tr")).toHaveClass("font-semibold");
    expect(screen.getByRole("button", { name: "planning.formatBold" })).toHaveAttribute("aria-pressed", "true");
  });

  it("shares multi-row selection with Task ribbon formatting actions", async () => {
    const first = activity("first", "wbs-first");
    const second = activity("second", "wbs-second");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-first", first.id, 0), wbsNode("wbs-second", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);
    const rows = screen.getAllByTestId("planning-grid-row");
    fireEvent.mouseDown(within(rows[0]!).getAllByRole("cell")[3]!);
    fireEvent.mouseDown(within(rows[1]!).getAllByRole("cell")[3]!, { ctrlKey: true });
    fireEvent.click(screen.getByRole("button", { name: "planning.formatBold" }));
    await waitFor(() => expect(mutations.bulkUpdateActivities.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ items: expect.arrayContaining([
      expect.objectContaining({ activityId: first.id, update: expect.objectContaining({ textFormat: expect.objectContaining({ isBold: true }) }) }),
      expect.objectContaining({ activityId: second.id, update: expect.objectContaining({ textFormat: expect.objectContaining({ isBold: true }) }) }),
    ]) })));
  });

  it("keeps double-clicking an editable cell in table edit mode instead of opening task details", () => {
    const task = activity("task-1", "wbs-1");
    const onEdit = vi.fn();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={onEdit} onDelete={vi.fn()} />);

    fireEvent.doubleClick(screen.getByText("Task task-1"));

    expect(onEdit).not.toHaveBeenCalled();
    expect(screen.getByDisplayValue("Task task-1")).toBeInTheDocument();
  });

  it("edits the progress cell on double-click without opening task details", () => {
    const task = activity("task-1", "wbs-1");
    const onEdit = vi.fn();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[{ ...task, progress: 20 }]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={onEdit} onDelete={vi.fn()} />);

    fireEvent.doubleClick(screen.getByText("20%"));

    expect(onEdit).not.toHaveBeenCalled();
    expect(screen.getByDisplayValue("20")).toBeInTheDocument();
  });

  it("toggles scheduling mode from the mode cell and sends the update", async () => {
    const task = activity("task-1", "wbs-1");
    const updateActivity = vi.fn().mockResolvedValue(undefined);
    const mutations = { ...makeMutations(), updateActivity: { mutateAsync: updateActivity } };
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[{ ...task, schedulingMode: "Automatic" }]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "planning.schedulingMode: planning.automatic" }));

    const modeButton = screen.getByRole("button", { name: "planning.schedulingMode: planning.automatic" });
    expect(modeButton).toHaveAttribute("title", "planning.automatic");
    expect((modeButton.textContent ?? "").trim()).toBe("");
    expect(modeButton.querySelector("svg")).not.toBeNull();

    await waitFor(() => expect(updateActivity).toHaveBeenCalledWith(expect.objectContaining({ id: task.id, body: expect.objectContaining({ schedulingMode: "Manual" }) })));
  });

  it("saves a constraint-type ribbon-table edit only when its required date is present", async () => {
    const task = activity("task-1", "wbs-1");
    const updateActivity = vi.fn().mockResolvedValue(undefined);
    const mutations = { ...makeMutations(), updateActivity: { mutateAsync: updateActivity } };
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);
    fireEvent.click(screen.getByRole("button", { name: "planning.columnManager" }));
    fireEvent.click(screen.getByRole("button", { name: "planning.columns.constraintType" }));

    fireEvent.change(screen.getByRole("combobox", { name: "planning.columns.constraintType" }), { target: { value: "MustStartOn" } });

    expect(await screen.findByRole("alert")).toHaveTextContent("planning.constraintDateRequired");
    expect(updateActivity).not.toHaveBeenCalled();
  });

  it("inserts a task from the ribbon into the table ordering and selects the new row", async () => {
    const first = activity("task-1", "wbs-1");
    const initialWbs = [wbsNode("wbs-1", first.id, 0)];
    const mutations = makeMutations();
    const props = { projectId: "project-1", planId: "plan-1", wbs: initialWbs, activities: [first], dependencies: [], mutations, editable: true, showBaseline: false, ribbonTab: "task" as const, t, onEdit: vi.fn(), onDelete: vi.fn() };
    const { rerender } = renderWithProviders(<PlanningGrid {...props} />);

    fireEvent.click(screen.getByText("Task task-1"));
    fireEvent.click(screen.getByRole("button", { name: "planning.insertAbove" }));

    await waitFor(() => expect(mutations.createActivity.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ wbsId: "created-wbs", sortOrder: -1 })));
    expect(mutations.createWbs.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ parentWbsId: null, sortOrder: -1 }));
    rerender(<PlanningGrid {...props} wbs={[...initialWbs, wbsNode("created-wbs", "created-activity", -1)]} activities={[activity("created-activity", "created-wbs"), first]} />);
    expect(screen.getByText("Task created-activity").closest("tr")).toHaveAttribute("aria-selected", "true");
  });

  it("moves a selected task by dropping it above, below, or under another task row", async () => {
    const moveActivity = vi.fn().mockResolvedValue(undefined);
    const mutations = { ...makeMutations(), moveActivity: { mutateAsync: moveActivity, isPending: false } };
    const first = activity("task-1", "wbs-1");
    const second = activity("task-2", "wbs-2");
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", first.id, 0), wbsNode("wbs-2", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    const [firstHandle] = screen.getAllByRole("button", { name: "planning.moveActivity" });
    const targetRow = screen.getByText("Task task-2").closest("tr");
    expect(targetRow).not.toBeNull();
    const dataTransfer = { effectAllowed: "", dropEffect: "", setData: vi.fn() };
    fireEvent.dragStart(firstHandle, { dataTransfer });
    fireEvent.dragOver(targetRow!, { dataTransfer, clientY: 0 });
    fireEvent.drop(targetRow!, { dataTransfer });

    await waitFor(() => expect(moveActivity).toHaveBeenCalledWith(expect.objectContaining({
      id: first.id,
      body: expect.objectContaining({ targetActivityId: second.id, position: "Child", rowVersion: first.rowVersion, wbsRowVersion: "wbs-wbs-1", targetWbsRowVersion: "wbs-wbs-2" }),
    })));
  });

  it("indents and outdents the selected WBS task node through the hierarchy ribbon", async () => {
    const first = activity("task-1", "wbs-1");
    const second = activity("task-2", "wbs-2");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", first.id, 0), wbsNode("wbs-2", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Task task-2"));
    fireEvent.click(screen.getByRole("button", { name: "planning.indent" }));
    await waitFor(() => expect(mutations.updateWbs.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({
      id: "wbs-2",
      body: expect.objectContaining({ parentWbsId: "wbs-1", rowVersion: "wbs-wbs-2" }),
    })));

    cleanup();
    const nestedTask = activity("nested-task", "nested-wbs");
    const parent: PlanWbsNodeDto = { id: "parent-wbs", planVersionId: "plan-1", parentWbsId: undefined, sortOrder: 0, code: "parent", name: "Parent WBS", rowVersion: "parent-v1", children: [{ ...wbsNode("nested-wbs", nestedTask.id, 0), parentWbsId: "parent-wbs" }] };
    const outdentMutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[parent]} activities={[nestedTask]} dependencies={[]} mutations={outdentMutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);
    fireEvent.click(screen.getByText("Task nested-task"));
    fireEvent.click(screen.getByRole("button", { name: "planning.outdent" }));
    await waitFor(() => expect(outdentMutations.updateWbs.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({
      id: "nested-wbs",
      body: expect.objectContaining({ parentWbsId: null, rowVersion: "wbs-nested-wbs" }),
    })));
  });

  it("indents a selected WBS row under the preceding row at the same hierarchy level", async () => {
    const rootTask = activity("root-task", "root-wbs");
    const nestedTask = activity("nested-task", "nested-wbs");
    const siblingTask = activity("sibling-task", "sibling-wbs");
    const root: PlanWbsNodeDto = {
      ...wbsNode("root-wbs", rootTask.id, 0),
      children: [{ ...wbsNode("nested-wbs", nestedTask.id, 0), parentWbsId: "root-wbs" }],
    };
    const sibling = wbsNode("sibling-wbs", siblingTask.id, 1);
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[root, sibling]} activities={[rootTask, nestedTask, siblingTask]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Task sibling-task"));
    const hierarchyRibbon = within(screen.getByRole("group", { name: "planning.ribbon.hierarchy" }));
    expect(hierarchyRibbon.getByRole("button", { name: "planning.indent" })).toBeEnabled();
    fireEvent.click(hierarchyRibbon.getByRole("button", { name: "planning.indent" }));

    await waitFor(() => expect(mutations.updateWbs.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({
      id: "sibling-wbs",
      body: expect.objectContaining({ parentWbsId: "root-wbs", rowVersion: "wbs-sibling-wbs" }),
    })));
  });

  it("enables hierarchy commands with valid targets and updates collapse-all availability", () => {
    const rootTask = activity("root-task", "root-wbs");
    const previousTask = activity("previous-task", "previous-wbs");
    const selectedTask = activity("selected-task", "selected-wbs");
    const nextTask = activity("next-task", "next-wbs");
    const childTask = activity("child-task", "child-wbs");
    const previous = { ...wbsNode("previous-wbs", previousTask.id, 0), parentWbsId: "root-wbs" };
    const selected = { ...wbsNode("selected-wbs", selectedTask.id, 1), parentWbsId: "root-wbs", children: [wbsNode("child-wbs", childTask.id, 0)] };
    const next = { ...wbsNode("next-wbs", nextTask.id, 2), parentWbsId: "root-wbs" };
    const root: PlanWbsNodeDto = { ...wbsNode("root-wbs", rootTask.id, 0), children: [previous, selected, next] };
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[root]} activities={[rootTask, previousTask, selectedTask, nextTask, childTask]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Task selected-task"));

    const hierarchyRibbon = within(screen.getByRole("group", { name: "planning.ribbon.hierarchy" }));
    for (const command of ["indent", "outdent", "moveUp", "moveDown", "collapse", "collapseAll"]) {
      expect(hierarchyRibbon.getByRole("button", { name: `planning.${command}` })).toBeEnabled();
    }
    const collapseAll = hierarchyRibbon.getByRole("button", { name: "planning.collapseAll" });
    const expandAll = hierarchyRibbon.getByRole("button", { name: "planning.expandAll" });
    expect(expandAll).toBeDisabled();
    fireEvent.click(collapseAll);
    expect(collapseAll).toBeDisabled();
    expect(expandAll).toBeEnabled();
    fireEvent.click(expandAll);
    expect(collapseAll).toBeEnabled();
    expect(expandAll).toBeDisabled();
  });

  it("applies hierarchy actions to standalone WBS rows and to activities not linked to a WBS row", async () => {
    const pureNode = (id: string, sortOrder: number, parentWbsId?: string, children: PlanWbsNodeDto[] = []): PlanWbsNodeDto => ({ id, planVersionId: "plan-1", parentWbsId, sortOrder, code: id, name: `WBS ${id}`, rowVersion: `wbs-${id}`, children });
    const first = pureNode("wbs-1", 0);
    const second = pureNode("wbs-2", 1);
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[first, second]} activities={[]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("WBS wbs-2"));
    fireEvent.click(screen.getByRole("button", { name: "planning.indent" }));
    await waitFor(() => expect(mutations.updateWbs.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ id: second.id, body: expect.objectContaining({ parentWbsId: first.id, rowVersion: second.rowVersion }) })));

    cleanup();
    const parent = pureNode("parent", 0, undefined, [pureNode("child", 0, "parent")]);
    const outdentMutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[parent]} activities={[]} dependencies={[]} mutations={outdentMutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);
    fireEvent.click(screen.getByText("WBS child"));
    fireEvent.click(screen.getByRole("button", { name: "planning.outdent" }));
    await waitFor(() => expect(outdentMutations.updateWbs.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ id: "child", body: expect.objectContaining({ parentWbsId: null }) })));

    cleanup();
    const upper = pureNode("upper", 0);
    const lower = pureNode("lower", 1);
    const reorderMutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[upper, lower]} activities={[]} dependencies={[]} mutations={reorderMutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);
    fireEvent.click(screen.getByText("WBS lower"));
    fireEvent.click(screen.getByRole("button", { name: "planning.moveUp" }));
    await waitFor(() => expect(reorderMutations.bulkUpdateWbsPlacements.mutateAsync).toHaveBeenCalledWith([
      { nodeId: "lower", parentWbsId: null, sortOrder: 0, rowVersion: "wbs-lower" },
      { nodeId: "upper", parentWbsId: null, sortOrder: 1, rowVersion: "wbs-upper" },
    ]));
  });

  it("reorders a secondary activity within its WBS and can attach a root task under a WBS", async () => {
    const first = activity("task-1", "shared-wbs");
    const second = activity("task-2", "shared-wbs");
    const node = { ...wbsNode("shared-wbs", first.id, 0), children: [] };
    const reorderMutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[node]} activities={[first, second]} dependencies={[]} mutations={reorderMutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Task task-2"));
    fireEvent.click(screen.getByRole("button", { name: "planning.moveUp" }));
    await waitFor(() => expect(reorderMutations.bulkUpdateActivities.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ items: expect.arrayContaining([
      expect.objectContaining({ activityId: second.id, update: expect.objectContaining({ sortOrder: 0 }) }),
      expect.objectContaining({ activityId: first.id, update: expect.objectContaining({ sortOrder: 1 }) }),
    ]) })));

    cleanup();
    const rootTask = activity("root-task", "unused");
    rootTask.wbsId = undefined;
    const attachMutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[node]} activities={[first, rootTask]} dependencies={[]} mutations={attachMutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);
    fireEvent.click(screen.getByText("Task root-task"));
    fireEvent.click(screen.getByRole("button", { name: "planning.indent" }));
    await waitFor(() => expect(attachMutations.updateActivity.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ id: rootTask.id, body: expect.objectContaining({ wbsId: node.id }) })));
  });

  it("moves a contiguous multi-row WBS selection as one atomic reorder", async () => {
    const tasks = ["one", "two", "three", "four"].map((id) => activity(id, `wbs-${id}`));
    const nodes = tasks.map((task, index) => wbsNode(`wbs-${task.id}`, task.id, index));
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={nodes} activities={tasks} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} />);

    const rows = screen.getAllByTestId("planning-grid-row");
    fireEvent.click(rows[1]!);
    fireEvent.click(rows[2]!, { ctrlKey: true });
    fireEvent.click(screen.getByRole("button", { name: "planning.moveUp" }));

    await waitFor(() => expect(mutations.bulkUpdateWbsPlacements.mutateAsync).toHaveBeenCalledWith([
      { nodeId: "wbs-two", parentWbsId: null, sortOrder: 0, rowVersion: "wbs-wbs-two" },
      { nodeId: "wbs-three", parentWbsId: null, sortOrder: 1, rowVersion: "wbs-wbs-three" },
      { nodeId: "wbs-one", parentWbsId: null, sortOrder: 2, rowVersion: "wbs-wbs-one" },
    ]));
    expect(mutations.updateWbs.mutateAsync).not.toHaveBeenCalled();
  });

  it("enables hierarchy commands only when the selected row has a valid target", () => {
    const first = activity("task-1", "wbs-1");
    const second = activity("task-2", "wbs-2");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", first.id, 0), wbsNode("wbs-2", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    const indent = screen.getByRole("button", { name: "planning.indent" });
    const outdent = screen.getByRole("button", { name: "planning.outdent" });
    const moveUp = screen.getByRole("button", { name: "planning.moveUp" });
    const moveDown = screen.getByRole("button", { name: "planning.moveDown" });
    expect(indent).toBeDisabled();
    expect(outdent).toBeDisabled();
    expect(moveUp).toBeDisabled();
    expect(moveDown).toBeDisabled();

    fireEvent.click(screen.getByText("Task task-1"));
    expect(indent).toBeDisabled();
    expect(outdent).toBeEnabled();
    expect(moveUp).toBeDisabled();
    expect(moveDown).toBeEnabled();

    fireEvent.click(screen.getByText("Task task-2"));
    expect(indent).toBeEnabled();
    expect(outdent).toBeEnabled();
    expect(moveUp).toBeEnabled();
    expect(moveDown).toBeDisabled();
  });

  it("outdents an activity from a root WBS to the plan root", async () => {
    const task = activity("task-1", "wbs-1");
    const updateActivity = vi.fn().mockResolvedValue(undefined);
    const mutations = { ...makeMutations(), updateActivity: { mutateAsync: updateActivity } };
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Task task-1"));
    const outdent = screen.getByRole("button", { name: "planning.outdent" });
    expect(outdent).toBeEnabled();
    fireEvent.click(outdent);

    await waitFor(() => expect(updateActivity).toHaveBeenCalledWith(expect.objectContaining({
      id: task.id,
      body: expect.objectContaining({ wbsId: null }),
    })));
  });

  it("collapses and expands WBS descendants from the hierarchy ribbon", () => {
    const child = activity("child-task", "child-wbs");
    const parent: PlanWbsNodeDto = { id: "parent-wbs", planVersionId: "plan-1", parentWbsId: undefined, sortOrder: 0, code: "parent", name: "Parent WBS", rowVersion: "parent-v1", children: [{ ...wbsNode("child-wbs", child.id, 0), parentWbsId: "parent-wbs" }] };
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[parent]} activities={[child]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Parent WBS"));
    const hierarchyRibbon = within(screen.getByRole("group", { name: "planning.ribbon.hierarchy" }));
    fireEvent.click(hierarchyRibbon.getByRole("button", { name: "planning.collapse" }));
    expect(screen.queryByText("Task child-task")).not.toBeInTheDocument();
    fireEvent.click(hierarchyRibbon.getByRole("button", { name: "planning.expand" }));
    expect(screen.getByText("Task child-task")).toBeInTheDocument();
    fireEvent.click(hierarchyRibbon.getByRole("button", { name: "planning.collapseAll" }));
    expect(screen.queryByText("Task child-task")).not.toBeInTheDocument();
    fireEvent.click(hierarchyRibbon.getByRole("button", { name: "planning.expandAll" }));
    expect(screen.getByText("Task child-task")).toBeInTheDocument();
  });

  it("moves between editable task cells with the arrow keys", async () => {
    const first = activity("task-1", "wbs-1");
    const second = activity("task-2", "wbs-2");
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", first.id, 0), wbsNode("wbs-2", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.doubleClick(screen.getByText("Task task-1"));
    fireEvent.keyDown(screen.getByDisplayValue("Task task-1"), { key: "ArrowDown" });

    await waitFor(() => expect(screen.getByDisplayValue("Task task-2")).toBeInTheDocument());
  });

  it("creates multiple task rows when a newline list is pasted into a task name cell", async () => {
    const task = activity("task-1", "wbs-1");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.doubleClick(screen.getByText("Task task-1"));
    fireEvent.paste(screen.getByDisplayValue("Task task-1"), { clipboardData: { getData: () => "First pasted task\nSecond pasted task" } });

    await waitFor(() => expect(mutations.createActivity.mutateAsync).toHaveBeenCalledTimes(2));
    expect(mutations.createWbs.mutateAsync).toHaveBeenCalledTimes(2);
    expect(mutations.createWbs.mutateAsync).toHaveBeenNthCalledWith(1, expect.objectContaining({ name: "First pasted task" }));
    expect(mutations.createWbs.mutateAsync).toHaveBeenNthCalledWith(2, expect.objectContaining({ name: "Second pasted task" }));
  });

  it("shows hierarchical outline numbers in the WBS column and bolds summary rows", () => {
    const parentTask = activity("task-1", "wbs-1");
    const childTask = activity("task-2", "wbs-2");
    const parent: PlanWbsNodeDto = { id: "wbs-1", planVersionId: "plan-1", parentWbsId: undefined, sortOrder: 0, code: "T-AAA", name: "Phase", rowVersion: "w1", activityId: parentTask.id, children: [{ ...wbsNode("wbs-2", childTask.id, 0), parentWbsId: "wbs-1", code: "T-BBB" }] };
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[parent]} activities={[parentTask, childTask]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    expect(screen.getByTitle("T-AAA")).toHaveTextContent("1");
    expect(screen.getByTitle("T-BBB")).toHaveTextContent("1.1");
    const summaryRow = screen.getByText("Task task-1").closest("tr");
    expect(summaryRow).toHaveAttribute("data-row-kind", "summary");
    expect(screen.getByText("Task task-1").closest("td")).toHaveClass("font-semibold");
    const parentIndent = Number.parseInt(screen.getByText("Task task-1").closest("td")!.style.paddingInlineStart || "0", 10);
    const childIndent = Number.parseInt(screen.getByText("Task task-2").closest("td")!.style.paddingInlineStart || "0", 10);
    expect(childIndent).toBeGreaterThan(parentIndent);
  });

  it("renders a milestone diamond and milestone row kind for milestone rows", () => {
    const milestone: PlanActivityDto = { ...activity("mile-1", "wbs-1"), type: "Milestone", duration: 0 };
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", milestone.id, 0)]} activities={[milestone]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    expect(screen.getByRole("img", { name: "planning.milestone" })).toBeInTheDocument();
    const row = screen.getByText("Task mile-1").closest("tr");
    expect(row).toHaveAttribute("data-row-kind", "milestone");
  });

  it("adds a subtask under the selected WBS-linked row from the ribbon and context menu", async () => {
    const task = activity("task-1", "wbs-1");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Task task-1"));
    fireEvent.click(screen.getByRole("button", { name: "planning.addSubtask" }));

    await waitFor(() => expect(mutations.createWbs.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ parentWbsId: "wbs-1" })));
    expect(mutations.createActivity.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ wbsId: "created-wbs" }));
  });

  it("indents and outdents the selected row with Ctrl+Shift+Arrow shortcuts", async () => {
    const first = activity("task-1", "wbs-1");
    const second = activity("task-2", "wbs-2");
    const updateWbs = vi.fn().mockResolvedValue(undefined);
    const mutations = { ...makeMutations(), updateWbs: { mutateAsync: updateWbs } };
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", first.id, 0), wbsNode("wbs-2", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Task task-2"));
    fireEvent.keyDown(screen.getByText("Task task-2").closest("tr")!, { key: "ArrowRight", ctrlKey: true, shiftKey: true });

    await waitFor(() => expect(updateWbs).toHaveBeenCalledWith(expect.objectContaining({
      id: "wbs-2",
      body: expect.objectContaining({ parentWbsId: "wbs-1" }),
    })));
  });

  it("moves row selection with ArrowUp and ArrowDown when not editing", () => {
    const first = activity("task-1", "wbs-1");
    const second = activity("task-2", "wbs-2");
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", first.id, 0), wbsNode("wbs-2", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Task task-1"));
    fireEvent.keyDown(screen.getByText("Task task-1").closest("tr")!, { key: "ArrowDown" });

    expect(screen.getByText("Task task-2").closest("tr")).toHaveAttribute("aria-selected", "true");
  });

  it("starts editing the selected row with Enter when no cell is being edited", () => {
    const task = activity("task-1", "wbs-1");
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Task task-1"));
    fireEvent.keyDown(screen.getByText("Task task-1").closest("tr")!, { key: "Enter" });

    expect(screen.getByDisplayValue("Task task-1")).toBeInTheDocument();
  });

  it("commits with Enter and moves down to the editable cell in the row below", async () => {
    const first = activity("task-1", "wbs-1");
    const second = activity("task-2", "wbs-2");
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", first.id, 0), wbsNode("wbs-2", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.doubleClick(screen.getByText("Task task-1"));
    fireEvent.keyDown(screen.getByDisplayValue("Task task-1"), { key: "Enter" });

    await waitFor(() => expect(screen.getByDisplayValue("Task task-2")).toBeInTheDocument());
  });

  it("opens the active cell for editing when a printable character is typed", () => {
    const task = activity("task-1", "wbs-1");
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Task task-1"));
    fireEvent.keyDown(screen.getByText("Task task-1").closest("tr")!, { key: "R" });

    expect(screen.getByDisplayValue("R")).toBeInTheDocument();
  });

  it("rejects a Manual finish date earlier than the start date without saving", async () => {
    const updateActivity = vi.fn().mockResolvedValue(undefined);
    const mutations = { ...makeMutations(), updateActivity: { mutateAsync: updateActivity } };
    const task: PlanActivityDto = { ...activity("task-1", "wbs-1"), schedulingMode: "Manual", startDate: "2026-01-10T00:00:00Z", endDate: "2026-01-20T00:00:00Z" };
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.doubleClick(screen.getByText("2026-01-20"));
    const input = screen.getByDisplayValue("20/01/2026");
    fireEvent.change(input, { target: { value: "05/01/2026" } });
    fireEvent.keyDown(input, { key: "Enter" });

    expect(await screen.findByRole("alert")).toHaveTextContent("planning.invalidDateRange");
    expect(updateActivity).not.toHaveBeenCalled();
  });

  it("saves a valid finish date edit as an ISO date", async () => {
    const updateActivity = vi.fn().mockResolvedValue(undefined);
    const mutations = { ...makeMutations(), updateActivity: { mutateAsync: updateActivity } };
    const task: PlanActivityDto = { ...activity("task-1", "wbs-1"), schedulingMode: "Manual", startDate: "2026-01-10T00:00:00Z", endDate: "2026-01-20T00:00:00Z" };
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.doubleClick(screen.getByText("2026-01-20"));
    const input = screen.getByDisplayValue("20/01/2026");
    fireEvent.change(input, { target: { value: "25/01/2026" } });
    fireEvent.blur(input);

    await waitFor(() => expect(updateActivity).toHaveBeenCalledWith(expect.objectContaining({ id: task.id, body: expect.objectContaining({ endDate: "2026-01-25" }) })));
  });

  it("uses Delete to clear a selected editable cell without deleting its task row", async () => {
    const task = activity("task-1", "wbs-1");
    const updateActivity = vi.fn().mockResolvedValue(undefined);
    const mutations = { ...makeMutations(), updateActivity: { mutateAsync: updateActivity } };
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[{ ...task, progress: 20 }]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    const progressCell = screen.getByText("20%").closest("td")!;
    fireEvent.mouseDown(progressCell);
    fireEvent.keyDown(progressCell, { key: "Delete" });

    await waitFor(() => expect(updateActivity).toHaveBeenCalledWith(expect.objectContaining({ id: task.id, body: expect.objectContaining({ progress: 0 }) })));
    expect(screen.getByText("Task task-1")).toBeInTheDocument();
    expect(mutations.deleteActivity.mutateAsync).not.toHaveBeenCalled();
  });

  it("confirms row-toolbar deletion and routes it through the shared cleanup", async () => {
    const task = activity("task-1", "wbs-1");
    const onDelete = vi.fn();
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={onDelete} />);

    fireEvent.click(screen.getByTestId("planning-grid-row-delete"));
    expect(await screen.findByRole("alertdialog")).toHaveTextContent("planning.confirmDelete");
    expect(mutations.deleteActivity.mutateAsync).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "common.confirm" }));

    await waitFor(() => expect(mutations.deleteActivity.mutateAsync).toHaveBeenCalledWith({ id: task.id, rowVersion: task.rowVersion }));
    expect(onDelete).not.toHaveBeenCalled();
  });

  it("uses Ctrl+Minus to delete the selected row through the same confirmation", async () => {
    const task = activity("task-1", "wbs-1");
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    const taskCell = screen.getByText(task.name);
    fireEvent.click(taskCell);
    fireEvent.keyDown(taskCell.closest("tr")!, { key: "-", code: "NumpadSubtract", ctrlKey: true });
    expect(await screen.findByRole("alertdialog")).toHaveTextContent("planning.confirmDelete");
    fireEvent.click(screen.getByRole("button", { name: "common.confirm" }));

    await waitFor(() => expect(mutations.deleteActivity.mutateAsync).toHaveBeenCalledWith({ id: task.id, rowVersion: task.rowVersion }));
  });

  it("deletes a summary subtree through one confirmed path and removes its related links and assignments", async () => {
    const parentTask = { ...activity("task-parent", "wbs-parent"), isSummaryTask: true };
    const childTask = activity("task-child", "wbs-child");
    const childNode = { ...wbsNode("wbs-child", childTask.id, 0), parentWbsId: "wbs-parent" };
    const parentNode = { ...wbsNode("wbs-parent", parentTask.id, 0), children: [childNode] };
    const dependency: PlanDependencyDto = { id: "dep-child", planVersionId: "plan-1", activityId: childTask.id, predecessorActivityId: "external-task", type: "FinishToStart", rowVersion: "dep-row" };
    const assignments: PlanAssignmentDto[] = [
      { id: "assignment-parent", planVersionId: "plan-1", activityId: parentTask.id, resourceId: "resource-1", rowVersion: "assignment-parent-row" },
      { id: "assignment-child", planVersionId: "plan-1", activityId: childTask.id, resourceId: "resource-2", rowVersion: "assignment-child-row" },
    ];
    vi.mocked(usePlanningAssignmentsFull).mockReturnValue({ data: assignments, isSuccess: true, isError: false } as ReturnType<typeof usePlanningAssignmentsFull>);
    const latestParentTask = { ...parentTask, rowVersion: "parent-current-row" };
    const latestChildTask = { ...childTask, rowVersion: "child-current-row" };
    vi.mocked(planningApiFull.activities).mockResolvedValue([latestParentTask, latestChildTask]);
    const mutations = makeMutations();
    renderWithProviders(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[parentNode]} activities={[parentTask, childTask]} dependencies={[dependency]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText(parentTask.name));
    fireEvent.click(screen.getAllByRole("button", { name: "common.delete" })[0]);
    expect(await screen.findByRole("alertdialog")).toHaveTextContent("planning.confirmDeleteSummary");
    fireEvent.click(screen.getByRole("button", { name: "common.confirm" }));

    await waitFor(() => expect(mutations.deleteWbs.mutateAsync).toHaveBeenCalledTimes(2));
    expect(mutations.deleteDependency.mutateAsync).toHaveBeenCalledWith({ id: dependency.id, rowVersion: dependency.rowVersion });
    expect(mutations.deleteAssignment.mutateAsync).toHaveBeenCalledTimes(2);
    expect(mutations.deleteActivity.mutateAsync).toHaveBeenNthCalledWith(1, { id: latestParentTask.id, rowVersion: latestParentTask.rowVersion });
    expect(mutations.deleteActivity.mutateAsync).toHaveBeenNthCalledWith(2, { id: latestChildTask.id, rowVersion: latestChildTask.rowVersion });
    expect(mutations.deleteWbs.mutateAsync).toHaveBeenNthCalledWith(1, { id: childNode.id, rowVersion: childNode.rowVersion });
    expect(mutations.deleteWbs.mutateAsync).toHaveBeenNthCalledWith(2, { id: parentNode.id, rowVersion: parentNode.rowVersion });
    expect(planningApiFull.activities).toHaveBeenCalledWith("project-1", "plan-1");
    expect(mutations.deleteDependency.mutateAsync.mock.invocationCallOrder[0]).toBeLessThan(mutations.deleteAssignment.mutateAsync.mock.invocationCallOrder[0]);
    expect(mutations.deleteAssignment.mutateAsync.mock.invocationCallOrder[1]).toBeLessThan(mutations.deleteActivity.mutateAsync.mock.invocationCallOrder[0]);
  });
});
