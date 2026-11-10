import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { MyWorkApprovalsPage } from "@features/my-work/MyWorkApprovalsPage";

const auth = vi.hoisted(() => ({
  permissions: ["Project.View", "ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"] as string[],
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { id: "reviewer-1", permissions: auth.permissions, roles: [] }, status: "authenticated" }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ refreshSnapshot: vi.fn(), effectiveReadOnly: false }),
}));

vi.mock("@features/my-work/AnnualBudgetReviewDetailsSheet", () => ({
  AnnualBudgetReviewDetailsSheet: ({ requestId, open, readOnly }: { requestId: string; open: boolean; readOnly: boolean }) => open
    ? <div role="dialog" aria-label="Annual Budget review details" data-request-id={requestId} data-read-only={String(readOnly)} />
    : null,
}));

const pendingApproval = {
  requestId: "request-pending",
  budgetId: "budget-pending",
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

const approvedHistoryItem = {
  ...pendingApproval,
  requestId: "request-approved",
  budgetId: "budget-approved",
  status: "Approved",
  requestType: "ProjectAnnualBudgetReview",
  canDecide: false,
  assignedStepState: "Approved",
};

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  const view = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={["/my-work/approvals"]}>
        <I18nProvider><MyWorkApprovalsPage /></I18nProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { queryClient, ...view };
}

describe("My Work approval list UX", () => {
  beforeEach(() => {
    auth.permissions = ["Project.View", "ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"];
    vi.stubGlobal("fetch", vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/my-work/annual-budget-approvals/history")) {
        return Promise.resolve(new Response(JSON.stringify([approvedHistoryItem]), { status: 200 }));
      }
      return Promise.resolve(new Response(JSON.stringify([pendingApproval]), { status: 200 }));
    }));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    window.localStorage.clear();
  });

  it("opens the pending Annual Budget request in its details sheet", async () => {
    const { queryClient } = renderPage();

    expect(await screen.findByText("Core Modernization · PRJ-104")).toBeInTheDocument();
    expect(screen.getByText(/Finance Lead/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Review Details" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Review Details" }));

    expect(await screen.findByRole("dialog", { name: "Annual Budget review details" })).toHaveAttribute("data-request-id", "request-pending");
    expect(screen.getByRole("dialog", { name: "Annual Budget review details" })).toHaveAttribute("data-read-only", "false");
    queryClient.clear();
  });

  it("loads assigned Annual Budget history independently and opens completed requests read-only", async () => {
    const fetchMock = vi.mocked(fetch);
    const { queryClient } = renderPage();

    await screen.findByText("Core Modernization · PRJ-104");
    fireEvent.click(screen.getByRole("tab", { name: "History (0)" }));

    expect(await screen.findByText("Approved")).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes("/api/my-work/annual-budget-approvals/history"))).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Review Details" }));
    expect(await screen.findByRole("dialog", { name: "Annual Budget review details" })).toHaveAttribute("data-request-id", "request-approved");
    expect(screen.getByRole("dialog", { name: "Annual Budget review details" })).toHaveAttribute("data-read-only", "true");
    queryClient.clear();
  });

  it("allows View-only users to load assigned history without querying actionable approvals", async () => {
    auth.permissions = ["Project.View", "ProjectAnnualBudget.View"];
    const fetchMock = vi.mocked(fetch);
    const { queryClient } = renderPage();

    fireEvent.click(screen.getByRole("tab", { name: "History (0)" }));
    expect(await screen.findByText("Approved")).toBeInTheDocument();
    const requestUrls = fetchMock.mock.calls.map(([input]) => String(input));
    expect(requestUrls.some((url) => url.endsWith("/api/my-work/annual-budget-approvals"))).toBe(false);
    expect(requestUrls.some((url) => url.includes("/api/my-work/annual-budget-approvals/history"))).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Review Details" }));
    expect(await screen.findByRole("dialog", { name: "Annual Budget review details" })).toHaveAttribute("data-request-id", "request-approved");
    expect(screen.getByRole("dialog", { name: "Annual Budget review details" })).toHaveAttribute("data-read-only", "true");
    queryClient.clear();
  });
});
