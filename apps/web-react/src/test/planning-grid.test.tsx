import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { PlanActivityDto, PlanWbsNodeDto } from "@features/projects/api/planningFull";
import { PlanningGrid } from "@features/projects/components/PlanningGrid";

vi.mock("@features/projects/api/planningFull", () => ({
  usePlanningAssignmentsFull: () => ({ data: [] }),
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
  updateActivity: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, deleteActivity: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, createActivity: { mutateAsync: vi.fn().mockImplementation(async () => activity("created-activity", "created-wbs")) },
  updateWbs: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, createWbs: { mutateAsync: vi.fn().mockImplementation(async () => wbsNode("created-wbs", "created-activity", 0)) }, deleteWbs: { mutateAsync: vi.fn().mockResolvedValue(undefined) },
  createDependency: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, deleteDependency: { mutateAsync: vi.fn().mockResolvedValue(undefined) }, moveActivity: { mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false },
});

describe("planning grid row and ribbon behavior", () => {
  it("renders the row context menu outside the clipped planning frame", () => {
    const task = activity("task-1", "wbs-1");
    const { getByTestId } = render(
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
    const props = { projectId: "project-1", planId: "plan-1", wbs, activities: [task], dependencies: [], mutations, editable: true, showBaseline: false, ribbonTab: "format" as const, t, onEdit: vi.fn(), onDelete: vi.fn() };
    const { rerender } = render(<PlanningGrid {...props} />);

    fireEvent.click(screen.getByText("Task task-1"));
    fireEvent.click(screen.getByRole("button", { name: "planning.formatBold" }));
    await waitFor(() => expect(updateActivity).toHaveBeenCalledWith(expect.objectContaining({ id: task.id, body: expect.objectContaining({ textFormat: { isBold: true, isItalic: false, isUnderline: false } }) })));

    rerender(<PlanningGrid {...props} activities={[{ ...task, textFormat: { isBold: true, isItalic: false, isUnderline: false } }]} />);
    expect(screen.getByText("Task task-1").closest("tr")).toHaveClass("font-semibold");
    expect(screen.getByRole("button", { name: "planning.formatBold" })).toHaveAttribute("aria-pressed", "true");
  });

  it("keeps double-clicking an editable cell in table edit mode instead of opening task details", () => {
    const task = activity("task-1", "wbs-1");
    const onEdit = vi.fn();
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={onEdit} onDelete={vi.fn()} />);

    fireEvent.doubleClick(screen.getByText("Task task-1"));

    expect(onEdit).not.toHaveBeenCalled();
    expect(screen.getByDisplayValue("Task task-1")).toBeInTheDocument();
  });

  it("edits the progress cell on double-click without opening task details", () => {
    const task = activity("task-1", "wbs-1");
    const onEdit = vi.fn();
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[{ ...task, progress: 20 }]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={onEdit} onDelete={vi.fn()} />);

    fireEvent.doubleClick(screen.getByText("20%"));

    expect(onEdit).not.toHaveBeenCalled();
    expect(screen.getByDisplayValue("20")).toBeInTheDocument();
  });

  it("toggles scheduling mode from the mode cell and sends the update", async () => {
    const task = activity("task-1", "wbs-1");
    const updateActivity = vi.fn().mockResolvedValue(undefined);
    const mutations = { ...makeMutations(), updateActivity: { mutateAsync: updateActivity } };
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[{ ...task, schedulingMode: "Automatic" }]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "planning.schedulingMode: planning.automatic" }));

    await waitFor(() => expect(updateActivity).toHaveBeenCalledWith(expect.objectContaining({ id: task.id, body: expect.objectContaining({ schedulingMode: "Manual" }) })));
  });

  it("saves a constraint-type ribbon-table edit only when its required date is present", async () => {
    const task = activity("task-1", "wbs-1");
    const updateActivity = vi.fn().mockResolvedValue(undefined);
    const mutations = { ...makeMutations(), updateActivity: { mutateAsync: updateActivity } };
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);
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
    const { rerender } = render(<PlanningGrid {...props} />);

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
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", first.id, 0), wbsNode("wbs-2", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

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
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", first.id, 0), wbsNode("wbs-2", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

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
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[parent]} activities={[nestedTask]} dependencies={[]} mutations={outdentMutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);
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
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[root, sibling]} activities={[rootTask, nestedTask, siblingTask]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

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
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[root]} activities={[rootTask, previousTask, selectedTask, nextTask, childTask]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

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
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[first, second]} activities={[]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("WBS wbs-2"));
    fireEvent.click(screen.getByRole("button", { name: "planning.indent" }));
    await waitFor(() => expect(mutations.updateWbs.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ id: second.id, body: expect.objectContaining({ parentWbsId: first.id, rowVersion: second.rowVersion }) })));

    cleanup();
    const parent = pureNode("parent", 0, undefined, [pureNode("child", 0, "parent")]);
    const outdentMutations = makeMutations();
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[parent]} activities={[]} dependencies={[]} mutations={outdentMutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);
    fireEvent.click(screen.getByText("WBS child"));
    fireEvent.click(screen.getByRole("button", { name: "planning.outdent" }));
    await waitFor(() => expect(outdentMutations.updateWbs.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ id: "child", body: expect.objectContaining({ parentWbsId: null }) })));

    cleanup();
    const upper = pureNode("upper", 0);
    const lower = pureNode("lower", 1);
    const reorderMutations = makeMutations();
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[upper, lower]} activities={[]} dependencies={[]} mutations={reorderMutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);
    fireEvent.click(screen.getByText("WBS lower"));
    fireEvent.click(screen.getByRole("button", { name: "planning.moveUp" }));
    await waitFor(() => expect(reorderMutations.updateWbs.mutateAsync).toHaveBeenCalledTimes(2));
    expect(reorderMutations.updateWbs.mutateAsync).toHaveBeenNthCalledWith(1, expect.objectContaining({ id: "lower", body: expect.objectContaining({ sortOrder: 0 }) }));
    expect(reorderMutations.updateWbs.mutateAsync).toHaveBeenNthCalledWith(2, expect.objectContaining({ id: "upper", body: expect.objectContaining({ sortOrder: 1 }) }));
  });

  it("reorders a secondary activity within its WBS and can attach a root task under a WBS", async () => {
    const first = activity("task-1", "shared-wbs");
    const second = activity("task-2", "shared-wbs");
    const node = { ...wbsNode("shared-wbs", first.id, 0), children: [] };
    const reorderMutations = makeMutations();
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[node]} activities={[first, second]} dependencies={[]} mutations={reorderMutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.click(screen.getByText("Task task-2"));
    fireEvent.click(screen.getByRole("button", { name: "planning.moveUp" }));
    await waitFor(() => expect(reorderMutations.moveActivity.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ id: second.id, body: expect.objectContaining({ targetActivityId: first.id, position: "Before" }) })));

    cleanup();
    const rootTask = activity("root-task", "unused");
    rootTask.wbsId = undefined;
    const attachMutations = makeMutations();
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[node]} activities={[first, rootTask]} dependencies={[]} mutations={attachMutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);
    fireEvent.click(screen.getByText("Task root-task"));
    fireEvent.click(screen.getByRole("button", { name: "planning.indent" }));
    await waitFor(() => expect(attachMutations.updateActivity.mutateAsync).toHaveBeenCalledWith(expect.objectContaining({ id: rootTask.id, body: expect.objectContaining({ wbsId: node.id }) })));
  });

  it("enables hierarchy commands only when the selected row has a valid target", () => {
    const first = activity("task-1", "wbs-1");
    const second = activity("task-2", "wbs-2");
    const mutations = makeMutations();
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", first.id, 0), wbsNode("wbs-2", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

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
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

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
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[parent]} activities={[child]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

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
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", first.id, 0), wbsNode("wbs-2", second.id, 1)]} activities={[first, second]} dependencies={[]} mutations={makeMutations()} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.doubleClick(screen.getByText("Task task-1"));
    fireEvent.keyDown(screen.getByDisplayValue("Task task-1"), { key: "ArrowDown" });

    await waitFor(() => expect(screen.getByDisplayValue("Task task-2")).toBeInTheDocument());
  });

  it("creates multiple task rows when a newline list is pasted into a task name cell", async () => {
    const task = activity("task-1", "wbs-1");
    const mutations = makeMutations();
    render(<PlanningGrid projectId="project-1" planId="plan-1" wbs={[wbsNode("wbs-1", task.id, 0)]} activities={[task]} dependencies={[]} mutations={mutations} editable showBaseline={false} ribbonTab="task" t={t} onEdit={vi.fn()} onDelete={vi.fn()} />);

    fireEvent.doubleClick(screen.getByText("Task task-1"));
    fireEvent.paste(screen.getByDisplayValue("Task task-1"), { clipboardData: { getData: () => "First pasted task\nSecond pasted task" } });

    await waitFor(() => expect(mutations.createActivity.mutateAsync).toHaveBeenCalledTimes(2));
    expect(mutations.createWbs.mutateAsync).toHaveBeenCalledTimes(2);
    expect(mutations.createWbs.mutateAsync).toHaveBeenNthCalledWith(1, expect.objectContaining({ name: "First pasted task" }));
    expect(mutations.createWbs.mutateAsync).toHaveBeenNthCalledWith(2, expect.objectContaining({ name: "Second pasted task" }));
  });
});
