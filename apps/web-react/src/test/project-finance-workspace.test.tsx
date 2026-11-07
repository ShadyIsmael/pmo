import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectFinanceTab } from "../features/projects/ProjectFinanceTab";

const fixtures = vi.hoisted(() => ({
  permissions: ["Project.View", "ProjectFinance.View", "ProjectFinancialFoundation.View", "ProjectAnnualBudget.View", "ProjectPaymentRequest.View", "ProjectSettlement.View", "ProjectAnnualBudgetClosing.View", "ProjectFinancialAuditTrail.View", "PaymentObligation.View"],
  useAnnualBudgets: vi.fn(),
}));

vi.mock("../features/projects/api/project-annual-budgets", () => ({
  useProjectAnnualBudgets: fixtures.useAnnualBudgets,
}));

vi.mock("../features/projects/ProjectFinanceOverview", () => ({
  ProjectFinanceOverview: ({ active, selectedFiscalYear, canViewFoundation, canViewAnnualBudgets, canViewPaymentRequests, canViewAnnualBudgetClosing }: { active: boolean; selectedFiscalYear: number | null; canViewFoundation: boolean; canViewAnnualBudgets: boolean; canViewPaymentRequests: boolean; canViewAnnualBudgetClosing: boolean }) =>
    <div data-testid="finance-overview" data-active={String(active)} data-year={selectedFiscalYear ?? ""} data-foundation={String(canViewFoundation)} data-annual-budgets={String(canViewAnnualBudgets)} data-payment-requests={String(canViewPaymentRequests)} data-closing={String(canViewAnnualBudgetClosing)} />,
}));

vi.mock("../features/projects/ProjectFinancialWorkspace", () => ({
  ProjectFinancialWorkspace: ({ activeSection, selectedFiscalYear, canCreateAnnualBudget, canApprove, canPrepareClosing, canApproveClosing, canCarryForward }: { activeSection: string; selectedFiscalYear: number | null; canCreateAnnualBudget: boolean; canApprove: boolean; canPrepareClosing: boolean; canApproveClosing: boolean; canCarryForward: boolean }) =>
    <div data-testid="finance-workspace" data-section={activeSection} data-year={selectedFiscalYear ?? ""} data-can-create-budget={String(canCreateAnnualBudget)} data-can-approve-budget={String(canApprove)} data-can-prepare-closing={String(canPrepareClosing)} data-can-approve-closing={String(canApproveClosing)} data-can-carry-forward={String(canCarryForward)} />,
}));

vi.mock("../features/projects/ProjectPaymentRequests", () => ({
  ProjectPaymentRequests: ({ section, active, canViewRequests, canCreateRequests, canApproveRequests, canRecordSettlements, canCreateReversals, canApproveReversals }: { section: string; active: boolean; canViewRequests: boolean; canCreateRequests: boolean; canApproveRequests: boolean; canRecordSettlements: boolean; canCreateReversals: boolean; canApproveReversals: boolean }) =>
    <div data-testid="payment-requests" data-section={section} data-active={String(active)} data-can-view={String(canViewRequests)} data-can-create={String(canCreateRequests)} data-can-approve={String(canApproveRequests)} data-can-record-settlement={String(canRecordSettlements)} data-can-create-reversal={String(canCreateReversals)} data-can-approve-reversal={String(canApproveReversals)} />,
}));

vi.mock("../features/finance/PlannedPaymentsPage", () => ({
  PlannedPaymentsPage: ({ active }: { active: boolean }) =>
    <div data-testid="planned-payments" data-active={String(active)} />,
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { permissions: fixtures.permissions } }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ canMutate: true, effectiveReadOnly: false }),
}));

function ProjectOutlet() {
  return <Outlet context={{
    projectId: "project-42",
    project: { id: "project-42", publicId: "P-42", name: "Project Atlas", code: "ATLAS", currency: "USD" },
  }} />;
}

function renderFinance(locale = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/projects/project-42/financials"]}>
        <Routes>
          <Route path="/projects/:id" element={<ProjectOutlet />}>
            <Route path="financials" element={<ProjectFinanceTab />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("Project Finance workspace navigation", () => {
  beforeEach(() => {
    fixtures.permissions = ["Project.View", "ProjectFinance.View", "ProjectFinancialFoundation.View", "ProjectAnnualBudget.View", "ProjectAnnualBudget.Create", "ProjectPaymentRequest.View", "ProjectSettlement.View", "ProjectAnnualBudgetClosing.View", "ProjectFinancialAuditTrail.View", "PaymentObligation.View"];
    fixtures.useAnnualBudgets.mockReturnValue({
      data: {
        currentFiscalYear: 2026,
        items: [
          { fiscalYear: 2026, fiscalYearLabel: "FY 2026" },
          { fiscalYear: 2025, fiscalYearLabel: "FY 2025" },
        ],
      },
      isLoading: false,
    });
  });

  it("keeps the six approved workspaces inside the Project Finance navigation", () => {
    renderFinance();

    expect(screen.getByRole("heading", { name: "Project Finance" })).toBeInTheDocument();
    expect(screen.getByText("Project: Project Atlas (ATLAS)")).toBeInTheDocument();
    expect(screen.getAllByRole("navigation")).toHaveLength(1);

    const sections = [
      ["Overview", "overview"],
      ["Annual Budgets", "annualBudgets"],
      ["Payment Requests", "paymentRequests"],
      ["Settlements & Reversals", "settlements"],
      ["Fiscal Year Closing", "closing"],
      ["Financial Audit Trail", "auditTrail"],
    ] as const;

    for (const [label, section] of sections) {
      const navigationButton = screen.getByRole("button", { name: label });
      fireEvent.click(navigationButton);
      expect(navigationButton).toHaveAttribute("aria-current", "page");
      expect(screen.getByTestId("finance-workspace")).toHaveAttribute("data-section", section);
    }
  });

  it("keeps the selected fiscal year while navigating and scopes planned payments to Settlements", () => {
    renderFinance();

    fireEvent.change(screen.getByLabelText("Selected fiscal year"), { target: { value: "2025" } });
    fireEvent.click(screen.getByRole("button", { name: "Financial Audit Trail" }));
    expect(screen.getByTestId("finance-workspace")).toHaveAttribute("data-year", "2025");

    fireEvent.click(screen.getByRole("button", { name: "Payment Requests" }));
    expect(screen.getByTestId("payment-requests")).toHaveAttribute("data-section", "requests");
    expect(screen.getByTestId("payment-requests")).toHaveAttribute("data-active", "true");
    expect(screen.getByTestId("planned-payments")).not.toBeVisible();

    fireEvent.click(screen.getByRole("button", { name: "Settlements & Reversals" }));
    expect(screen.getByTestId("payment-requests")).toHaveAttribute("data-section", "settlements");
    expect(screen.getByTestId("planned-payments")).toHaveAttribute("data-active", "true");
  });

  it("does not expose Finance content without the existing read or write permission", () => {
    fixtures.permissions = [];
    renderFinance();

    expect(screen.getByText("You do not have permission to view project financials.")).toBeInTheDocument();
    expect(screen.queryByTestId("finance-workspace")).not.toBeInTheDocument();
    expect(screen.queryByTestId("payment-requests")).not.toBeInTheDocument();
    expect(screen.queryByTestId("planned-payments")).not.toBeInTheDocument();
    expect(fixtures.useAnnualBudgets).toHaveBeenCalledWith("project-42", false);
  });

  it("opens the Finance shell without fetching or displaying financial data when no section View is granted", () => {
    fixtures.permissions = ["ProjectFinance.View"];
    renderFinance();

    expect(screen.getByText("Access to the Finance shell is enabled. Ask your administrator for View access to a specific Finance workspace.")).toBeInTheDocument();
    expect(screen.queryByTestId("finance-workspace")).not.toBeInTheDocument();
    expect(screen.queryByTestId("payment-requests")).not.toBeInTheDocument();
    expect(screen.queryByTestId("planned-payments")).not.toBeInTheDocument();
    expect(fixtures.useAnnualBudgets).toHaveBeenCalledWith("project-42", false);
  });

  it("shows the financial overview with its dedicated read permission and gates other overview queries", () => {
    fixtures.permissions = ["ProjectFinancialFoundation.View"];
    renderFinance();

    expect(screen.getByRole("button", { name: "Overview" })).toBeInTheDocument();
    expect(screen.getByTestId("finance-overview")).toHaveAttribute("data-foundation", "true");
    expect(screen.getByTestId("finance-overview")).toHaveAttribute("data-annual-budgets", "false");
    expect(screen.getByTestId("finance-overview")).toHaveAttribute("data-payment-requests", "false");
    expect(screen.getByTestId("finance-overview")).toHaveAttribute("data-closing", "false");
    expect(screen.queryByRole("button", { name: "Annual Budgets" })).not.toBeInTheDocument();
    expect(fixtures.useAnnualBudgets).toHaveBeenCalledWith("project-42", false);
  });

  it("explicitly denies the retired ProjectAnnualBudget.Manage alias", () => {
    fixtures.permissions = ["ProjectAnnualBudget.Manage"];
    renderFinance();

    expect(screen.getByText("You do not have permission to view project financials.")).toBeInTheDocument();
    expect(screen.queryByTestId("finance-workspace")).not.toBeInTheDocument();
    expect(screen.queryByTestId("payment-requests")).not.toBeInTheDocument();
  });

  it("allows a canonical budget draft action without approval or closing authority", () => {
    fixtures.permissions = ["ProjectAnnualBudget.View", "ProjectAnnualBudget.Create"];
    renderFinance();

    const workspace = screen.getByTestId("finance-workspace");
    expect(workspace).toHaveAttribute("data-section", "annualBudgets");
    expect(workspace).toHaveAttribute("data-can-create-budget", "true");
    expect(workspace).toHaveAttribute("data-can-approve-budget", "false");
    expect(workspace).toHaveAttribute("data-can-approve-closing", "false");
    expect(workspace).toHaveAttribute("data-can-carry-forward", "false");
    expect(screen.queryByRole("button", { name: "Payment Requests" })).not.toBeInTheDocument();
  });

  it("explicitly denies the retired ProjectPaymentRequest.Manage alias", () => {
    fixtures.permissions = ["ProjectPaymentRequest.Manage"];
    renderFinance();

    expect(screen.getByText("You do not have permission to view project financials.")).toBeInTheDocument();
    expect(screen.queryByTestId("finance-workspace")).not.toBeInTheDocument();
    expect(screen.queryByTestId("payment-requests")).not.toBeInTheDocument();
    expect(screen.queryByTestId("planned-payments")).not.toBeInTheDocument();
  });

  it("does not let Settlement View expose planned payment obligation records", () => {
    fixtures.permissions = ["ProjectSettlement.View"];
    renderFinance();

    expect(screen.getByTestId("finance-workspace")).toHaveAttribute("data-section", "settlements");
    expect(screen.queryByTestId("planned-payments")).not.toBeInTheDocument();
    expect(screen.queryByTestId("payment-requests")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Payment Requests" })).not.toBeInTheDocument();
  });

  it("shows payment obligations only with their canonical View permission", () => {
    fixtures.permissions = ["PaymentObligation.View"];
    renderFinance();

    expect(screen.getByTestId("finance-workspace")).toHaveAttribute("data-section", "settlements");
    expect(screen.getByTestId("planned-payments")).toHaveAttribute("data-active", "true");
    expect(screen.queryByTestId("payment-requests")).not.toBeInTheDocument();
  });

  it("renders Arabic labels with document RTL direction", () => {
    renderFinance("ar");

    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.getByRole("navigation").getAttribute("aria-label")).toBeTruthy();
    expect(screen.getByText("السنة المالية المحددة")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "نظرة عامة" })).toBeInTheDocument();
  });
});
