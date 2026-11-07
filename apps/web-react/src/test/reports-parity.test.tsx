import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ReportsPageFull } from "../features/operations/OperationsPagesFull";

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  useManagedPortfolios: vi.fn(),
  usePortfolioManagementOverview: vi.fn(),
  usePortfolioFinancialRollup: vi.fn(),
  permissions: ["Portfolio.View"],
  managementContext: { perspective: "PortfolioManager" } as { perspective: string } | null,
  managementLoading: false,
}));

vi.mock("@api/client", () => ({ api: { get: mocks.get }, setApiErrorLocale: () => undefined }));
vi.mock("@features/delivery/api/delivery", () => ({
  useManagedPortfolios: mocks.useManagedPortfolios,
  usePortfolioManagementOverview: mocks.usePortfolioManagementOverview,
  usePortfolioFinancialRollup: mocks.usePortfolioFinancialRollup,
}));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "user-1", permissions: mocks.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ context: mocks.managementContext, loading: mocks.managementLoading }) }));

const reports = [
  { id: "executive-1", name: "Executive Summary", description: "Leadership view", category: "Executive", outputFormat: "PDF", isSystem: true, isActive: true, updatedAt: "2026-10-01T00:00:00Z" },
  { id: "portfolio-1", name: "Portfolio Health", description: "Portfolio status", category: "Portfolio", outputFormat: "XLSX", isSystem: true, isActive: true, updatedAt: "2026-10-02T00:00:00Z" },
  { id: "custom-1", name: "Custom Delivery View", description: "User-defined", category: "Custom", outputFormat: "CSV", isSystem: false, isActive: true, updatedAt: "2026-10-03T00:00:00Z" },
];

function renderReports(path = "/reports") {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes><Route path="/reports/*" element={<ReportsPageFull />} /></Routes>
        </MemoryRouter>
      </I18nProvider>
    </QueryClientProvider>,
  );
}

describe("reports source parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.get.mockReset().mockResolvedValue({ items: reports, totalCount: reports.length, totalPages: 1 });
    mocks.permissions = ["Portfolio.View"];
    mocks.managementContext = { perspective: "PortfolioManager" };
    mocks.managementLoading = false;
    mocks.useManagedPortfolios.mockReset().mockReturnValue({ data: { items: [] }, isLoading: false, isError: false, refetch: vi.fn() });
    mocks.usePortfolioManagementOverview.mockReset().mockReturnValue({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() });
    mocks.usePortfolioFinancialRollup.mockReset().mockReturnValue({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() });
  });

  it("uses live report definitions for categories, search, and templates", async () => {
    renderReports();

    expect(await screen.findByRole("row", { name: /Executive Summary/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Portfolio reports 1 reports/ })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Portfolio reports 1 reports/ }));
    expect(screen.getByRole("row", { name: /Portfolio Health/ })).toBeInTheDocument();
    expect(screen.queryByRole("row", { name: /Executive Summary/ })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "Report Templates" }));
    expect(await screen.findByText("Portfolio Health")).toBeInTheDocument();
    expect(screen.queryByText("Executive Summary")).not.toBeInTheDocument();
    expect(screen.queryByText("Custom Delivery View")).not.toBeInTheDocument();
  });

  it("does not present report definitions as scheduled jobs", async () => {
    renderReports("/reports/scheduled");

    expect(await screen.findByText("The reporting service does not currently provide scheduled report data.")).toBeInTheDocument();
    expect(mocks.get).not.toHaveBeenCalled();
  });

  it("filters category routes against the API category", async () => {
    renderReports("/reports/executive");

    expect(await screen.findByRole("row", { name: /Executive Summary/ })).toBeInTheDocument();
    expect(screen.queryByRole("row", { name: /Portfolio Health/ })).not.toBeInTheDocument();
  });

  it("shows scoped live portfolio summaries without fetching generic report definitions", async () => {
    const portfolio = { id: "portfolio-live", name: "Digital Transformation", health: "OnTrack", totalProjectCount: 2 };
    mocks.permissions = ["Portfolio.View", "PortfolioFinancialRollup.View"];
    mocks.useManagedPortfolios.mockReturnValue({ data: { items: [portfolio] }, isLoading: false, isError: false, refetch: vi.fn() });
    mocks.usePortfolioManagementOverview.mockReturnValue({
      data: {
        summary: { totalProjects: 2, averageProjectProgress: 63 },
      },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    });
    mocks.usePortfolioFinancialRollup.mockReturnValue({
      data: {
        entityType: "Portfolio", entityId: "portfolio-live", fiscalYear: 2026, fiscalYearLabel: "FY 2026",
        fiscalYearStartDate: "2026-01-01", fiscalYearEndDate: "2026-12-31", tenantTimeZone: "UTC",
        hierarchyBasis: "Portfolio", coverage: "Complete", includedProjectCount: 2,
        projectsMissingApprovedAnnualBudget: 0, projectsWithFinancialDataGaps: 0, unresolvedLegacyPaymentRequests: 0,
        hasMixedCurrencies: true, reconciliationStatus: "Reconciled",
        closingCoverage: { projectFinancialYears: 2, open: 0, underReview: 0, pendingApproval: 0, closed: 2, projectsWithUnresolvedFinancialData: 0, status: "Closed" },
        currencyGroups: [
          { currency: "USD", approvedAnnualBudget: 1000, actualSettlementsInFiscalYear: 400 },
          { currency: "EGP", approvedAnnualBudget: 2000, actualSettlementsInFiscalYear: 900 },
        ],
        programBreakdowns: [], projects: [], projectsPage: 1, projectsPageSize: 25, projectsTotalCount: 2,
      },
      isLoading: false, isError: false, refetch: vi.fn(),
    });

    renderReports("/reports/portfolio");

    const card = (await screen.findByText("Digital Transformation")).parentElement?.parentElement?.parentElement;
    expect(card).not.toBeNull();
    expect(within(card as HTMLElement).getByText("Total Projects")).toBeInTheDocument();
    expect(within(card as HTMLElement).getByText("63%")).toBeInTheDocument();
    expect(within(card as HTMLElement).getByText("USD")).toBeInTheDocument();
    expect(within(card as HTMLElement).getByText("EGP")).toBeInTheDocument();
    expect(within(card as HTMLElement).getAllByText("Budget")).toHaveLength(2);
    expect(within(card as HTMLElement).getByText("Amounts remain grouped by currency; no mixed-currency total is shown.")).toBeInTheDocument();
    expect(within(card as HTMLElement).getByRole("link", { name: "Open" })).toHaveAttribute("href", "/portfolios/portfolio-live");
    expect(mocks.useManagedPortfolios).toHaveBeenCalledWith("", true);
    expect(mocks.usePortfolioManagementOverview).toHaveBeenCalledWith("portfolio-live", { programsPage: 1, projectsPage: 1, pageSize: 1 }, true);
    expect(mocks.usePortfolioFinancialRollup).toHaveBeenCalledWith("portfolio-live", {}, true);
    expect(mocks.get).not.toHaveBeenCalled();
  });

  it("does not request or display portfolio financials without PortfolioFinancialRollup.View", async () => {
    const portfolio = { id: "portfolio-live", name: "Digital Transformation", health: "OnTrack", totalProjectCount: 2 };
    mocks.useManagedPortfolios.mockReturnValue({ data: { items: [portfolio] }, isLoading: false, isError: false, refetch: vi.fn() });
    mocks.usePortfolioManagementOverview.mockReturnValue({
      data: { summary: { totalProjects: 2, averageProjectProgress: 63 } }, isLoading: false, isError: false, refetch: vi.fn(),
    });

    renderReports("/reports/portfolio");

    const card = (await screen.findByText("Digital Transformation")).parentElement?.parentElement?.parentElement;
    expect(card).not.toBeNull();
    expect(within(card as HTMLElement).getByText("63%")).toBeInTheDocument();
    expect(within(card as HTMLElement).queryByText("Budget")).not.toBeInTheDocument();
    expect(within(card as HTMLElement).queryByText("USD")).not.toBeInTheDocument();
    expect(mocks.usePortfolioFinancialRollup).toHaveBeenCalledWith("portfolio-live", {}, false);
  });

  it("does not query portfolio data without the Portfolio Manager scope", async () => {
    mocks.managementContext = null;

    renderReports("/reports/portfolio");

    expect(await screen.findByText("Select a Portfolio Manager management scope to view live portfolio reports.")).toBeInTheDocument();
    expect(mocks.useManagedPortfolios).toHaveBeenCalledWith("", false);
    expect(mocks.get).not.toHaveBeenCalled();
  });

  it("does not query portfolio data without Portfolio.View permission", async () => {
    mocks.permissions = [];

    renderReports("/reports/portfolio");

    expect(await screen.findByText("You do not have permission to view portfolio data.")).toBeInTheDocument();
    expect(mocks.useManagedPortfolios).toHaveBeenCalledWith("", false);
    expect(mocks.get).not.toHaveBeenCalled();
  });
});
