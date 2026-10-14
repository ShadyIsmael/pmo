import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { TestManagementPageFull } from "../features/operations/OperationsPagesFull";

const fixtures = vi.hoisted(() => ({
  items: [] as Array<Record<string, unknown>>,
  statuses: [
    { id: "passed", code: "Passed", nameEn: "Passed", nameAr: "ناجح", category: "Done", displayOrder: 1, isActive: true },
    { id: "failed", code: "Failed", nameEn: "Failed", nameAr: "فاشل", category: "Active", displayOrder: 2, isActive: true },
    { id: "blocked", code: "Blocked", nameEn: "Blocked", nameAr: "متعذر", category: "Active", displayOrder: 3, isActive: true },
  ],
  insights: { data: { total: 9, open: 7, overdue: 0, unassigned: 0, dueThisWeek: 0, byStatus: [{ key: "Passed", count: 2 }, { key: "Failed", count: 1 }, { key: "Blocked", count: 3 }, { key: "Draft", count: 1 }, { key: "Ready", count: 2 }], byPriority: [] }, isLoading: false, isError: false, refetch: vi.fn() },
  requests: [] as Array<Record<string, unknown>>,
  listError: false,
  refetchList: vi.fn(),
}));

vi.mock("@api/workItemsFull", () => ({
  useFullWorkItems: (params: Record<string, string | number | boolean | undefined>) => {
    fixtures.requests.push(params);
    let matching = fixtures.items.filter((item) => item.type === params.type);
    if (params.status) matching = matching.filter((item) => item.status === params.status);
    if (params.search) matching = matching.filter((item) => String(item.title).toLowerCase().includes(String(params.search).toLowerCase()));
    const page = Number(params.page ?? 1);
    const pageSize = Number(params.pageSize ?? 20);
    const start = (page - 1) * pageSize;
    return {
      data: { items: matching.slice(start, start + pageSize), totalCount: matching.length, totalPages: Math.max(1, Math.ceil(matching.length / pageSize)) },
      isLoading: false,
      isError: fixtures.listError,
      refetch: fixtures.refetchList,
    };
  },
  useFullWorkItemInsights: () => fixtures.insights,
}));

vi.mock("@api/workItems", () => ({ useWorkItemStatuses: () => ({ data: fixtures.statuses }) }));

function makeItem(index: number, status = "Passed") {
  return {
    id: `test-${index}`,
    title: `Test case ${index}`,
    type: "TestCase",
    priority: index % 2 ? "High" : "Medium",
    status,
    assignedResourceName: index % 2 ? "Alex Tester" : undefined,
    assignedTeamName: undefined,
    createdAt: "2026-10-10T09:00:00Z",
    updatedAt: "2026-10-10T10:00:00Z",
  };
}

function renderPage() {
  return render(<I18nProvider><MemoryRouter><TestManagementPageFull /></MemoryRouter></I18nProvider>);
}

describe("Test Management source parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.items = [makeItem(1, "Passed"), makeItem(2, "Failed"), makeItem(3, "Blocked"), makeItem(4, "Draft")];
    fixtures.requests = [];
    fixtures.listError = false;
    fixtures.refetchList.mockClear();
    fixtures.insights = {
      data: { total: 9, open: 7, overdue: 0, unassigned: 0, dueThisWeek: 0, byStatus: [{ key: "Passed", count: 2 }, { key: "Failed", count: 1 }, { key: "Blocked", count: 3 }, { key: "Draft", count: 1 }, { key: "Ready", count: 2 }], byPriority: [] },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    };
  });

  it("renders live TestCase metrics, status tabs, localized statuses, and no mock detail link", () => {
    renderPage();

    expect(screen.getByText("Test management")).toBeInTheDocument();
    expect(screen.getAllByText("Passed").length).toBeGreaterThan(1);
    expect(screen.getAllByText("Failed").length).toBeGreaterThan(1);
    expect(screen.getAllByText("Blocked").length).toBeGreaterThan(1);
    expect(screen.getByText("Draft / Ready")).toBeInTheDocument();
    expect(screen.getByRole("tablist")).toBeInTheDocument();
    expect(screen.getByText("Test case 1")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Open work items/i })).toHaveAttribute("href", "/work-items");
    expect(screen.queryByRole("link", { name: "Test case 1" })).not.toBeInTheDocument();
    expect(fixtures.requests.at(-1)).toMatchObject({ type: "TestCase", page: 1, pageSize: 20 });
    expect(screen.getByText("2")).toBeInTheDocument();
  });

  it("queries the server by selected status and search instead of filtering only the first page", async () => {
    fixtures.items = [makeItem(1, "Failed"), makeItem(2, "Passed")];
    renderPage();

    fireEvent.click(screen.getByRole("tab", { name: "Failed" }));
    await waitFor(() => expect(fixtures.requests.at(-1)).toMatchObject({ type: "TestCase", status: "Failed", page: 1 }));
    expect(screen.getByText("Test case 1")).toBeInTheDocument();
    expect(screen.queryByText("Test case 2")).not.toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText("Search test cases..."), { target: { value: "Test case 1" } });
    await waitFor(() => expect(fixtures.requests.at(-1)).toMatchObject({ status: "Failed", search: "Test case 1", page: 1 }), { timeout: 1500 });
  });

  it("supports server pagination instead of silently truncating the list", async () => {
    fixtures.items = Array.from({ length: 23 }, (_, index) => makeItem(index + 1));
    renderPage();

    expect(screen.getByText("Test case 1")).toBeInTheDocument();
    expect(screen.queryByText("Test case 21")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    await waitFor(() => expect(fixtures.requests.at(-1)).toMatchObject({ page: 2, pageSize: 20 }));
    expect(await screen.findByText("Test case 21")).toBeInTheDocument();
  });

  it("offers retry when the live TestCase query fails", () => {
    fixtures.listError = true;
    renderPage();

    const alert = screen.getByRole("alert");
    fireEvent.click(within(alert).getByRole("button", { name: "Retry" }));
    expect(fixtures.refetchList).toHaveBeenCalledOnce();
  });

  it("uses Arabic text and localized work-item status references", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderPage();

    expect(screen.getByText("إدارة الاختبارات")).toBeInTheDocument();
    expect(screen.getAllByText("ناجح").length).toBeGreaterThan(1);
  });
});
