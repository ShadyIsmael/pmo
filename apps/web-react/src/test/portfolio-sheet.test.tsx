import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { MemoryRouter } from "react-router-dom";
import { PortfolioSheet } from "../features/delivery/PortfolioSheet";
import { ManagedPortfoliosPage } from "../features/delivery/PortfolioProgramPages";

const fixtures = vi.hoisted(() => ({
  create: vi.fn(),
  update: vi.fn(),
  permissions: ["Portfolio.Manage"],
  canMutate: true,
  effectiveReadOnly: false,
  portfolios: [{
    id: "portfolio-1", name: "Delivery Transformation", code: "PF-001", ownerName: "Nexus Manager", status: "Active", health: "Amber", currency: "USD",
    programCount: 2, directProjectCount: 1, totalProjectCount: 3, projectsRequiringAttention: 1, programsRequiringAttention: 2,
    financialIndicator: "Available", riskIndicator: "NeedsAttention", reportingIndicator: "Missing",
  }],
}));

vi.mock("@features/delivery/api/delivery", () => ({
  useManagedPortfolios: () => ({ data: { items: fixtures.portfolios }, isLoading: false, isError: false }),
  useCreatePortfolio: () => ({ mutateAsync: fixtures.create, isPending: false }),
  useUpdatePortfolio: () => ({ mutateAsync: fixtures.update, isPending: false }),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));
vi.mock("@api/management", () => ({
  managementCacheScopeKey: (userId?: string) => userId ?? "anonymous",
  useManagement: () => ({ canMutate: fixtures.canMutate, effectiveReadOnly: fixtures.effectiveReadOnly }),
}));

vi.mock("@features/projects/lib/project-lookups", () => ({
  useUserLookup: () => ({ data: [{ id: "manager-1", fullName: "Nexus Manager", email: "manager@nexus.com" }], isLoading: false }),
  toUserSelectOptions: (users: { id: string; fullName: string; email: string }[] | undefined) => (users ?? []).map((user) => ({ value: user.id, label: `${user.fullName} · ${user.email}`, keywords: [] })),
}));

vi.mock("@features/administration/api/admin", () => ({
  useCurrencies: () => ({ data: [{ id: "currency-usd", code: "USD", nameEn: "US Dollar", nameAr: "دولار أمريكي", isActive: true }] }),
}));

function renderSheet(canManage = true, portfolio?: { id: string; name: string; description?: string; ownerId?: string; status?: string; currency?: string; code?: string }) {
  const onOpenChange = vi.fn();
  render(<I18nProvider><PortfolioSheet open onOpenChange={onOpenChange} portfolio={portfolio} canManage={canManage} /></I18nProvider>);
  return onOpenChange;
}

describe("portfolio management sheet", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.create.mockReset().mockResolvedValue({ id: "portfolio-1" });
    fixtures.update.mockReset().mockResolvedValue({ id: "portfolio-1" });
    fixtures.permissions = ["Portfolio.Manage"];
    fixtures.canMutate = true;
    fixtures.effectiveReadOnly = false;
  });

  it("creates a portfolio with its required manager and active currency", async () => {
    const onOpenChange = renderSheet();
    fireEvent.change(screen.getByLabelText("Name *"), { target: { value: "Delivery Transformation" } });
    fireEvent.click(screen.getByText("Select portfolio manager"));
    fireEvent.click(screen.getByRole("option", { name: "Nexus Manager · manager@nexus.com" }));
    fireEvent.click(screen.getByRole("button", { name: "Save Portfolio" }));

    await waitFor(() => expect(fixtures.create).toHaveBeenCalledWith({
      name: "Delivery Transformation",
      description: "",
      ownerId: "manager-1",
      status: "Active",
      currency: "USD",
    }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("loads and updates an existing portfolio", async () => {
    const onOpenChange = renderSheet(true, { id: "portfolio-1", name: "Existing Portfolio", description: "Original", ownerId: "manager-1", status: "Active", currency: "USD", code: "PF-001" });
    expect(screen.getByLabelText("Name *")).toHaveValue("Existing Portfolio");
    fireEvent.change(screen.getByLabelText("Name *"), { target: { value: "Updated Portfolio" } });
    fireEvent.click(screen.getByRole("button", { name: "Save Portfolio" }));

    await waitFor(() => expect(fixtures.update).toHaveBeenCalledWith({
      id: "portfolio-1",
      dto: { name: "Updated Portfolio", description: "Original", ownerId: "manager-1", status: "Active", currency: "USD" },
    }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("does not allow a read-only or unauthorized context to save", () => {
    renderSheet(false);
    expect(screen.getByRole("button", { name: "Save Portfolio" })).toBeDisabled();
    expect(fixtures.create).not.toHaveBeenCalled();
  });

  it("shows create only with Portfolio.Manage in a mutable management context", () => {
    const renderList = () => render(<I18nProvider><MemoryRouter><ManagedPortfoliosPage /></MemoryRouter></I18nProvider>);

    renderList();
    expect(screen.getByRole("button", { name: "Create Portfolio" })).toBeInTheDocument();

    cleanup();
    fixtures.permissions = [];
    renderList();
    expect(screen.queryByRole("button", { name: "Create Portfolio" })).not.toBeInTheDocument();

    cleanup();
    fixtures.permissions = ["Portfolio.Manage"];
    fixtures.effectiveReadOnly = true;
    renderList();
    expect(screen.queryByRole("button", { name: "Create Portfolio" })).not.toBeInTheDocument();
  });

  it("shows the source-aligned live portfolio fields and localized indicators", () => {
    render(<I18nProvider><MemoryRouter><ManagedPortfoliosPage /></MemoryRouter></I18nProvider>);

    for (const heading of ["Portfolio", "Portfolio Manager", "Status", "Health", "Programs", "Direct Projects", "Total Projects", "Attention", "Financials", "Risk", "Reporting"]) {
      expect(screen.getByRole("columnheader", { name: heading })).toBeInTheDocument();
    }
    expect(screen.getByText("PF-001 · USD")).toBeInTheDocument();
    expect(screen.getByText("Available")).toBeInTheDocument();
    expect(screen.getByText("Needs attention")).toBeInTheDocument();
    expect(screen.getByText("Missing")).toBeInTheDocument();
  });

  it("localizes portfolio statuses and indicators in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    render(<I18nProvider><MemoryRouter><ManagedPortfoliosPage /></MemoryRouter></I18nProvider>);

    expect(screen.getByRole("columnheader", { name: "مشروعات مباشرة" })).toBeInTheDocument();
    expect(screen.getByText("نشط")).toBeInTheDocument();
    expect(screen.getByText("متاح")).toBeInTheDocument();
    expect(screen.getByText("يتطلب الانتباه")).toBeInTheDocument();
    expect(screen.getByText("ناقص")).toBeInTheDocument();
  });
});
