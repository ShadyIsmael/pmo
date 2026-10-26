import * as React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { useConfirm } from "@components/ui/ConfirmProvider";
import { I18nProvider } from "@i18n/messages";

function ConfirmationHarness() {
  const confirm = useConfirm();
  const [result, setResult] = React.useState<string>("waiting");

  async function askForConfirmation() {
    const accepted = await confirm({ description: "Delete the selected item?", destructive: true });
    setResult(accepted ? "accepted" : "cancelled");
  }

  return <><button type="button" onClick={() => void askForConfirmation()}>Open confirmation</button><output>{result}</output></>;
}

describe("shared confirmation provider", () => {
  it("uses one accessible destructive confirmation dialog and resolves true on confirmation", async () => {
    const user = userEvent.setup();
    render(<I18nProvider><ConfirmationHarness /></I18nProvider>);
    const trigger = screen.getByRole("button", { name: "Open confirmation" });

    await user.click(trigger);
    const dialog = screen.getByRole("alertdialog", { name: "Confirm action" });
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(dialog).toHaveTextContent("Delete the selected item?");
    expect(screen.getByRole("button", { name: "Cancel" })).toHaveFocus();
    expect(screen.getByRole("button", { name: "Confirm" })).toHaveClass("border-destructive");

    await user.click(screen.getByRole("button", { name: "Confirm" }));
    expect(await screen.findByText("accepted")).toBeInTheDocument();
    expect(trigger).toHaveFocus();
  });

  it("resolves false when dismissed with Escape", async () => {
    const user = userEvent.setup();
    render(<I18nProvider><ConfirmationHarness /></I18nProvider>);

    await user.click(screen.getByRole("button", { name: "Open confirmation" }));
    await user.keyboard("{Escape}");

    expect(await screen.findByText("cancelled")).toBeInTheDocument();
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
  });

  it("uses centralized Arabic title and action labels", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    render(<I18nProvider><ConfirmationHarness /></I18nProvider>);
    fireEvent.click(screen.getByRole("button", { name: "Open confirmation" }));

    expect(screen.getByRole("alertdialog", { name: "تأكيد الإجراء" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "تأكيد" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إلغاء" })).toBeInTheDocument();
  });
});
