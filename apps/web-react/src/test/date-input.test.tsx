import * as React from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DateInput } from "@components/ui/DateInput";
import { Input } from "@components/ui/Input";

afterEach(cleanup);

function ControlledDateInput(props: Partial<React.ComponentProps<typeof DateInput>> = {}) {
  const [value, setValue] = React.useState(props.value ?? "");
  return <DateInput aria-label="Deadline" {...props} value={value} onChange={(event) => setValue(event.target.value)} />;
}

describe("DateInput", () => {
  it("keeps letters out of the field and formats digits with a dd/mm/yyyy mask", async () => {
    const user = userEvent.setup();
    render(<ControlledDateInput />);
    const field = screen.getByLabelText("Deadline");

    await user.type(field, "abc");
    expect(field).toHaveValue("");

    await user.type(field, "1512");
    expect(field).toHaveValue("15/12");

    await user.type(field, "2026");
    expect(field).toHaveValue("15/12/2026");
  });

  it("commits the ISO date only once the typed date is complete and valid", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<DateInput aria-label="Deadline" value="" onChange={onChange} />);
    const field = screen.getByLabelText("Deadline");

    await user.type(field, "1512");
    expect(onChange).not.toHaveBeenCalled();

    await user.type(field, "2026");
    expect(onChange).toHaveBeenCalled();
    expect(onChange.mock.calls.at(-1)?.[0].target.value).toBe("2026-12-15");
  });

  it("rejects impossible calendar dates and restores the last valid value on blur", () => {
    render(<ControlledDateInput value="2026-12-15" />);
    const field = screen.getByLabelText("Deadline");

    fireEvent.change(field, { target: { value: "31/02/2026" } });
    expect(field).toHaveValue("31/02/2026");

    fireEvent.blur(field);
    expect(field).toHaveValue("15/12/2026");
  });

  it("accepts a full ISO date pasted into the field", () => {
    const onChange = vi.fn();
    render(<DateInput aria-label="Deadline" value="" onChange={onChange} />);

    fireEvent.change(screen.getByLabelText("Deadline"), { target: { value: "2026-12-15" } });

    expect(onChange).toHaveBeenCalled();
    expect(onChange.mock.calls.at(-1)?.[0].target.value).toBe("2026-12-15");
    expect(screen.getByLabelText("Deadline")).toHaveValue("15/12/2026");
  });

  it("renders a date-only control through the shared Input component", () => {
    render(<Input type="date" aria-label="Deadline" value="2026-12-15" onChange={() => undefined} />);

    expect(screen.getByLabelText("Deadline")).toHaveAttribute("inputmode", "numeric");
    expect(screen.getByLabelText("Deadline")).not.toHaveAttribute("type", "date");
  });

  it("opens a calendar and commits the selected day", async () => {
    const user = userEvent.setup();
    render(<ControlledDateInput value="2026-12-01" />);

    await user.click(screen.getByRole("button", { name: "Open calendar" }));
    await user.click(await screen.findByRole("button", { name: "2026-12-15" }));

    expect(screen.getByLabelText("Deadline")).toHaveValue("15/12/2026");
  });

  it("disables calendar days outside the min and max bounds", async () => {
    const user = userEvent.setup();
    render(<ControlledDateInput value="2026-12-15" min="2026-12-10" max="2026-12-20" />);

    await user.click(screen.getByRole("button", { name: "Open calendar" }));

    expect(await screen.findByRole("button", { name: "2026-12-09" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "2026-12-15" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "2026-12-21" })).toBeDisabled();
  });

  it("exposes the ISO value to native form submission", () => {
    const { container } = render(
      <form>
        <DateInput aria-label="Deadline" name="dueDate" defaultValue="2026-12-15" />
      </form>
    );

    const form = container.querySelector("form");
    expect(new FormData(form as HTMLFormElement).get("dueDate")).toBe("2026-12-15");
  });

  it("reports the field as required while it is empty", () => {
    render(<DateInput aria-label="Deadline" required value="" onChange={() => undefined} />);

    expect(screen.getByLabelText("Deadline")).not.toHaveValue();
    expect(screen.getByLabelText("Deadline")).toBeRequired();
  });
});
