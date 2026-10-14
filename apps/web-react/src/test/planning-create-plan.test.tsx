import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { PlanningCreatePlanPanel } from "../features/projects/components/PlanningCreatePlanPanel";

const t = (key: string) => key;

describe("planning create-plan panel", () => {
  it("validates, trims, and submits the selected plan fields before selecting the created plan", async () => {
    const mutateAsync = vi.fn().mockResolvedValue({ id: "plan-new" });
    const onCreated = vi.fn();
    const onCancel = vi.fn();
    render(<PlanningCreatePlanPanel mutation={{ mutateAsync, isPending: false, error: null }} t={t} onCreated={onCreated} onCancel={onCancel} />);

    const createButton = screen.getByRole("button", { name: "projects.planning.createPlan" });
    expect(createButton).toBeDisabled();
    fireEvent.change(screen.getByLabelText("projects.planning.planName"), { target: { value: "  Delivery plan  " } });
    fireEvent.change(screen.getByLabelText("projects.planning.planDescription"), { target: { value: "  Phase one  " } });
    fireEvent.click(createButton);

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledWith({ planType: "Execution", name: "Delivery plan", description: "Phase one" }));
    expect(onCreated).toHaveBeenCalledWith("plan-new");
    expect(onCancel).not.toHaveBeenCalled();
  });

  it("keeps mutation failures visible and lets the user cancel the inline form", async () => {
    const onCancel = vi.fn();
    const mutation = { mutateAsync: vi.fn().mockRejectedValue(new Error("server error")), isPending: false, error: null as unknown };
    const { rerender } = render(<PlanningCreatePlanPanel mutation={mutation} t={t} onCreated={vi.fn()} onCancel={onCancel} />);
    fireEvent.change(screen.getByLabelText("projects.planning.planName"), { target: { value: "Delivery plan" } });
    fireEvent.click(screen.getByRole("button", { name: "projects.planning.createPlan" }));
    await waitFor(() => expect(mutation.mutateAsync).toHaveBeenCalled());

    rerender(<PlanningCreatePlanPanel mutation={{ ...mutation, error: new Error("server error") }} t={t} onCreated={vi.fn()} onCancel={onCancel} />);
    expect(screen.getByRole("alert")).toHaveTextContent("server error");
    fireEvent.click(screen.getByRole("button", { name: "projects.planning.cancelCreatePlan" }));
    expect(onCancel).toHaveBeenCalledOnce();
  });
});
