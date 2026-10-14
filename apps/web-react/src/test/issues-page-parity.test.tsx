import * as React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { IssuesPage } from "../features/risks/IssuesPage";

const fixtures = vi.hoisted(() => ({
  permissions: ["WorkItem.Create"] as string[],
  statuses: [
    { id: "open", code: "Open", nameEn: "Open", nameAr: "مفتوح", displayOrder: 1, isActive: true, isTerminal: false },
    { id: "done", code: "Done", nameEn: "Done", nameAr: "مكتمل", displayOrder: 2, isActive: true, isTerminal: true },
  ],
  params: [] as Record<string, unknown>[],
  createIssue: vi.fn(),
  uploadAttachments: vi.fn(),
  query: {
    data: { items: [] as Array<Record<string, unknown>>, totalCount: 0 },
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
  },
}));

vi.mock("@api/workItemsFull", () => ({
  useFullWorkItems: (params: Record<string, unknown>) => {
    fixtures.params.push(params);
    return fixtures.query;
  },
  useFullWorkItemAssignees: () => ({ data: [], isLoading: false }),
  useFullWorkItemMutations: () => ({ create: { mutateAsync: fixtures.createIssue, isPending: false } }),
  workItemsApiFull: { uploadAttachments: fixtures.uploadAttachments },
}));

vi.mock("@api/workItems", () => ({ useWorkItemStatuses: () => ({ data: fixtures.statuses }) }));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, effectiveReadOnly: false }) }));
vi.mock("@features/projects/api/projects", () => ({ useProjects: () => ({ data: { items: [] }, isLoading: false }) }));
vi.mock("@features/delivery/WorkItemForm", () => ({
  WorkItemForm: ({ draft, onChange, extraContent }: { draft: { title: string; type: string; priority: string; severity?: string }; onChange: (patch: { title: string }) => void; extraContent?: import("react").ReactNode }) => <div><label htmlFor="issue-title">Issue title</label><input id="issue-title" value={draft.title} data-type={draft.type} data-priority={draft.priority} data-severity={draft.severity} onChange={(event) => onChange({ title: event.target.value })} />{extraContent}</div>,
}));

function renderIssues(path = "/risks-issues/issues") {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/risks-issues/issues" element={<IssuesPage />} />
          <Route path="/work-items/:id" element={<p>Work item details</p>} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("Issues page source parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.permissions = ["WorkItem.Create"];
    fixtures.params.length = 0;
    fixtures.createIssue.mockReset();
    fixtures.createIssue.mockResolvedValue({ id: "issue-created" });
    fixtures.uploadAttachments.mockReset();
    fixtures.uploadAttachments.mockResolvedValue([]);
    fixtures.query = {
      data: { items: [], totalCount: 0 },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    };
  });

  it("starts in the issue scope and renders the source KPI and table columns", () => {
    fixtures.query.data = {
      totalCount: 2,
      items: [
        { id: "issue-1", type: "Issue", title: "Supplier delay", projectName: "Core Upgrade", priority: "Critical", status: "Open", dueDate: "2026-10-18", targetResolutionDate: "2026-10-19" },
        { id: "issue-2", type: "Issue", title: "Build failure", projectName: "Core Upgrade", priority: "High", status: "Done", dueDate: "2026-10-19", targetResolutionDate: "2026-10-20" },
      ],
    };
    renderIssues();

    expect(fixtures.params.at(-1)).toMatchObject({ page: 1, pageSize: 100, type: "Issue" });
    expect(screen.getByRole("heading", { name: "Issues" })).toBeInTheDocument();
    expect(screen.getByText("Supplier delay").closest("a")).toHaveAttribute("href", "/work-items/issue-1");
    expect(screen.getByRole("columnheader", { name: "Project" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Due date" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Status" })).toBeInTheDocument();
    expect(screen.getByText("Supplier delay").closest("tr")).toHaveTextContent("Core Upgrade");
    expect(screen.getByText("Supplier delay").closest("tr")).toHaveTextContent("Oct 19, 2026");
    expect(screen.getByText("Supplier delay").closest("tr")).not.toHaveTextContent("Oct 18, 2026");
  });

  it("preserves the project query scope when loading issues", () => {
    renderIssues("/risks-issues/issues?projectId=project-1");

    expect(fixtures.params.at(-1)).toMatchObject({ projectId: "project-1", type: "Issue" });
  });

  it("applies type, search, priority and status filters to the API request and can clear them", async () => {
    renderIssues();

    fireEvent.click(screen.getByRole("tab", { name: "All" }));
    fireEvent.change(screen.getByRole("textbox", { name: "Search issues..." }), { target: { value: "release" } });
    fireEvent.change(screen.getByRole("combobox", { name: "Priority" }), { target: { value: "High" } });
    fireEvent.click(screen.getByRole("button", { name: "Filter" }));
    fireEvent.change(screen.getByRole("combobox", { name: "Status" }), { target: { value: "Done" } });

    await waitFor(() => expect(fixtures.params.at(-1)).toMatchObject({ type: undefined, search: "release", priority: "High", status: "Done" }));
    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));
    await waitFor(() => expect(fixtures.params.at(-1)).toMatchObject({ type: undefined, search: undefined, priority: undefined, status: undefined }));
    expect(screen.getByRole("combobox", { name: "Status" })).toHaveValue("all");
  });

  it("provides a retry action for API failures", () => {
    fixtures.query.isError = true;
    renderIssues();
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(fixtures.query.refetch).toHaveBeenCalledOnce();
  });

  it("offers issue creation from the empty state and respects create permission", () => {
    fixtures.permissions = [];
    renderIssues();
    expect(screen.getByText("No issues found")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "New Issue" }).every((button) => (button as HTMLButtonElement).disabled)).toBe(true);
    expect(screen.queryByRole("dialog", { name: "New Issue" })).not.toBeInTheDocument();
  });

  it("creates an issue through the real work-item mutation and opens its details", async () => {
    renderIssues();
    fireEvent.click(screen.getAllByRole("button", { name: "New Issue" })[0]!);
    const title = screen.getByRole("textbox", { name: "Issue title" });
    expect(title).toHaveAttribute("data-type", "Issue");
    expect(title).toHaveAttribute("data-priority", "Medium");
    expect(title).toHaveAttribute("data-severity", "Medium");
    fireEvent.change(title, { target: { value: "Supplier delay" } });
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    await waitFor(() => expect(fixtures.createIssue).toHaveBeenCalledWith(expect.objectContaining({ title: "Supplier delay", type: "Issue", priority: "Medium", severity: "Medium" })));
    expect(await screen.findByText("Work item details")).toBeInTheDocument();
  });

  it("retries attachment upload on the created issue without creating a duplicate", async () => {
    fixtures.uploadAttachments.mockRejectedValueOnce(new Error("Upload failed"));
    renderIssues();
    fireEvent.click(screen.getAllByRole("button", { name: "New Issue" })[0]!);
    fireEvent.change(screen.getByRole("textbox", { name: "Issue title" }), { target: { value: "Supplier delay" } });
    const file = new File(["support"], "supplier-note.txt", { type: "text/plain" });
    fireEvent.change(screen.getByLabelText("Choose files"), { target: { files: [file] } });
    fireEvent.click(screen.getByRole("button", { name: "Create" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Attachments could not be uploaded.");
    expect(screen.getByText("supplier-note.txt")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Upload" }));

    expect(await screen.findByText("Work item details")).toBeInTheDocument();
    expect(fixtures.createIssue).toHaveBeenCalledOnce();
    expect(fixtures.uploadAttachments).toHaveBeenNthCalledWith(1, "issue-created", [file]);
    expect(fixtures.uploadAttachments).toHaveBeenNthCalledWith(2, "issue-created", [file]);
  });

  it("localizes the issue tabs and side-sheet in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderIssues();
    expect(screen.getByRole("tab", { name: "مشكلة" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByText(/· 0 إجمالي المشكلات/)).toBeInTheDocument();
    fireEvent.click(screen.getAllByRole("button", { name: "مشكلة جديدة" })[0]!);
    expect(screen.getByRole("dialog", { name: "مشكلة جديدة" })).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Issue title" })).toHaveAttribute("data-type", "Issue");
  });
});
