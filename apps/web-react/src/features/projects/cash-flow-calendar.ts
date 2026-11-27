export type CashFlowDateRange = { from: string; to: string };

/** Format a calendar day as a date-only key without applying a UTC conversion. */
export function cashFlowDateKey(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

/** Parse a canonical YYYY-MM-DD calendar date, rejecting impossible dates. */
export function parseCashFlowDateKey(value: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) return null;

  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const date = new Date(year, month - 1, day);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) return null;
  return date;
}

/** Inclusive date-only range for the month containing the anchor date. */
export function getCashFlowMonthRange(anchor: Date): CashFlowDateRange {
  const first = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
  const last = new Date(anchor.getFullYear(), anchor.getMonth() + 1, 0);
  return { from: cashFlowDateKey(first), to: cashFlowDateKey(last) };
}

/**
 * Six rows of calendar days for a month. `firstDayOfWeek` follows `Date.getDay()`:
 * Sunday is 0, Monday is 1, and Saturday is 6.
 */
export function getCashFlowMonthGridDays(anchor: Date, firstDayOfWeek = 0): Date[] {
  if (!Number.isInteger(firstDayOfWeek) || firstDayOfWeek < 0 || firstDayOfWeek > 6) {
    throw new RangeError("firstDayOfWeek must be an integer from 0 to 6");
  }

  const first = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
  const offset = (first.getDay() - firstDayOfWeek + 7) % 7;
  const start = new Date(first.getFullYear(), first.getMonth(), first.getDate() - offset);
  return Array.from({ length: 42 }, (_, index) =>
    new Date(start.getFullYear(), start.getMonth(), start.getDate() + index));
}

/** Move to another month while returning a first-of-month anchor. */
export function shiftCashFlowMonth(anchor: Date, offset: number): Date {
  if (!Number.isInteger(offset)) throw new RangeError("month offset must be an integer");
  return new Date(anchor.getFullYear(), anchor.getMonth() + offset, 1);
}
