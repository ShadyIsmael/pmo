import { Outlet, MemoryRouter, Route, Routes } from "react-router-dom";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import arMessages from "../i18n/messages/ar.json";
import enMessages from "../i18n/messages/en.json";
import { ApiError } from "../api/client";
import { ProjectHandoverTab } from "../features/projects/ProjectGovernanceTabs";

const fixtures = vi.hoisted(() => ({
  userPermissions: ["Handover.View"] as string[],
  requestsData: undefined as unknown,
  requestsLoading: false,
  requestsError: false,
  requestsErrorObj: null as unknown,
  requestsEnabled: false,
  requestsRefetch: vi.fn(),
  capabilitiesData: {
    canInitiate: false,
    canAdminInitiate: false,
    canAssignInitial: false,
    canCancelOwnPending: false,
  },
  capabilitiesLoading: false,
  capabilitiesError: false,
  capabilitiesErrorObj: null as unknown,
  capabilitiesEnabled: false,
  capabilitiesRefetch: vi.fn(),
}));

vi.mock("@features/projects/api/projects", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/projects/api/projects")>();
  return {
    ...actual,
    useProjectHandoverRequests: (projectId: string, enabled = true) => {
      fixtures.requestsEnabled = Boolean(projectId) && enabled;
      return {
      data: fixtures.requestsData,
      isLoading: fixtures.requestsLoading,
      isError: fixtures.requestsError,
      error: fixtures.requestsErrorObj,
      refetch: fixtures.requestsRefetch,
      };
    },
    useProjectHandoverCapabilities: (projectId: string, enabled = true) => {
      fixtures.capabilitiesEnabled = Boolean(projectId) && enabled;
      return {
      data: fixtures.capabilitiesData,
      isLoading: fixtures.capabilitiesLoading,
      isSuccess: !fixtures.capabilitiesLoading && !fixtures.capabilitiesError,
      isError: fixtures.capabilitiesError,
      error: fixtures.capabilitiesErrorObj,
      refetch: fixtures.capabilitiesRefetch,
      };
    },
    useProjectHandoverManagers: () => ({ data: [], isLoading: false, isError: false }),
    useCreateProjectHandoverRequest: () => ({ mutateAsync: vi.fn(), isPending: false }),
    useCancelProjectHandoverRequest: () => ({ mutateAsync: vi.fn(), isPending: false }),
  };
});

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { id: "user-1", permissions: fixtures.userPermissions } }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ canMutate: true, effectiveReadOnly: false }),
}));

function ProjectOutlet() {
  return <Outlet context={{ projectId: "project-1", project: { id: "project-1", name: "Test Project" } }} />;
}

function renderTab(initialLocale = "en") {
  window.localStorage.setItem("nexus-locale", initialLocale);
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/projects/project-1/handover"]}>
        <Routes>
          <Route path="/projects/:id" element={<ProjectOutlet />}>
            <Route path="handover" element={<ProjectHandoverTab />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("ProjectHandoverTab error message display", () => {
  beforeEach(() => {
    fixtures.userPermissions = ["Handover.View"];
    fixtures.requestsData = undefined;
    fixtures.requestsLoading = false;
    fixtures.requestsError = false;
    fixtures.requestsErrorObj = null;
    fixtures.requestsEnabled = false;
    fixtures.requestsRefetch.mockClear();
    fixtures.capabilitiesData = {
      canInitiate: false,
      canAdminInitiate: false,
      canAssignInitial: false,
      canCancelOwnPending: false,
    };
    fixtures.capabilitiesLoading = false;
    fixtures.capabilitiesError = false;
    fixtures.capabilitiesErrorObj = null;
    fixtures.capabilitiesEnabled = false;
    fixtures.capabilitiesRefetch.mockClear();
    window.localStorage.setItem("nexus-locale", "en");
  });

  it("shows a persistent localized unavailable message for a service-denial 404", () => {
    fixtures.requestsError = true;
    fixtures.requestsErrorObj = new ApiError(404, "You do not have permission to view handovers", "NOT_FOUND");

    renderTab("en");

    const alert = screen.getByRole("alert");
    expect(alert).toBeInTheDocument();
    expect(alert).toHaveTextContent(enMessages.projects.handoverUnavailable);
    expect(alert).not.toHaveTextContent("You do not have permission to view handovers");
    expect(screen.queryByRole("button", { name: "Retry" })).not.toBeInTheDocument();
    expect(fixtures.requestsRefetch).not.toHaveBeenCalled();
  });

  it("localizes a service-denial 404 in Arabic without exposing the English API message", () => {
    fixtures.requestsError = true;
    fixtures.requestsErrorObj = new ApiError(404, "You do not have permission to view handovers", "NOT_FOUND");

    renderTab("ar");

    const alert = screen.getByRole("alert");
    expect(alert).toBeInTheDocument();
    expect(alert).toHaveTextContent(arMessages.projects.handoverUnavailable);
    expect(alert).not.toHaveTextContent("You do not have permission to view handovers");
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(fixtures.requestsRefetch).not.toHaveBeenCalled();
  });

  it("resolves Organization-owned access from project capabilities when the global permission snapshot lacks View", () => {
    fixtures.userPermissions = [];
    fixtures.requestsData = [];

    renderTab("en");

    expect(fixtures.capabilitiesEnabled).toBe(true);
    expect(fixtures.requestsEnabled).toBe(true);
    expect(screen.getByText(enMessages.projects.noHandoverRequests)).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("shows a localized permission message and skips the requests query when scoped capabilities deny access", () => {
    fixtures.userPermissions = [];
    fixtures.capabilitiesError = true;
    fixtures.capabilitiesErrorObj = new ApiError(403, "Forbidden", "FORBIDDEN");

    renderTab("en");

    const alert = screen.getByRole("alert");
    expect(alert).toBeInTheDocument();
    expect(alert).toHaveTextContent(enMessages.projects.handoverUnavailable);
    expect(fixtures.capabilitiesEnabled).toBe(true);
    expect(fixtures.requestsEnabled).toBe(false);
    expect(fixtures.capabilitiesRefetch).not.toHaveBeenCalled();
    expect(fixtures.requestsRefetch).not.toHaveBeenCalled();
  });

  it("localizes scoped permission denial in Arabic without exposing the API response", () => {
    fixtures.capabilitiesError = true;
    fixtures.capabilitiesErrorObj = new ApiError(404, "You do not have permission to view handovers", "NOT_FOUND");

    renderTab("ar");

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent(arMessages.projects.handoverUnavailable);
    expect(alert).not.toHaveTextContent("You do not have permission to view handovers");
    expect(fixtures.requestsEnabled).toBe(false);
  });

  it("renders empty state when permission is granted and there are no requests", () => {
    fixtures.requestsData = [];

    renderTab("en");

    expect(screen.getByText("No project manager handover requests.")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("renders an authorized request normally", () => {
    fixtures.requestsData = [{
      id: "request-1",
      projectId: "project-1",
      projectName: "Test Project",
      projectCode: "PRJ-1",
      currentProjectManagerId: "manager-1",
      currentProjectManagerName: "Current Manager",
      newProjectManagerId: "manager-2",
      newProjectManagerName: "Incoming Manager",
      effectiveHandoverDate: "2026-12-01T00:00:00Z",
      reason: "Planned transition",
      status: "Pending",
      initiationType: "ManagerInitiated",
      requestedBy: "user-1",
      requestedByName: "Requester",
      requestedAt: "2026-11-01T00:00:00Z",
      createdAt: "2026-11-01T00:00:00Z",
    }];

    renderTab("en");

    expect(screen.getByText("Current Manager → Incoming Manager")).toBeInTheDocument();
    expect(screen.getByText("Planned transition")).toBeInTheDocument();
    expect(fixtures.requestsEnabled).toBe(true);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
