import { Outlet, MemoryRouter, Route, Routes } from "react-router-dom";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectRisksTab } from "../features/projects/ProjectRisksTab";

const fixtures = vi.hoisted(() => ({
  riskFilters: vi.fn(),
  risks: [{ id: "risk-1", title: "Supplier delay", riskLevel: "High", status: "Identified", ownerResourceName: "Alex" }],
  workItems: [] as Array<{ id: string; title: string; type: string; status: string; dueDate?: string }>,
}));

vi.mock("@features/risks/api/risks", () => ({
  useRisks: (filters: unknown, enabled: boolean) => {
    fixtures.riskFilters(filters, enabled);
    return { data: { items: fixtures.risks }, isLoading: false, isError: false };
  },
}));

vi.mock("@api/workItems", () => ({
  useWorkItems: () => ({ data: { items: fixtures.workItems }, isLoading: false, isError: false }),
  useWorkItemStatuses: () => ({ data: [] }),
}));

function ProjectOutlet() {
  return <Outlet context={{ projectId: "project-1", project: { name: "Delivery Project" } }} />;
}

describe("project risk preview parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.riskFilters.mockReset();
    fixtures.workItems = [];
  });

  it("loads the source preview page size and links to the seeded risk creation route", () => {
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/risks-issues"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="risks-issues" element={<ProjectRisksTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(fixtures.riskFilters).toHaveBeenCalledWith({ projectId: "project-1", pageSize: 5 }, true);
    expect(screen.getByRole("link", { name: "Create Risk" })).toHaveAttribute("href", "/risks-issues/risks/new?projectId=project-1");
    expect(screen.getByRole("link", { name: "Create Risk" }).querySelector("button")).toHaveClass("bg-primary");
    expect(screen.getAllByRole("link", { name: "View all" })[0].querySelector("svg")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Delivery Project: Project risks" })).toHaveClass("text-blue-700", "dark:text-blue-300");
    expect(screen.getByRole("link", { name: "Supplier delay" })).toHaveAttribute("href", "/risks-issues/risks/risk-1?from=project&projectId=project-1");
  });

  it("formats work-item due dates with the source locale conventions", () => {
    fixtures.workItems = [{ id: "item-1", title: "Prepare release", type: "Task", status: "InProgress", dueDate: "2026-02-03T12:00:00Z" }];

    const { rerender } = render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/risks-issues"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="risks-issues" element={<ProjectRisksTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(screen.getByText("3 Feb 2026")).toBeInTheDocument();

    window.localStorage.setItem("nexus-locale", "ar");
    rerender(
      <I18nProvider key="ar">
        <MemoryRouter initialEntries={["/projects/project-1/risks-issues"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="risks-issues" element={<ProjectRisksTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(screen.getByText(new Intl.DateTimeFormat("ar-EG", { day: "numeric", month: "short", year: "numeric" }).format(new Date("2026-02-03T12:00:00Z")))).toBeInTheDocument();
  });

  it("includes a trailing navigation affordance on task and issue preview rows", () => {
    fixtures.workItems = [{ id: "item-1", title: "Prepare release", type: "Task", status: "InProgress" }];
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-1/risks-issues"]}>
          <Routes>
            <Route path="/projects/:id" element={<ProjectOutlet />}>
              <Route path="risks-issues" element={<ProjectRisksTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>,
    );

    const taskLink = screen.getByRole("link", { name: /Prepare release/ });
    expect(taskLink).toHaveAttribute("href", "/work-items/item-1");
    expect(taskLink.querySelectorAll("svg")).toHaveLength(2);
  });
});
