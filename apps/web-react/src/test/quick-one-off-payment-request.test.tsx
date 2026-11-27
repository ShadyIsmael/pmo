import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectPaymentRequests } from "../features/projects/ProjectPaymentRequests";

const fixtures = vi.hoisted(() => ({
  createQuickOneOff: vi.fn(),
  requests: vi.fn(),
  foundation: vi.fn(),
  fundingOptions: vi.fn(),
  lifetimeBudget: vi.fn(),
  approvers: vi.fn(),
}));

vi.mock("../features/projects/api/project-financials", () => {
  const query = (data: unknown = null) => ({ data, isLoading: false, isError: false, refetch: vi.fn() });
  const mutation = () => ({ mutateAsync: vi.fn(), isPending: false });
  return {
    useCancelProjectPaymentRequest: mutation,
    useCancelProjectPaymentReversal: mutation,
    useCreateQuickOneOffPaymentRequest: () => ({ mutateAsync: fixtures.createQuickOneOff, isPending: false }),
    useCreateProjectPaymentRequest: mutation,
    useCreateProjectPaymentReversal: mutation,
    useDecideProjectPaymentRequest: mutation,
    useDecideProjectPaymentReversal: mutation,
    usePaymentRequestApprovers: fixtures.approvers,
    usePaymentRequestMilestones: () => query([]),
    useProjectPaymentReversalApprovers: () => query([]),
    useProjectFinancialFoundation: fixtures.foundation,
    useProjectPaymentRequestFundingOptions: fixtures.fundingOptions,
    useProjectPaymentRequestSettlements: () => query(null),
    useProjectPaymentReversals: () => query({ items: [] }),
    useAssignedProjectPaymentReversalReview: () => query(null),
    useProjectPaymentRequest: () => query(null),
    useProjectPaymentRequests: fixtures.requests,
    useRecordProjectPaymentRequestSettlement: mutation,
    useRecordSettlementAdjustment: mutation,
    useSettlementAdjustments: () => query([]),
    useSubmitProjectPaymentReversal: mutation,
    useSubmitProjectPaymentRequest: mutation,
    useUpdateProjectPaymentReversal: mutation,
    useUpdateProjectPaymentRequest: mutation,
  };
});

vi.mock("../features/projects/api/planned-payments", () => ({
  usePlannedPaymentBudget: fixtures.lifetimeBudget,
}));

function queryResult(data: unknown) {
  return { data, isLoading: false, isError: false, refetch: vi.fn() };
}

function renderRequests(locale: "en" | "ar" = "en", canCreatePlannedPayments = true) {
  window.localStorage.setItem("nexus-locale", locale);
  return render(
    <I18nProvider>
      <MemoryRouter>
        <ProjectPaymentRequests
          projectId="project-42"
          canViewRequests
          canReviewRequests={false}
          canReviewReversals={false}
          canCreateRequests
          canCreatePlannedPayments={canCreatePlannedPayments}
          canEditRequests={false}
          canSubmitRequests
          canCancelRequests={false}
          canApproveRequests={false}
          canRecordSettlements={false}
          canCreateReversals={false}
          canApproveReversals={false}
        />
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("Quick One-off Payment Request", () => {
  beforeEach(() => {
    fixtures.createQuickOneOff.mockReset().mockResolvedValue({
      plannedPayment: { id: "plan-1" },
      installment: { id: "installment-1" },
      paymentRequest: { id: "request-1" },
    });
    fixtures.requests.mockReturnValue(queryResult({ items: [], total: 0, budgetAvailability: null }));
    fixtures.foundation.mockReturnValue(queryResult({
      projectId: "project-42",
      projectCurrency: "EGP",
      financeApprovalMode: "Sequential",
      isCurrentProjectManager: true,
    }));
    fixtures.fundingOptions.mockReturnValue(queryResult({
      projectCurrency: "EGP",
      budgets: [{
        id: "annual-1", reference: "AB-2026", fiscalYear: 2026, currency: "EGP", status: "Approved",
        annualBudgetAmount: 10000, reservedAmount: 1000, availableAmount: 9000,
        lines: [{ id: "line-1", reference: "BL-001", description: "Equipment", authorizedLimit: 5000, reservedAmount: 500, availableAmount: 4500 }],
      }],
    }));
    fixtures.lifetimeBudget.mockReturnValue(queryResult({
      projectId: "project-42", currency: "EGP", lifetimeCeiling: 50000,
      lifetimeConsumed: 10000, lifetimeReserved: 5000, availableLifetimeBudget: 35000, canRegister: true,
    }));
    fixtures.approvers.mockReturnValue(queryResult([{ userId: "approver-1", displayName: "Amina Approver" }]));
  });

  it("creates a linked one-off request as a draft and displays server budget availability", async () => {
    renderRequests();
    fireEvent.click(screen.getByRole("button", { name: "Quick One-off Payment" }));

    expect(screen.getByText("Available Lifetime Budget").parentElement).toHaveTextContent("35,000");

    fireEvent.change(screen.getByLabelText("Description"), { target: { value: "Laptop purchase" } });
    fireEvent.change(screen.getByLabelText("Amount"), { target: { value: "1200" } });
    expect(screen.getByText("Reservation amount").parentElement).toHaveTextContent("1,200");
    fireEvent.change(screen.getByLabelText("Purpose"), { target: { value: "Replace damaged equipment" } });
    fireEvent.change(screen.getByLabelText("Due date"), { target: { value: "2026-12-15" } });
    fireEvent.change(screen.getByRole("combobox", { name: "Annual budget for this Fiscal Year" }), { target: { value: "annual-1" } });
    fireEvent.change(screen.getByRole("combobox", { name: "Budget Line" }), { target: { value: "line-1" } });
    fireEvent.click(screen.getByRole("button", { name: "Save Draft" }));

    await waitFor(() => expect(fixtures.createQuickOneOff).toHaveBeenCalledWith(expect.objectContaining({
      description: "Laptop purchase",
      amount: 1200,
      purpose: "Replace damaged equipment",
      dueDateUtc: "2026-12-15T00:00:00Z",
      projectAnnualBudgetId: "annual-1",
      projectAnnualBudgetLineId: "line-1",
      submitForApproval: false,
      idempotencyKey: expect.any(String),
    })));
  });

  it("sends funding and approval configuration with the atomic submit action", async () => {
    renderRequests();
    fireEvent.click(screen.getByRole("button", { name: "Quick One-off Payment" }));
    fireEvent.change(screen.getByLabelText("Description"), { target: { value: "Laptop purchase" } });
    fireEvent.change(screen.getByLabelText("Amount"), { target: { value: "1200" } });
    fireEvent.change(screen.getByLabelText("Purpose"), { target: { value: "Replace damaged equipment" } });
    fireEvent.change(screen.getByLabelText("Due date"), { target: { value: "2026-12-15" } });
    fireEvent.change(screen.getByRole("combobox", { name: "Annual budget for this Fiscal Year" }), { target: { value: "annual-1" } });
    fireEvent.change(screen.getByRole("combobox", { name: "Budget Line" }), { target: { value: "line-1" } });
    fireEvent.click(screen.getByLabelText("Amina Approver"));
    fireEvent.click(screen.getByRole("button", { name: "Submit for Approval" }));

    await waitFor(() => expect(fixtures.createQuickOneOff).toHaveBeenCalledWith(expect.objectContaining({
      submitForApproval: true,
      approvalMode: "Sequential",
      approvers: [{ userId: "approver-1", position: 1, requirement: "Mandatory" }],
    })));
  });

  it("renders the Quick One-off action in Arabic and sets RTL", () => {
    renderRequests("ar");
    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.getByRole("button", { name: "طلب دفع سريع لمرة واحدة" })).toBeInTheDocument();
  });
  it("hides Quick One-off when Planned Payment creation permission is missing", () => {
    renderRequests("en", false);
    expect(screen.queryByRole("button", { name: "Quick One-off Payment" })).not.toBeInTheDocument();
  });});
