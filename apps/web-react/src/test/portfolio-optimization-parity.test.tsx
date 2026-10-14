import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { useManagedPortfolios, usePortfolioManagementOverview } from "../features/delivery/api/delivery";
import { PortfolioOptimizationPage } from "../features/delivery/PortfolioOptimizationPage";

vi.mock("../features/delivery/api/delivery", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/delivery/api/delivery")>();
  return { ...actual, useManagedPortfolios: vi.fn(), usePortfolioManagementOverview: vi.fn() };
});
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "test-user" } }) }));
vi.mock("@api/management", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@api/management")>();
  return { ...actual, useManagement: () => ({ context: null, loading: false }) };
});

const managedPortfoliosMock = vi.mocked(useManagedPortfolios);
const portfolioOverviewMock = vi.mocked(usePortfolioManagementOverview);

function renderPage() {
  return render(<MemoryRouter><I18nProvider><PortfolioOptimizationPage /></I18nProvider></MemoryRouter>);
}

describe("portfolio optimization parity", () => {
  beforeEach(() => {
    managedPortfoliosMock.mockReset();
    portfolioOverviewMock.mockReset();
    managedPortfoliosMock.mockReturnValue({ data: { items: [] }, isLoading: false, isError: false } as unknown as ReturnType<typeof useManagedPortfolios>);
    portfolioOverviewMock.mockReturnValue({ data: undefined, isLoading: false, isError: false } as unknown as ReturnType<typeof usePortfolioManagementOverview>);
  });

  it("preserves the source scenario and constraints tabs without fabricating optimizer results", () => {
    renderPage();

    expect(screen.getByRole("tab", { name: "What-If Scenarios" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByText("Scenario simulation requires a configured optimization service.")).toBeInTheDocument();
    expect(screen.queryByText("72/100")).not.toBeInTheDocument();
    expect(screen.queryByText("Balanced Growth")).not.toBeInTheDocument();
    expect(screen.queryByText("18.5%")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "Constraints" }));

    expect(screen.getByRole("tab", { name: "Constraints" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByText("No optimization constraints are exposed by the current API.")).toBeInTheDocument();
    expect(screen.queryByText("120 FTE")).not.toBeInTheDocument();
  });
});
