import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { MyWorkApprovalsPage } from "@features/my-work/MyWorkApprovalsPage";

const state = vi.hoisted(() => ({
  permissions: ["Project.View", "ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"] as string[],
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { id: "reviewer-1", permissions: state.permissions }, status: "authenticated" }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ context: null, refreshSnapshot: vi.fn(), effectiveReadOnly: false }),
}));

const approval = {
  requestId: "request-1",
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

function createClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
}

function renderPage(locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  const client = createClient();
  const view = render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={["/my-work/approvals"]}>
        <I18nProvider><MyWorkApprovalsPage /></I18nProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { client, ...view };
}

describe("My Work Annual Budget approval inbox", () => {
  beforeEach(() => {
    state.permissions = ["Project.View", "ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"];
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify([approval]), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    })));
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    window.localStorage.clear();
  });

  it("shows a scannable assigned budget approval with a Review Details action", async () => {
    const { client } = renderPage();

    expect(await screen.findByText("Core Modernization · PRJ-104")).toBeInTheDocument();
    expect(screen.getByText("Fiscal year 2026")).toBeInTheDocument();
    expect(
      screen.getByText((_, element) => element?.textContent === "EGP\u00a0125,000"),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Review Details" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Pending (1)" })).toBeInTheDocument();
    client.clear();
  });

  it("does not query the assigned inbox without project and budget View permissions", async () => {
    state.permissions = ["ProjectAnnualBudget.Approve"];
    const fetchMock = vi.mocked(fetch);
    const { client } = renderPage();

    expect(await screen.findByText("No pending approvals")).toBeInTheDocument();
    await waitFor(() => expect(fetchMock).not.toHaveBeenCalled());
    client.clear();
  });

  it("localizes Annual Budget approvals in Arabic while preserving RTL", async () => {
    const { client } = renderPage("ar");

    expect(await screen.findByText("Core Modernization · PRJ-104")).toBeInTheDocument();
    expect(screen.getByRole("button")).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute("dir", "rtl");
    client.clear();
  });
});
