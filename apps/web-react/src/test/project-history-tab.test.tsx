import { Outlet, MemoryRouter, Route, Routes } from "react-router-dom";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectHistoryTab } from "../features/projects/ProjectGovernanceTabs";

const fixtures = vi.hoisted(() => ({
  managerHistory: [
    {
      id: "handover-1",
      projectId: "project-1",
      projectManagerId: "manager-1",
      projectManagerName: "Jane Manager",
      projectManagerEmail: "jane@example.com",
      startDate: "2026-01-01T00:00:00Z",
      endDate: null,
      isCurrent: true,
      handoverFromProjectManagerId: null,
      handoverFromProjectManagerName: null,
      handoverDate: null,
      handoverReason: null,
      handoverNotes: null,
      createdAt: "2026-01-01T00:00:00Z",
      createdBy: "System",
    },
  ],
  managerHistoryQuery: vi.fn(),
}));

vi.mock("@features/projects/api/projects", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/projects/api/projects")>();
  return {
    ...actual,
    useProjectManagerHistory: () => {
      fixtures.managerHistoryQuery();
      return { data: fixtures.managerHistory, isLoading: false, isError: false, refetch: vi.fn() } as unknown as ReturnType<typeof actual.useProjectManagerHistory>;
    },
  };
});

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { permissions: [] as string[] } }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ canMutate: false, effectiveReadOnly: false }),
}));

function ProjectOutlet() {
  return <Outlet context={{ projectId: "project-1" }} />;
}

function renderTab() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/projects/project-1/history"]}>
        <Routes>
          <Route path="/projects/:id" element={<ProjectOutlet />}>
            <Route path="history" element={<ProjectHistoryTab />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("project history tab source parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.managerHistoryQuery.mockReset();
  });

  it("queries and renders manager handover history", () => {
    renderTab();
    expect(fixtures.managerHistoryQuery).toHaveBeenCalledTimes(1);

    expect(screen.getByText("Project Manager History")).toBeInTheDocument();
    expect(screen.getByText("Jane Manager")).toBeInTheDocument();
    expect(screen.queryByText("Change log")).not.toBeInTheDocument();
  });

  it("renders Arabic manager history labels", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderTab();

    expect(screen.getByText("سجل مديري المشروع")).toBeInTheDocument();
    expect(screen.getByText("Jane Manager")).toBeInTheDocument();
  });
});
