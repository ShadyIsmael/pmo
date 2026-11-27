import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../api/client";
import { I18nProvider } from "../i18n/messages";
import { PlannedPaymentSection } from "../features/projects/components/PlannedPaymentSection";

const fixtures = vi.hoisted(() => ({
  budget: vi.fn(),
  plans: vi.fn(),
  milestones: vi.fn(),
  register: vi.fn(),
  replace: vi.fn(),
  replacementPreview: vi.fn(),
  cancelRequest: vi.fn(),
  createReversal: vi.fn(),
  reversalApprovers: vi.fn(),
}));

vi.mock("../features/projects/api/planned-payments", () => ({
  usePlannedPaymentBudget: fixtures.budget,
  useProjectPlannedPayments: fixtures.plans,
  usePlannedPaymentMilestones: fixtures.milestones,
  useRegisterPlannedPayment: fixtures.register,
  useReplacePlannedPayment: fixtures.replace,
  usePlannedPaymentReplacementPreview: fixtures.replacementPreview,
}));

vi.mock("../features/projects/api/project-financials", () => ({
  useCancelProjectPaymentRequest: fixtures.cancelRequest,
  useCreateProjectPaymentReversal: fixtures.createReversal,
  usePaymentRequestApprovers: fixtures.reversalApprovers,
}));

const registeredPlan = {
  id: "plan-1",
  projectId: "project-42",
  reference: "PP-20261122-0001",
  description: "Site works",
  currency: "EGP",
  totalAmount: 1500,
  state: "Registered",
  isLocked: false,
  reservedAmount: 1500,
  releasedAmount: 0,
  outstandingReservedAmount: 1500,
  settledAmount: 0,
  registeredAtUtc: "2026-11-22T10:00:00Z",
  firstRequestAtUtc: null,
  installments: [{
    id: "installment-1",
    sequence: 1,
    amount: 1500,
    dueDateUtc: "2026-12-01T00:00:00Z",
    planActivityId: null,
    planActivityName: null,
    requestedAmount: 0,
    settledAmount: 0,
    remainingRequestableAmount: 1500,
    rowVersion: "AAAAAAAAAAA=",
  }],
  rowVersion: "AAAAAAAAAAA=",
  canEdit: true,
};

function queryResult(data: unknown, overrides: Record<string, unknown> = {}) {
  return { data, isLoading: false, isError: false, refetch: vi.fn(), ...overrides };
}

function renderSection(locale = "en", canRegister = true, canReplace = true, onCreateRequest = vi.fn()) {
  window.localStorage.setItem("nexus-locale", locale);
  return render(
    <I18nProvider>
      <PlannedPaymentSection projectId="project-42" projectCurrency="EGP" canRegister={canRegister} canReplace={canReplace}
        canCreateRequest onCreateRequest={onCreateRequest} />
    </I18nProvider>,
  );
}

describe("project Financial Tab Planned Payments", () => {
  beforeEach(() => {
    fixtures.budget.mockReset().mockReturnValue(queryResult({
      projectId: "project-42",
      currency: "EGP",
      lifetimeCeiling: 10000,
      lifetimeConsumed: 1000,
      lifetimeReserved: 1500,
      availableLifetimeBudget: 7500,
      canRegister: true,
    }));
    fixtures.plans.mockReset().mockReturnValue(queryResult({ items: [registeredPlan], page: 1, pageSize: 5, totalCount: 1 }));
    fixtures.milestones.mockReset().mockReturnValue(queryResult([{ id: "milestone-1", name: "Foundation complete", endDate: null }]));
    fixtures.register.mockReset().mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(registeredPlan), isPending: false });
    fixtures.replace.mockReset().mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(registeredPlan), isPending: false });
    fixtures.replacementPreview.mockReset().mockReturnValue(queryResult(undefined, { isSuccess: false, isFetching: false }));
    fixtures.cancelRequest.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    fixtures.createReversal.mockReset().mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    fixtures.reversalApprovers.mockReset().mockReturnValue(queryResult([]));
  });

  it("renders authoritative lifetime budget and plan data without hardcoded financial values", () => {
    renderSection();

    expect(screen.getByText("Approved lifetime ceiling")).toBeInTheDocument();
    expect(screen.getByText((content) => content.includes("10,000"))).toBeInTheDocument();
    expect(screen.getByText("PP-20261122-0001")).toBeInTheDocument();
    expect(screen.getByText("Site works")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Add planned payment" })).toBeEnabled();
  });

  it("registers installments with a stable idempotency key and UTC due dates", async () => {
    const mutateAsync = vi.fn().mockResolvedValue(registeredPlan);
    fixtures.register.mockReturnValue({ mutateAsync, isPending: false });
    renderSection();

    fireEvent.click(screen.getByRole("button", { name: "Add planned payment" }));
    const dialog = screen.getByRole("dialog", { name: "Add planned payment" });
    fireEvent.change(within(dialog).getByLabelText("Description *"), { target: { value: "Site works" } });
    fireEvent.change(within(dialog).getByLabelText("Amount *"), { target: { value: "1500.0000" } });
    fireEvent.change(within(dialog).getByLabelText("Due date *"), { target: { value: "2026-12-01" } });
    fireEvent.change(within(dialog).getByLabelText("Milestone"), { target: { value: "milestone-1" } });
    fireEvent.submit(within(dialog).getByRole("button", { name: "Register planned payment" }).closest("form")!);

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1));
    expect(mutateAsync).toHaveBeenCalledWith(expect.objectContaining({
      description: "Site works",
      installments: [expect.objectContaining({
        amount: 1500,
        dueDateUtc: "2026-12-01T00:00:00Z",
        planActivityId: "milestone-1",
      })],
      idempotencyKey: expect.any(String),
    }));
  });

  it("starts a Payment Request from the selected installment with its persisted remaining amount", async () => {
    const onCreateRequest = vi.fn();
    renderSection("en", true, true, onCreateRequest);

    fireEvent.click(screen.getByRole("button", { name: "View details" }));
    await waitFor(() => expect(screen.getByRole("dialog")).toBeInTheDocument());
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "From existing Planned Payment" }));

    expect(onCreateRequest).toHaveBeenCalledWith(expect.objectContaining({
      installmentId: "installment-1",
      remainingRequestableAmount: 1500,
      currency: "EGP",
      planReference: "PP-20261122-0001",
    }));
  });

  it("localizes the insufficient lifetime budget response", async () => {
    const mutateAsync = vi.fn().mockRejectedValue(new ApiError(409, "Registration requires more budget", "INSUFFICIENT_LIFETIME_BUDGET"));
    fixtures.register.mockReturnValue({ mutateAsync, isPending: false });
    renderSection();

    fireEvent.click(screen.getByRole("button", { name: "Add planned payment" }));
    const dialog = screen.getByRole("dialog", { name: "Add planned payment" });
    fireEvent.change(within(dialog).getByLabelText("Description *"), { target: { value: "Site works" } });
    fireEvent.change(within(dialog).getByLabelText("Amount *"), { target: { value: "1500" } });
    fireEvent.change(within(dialog).getByLabelText("Due date *"), { target: { value: "2026-12-01" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Register planned payment" }));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("The installment total exceeds the available approved lifetime budget."));
  });

  it("shows the financial lock in plan details", async () => {
    fixtures.plans.mockReturnValue(queryResult({ items: [{ ...registeredPlan, state: "Locked", isLocked: true }], totalCount: 1 }));
    renderSection();

    fireEvent.click(screen.getByRole("button", { name: "View details" }));
    await waitFor(() => expect(screen.getByRole("dialog")).toBeInTheDocument());
    expect(screen.getByText(/financially locked/i)).toBeInTheDocument();
    expect(screen.queryByText("Foundation")).not.toBeInTheDocument();
  });

  it("provides error and loading states for authoritative queries", () => {
    fixtures.plans.mockReturnValue(queryResult(null, { isLoading: true }));
    const { unmount } = renderSection();
    expect(screen.getByText("Loading...")).toBeInTheDocument();
    unmount();

    fixtures.plans.mockReturnValue(queryResult(null, { isError: true }));
    renderSection();
    expect(screen.getByRole("alert")).toHaveTextContent("Planned payments could not be loaded.");
    expect(screen.getByRole("button", { name: "Retry" })).toBeEnabled();
  });

  it("renders Arabic labels in an RTL document", () => {
    renderSection("ar");

    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.getByText("المدفوعات المخططة")).toBeInTheDocument();
    expect(screen.getByText("إضافة دفعة مخططة")).toBeInTheDocument();
  });
});
