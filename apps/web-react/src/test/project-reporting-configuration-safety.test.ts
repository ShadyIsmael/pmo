import { describe, expect, it } from "vitest";
import { updatePlanSelection } from "@features/projects/lib/project-reporting-configuration";

describe("project reporting plan selection keys", () => {
  it("stores prototype-like plan IDs as ordinary independent keys", () => {
    const initial = new Map();
    const withPrototypeKey = updatePlanSelection(initial, "__proto__", { include: true });
    const withConstructorKey = updatePlanSelection(withPrototypeKey, "constructor", { baselineVersionId: "baseline-1" });

    expect(withConstructorKey.get("__proto__")).toEqual({
      include: true,
      workingVersionId: "",
      baselineVersionId: "",
    });
    expect(withConstructorKey.get("constructor")).toEqual({
      include: false,
      workingVersionId: "",
      baselineVersionId: "baseline-1",
    });
    expect(Object.getPrototypeOf(withConstructorKey)).toBe(Map.prototype);
    expect(Object.prototype).not.toHaveProperty("include");
  });
});
