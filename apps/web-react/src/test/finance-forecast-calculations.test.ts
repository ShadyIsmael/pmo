import { describe, expect, it } from "vitest";
import { forecastVariance, forecastVariancePercent, summarizeForecastPage } from "../features/finance/forecast-calculations";
import type { BudgetDto } from "../features/finance/api/finance";

function budget(overrides: Partial<BudgetDto> = {}): BudgetDto {
  return {
    id: "budget-1",
    name: "Delivery",
    category: "Capital",
    plannedAmount: 100,
    approvedAmount: 100,
    forecastAmount: 90,
    currency: "USD",
    fiscalYear: 2026,
    status: "Approved",
    rowVersion: "v1",
    ...overrides,
  };
}

describe("finance forecast calculations", () => {
  it("keeps variance signs consistent with the source: positive is under allocation", () => {
    expect(forecastVariance(budget())).toBe(10);
    expect(forecastVariancePercent(budget())).toBe(-10);
    expect(forecastVariance(budget({ forecastAmount: 120 }))).toBe(-20);
    expect(forecastVariancePercent(budget({ forecastAmount: 120 }))).toBe(20);
  });

  it("summarizes only the current page and never combines currencies", () => {
    expect(summarizeForecastPage([
      budget({ id: "usd-1", currency: " usd ", approvedAmount: 100, forecastAmount: 90 }),
      budget({ id: "usd-2", currency: "USD", approvedAmount: 50, forecastAmount: 65 }),
      budget({ id: "eur-1", currency: "EUR", approvedAmount: 40, forecastAmount: 30 }),
    ])).toEqual([
      { currency: "USD", allocation: 150, forecast: 155, variance: -5, count: 2 },
      { currency: "EUR", allocation: 40, forecast: 30, variance: 10, count: 1 },
    ]);
  });

  it("does not invent percentages for unallocated or invalid budgets", () => {
    expect(forecastVariancePercent(budget({ approvedAmount: 0 }))).toBeNull();
    expect(forecastVariance(budget({ forecastAmount: Number.NaN }))).toBeNull();
  });
});
