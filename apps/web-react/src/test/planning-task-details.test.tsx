import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { PlanActivityDto, PlanDependencyDto, PlanSchedulingExplanationDto, PlanWbsNodeDto } from "@features/projects/api/planningFull";
import { PlanningActivityDetailsSheet } from "@features/projects/PlanningActivityDetailsSheet";
import { I18nProvider } from "@i18n/messages";

const planningMocks = vi.hoisted(() => ({
  activities: [] as PlanActivityDto[],
  dependencies: [] as PlanDependencyDto[],
  wbs: [] as PlanWbsNodeDto[],
  assignments: [] as { id: string; planVersionId: string; activityId: string; resourceId: string; resourceName?: string; allocationPct?: number; hoursPerWeek?: number; plannedHours?: number; rowVersion: string }[],
  resources: [] as { id: string; name: string; isActive: boolean }[],
  calendars: [] as { id: string; name: string; isDefault: boolean; rowVersion: string }[],
  explanationData: undefined as PlanSchedulingExplanationDto | undefined,
  calendarEnabled: [] as boolean[],
  assignmentEnabled: [] as boolean[],
  resourcesEnabled: [] as boolean[],
  explanationEnabled: [] as boolean[],
  updateActivity: vi.fn(),
  createDependency: vi.fn(),
  deleteDependency: vi.fn(),
}));

vi.mock("@tanstack/react-query", () => ({
  useQueries: ({ queries }: { queries: Array<{ queryKey: unknown[] }> }) => queries.map((query) => ({
    data: { resourceId: String(query.queryKey[4]), projectAllocationPercentage: 100, remainingAssignableHours: 80, isScheduleWithinMembership: true },
    isError: false,
    isFetching: false,
    isLoading: false,
  })),
}));

vi.mock("@features/projects/api/planningFull", () => ({
  planningApiFull: { assignmentCapacity: vi.fn() },
  planningKeys: { assignmentCapacity: (...parts: string[]) => ["planning-full", "assignment-capacity", ...parts] },
  usePlanningFullMutations: () => ({
    updateActivity: { mutateAsync: planningMocks.updateActivity, isPending: false },
    createDependency: { mutateAsync: planningMocks.createDependency },
    deleteDependency: { mutateAsync: planningMocks.deleteDependency },
  }),
  usePlanningCalendarsFull: (_projectId: string, _planId: string, enabled = true) => { planningMocks.calendarEnabled.push(enabled); return { data: planningMocks.calendars, isLoading: false }; },
  usePlanningSchedulingExplanationFull: (_projectId: string, _planId: string, _activityId: string, enabled = true) => { planningMocks.explanationEnabled.push(enabled); return { data: planningMocks.explanationData, isLoading: false, isError: !planningMocks.explanationData }; },
  usePlanningActivitiesFull: () => ({ data: planningMocks.activities }),
  usePlanningAssignmentsFull: (_projectId: string, _planId: string, enabled = true) => { planningMocks.assignmentEnabled.push(enabled); return { data: planningMocks.assignments, isLoading: false }; },
  usePlanningDependenciesFull: () => ({ data: planningMocks.dependencies }),
  usePlanningResourcesFull: (enabled = true) => { planningMocks.resourcesEnabled.push(enabled); return { data: planningMocks.resources }; },
  usePlanningWbsFull: () => ({ data: planningMocks.wbs }),
}));

vi.mock("@features/projects/api/projects", () => ({
  useProjectTeam: () => ({ data: [], isLoading: false, isError: false }),
}));

const t = (key: string) => key;
function activity(id: string, sortOrder: number, extra: Partial<PlanActivityDto> = {}): PlanActivityDto {
  return { id, planVersionId: "plan-1", code: `CODE-${id}`, name: `Task ${id}`, type: "Task", duration: 3, progress: 20, sortOrder, critical: false, rowVersion: `fresh-${id}`, wbsId: `wbs-${id}`, ...extra };
}
function leafWbs(id: string, order: number, activityId?: string): PlanWbsNodeDto {
  return { id: `wbs-${id}`, planVersionId: "plan-1", sortOrder: order, code: `WBS-${id}`, name: `WBS ${id}`, activityId, rowVersion: `wbs-version-${id}`, children: [] };
}
function renderDetails(selected = planningMocks.activities[0]) {
  return render(<I18nProvider><PlanningActivityDetailsSheet open onOpenChange={vi.fn()} activity={selected} projectId="project-1" planId="plan-1" editable t={t} /></I18nProvider>);
}

beforeEach(() => {
  planningMocks.activities = [activity("a", 1)];
  planningMocks.dependencies = [];
  planningMocks.wbs = [leafWbs("a", 1, "a")];
  planningMocks.assignments = [];
  planningMocks.resources = [];
  planningMocks.calendars = [];
  planningMocks.explanationData = undefined;
  planningMocks.calendarEnabled = [];
  planningMocks.assignmentEnabled = [];
  planningMocks.resourcesEnabled = [];
  planningMocks.explanationEnabled = [];
  planningMocks.updateActivity.mockReset().mockResolvedValue(undefined);
  planningMocks.createDependency.mockReset().mockResolvedValue(undefined);
  planningMocks.deleteDependency.mockReset().mockResolvedValue(undefined);
});

describe("planning task details", () => {
  it("uses the expanded details sheet and keeps six tabs keyboard-operable with tab data lazy", () => {
    renderDetails();

    const dialog = screen.getByRole("dialog", { name: "projects.planning.taskDetails" });
    expect(dialog.children.item(1)).toHaveClass("sm:max-w-6xl");
    expect(screen.getByRole("tablist", { name: "projects.planning.taskDetails" })).toBeInTheDocument();
    expect(planningMocks.calendarEnabled.at(-1)).toBe(false);
    expect(planningMocks.assignmentEnabled.at(-1)).toBe(true);
    expect(planningMocks.resourcesEnabled.at(-1)).toBe(false);
    expect(planningMocks.explanationEnabled.at(-1)).toBe(false);

    fireEvent.keyDown(screen.getByRole("tablist"), { key: "ArrowRight" });
    expect(screen.getByRole("tab", { name: "projects.planning.detailTabs.schedule" })).toHaveAttribute("aria-selected", "true");
    expect(planningMocks.calendarEnabled.at(-1)).toBe(true);
    expect(planningMocks.explanationEnabled.at(-1)).toBe(true);
    expect(planningMocks.assignmentEnabled.at(-1)).toBe(true);

    fireEvent.click(screen.getByRole("tab", { name: "projects.planning.detailTabs.resources" }));
    expect(planningMocks.assignmentEnabled.at(-1)).toBe(true);
    expect(planningMocks.resourcesEnabled.at(-1)).toBe(true);
    expect(planningMocks.calendarEnabled.at(-1)).toBe(false);
  });

  it("keeps activity identity read-only and shows the latest server version", async () => {
    const fresh = activity("a", 1, { name: "Current task name", baselineStartDate: "2026-01-05", baselineFinishDate: "2026-01-08", baselineDuration: 3, startVarianceDays: 2 });
    planningMocks.activities = [fresh];
    const stale = { ...fresh, name: "Stale task name", rowVersion: "stale-version" };
    renderDetails(stale);

    expect(await screen.findByDisplayValue("Current task name")).toBeInTheDocument();
    expect(screen.getByText("CODE-a")).toBeInTheDocument();
    expect(screen.queryByLabelText("projects.planning.code")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "projects.planning.detailTabs.analysis" }));
    expect(await screen.findByText("Jan 5, 2026")).toBeInTheDocument();
    expect(screen.getByText("+2 projects.days")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "projects.planning.detailTabs.resources" }));
    expect(screen.getByLabelText("projects.planning.estimateHours")).toBeInTheDocument();
  });

  it("allows editing estimated hours on a summary task while its calculated schedule stays read-only", async () => {
    const summary = activity("summary", 1, { isSummaryTask: true, estimateHours: 5 });
    planningMocks.activities = [summary];
    planningMocks.wbs = [leafWbs("summary", 1, "summary")];
    renderDetails(summary);

    fireEvent.click(screen.getByRole("tab", { name: "projects.planning.detailTabs.resources" }));
    const estimateHours = screen.getByLabelText("projects.planning.estimateHours");
    expect(estimateHours).toBeEnabled();
    fireEvent.change(estimateHours, { target: { value: "8" } });
    fireEvent.click(screen.getByRole("button", { name: "common.save" }));

    await waitFor(() => expect(planningMocks.updateActivity).toHaveBeenCalledWith(expect.objectContaining({
      id: "summary",
      body: expect.objectContaining({ estimateHours: 8 }),
    })));
    fireEvent.click(screen.getByRole("tab", { name: "projects.planning.detailTabs.schedule" }));
    expect(screen.queryByLabelText("projects.planning.durationShort")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("projects.planning.start")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("projects.planning.finish")).not.toBeInTheDocument();
  });

  it("validates actual finish and keeps validation errors visible", async () => {
    renderDetails();
    fireEvent.click(screen.getByRole("tab", { name: "projects.planning.detailTabs.schedule" }));
    fireEvent.change(screen.getByLabelText("projects.planning.actualFinish"), { target: { value: "2026-02-01" } });
    fireEvent.click(screen.getByRole("button", { name: "common.save" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("projects.planning.actualFinishRequiresStart");
    expect(planningMocks.updateActivity).not.toHaveBeenCalled();
  });

  it("preserves a cleared progress value as null when saving", async () => {
    planningMocks.activities = [activity("a", 1, { progress: 40 })];
    renderDetails();
    fireEvent.change(screen.getByRole("spinbutton", { name: "projects.planning.complete" }), { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: "common.save" }));

    await waitFor(() => expect(planningMocks.updateActivity).toHaveBeenCalledWith(expect.objectContaining({
      id: "a",
      body: expect.objectContaining({ progress: null }),
    })));
  });

  it("keeps scheduling explanation in Schedule and uses activity fields in Analysis", async () => {
    planningMocks.activities = [activity("a", 1, { baselineStartDate: "2026-01-05", totalSlack: 12, freeSlack: 7, critical: true }), activity("pred", 2)];
    planningMocks.calendars = [{ id: "calendar-1", name: "Project Working Calendar", isDefault: true, rowVersion: "calendar-v1" }];
    planningMocks.explanationData = {
      activityId: "a",
      schedulingMode: "Manual",
      constraintType: "AsSoonAsPossible",
      effectiveCalendarId: "calendar-1",
      calendarOverride: false,
      start: "2026-10-03",
      finish: "2026-10-06",
      duration: 3,
      totalSlack: 4,
      freeSlack: 2,
      critical: true,
      deadlineExceeded: false,
      hasPredecessors: true,
      hasSuccessors: false,
      reasons: [
        { code: "MovedByPredecessor", relatedActivityId: "pred" },
        { code: "DeadlineExceeded", workingDays: 2 },
      ],
      issues: [],
    };
    renderDetails();

    fireEvent.click(screen.getByRole("tab", { name: "projects.planning.detailTabs.analysis" }));
    expect(planningMocks.explanationEnabled.at(-1)).toBe(false);
    expect(await screen.findByText("Jan 5, 2026")).toBeInTheDocument();
    expect(screen.getByText("12")).toBeInTheDocument();
    expect(screen.getByText("7")).toBeInTheDocument();
    expect(screen.queryByText("projects.planning.schedulingReasons.MovedByPredecessor · CODE-pred · Task pred")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "projects.planning.detailTabs.schedule" }));

    expect(planningMocks.explanationEnabled.at(-1)).toBe(true);
    expect(await screen.findByText("Oct 3, 2026")).toBeInTheDocument();
    expect(await screen.findByText("Project Working Calendar")).toBeInTheDocument();
    expect(screen.getByText("projects.planning.schedulingReasons.MovedByPredecessor · CODE-pred · Task pred")).toBeInTheDocument();
    expect(screen.getByText("projects.planning.schedulingReasons.DeadlineExceeded · 2 projects.days")).toBeInTheDocument();
  });

  it("shows dependency counts and localized empty states in Task Details", async () => {
    renderDetails();

    fireEvent.click(screen.getByRole("tab", { name: "projects.planning.detailTabs.dependencies" }));

    expect(screen.getByRole("heading", { name: "projects.planning.detailSections.dependencies" })).toBeInTheDocument();
    expect(screen.getByText("projects.planning.predecessorCount")).toBeInTheDocument();
    expect(screen.getByText("projects.planning.successorCount")).toBeInTheDocument();
    expect(screen.getByText("projects.planning.noPredecessors")).toBeInTheDocument();
    expect(screen.getByText("projects.planning.noSuccessors")).toBeInTheDocument();
  });

  it("edits predecessor rows with tasks from the plan hierarchy, link type, and lag", async () => {
    const first = activity("a", 1);
    const selected = activity("b", 2);
    const third = activity("c", 3);
    planningMocks.activities = [first, selected, third];
    const root: PlanWbsNodeDto = { id: "root", planVersionId: "plan-1", sortOrder: 0, code: "ROOT", name: "Root group", rowVersion: "root-version", children: [leafWbs("a", 1, "a"), leafWbs("b", 2, "b"), leafWbs("c", 3, "c")] };
    planningMocks.wbs = [root];
    planningMocks.dependencies = [{ id: "link-old", planVersionId: "plan-1", activityId: "b", predecessorActivityId: "a", type: "FinishToStart", rowVersion: "link-version" }];
    renderDetails({ ...selected, rowVersion: "stale-version" });

    fireEvent.click(screen.getByRole("tab", { name: "projects.planning.detailTabs.dependencies" }));
    fireEvent.click(screen.getByRole("button", { name: "projects.planning.addPredecessor" }));
    const taskSelectors = screen.getAllByRole("combobox", { name: /projects\.planning\.selectPredecessorTask/ });
    expect(taskSelectors).toHaveLength(2);
    const newTaskSelector = taskSelectors[1]!;
    const nestedTaskOption = newTaskSelector.querySelector<HTMLOptionElement>('option[value="c"]');
    expect(newTaskSelector.querySelector('option[value="a"]')).not.toBeNull();
    expect(nestedTaskOption).not.toBeNull();
    expect(nestedTaskOption?.textContent).toMatch(/^\u00a0\u00a0CODE-c/);
    expect(newTaskSelector.querySelector<HTMLOptionElement>('option[value="group:wbs:root"]')?.disabled).toBe(true);
    expect(newTaskSelector.querySelector('option[value="b"]')).toBeDisabled();
    fireEvent.change(newTaskSelector, { target: { value: "c" } });
    fireEvent.change(screen.getByRole("combobox", { name: "projects.planning.dependencyType 2" }), { target: { value: "StartToStart" } });
    fireEvent.change(screen.getByRole("spinbutton", { name: "projects.planning.dependencyLag 2" }), { target: { value: "2" } });
    fireEvent.click(screen.getByRole("button", { name: "common.save" }));

    await waitFor(() => expect(planningMocks.createDependency).toHaveBeenCalledWith({ activityId: "b", predecessorActivityId: "c", type: "StartToStart", lag: 2 }));
    expect(planningMocks.deleteDependency).not.toHaveBeenCalled();
    expect(planningMocks.updateActivity).toHaveBeenCalledWith(expect.objectContaining({ id: "b", body: expect.objectContaining({ rowVersion: "fresh-b" }) }));
  });

  it("shows server save failures to the user", async () => {
    planningMocks.updateActivity.mockRejectedValueOnce(new Error("Version conflict"));
    renderDetails();
    fireEvent.change(await screen.findByDisplayValue("Task a"), { target: { value: "Updated task" } });
    fireEvent.click(screen.getByRole("button", { name: "common.save" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Version conflict");
  });
});
