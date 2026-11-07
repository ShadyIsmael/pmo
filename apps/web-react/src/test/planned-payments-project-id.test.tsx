import { fireEvent, render, screen } from "@testing-library/react";
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
}));

vi.mock("../features/projects/api/projects", () => ({ usePaymentProjectLookup: fixtures.usePaymentProjectLookup }));
vi.mock("../features/projects/api/planningFull", () => ({ usePlanningBootstrapFull: fixtures.usePlanning }));
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

function renderPayments(projectId: string) {
  window.localStorage.setItem("nexus-locale", "en");
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

describe("planned payment project identifiers", () => {
  beforeEach(() => {
    fixtures.permissions = ["PaymentObligation.View", "PaymentObligation.Create", "PaymentObligation.Edit", "PaymentObligation.Delete", "PaymentObligationSettlement.Record"];
    fixtures.usePaymentProjectLookup.mockReset().mockReturnValue(queryResult([
      { id: "internal-project-id", publicId: "public-project-id", name: "Project", code: "PRJ-1", currency: "EGP" },
    ]));
    fixtures.usePlanning.mockReset().mockReturnValue(queryResult({ activities: [] }));
    fixtures.usePaymentObligations.mockReset().mockReturnValue(queryResult({ items: [], totalCount: 0 }));
    fixtures.usePaymentSettlements.mockReset().mockReturnValue(queryResult({ items: [], totalCount: 0 }));
    fixtures.useMutations.mockReset().mockReturnValue({
      create: { mutateAsync: vi.fn(), isPending: false },
      update: { mutateAsync: vi.fn(), isPending: false },
      delete: { mutateAsync: vi.fn(), isPending: false },
      settle: { mutateAsync: vi.fn(), isPending: false },
    });
  });

  it("uses the public project ID for project-scoped reads and planning", () => {
    renderPayments("PUBLIC-PROJECT-ID");

    expect(fixtures.usePaymentObligations).toHaveBeenCalledWith(
      { page: 1, pageSize: 20, projectId: "public-project-id" },
      true,
    );
    expect(fixtures.usePlanning).toHaveBeenCalledWith("public-project-id");
  });

  it("requires the selected project currency before creating a payment", () => {
    fixtures.usePaymentProjectLookup.mockReturnValue(queryResult([{ id: "internal-project-id", publicId: "public-project-id", name: "Project", code: "PRJ-1" }]));
    renderPayments("PUBLIC-PROJECT-ID");
    fireEvent.click(screen.getByRole("button", { name: "Add planned payment" }));
    expect(screen.getByText("A valid currency from the selected project is unavailable.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save" })).toBeDisabled();
  });

  it("does not issue an unfiltered payment request while a project ID is unresolved", () => {
    renderPayments("unknown-project-id");

    expect(fixtures.usePaymentObligations).toHaveBeenCalledWith({ page: 1, pageSize: 20 }, false);
  });

  it("loads project options for PaymentObligation.Create without Project.View", () => {
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
});
