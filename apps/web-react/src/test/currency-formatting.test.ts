import { describe, expect, it } from "vitest";
import { formatCurrencyValue } from "../lib/formatting/currency";
import { formatNumberValue } from "../lib/formatting/number";
import { formatFileSize } from "../lib/formatting/file-size";

describe("formatCurrencyValue", () => {
  it("formats each configured currency without replacing it with a default", () => {
    expect(formatCurrencyValue(1234.5, "USD", "en", { minimumFractionDigits: 2 })).toBe("$1,234.50");
    expect(formatCurrencyValue(1234.5, "GBP", "en", { minimumFractionDigits: 2 })).toBe("£1,234.50");
    expect(formatCurrencyValue(1234.5, "EGP", "en", { minimumFractionDigits: 2 })).not.toBe("$1,234.50");
  });

  it("returns an unavailable marker for missing, malformed, or non-finite data", () => {
    expect(formatCurrencyValue(10, undefined, "en")).toBe("—");
    expect(formatCurrencyValue(10, "US", "en")).toBe("—");
    expect(formatCurrencyValue(Number.NaN, "USD", "en")).toBe("—");
    expect(formatCurrencyValue(Number.POSITIVE_INFINITY, "USD", "en")).toBe("—");
  });

  it("uses Arabic locale formatting when selected", () => {
    expect(formatCurrencyValue(1234.5, "EGP", "ar")).not.toBe(formatCurrencyValue(1234.5, "EGP", "en"));
  });
});


describe("shared locale formatting", () => {
  it("formats finite numbers with Western digits and preserves requested precision", () => {
    expect(formatNumberValue(1234.5, "en", { maximumFractionDigits: 2 })).toBe("1,234.5");
    expect(formatNumberValue(1234.5, "ar", { maximumFractionDigits: 2 })).toBe("1,234.5");
    expect(formatNumberValue(Number.NaN, "en")).toBe("—");
  });

  it("formats file sizes with locale-aware numbers and safe missing values", () => {
    expect(formatFileSize(0, "en")).toBe("0 B");
    expect(formatFileSize(1536, "en")).toBe("1.5 KB");
    expect(formatFileSize(undefined, "en")).toBe("—");
    expect(formatFileSize(-1, "en")).toBe("—");
  });
});
