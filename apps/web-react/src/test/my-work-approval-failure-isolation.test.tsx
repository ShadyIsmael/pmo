import { QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { ManagementProvider } from "@api/management";
import { createSessionQueryClient } from "@api/queryClient";
import { I18nProvider } from "@i18n/messages";
import { MyWorkApprovalsPage } from "@features/my-work/MyWorkApprovalsPage";

const authState = vi.hoisted(() => ({
  permissions: [
    "Handover.Approve",
    "ChangeRequest.Approve",
    "Project.View",
    "ProjectAnnualBudget.View",
    "ProjectAnnualBudget.Approve",
  ] as string[],
  roles: [{ id: "management-role", name: "Project Manager", category: "Management" }],
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({
    user: { id: "reviewer-1", permissions: authState.permissions, roles: authState.roles },
    status: "authenticated",
  }),
}));

const changeApproval = {
  id: "change-1",
  number: 17,
  type: "Baseline",
  status: "Pending",
  reason: "Revised delivery baseline",
  projectName: "Customer Portal",
  requestedByName: "Project Lead",
  requestedAt: "2026-11-10T08:00:00Z",
  approvalCount: 0,
  requiredApprovalCount: 1,
};

const annualBudgetApproval = {
  requestId: "budget-request-1",
  budgetId: "budget-1",
  projectPublicId: "11111111-1111-4111-8111-111111111111",
  projectName: "Core Modernization",
  projectCode: "PRJ-104",
  fiscalYear: 2026,
  financialReference: "FIN-2026-001",
  amount: 125000,
  currency: "EGP",
  approvalMode: "Sequential",
  requestedAtUtc: "2026-11-10T08:30:00Z",
  actionableAtUtc: "2026-11-10T08:30:00Z",
  requestedByName: "Finance Lead",
};

function response(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

function renderPage() {
  const client = createSessionQueryClient();
  const view = render(
    <QueryClientProvider client={client}>
      <ManagementProvider user={null}>
        <I18nProvider><MemoryRouter><MyWorkApprovalsPage /></MemoryRouter></I18nProvider>
      </ManagementProvider>
    </QueryClientProvider>,
  );
  return { client, ...view };
}

describe("My Work approval category failure isolation", () => {
  beforeEach(() => {
    authState.permissions = [
      "Handover.Approve",
      "ChangeRequest.Approve",
      "Project.View",
      "ProjectAnnualBudget.View",
      "ProjectAnnualBudget.Approve",
    ];
    authState.roles = [{ id: "management-role", name: "Project Manager", category: "Management" }];
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    window.localStorage.clear();
  });

  it("keeps valid approval categories visible when both handover queues are denied and does not retry either denial", async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const requestUrl = String(input);
      if (requestUrl.endsWith("/api/handover-requests/pending-for-me"))
        return Promise.resolve(response({ error: "Management scope required" }, 403));
      if (requestUrl.endsWith("/api/manager-handovers/pending-for-me"))
        return Promise.resolve(response({ error: "Handover approval is unavailable" }, 403));
      if (requestUrl.endsWith("/api/project-change-requests/pending-for-me"))
        return Promise.resolve(response([changeApproval]));
      if (requestUrl.endsWith("/api/my-work/annual-budget-approvals"))
        return Promise.resolve(response([annualBudgetApproval]));
      return Promise.resolve(response([]));
    });
    vi.stubGlobal("fetch", fetchMock);
    const { client } = renderPage();

    expect(await screen.findByText("Project Annual Budget Approval")).toBeInTheDocument();
    expect(await screen.findByText(/Core Modernization/)).toBeInTheDocument();
    expect(await screen.findByText("Customer Portal")).toBeInTheDocument();
    expect(screen.queryByText("Loading...")).not.toBeInTheDocument();

    await waitFor(() => {
      expect(fetchMock.mock.calls.filter(([input]) => String(input).endsWith("/api/handover-requests/pending-for-me"))).toHaveLength(1);
      expect(fetchMock.mock.calls.filter(([input]) => String(input).endsWith("/api/manager-handovers/pending-for-me"))).toHaveLength(1);
    });
    expect(fetchMock.mock.calls.filter(([input]) => String(input).endsWith("/api/project-change-requests/pending-for-me"))).toHaveLength(1);
    expect(fetchMock.mock.calls.filter(([input]) => String(input).endsWith("/api/my-work/annual-budget-approvals"))).toHaveLength(1);
    client.clear();
  });

  it("skips both handover queues without a Management-category role while Annual Budget approvals still load", async () => {
    authState.permissions = [
      "Handover.Approve",
      "Project.View",
      "ProjectAnnualBudget.View",
      "ProjectAnnualBudget.Approve",
    ];
    authState.roles = [{ id: "business-role", name: "Business Reviewer", category: "Business" }];
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const requestUrl = String(input);
      if (requestUrl.endsWith("/api/my-work/annual-budget-approvals"))
        return Promise.resolve(response([annualBudgetApproval]));
      return Promise.resolve(response([]));
    });
    vi.stubGlobal("fetch", fetchMock);
    const { client } = renderPage();

    expect(await screen.findByText("Project Annual Budget Approval")).toBeInTheDocument();
    expect(await screen.findByText(/Core Modernization/)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input]) => String(input).endsWith("/api/handover-requests/pending-for-me"))).toBe(false);
    expect(fetchMock.mock.calls.some(([input]) => String(input).endsWith("/api/manager-handovers/pending-for-me"))).toBe(false);
    expect(fetchMock.mock.calls.filter(([input]) => String(input).endsWith("/api/my-work/annual-budget-approvals"))).toHaveLength(1);
    client.clear();
  });
});
