import * as React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { I18nProvider, useI18n } from "../i18n/messages";
import { ThemeProvider, useTheme } from "../theme/ThemeProvider";

function Probe() { const { setLocale, t } = useI18n(); const { toggleTheme } = useTheme(); return <><button onClick={() => setLocale("ar")}>AR</button><button onClick={toggleTheme}>Theme</button><span>{t("home")}</span></>; }

describe("locale and theme shell", () => {
  it("supports Arabic RTL and dark theme", async () => {
    render(<ThemeProvider><I18nProvider><Probe /></I18nProvider></ThemeProvider>);
    fireEvent.click(screen.getByRole("button", { name: "AR" }));
    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.getByText("الرئيسية")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Theme" }));
    expect(document.documentElement.dataset.theme).toBe("dark");
  });
});
