import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { ApiError } from "@api/client";
import { I18nProvider } from "../i18n/messages";
import { RiskActionFollowUpPage } from "../features/risks/RiskActionFollowUpPage";

const mocks = vi.hoisted(() => ({
  target: vi.fn(),
  riskDetail: vi.fn((_props: unknown) => null),
}));

vi.mock("../features/risks/api/risks", () => ({ useRiskActionFollowUpTarget: mocks.target }));
vi.mock("../features/risks/RiskDetailPage", () => ({ RiskDetailPage: mocks.riskDetail }));

function CurrentPath() {
  const location = useLocation();
  return <div data-testid="current-path">{location.pathname}{location.search}</div>;
}

function renderFollowUpPage(actionPublicId = "action-public-id") {
  return render(
    <MemoryRouter initialEntries={[`/risks-issues/risk-action-follow-up/${actionPublicId}`]}>
      <I18nProvider>
        <CurrentPath />
        <Routes>
          <Route path="/risks-issues/risk-action-follow-up/:actionPublicId" element={<RiskActionFollowUpPage />} />
        </Routes>
      </I18nProvider>
    </MemoryRouter>,
  );
}

describe("Risk Action follow-up route", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.target.mockReset();
    mocks.riskDetail.mockClear();
  });

  it("shows localized loading and unavailable states", () => {
    mocks.target.mockReturnValue({ isLoading: true, isError: false });
    const loading = renderFollowUpPage();
    expect(screen.getByRole("status")).toHaveTextContent("Loading...");
    loading.unmount();

    mocks.target.mockReturnValue({ isLoading: false, isError: true, error: new ApiError(404, "not found") });
    renderFollowUpPage();
    expect(screen.getByRole("alert")).toHaveTextContent("Risk not found.");
  });

  it("renders the existing risk detail with resolved internal IDs and the public project ID", () => {
    mocks.target.mockReturnValue({
      data: { projectPublicId: "project-public-id", riskId: "risk-id", actionId: "action-id" },
      isLoading: false,
      isError: false,
    });

    renderFollowUpPage();

    expect(mocks.target).toHaveBeenCalledWith("action-public-id", true);
    expect(mocks.riskDetail.mock.calls[0][0]).toEqual({
      riskIdOverride: "risk-id",
      actionIdOverride: "action-id",
      projectPublicIdOverride: "project-public-id",
    });
    expect(screen.getByTestId("current-path")).toHaveTextContent("/risks-issues/risk-action-follow-up/action-public-id");
  });

  it("uses the common localized error when target resolution fails unexpectedly", () => {
    mocks.target.mockReturnValue({ isLoading: false, isError: true, error: new Error("network") });
    renderFollowUpPage();

    expect(screen.getByRole("alert")).toHaveTextContent("Failed to load data.");
  });
});
