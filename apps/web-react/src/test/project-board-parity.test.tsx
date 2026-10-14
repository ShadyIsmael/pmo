import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { ProjectBoardPage } from "../features/projects/ProjectWorkspaceViews";

const mocks = vi.hoisted(() => ({
  permissions: ["WorkItem.Create", "WorkItem.Edit"],
  readOnly: false,
  create: vi.fn(),
  workflowTransitions: vi.fn(),
  executeWorkflowTransition: vi.fn(),
  workItemQuery: vi.fn(),
  projects: [
    { id: "project-active", name: "Active project", code: "PRJ-100", status: "Active", health: "Green", ragStatus: "Green", progress: 25, currency: "USD" },
    { id: "project-completed", name: "Completed project", code: "PRJ-101", status: "Completed", health: "Green", ragStatus: "Green", progress: 100, currency: "USD" },
  ],
  statuses: [
    { id: "status-open", code: "Open", nameEn: "Open", nameAr: "مفتوح", displayOrder: 1, isActive: true, isTerminal: false },
    { id: "status-done", code: "Done", nameEn: "Done", nameAr: "منتهٍ", displayOrder: 2, isActive: true, isTerminal: true },
  ],
  items: [{ id: "item-1", title: "Prepare release", type: "Task", priority: "High", status: "Open", source: "Manual", projectId: "project-active", projectName: "Active project", dueDate: "2026-10-20", estimatedHours: 4, createdAt: "2026-10-01", createdBy: "user-1" }],
}));

vi.mock("@features/projects/api/projects", () => ({
  useProjects: () => ({ data: { items: mocks.projects }, isLoading: false, isError: false }),
}));

vi.mock("@api/workItemsFull", () => ({
  useFullWorkItems: (params: unknown, enabled: boolean) => {
    mocks.workItemQuery(params, enabled);
    return { data: { items: mocks.items, totalCount: mocks.items.length }, isLoading: false, isError: false, error: undefined };
  },
  useFullWorkItemAssignees: () => ({ data: [], isLoading: false }),
  useFullWorkItemMutations: () => ({ create: { mutateAsync: mocks.create, isPending: false } }),
  workItemsApiFull: {
    workflowTransitions: mocks.workflowTransitions,
    executeWorkflowTransition: mocks.executeWorkflowTransition,
  },
  workItemKeys: { all: ["work-items"] },
}));

vi.mock("@api/workItems", () => ({
  useWorkItemStatuses: () => ({ data: mocks.statuses, isLoading: false, isError: false, error: undefined }),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1", permissions: mocks.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: !mocks.readOnly, effectiveReadOnly: mocks.readOnly }) }));

function renderBoard(locale: "en" | "ar" = "en", initialEntry = "/projects/board") {
  window.localStorage.setItem("nexus-locale", locale);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <MemoryRouter initialEntries={[initialEntry]}>
          <Routes><Route path="/projects/board" element={<ProjectBoardPage />} /><Route path="/projects/:id/board" element={<ProjectBoardPage />} /></Routes>
        </MemoryRouter>
      </I18nProvider>
    </QueryClientProvider>,
  );
}

describe("project board parity", () => {
  beforeEach(() => {
    mocks.permissions = ["WorkItem.Create", "WorkItem.Edit"];
    mocks.readOnly = false;
    mocks.create.mockReset().mockResolvedValue({ id: "item-new" });
    mocks.workflowTransitions.mockReset().mockResolvedValue({
      currentStateCode: "Open",
      currentRowVersion: [1],
      transitions: [{ code: "COMPLETE", nameEn: "Complete work", nameAr: "إنهاء العمل", targetStateCode: "Done", targetStateNameEn: "Done", targetStateNameAr: "منتهٍ", displayOrder: 1, canExecute: true }],
    });
    mocks.executeWorkflowTransition.mockReset().mockResolvedValue(undefined);
    mocks.workItemQuery.mockReset();
  });

  it("uses API-defined workflow columns and excludes completed projects in the global selector", () => {
    renderBoard();
    expect(screen.getByRole("option", { name: /Active project/ })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: /Completed project/ })).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Done" })).toBeInTheDocument();
    expect(screen.getByText("Prepare release")).toBeInTheDocument();
    expect(mocks.workItemQuery).toHaveBeenCalledWith(expect.objectContaining({ projectId: "project-active" }), true);
  });

  it("moves cards through the authorized workflow transition API", async () => {
    renderBoard();
    const card = screen.getByText("Prepare release").closest("article");
    const targetColumn = screen.getByRole("heading", { name: "Done" }).closest("section");
    const dataTransfer = { effectAllowed: "", dropEffect: "", setData: vi.fn(), getData: vi.fn(() => "item-1") };

    fireEvent.dragStart(card!, { dataTransfer });
    fireEvent.drop(targetColumn!, { dataTransfer });

    await waitFor(() => expect(mocks.workflowTransitions).toHaveBeenCalledWith("item-1"));
    expect(mocks.executeWorkflowTransition).toHaveBeenCalledWith("item-1", "COMPLETE", { expectedStateCode: "Open", expectedRowVersion: [1] });
  });

  it("surfaces the server denial reason when the transition cannot execute", async () => {
    mocks.workflowTransitions.mockResolvedValue({
      currentStateCode: "Open",
      currentRowVersion: [1],
      transitions: [{ code: "COMPLETE", nameEn: "Complete work", nameAr: "إنهاء العمل", targetStateCode: "Done", targetStateNameEn: "Done", targetStateNameAr: "منتهٍ", displayOrder: 1, canExecute: false, denialReason: "Only the project manager can complete this task." }],
    });
    renderBoard();
    const card = screen.getByText("Prepare release").closest("article");
    const targetColumn = screen.getByRole("heading", { name: "Done" }).closest("section");
    const dataTransfer = { effectAllowed: "", dropEffect: "", setData: vi.fn(), getData: vi.fn(() => "item-1") };

    fireEvent.dragStart(card!, { dataTransfer });
    fireEvent.drop(targetColumn!, { dataTransfer });

    expect(await screen.findByRole("alert")).toHaveTextContent("Only the project manager can complete this task.");
    expect(mocks.executeWorkflowTransition).not.toHaveBeenCalled();
  });

  it("opens the transition chooser when several transitions lead to the target state", async () => {
    mocks.workflowTransitions.mockResolvedValue({
      currentStateCode: "Open",
      currentRowVersion: [1],
      transitions: [
        { code: "COMPLETE", nameEn: "Complete work", nameAr: "إنهاء العمل", targetStateCode: "Done", targetStateNameEn: "Done", targetStateNameAr: "منتهٍ", displayOrder: 1, canExecute: true },
        { code: "FAST_COMPLETE", nameEn: "Fast complete", nameAr: "إنهاء سريع", targetStateCode: "Done", targetStateNameEn: "Done", targetStateNameAr: "منتهٍ", displayOrder: 2, canExecute: true },
      ],
    });
    renderBoard();
    const card = screen.getByText("Prepare release").closest("article");
    const targetColumn = screen.getByRole("heading", { name: "Done" }).closest("section");
    const dataTransfer = { effectAllowed: "", dropEffect: "", setData: vi.fn(), getData: vi.fn(() => "item-1") };

    fireEvent.dragStart(card!, { dataTransfer });
    fireEvent.drop(targetColumn!, { dataTransfer });

    const sheet = await screen.findByRole("dialog", { name: "Choose a workflow transition" });
    fireEvent.click(within(sheet).getByRole("button", { name: /Complete work/ }));

    await waitFor(() => expect(mocks.executeWorkflowTransition).toHaveBeenCalledWith("item-1", "COMPLETE", { expectedStateCode: "Open", expectedRowVersion: [1] }));
  });

  it("creates a Task in a project-locked Side Sheet and preserves project context", async () => {
    renderBoard();
    fireEvent.click(screen.getByRole("button", { name: "Add Task" }));
    const sheet = screen.getByRole("dialog", { name: "Add Task" });
    const title = within(sheet).getByLabelText("Title *");
    fireEvent.change(title, { target: { value: "Prepare the release" } });
    expect(within(sheet).getByLabelText("Project")).toBeDisabled();
    expect(within(sheet).queryByLabelText("Type")).not.toBeInTheDocument();
    fireEvent.click(within(sheet).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(mocks.create).toHaveBeenCalledWith(expect.objectContaining({ title: "Prepare the release", type: "Task", projectId: "project-active" })));
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Add Task" })).not.toBeInTheDocument());
  });

  it("hides creation and prevents drag changes in read-only management context", () => {
    mocks.readOnly = true;
    renderBoard();

    expect(screen.queryByRole("button", { name: "Add Task" })).not.toBeInTheDocument();
    expect(screen.getByText("Prepare release").closest("article")).toHaveAttribute("draggable", "false");
  });

  it("localizes the task entry point in Arabic", () => {
    renderBoard("ar");
    expect(screen.getByRole("button", { name: "إضافة مهمة" })).toBeInTheDocument();
  });
});
