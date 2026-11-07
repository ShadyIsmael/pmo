import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PlanningActivityDetailsSheet } from "../features/projects/PlanningActivityDetailsSheet";
import type { PlanActivityDto } from "../features/projects/api/planningFull";
import { I18nProvider } from "../i18n/messages";

const mocks = vi.hoisted(() => ({
  createAssignmentsBatch: vi.fn(),
  updateActivity: vi.fn(),
  assignments: [] as Array<Record<string, unknown>>,
  members: [
    { resourceId: "resource-a", resourceName: "Resource A", allocationPercentage: 50, status: "Active", startDate: "2026-01-01", endDate: "2026-12-31" },
    { resourceId: "resource-b", resourceName: "Resource B", allocationPercentage: 50, status: "Active", startDate: "2026-01-01", endDate: "2026-12-31" },
  ],
}));

vi.mock("@tanstack/react-query", () => ({
  useQueries: ({ queries }: { queries: Array<{ queryKey: unknown[] }> }) => queries.map((query) => {
    const resourceId = String(query.queryKey[4]);
    return {
      data: { resourceId, projectAllocationPercentage: 50, remainingAssignableHours: 24, isScheduleWithinMembership: true },
      isError: false,
      isFetching: false,
      isLoading: false,
    };
  }),
}));

vi.mock("../features/projects/api/planningFull", () => ({
  planningApiFull: { assignmentCapacity: vi.fn() },
  planningKeys: { assignmentCapacity: (...parts: string[]) => ["planning-full", "assignment-capacity", ...parts] },
  usePlanningCalendarsFull: () => ({ data: [], isLoading: false }),
  usePlanningFullMutations: () => ({
    updateActivity: { mutateAsync: mocks.updateActivity, isPending: false },
    createAssignmentsBatch: { mutateAsync: mocks.createAssignmentsBatch, isPending: false },
  }),
  usePlanningSchedulingExplanationFull: () => ({ data: undefined, isLoading: false, isError: false }),
  usePlanningActivitiesFull: () => ({ data: [activity], isLoading: false }),
  usePlanningAssignmentsFull: () => ({ data: mocks.assignments, isLoading: false, isError: false }),
  usePlanningDependenciesFull: () => ({ data: [], isLoading: false }),
  usePlanningResourcesFull: () => ({ data: [], isLoading: false }),
  usePlanningWbsFull: () => ({ data: [], isLoading: false }),
}));

vi.mock("../features/projects/api/projects", () => ({
  useProjectTeam: () => ({ data: mocks.members, isLoading: false, isError: false }),
}));

const activity: PlanActivityDto = {
  id: "activity-1",
  planVersionId: "working-1",
  code: "T-1",
  name: "Prepare migration plan",
  type: "Task",
  startDate: "2026-01-04T00:00:00",
  endDate: "2026-01-10T00:00:00",
  duration: 5,
  estimateHours: 40,
  critical: false,
  rowVersion: "AQID",
};

describe("PlanningActivityDetailsSheet resource assignments", () => {
  beforeEach(() => {
    mocks.createAssignmentsBatch.mockReset().mockResolvedValue([]);
    mocks.updateActivity.mockReset().mockResolvedValue(activity);
    mocks.assignments = [];
    mocks.members = [
      { resourceId: "resource-a", resourceName: "Resource A", allocationPercentage: 50, status: "Active", startDate: "2026-01-01", endDate: "2026-12-31" },
      { resourceId: "resource-b", resourceName: "Resource B", allocationPercentage: 50, status: "Active", startDate: "2026-01-01", endDate: "2026-12-31" },
    ];
  });
  afterEach(() => cleanup());

  it("adds resources immediately, recalculates shares, supports removal, and saves only new assignments", async () => {
    const user = userEvent.setup();
    const translate = (key: string) => key.split(".").at(-1) ?? key;
    render(<I18nProvider><PlanningActivityDetailsSheet
      open
      onOpenChange={vi.fn()}
      activity={activity}
      row={{ activity, rowNumber: 1 } as never}
      projectId="project-1"
      planId="plan-1"
      editable
      t={translate}
    /></I18nProvider>);

    await user.click(screen.getByRole("tab", { name: "resources" }));
    let selector = screen.getByLabelText("selectProjectResource");
    await waitFor(() => expect(selector).toBeEnabled());
    fireEvent.change(selector, { target: { value: "resource-a" } });
    expect(await screen.findByText("Resource A")).toBeInTheDocument();
    selector = screen.getByLabelText("selectProjectResource");
    expect(selector.querySelector('option[value="resource-b"]')).not.toBeNull();
    fireEvent.change(selector, { target: { value: "resource-b" } });
    expect(await screen.findByText("Resource B")).toBeInTheDocument();
    selector = screen.getByLabelText("selectProjectResource");
    expect(selector.querySelector('option[value="resource-b"]')).toBeNull();
    expect(screen.getAllByText("40 hours").length).toBeGreaterThanOrEqual(2);
    expect(screen.getAllByText("0 hours").length).toBeGreaterThan(0);

    const pendingRows = screen.getAllByText(/proposedAssignedWork/);
    expect(pendingRows.length).toBeGreaterThan(0);
    await user.click(screen.getAllByRole("button", { name: /^removePendingResource/ })[1]!);
    selector = screen.getByLabelText("selectProjectResource");
    await waitFor(() => expect(selector.querySelector('option[value="resource-b"]')).not.toBeNull());
    expect(screen.getAllByText("20 hours").length).toBeGreaterThanOrEqual(2);

    await user.click(screen.getByRole("button", { name: "saveResourceAssignments" }));
    await waitFor(() => expect(mocks.createAssignmentsBatch).toHaveBeenCalledWith({
      activityId: activity.id,
      assignments: [{
        resourceId: "resource-a",
        assignedWorkHours: 20,
        startDate: "2026-01-04",
        endDate: "2026-01-10",
      }],
    }));
    expect(await screen.findByText("resourceAssignmentsSaved")).toBeInTheDocument();
  });
});
