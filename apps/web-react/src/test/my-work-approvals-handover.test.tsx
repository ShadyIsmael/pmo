import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, renderHook, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { ManagementProvider } from "@api/management";
import { MyWorkApprovalsPage } from "@features/my-work/MyWorkApprovalsPage";
import { useAcceptManagerHandover, useApproveProjectChangeRequest } from "@features/my-work/api/myWork";

const approvalsAuth = vi.hoisted(() => ({ permissions: ["ChangeRequest.Approve", "Handover.Approve"] as string[] }));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "approver-1", permissions: approvalsAuth.permissions }, status: "authenticated" }) }));

afterEach(() => {
  vi.unstubAllGlobals();
  approvalsAuth.permissions = ["ChangeRequest.Approve", "Handover.Approve"];
});

function renderApprovalsPage(client: QueryClient) {
  return render(
    <QueryClientProvider client={client}>
      <ManagementProvider user={null}>
        <I18nProvider><MemoryRouter><MyWorkApprovalsPage /></MemoryRouter></I18nProvider>
      </ManagementProvider>
    </QueryClientProvider>,
  );
}

describe("project handover approvals", () => {
  it("does not query project or manager handovers without Handover.Approve", async () => {
    approvalsAuth.permissions = ["ChangeRequest.Approve"];
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/project-change-requests/pending-for-me")) return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
      if (input.includes("/api/handover-requests/pending-for-me") || input.includes("/api/manager-handovers/pending-for-me")) {
        return Promise.resolve(new Response(JSON.stringify({ error: "Forbidden" }), { status: 403 }));
      }
      return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });

    renderApprovalsPage(client);

    expect(await screen.findByText("No pending approvals")).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes("/api/handover-requests/pending-for-me"))).toBe(false);
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes("/api/manager-handovers/pending-for-me"))).toBe(false);
    expect(screen.queryByText("Project and manager handover approvals could not be loaded.")).not.toBeInTheDocument();
    client.clear();
  });

  it("shows a retry action when an authorized handover queue fails to load", async () => {
    approvalsAuth.permissions = ["Handover.Approve"];
    let handoverCalls = 0;
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/handover-requests/pending-for-me")) {
        handoverCalls += 1;
        return Promise.resolve(handoverCalls === 1
          ? new Response(JSON.stringify({ error: "Temporary failure" }), { status: 500 })
          : new Response(JSON.stringify([]), { status: 200 }));
      }
      if (input.includes("/api/manager-handovers/pending-for-me")) return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
      return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });

    renderApprovalsPage(client);

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Project and manager handover approvals could not be loaded.");
    fireEvent.click(within(alert).getByRole("button", { name: "Retry" }));
    await waitFor(() => expect(handoverCalls).toBe(2));
    expect(await screen.findByText("No pending approvals")).toBeInTheDocument();
    client.clear();
  });

  it("handles an already-decided request without an unhandled error and refreshes the pending list", async () => {
    const pendingRequest = {
      id: "handover-1",
      projectId: "project-1",
      projectName: "Core Modernization",
      projectCode: "PRJ-104",
      currentProjectManagerId: "manager-1",
      currentProjectManagerName: "Current Manager",
      newProjectManagerId: "manager-2",
      newProjectManagerName: "New Manager",
      effectiveHandoverDate: "2026-12-15",
      reason: "Ownership change",
      status: "Pending",
      requestedBy: "requester-1",
      requestedByName: "Alex Admin",
      requestedAt: "2026-10-01T10:00:00Z",
    };
    let pendingCalls = 0;
    const fetchMock = vi.fn().mockImplementation((input: string, init?: RequestInit) => {
      if (input.includes("/api/handover-requests/pending-for-me")) {
        pendingCalls += 1;
        return Promise.resolve(new Response(JSON.stringify(pendingCalls === 1 ? [pendingRequest] : []), { status: 200 }));
      }
      if (input.includes("/api/handover-requests/handover-1/accept") && init?.method === "POST") {
        return Promise.resolve(new Response(JSON.stringify({ error: "The handover request is no longer pending" }), { status: 409 }));
      }
      if (input.includes("/api/manager-handovers/pending-for-me") || input.includes("/api/project-change-requests/pending-for-me")) {
        return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
      }
      return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);

    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
    renderApprovalsPage(client);

    await screen.findByText("Core Modernization");
    fireEvent.click(screen.getByRole("button", { name: "Approve" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("This handover request is no longer pending. The approvals list has been refreshed.");
    await waitFor(() => expect(pendingCalls).toBeGreaterThanOrEqual(2));
    await waitFor(() => expect(screen.getByText("No pending approvals")).toBeInTheDocument());
    expect(fetchMock.mock.calls.filter(([input, init]) => String(input).includes("/api/handover-requests/handover-1/accept") && init?.method === "POST")).toHaveLength(1);
  });

  it("refreshes the manager-handover approval queue after a decision", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 60_000 }, mutations: { retry: false } } });
    const pendingKey = ["managerHandovers", "pendingForMe"] as const;
    client.setQueryData(pendingKey, [{ id: "manager-handover-1" }]);
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: "manager-handover-1" }), { status: 200 })));
    const wrapper = ({ children }: { children: React.ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
    const { result } = renderHook(() => useAcceptManagerHandover("programs"), { wrapper });

    await act(async () => { await result.current.mutateAsync("manager-handover-1"); });

    expect(client.getQueryState(pendingKey)?.isInvalidated).toBe(true);
    client.clear();
  });

  it("refreshes project-change approval and project read-model queries after approval", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 60_000 }, mutations: { retry: false } } });
    const pendingKey = ["projectChangeRequests", "pendingForMe"] as const;
    const projectChangeKey = ["projects", "change-requests", "project-1"] as const;
    const projectDetailKey = ["projects", "detail", "project-1"] as const;
    client.setQueryData(pendingKey, [{ id: "change-1" }]);
    client.setQueryData(projectChangeKey, [{ id: "change-1" }]);
    client.setQueryData(projectDetailKey, { id: "project-1" });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: "change-1", projectId: "project-1" }), { status: 200 })));
    const wrapper = ({ children }: { children: React.ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
    const { result } = renderHook(() => useApproveProjectChangeRequest(), { wrapper });

    await act(async () => { await result.current.mutateAsync({ id: "change-1", dto: {} }); });

    expect(client.getQueryState(pendingKey)?.isInvalidated).toBe(true);
    expect(client.getQueryState(projectChangeKey)?.isInvalidated).toBe(true);
    expect(client.getQueryState(projectDetailKey)?.isInvalidated).toBe(true);
    client.clear();
  });
});

describe("project change request approvals", () => {
  it("does not request the change-approval queue without ChangeRequest.Approve", async () => {
    approvalsAuth.permissions = [];
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify([]), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });

    renderApprovalsPage(client);

    expect(await screen.findByText("No pending approvals")).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes("/api/project-change-requests/pending-for-me"))).toBe(false);
    expect(screen.queryByText("Could not load change requests.")).not.toBeInTheDocument();
    client.clear();
  });

  it("shows an approval conflict inline on the affected request in Arabic", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    const pendingRequests = [
      {
        id: "change-1",
        projectId: "project-1",
        projectName: "Core Modernization",
        number: 7,
        type: "Baseline",
        status: "Pending",
        reason: "Update delivery baseline",
        requestedBy: "requester-1",
        requestedByName: "Alex Admin",
        requestedAt: "2026-10-01T10:00:00Z",
      },
      {
        id: "change-2",
        projectId: "project-2",
        projectName: "Customer Portal",
        number: 8,
        type: "Administrative",
        status: "Pending",
        reason: "Update project governance",
        requestedBy: "requester-2",
        requestedByName: "Sam Lee",
        requestedAt: "2026-10-02T10:00:00Z",
      },
    ];
    const fetchMock = vi.fn().mockImplementation((input: string, init?: RequestInit) => {
      if (input.includes("/api/project-change-requests/pending-for-me")) {
        return Promise.resolve(new Response(JSON.stringify(pendingRequests), { status: 200 }));
      }
      if (input.includes("/api/project-change-requests/change-1/approve") && init?.method === "POST") {
        return Promise.resolve(new Response(JSON.stringify({ error: "Request is no longer pending" }), { status: 409 }));
      }
      return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);

    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
    renderApprovalsPage(client);

    const projectLink = await screen.findByRole("link", { name: "Core Modernization" });
    const requestRow = projectLink.closest<HTMLElement>("div.flex.items-start");
    expect(requestRow).not.toBeNull();
    fireEvent.click(within(requestRow!).getByRole("button", { name: "موافقة" }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("لم يعد طلب التغيير معلقًا. تم تحديث قائمة الموافقات.");
    expect(alert.closest("div.flex.items-start")).toBe(requestRow);
    const otherProjectLink = screen.getByRole("link", { name: "Customer Portal" });
    expect(within(otherProjectLink.closest<HTMLElement>("div.flex.items-start")!).queryByRole("alert")).not.toBeInTheDocument();
    client.clear();
  });

  it("localizes the request details and handles a stale rejection without an unhandled error", async () => {
    const pendingRequest = {
      id: "change-1",
      projectId: "project-1",
      projectName: "Core Modernization",
      number: 7,
      type: "Baseline",
      status: "Pending",
      reason: "Update delivery baseline",
      justification: "Schedule was re-estimated",
      requestedBy: "requester-1",
      requestedByName: "Alex Admin",
      requestedAt: "2026-10-01T10:00:00Z",
    };
    let pendingCalls = 0;
    const fetchMock = vi.fn().mockImplementation((input: string, init?: RequestInit) => {
      if (input.includes("/api/project-change-requests/pending-for-me")) {
        pendingCalls += 1;
        return Promise.resolve(new Response(JSON.stringify(pendingCalls === 1 ? [pendingRequest] : []), { status: 200 }));
      }
      if (input.includes("/api/project-change-requests/change-1/reject") && init?.method === "POST") {
        return Promise.resolve(new Response(JSON.stringify({ error: "The change request is no longer pending" }), { status: 409 }));
      }
      return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);

    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
    renderApprovalsPage(client);

    await screen.findByText("Core Modernization");
    expect(screen.getByRole("link", { name: "Core Modernization" })).toHaveAttribute("href", "/projects/project-1");
    expect(screen.getByText(/Baseline Change/)).toBeInTheDocument();
    expect(screen.getByText("Pending")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Reject" }));
    await screen.findByText("Reject change request");
    const dialog = screen.getByRole("dialog");
    fireEvent.change(within(dialog).getByPlaceholderText("Notes for your decision..."), { target: { value: "Reconsider dates" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Confirm rejection" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("This change request is no longer pending. The approvals list has been refreshed.");
    await waitFor(() => expect(pendingCalls).toBeGreaterThanOrEqual(2));
    await waitFor(() => expect(screen.queryByText("Update delivery baseline")).not.toBeInTheDocument());
    expect(fetchMock.mock.calls.filter(([input, init]) => String(input).includes("/api/project-change-requests/change-1/reject") && init?.method === "POST")).toHaveLength(1);
    client.clear();
  });
});
