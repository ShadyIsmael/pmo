import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PlanningBaselineAccessHint } from "../features/projects/components/PlanningBaselineAccessHint";

const translate = (key: string) => ({
  "planning.baselineLifecycleRequired": "Baseline lifecycle permission is required.",
  "planning.baselineEditableContextRequired": "An editable management context is required.",
}[key] ?? key);

describe("PlanningBaselineAccessHint", () => {
  it("explains the missing permission using an info semantic text color", () => {
    render(<PlanningBaselineAccessHint reason="permission-required" t={translate} />);

    const note = screen.getByRole("note");
    expect(note).toHaveTextContent("Baseline lifecycle permission is required.");
    expect(note).toHaveClass("text-info");
    expect(note.className).not.toMatch(/\bbg-/);
  });

  it("explains the read-only context using a warning semantic text color", () => {
    render(<PlanningBaselineAccessHint reason="editable-context-required" t={translate} />);

    const note = screen.getByRole("note");
    expect(note).toHaveTextContent("An editable management context is required.");
    expect(note).toHaveClass("text-warning");
    expect(note.className).not.toMatch(/\bbg-/);
  });

  it("renders nothing when baseline actions are available", () => {
    const { container } = render(<PlanningBaselineAccessHint reason={null} t={translate} />);
    expect(container).toBeEmptyDOMElement();
  });
});
