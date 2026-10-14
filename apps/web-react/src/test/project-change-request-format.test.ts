import { describe, expect, it } from "vitest";
import { formatProjectChangeRequestAmount } from "../features/projects/lib/project-change-request-format";

describe("formatProjectChangeRequestAmount", () => {
  it("keeps western digits in Arabic while formatting the requested currency", () => {
    const result = formatProjectChangeRequestAmount(125.5, "USD", "ar");
    expect(result).toMatch(/\d/);
    expect(result).not.toMatch(/[٠-٩]/);
  });

  it("uses English formatting for English pages", () => {
    expect(formatProjectChangeRequestAmount(-125.5, "USD", "en")).toContain("125.5");
  });
});
