import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectWorkflowSettings } from "../features/projects/ProjectWorkflowSettings";
import { ProjectHeader } from "../features/projects/components/ProjectHeader";
import type { ProjectDto } from "../features/projects/api/projects";

const fixtures = vi.hoisted(() => ({
  get: vi.fn(),
  put: vi.fn(),
  permissions: ["Project.View", "Project.Edit"] as string[],
  canMutate: true,
  effectiveReadOnly: false,
}));

vi.mock("@api/client", () => ({ api: { get: fixtures.get, put: fixtures.put, post: vi.fn(), patch: vi.fn(), delete: vi.fn() } }));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: fixtures.canMutate, effectiveReadOnly: fixtures.effectiveReadOnly }) }));

const projectSettings = {
  projectId: "project-1",
  domains: [{
    id: "configuration-task",
    projectId: "project-1",
    domain: "Task",
    mode: "Custom",
    workflowDefinitionId: "workflow-task",
    workflowDefinitionName: "Custom Tasks",
    effectiveWorkflowVersionId: "version-3",
    effectiveWorkflowVersionNumber: 3,
    isPublished: true,
    configuredAt: "2026-01-01T00:00:00.000Z",
    configuredBy: "admin",
    rowVersion: "AQID",
  }],
};

const headerProject: ProjectDto = {
  id: "project-1", tenantId: "tenant-1", code: "PRJ-001", name: "Delivery Project", status: "InProgress", priority: "High",
  currency: "USD", progress: 35, health: "Green", ragStatus: "Green", confidenceLevel: "Green",
  createdAt: "2026-01-01T00:00:00.000Z", createdBy: "admin", isDeleted: false, members: [],
};

function createQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
}

function renderSettings() {
  return render(
    <QueryClientProvider client={createQueryClient()}>
      <I18nProvider><ProjectWorkflowSettings projectId="project-1" /></I18nProvider>
    </QueryClientProvider>,
  );
}

function CurrentLocation() {
  const location = useLocation();
  return <output data-testid="location">{location.pathname}</output>;
}

describe("project workflow settings parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.permissions = ["Project.View", "Project.Edit"];
    fixtures.canMutate = true;
    fixtures.effectiveReadOnly = false;
    fixtures.get.mockReset().mockImplementation(async (url: string, params?: Record<string, string | number | boolean | undefined>) => {
      if (url === "/api/projects/project-1/workflows") return projectSettings;
      if (url === "/api/users/lookup") return [];
      if (url === "/api/portfolios" || url === "/api/programs") return { items: [] };
      if (url === "/api/organization/units" || url === "/api/currencies") return [];
      if (url === "/api/workflows" && params?.domain === "Task") return [
        { id: "workflow-task", name: "Custom Tasks", code: "CUSTOM-TASK", domain: "Task", entityType: "WorkItem", isActive: true, activePublishedVersionId: "version-3", activePublishedVersionNumber: 3, activePublishedLifecycleStatus: "Published", versionCount: 3 },
        { id: "draft-only", name: "Draft Only", code: "DRAFT", domain: "Task", entityType: "WorkItem", isActive: true, activePublishedVersionNumber: 1, activePublishedLifecycleStatus: "Draft", versionCount: 1 },
      ];
      if (url === "/api/workflows") return [];
      throw new Error(`Unexpected request: ${url}`);
    });
    fixtures.put.mockReset().mockImplementation(async (url: string, body?: Record<string, unknown>) => {
      if (url === "/api/projects/project-1") return { ...headerProject, ...body };
      return { ...projectSettings.domains[0], mode: "Standard", workflowDefinitionId: undefined };
    });
  });

  afterEach(() => vi.restoreAllMocks());

  it("reads the canonical domains contract, only offers published versions, and sends rowVersion when saving", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    renderSettings();

    await screen.findByRole("option", { name: "Custom Tasks · v3" });
    const taskSelector = screen.getByRole("combobox", { name: "Tasks" });
    expect(screen.getByRole("option", { name: "Custom Tasks · v3" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: /Draft Only/ })).not.toBeInTheDocument();

    fireEvent.change(taskSelector, { target: { value: "Standard" } });

    await waitFor(() => expect(fixtures.put).toHaveBeenCalledWith("/api/projects/project-1/workflows/Task", {
      mode: "Standard",
      workflowDefinitionId: undefined,
      rowVersion: "AQID",
    }));
  });

  it("surfaces the project workflow API error instead of replacing it with generic copy", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    fixtures.put.mockRejectedValueOnce(new Error("The selected workflow is no longer published."));
    renderSettings();

    const taskSelector = await screen.findByRole("combobox", { name: "Tasks" });
    fireEvent.change(taskSelector, { target: { value: "Standard" } });

    expect(await screen.findByRole("alert")).toHaveTextContent("The selected workflow is no longer published.");
  });

  it("keeps workflow configuration controls read-only without Project.Edit", async () => {
    fixtures.permissions = ["Project.View"];
    renderSettings();

    expect(await screen.findByRole("combobox", { name: "Tasks" })).toBeDisabled();
    expect(fixtures.put).not.toHaveBeenCalled();
  });

  it("keeps workflow configuration controls read-only in ReadOnlyViewAs", async () => {
    fixtures.canMutate = false;
    fixtures.effectiveReadOnly = true;
    renderSettings();

    expect(await screen.findByRole("combobox", { name: "Tasks" })).toBeDisabled();
    expect(screen.getByText("Read-only view")).toBeInTheDocument();
    expect(fixtures.put).not.toHaveBeenCalled();
  });

  it("shows the project settings action and opens the existing workflow settings route", async () => {
    const queryClient = createQueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <I18nProvider>
          <MemoryRouter initialEntries={["/projects/project-1/overview"]}>
            <ProjectHeader project={headerProject} />
            <CurrentLocation />
          </MemoryRouter>
        </I18nProvider>
      </QueryClientProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Project settings" }));

    expect(screen.getByTestId("location")).toHaveTextContent("/projects/project-1/workflows");
  });

  it("opens the full source-parity project edit sheet and saves through the project API", async () => {
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider>
          <MemoryRouter initialEntries={["/projects/project-1/overview"]}>
            <ProjectHeader project={headerProject} />
          </MemoryRouter>
        </I18nProvider>
      </QueryClientProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Edit" }));
    const sheet = await screen.findByRole("dialog", { name: "Edit Project" });
    expect(screen.getByText("Ownership & Hierarchy")).toBeInTheDocument();
    expect(screen.getByText("Financial Foundation")).toBeInTheDocument();
    expect(within(sheet).getAllByText("Project Manager")).toHaveLength(2);
    expect(within(sheet).getAllByText("Department")).toHaveLength(2);

    const nameField = within(sheet).getByPlaceholderText("Name");
    await user.clear(nameField);
    await user.type(nameField, "Delivery Project Revised");
    await user.click(within(sheet).getByRole("button", { name: "Update Project" }));

    await waitFor(() => expect(fixtures.put).toHaveBeenCalledWith("/api/projects/project-1", expect.objectContaining({
      name: "Delivery Project Revised",
      status: "InProgress",
      priority: "High",
      currency: "USD",
    })));
  });

  it("renders workflow-domain labels in Arabic", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderSettings();

    expect(await screen.findByRole("combobox", { name: "المهام" })).toBeInTheDocument();
    expect(screen.getByText("مسارات عمل المشروع")).toBeInTheDocument();
  });

  it("hides the project settings entry point without Project.Edit", () => {
    fixtures.permissions = ["Project.View"];
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider><MemoryRouter><ProjectHeader project={headerProject} /></MemoryRouter></I18nProvider>
      </QueryClientProvider>,
    );

    expect(screen.queryByRole("button", { name: "Project settings" })).not.toBeInTheDocument();
  });
});
