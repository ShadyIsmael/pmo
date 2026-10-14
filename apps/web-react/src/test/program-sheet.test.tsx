import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { ProgramSheet } from "../features/delivery/ProgramSheet";
import { ManagedProgramsPage } from "../features/delivery/PortfolioProgramPages";

const fixtures = vi.hoisted(() => ({
  create: vi.fn(),
  update: vi.fn(),
  permissions: ["Program.Manage"],
  canMutate: true,
  effectiveReadOnly: false,
  programs: [{
    id: "program-1", name: "Cloud Modernization", code: "PGM-001", health: "Amber", status: "Active",
    programManagerName: "Nexus Manager", portfolioName: "Technology", startDate: "2026-03-01T00:00:00Z", endDate: "2026-12-31T00:00:00Z", currency: "EUR",
    projectCount: 4, projectsRequiringAttention: 2, scheduleIndicator: "Delayed", financialIndicator: "Available",
  }],
}));

vi.mock("@features/delivery/api/delivery", () => ({
  useManagedPrograms: () => ({ data: { items: fixtures.programs }, isLoading: false, isError: false }),
  useCreateProgram: () => ({ mutateAsync: fixtures.create, isPending: false }),
  useUpdateProgram: () => ({ mutateAsync: fixtures.update, isPending: false }),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));
vi.mock("@api/management", () => ({
  managementCacheScopeKey: (userId?: string) => userId ?? "anonymous",
  useManagement: () => ({ canMutate: fixtures.canMutate, effectiveReadOnly: fixtures.effectiveReadOnly }),
}));

vi.mock("@features/projects/lib/project-lookups", () => ({
  useUserLookup: () => ({ data: [{ id: "manager-1", fullName: "Nexus Manager", email: "manager@nexus.com" }], isLoading: false }),
  usePortfolioLookup: () => ({ data: { items: [{ id: "portfolio-1", name: "Technology", code: "PORT-01", currency: "EUR", status: "Active" }] }, isLoading: false }),
  toUserSelectOptions: (users: { id: string; fullName: string; email: string }[] | undefined) => (users ?? []).map((user) => ({ value: user.id, label: `${user.fullName} · ${user.email}`, keywords: [] })),
}));

vi.mock("@features/administration/api/admin", () => ({
  useCurrencies: () => ({ data: [
    { id: "currency-usd", code: "USD", nameEn: "US Dollar", nameAr: "دولار أمريكي", isActive: true },
    { id: "currency-eur", code: "EUR", nameEn: "Euro", nameAr: "يورو", isActive: true },
  ] }),
}));

function renderSheet(canManage = true, program?: { id: string; name: string; description?: string; portfolioId?: string; programManagerId?: string; status?: string; startDate?: string; endDate?: string; currency?: string; budget?: number; health?: string; code?: string; rowVersion?: number[] }) {
  const onOpenChange = vi.fn();
  render(<I18nProvider><ProgramSheet open onOpenChange={onOpenChange} program={program} canManage={canManage} /></I18nProvider>);
  return onOpenChange;
}

describe("program management sheet", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.create.mockReset().mockResolvedValue({ id: "program-1" });
    fixtures.update.mockReset().mockResolvedValue({ id: "program-1" });
    fixtures.programs = [{
      id: "program-1", name: "Cloud Modernization", code: "PGM-001", health: "Amber", status: "Active",
      programManagerName: "Nexus Manager", portfolioName: "Technology", startDate: "2026-03-01T00:00:00Z", endDate: "2026-12-31T00:00:00Z", currency: "EUR",
      projectCount: 4, projectsRequiringAttention: 2, scheduleIndicator: "Delayed", financialIndicator: "Available",
    }];
    fixtures.permissions = ["Program.Manage"];
    fixtures.canMutate = true;
    fixtures.effectiveReadOnly = false;
  });

  it("creates a program with portfolio currency and optional manager fields", async () => {
    const onOpenChange = renderSheet();
    fireEvent.change(screen.getByLabelText("Name *"), { target: { value: "Cloud Modernization" } });
    fireEvent.click(screen.getAllByRole("combobox")[0]);
    fireEvent.click(screen.getByRole("option", { name: "Technology (PORT-01)" }));
    fireEvent.click(screen.getAllByRole("combobox")[1]);
    fireEvent.click(screen.getByRole("option", { name: "Nexus Manager · manager@nexus.com" }));
    fireEvent.change(screen.getByLabelText("Budget"), { target: { value: "125000" } });
    fireEvent.click(screen.getByRole("button", { name: "Save Program" }));

    await waitFor(() => expect(fixtures.create).toHaveBeenCalledWith({
      name: "Cloud Modernization",
      description: undefined,
      portfolioId: "portfolio-1",
      programManagerId: "manager-1",
      status: "Planning",
      startDate: undefined,
      endDate: undefined,
      currency: "EUR",
      budget: 125000,
      health: undefined,
    }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("loads and updates an existing program using its concurrency token", async () => {
    const onOpenChange = renderSheet(true, {
      id: "program-1", name: "Existing Program", description: "Current description", portfolioId: "portfolio-1", programManagerId: "manager-1",
      status: "Active", startDate: "2026-01-01T00:00:00Z", endDate: "2026-12-31T00:00:00Z", currency: "USD", budget: 45000, health: "Green", code: "PGM-001", rowVersion: [4, 8],
    });
    expect(screen.getByLabelText("Name *")).toHaveValue("Existing Program");
    fireEvent.change(screen.getByLabelText("Name *"), { target: { value: "Revised Program" } });
    fireEvent.click(screen.getByRole("button", { name: "Save Program" }));

    await waitFor(() => expect(fixtures.update).toHaveBeenCalledWith({
      id: "program-1",
      dto: {
        name: "Revised Program", description: "Current description", portfolioId: "portfolio-1", programManagerId: "manager-1",
        status: "Active", startDate: "2026-01-01", endDate: "2026-12-31", currency: "USD", budget: 45000, health: "Green", rowVersion: [4, 8],
      },
    }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("blocks saving when unauthorized or in read-only context", () => {
    renderSheet(false);
    expect(screen.getByRole("button", { name: "Save Program" })).toBeDisabled();
    expect(fixtures.create).not.toHaveBeenCalled();
  });

  it("shows the create action only with Program.Manage in a mutable context", () => {
    const renderList = () => render(<I18nProvider><MemoryRouter><ManagedProgramsPage /></MemoryRouter></I18nProvider>);

    renderList();
    expect(screen.getByRole("button", { name: "Create Program" })).toBeInTheDocument();

    cleanup();
    fixtures.permissions = [];
    renderList();
    expect(screen.queryByRole("button", { name: "Create Program" })).not.toBeInTheDocument();

    cleanup();
    fixtures.permissions = ["Program.Manage"];
    fixtures.effectiveReadOnly = true;
    renderList();
    expect(screen.queryByRole("button", { name: "Create Program" })).not.toBeInTheDocument();
  });

  it("shows the complete live program list, including schedule and financial indicators", () => {
    render(<I18nProvider><MemoryRouter><ManagedProgramsPage /></MemoryRouter></I18nProvider>);

    for (const heading of ["Program", "Health", "Status", "Manager", "Portfolio", "Start", "End", "Projects", "Attention", "Schedule", "Financial"]) {
      expect(screen.getByRole("columnheader", { name: heading })).toBeInTheDocument();
    }
    expect(screen.getByText("PGM-001 · EUR")).toBeInTheDocument();
    expect(screen.getByText("Nexus Manager")).toBeInTheDocument();
    expect(screen.getByText("Technology")).toBeInTheDocument();
    expect(screen.getByText("Overdue")).toBeInTheDocument();
    expect(screen.getByText("Available")).toBeInTheDocument();
  });

  it("localizes program status and health values in Arabic", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    render(<I18nProvider><MemoryRouter><ManagedProgramsPage /></MemoryRouter></I18nProvider>);

    expect(screen.getByRole("columnheader", { name: "البداية" })).toBeInTheDocument();
    expect(screen.getByText("نشط")).toBeInTheDocument();
    expect(screen.getByText("كهرماني")).toBeInTheDocument();
    expect(screen.getByText("متأخر")).toBeInTheDocument();
    expect(screen.getByText("متاح")).toBeInTheDocument();
  });
});
