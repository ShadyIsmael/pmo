import * as React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { I18nProvider, useI18n } from "../i18n/messages";

const expectedTitles = {
  "projects.title": ["Project Registry", "سجل المشاريع"],
  "projects.registryDescription": ["Browse and manage projects across your organization.", "استعرض مشاريع المؤسسة وأدرها."],
  "projects.loadingProjectDetails": ["Loading project details...", "جارٍ تحميل تفاصيل المشروع..."],
  "projects.description": ["Description", "الوصف"],
  "projects.tabs.financials": ["Financials", "الشؤون المالية"],
  "projects.tabs.risksIssues": ["Risks & Issues", "المخاطر والمشكلات"],
  "projects.tabs.handover": ["Manager handover", "تسليم الإدارة"],
  "portfolioManager.myPortfolios": ["My Portfolios", "محافظي"],
  "portfolioManager.portfolioWorkspace": ["Portfolio workspace", "مساحة عمل المحفظة"],
  "portfolioManager.financials": ["Financials", "الشؤون المالية"],
  "portfolioManager.risksIssues": ["Risks & Issues", "المخاطر والمشكلات"],
  "programManager.myPrograms": ["My Programs", "برامجي"],
  "programManager.delivery": ["Delivery", "التنفيذ"],
  "programManager.financials": ["Financials", "الشؤون المالية"],
  "programManager.risksIssues": ["Risks & Issues", "المخاطر والمشكلات"],
  "programManager.meetingsActivity": ["Meetings / Activity", "الاجتماعات والنشاط"],
} as const;

function TitleTranslationsProbe() {
  const { locale, setLocale, t } = useI18n();

  return (
    <>
      <button type="button" onClick={() => setLocale("en")}>EN</button>
      <button type="button" onClick={() => setLocale("ar")}>AR</button>
      <span data-testid="active-locale">{locale}</span>
      {Object.keys(expectedTitles).map((key) => (
        <output key={key} data-testid={key}>{t(key)}</output>
      ))}
    </>
  );
}

describe("Project, Portfolio, and Program localization", () => {
  it("uses the reviewed English and Arabic titles without changing shared field labels", () => {
    render(<I18nProvider><TitleTranslationsProbe /></I18nProvider>);

    for (const [key, [english]] of Object.entries(expectedTitles)) {
      expect(screen.getByTestId(key).textContent).toBe(english);
    }

    fireEvent.click(screen.getByRole("button", { name: "AR" }));

    for (const [key, [, arabic]] of Object.entries(expectedTitles)) {
      expect(screen.getByTestId(key).textContent).toBe(arabic);
    }
  });
});
