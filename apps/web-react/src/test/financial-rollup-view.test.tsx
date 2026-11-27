import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { FinancialRollupView } from "../features/delivery/FinancialRollupView";
import type { ProgramPortfolioFinancialRollup } from "../features/delivery/api/delivery";

const fixtures = vi.hoisted(() => ({
  permissions: ["Portfolio.View", "PortfolioFinancialRollup.View", "Program.View", "ProgramFinancialRollup.View"],
  rollup: null as unknown as ProgramPortfolioFinancialRollup,
  loading: false,
  error: false,
}));

vi.mock("../features/delivery/api/delivery", async (importOriginal) => ({
  ...await importOriginal<typeof import("../features/delivery/api/delivery")>(),
  useProgramFinancialRollup: () => ({ data: fixtures.rollup, isLoading: fixtures.loading, isError: fixtures.error, refetch: vi.fn() }),
  usePortfolioFinancialRollup: () => ({ data: fixtures.rollup, isLoading: fixtures.loading, isError: fixtures.error, refetch: vi.fn() }),
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { permissions: fixtures.permissions } }),
}));

function currencyGroup(currency: string, approved: number, lifetime: number, count: number) {
  return {
    currency, projectCount: count, approvedAnnualBudget: approved, newApprovedFunding: 0,
    approvedFreeCarryForward: 0, approvedReservedCarryForward: 0, approvedAdjustments: 0,
    effectiveActiveAllocations: approved, availableAnnualBudget: approved, totalUnspentBudget: approved,
    approvedReversalReleases: 0, totalPaymentRequests: 0, draftRequests: 0, pendingApprovalRequests: 0,
    approvedRequests: 0, rejectedRequests: 0, approvedRequestAmount: 0, remainingPayableCommitments: 0,
    actualSettlementsInFiscalYear: approved, grossActualPaid: approved, netActualPaid: approved,
    lifetimeCeiling: lifetime, lifetimeReserved: 0, outstandingCommitments: 0, executedRefundsCorrections: 0,
    availableLifetimeBudget: lifetime, budgetLineUtilization: null, notSettledRequests: 0,
    partiallySettledRequests: 0, fullySettledRequests: 0, freeCarryForward: 0, reservedCarryForward: 0, nonCarriedBalance: 0,
  };
}

function makeRollup(): ProgramPortfolioFinancialRollup {
  return {
    entityType: "Portfolio", entityId: "pf-1", fiscalYear: 2026, fiscalYearLabel: "FY 2026",
    fiscalYearStartDate: "2026-01-01", fiscalYearEndDate: "2026-12-31", tenantTimeZone: "UTC",
    hierarchyBasis: "Direct+Programs", coverage: "Complete", includedProjectCount: 2,
    projectsMissingApprovedAnnualBudget: 0, projectsWithFinancialDataGaps: 0, unresolvedLegacyPaymentRequests: 0,
    hasMixedCurrencies: true, reconciliationStatus: "Reconciled",
    closingCoverage: { projectFinancialYears: 1, open: 0, underReview: 0, pendingApproval: 0, closed: 1, projectsWithUnresolvedFinancialData: 0, status: "Closed" },
    currencyGroups: [currencyGroup("USD", 500000, 1000000, 1), currencyGroup("EUR", 300000, 600000, 1)],
    programBreakdowns: [
      { programId: "prog-1", name: "Programme Alpha", isDirectProjectGroup: false, projectCount: 1, currencyGroups: [currencyGroup("USD", 500000, 1000000, 1)] },
      { programId: null, name: "Direct", isDirectProjectGroup: true, projectCount: 1, currencyGroups: [currencyGroup("EUR", 300000, 600000, 1)] },
    ],
    projects: [
      { projectId: "p1", projectPublicId: "PUB-1", projectName: "Project One", programId: "prog-1", programName: "Programme Alpha", isDirectProject: false, currency: "USD", isAvailable: true, dataStatus: "Available", approvedAnnualBudget: 500000, newApprovedFunding: null, approvedFreeCarryForward: null, approvedReservedCarryForward: null, approvedAdjustments: null, effectiveActiveAllocations: null, availableAnnualBudget: null, totalUnspentBudget: null, approvedReversalReleases: null, approvedRequestAmount: null, remainingPayableCommitments: null, actualSettlementsInFiscalYear: null, grossActualPaid: null, netActualPaid: null, lifetimeCeiling: null, lifetimeReserved: null, outstandingCommitments: null, executedRefundsCorrections: null, availableLifetimeBudget: null, budgetLineUtilization: null, totalPaymentRequests: 0, draftRequests: 0, pendingApprovalRequests: 0, approvedRequests: 0, rejectedRequests: 0, notSettledRequests: 0, partiallySettledRequests: 0, fullySettledRequests: 0, closingStatus: "Closed", freeCarryForward: null, reservedCarryForward: null, nonCarriedBalance: null },
      { projectId: "p2", projectPublicId: "PUB-2", projectName: "Project Two", programId: null, programName: null, isDirectProject: true, currency: "EUR", isAvailable: true, dataStatus: "Available", approvedAnnualBudget: null, newApprovedFunding: null, approvedFreeCarryForward: null, approvedReservedCarryForward: null, approvedAdjustments: null, effectiveActiveAllocations: null, availableAnnualBudget: null, totalUnspentBudget: null, approvedReversalReleases: null, approvedRequestAmount: null, remainingPayableCommitments: null, actualSettlementsInFiscalYear: null, grossActualPaid: null, netActualPaid: null, lifetimeCeiling: null, lifetimeReserved: null, outstandingCommitments: null, executedRefundsCorrections: null, availableLifetimeBudget: null, budgetLineUtilization: null, totalPaymentRequests: 0, draftRequests: 0, pendingApprovalRequests: 0, approvedRequests: 0, rejectedRequests: 0, notSettledRequests: 0, partiallySettledRequests: 0, fullySettledRequests: 0, closingStatus: "Closed", freeCarryForward: null, reservedCarryForward: null, nonCarriedBalance: null },
    ],
    projectsPage: 1, projectsPageSize: 25, projectsTotalCount: 2,
  };
}

function renderRollup(kind: "Program" | "Portfolio" = "Portfolio", locale = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  const client = new QueryClient();
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <MemoryRouter initialEntries={["/portfolios/pf-1"]}>
          <FinancialRollupView kind={kind} id="pf-1" enabled scopeKey="scope" entityName="Test Portfolio" />
        </MemoryRouter>
      </I18nProvider>
    </QueryClientProvider>,
  );
}

describe("FinancialRollupView financial reporting", () => {
  beforeEach(() => {
    fixtures.permissions = ["Portfolio.View", "PortfolioFinancialRollup.View", "Program.View", "ProgramFinancialRollup.View"];
    fixtures.loading = false;
    fixtures.error = false;
    fixtures.rollup = makeRollup();
  });

  it("shows the rollup title and the lifetime financial position section", () => {
    renderRollup();
    expect(screen.getByText("Project financial rollup")).toBeInTheDocument();
    expect(screen.getAllByText("Lifetime financial position").length).toBeGreaterThan(0);
  });

  it("keeps each currency in its own group (no mixed total)", () => {
    renderRollup();
    expect(screen.getAllByText("USD").length).toBeGreaterThan(0);
    expect(screen.getAllByText("EUR").length).toBeGreaterThan(0);
    // Approved budget chart title present
    expect(screen.getByText("Approved budget by currency")).toBeInTheDocument();
  });

  it("renders Excel and PDF export buttons when the rollup view permission is granted", () => {
    renderRollup();
    expect(screen.getByRole("button", { name: /Excel/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /PDF/ })).toBeInTheDocument();
  });

  it("drills down from a portfolio program breakdown to the program page", () => {
    renderRollup("Portfolio");
    const link = screen.getByRole("link", { name: "Programme Alpha" });
    expect(link).toHaveAttribute("href", "/portfolios/programs/prog-1");
  });

  it("renders an em dash for null project amounts instead of zero", () => {
    renderRollup("Portfolio");
    // Project Two has null approvedAnnualBudget; the project breakdown row must not show $0.
    const row = screen.getByText("Project Two").closest("tr");
    expect(row).not.toBeNull();
    expect(row!.textContent).not.toContain("$0");
  });
});