import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useAuth } from "../auth/AuthProvider";
import { useManagement } from "../api/management";
import { useWorkItemStatuses } from "../api/workItems";
import { useFullWorkItem, useFullWorkItemActivity, useFullWorkItemAssignees, useFullWorkItemCommentMutation, useFullWorkItemComments, useFullWorkItemInsights, useFullWorkItemMutations, useFullWorkItemWorkflow, useFullWorkItems, type WorkItemDto } from "../api/workItemsFull";
import { useProjects } from "../features/projects/api/projects";
import { useUserLookup } from "../features/projects/lib/project-lookups";
import { I18nProvider } from "../i18n/messages";
import { WorkItemsPageFull } from "../features/delivery/WorkItemPages";

vi.mock("../auth/AuthProvider", () => ({ useAuth: vi.fn() }));
vi.mock("../api/management", () => ({ useManagement: vi.fn() }));
vi.mock("../api/workItems", () => ({ useWorkItemStatuses: vi.fn() }));
vi.mock("../features/projects/api/projects", () => ({ useProjects: vi.fn() }));
vi.mock("../features/projects/lib/project-lookups", () => ({ useUserLookup: vi.fn() }));
vi.mock("../api/workItemsFull", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/workItemsFull")>();
  return { ...actual, useFullWorkItems: vi.fn(), useFullWorkItemInsights: vi.fn(), useFullWorkItem: vi.fn(), useFullWorkItemActivity: vi.fn(), useFullWorkItemAssignees: vi.fn(), useFullWorkItemCommentMutation: vi.fn(), useFullWorkItemComments: vi.fn(), useFullWorkItemMutations: vi.fn(), useFullWorkItemWorkflow: vi.fn() };
});

const useAuthMock = vi.mocked(useAuth);
const useManagementMock = vi.mocked(useManagement);
const useStatusesMock = vi.mocked(useWorkItemStatuses);
const useProjectsMock = vi.mocked(useProjects);
const useUsersMock = vi.mocked(useUserLookup);
const useItemsMock = vi.mocked(useFullWorkItems);
const useInsightsMock = vi.mocked(useFullWorkItemInsights);
const useDetailMock = vi.mocked(useFullWorkItem);
const useActivityMock = vi.mocked(useFullWorkItemActivity);
const useAssigneesMock = vi.mocked(useFullWorkItemAssignees);
const useCommentMutationMock = vi.mocked(useFullWorkItemCommentMutation);
const useCommentsMock = vi.mocked(useFullWorkItemComments);
const useMutationsMock = vi.mocked(useFullWorkItemMutations);
const useWorkflowMock = vi.mocked(useFullWorkItemWorkflow);
const updateWorkItem = vi.fn();
const createWorkItem = vi.fn();
const addComment = vi.fn();
const executeTransition = vi.fn();

function makeWorkItem(overrides: Partial<WorkItemDto> = {}): WorkItemDto {
  return { id: "task-1", title: "Prepare release", description: "Confirm the release checklist.", type: "Task", priority: "High", status: "Open", source: "Manual", projectId: "project-1", projectName: "Core Platform", assignedResourceId: "resource-1", assignedResourceName: "Avery Chen", storyPoints: 5, estimatedHours: 3, dueDate: "2099-04-01", createdAt: "2026-01-01", createdBy: "user-1", rowVersion: [3], ...overrides };
}

function renderWorkItems(path = "/work-items") {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}><MemoryRouter initialEntries={[path]}><I18nProvider><WorkItemsPageFull /></I18nProvider></MemoryRouter></QueryClientProvider>);
}

describe("Work Items feature and design parity", () => {
  beforeEach(() => {
    window.localStorage.clear();
    updateWorkItem.mockReset().mockResolvedValue(makeWorkItem());
    createWorkItem.mockReset().mockResolvedValue(makeWorkItem({ id: "task-created" }));
    addComment.mockReset().mockResolvedValue({ id: "comment-2" });
    executeTransition.mockReset().mockResolvedValue({ success: true });
    useAuthMock.mockReturnValue({ user: { id: "user-1", permissions: ["WorkItem.View", "WorkItem.Create", "WorkItem.Edit"] } } as unknown as ReturnType<typeof useAuth>);
    useManagementMock.mockReturnValue({ canMutate: true, effectiveReadOnly: false } as unknown as ReturnType<typeof useManagement>);
    useStatusesMock.mockReturnValue({ data: [{ id: "status-open", code: "Open", nameEn: "Open", nameAr: "مفتوح", displayOrder: 1, isActive: true, isTerminal: false }] } as unknown as ReturnType<typeof useWorkItemStatuses>);
    useProjectsMock.mockReturnValue({ data: { items: [{ id: "project-1", publicId: "public-project-1", name: "Core Platform", code: "PRJ-1" }] } } as unknown as ReturnType<typeof useProjects>);
    useUsersMock.mockReturnValue({ data: [] } as unknown as ReturnType<typeof useUserLookup>);
    useItemsMock.mockReturnValue({ data: { items: [makeWorkItem()], totalCount: 1, totalPages: 1 }, isLoading: false, isError: false } as unknown as ReturnType<typeof useFullWorkItems>);
    useInsightsMock.mockReturnValue({ data: { total: 8, open: 5, overdue: 2, unassigned: 1, dueThisWeek: 3, byStatus: [{ key: "Open", count: 5 }], byPriority: [{ key: "High", count: 3 }] }, isLoading: false, isError: false, refetch: vi.fn() } as unknown as ReturnType<typeof useFullWorkItemInsights>);
    useDetailMock.mockReturnValue({ data: makeWorkItem(), isLoading: false, isError: false } as unknown as ReturnType<typeof useFullWorkItem>);
    useActivityMock.mockReturnValue({ data: { items: [] }, isLoading: false, isError: false } as unknown as ReturnType<typeof useFullWorkItemActivity>);
    useAssigneesMock.mockReturnValue({ data: [{ targetType: "Resource", targetId: "resource-2", resourceId: "resource-2", name: "Jordan Lee" }] } as unknown as ReturnType<typeof useFullWorkItemAssignees>);
    useCommentMutationMock.mockReturnValue({ mutateAsync: addComment, isPending: false } as unknown as ReturnType<typeof useFullWorkItemCommentMutation>);
    useCommentsMock.mockReturnValue({ data: { items: [{ id: "comment-1", workItemId: "task-1", commentText: "Please confirm the test run.", createdBy: "user-2", authorName: "Morgan", createdAt: "2026-01-03" }] }, isLoading: false, isError: false } as unknown as ReturnType<typeof useFullWorkItemComments>);
    useMutationsMock.mockReturnValue({ create: { mutate: vi.fn(), mutateAsync: createWorkItem, isPending: false }, update: { mutateAsync: updateWorkItem, isPending: false } } as unknown as ReturnType<typeof useFullWorkItemMutations>);
    useWorkflowMock.mockReturnValue({ isLoading: false, availableTransitions: [{ code: "StartWork", nameEn: "Start work", nameAr: "بدء العمل", targetStateCode: "InProgress", targetStateNameEn: "In progress", targetStateNameAr: "قيد التنفيذ", displayOrder: 1, canExecute: true }], execute: { mutateAsync: executeTransition, isPending: false } } as unknown as ReturnType<typeof useFullWorkItemWorkflow>);
  });

  it("renders API-backed backlog analytics and project scope with the source tab layout", () => {
    renderWorkItems();

    expect(screen.getByRole("tab", { name: "Backlog" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tab", { name: "Board" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "My Work" })).toBeInTheDocument();
    expect(screen.getByLabelText("Project scope")).toHaveValue("");
    expect(screen.getByRole("option", { name: "PRJ-1 · Core Platform" })).toBeInTheDocument();
    expect(screen.getByText("5", { selector: "p" })).toBeInTheDocument();
    expect(screen.getByText("Work items by status")).toBeInTheDocument();
    expect(useInsightsMock).toHaveBeenCalledWith(expect.objectContaining({ projectId: undefined }), true);
  });

  it("uses server filters for selected project, quick views, and the My Work tab", async () => {
    renderWorkItems();
    fireEvent.change(screen.getByLabelText("Project scope"), { target: { value: "public-project-1" } });
    await waitFor(() => expect(useItemsMock).toHaveBeenLastCalledWith(expect.objectContaining({ projectId: "public-project-1" })));

    fireEvent.click(screen.getByRole("button", { name: "Overdue" }));
    await waitFor(() => expect(useItemsMock).toHaveBeenLastCalledWith(expect.objectContaining({ projectId: "public-project-1", overdue: true })));

    fireEvent.click(screen.getByRole("tab", { name: "My Work" }));
    await waitFor(() => expect(useItemsMock).toHaveBeenLastCalledWith(expect.objectContaining({ myWork: true, projectId: "public-project-1" })));
    expect(useInsightsMock).toHaveBeenLastCalledWith(expect.any(Object), false);
  });

  it("keeps search and filters in one row with advanced filters in the More filters sheet", async () => {
    renderWorkItems();
    expect(screen.getByRole("textbox", { name: "Search" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /More filters/ })).toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: "Type" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /More filters/ }));
    const filtersSheet = await screen.findByRole("dialog", { name: "Advanced filters" });
    const [typeSelect] = within(filtersSheet).getAllByRole("combobox");
    fireEvent.change(typeSelect, { target: { value: "Issue" } });
    fireEvent.click(within(filtersSheet).getByRole("button", { name: "Apply filters" }));
    await waitFor(() => expect(useItemsMock).toHaveBeenLastCalledWith(expect.objectContaining({ type: "Issue" })));

    fireEvent.click(screen.getByRole("button", { name: "Create work item" }));
    const createDialog = await screen.findByRole("dialog", { name: "Create work item" });
    expect(within(createDialog).getByText("Capture the essentials, project context, ownership, and target date.")).toBeInTheDocument();
    expect(within(createDialog).getByRole("region", { name: "Work item details" })).toBeInTheDocument();
    expect(within(createDialog).getByRole("region", { name: "Planning & ownership" })).toBeInTheDocument();
    expect(within(createDialog).getByRole("region", { name: "Description" })).toBeInTheDocument();
    expect(within(createDialog).getByRole("textbox", { name: "Description" })).toHaveClass("flex-1", "max-h-none");
    expect(within(createDialog).getByRole("button", { name: "Save" })).toBeInTheDocument();
    expect(within(createDialog).getByLabelText("Title *")).toBeInTheDocument();
    expect(within(createDialog).getByRole("combobox", { name: "Assignee" })).toBeDisabled();
    fireEvent.change(within(createDialog).getByLabelText("Project"), { target: { value: "project-1" } });
    fireEvent.change(within(createDialog).getByLabelText("Title *"), { target: { value: "Prepare rollout" } });
    fireEvent.click(within(createDialog).getByRole("combobox", { name: "Assignee" }));
    fireEvent.click(await screen.findByRole("option", { name: "Jordan Lee · Resource" }));
    fireEvent.click(within(createDialog).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(createWorkItem).toHaveBeenCalledWith(expect.objectContaining({ title: "Prepare rollout", projectId: "project-1", assignedResourceId: "resource-2" })));
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Create work item" })).not.toBeInTheDocument());
  });

  it("shows a localized severity selector without free-text severity or resolution for Issues", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderWorkItems();

    fireEvent.click(screen.getByRole("button", { name: "إنشاء عنصر عمل" }));
    const createDialog = await screen.findByRole("dialog", { name: "إنشاء عنصر عمل" });
    expect(within(createDialog).getByText("أدخل التفاصيل الأساسية للمشروع والمسؤول عنه وموعده المستهدف.")).toBeInTheDocument();
    expect(within(createDialog).getByRole("region", { name: "تفاصيل عنصر العمل" })).toBeInTheDocument();
    expect(within(createDialog).getByRole("region", { name: "التخطيط والإسناد" })).toBeInTheDocument();
    expect(within(createDialog).getByRole("region", { name: "الوصف" })).toBeInTheDocument();
    expect(within(createDialog).getByRole("textbox", { name: "الوصف" })).toHaveClass("flex-1", "max-h-none");

    fireEvent.change(within(createDialog).getByLabelText("النوع"), { target: { value: "Issue" } });
    const severity = within(createDialog).getByRole("combobox", { name: "الخطورة" });
    expect(severity).toHaveValue("Medium");
    expect(within(severity).getByRole("option", { name: "رئيسية" })).toHaveValue("Major");
    expect(within(createDialog).queryByLabelText("الأولوية")).not.toBeInTheDocument();
    expect(within(createDialog).queryByRole("region", { name: "تفاصيل المشكلة" })).not.toBeInTheDocument();
    expect(within(createDialog).queryByRole("textbox", { name: "الخطورة" })).not.toBeInTheDocument();
    expect(within(createDialog).queryByLabelText("الحل")).not.toBeInTheDocument();
    fireEvent.change(severity, { target: { value: "Major" } });
    fireEvent.change(within(createDialog).getByLabelText("العنوان *"), { target: { value: "مشكلة اختبار" } });
    fireEvent.click(within(createDialog).getByRole("button", { name: "حفظ" }));
    await waitFor(() => expect(createWorkItem).toHaveBeenCalledWith(expect.objectContaining({ title: "مشكلة اختبار", type: "Issue", priority: "Medium", severity: "Major" })));
    await waitFor(() => expect(document.documentElement).toHaveAttribute("dir", "rtl"));
  });

  it("opens the source-like detail side sheet and performs authorized workflow, assignment, and threaded-comment actions", async () => {
    renderWorkItems();
    fireEvent.click(screen.getByRole("button", { name: "Prepare release" }));

    expect(await screen.findByRole("dialog", { name: "Prepare release" })).toBeInTheDocument();
    expect(screen.getByText("Confirm the release checklist.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Open full details" })).toHaveAttribute("href", "/work-items/task-1");
    fireEvent.click(screen.getByRole("button", { name: "Start work" }));
    await waitFor(() => expect(executeTransition).toHaveBeenCalledWith({ transitionCode: "StartWork" }));

    fireEvent.click(within(screen.getByRole("dialog", { name: "Prepare release" })).getByRole("combobox", { name: "Assignee" }));
    fireEvent.click(await screen.findByRole("option", { name: "Jordan Lee · Resource" }));
    await waitFor(() => expect(updateWorkItem).toHaveBeenCalledWith(expect.objectContaining({ id: "task-1", body: expect.objectContaining({ assignedResourceId: "resource-2", rowVersion: [3], estimatedHours: 3 }) })));

    fireEvent.click(screen.getByRole("tab", { name: "Comments" }));
    expect(await screen.findByText("Please confirm the test run.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Reply" }));
    fireEvent.input(screen.getByRole("textbox", { name: "Write a reply..." }), { target: { innerHTML: "Run passed." } });
    fireEvent.click(screen.getAllByRole("button", { name: "Reply" }).at(-1)!);
    await waitFor(() => expect(addComment).toHaveBeenCalledWith({ commentText: "Run passed.", parentCommentId: "comment-1" }));
  });

  it("keeps workflow and assignee changes hidden in read-only management context", async () => {
    useManagementMock.mockReturnValue({ canMutate: false, effectiveReadOnly: true } as unknown as ReturnType<typeof useManagement>);
    renderWorkItems();
    fireEvent.click(screen.getByRole("button", { name: "Prepare release" }));
    expect(await screen.findByRole("dialog", { name: "Prepare release" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Start work" })).toBeDisabled();
    expect(within(screen.getByRole("dialog", { name: "Prepare release" })).queryByRole("combobox", { name: "Assignee" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Create work item" })).toBeDisabled();
  });
});
