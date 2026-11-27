import { describe, expect, it } from "vitest";
import {
  cashFlowDateKey,
  getCashFlowMonthGridDays,
  getCashFlowMonthRange,
  parseCashFlowDateKey,
  shiftCashFlowMonth,
} from "../features/projects/cash-flow-calendar";

describe("cash flow calendar date helpers", () => {
  it("builds a stable six-week grid for Sunday, Monday, and Saturday week starts", () => {
    const month = new Date(2026, 1, 1);
    const sundayStart = getCashFlowMonthGridDays(month, 0);
    const mondayStart = getCashFlowMonthGridDays(month, 1);
    const saturdayStart = getCashFlowMonthGridDays(month, 6);

    expect(sundayStart).toHaveLength(42);
    expect(sundayStart[0].getDay()).toBe(0);
    expect(mondayStart[0].getDay()).toBe(1);
    expect(saturdayStart[0].getDay()).toBe(6);
    const firstDayInMonth = sundayStart.find(day => day.getMonth() === month.getMonth());
    expect(firstDayInMonth).toBeDefined();
    expect(cashFlowDateKey(firstDayInMonth!)).toBe("2026-02-01");
  });

  it("returns inclusive month boundaries including leap days", () => {
    expect(getCashFlowMonthRange(new Date(2024, 1, 14))).toEqual({ from: "2024-02-01", to: "2024-02-29" });
    expect(getCashFlowMonthRange(new Date(2026, 11, 31))).toEqual({ from: "2026-12-01", to: "2026-12-31" });
  });

  it("parses only valid canonical date keys without shifting the calendar day", () => {
    expect(cashFlowDateKey(parseCashFlowDateKey("2026-02-28")!)).toBe("2026-02-28");
    expect(parseCashFlowDateKey("2026-02-29")).toBeNull();
    expect(parseCashFlowDateKey("2024-02-29")).not.toBeNull();
    expect(parseCashFlowDateKey("2026-11-31")).toBeNull();
    expect(parseCashFlowDateKey("2026-02-01T00:00:00Z")).toBeNull();
  });

  it("moves month anchors across year boundaries without retaining a day offset", () => {
    expect(cashFlowDateKey(shiftCashFlowMonth(new Date(2026, 11, 31), 1))).toBe("2027-01-01");
    expect(cashFlowDateKey(shiftCashFlowMonth(new Date(2026, 0, 31), -1))).toBe("2025-12-01");
  });

  it("rejects invalid week-start and month offsets", () => {
    expect(() => getCashFlowMonthGridDays(new Date(2026, 0, 1), 7)).toThrow(RangeError);
    expect(() => shiftCashFlowMonth(new Date(2026, 0, 1), 1.5)).toThrow(RangeError);
  });
});
