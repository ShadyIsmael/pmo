import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ApiError } from "../api/client";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { MemoryRouter } from "react-router-dom";
import { PortfolioSheet } from "../features/delivery/PortfolioSheet";
import { ManagedPortfoliosPage } from "../features/delivery/PortfolioProgramPages";

const fixtures = vi.hoisted(() => ({
  create: vi.fn(),
  update: vi.fn(),
  managerCandidates: [{ id: "manager-1", fullName: "Nexus Manager" }],
  managerLookupError: null as unknown,
  managerLookupRefetch: vi.fn(),
  permissions: ["Portfolio.Create", "Portfolio.Edit", "Handover.AdminInitiate"],
  canMutate: true,
  effectiveReadOnly: false,
  portfolios: [{
    id: "portfolio-1", name: "Delivery Transformation", code: "PF-001", ownerName: "Nexus Manager", status: "Active", health: "Amber", currency: "USD",
    programCount: 2, directProjectCount: 1, totalProjectCount: 3, projectsRequiringAttention: 1, programsRequiringAttention: 2,
    riskIndicator: "NeedsAttention", reportingIndicator: "Missing",
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
  usePortfolioWorkflowManagerLookup: () => ({ data: fixtures.managerLookupError ? undefined : fixtures.managerCandidates, isLoading: false, isError: Boolean(fixtures.managerLookupError), error: fixtures.managerLookupError, refetch: fixtures.managerLookupRefetch }),
  useWorkflowCurrencyLookup: () => ({ data: [{ id: "currency-usd", code: "USD", name: "US Dollar", symbol: "$", isBase: true }] }),
  toUserSelectOptions: (users: { id: string; fullName: string }[] | undefined) => (users ?? []).map((user) => ({ value: user.id, label: user.fullName, keywords: [] })),
}));

function renderSheet(canSave = true, portfolio?: { id: string; name: string; description?: string; ownerId?: string; status?: string; currency?: string; code?: string }) {
  const onOpenChange = vi.fn();
  render(<I18nProvider><PortfolioSheet open onOpenChange={onOpenChange} portfolio={portfolio} canSave={canSave} canAssignInitialManager /></I18nProvider>);
  return onOpenChange;
}

describe("portfolio management sheet", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.create.mockReset().mockResolvedValue({ id: "portfolio-1" });
    fixtures.update.mockReset().mockResolvedValue({ id: "portfolio-1" });
    fixtures.managerCandidates = [{ id: "manager-1", fullName: "Nexus Manager" }];
    fixtures.managerLookupError = null;
    fixtures.managerLookupRefetch.mockReset();
    fixtures.permissions = ["Portfolio.Create", "Portfolio.Edit", "Handover.AdminInitiate"];
    fixtures.canMutate = true;
    fixtures.effectiveReadOnly = false;
  });

  it("creates a Portfolio from an eligible System User without requiring a Resource", async () => {
    const onOpenChange = renderSheet();
    fireEvent.change(screen.getByLabelText("Name *"), { target: { value: "Delivery Transformation" } });
    fireEvent.click(screen.getByText("Select portfolio manager"));
    fireEvent.click(screen.getByRole("option", { name: "Nexus Manager" }));
    fireEvent.click(screen.getByRole("button", { name: "Save Portfolio" }));

    await waitFor(() => expect(fixtures.create).toHaveBeenCalledWith({
      name: "Delivery Transformation",
      description: "",
      ownerId: "manager-1",
      status: "Active",
      currency: "USD",
    }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(fixtures.create.mock.calls[0]?.[0]).not.toHaveProperty("resourceId");
  });

  it("shows localized empty and authorization states for Portfolio Manager candidates", () => {
    fixtures.managerCandidates = [];
    renderSheet();
    fireEvent.click(screen.getByText("Select portfolio manager"));
    expect(screen.getByText("No eligible active Portfolio Managers found")).toBeInTheDocument();

    cleanup();
    fixtures.managerLookupError = new ApiError(403, "Forbidden");
    renderSheet();
    expect(screen.getByRole("alert")).toHaveTextContent("You are not authorized to load Portfolio Manager candidates for this action.");

    cleanup();
    fixtures.managerLookupError = new ApiError(403, "Forbidden");
    window.localStorage.setItem("nexus-locale", "ar");
    renderSheet();
    expect(screen.getByRole("alert")).toHaveTextContent("غير مصرح لك بتحميل مرشحي مدير المحفظة لهذا الإجراء.");
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

  it("shows create only with Portfolio.Create in a mutable management context", () => {
    const renderList = () => render(<I18nProvider><MemoryRouter><ManagedPortfoliosPage /></MemoryRouter></I18nProvider>);

    renderList();
    expect(screen.getByRole("button", { name: "Create Portfolio" })).toBeInTheDocument();

    cleanup();
    fixtures.permissions = [];
    renderList();
    expect(screen.queryByRole("button", { name: "Create Portfolio" })).not.toBeInTheDocument();

    cleanup();
    fixtures.permissions = ["Portfolio.Create"];
    fixtures.effectiveReadOnly = true;
    renderList();
    expect(screen.queryByRole("button", { name: "Create Portfolio" })).not.toBeInTheDocument();
  });

  it("shows the source-aligned live portfolio fields and localized indicators", () => {
    render(<I18nProvider><MemoryRouter><ManagedPortfoliosPage /></MemoryRouter></I18nProvider>);

    for (const heading of ["Portfolio", "Portfolio Manager", "Status", "Health", "Programs", "Direct Projects", "Total Projects", "Attention", "Risk", "Reporting"]) {
      expect(screen.getByRole("columnheader", { name: heading })).toBeInTheDocument();
    }
    expect(screen.getByText("PF-001 · USD")).toBeInTheDocument();
    expect(screen.getByText("Needs attention")).toBeInTheDocument();
    expect(screen.getByText("Missing")).toBeInTheDocument();
  });

  it("localizes portfolio statuses and indicators in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    render(<I18nProvider><MemoryRouter><ManagedPortfoliosPage /></MemoryRouter></I18nProvider>);

    expect(screen.getByRole("columnheader", { name: "مشروعات مباشرة" })).toBeInTheDocument();
    expect(screen.getByText("نشط")).toBeInTheDocument();
    expect(screen.getByText("يتطلب الانتباه")).toBeInTheDocument();
    expect(screen.getByText("ناقص")).toBeInTheDocument();
  });
});
