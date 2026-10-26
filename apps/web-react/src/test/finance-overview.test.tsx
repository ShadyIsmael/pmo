import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { useGlobalFinancialSummary, type CurrencyFinancialSummary, type GlobalFinancialSummaryDto, type ProjectFinancialBreakdown } from "../features/finance/api/finance";
import { CostsOverviewPage } from "../features/finance/CostsOverviewPage";

vi.mock("../features/finance/api/finance", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/finance/api/finance")>();
  return { ...actual, useGlobalFinancialSummary: vi.fn() };
});

const useGlobalSummaryMock = vi.mocked(useGlobalFinancialSummary);

function currency(overrides: Partial<CurrencyFinancialSummary> = {}): CurrencyFinancialSummary {
  return {
    currency: "USD", originalApprovedBudget: 1200, approvedBudget: 1000, allocatedBudget: 700, unallocatedBudget: 300,
    availableBudget: 500, actualCost: 300, committedCost: 200, forecastAtCompletion: 900, forecastVariance: 100,
    budgetUtilizationPercentage: 50, projectCount: 1, ...overrides,
  };
}

function project(overrides: Partial<ProjectFinancialBreakdown> = {}): ProjectFinancialBreakdown {
  return {
    projectId: "a0000001-0000-0000-0000-000000000001", projectPublicId: "f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d", projectName: "Core Platform", currency: "USD", originalApprovedBudget: 1200,
    approvedBudget: 1000, allocatedBudget: 700, unallocatedBudget: 300, availableBudget: 500, actualCost: 300,
    committedCost: 200, forecastAtCompletion: 900, forecastVariance: 100, budgetUtilizationPercentage: 50, ...overrides,
  };
}

function renderDashboard() {
  return render(<MemoryRouter><I18nProvider><CostsOverviewPage /></I18nProvider></MemoryRouter>);
}

describe("global finance dashboard parity", () => {
  beforeEach(() => {
    useGlobalSummaryMock.mockReset();
  });

  it("renders the API-backed authority, spending, forecast and project views without mixing currencies", () => {
    const summary: GlobalFinancialSummaryDto = {
      byCurrency: [currency(), currency({ currency: "EUR", approvedBudget: 2000, projectCount: 1 })],
      byProject: [project(), project({ projectId: "a0000001-0000-0000-0000-000000000002", projectPublicId: "f5d9d74f-15aa-4c2c-9df3-a8141bb37b5e", projectName: "Regional rollout", currency: "EUR", approvedBudget: 2000 })],
    };
    useGlobalSummaryMock.mockReturnValue({ data: summary, isLoading: false, isError: false, refetch: vi.fn() } as unknown as ReturnType<typeof useGlobalFinancialSummary>);

    renderDashboard();

    expect(useGlobalSummaryMock).toHaveBeenCalled();
    expect(screen.getByText("Budget Authority")).toBeInTheDocument();
    expect(screen.getByText("Spending Position")).toBeInTheDocument();
    expect(screen.getByText("Forecast Position")).toBeInTheDocument();
    expect(screen.getByText("Project Financial Position")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Core Platform/ })).toHaveAttribute("href", "/projects/f5d9d74f-15aa-4c2c-9df3-a8141bb37b5d?tab=financials");
    expect(screen.queryByText("Regional rollout")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "EUR" }));
    expect(screen.getByText("Regional rollout")).toBeInTheDocument();
    expect(screen.queryByText("Core Platform")).not.toBeInTheDocument();
  });

  it("shows an actionable error state and retries the summary request", () => {
    const refetch = vi.fn();
    useGlobalSummaryMock.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch } as unknown as ReturnType<typeof useGlobalFinancialSummary>);

    renderDashboard();

    expect(screen.getByText("Could not load cost data.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(refetch).toHaveBeenCalledOnce();
  });
});
