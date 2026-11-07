import type { CostUtilizationDaily, CostUtilizationMetric } from "./api/cost-utilization";

export function dateKey(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function parseDateKey(value: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) return null;
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  return Number.isNaN(date.getTime()) ? null : date;
}

export function getMonthRange(anchor: Date): { from: string; to: string } {
  const start = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
  const end = new Date(anchor.getFullYear(), anchor.getMonth() + 1, 0);
  return { from: dateKey(start), to: dateKey(end) };
}

export function monthGridDays(anchor: Date, firstDayOfWeek = 0): Date[] {
  const first = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
  const offset = (first.getDay() - firstDayOfWeek + 7) % 7;
  const start = new Date(first.getFullYear(), first.getMonth(), first.getDate() - offset);
  return Array.from({ length: 42 }, (_, index) => new Date(start.getFullYear(), start.getMonth(), start.getDate() + index));
}

const metricFields = [
  "capacityHours", "plannedHours", "actualHours", "regularHours", "planningGapHours", "executionGapHours",
  "unusedCapacityHours", "excessHours", "overtimeHours", "uncostedActualHours", "uncostedPlannedHours",
] as const;

const costFields = [
  "plannedLaborCost", "actualLaborCost", "laborCostVariance", "overtimeCost", "unusedCapacityCost",
  "committedCapacityCost", "totalResourceCost",
] as const;

type NumericMetricField = typeof metricFields[number];
type CostMetricField = typeof costFields[number];

export function aggregateCostUtilizationMetrics(rows: readonly CostUtilizationDaily[]): CostUtilizationMetric {
  const first = rows[0];
  const totals = Object.fromEntries(metricFields.map((field) => [field, rows.reduce((total, row) => total + (Number.isFinite(row[field]) ? row[field] : 0), 0)])) as Record<NumericMetricField, number>;
  const costTotals = Object.fromEntries(costFields.map((field) => {
    const values = rows.map((row) => row[field]);
    const complete = values.length > 0 && values.every((value) => typeof value === "number" && Number.isFinite(value));
    return [field, complete ? values.reduce<number>((total, value) => total + (value as number), 0) : undefined];
  })) as Record<CostMetricField, number | undefined>;

  return {
    currency: first?.currency,
    ...totals,
    plannedUtilization: totals.capacityHours > 0 ? totals.plannedHours / totals.capacityHours * 100 : undefined,
    actualUtilization: totals.capacityHours > 0 ? totals.actualHours / totals.capacityHours * 100 : undefined,
    ...costTotals,
    costProfileMissing: rows.some((row) => row.costProfileMissing),
    costCoverageComplete: rows.length > 0 && rows.every((row) => row.costCoverageComplete),
  };
}

export function groupCostUtilizationByCurrency(rows: readonly CostUtilizationDaily[]): CostUtilizationMetric[] {
  const groups = new Map<string, CostUtilizationDaily[]>();
  for (const row of rows) {
    const currencyKey = row.currency?.trim().toUpperCase() ?? "";
    const group = groups.get(currencyKey) ?? [];
    group.push(row);
    groups.set(currencyKey, group);
  }
  return [...groups.entries()]
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([currency, group]) => ({ ...aggregateCostUtilizationMetrics(group), currency: currency || undefined }));
}

export function groupCostUtilizationByDate(rows: readonly CostUtilizationDaily[]): Map<string, CostUtilizationDaily[]> {
  const days = new Map<string, CostUtilizationDaily[]>();
  for (const row of rows) {
    const key = row.workDate.slice(0, 10);
    const group = days.get(key) ?? [];
    group.push(row);
    days.set(key, group);
  }
  return days;
}
