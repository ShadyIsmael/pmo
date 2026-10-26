import { describe, expect, it } from "vitest";
import { getRiskLevelTextClass, getRiskStatusTextClass } from "../lib/status-presentation";

describe("risk semantic text colors", () => {
  it("uses theme semantic tokens for risk levels", () => {
    expect(getRiskLevelTextClass("Low")).toBe("text-success");
    expect(getRiskLevelTextClass("Medium")).toBe("text-warning");
    expect(getRiskLevelTextClass("High")).toBe("text-warning");
    expect(getRiskLevelTextClass("Critical")).toBe("text-destructive");
  });

  it("uses theme semantic tokens for risk statuses", () => {
    expect(getRiskStatusTextClass("Identified")).toBe("text-info");
    expect(getRiskStatusTextClass("ActionRequired")).toBe("text-warning");
    expect(getRiskStatusTextClass("Mitigated")).toBe("text-success");
    expect(getRiskStatusTextClass("Closed")).toBe("text-muted-foreground");
  });
});
