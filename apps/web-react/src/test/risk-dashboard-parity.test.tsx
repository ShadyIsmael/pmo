import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { RiskDashboardPage } from "../features/risks/RiskDashboardPage";

const fixtures = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock("@api/client", () => ({ api: { get: fixtures.get }, setApiErrorLocale: () => undefined }));

function risk(overrides: Record<string, unknown> = {}) {
  return {
    id: "risk-default",
    projectId: "project-default",
    projectName: "Default Project",
    title: "Default risk",
    description: "Risk description",
    riskLevel: "Low",
    status: "Identified",
    createdAt: "2026-10-01T10:00:00.000Z",
    updatedAt: "2026-10-01T10:00:00.000Z",
    rowVersion: [1],
    ...overrides,
  };
}

function renderDashboard() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<I18nProvider><QueryClientProvider client={queryClient}><MemoryRouter initialEntries={["/risks-issues/risks/dashboard"]}><Routes><Route path="*" element={<RiskDashboardPage />} /></Routes></MemoryRouter></QueryClientProvider></I18nProvider>);
}

describe("risk dashboard parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.get.mockReset().mockResolvedValue({
      items: [
        risk({ id: "risk-critical", projectName: "Critical Project", title: "Critical exposure", riskLevel: " CRITICAL ", status: "Action Required", lastActionUpdateAt: "2026-10-09T13:45:00.000Z", lastActionUpdateText: "Escalated to steering committee" }),
        risk({ id: "risk-high", projectName: "High Project", title: "High exposure", riskLevel: "HIGH", status: "Follow-Up", lastActionUpdateAt: "2026-10-08T09:30:00.000Z", lastActionUpdateText: "Supplier response pending" }),
        risk({ id: "risk-low", projectName: "Low Project", title: "Low exposure", riskLevel: "Low", status: "Closed" }),
      ],
      totalCount: 3,
      totalPages: 1,
    });
  });

  it("normalizes incoming enum values for counts, localized labels, and top-risk ordering", async () => {
    renderDashboard();

    expect(await screen.findByText("Risk Dashboard")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Critical exposure.*Critical Project.*Critical/ })).toHaveAttribute("href", "/risks-issues/risks/risk-critical?from=risks");
    const actionRequiredLabels = screen.getAllByText("Action required");
    expect(actionRequiredLabels.at(-1)?.parentElement).toHaveTextContent("1");
  });

  it("localizes risk levels and statuses in Arabic", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderDashboard();

    expect(await screen.findByText("لوحة المخاطر")).toBeInTheDocument();
    expect(screen.getAllByText("حرج").length).toBeGreaterThan(0);
    expect(screen.getAllByText("يتطلب إجراءً").at(-1)?.parentElement).toHaveTextContent("1");
  });

  it("shows localized update date/time and project context while retaining the risk return context", async () => {
    const { container } = renderDashboard();

    await screen.findByText("Risk Dashboard");
    const recentLink = Array.from(container.querySelectorAll<HTMLAnchorElement>('a[href="/risks-issues/risks/risk-critical?from=risks"]'))
      .find((link) => link.textContent?.includes("Escalated to steering committee"));
    expect(recentLink).toBeInTheDocument();
    expect(recentLink).toHaveTextContent("Escalated to steering committee");
    expect(recentLink).toHaveTextContent("Critical Project");
    expect(recentLink?.querySelector("time")).toHaveAttribute("dateTime", "2026-10-09T13:45:00.000Z");
  });
});
