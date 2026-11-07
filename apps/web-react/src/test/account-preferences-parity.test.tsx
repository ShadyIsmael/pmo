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

  it("persists theme and locale through their existing providers in EN and AR", () => {
    render(<ThemeProvider><I18nProvider><PreferencesPage /></I18nProvider></ThemeProvider>);

    expect(screen.getByText("Display")).toBeInTheDocument();
    expect(screen.getByText("Choose how FOPX PM appears for you.")).toBeInTheDocument();
    expect(screen.getByText("A compact layout preference is not currently supported by this application.")).toBeInTheDocument();
    fireEvent.change(screen.getByRole("combobox", { name: "Theme" }), { target: { value: "dark" } });
    expect(window.localStorage.getItem("nexus-theme")).toBe("dark");

    fireEvent.change(screen.getByRole("combobox", { name: "Language" }), { target: { value: "ar" } });
    expect(screen.getByText("اختر طريقة ظهور FOPX PM لك.")).toBeInTheDocument();
    expect(window.localStorage.getItem("nexus-locale")).toBe("ar");
    expect(document.documentElement.dir).toBe("rtl");
  });
});
