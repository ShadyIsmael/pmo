import { describe, expect, it } from "vitest";
import type { CostUtilizationDaily } from "../features/resources/api/cost-utilization";
import { aggregateCostUtilizationMetrics, dateKey, getMonthRange, groupCostUtilizationByCurrency, groupCostUtilizationByDate, monthGridDays } from "../features/resources/cost-utilization-calendar";

function daily(overrides: Partial<CostUtilizationDaily> = {}): CostUtilizationDaily {
  return {
    resourceId: "resource-1",
    resourceName: "Resource One",
    workDate: "2026-02-28T00:00:00",
    currency: "USD",
    capacityHours: 8,
    plannedHours: 4,
    actualHours: 3,
    regularHours: 3,
    planningGapHours: 1,
    executionGapHours: 1,
    unusedCapacityHours: 5,
    excessHours: 0,
    overtimeHours: 0,
    plannedUtilization: 50,
    actualUtilization: 37.5,
    plannedLaborCost: 400,
    actualLaborCost: 300,
    laborCostVariance: -100,
    overtimeCost: 0,
    unusedCapacityCost: 500,
    committedCapacityCost: 800,
    totalResourceCost: 800,
    costProfileMissing: false,
    costCoverageComplete: true,
    uncostedActualHours: 0,
    uncostedPlannedHours: 0,
    ...overrides,
  };
}

describe("Cost Utilization calendar helpers", () => {
  it("builds a stable six-week calendar using the configured first weekday", () => {
    const month = new Date(2026, 1, 1);
    const sundayStart = monthGridDays(month, 0);
    const mondayStart = monthGridDays(month, 1);

    expect(sundayStart).toHaveLength(42);
    expect(sundayStart[0].getDay()).toBe(0);
    expect(mondayStart[0].getDay()).toBe(1);
    expect(dateKey(sundayStart[sundayStart.findIndex((day) => day.getMonth() === 1)])).toBe("2026-02-01");
    expect(getMonthRange(month)).toEqual({ from: "2026-02-01", to: "2026-02-28" });
  });

  it("aggregates daily rows per currency and keeps missing monetary values unavailable", () => {
    const rows = [
      daily(),
      daily({ resourceId: "resource-2", resourceName: "Resource Two", plannedHours: 2, actualHours: 1, actualLaborCost: 100, plannedLaborCost: 200, laborCostVariance: -100, totalResourceCost: 400 }),
      daily({ resourceId: "resource-3", currency: "EUR", actualLaborCost: 80, plannedLaborCost: 120, laborCostVariance: -40, totalResourceCost: 200 }),
      daily({ resourceId: "resource-4", currency: "EUR", actualLaborCost: undefined, costProfileMissing: true, costCoverageComplete: false, totalResourceCost: undefined }),
    ];

    const groups = groupCostUtilizationByCurrency(rows);
    const usd = groups.find((group) => group.currency === "USD");
    const eur = groups.find((group) => group.currency === "EUR");
    const dateGroups = groupCostUtilizationByDate(rows);

    expect(usd?.actualLaborCost).toBe(400);
    expect(usd?.plannedLaborCost).toBe(600);
    expect(usd?.actualUtilization).toBeCloseTo(25);
    expect(eur?.actualLaborCost).toBeUndefined();
    expect(eur?.costCoverageComplete).toBe(false);
    expect(aggregateCostUtilizationMetrics(rows).actualHours).toBe(10);
    expect(dateGroups.get("2026-02-28")).toHaveLength(4);
    expect(groupCostUtilizationByCurrency(rows)).toHaveLength(2);
  });
});
