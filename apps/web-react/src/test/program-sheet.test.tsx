import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ApiError } from "../api/client";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { ProgramSheet } from "../features/delivery/ProgramSheet";
import { ManagedProgramsPage } from "../features/delivery/PortfolioProgramPages";

const fixtures = vi.hoisted(() => ({
  create: vi.fn(),
  update: vi.fn(),
  managerCandidates: [{ id: "manager-1", fullName: "Nexus Manager" }],
  managerLookupError: null as unknown,
  managerLookupRefetch: vi.fn(),
  permissions: ["Program.Create", "Handover.AdminInitiate"],
  canMutate: true,
  effectiveReadOnly: false,
  programs: [{
    id: "program-1", name: "Cloud Modernization", code: "PGM-001", health: "Amber", status: "Active",
    programManagerName: "Nexus Manager", portfolioName: "Technology", startDate: "2026-03-01T00:00:00Z", endDate: "2026-12-31T00:00:00Z", currency: "EUR",
    projectCount: 4, projectsRequiringAttention: 2, scheduleIndicator: "Delayed",
  }],
}));

vi.mock("@features/delivery/api/delivery", () => ({
  useManagedPrograms: () => ({ data: { items: fixtures.programs }, isLoading: false, isError: false }),
  useEligibleProgramPortfolios: () => ({ data: { items: [{ id: "portfolio-1", name: "Technology", code: "PORT-01", currency: "EUR", status: "Active" }] }, isFetching: false, isError: false }),
  useEditableProgramPortfolios: () => ({ data: { items: [
    { id: "portfolio-1", name: "Technology", code: "PORT-01", currency: "EUR", status: "Active" },
    { id: "portfolio-2", name: "Research", code: "PORT-02", currency: "USD", status: "Active" },
  ] }, isFetching: false, isError: false }),
  useCreateProgram: () => ({ mutateAsync: fixtures.create, isPending: false }),
  useUpdateProgram: () => ({ mutateAsync: fixtures.update, isPending: false }),
  useArchiveProgram: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useReactivateProgram: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));
vi.mock("@api/management", () => ({
  managementCacheScopeKey: (userId?: string) => userId ?? "anonymous",
  useManagement: () => ({ canMutate: fixtures.canMutate, effectiveReadOnly: fixtures.effectiveReadOnly }),
}));

vi.mock("@features/projects/lib/project-lookups", () => ({
  useProgramWorkflowManagerLookup: () => ({ data: fixtures.managerLookupError ? undefined : fixtures.managerCandidates, isLoading: false, isError: Boolean(fixtures.managerLookupError), error: fixtures.managerLookupError, refetch: fixtures.managerLookupRefetch }),
  usePortfolioLookup: () => ({ data: { items: [{ id: "portfolio-1", name: "Technology", code: "PORT-01", currency: "EUR", status: "Active" }] }, isLoading: false }),
  useWorkflowCurrencyLookup: () => ({ data: [
    { id: "currency-usd", code: "USD", name: "US Dollar", symbol: "$", isBase: true },
    { id: "currency-eur", code: "EUR", name: "Euro", symbol: "€", isBase: false },
  ] }),
  toUserSelectOptions: (users: { id: string; fullName: string }[] | undefined) => (users ?? []).map((user) => ({ value: user.id, label: user.fullName, keywords: [] })),
}));

function renderSheet(canSave = true, program?: { id: string; name: string; description?: string; portfolioId?: string; programManagerId?: string; status?: string; startDate?: string; endDate?: string; currency?: string; health?: string; code?: string; rowVersion?: number[] }) {
  const onOpenChange = vi.fn();
  render(<I18nProvider><ProgramSheet open onOpenChange={onOpenChange} program={program} canSave={canSave} canAssignInitialManager cacheScope="test" /></I18nProvider>);
  return onOpenChange;
}

describe("program management sheet", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.create.mockReset().mockResolvedValue({ id: "program-1" });
    fixtures.update.mockReset().mockResolvedValue({ id: "program-1" });
    fixtures.managerCandidates = [{ id: "manager-1", fullName: "Nexus Manager" }];
    fixtures.managerLookupError = null;
    fixtures.managerLookupRefetch.mockReset();
    fixtures.programs = [{
      id: "program-1", name: "Cloud Modernization", code: "PGM-001", health: "Amber", status: "Active",
      programManagerName: "Nexus Manager", portfolioName: "Technology", startDate: "2026-03-01T00:00:00Z", endDate: "2026-12-31T00:00:00Z", currency: "EUR",
      projectCount: 4, projectsRequiringAttention: 2, scheduleIndicator: "Delayed",
    }];
    fixtures.permissions = ["Program.Create", "Handover.AdminInitiate"];
    fixtures.canMutate = true;
    fixtures.effectiveReadOnly = false;
  });

  it("creates a Program from an eligible System User without requiring a Resource", async () => {
    const onOpenChange = renderSheet();
    expect(screen.queryByLabelText("Budget")).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Name *"), { target: { value: "Cloud Modernization" } });
    fireEvent.click(screen.getByRole("combobox", { name: "Portfolio" }));
    fireEvent.click(screen.getByRole("option", { name: "Technology (PORT-01)" }));
    fireEvent.click(screen.getAllByRole("combobox")[1]);
    fireEvent.click(screen.getByRole("option", { name: "Nexus Manager" }));
    fireEvent.click(screen.getByRole("button", { name: "Save Program" }));

    await waitFor(() => expect(fixtures.create).toHaveBeenCalledWith({
      name: "Cloud Modernization",
      description: undefined,
      portfolioId: "portfolio-1",
      programManagerId: "manager-1",
      status: "Planning",
      currency: "EUR",
    }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
    expect(fixtures.create.mock.calls[0]?.[0]).not.toHaveProperty("resourceId");
  });

  it("creates a Program without requiring an initial Program Manager", async () => {
    renderSheet();
    fireEvent.change(screen.getByLabelText("Name *"), { target: { value: "Unassigned Program" } });
    fireEvent.click(screen.getByRole("combobox", { name: "Portfolio" }));
    fireEvent.click(screen.getByRole("option", { name: "Technology (PORT-01)" }));
    fireEvent.click(screen.getByRole("button", { name: "Save Program" }));

    await waitFor(() => expect(fixtures.create).toHaveBeenCalledWith({
      name: "Unassigned Program",
      description: undefined,
      portfolioId: "portfolio-1",
      programManagerId: undefined,
      status: "Planning",
      currency: "EUR",
    }));
  });

  it("shows localized empty and authorization states for Program Manager candidates", () => {
    fixtures.managerCandidates = [];
    renderSheet();
    fireEvent.click(screen.getByRole("combobox", { name: "Manager" }));
    expect(screen.getByText("No eligible active Program Managers found")).toBeInTheDocument();

    cleanup();
    fixtures.managerLookupError = new ApiError(403, "Forbidden");
    renderSheet();
    expect(screen.getByRole("alert")).toHaveTextContent("You are not authorized to load Program Manager candidates for this action.");

    cleanup();
    fixtures.managerLookupError = new ApiError(403, "Forbidden");
    window.localStorage.setItem("nexus-locale", "ar");
    renderSheet();
    expect(screen.getByRole("alert")).toHaveTextContent("غير مصرح لك بتحميل مرشحي مدير البرنامج لهذا الإجراء.");
  });

  it("loads and updates an existing program using its concurrency token", async () => {
    fixtures.permissions = ["Program.Edit", "Handover.AdminInitiate"];
    const onOpenChange = renderSheet(true, {
      id: "program-1", name: "Existing Program", description: "Current description", portfolioId: "portfolio-1", programManagerId: "manager-1",
      status: "Active", startDate: "2026-01-01T00:00:00Z", endDate: "2026-12-31T00:00:00Z", currency: "USD", health: "Green", code: "PGM-001", rowVersion: [4, 8],
    });
    expect(screen.getByLabelText("Name *")).toHaveValue("Existing Program");
    fireEvent.change(screen.getByLabelText("Name *"), { target: { value: "Revised Program" } });
    fireEvent.click(screen.getByRole("button", { name: "Save Program" }));

    await waitFor(() => expect(fixtures.update).toHaveBeenCalledWith({
      id: "program-1",
      dto: {
        name: "Revised Program", description: "Current description", portfolioId: "portfolio-1", programManagerId: "manager-1",
        status: "Active", currency: "USD", rowVersion: [4, 8],
      },
    }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("allows Program.Edit to load scoped Portfolio options without Portfolio.View", async () => {
    fixtures.permissions = ["Program.Edit", "Handover.AdminInitiate"];
    renderSheet(true, {
      id: "program-1", name: "Existing Program", portfolioId: "portfolio-1", programManagerId: "manager-1",
      status: "Active", currency: "USD", code: "PGM-001",
    });

    expect(screen.getByRole("combobox", { name: "Portfolio" })).toBeEnabled();
    expect(screen.queryByText(/Program.Edit permission is required/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("combobox", { name: "Portfolio" }));
    fireEvent.click(screen.getByRole("option", { name: "Research (PORT-02)" }));
    fireEvent.click(screen.getByRole("button", { name: "Save Program" }));

    await waitFor(() => expect(fixtures.update).toHaveBeenCalledWith(expect.objectContaining({
      id: "program-1",
      dto: expect.objectContaining({ portfolioId: "portfolio-2" }),
    })));
  });

  it("blocks saving when unauthorized or in read-only context", () => {
    renderSheet(false);
    expect(screen.getByRole("button", { name: "Save Program" })).toBeDisabled();
    expect(fixtures.create).not.toHaveBeenCalled();
  });

  it("allows Program.Create without Portfolio.View in a mutable context", () => {
    const renderList = () => render(<I18nProvider><MemoryRouter><ManagedProgramsPage /></MemoryRouter></I18nProvider>);

    fixtures.permissions = ["Program.Create"];
    renderList();
    expect(screen.getByRole("button", { name: "Create Program" })).toBeInTheDocument();

    cleanup();
    fixtures.permissions = [];
    renderList();
    expect(screen.queryByRole("button", { name: "Create Program" })).not.toBeInTheDocument();

    cleanup();
    fixtures.permissions = ["Program.Create"];
    fixtures.effectiveReadOnly = true;
    renderList();
    expect(screen.queryByRole("button", { name: "Create Program" })).not.toBeInTheDocument();
  });

  it("shows the complete live program list without a duplicate finance indicator", () => {
    render(<I18nProvider><MemoryRouter><ManagedProgramsPage /></MemoryRouter></I18nProvider>);

    for (const heading of ["Program", "Health", "Status", "Manager", "Portfolio", "Start", "End", "Projects", "Attention", "Schedule"]) {
      expect(screen.getByRole("columnheader", { name: heading })).toBeInTheDocument();
    }
    expect(screen.queryByRole("columnheader", { name: "Financial" })).not.toBeInTheDocument();
    expect(screen.getByText("PGM-001 · EUR")).toBeInTheDocument();
    expect(screen.getByText("Nexus Manager")).toBeInTheDocument();
    expect(screen.getByText("Technology")).toBeInTheDocument();
    expect(screen.getByText("Overdue")).toBeInTheDocument();
  });

  it("localizes program status and health values in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    render(<I18nProvider><MemoryRouter><ManagedProgramsPage /></MemoryRouter></I18nProvider>);

    expect(screen.getByRole("columnheader", { name: "البداية" })).toBeInTheDocument();
    expect(screen.getByText("نشط")).toBeInTheDocument();
    expect(screen.getByText("كهرماني")).toBeInTheDocument();
    expect(screen.getByText("متأخر")).toBeInTheDocument();
  });
});
