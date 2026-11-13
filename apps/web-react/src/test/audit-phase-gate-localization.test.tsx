import * as React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { I18nProvider, useI18n } from "../i18n/messages";

const auditEntityKeys = ["audit.entityTypes.ProjectPhase", "audit.entityTypes.ProjectPhaseGateEvidence"] as const;

function AuditEntityTranslationProbe() {
  const { locale, setLocale, t } = useI18n();
  return <>
    <button type="button" onClick={() => setLocale("en")}>EN</button>
    <button type="button" onClick={() => setLocale("ar")}>AR</button>
    <span data-testid="locale">{locale}</span>
    {auditEntityKeys.map(key => <output key={key} data-testid={key}>{t(key)}</output>)}
  </>;
}

describe("Phase Gate audit entity localization", () => {
  it("labels project phase and evidence events in English and Arabic", () => {
    render(<I18nProvider><AuditEntityTranslationProbe /></I18nProvider>);

    expect(screen.getByTestId(auditEntityKeys[0]).textContent).toBe("Project phase");
    expect(screen.getByTestId(auditEntityKeys[1]).textContent).toBe("Phase gate evidence");
    fireEvent.click(screen.getByRole("button", { name: "AR" }));
    expect(screen.getByTestId("locale").textContent).toBe("ar");
    for (const key of auditEntityKeys) {
      const label = screen.getByTestId(key).textContent ?? "";
      expect(label.length).toBeGreaterThan(0);
      expect(label).not.toBe(key);
      expect(label).not.toBe(key.split(".").at(-1));
    }
  });
});
