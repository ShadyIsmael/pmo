import type * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectWorkflowSettings } from "../features/projects/ProjectWorkflowSettings";
import { ProjectHeader } from "../features/projects/components/ProjectHeader";
import type { ProjectDto } from "../features/projects/api/projects";

const fixtures = vi.hoisted(() => ({
  get: vi.fn(),
  put: vi.fn(),
  permissions: ["Project.View", "Project.Edit", "ProjectSettings.View", "ProjectSettings.Edit", "Workflow.View", "ProjectAnnualBudget.View", "ProjectAnnualBudget.ManageApprovalConfiguration", "ProjectFinancialFoundation.View"] as string[],
  canMutate: true,
  effectiveReadOnly: false,
}));

vi.mock("@api/client", async (importOriginal) => ({ ...await importOriginal<typeof import("@api/client")>(), api: { get: fixtures.get, put: fixtures.put, post: vi.fn(), patch: vi.fn(), delete: vi.fn() }, setApiErrorLocale: () => undefined }));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: fixtures.canMutate, effectiveReadOnly: fixtures.effectiveReadOnly }) }));
vi.mock("@components/ui/ConfirmProvider", () => ({
  useConfirm: () => vi.fn().mockResolvedValue(true),
  ConfirmProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

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

const annualBudgetApprovalConfiguration = {
  projectId: "public-project-1",
  projectName: "Delivery Project",
  configurationSource: "ProjectSettings" as const,
  financeApprovalMode: "Sequential" as const,
  approvalMode: "Sequential" as const,
  allowedModes: ["Sequential", "Parallel"] as const,
  configuredApprovers: [{ userId: "approver-1", displayName: "Approver One", position: 1, isEligible: true, issueCode: null }],
  eligibleApprovers: [
    { userId: "approver-1", displayName: "Approver One" },
    { userId: "approver-2", displayName: "Approver Two" },
  ],
  rowVersion: "AQID",
  projectSettingsRowVersion: "BAUG",
  isConfigured: true,
  isValid: true,
  canSubmitWithDefaults: true,
  validationIssues: [] as string[],
  canOverride: false,
};

const financialFoundation = {
  projectId: "public-project-1",
  projectCurrency: "USD",
  approvedBudget: 100000,
  approvedPaymentRequestAllocations: 25000,
  availableBudget: 75000,
  actualPaid: 0,
  outstandingPaymentBalance: 25000,
  financeApprovalMode: "Sequential" as const,
  rowVersion: "AQID",
};

const headerProject: ProjectDto = {
  id: "project-1", publicId: "public-project-1", tenantId: "tenant-1", code: "PRJ-001", name: "Delivery Project", status: "InProgress", priority: "High",
  currency: "USD", progress: 35, health: "Green", ragStatus: "Green", confidenceLevel: "Green",
  createdAt: "2026-01-01T00:00:00.000Z", createdBy: "admin", isDeleted: false, members: [],
};

let persistedWorkflowSettings: typeof projectSettings;

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

describe("project workflow settings parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.permissions = ["Project.View", "Project.Edit", "ProjectSettings.View", "ProjectSettings.Edit", "Workflow.View", "ProjectAnnualBudget.View", "ProjectAnnualBudget.ManageApprovalConfiguration", "ProjectFinancialFoundation.View"];
    persistedWorkflowSettings = structuredClone(projectSettings);
    annualBudgetApprovalConfiguration.eligibleApprovers = [
      { userId: "approver-1", displayName: "Approver One" },
      { userId: "approver-2", displayName: "Approver Two" },
    ];
    annualBudgetApprovalConfiguration.isValid = true;
    annualBudgetApprovalConfiguration.validationIssues = [];
    fixtures.canMutate = true;
    fixtures.effectiveReadOnly = false;
    fixtures.get.mockReset().mockImplementation(async (url: string, params?: Record<string, string | number | boolean | undefined>) => {
      if (url === "/api/projects/project-1/workflows") return persistedWorkflowSettings;
      if (url === "/api/projects/public-project-1/financial-foundation") return financialFoundation;
      if (url === "/api/projects/public-project-1/annual-budgets/approval-configuration") return annualBudgetApprovalConfiguration;
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
      if (url === "/api/projects/public-project-1") return { ...headerProject, ...body };
      if (url === "/api/projects/public-project-1/annual-budgets/approval-configuration") return annualBudgetApprovalConfiguration;
      if (url === "/api/projects/public-project-1/financial-foundation/approval-configuration") return financialFoundation;
      if (url.startsWith("/api/projects/project-1/workflows/")) {
        const domain = url.split("/").pop()!;
        const previous = persistedWorkflowSettings.domains.find((item) => item.domain === domain) ?? { ...projectSettings.domains[0], id: "configuration-" + domain, domain };
        const updated = { ...previous, ...(body ?? {}), domain, rowVersion: "AgMEBQ==", configuredBy: "admin", configuredAt: new Date().toISOString() };
        persistedWorkflowSettings = { ...persistedWorkflowSettings, domains: [...persistedWorkflowSettings.domains.filter((item) => item.domain !== domain), updated] };
        return updated;
      }
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
    await waitFor(() => expect(taskSelector).toHaveValue("Standard"));
    expect(await screen.findByRole("status")).toHaveTextContent("Saved");
  });

  it("shows a settings loading state, then reloads saved configuration from the API", async () => {
    let resolveSettings: ((value: typeof projectSettings) => void) | undefined;
    fixtures.get.mockImplementation(async (url: string) => {
      if (url === "/api/projects/project-1/workflows") return new Promise<typeof projectSettings>((resolve) => { resolveSettings = resolve; });
      if (url === "/api/workflows") return [];
      throw new Error("Unexpected request: " + url);
    });
    renderSettings();
    expect(screen.getByRole("status")).toHaveTextContent("Loading...");
    await waitFor(() => expect(resolveSettings).toBeDefined());
    resolveSettings!(persistedWorkflowSettings);
    expect(await screen.findByRole("combobox", { name: "Tasks" })).toHaveValue("workflow-task");
  });

  it("distinguishes workflow-definition errors from an empty published-workflow list and retries", async () => {
    let taskFailed = false;
    fixtures.get.mockImplementation(async (url: string, params?: Record<string, string | number | boolean | undefined>) => {
      if (url === "/api/projects/project-1/workflows") return persistedWorkflowSettings;
      if (url === "/api/workflows" && params?.domain === "Task" && !taskFailed) { taskFailed = true; throw new Error("network unavailable"); }
      if (url === "/api/workflows") return [];
      throw new Error("Unexpected request: " + url);
    });
    renderSettings();
    expect(await screen.findByRole("alert")).toHaveTextContent("Failed to load data.");
    const retry = await screen.findByRole("button", { name: "Retry" });
    await userEvent.click(retry);
    expect((await screen.findAllByText("No active Published workflows are available for this domain.")).length).toBeGreaterThan(0);
  });

  it("allows switching an existing custom selection to Standard without Workflow.View", async () => {
    fixtures.permissions = ["ProjectSettings.View", "ProjectSettings.Edit"];
    renderSettings();
    expect((await screen.findAllByText("Viewing published workflows requires Workflow.View. You can switch to Standard behavior.")).length).toBeGreaterThan(0);
    const selector = screen.getByRole("combobox", { name: "Tasks" });
    expect(selector).toBeDisabled();
    await userEvent.click(screen.getAllByRole("button", { name: "Use standard" })[0]);
    await waitFor(() => expect(fixtures.put).toHaveBeenCalledWith("/api/projects/project-1/workflows/Task", expect.objectContaining({ mode: "Standard" })));
  });

  it("surfaces the project workflow API error instead of replacing it with generic copy", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    fixtures.put.mockRejectedValueOnce(new Error("The selected workflow is no longer published."));
    renderSettings();

    const taskSelector = await screen.findByRole("combobox", { name: "Tasks" });
    fireEvent.change(taskSelector, { target: { value: "Standard" } });

    expect(await screen.findByRole("alert")).toHaveTextContent("The selected workflow is no longer published.");
  });

  it("keeps workflow configuration controls read-only without ProjectSettings.Edit", async () => {
    fixtures.permissions = ["ProjectSettings.View"];
    renderSettings();

    expect(await screen.findByRole("combobox", { name: "Tasks" })).toBeDisabled();
    expect(screen.getByText("To change Project workflow settings, you need Project Settings Edit permission and a mutable, non-read-only Project context.")).toBeInTheDocument();
    expect(fixtures.put).not.toHaveBeenCalled();
  });

  it("keeps workflow configuration controls read-only in ReadOnlyViewAs", async () => {
    fixtures.canMutate = false;
    fixtures.effectiveReadOnly = true;
    renderSettings();

    expect(await screen.findByRole("combobox", { name: "Tasks" })).toBeDisabled();
    expect(screen.getByText("Read-only view")).toBeInTheDocument();
    expect(screen.getByText("To change Project workflow settings, you need Project Settings Edit permission and a mutable, non-read-only Project context.")).toBeInTheDocument();
    expect(fixtures.put).not.toHaveBeenCalled();
  });

  it("opens all four configuration sections in one Settings Side Sheet for an authorized editor", async () => {
    const queryClient = createQueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <I18nProvider>
          <MemoryRouter initialEntries={["/projects/project-1/overview"]}><ProjectHeader project={headerProject} /></MemoryRouter>
        </I18nProvider>
      </QueryClientProvider>,
    );

    await userEvent.click(screen.getByRole("button", { name: "Actions" }));
    expect(screen.queryByRole("menuitem", { name: "Edit" })).not.toBeInTheDocument();
    await userEvent.keyboard("{Escape}");
    fireEvent.click(screen.getByRole("button", { name: "Project settings" }));

    const settingsSheet = await screen.findByRole("dialog", { name: "Project settings" });
    const nav = within(settingsSheet).getByRole("navigation", { name: "Project settings" });
    const generalSettings = within(settingsSheet).getByRole("region", { name: "General Settings" });
    expect(within(generalSettings).getByRole("button", { name: "Edit" })).toBeInTheDocument();
    for (const sectionName of ["Workflow & Approval Configuration", "Financial Approval Settings", "Annual Budget Approval Defaults"]) {
      await userEvent.click(within(nav).getByRole("button", { name: new RegExp(sectionName.replace("&", "&")) }));
      expect(within(within(settingsSheet).getByRole("region", { name: sectionName })).queryByRole("note")).not.toBeInTheDocument();
    }
    expect(await within(settingsSheet).findByRole("heading", { name: "Annual Budget Project Defaults", level: 3 })).toBeInTheDocument();
    expect(within(settingsSheet).getByRole("button", { name: "Configure Project defaults" })).toBeInTheDocument();
    expect(within(settingsSheet).getByRole("region", { name: "Annual Budget Approval Defaults" })).toBeInTheDocument();
  });

  it.each([
    { scenario: "Settings view-only actor", permissions: ["ProjectSettings.View"] },
    { scenario: "Settings editor without Project.Edit", permissions: ["ProjectSettings.View", "ProjectSettings.Edit"] },
  ])("does not expose General Settings editing to $scenario", async ({ permissions }) => {
    fixtures.permissions = permissions;
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider><MemoryRouter><ProjectHeader project={headerProject} /></MemoryRouter></I18nProvider>
      </QueryClientProvider>,
    );

    await userEvent.click(screen.getByRole("button", { name: "Project settings" }));
    const settingsSheet = await screen.findByRole("dialog", { name: "Project settings" });
    const generalSettings = within(settingsSheet).getByRole("region", { name: "General Settings" });
    expect(within(generalSettings).queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(within(generalSettings).getByRole("note")).toHaveTextContent("To edit Project details here, you need Project Settings View and Edit permissions, Project Edit permission, and a mutable, non-read-only Project context.");
  });

  it("allows an explicitly authorized editor to save approval defaults while retaining the protected financial permission", async () => {
    const user = userEvent.setup();
    fixtures.permissions = ["ProjectSettings.View", "ProjectSettings.Edit", "ProjectAnnualBudget.View", "ProjectFinancialFoundation.View", "ProjectAnnualBudget.ManageApprovalConfiguration"];
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider><MemoryRouter><ProjectHeader project={headerProject} /></MemoryRouter></I18nProvider>
      </QueryClientProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Project settings" }));
    const settingsSheet = await screen.findByRole("dialog", { name: "Project settings" });
    await user.click(within(settingsSheet).getByRole("button", { name: /Annual Budget Approval Defaults/ }));
    expect(within(settingsSheet).queryByText("Project approval defaults need attention before they can be used.")).not.toBeInTheDocument();
    expect(within(settingsSheet).queryByText("Set the Project's allowed Finance approval method in Project Financial Settings before configuring Annual Budget defaults.")).not.toBeInTheDocument();
    await user.click(await within(settingsSheet).findByRole("button", { name: "Configure Project defaults" }));
    await user.selectOptions(within(settingsSheet).getByRole("combobox", { name: "Method" }), "Parallel");
    const annualBudgetSection = within(settingsSheet).getByRole("region", { name: "Annual Budget Approval Defaults" });
    await user.click(within(annualBudgetSection).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(fixtures.put).toHaveBeenCalledWith(
      "/api/projects/public-project-1/annual-budgets/approval-configuration",
      { approvalMode: "Parallel", approverUserIds: ["approver-1"], rowVersion: "AQID", projectSettingsRowVersion: "BAUG" },
    ));
  });

  it("offers and saves the Both Financial Approval method", async () => {
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider><MemoryRouter><ProjectHeader project={headerProject} /></MemoryRouter></I18nProvider>
      </QueryClientProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Project settings" }));
    const settingsSheet = await screen.findByRole("dialog", { name: "Project settings" });
    await user.click(within(settingsSheet).getByRole("button", { name: /Financial Approval Settings/ }));
    const financialSection = within(settingsSheet).getByRole("region", { name: "Financial Approval Settings" });
    const method = await within(financialSection).findByRole("combobox", { name: "Finance approval configuration" });
    expect(within(method).getByRole("option", { name: "Sequential" })).toBeInTheDocument();
    expect(within(method).getByRole("option", { name: "Parallel" })).toBeInTheDocument();
    expect(within(method).getByRole("option", { name: "Both (choose per request)" })).toBeInTheDocument();
    await user.selectOptions(method, "Both");
    await user.click(within(financialSection).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(fixtures.put).toHaveBeenCalledWith(
      "/api/projects/public-project-1/financial-foundation/approval-configuration",
      { mode: "Both", rowVersion: "AQID" },
    ));
  });

  it("saves mandatory approvers in the selected sequential order", async () => {
    const user = userEvent.setup();
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider><MemoryRouter><ProjectHeader project={headerProject} /></MemoryRouter></I18nProvider>
      </QueryClientProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Project settings" }));
    const settingsSheet = await screen.findByRole("dialog", { name: "Project settings" });
    await user.click(within(settingsSheet).getByRole("button", { name: /Annual Budget Approval Defaults/ }));
    const annualBudgetSection = within(settingsSheet).getByRole("region", { name: "Annual Budget Approval Defaults" });
    await user.click(await within(annualBudgetSection).findByRole("button", { name: "Configure Project defaults" }));
    await user.click(within(annualBudgetSection).getByRole("checkbox", { name: "Approver Two" }));
    await user.click(within(annualBudgetSection).getByRole("button", { name: "Move Approver One later in approval order" }));
    await user.click(within(annualBudgetSection).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(fixtures.put).toHaveBeenCalledWith(
      "/api/projects/public-project-1/annual-budgets/approval-configuration",
      { approvalMode: "Sequential", approverUserIds: ["approver-2", "approver-1"], rowVersion: "AQID", projectSettingsRowVersion: "BAUG" },
    ));
  });

  it("explains when the API has no eligible Annual Budget approvers to select", async () => {
    annualBudgetApprovalConfiguration.eligibleApprovers = [];
    annualBudgetApprovalConfiguration.isValid = false;
    annualBudgetApprovalConfiguration.validationIssues = ["approver_required"];
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider><MemoryRouter><ProjectHeader project={headerProject} /></MemoryRouter></I18nProvider>
      </QueryClientProvider>,
    );

    await userEvent.click(screen.getByRole("button", { name: "Project settings" }));
    const settingsSheet = await screen.findByRole("dialog", { name: "Project settings" });
    await userEvent.click(within(settingsSheet).getByRole("button", { name: /Annual Budget Approval Defaults/ }));
    const annualBudgetSection = within(settingsSheet).getByRole("region", { name: "Annual Budget Approval Defaults" });
    await userEvent.click(await within(annualBudgetSection).findByRole("button", { name: "Configure Project defaults" }));

    expect(within(annualBudgetSection).getByText("No active Project members with Project Annual Budget approval permission are available.")).toBeInTheDocument();
    expect(within(annualBudgetSection).getByRole("button", { name: "Save" })).toBeDisabled();
  });

  it("keeps settings read-only when the financial configuration permission is absent", async () => {
    fixtures.permissions = ["ProjectSettings.View", "ProjectSettings.Edit", "ProjectAnnualBudget.View", "ProjectFinancialFoundation.View"];
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider><MemoryRouter><ProjectHeader project={headerProject} /></MemoryRouter></I18nProvider>
      </QueryClientProvider>,
    );

    await fireEvent.click(screen.getByRole("button", { name: "Project settings" }));
    const settingsSheet = await screen.findByRole("dialog", { name: "Project settings" });
    await userEvent.click(within(settingsSheet).getByRole("button", { name: /Annual Budget Approval Defaults/ }));
    expect(await within(settingsSheet).findByRole("heading", { name: "Annual Budget Project Defaults", level: 3 })).toBeInTheDocument();
    expect(within(settingsSheet).queryByRole("button", { name: "Configure Project defaults" })).not.toBeInTheDocument();
    expect(within(within(settingsSheet).getByRole("region", { name: "Annual Budget Approval Defaults" })).getByRole("note")).toHaveTextContent("To change Annual Budget defaults, you need Project Settings View and Edit, Project Annual Budget View, Project Annual Budget approval configuration permissions, and a mutable, non-read-only Project context.");
    await userEvent.click(within(settingsSheet).getByRole("button", { name: /Financial Approval Settings/ }));
    expect(within(settingsSheet).getByRole("combobox", { name: "Finance approval configuration" })).toBeDisabled();
    expect(within(within(settingsSheet).getByRole("region", { name: "Financial Approval Settings" })).getByRole("note")).toHaveTextContent("To change this Finance approval setting, you need Project Settings Edit and Project Annual Budget approval configuration permissions, plus a mutable, non-read-only Project context.");
    expect(fixtures.put).not.toHaveBeenCalled();
  });

  it("requires ProjectSettings.Edit even when financial configuration permission is present", async () => {
    fixtures.permissions = ["ProjectSettings.View", "ProjectAnnualBudget.View", "ProjectFinancialFoundation.View", "ProjectAnnualBudget.ManageApprovalConfiguration"];
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider><MemoryRouter><ProjectHeader project={headerProject} /></MemoryRouter></I18nProvider>
      </QueryClientProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Project settings" }));
    const settingsSheet = await screen.findByRole("dialog", { name: "Project settings" });
    await userEvent.click(within(settingsSheet).getByRole("button", { name: /Annual Budget Approval Defaults/ }));
    expect(await within(settingsSheet).findByRole("heading", { name: "Annual Budget Project Defaults", level: 3 })).toBeInTheDocument();
    expect(within(settingsSheet).queryByRole("button", { name: "Configure Project defaults" })).not.toBeInTheDocument();
    await userEvent.click(within(settingsSheet).getByRole("button", { name: /Financial Approval Settings/ }));
    expect(within(settingsSheet).getByRole("combobox", { name: "Finance approval configuration" })).toBeDisabled();
    for (const sectionName of ["General Settings", "Workflow & Approval Configuration", "Financial Approval Settings", "Annual Budget Approval Defaults"]) {
      await userEvent.click(within(settingsSheet).getByRole("button", { name: new RegExp(sectionName) }));
      expect(within(within(settingsSheet).getByRole("region", { name: sectionName })).getAllByRole("note").length).toBeGreaterThan(0);
    }
    expect(fixtures.put).not.toHaveBeenCalled();
  });

  it("explains why each visible section cannot be edited in a read-only Project context", async () => {
    fixtures.canMutate = false;
    fixtures.effectiveReadOnly = true;
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider><MemoryRouter><ProjectHeader project={headerProject} /></MemoryRouter></I18nProvider>
      </QueryClientProvider>,
    );

    await userEvent.click(screen.getByRole("button", { name: "Project settings" }));
    const settingsSheet = await screen.findByRole("dialog", { name: "Project settings" });
    const notesBySection = [
      ["General Settings", "To edit Project details here, you need Project Settings View and Edit permissions, Project Edit permission, and a mutable, non-read-only Project context."],
      ["Workflow & Approval Configuration", "To change Project workflow settings, you need Project Settings Edit permission and a mutable, non-read-only Project context."],
      ["Financial Approval Settings", "To change this Finance approval setting, you need Project Settings Edit and Project Annual Budget approval configuration permissions, plus a mutable, non-read-only Project context."],
      ["Annual Budget Approval Defaults", "To change Annual Budget defaults, you need Project Settings View and Edit, Project Annual Budget View, Project Annual Budget approval configuration permissions, and a mutable, non-read-only Project context."],
    ] as const;

    for (const [sectionName, explanation] of notesBySection) {
      await userEvent.click(within(settingsSheet).getByRole("button", { name: new RegExp(sectionName) }));
      expect(within(within(settingsSheet).getByRole("region", { name: sectionName })).getByRole("note")).toHaveTextContent(explanation);
    }
    expect(fixtures.put).not.toHaveBeenCalled();
  });

  it("opens the full source-parity project edit sheet and saves through the project API", async () => {
    const user = userEvent.setup();
    fixtures.permissions = ["Project.View", "Project.Edit", "ProjectSettings.View"];
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider>
          <MemoryRouter initialEntries={["/projects/project-1/overview"]}>
            <ProjectHeader project={headerProject} />
          </MemoryRouter>
        </I18nProvider>
      </QueryClientProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Actions" }));
    fireEvent.click(await screen.findByRole("menuitem", { name: "Edit" }));
    const sheet = await screen.findByRole("dialog", { name: "Edit Project" });
    expect(screen.getByText("Organization & Project Hierarchy")).toBeInTheDocument();
    expect(screen.getByText("Financial Foundation")).toBeInTheDocument();
    expect(within(sheet).getAllByText("Project Manager").length).toBeGreaterThan(0);
    expect(within(sheet).getAllByText("Department").length).toBeGreaterThan(0);

    const nameField = within(sheet).getByPlaceholderText("Name");
    await user.clear(nameField);
    await user.type(nameField, "Delivery Project Revised");
    await user.click(within(sheet).getByRole("button", { name: "Update Project" }));

    await waitFor(() => expect(fixtures.put).toHaveBeenCalledWith("/api/projects/public-project-1", expect.objectContaining({
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

  it("does not let Project.Edit or Edit-without-View open Project Settings", () => {
    fixtures.permissions = ["Project.Edit"];
    render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider><MemoryRouter><ProjectHeader project={headerProject} /></MemoryRouter></I18nProvider>
      </QueryClientProvider>,
    );

    expect(screen.queryByRole("button", { name: "Project settings" })).not.toBeInTheDocument();

    fixtures.permissions = ["ProjectSettings.Edit"];
    const secondRender = render(
      <QueryClientProvider client={createQueryClient()}>
        <I18nProvider><MemoryRouter><ProjectHeader project={headerProject} /></MemoryRouter></I18nProvider>
      </QueryClientProvider>,
    );
    expect(secondRender.queryByRole("button", { name: "Project settings" })).not.toBeInTheDocument();
  });
});
