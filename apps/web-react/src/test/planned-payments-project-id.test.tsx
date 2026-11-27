import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { PlannedPaymentsPage } from "../features/finance/PlannedPaymentsPage";

const fixtures = vi.hoisted(() => ({
  permissions: ["PaymentObligation.View", "PaymentObligation.Create", "PaymentObligation.Edit", "PaymentObligation.Delete", "PaymentObligationSettlement.Record"] as string[],
  usePaymentProjectLookup: vi.fn(),
  usePlanning: vi.fn(),
  usePaymentObligations: vi.fn(),
  usePaymentSettlements: vi.fn(),
  useMutations: vi.fn(),
  useFinancialFoundation: vi.fn(),
  useProjectPaymentRequests: vi.fn(),
}));

vi.mock("../features/projects/api/projects", () => ({ usePaymentProjectLookup: fixtures.usePaymentProjectLookup }));
vi.mock("../features/projects/api/planningFull", () => ({ usePlanningBootstrapFull: fixtures.usePlanning }));
vi.mock("../features/projects/api/project-financials", () => ({
  useProjectFinancialFoundation: fixtures.useFinancialFoundation,
  useProjectPaymentRequests: fixtures.useProjectPaymentRequests,
}));
vi.mock("../features/finance/api/finance", () => ({
  usePaymentObligations: fixtures.usePaymentObligations,
  usePaymentSettlements: fixtures.usePaymentSettlements,
  usePaymentObligationMutations: fixtures.useMutations,
}));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, effectiveReadOnly: false }) }));
vi.mock("@components/ui/ConfirmProvider", async (importOriginal) => ({
  ...await importOriginal<typeof import("@components/ui/ConfirmProvider")>(),
  useConfirm: () => vi.fn(),
}));

function queryResult(data: unknown) {
  return { data, isLoading: false, isError: false, refetch: vi.fn() };
}

function renderPayments(projectId: string, locale = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[`/payments?projectId=${projectId}`]}>
        <Routes>
          <Route path="/payments" element={<PlannedPaymentsPage />} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("project-scoped planned payments", () => {
  beforeEach(() => {
    fixtures.permissions = ["PaymentObligation.View", "PaymentObligation.Create", "PaymentObligation.Edit", "PaymentObligation.Delete", "PaymentObligationSettlement.Record"];
    fixtures.usePaymentProjectLookup.mockReset().mockReturnValue(queryResult([
      { id: "internal-project-id", publicId: "public-project-id", name: "Project", code: "PRJ-1", currency: "EGP" },
    ]));
    fixtures.usePlanning.mockReset().mockReturnValue(queryResult({ activities: [] }));
    fixtures.useFinancialFoundation.mockReset().mockReturnValue(queryResult({ approvedBudget: 100000, projectCurrency: "EGP" }));
    fixtures.useProjectPaymentRequests.mockReset().mockReturnValue(queryResult({ items: [] }));
    fixtures.usePaymentObligations.mockReset().mockReturnValue(queryResult({ items: [], totalCount: 0 }));
    fixtures.usePaymentSettlements.mockReset().mockReturnValue(queryResult({ items: [], totalCount: 0 }));
    fixtures.useMutations.mockReset().mockReturnValue({
      create: { mutateAsync: vi.fn(), isPending: false },
      update: { mutateAsync: vi.fn(), isPending: false },
      delete: { mutateAsync: vi.fn(), isPending: false },
      settle: { mutateAsync: vi.fn(), isPending: false },
    });
  });

  it("scopes reads and planning to the current project's public id", () => {
    renderPayments("PUBLIC-PROJECT-ID");

    expect(fixtures.usePaymentObligations).toHaveBeenCalledWith(
      { page: 1, pageSize: 20, projectId: "public-project-id" },
      true,
    );
    expect(fixtures.usePlanning).toHaveBeenCalledWith("public-project-id");
  });

  it("does not expose another project's records when several are in scope", () => {
    fixtures.usePaymentProjectLookup.mockReturnValue(queryResult([
      { id: "internal-1", publicId: "public-1", name: "Alpha", code: "A-1", currency: "EGP" },
      { id: "internal-2", publicId: "public-2", name: "Beta", code: "B-2", currency: "USD" },
    ]));
    fixtures.usePaymentObligations.mockReturnValue(queryResult({
      items: [
        {
          id: "payment-1", projectId: "internal-1", projectName: "Alpha", paymentReference: "PAY-1", purpose: "Own",
          amount: 100, settledAmount: 0, outstandingAmount: 100, dueDate: "2026-12-01T00:00:00Z",
          settlementProgressStatus: "None", dueDateStatus: "Upcoming", isOverdue: false, settlements: [],
        },
        {
          id: "payment-2", projectId: "internal-2", projectName: "Beta", paymentReference: "PAY-2", purpose: "Foreign",
          amount: 200, settledAmount: 0, outstandingAmount: 200, dueDate: "2026-12-01T00:00:00Z",
          settlementProgressStatus: "None", dueDateStatus: "Upcoming", isOverdue: false, settlements: [],
        },
      ],
      totalCount: 2,
    }));
    renderPayments("public-1");

    expect(fixtures.usePaymentObligations).toHaveBeenCalledWith(
      { page: 1, pageSize: 20, projectId: "public-1" },
      true,
    );
    expect(fixtures.usePlanning).toHaveBeenCalledWith("public-1");
    expect(screen.getByText("Project: Alpha (A-1)")).toBeInTheDocument();
    expect(screen.getByText("PAY-1")).toBeInTheDocument();
    expect(screen.queryByText("PAY-2")).not.toBeInTheDocument();
    expect(screen.queryByText(/Beta/)).not.toBeInTheDocument();
  });

  it("renders the current project name with no project dropdown or switching control", () => {
    renderPayments("internal-project-id");

    expect(screen.getByText("Project: Project (PRJ-1)")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Project" })).not.toBeInTheDocument();
    expect(screen.queryByText("All projects")).not.toBeInTheDocument();
    expect(screen.queryByText("Select a project")).not.toBeInTheDocument();
    expect(screen.queryByRole("option", { name: /Project \(PRJ-1\)/ })).not.toBeInTheDocument();
  });

  it("does not show a project field in the create form and assigns the current internal id", async () => {
    const create = vi.fn().mockResolvedValue({});
    fixtures.useMutations.mockReturnValue({
      create: { mutateAsync: create, isPending: false },
      update: { mutateAsync: vi.fn(), isPending: false },
      delete: { mutateAsync: vi.fn(), isPending: false },
      settle: { mutateAsync: vi.fn(), isPending: false },
    });
    renderPayments("public-project-id");

    fireEvent.click(screen.getByRole("button", { name: "Add planned payment" }));
    const dialog = screen.getByRole("dialog", { name: "Add planned payment" });
    expect(within(dialog).queryByRole("button", { name: "Project" })).not.toBeInTheDocument();
    expect(within(dialog).queryByText("Select a project")).not.toBeInTheDocument();
    expect(within(dialog).getByText("Project (PRJ-1)")).toBeInTheDocument();

    fireEvent.change(dialog.querySelector('input[maxlength="100"]')!, { target: { value: "PAY-1" } });
    fireEvent.change(dialog.querySelector('input[maxlength="250"]')!, { target: { value: "Milestone" } });
    fireEvent.change(dialog.querySelector('input[type="number"]')!, { target: { value: "100" } });
    fireEvent.change(dialog.querySelector('input[placeholder="dd/mm/yyyy"]')!, { target: { value: "01/12/2026" } });
    fireEvent.submit(dialog.querySelector("form")!);

    await waitFor(() => expect(create).toHaveBeenCalledWith(expect.objectContaining({
      projectId: "internal-project-id",
      currency: "EGP",
    })));
  });

  it("requires a valid project currency before creating a payment", () => {
    fixtures.usePaymentProjectLookup.mockReturnValue(queryResult([{ id: "internal-project-id", publicId: "public-project-id", name: "Project", code: "PRJ-1" }]));
    renderPayments("public-project-id");
    fireEvent.click(screen.getByRole("button", { name: "Add planned payment" }));
    expect(screen.getByText("A valid currency from the selected project is unavailable.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save" })).toBeDisabled();
  });

  it("does not issue an unfiltered payment request while a project is unresolved", () => {
    renderPayments("unknown-project-id");

    expect(fixtures.usePaymentObligations).toHaveBeenCalledWith({ page: 1, pageSize: 20 }, false);
    expect(screen.getByText("The project is unavailable, or you do not have access to it in your current scope.")).toBeInTheDocument();
    expect(screen.queryByText("unknown-project-id")).not.toBeInTheDocument();
  });

  it("loads project context for PaymentObligation.Create without Project.View", () => {
    fixtures.permissions = ["PaymentObligation.Create"];
    renderPayments("public-project-id");

    expect(fixtures.usePaymentProjectLookup).toHaveBeenCalledWith(true);
    expect(screen.getByRole("button", { name: "Add planned payment" })).toBeEnabled();
    expect(screen.getByText("You can create payment obligations, but you do not have permission to view existing payment records.")).toBeInTheDocument();
    expect(fixtures.usePaymentObligations).toHaveBeenCalledWith(
      { page: 1, pageSize: 20, projectId: "public-project-id" },
      false,
    );
  });

  it("keeps the project context localized and RTL in Arabic", async () => {
    renderPayments("public-project-id", "ar");

    await waitFor(() => expect(document.documentElement.dir).toBe("rtl"));
    expect(screen.getByText("المشروع: Project (PRJ-1)")).toBeInTheDocument();
  });
});
