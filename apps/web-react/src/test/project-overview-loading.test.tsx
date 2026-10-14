import { Outlet, MemoryRouter, Route, Routes } from "react-router-dom";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectOverviewTab } from "../features/projects/ProjectOverviewTab";

const fixtures = vi.hoisted(() => ({
  query: { isLoading: true, isError: false, data: undefined, refetch: vi.fn() },
}));

vi.mock("../features/projects/api/projects", () => ({
  useProjectOverview: () => fixtures.query,
}));

const project = {
  id: "project-1",
  code: "PRJ-0042",
  name: "Cloud Modernization",
  status: "Active",
  priority: "High",
  health: "Amber",
  ragStatus: "Amber",
  currency: "USD",
  progress: 35,
  members: [],
  description: "Useful project description",
  businessCase: "Modernize critical services",
  notes: "Preserve existing delivery controls",
  createdAt: "2026-10-01T00:00:00.000Z",
};

function ProjectOutlet() {
  return <Outlet context={{ projectId: "project-1", project }} />;
}

function renderOverview() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/projects/project-1/overview"]}>
        <Routes>
          <Route path="/projects/:id" element={<ProjectOutlet />}>
            <Route path="overview" element={<ProjectOverviewTab />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("project overview partial loading", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.query = { isLoading: true, isError: false, data: undefined, refetch: vi.fn() };
  });

  it("keeps project information and shell-based progress visible while overview data loads", () => {
    renderOverview();

    expect(screen.getByText("Project Information")).toBeInTheDocument();
    expect(screen.getByText("Useful project description")).toBeInTheDocument();
    expect(screen.getByText("Modernize critical services")).toBeInTheDocument();
    expect(screen.getByText("Preserve existing delivery controls")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: /35%/ })).toBeInTheDocument();
    expect(screen.getByRole("status", { name: /Loading/ })).toBeInTheDocument();
  });

  it("keeps project information available when only the overview read model fails", () => {
    fixtures.query = { isLoading: false, isError: true, data: undefined, refetch: vi.fn() };
    renderOverview();

    expect(screen.getByText("Project Information")).toBeInTheDocument();
    expect(screen.getByText("Useful project description")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: /35%/ })).toBeInTheDocument();
  });
});
