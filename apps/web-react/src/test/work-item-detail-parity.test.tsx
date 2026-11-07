import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useAuth } from "../auth/AuthProvider";
import { useManagement } from "../api/management";
import { useWorkItemStatuses } from "../api/workItems";
import { useProjects } from "../features/projects/api/projects";
import { useFullWorkItem, useFullWorkItemActivity, useFullWorkItemAssignees, useFullWorkItemAttachments, useFullWorkItemCommentMutation, useFullWorkItemComments, useFullWorkItemDeleteAttachment, useFullWorkItemMutations, useFullWorkItemUploadMutation, useFullWorkItemWorkflow, type WorkItemDto } from "../api/workItemsFull";
import { I18nProvider } from "../i18n/messages";
import { WorkItemDetailPageFull } from "../features/delivery/WorkItemPages";
import { WorkItemDetailsSideSheetFull } from "../features/delivery/WorkItemDetailsSideSheetFull";

vi.mock("../auth/AuthProvider", () => ({ useAuth: vi.fn() }));
vi.mock("../api/management", () => ({ useManagement: vi.fn() }));
vi.mock("../api/workItems", () => ({ useWorkItemStatuses: vi.fn() }));
vi.mock("../features/projects/api/projects", () => ({ useProjects: vi.fn() }));
vi.mock("../api/workItemsFull", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/workItemsFull")>();
  return { ...actual, useFullWorkItem: vi.fn(), useFullWorkItemActivity: vi.fn(), useFullWorkItemAssignees: vi.fn(), useFullWorkItemAttachments: vi.fn(), useFullWorkItemCommentMutation: vi.fn(), useFullWorkItemComments: vi.fn(), useFullWorkItemDeleteAttachment: vi.fn(), useFullWorkItemMutations: vi.fn(), useFullWorkItemUploadMutation: vi.fn(), useFullWorkItemWorkflow: vi.fn(), workItemsApiFull: { downloadAttachment: vi.fn() } };
});

const useAuthMock = vi.mocked(useAuth);
const useManagementMock = vi.mocked(useManagement);
const useStatusesMock = vi.mocked(useWorkItemStatuses);
const useProjectsMock = vi.mocked(useProjects);
const useItemMock = vi.mocked(useFullWorkItem);
const useActivityMock = vi.mocked(useFullWorkItemActivity);
const useAssigneesMock = vi.mocked(useFullWorkItemAssignees);
const useAttachmentsMock = vi.mocked(useFullWorkItemAttachments);
const useCommentMutationMock = vi.mocked(useFullWorkItemCommentMutation);
const useCommentsMock = vi.mocked(useFullWorkItemComments);
const useDeleteAttachmentMock = vi.mocked(useFullWorkItemDeleteAttachment);
const useMutationsMock = vi.mocked(useFullWorkItemMutations);
const useUploadMock = vi.mocked(useFullWorkItemUploadMutation);
const useWorkflowMock = vi.mocked(useFullWorkItemWorkflow);
const saveWorkItem = vi.fn();
const addComment = vi.fn();
const transition = vi.fn();

function makeWorkItem(overrides: Partial<WorkItemDto> = {}): WorkItemDto {
  return { id: "task-42", title: "Prepare quarterly forecast", description: "<p>Confirm assumptions and review the model.</p>", type: "Task", priority: "High", status: "Open", source: "Manual", projectId: "project-1", projectName: "Core Platform", assignedResourceId: "resource-1", assignedResourceName: "Alex Morgan", storyPoints: 5, estimatedHours: 8, actualHours: 2, dueDate: "2099-04-01", createdAt: "2026-01-01", createdBy: "user-1", rowVersion: [4], ...overrides };
}

function renderDetail() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><MemoryRouter initialEntries={["/work-items/task-42"]}><I18nProvider><Routes><Route path="/work-items/:id" element={<WorkItemDetailPageFull />} /><Route path="/work-items" element={<div>Work Items list</div>} /></Routes></I18nProvider></MemoryRouter></QueryClientProvider>);
}

describe("Work Item detail feature and design parity", () => {
  beforeEach(() => {
    window.localStorage.clear();
    saveWorkItem.mockReset().mockResolvedValue(makeWorkItem({ title: "Forecast reviewed" }));
    addComment.mockReset().mockResolvedValue({ id: "comment-2" });
    transition.mockReset().mockResolvedValue(undefined);
    useAuthMock.mockReturnValue({ user: { id: "user-1", permissions: ["WorkItem.View", "WorkItem.Edit"] } } as unknown as ReturnType<typeof useAuth>);
    useManagementMock.mockReturnValue({ canMutate: true, effectiveReadOnly: false } as unknown as ReturnType<typeof useManagement>);
    useStatusesMock.mockReturnValue({ data: [{ id: "status-open", code: "Open", nameEn: "Open", nameAr: "مفتوح", displayOrder: 1, isActive: true, isTerminal: false, color: "blue" }, { id: "status-in-progress", code: "InProgress", nameEn: "In progress", nameAr: "In progress", displayOrder: 2, isActive: true, isTerminal: false, color: "blue" }] } as unknown as ReturnType<typeof useWorkItemStatuses>);
    useProjectsMock.mockReturnValue({ data: { items: [{ id: "project-1", name: "Core Platform", code: "PRJ-1" }] } } as unknown as ReturnType<typeof useProjects>);
    useItemMock.mockReturnValue({ data: makeWorkItem(), isLoading: false, isError: false, refetch: vi.fn() } as unknown as ReturnType<typeof useFullWorkItem>);
    useActivityMock.mockReturnValue({ data: { items: [{ id: "activity-1", action: "Updated", changes: JSON.stringify({ priority: { from: "Medium", to: "High" } }), userId: "user-2", userName: "Jordan", timestamp: "2026-01-03" }], totalPages: 1, totalCount: 1 }, isLoading: false, isError: false } as unknown as ReturnType<typeof useFullWorkItemActivity>);
    useAssigneesMock.mockReturnValue({ data: [{ targetType: "Resource", targetId: "resource-2", name: "Jordan Lee" }], isLoading: false } as unknown as ReturnType<typeof useFullWorkItemAssignees>);
    useAttachmentsMock.mockReturnValue({ data: [], isLoading: false, isError: false } as unknown as ReturnType<typeof useFullWorkItemAttachments>);
    useCommentMutationMock.mockReturnValue({ mutateAsync: addComment, isPending: false } as unknown as ReturnType<typeof useFullWorkItemCommentMutation>);
    useCommentsMock.mockReturnValue({ data: { items: [{ id: "comment-1", workItemId: "task-42", commentText: "Please validate the baseline.", createdBy: "user-2", authorName: "Jordan", createdAt: "2026-01-03" }], totalPages: 1, totalCount: 1 }, isLoading: false, isError: false, refetch: vi.fn() } as unknown as ReturnType<typeof useFullWorkItemComments>);
    useDeleteAttachmentMock.mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as unknown as ReturnType<typeof useFullWorkItemDeleteAttachment>);
    useUploadMock.mockReturnValue({ mutateAsync: vi.fn(), isPending: false } as unknown as ReturnType<typeof useFullWorkItemUploadMutation>);
    useMutationsMock.mockReturnValue({ create: { mutateAsync: vi.fn(), isPending: false }, update: { mutateAsync: saveWorkItem, isPending: false } } as unknown as ReturnType<typeof useFullWorkItemMutations>);
    useWorkflowMock.mockReturnValue({ isLoading: false, availableTransitions: [{ code: "StartWork", nameEn: "Start work", nameAr: "بدء العمل", targetStateCode: "InProgress", targetStateNameEn: "In progress", targetStateNameAr: "قيد التنفيذ", displayOrder: 1, canExecute: true }], execute: { mutateAsync: transition, isPending: false } } as unknown as ReturnType<typeof useFullWorkItemWorkflow>);
  });

  it("renders the task workspace details with back navigation, comments, and activity tabs", async () => {
    renderDetail();

    expect(screen.getByRole("link", { name: "Back to work items" })).toHaveAttribute("href", "/work-items");
    expect(screen.getByRole("heading", { name: "Prepare quarterly forecast" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Comments" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tab", { name: "Activity history" })).toBeInTheDocument();
    expect(await screen.findByText("Please validate the baseline.")).toBeInTheDocument();
  });

  it("saves edits and executes authorized workflow transitions", async () => {
    renderDetail();
    fireEvent.click(screen.getByRole("button", { name: "Edit" }));
    fireEvent.change(screen.getByLabelText("Title *"), { target: { value: "Forecast reviewed" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    await waitFor(() => expect(saveWorkItem).toHaveBeenCalledWith(expect.objectContaining({ id: "task-42", body: expect.objectContaining({ title: "Forecast reviewed", rowVersion: [4] }) })));

    fireEvent.click(screen.getByRole("button", { name: "In progress" }));
    await waitFor(() => expect(transition).toHaveBeenCalledWith({ transitionCode: "StartWork" }));
  });

  it("posts replies to the selected parent comment and uses localized activity labels", async () => {
    renderDetail();
    fireEvent.click(screen.getAllByRole("button", { name: "Reply" }).at(-1)!);
    fireEvent.input(screen.getByRole("textbox", { name: "Write a reply..." }), { target: { innerHTML: "Reviewed." } });
    fireEvent.click(screen.getAllByRole("button", { name: "Reply" }).at(-1)!);
    await waitFor(() => expect(addComment).toHaveBeenCalledWith({ commentText: "Reviewed.", parentCommentId: "comment-1" }));

    fireEvent.click(screen.getByRole("tab", { name: "Activity history" }));
    expect(await screen.findByText("Updated")).toBeInTheDocument();
    expect(screen.getByText("Priority: Medium → High")).toBeInTheDocument();
  });

  it("keeps comment and reply entry points available to view-only readers, matching Next while the API enforces comment access", () => {
    render(<MemoryRouter><I18nProvider><WorkItemDetailsSideSheetFull id="task-42" open onOpenChange={vi.fn()} canEdit={false} statuses={[]} /></I18nProvider></MemoryRouter>);
    fireEvent.click(screen.getByRole("tab", { name: "Comments" }));

    expect(screen.getByRole("textbox", { name: "Write a comment..." })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Reply" }));
    expect(screen.getByRole("textbox", { name: "Write a reply..." })).toBeInTheDocument();
  });

  it("keeps editing and workflow actions disabled in a read-only management context", async () => {
    useManagementMock.mockReturnValue({ canMutate: false, effectiveReadOnly: true } as unknown as ReturnType<typeof useManagement>);
    renderDetail();
    expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "In progress" })).toBeDisabled();
    expect(await screen.findByText("Please validate the baseline.")).toBeInTheDocument();
  });
});
