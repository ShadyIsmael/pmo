import type { BudgetDto } from "./api/finance";

export interface ForecastPageSummary {
  currency: string;
  allocation: number | null;
  forecast: number | null;
  variance: number | null;
  count: number;
}

export function normalizedCurrency(value: string | null | undefined): string {
  return value?.trim().toUpperCase() ?? "";
}

export function forecastVariance(budget: BudgetDto): number | null {
  if (!Number.isFinite(budget.approvedAmount) || !Number.isFinite(budget.forecastAmount)) return null;
  return budget.approvedAmount - budget.forecastAmount;
}

export function forecastVariancePercent(budget: BudgetDto): number | null {
  if (!Number.isFinite(budget.approvedAmount) || !Number.isFinite(budget.forecastAmount) || budget.approvedAmount === 0) return null;
  return ((budget.forecastAmount - budget.approvedAmount) / budget.approvedAmount) * 100;
}

function sumAmount(items: readonly BudgetDto[], select: (item: BudgetDto) => number): number | null {
  const values = items.map(select);
  if (values.some((value) => !Number.isFinite(value))) return null;
  return values.reduce((total, value) => total + value, 0);
}

export function summarizeForecastPage(items: readonly BudgetDto[]): ForecastPageSummary[] {
  const groups = new Map<string, BudgetDto[]>();
  for (const item of items) {
    const currency = normalizedCurrency(item.currency);
    const key = currency || "__unknown__";
    groups.set(key, [...(groups.get(key) ?? []), item]);
  }

  return Array.from(groups.entries()).map(([key, group]) => {
    const allocation = sumAmount(group, (item) => item.approvedAmount);
    const forecast = sumAmount(group, (item) => item.forecastAmount);
    return {
      currency: key === "__unknown__" ? "" : key,
      allocation,
      forecast,
      variance: allocation === null || forecast === null ? null : allocation - forecast,
      count: group.length,
    };
  });
}
