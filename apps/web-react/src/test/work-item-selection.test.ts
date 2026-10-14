import { describe, expect, it } from "vitest";
import { isAllVisibleWorkItemsSelected, toggleAllVisibleWorkItemSelection, toggleWorkItemSelection } from "@features/delivery/workItemSelection";

describe("work item row selection", () => {
  it("toggles an individual row without losing other page selections", () => {
    expect(toggleWorkItemSelection(new Set(["a", "b"]), "b")).toEqual(new Set(["a"]));
    expect(toggleWorkItemSelection(new Set(["a"]), "c")).toEqual(new Set(["a", "c"]));
  });

  it("selects and clears all visible rows while retaining selections from other pages", () => {
    const visible = ["a", "b"];
    const selected = toggleAllVisibleWorkItemSelection(new Set(["other-page"]), visible);
    expect(selected).toEqual(new Set(["other-page", "a", "b"]));
    expect(isAllVisibleWorkItemsSelected(selected, visible)).toBe(true);
    expect(toggleAllVisibleWorkItemSelection(selected, visible)).toEqual(new Set(["other-page"]));
  });

  it("does not mark an empty page as fully selected", () => {
    expect(isAllVisibleWorkItemsSelected(new Set(), [])).toBe(false);
  });
});
