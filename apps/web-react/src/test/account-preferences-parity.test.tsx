import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ThemeProvider } from "../theme/ThemeProvider";
import { PreferencesPage } from "../features/account/AccountPages";

describe("account preferences source parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    window.localStorage.setItem("nexus-theme", "light");
  });

  it("renders the Display card and an accessible Compact Mode switch in EN and AR", () => {
    render(<ThemeProvider><I18nProvider><PreferencesPage /></I18nProvider></ThemeProvider>);

    expect(screen.getByText("Display")).toBeInTheDocument();
    expect(screen.getByText("Choose how NEXUS PMO appears for you.")).toBeInTheDocument();
    const compactMode = screen.getByRole("switch", { name: "Compact mode" });
    expect(compactMode).toHaveAttribute("aria-checked", "false");
    fireEvent.click(compactMode);
    expect(compactMode).toHaveAttribute("aria-checked", "true");

    fireEvent.change(screen.getByRole("combobox", { name: "Language" }), { target: { value: "ar" } });
    expect(screen.getByRole("switch", { name: "الوضع المضغوط" })).toHaveAttribute("aria-checked", "true");
    expect(screen.getByText("اختر طريقة ظهور NEXUS PMO لك.")).toBeInTheDocument();
  });
});
