import { Outlet, MemoryRouter, Route, Routes } from "react-router-dom";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectHistoryTab } from "../features/projects/ProjectGovernanceTabs";

// The history tab is the audit workspace. Manager handover history is rendered by the
// delivery/handover surface, so this test only guards the audit entry point and its
// permission gate.
const fixtures = vi.hoisted(() => ({
  auditTrailQuery: vi.fn(),
}));

vi.mock("@features/projects/api/projects", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/projects/api/projects")>();
  return {
    ...actual,
    // The audit workspace is covered by its own backend contract; here it only needs to
    // resolve to an empty, permission-denied state without touching the network.
    useProjectAuditTrail: () => {
      fixtures.auditTrailQuery();
      return { data: undefined, isLoading: false, isError: false, isFetching: false, refetch: vi.fn() };
    },
    useProjectAuditFacets: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
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
    fixtures.auditTrailQuery.mockReset();
  });

  it("renders the audit workspace without the legacy manager history card", () => {
    renderTab();
    expect(fixtures.auditTrailQuery).toHaveBeenCalledTimes(1);

    expect(screen.getByText("Audit Trail")).toBeInTheDocument();
    // Callers without Audit.View see an explicit no-access state instead of an
    // unfiltered log.
    expect(screen.getByText("Audit trail access required")).toBeInTheDocument();
    expect(screen.queryByText("Project Manager History")).not.toBeInTheDocument();
    expect(screen.queryByText("Change log")).not.toBeInTheDocument();
  });

  it("renders Arabic audit workspace labels", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderTab();

    expect(screen.getByText("سجل التدقيق")).toBeInTheDocument();
    expect(screen.getByText("يلزم إذن عرض سجل التدقيق")).toBeInTheDocument();
    expect(screen.queryByText("سجل مديري المشروع")).not.toBeInTheDocument();
  });
});
