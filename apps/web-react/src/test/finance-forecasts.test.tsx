import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { useBudgets, type BudgetDto } from "../features/finance/api/finance";
import { CostsForecastsPage } from "../features/finance/CostsForecastsPage";

vi.mock("../features/finance/api/finance", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/finance/api/finance")>();
  return { ...actual, useBudgets: vi.fn() };
});

const useBudgetsMock = vi.mocked(useBudgets);

function budget(overrides: Partial<BudgetDto> = {}): BudgetDto {
  return {
    id: "budget-1",
    projectId: "PRJ-100",
    name: "Data center",
    description: "Primary hosting",
    category: "Infrastructure",
    plannedAmount: 120,
    approvedAmount: 100,
    forecastAmount: 120,
    currency: "USD",
    fiscalYear: 2026,
    fiscalQuarter: 2,
    status: "Approved",
    rowVersion: "v1",
    ...overrides,
  };
}

function renderForecasts() {
  return render(<MemoryRouter><I18nProvider><CostsForecastsPage /></I18nProvider></MemoryRouter>);
}

describe("finance forecasts page", () => {
  beforeEach(() => {
    window.localStorage.clear();
    useBudgetsMock.mockReset();
  });

  it("loads real paged forecast data and shows currency summaries, variance, fiscal period and project link", () => {
    useBudgetsMock.mockReturnValue({
      data: { items: [budget(), budget({ id: "budget-eur", name: "Controls", projectId: undefined, currency: "EUR", approvedAmount: 50, forecastAmount: 40 })], totalCount: 21, totalPages: 2 },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useBudgets>);

    renderForecasts();

    expect(useBudgetsMock).toHaveBeenCalledWith({ page: 1, pageSize: 20, hasForecast: true });
    expect(screen.getByRole("heading", { name: "Budget Forecasts", level: 1 })).toBeInTheDocument();
    expect(screen.getByText("Current-page totals")).toBeInTheDocument();
    expect(screen.getByText("Data center")).toBeInTheDocument();
    expect(screen.getAllByText("FY 2026 · Q2")).toHaveLength(2);
    expect(screen.getByRole("link", { name: "View Financials: PRJ-100" })).toHaveAttribute("href", "/projects/PRJ-100?tab=financials");
    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    expect(useBudgetsMock).toHaveBeenLastCalledWith({ page: 2, pageSize: 20, hasForecast: true });
  });

  it("shows the source error state and keeps retry available on API failure", () => {
    const refetch = vi.fn();
    useBudgetsMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      refetch,
    } as unknown as ReturnType<typeof useBudgets>);

    renderForecasts();
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(refetch).toHaveBeenCalledOnce();
    expect(screen.getByText("Budget forecasts could not be loaded.")).toBeInTheDocument();
  });
});
