import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { I18nProvider } from "@i18n/messages";
import { ProjectChangeTrackerTab } from "@features/projects/ProjectGovernanceTabs";

const fixtures = vi.hoisted(() => ({
  user: { id: "requester-1", permissions: ["ChangeRequest.View"] as string[] } as { id: string; permissions: string[] } | null,
  status: "authenticated",
  useProjectChangeRequests: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: fixtures.user, status: fixtures.status }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, effectiveReadOnly: false }) }));
vi.mock("@features/projects/api/projects", () => ({
  useProjectChangeRequests: fixtures.useProjectChangeRequests,
  useCreateProjectChangeRequest: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useCancelProjectChangeRequest: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

const project = { id: "project-1", name: "Core Modernization", code: "PRJ-1", budget: 1000, currency: "USD" };
const request = {
  id: "change-1", projectId: "project-1", number: 7, type: "Baseline", reason: "Adjust the delivery baseline",
  budgetChangeAmount: 125.5, proposedProjectBudget: 1125.5, status: "Pending", requestedBy: "requester-1",
  requestedByName: "Alex Admin", requestedAt: "2026-10-01T10:00:00Z",
};

function renderTab(locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(
    <MemoryRouter initialEntries={["/projects/project-1/change-tracker"]}>
      <I18nProvider>
        <Routes>
          <Route path="/projects/:id" element={<Outlet context={{ projectId: project.id, project }} />}>
            <Route path="change-tracker" element={<ProjectChangeTrackerTab />} />
          </Route>
        </Routes>
      </I18nProvider>
    </MemoryRouter>,
  );
}

describe("project change tracker permission parity", () => {
  beforeEach(() => {
    fixtures.user = { id: "requester-1", permissions: ["ChangeRequest.View"] };
    fixtures.status = "authenticated";
    fixtures.useProjectChangeRequests.mockReset().mockImplementation(() => ({ data: [request], isLoading: false, isError: false, isFetching: false, refetch: vi.fn() }));
  });

  it("does not request or display cached tracker rows without ChangeRequest.View", () => {
    fixtures.user = { id: "requester-1", permissions: ["ChangeRequest.Manage"] };

    renderTab();

    expect(fixtures.useProjectChangeRequests).toHaveBeenCalledWith("project-1", false);
    expect(screen.getByRole("alert")).toHaveTextContent("You do not have permission to view this project's change requests.");
    expect(screen.queryByText("Adjust the delivery baseline")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Submit Change Request" })).not.toBeInTheDocument();
  });

  it("loads the real project requests for users with ChangeRequest.View", () => {
    renderTab();

    expect(fixtures.useProjectChangeRequests).toHaveBeenCalledWith("project-1", true);
    expect(screen.getByText("Adjust the delivery baseline")).toBeInTheDocument();
    expect(screen.getByRole("row", { name: /Adjust the delivery baseline/ })).toHaveTextContent("+$125.5");
  });

  it("waits for authentication before displaying an access denial", () => {
    fixtures.status = "loading";
    fixtures.user = null;

    const { container } = renderTab();

    expect(fixtures.useProjectChangeRequests).toHaveBeenCalledWith("project-1", false);
    expect(container.querySelector('[role="alert"]')).not.toBeInTheDocument();
    expect(screen.queryByText("You do not have permission to view this project's change requests.")).not.toBeInTheDocument();
  });

  it("localizes the permission message in Arabic", () => {
    fixtures.user = { id: "requester-1", permissions: [] };

    renderTab("ar");

    expect(screen.getByRole("alert")).toHaveTextContent("ليس لديك صلاحية عرض طلبات تغيير هذا المشروع.");
  });
});
