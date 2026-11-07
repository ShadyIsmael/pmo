import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { ManagementProvider } from "@api/management";
import { ApprovalReviewPage } from "@features/my-work/ApprovalReviewPage";

describe("assignment-scoped approval review page", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("shows the limited read-only projection and no decision controls when the API disallows action", async () => {
    const review = {
      approvalId: "change-1",
      approvalType: "ChangeRequest",
      businessObjectType: "Project",
      businessObjectName: "Core Banking Modernization",
      businessObjectCode: "PRJ-1001",
      businessObjectStatus: "Active",
      title: "Change Request #12",
      reason: "Align the approved budget",
      justification: "Supplier contract changed",
      requestedByName: "Alex Admin",
      requestedAt: "2026-10-20T10:00:00Z",
      status: "Pending",
      isReadOnly: true,
      canDecide: false,
    };
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify(review), { status: 200 })));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <ManagementProvider user={null}>
          <I18nProvider>
            <MemoryRouter initialEntries={["/my-work/approvals/review/change-request/change-1"]}>
              <Routes><Route path="/my-work/approvals/review/change-request/:id" element={<ApprovalReviewPage type="change-request" />} /></Routes>
            </MemoryRouter>
          </I18nProvider>
        </ManagementProvider>
      </QueryClientProvider>,
    );

    expect(await screen.findByText("Align the approved budget")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Change request review" })).toBeInTheDocument();
    expect(screen.getAllByText(/Core Banking Modernization/)).toHaveLength(2);
    expect(screen.getByText(/Approval Review — Read Only/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Approve" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reject" })).not.toBeInTheDocument();
    client.clear();
  });
});
