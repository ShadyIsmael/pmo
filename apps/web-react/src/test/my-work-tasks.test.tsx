import type { ReactNode } from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useParams } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { useAuth } from "../auth/AuthProvider";
import { useManagement } from "../api/management";
import { useProjects } from "../features/projects/api/projects";
import { useWorkItemStatuses } from "../api/workItems";
import { useFullWorkItemAssignees, useFullWorkItemMutations, useFullWorkItems, workItemsApiFull, type WorkItemDto } from "../api/workItemsFull";
import { MyWorkTasksPage } from "../features/my-work/MyWorkTasksPage";

vi.mock("../auth/AuthProvider", () => ({ useAuth: vi.fn() }));
vi.mock("../api/management", () => ({ useManagement: vi.fn() }));
vi.mock("../features/projects/api/projects", () => ({ useProjects: vi.fn() }));
vi.mock("../api/workItems", () => ({ useWorkItemStatuses: vi.fn() }));
vi.mock("../api/workItemsFull", () => ({ useFullWorkItemAssignees: vi.fn(), useFullWorkItemMutations: vi.fn(), useFullWorkItems: vi.fn(), workItemsApiFull: { uploadAttachments: vi.fn() } }));
vi.mock("../features/delivery/WorkItemForm", () => ({
  WorkItemForm: ({ draft, onChange, extraContent }: { draft: { title: string }; onChange: (patch: { title: string }) => void; extraContent?: ReactNode }) => (
    <><label>Task title<input aria-label="Task title" value={draft.title} onChange={(event) => onChange({ title: event.target.value })} /></label>{extraContent}</>
  ),
}));

const useAuthMock = vi.mocked(useAuth);
const useManagementMock = vi.mocked(useManagement);
const useProjectsMock = vi.mocked(useProjects);
const useStatusesMock = vi.mocked(useWorkItemStatuses);
const useAssigneesMock = vi.mocked(useFullWorkItemAssignees);
const useMutationsMock = vi.mocked(useFullWorkItemMutations);
const useItemsMock = vi.mocked(useFullWorkItems);
const createTask = vi.fn();
const uploadAttachments = vi.mocked(workItemsApiFull.uploadAttachments);

function TaskDetailRoute() {
  const { id } = useParams();
  return <div>Opened work item: {id}</div>;
}

function workItem(overrides: Partial<WorkItemDto> = {}): WorkItemDto {
  return {
    id: "task-1", title: "Prepare forecast", type: "Task", priority: "High", status: "InProgress", source: "Manual",
    projectId: "project-1", projectName: "Core Platform", dueDate: "2099-04-01", createdAt: "2026-01-01", createdBy: "user-1", ...overrides,
  };
}

function renderTasks() {
  return render(<MemoryRouter initialEntries={["/my-work/tasks"]}><I18nProvider><Routes>
    <Route path="/my-work/tasks" element={<MyWorkTasksPage />} />
    <Route path="/work-items/:id" element={<TaskDetailRoute />} />
  </Routes></I18nProvider></MemoryRouter>);
}

describe("My Work task parity", () => {
  beforeEach(() => {
    window.localStorage.clear();
    createTask.mockReset().mockResolvedValue(workItem({ id: "task-created" }));
    uploadAttachments.mockReset().mockResolvedValue([]);
    useAuthMock.mockReturnValue({ user: { id: "user-1", permissions: ["WorkItem.Create"] } } as unknown as ReturnType<typeof useAuth>);
    useManagementMock.mockReturnValue({ canMutate: true, effectiveReadOnly: false } as unknown as ReturnType<typeof useManagement>);
    useProjectsMock.mockReturnValue({ data: { items: [] }, isLoading: false } as unknown as ReturnType<typeof useProjects>);
    useStatusesMock.mockReturnValue({ data: [
      { id: "status-progress", code: "InProgress", nameEn: "In Progress", nameAr: "قيد التنفيذ", displayOrder: 1, isActive: true, isTerminal: false },
      { id: "status-done", code: "Done", nameEn: "Done", nameAr: "مكتملة", displayOrder: 2, isActive: true, isTerminal: true },
      { id: "status-cancelled", code: "Cancelled", nameEn: "Cancelled", nameAr: "ملغاة", displayOrder: 3, isActive: true, isTerminal: true },
    ] } as unknown as ReturnType<typeof useWorkItemStatuses>);
    useAssigneesMock.mockReturnValue({ data: [] } as unknown as ReturnType<typeof useFullWorkItemAssignees>);
    useMutationsMock.mockReturnValue({ create: { mutateAsync: createTask, isPending: false } } as unknown as ReturnType<typeof useFullWorkItemMutations>);
    useItemsMock.mockReturnValue({ data: { items: [workItem()], totalCount: 1 }, isLoading: false, isError: false, refetch: vi.fn() } as unknown as ReturnType<typeof useFullWorkItems>);
  });

  it("provides the source create-task action and navigates to the created item", async () => {
    renderTasks();
    expect(screen.getByText(/Your task queue, priorities, and deadlines in one place/)).toBeInTheDocument();
    expect(screen.getByText("High")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "New Task" }));
    fireEvent.change(screen.getByRole("textbox", { name: "Task title" }), { target: { value: "Create migration plan" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(createTask).toHaveBeenCalledWith(expect.objectContaining({ title: "Create migration plan", type: "Task", priority: "Medium" })));
    expect(await screen.findByText("Opened work item: task-created")).toBeInTheDocument();
  });

  it("uses Arabic priority labels and hides creation while the management context is read-only", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    useAuthMock.mockReturnValue({ user: { id: "user-1", permissions: ["WorkItem.Create"] } } as unknown as ReturnType<typeof useAuth>);
    useManagementMock.mockReturnValue({ canMutate: false, effectiveReadOnly: true } as unknown as ReturnType<typeof useManagement>);

    renderTasks();

    expect(screen.getByText("عالية")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "مهمة جديدة" })).not.toBeInTheDocument();
  });

  it("does not invite duplicate task creation when attachment upload fails after creation", async () => {
    uploadAttachments.mockRejectedValue(new Error("upload failed"));
    renderTasks();
    fireEvent.click(screen.getByRole("button", { name: "New Task" }));
    fireEvent.change(screen.getByRole("textbox", { name: "Task title" }), { target: { value: "Task with file" } });
    const fileInput = document.body.querySelector<HTMLInputElement>('input[type="file"]');
    expect(fileInput).not.toBeNull();
    fireEvent.change(fileInput!, { target: { files: [new File(["content"], "spec.pdf", { type: "application/pdf" })] } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Attachments could not be uploaded.")).toBeInTheDocument();
    expect(createTask).toHaveBeenCalledOnce();
    expect(screen.getByRole("button", { name: "Save" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "View details" }));
    expect(await screen.findByText("Opened work item: task-created")).toBeInTheDocument();
  });
});
