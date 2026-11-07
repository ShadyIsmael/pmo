import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectFinanceOverview } from "../features/projects/ProjectFinanceOverview";

const mocks = vi.hoisted(() => ({
  annualBudgets: vi.fn(),
  annualBudgetClosings: vi.fn(),
  financialFoundation: vi.fn(),
  paymentRequests: vi.fn(),
}));

vi.mock("../features/projects/api/project-annual-budgets", () => ({
  useProjectAnnualBudgets: mocks.annualBudgets,
}));
vi.mock("../features/projects/api/project-financials", () => ({
  useProjectAnnualBudgetClosings: mocks.annualBudgetClosings,
  useProjectFinancialFoundation: mocks.financialFoundation,
  useProjectPaymentRequests: mocks.paymentRequests,
}));
vi.mock("recharts", () => ({
  Bar: () => null,
  BarChart: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  CartesianGrid: () => null,
  ResponsiveContainer: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

describe("Project Finance Overview read permissions", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.annualBudgets.mockReset().mockReturnValue({
      data: {
        currentFiscalYear: 2026,
        items: [{ fiscalYear: 2026, status: "Approved", totalAnnualBudget: 987654, currency: "USD", isClosed: false }],
        totalProjectFinancialCeiling: 987654,
        projectCurrency: "USD",
        totalApprovedNewAllocations: 987654,
        pendingSubmittedAllocations: 0,
        remainingUnallocatedCeiling: 987654,
      },
      isLoading: false,
      isError: false,
      error: null,
    });
    mocks.annualBudgetClosings.mockReset().mockReturnValue({
      data: { items: [{ fiscalYear: 2026, status: "Closed", actualSettlementsInFiscalYear: 987654, currency: "USD" }] },
      isLoading: false,
      isError: false,
      error: null,
    });
    mocks.financialFoundation.mockReset().mockReturnValue({
      data: { approvedBudget: 123456, actualPaid: 234567, outstandingPaymentBalance: 345678, projectCurrency: "USD" },
      isLoading: false,
      isError: false,
      error: null,
    });
    mocks.paymentRequests.mockReset().mockReturnValue({
      data: { total: 987654, items: [], budgetAvailability: { fiscalYear: 2026, availabilityStatus: "Ready", availableAmount: 987654, currency: "USD" } },
      isLoading: false,
      isError: false,
      error: null,
    });
  });

  it("fetches and renders only the financial foundation when only its View permission is granted", () => {
    render(<I18nProvider><ProjectFinanceOverview
      projectId="project-42"
      selectedFiscalYear={2026}
      active
      canViewFoundation
      canViewAnnualBudgets={false}
      canViewPaymentRequests={false}
      canViewAnnualBudgetClosing={false}
      canManage={false}
      onOpenAnnualBudgets={() => undefined}
    /></I18nProvider>);

    expect(mocks.financialFoundation).toHaveBeenCalledWith("project-42", true);
    expect(mocks.annualBudgets).toHaveBeenCalledWith("project-42", false);
    expect(mocks.paymentRequests).toHaveBeenNthCalledWith(1, "project-42", false);
    expect(mocks.paymentRequests).toHaveBeenNthCalledWith(2, "project-42", false, "Pending", 100);
    expect(mocks.annualBudgetClosings).toHaveBeenCalledWith("project-42", false);
    expect(screen.getByText("Total Project Financial Ceiling")).toBeInTheDocument();
    expect(screen.getByText("Actual Paid to Date")).toBeInTheDocument();
    expect(screen.getByText("Outstanding Payment Obligations")).toBeInTheDocument();
    expect(screen.getByText("$123,456")).toBeInTheDocument();
    expect(screen.getByText("$234,567")).toBeInTheDocument();
    expect(screen.getByText("$345,678")).toBeInTheDocument();
    expect(screen.queryByText("$987,654")).not.toBeInTheDocument();
    expect(screen.queryByText("Approved New Annual Allocations")).not.toBeInTheDocument();
    expect(screen.queryByText("Approved Annual Budget")).not.toBeInTheDocument();
    expect(screen.queryByText("Available Budget")).not.toBeInTheDocument();
    expect(screen.queryByText("Pending Payment Requests")).not.toBeInTheDocument();
    expect(screen.queryByText("Actual Settlements")).not.toBeInTheDocument();
  });
});
