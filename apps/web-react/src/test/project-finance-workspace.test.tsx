import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectFinanceTab } from "../features/projects/ProjectFinanceTab";

const permissionsRef = { current: ["Project.View", "ProjectFinance.View"] as string[] };
const reportingRef = {
  current: { data: null as any, isLoading: false, isError: false, refetch: vi.fn() },
};

vi.mock("../features/projects/api/project-financials", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/projects/api/project-financials")>();
  return {
    ...actual,
    useProjectFinancialReporting: () => reportingRef.current,
  };
});

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { permissions: permissionsRef.current } }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ canMutate: true, effectiveReadOnly: false }),
}));

function OutletHost() {
  return <Outlet context={{ projectId: "project-42", project: { id: "project-42", currency: "USD" } }} />;
}

function makeFixture(overrides: any = {}) {
  return {
    projectId: "project-42",
    projectCode: "P-42",
    projectName: "Project Atlas",
    currency: "USD",
    lifetimeCeiling: 1_000_000,
    annualApprovedBudget: 500_000,
    lifetimeReserved: 100_000,
    outstandingCommitments: 50_000,
    grossActualPaid: 200_000,
    confirmedRefundsAndCorrections: -10_000,
    netActualPaid: 190_000,
    availableLifetimeBudget: 700_000,
    annualBudgetUtilizationPercentage: 40,
    budgetLineUtilizations: [
      {
        budgetLineId: "bl-1",
        reference: "BL-1",
        description: "Line one",
        authorizedLimit: 100_000,
        reservedAmount: 10_000,
        utilizationPercentage: 10,
        hasCurrencyMismatch: false,
      },
    ],
    dataQuality: {
      hasAnyGap: false,
      details: [],
      hasCurrencyMismatch: false,
      projectCurrencyUnavailable: false,
      hasMissingFinancialReferences: false,
      overallStatus: "Available",
      hasUnresolvedCurrency: false,
      hasIncompleteSettlementHistory: false,
      hasMissingAnnualBudgetAllocations: false,
    },
    ...overrides,
  };
}

function renderFinance(locale = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  const qc = new QueryClient();
  return render(
    <QueryClientProvider client={qc}>
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-42/financials"]}>
          <Routes>
            <Route path="/projects/:id" element={<OutletHost />}>
              <Route path="financials" element={<ProjectFinanceTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>
    </QueryClientProvider>,
  );
}

describe("ProjectFinanceTab", () => {
  beforeEach(() => {
    permissionsRef.current = ["Project.View", "ProjectFinance.View"];
    reportingRef.current = { data: makeFixture(), isLoading: false, isError: false, refetch: vi.fn() };
  });

  it("renders heading", () => {
    renderFinance();
    expect(screen.getByText("Financial Reporting Dashboard")).toBeInTheDocument();
  });

  it("renders lifetime ceiling KPI", () => {
    renderFinance();
    expect(screen.getByTestId("finance-metric-lifetime-ceiling")).toHaveTextContent("1,000,000");
  });

  it("shows permission message", () => {
    permissionsRef.current = ["Project.View"];
    renderFinance();
    expect(screen.getByText("You do not have permission to view project financials.")).toBeInTheDocument();
  });

  it("loading state", () => {
    reportingRef.current = { data: undefined, isLoading: true, isError: false, refetch: vi.fn() };
    renderFinance();
    expect(screen.getByText("Loading...")).toBeInTheDocument();
  });

  it("error state", () => {
    reportingRef.current = { data: undefined, isLoading: false, isError: true, refetch: vi.fn() };
    renderFinance();
    expect(screen.getByText("The financial reporting data could not be loaded.")).toBeInTheDocument();
  });

  it("null shows em dash", () => {
    reportingRef.current = { data: makeFixture({ grossActualPaid: null, netActualPaid: null }), isLoading: false, isError: false, refetch: vi.fn() };
    renderFinance();
    const m = screen.getByTestId("finance-metric-gross-actual-paid");
    expect(m).toHaveTextContent("—");
    expect(m).not.toHaveTextContent("$0");
  });

  it("export buttons present", () => {
    renderFinance();
    expect(screen.getByRole("button", { name: /Excel/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /PDF/ })).toBeInTheDocument();
  });

  it("Arabic rtl", () => {
    renderFinance("ar");
    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.getByText("لوحة تقارير المالية")).toBeInTheDocument();
  });
});
