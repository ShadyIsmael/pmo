import { fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { ProjectGanttPage } from "../features/projects/ProjectWorkspaceViews";

const ganttMocks = vi.hoisted(() => ({
  projects: [
    { id: "active-1", publicId: "public-active-1", name: "Active project", code: "PRJ-100", status: "Active", startDate: "2026-01-15", endDate: "2027-03-20" },
    { id: "completed-1", publicId: "public-completed-1", name: "Completed project", code: "PRJ-101", status: "Completed", startDate: "2025-01-01", endDate: "2025-12-31" },
  ],
  plans: [{ id: "plan-1", name: "Delivery plan", status: "Working" }],
  activities: [
    { id: "activity-1", planVersionId: "plan-1", code: "A-1", name: "Analysis phase", type: "Task", duration: 30, startDate: "2026-02-01", endDate: "2026-03-03", progress: 50, sortOrder: 1, critical: false, rowVersion: "v1" },
    { id: "activity-2", planVersionId: "plan-1", code: "A-2", name: "Build delivery", type: "Task", duration: 45, startDate: "2026-04-01", endDate: "2026-06-01", progress: 20, sortOrder: 2, critical: true, rowVersion: "v1" },
    { id: "activity-3", planVersionId: "plan-1", code: "M-1", name: "Phase 1 milestone", type: "Milestone", duration: 0, startDate: "2026-06-01", progress: 0, sortOrder: 3, critical: false, rowVersion: "v1" },
  ],
}));

vi.mock("../features/projects/api/projects", () => ({
  useProjects: () => ({ data: { items: ganttMocks.projects }, isLoading: false, isError: false }),
}));

vi.mock("../features/projects/api/planningFull", () => ({
  usePlanningPlansFull: () => ({ data: ganttMocks.plans, isLoading: false, isError: false }),
  usePlanningActivitiesFull: () => ({ data: ganttMocks.activities, isLoading: false, isError: false }),
}));

function renderGantt() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/projects/gantt"]}>
        <Routes><Route path="/projects/gantt" element={<ProjectGanttPage />} /></Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  window.localStorage.setItem("nexus-locale", "en");
});

describe("global project Gantt parity", () => {
  it("renders API-backed activities on a monthly timeline and excludes completed projects", () => {
    renderGantt();

    expect(screen.getByRole("option", { name: /Active project/ })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: /Completed project/ })).not.toBeInTheDocument();
    expect(screen.getByText("Task / Phase")).toBeInTheDocument();
    expect(screen.getByText("Jan 2026")).toBeInTheDocument();
    expect(screen.getByText("Mar 2027")).toBeInTheDocument();
    expect(screen.getByText("Jan 15, 2026 – Mar 20, 2027")).toBeInTheDocument();
    expect(screen.getAllByTestId("gantt-activity-row")).toHaveLength(3);
    expect(screen.getByRole("img", { name: "Build delivery: 20%" })).toBeInTheDocument();
  });

  it("filters planning activities and shows an empty result message", () => {
    renderGantt();
    fireEvent.click(screen.getByRole("button", { name: "Filter" }));
    const filter = screen.getByRole("textbox", { name: "Filter tasks and phases" });
    fireEvent.change(filter, { target: { value: "build" } });

    const rows = screen.getAllByTestId("gantt-activity-row");
    expect(rows).toHaveLength(1);
    expect(within(rows[0]!).getByText("Build delivery")).toBeInTheDocument();
    expect(screen.queryByText("Analysis phase")).not.toBeInTheDocument();

    fireEvent.change(filter, { target: { value: "does not exist" } });
    expect(screen.getByText("No activities match your search.")).toBeInTheDocument();
  });

  it("changes the timeline scale when Zoom is activated", () => {
    renderGantt();
    fireEvent.click(screen.getByRole("button", { name: "Zoom 100%" }));
    expect(screen.getByRole("button", { name: "Zoom 125%" })).toBeInTheDocument();
  });
});
