import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { MyWorkApprovalsPage } from "@features/my-work/MyWorkApprovalsPage";

const state = vi.hoisted(() => ({
  permissions: ["Document.TemplateDeletion.View", "Document.TemplateDeletion.Approve", "Document.TemplateDeletion.Reject"] as string[],
  requestStatus: "PendingApproval" as string,
  requesterId: "requester-2",
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { id: "reviewer-1", permissions: state.permissions }, status: "authenticated" }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ context: null, refreshSnapshot: vi.fn(), effectiveReadOnly: false }),
}));

const request = {
  id: "30000000-0000-4000-8000-000000000001",
  templateId: "40000000-0000-4000-8000-000000000001",
  templateName: "Approved Handbook",
  ownerOrganizationId: "50000000-0000-4000-8000-000000000001",
  ownerScope: "Organization",
  requesterId: "requester-2",
  requesterName: "Template Owner",
  reason: "Superseded by the current handbook.",
  status: "PendingApproval",
  requestedAt: "2026-11-18T10:00:00Z",
  approverId: null,
  approverName: null,
  decidedAt: null,
  decisionComments: null,
  deletionType: null,
};

function createClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
}

function renderPage(locale: "en" | "ar" = "en", initialPath = "/my-work/approvals") {
  window.localStorage.setItem("nexus-locale", locale);
  const client = createClient();
  const view = render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[initialPath]}>
        <I18nProvider><MyWorkApprovalsPage /></I18nProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { client, ...view };
}

function installInboxApi() {
  const calls: Array<{ url: string; method: string; body?: string }> = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const target = String(input);
    const method = init?.method ?? "GET";
    calls.push({ url: target, method, body: typeof init?.body === "string" ? init.body : undefined });
    if (method === "POST" && target.includes("/approve")) {
      return new Response(JSON.stringify({ ...request, status: "Approved", approverId: "reviewer-1", approverName: "Reviewer", decisionComments: "No protected references remain." }), { status: 200, headers: { "Content-Type": "application/json" } });
    }
    if (method === "POST" && target.includes("/reject")) {
      return new Response(JSON.stringify({ ...request, status: "Rejected", approverId: "reviewer-1", approverName: "Reviewer", decisionComments: "Keep this template for now." }), { status: 200, headers: { "Content-Type": "application/json" } });
    }
    const isHistory = target.includes("status=History");
    const items = isHistory
      ? [{ ...request, status: state.requestStatus, decisionComments: "The template changed after submission." }]
      : state.requestStatus === "PendingApproval" ? [{ ...request, status: state.requestStatus, requesterId: state.requesterId }] : [];
    return new Response(JSON.stringify({ items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1 }), { status: 200, headers: { "Content-Type": "application/json" } });
  });
  vi.stubGlobal("fetch", fetchMock);
  return { calls, fetchMock };
}

describe("template deletion inbox", () => {
  beforeEach(() => {
    state.permissions = ["Document.TemplateDeletion.View", "Document.TemplateDeletion.Approve", "Document.TemplateDeletion.Reject"];
    state.requestStatus = "PendingApproval";
    state.requesterId = "requester-2";
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    window.localStorage.clear();
  });

  it("requires decision comments and submits an approval", async () => {
    const { calls } = installInboxApi();
    const { client } = renderPage();

    expect(await screen.findByText("Approved Handbook")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Approve" }));
    const confirm = within(screen.getByRole("dialog")).getByRole("button", { name: "Approve" });
    expect(confirm).toBeDisabled();
    fireEvent.change(screen.getByPlaceholderText("Explain your decision…"), { target: { value: "No protected references remain." } });
    expect(confirm).toBeEnabled();
    fireEvent.click(confirm);

    await waitFor(() => expect(calls.some((call) => call.method === "POST" && call.url.includes("/approve")
      && JSON.parse(call.body ?? "{}").comments === "No protected references remain.")).toBe(true));
    client.clear();
  });

  it("never offers decisions on a request submitted by the current user", async () => {
    state.requesterId = "reviewer-1";
    installInboxApi();
    const { client } = renderPage();

    expect(await screen.findByText("Approved Handbook")).toBeInTheDocument();
    expect(screen.getByText("Superseded by the current handbook.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Approve" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reject" })).not.toBeInTheDocument();
    client.clear();
  });

  it("shows stale request history with a localized status", async () => {
    state.requestStatus = "Stale";
    installInboxApi();
    const { client } = renderPage("ar");
    fireEvent.click(screen.getByRole("tab", { name: /السجل/ }));

    expect(await screen.findByText("يلزم تقديم طلب جديد")).toBeInTheDocument();
    const staleRequestCard = document.querySelector('[data-template-deletion-request-id="30000000-0000-4000-8000-000000000001"]');
    expect(staleRequestCard?.textContent).toContain("The template changed after submission.");
    expect(document.documentElement).toHaveAttribute("dir", "rtl");
    client.clear();
  });

  it("keeps a pending notification target in Pending and focuses the matching request", async () => {
    installInboxApi();
    const { client } = renderPage("en", `/my-work/approvals?templateDeletionRequestId=${request.id}`);

    const templateName = await screen.findByText("Approved Handbook");
    expect(screen.getByRole("tab", { name: /Pending/ })).toHaveAttribute("aria-selected", "true");
    expect(templateName.closest(`[data-template-deletion-request-id="${request.id}"]`)).toHaveClass("ring-2");
    client.clear();
  });

  it.each(["Approved", "Rejected"])("opens History and focuses a %s notification target", async (status) => {
    state.requestStatus = status;
    installInboxApi();
    const { client } = renderPage("en", `/my-work/approvals?templateDeletionRequestId=${request.id}`);

    const templateName = await screen.findByText("Approved Handbook");
    expect(screen.getByRole("tab", { name: /History/ })).toHaveAttribute("aria-selected", "true");
    expect(templateName.closest(`[data-template-deletion-request-id="${request.id}"]`)).toHaveClass("ring-2");
    client.clear();
  });
});
