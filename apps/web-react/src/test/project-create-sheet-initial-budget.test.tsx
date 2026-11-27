import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectSheet } from "../features/projects/components/CreateProjectSheet";

const fixtures = vi.hoisted(() => ({
  permissions: ["Project.Create", "ProjectAnnualBudget.Create"] as string[],
  currencies: [{ id: "currency-usd", code: "USD", name: "US Dollar", symbol: "$", isBase: true }] as Array<{ id: string; code: string; name: string; symbol: string; isBase: boolean }>,
  portfolios: [] as Array<{ id: string; name: string; currency?: string }>,
  programs: [] as Array<{ id: string; name: string; portfolioId: string; currency?: string }>,
  createProject: vi.fn(),
}));

vi.mock("../features/projects/api/projects", () => ({
  useCreateProject: () => ({ mutateAsync: fixtures.createProject, isPending: false }),
  useUpdateProject: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock("../features/projects/lib/project-lookups", () => ({
  toUserSelectOptions: (items: Array<{ id: string; fullName?: string; name?: string }>) => items.map(item => ({ value: item.id, label: item.fullName ?? item.name ?? item.id })),
  useDepartmentLookup: () => ({ data: [], isLoading: false, error: null }),
  usePortfolioLookup: () => ({ data: { items: fixtures.portfolios }, isLoading: false, error: null }),
  useProjectCurrencyLookup: () => ({ data: fixtures.currencies, isLoading: false, error: null }),
  useProjectWorkflowUserLookup: () => ({ data: [], isLoading: false, error: null }),
  useProjectManagerCandidateLookup: () => ({ data: [], isLoading: false, isFetching: false, error: null, refetch: vi.fn() }),
  useProgramLookup: (_enabled = true, portfolioId?: string) => ({
    data: { items: fixtures.programs.filter(program => !portfolioId || program.portfolioId === portfolioId) },
    isLoading: false,
    error: null,
  }),
}));

vi.mock("../features/projects/lib/project-labels", () => ({
  useProjectEnumLabels: () => ({ statusLabel: (value: string) => value, priorityLabel: (value: string) => value, healthLabel: (value: string) => value }),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "actor-1", permissions: fixtures.permissions } }) }));

vi.mock("@api/management", () => ({
  managementCacheScopeKey: (userId?: string, context?: { perspective?: string } | null) => `${userId ?? "anonymous"}:${context?.perspective ?? "native"}`,
  useManagement: () => ({ canMutate: true, effectiveReadOnly: false, context: null }),
}));

vi.mock("../features/projects/api/project-lifecycle", () => ({
  useProjectLifecycleOptions: () => ({ data: undefined, isLoading: false, isError: false, refetch: vi.fn() }),
  projectLifecycleLabel: () => "",
}));

vi.mock("@components/ui/Select", () => ({
  Select: ({ value, onValueChange, children }: { value: string; onValueChange: (value: string) => void; children: React.ReactNode }) => <select aria-label={value === "USD" || value === "EUR" ? "Project currency" : undefined} value={value} onChange={event => onValueChange(event.target.value)}>{children}</select>,
  SelectTrigger: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  SelectValue: () => null,
  SelectContent: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  SelectItem: ({ value, children }: { value: string; children: React.ReactNode }) => <option value={value}>{children}</option>,
}));

vi.mock("@components/ui/SearchableSelect", () => ({
  SearchableSelect: ({ options, value, onValueChange, placeholder }: { options: Array<{ value: string; label: string }>; value: string; onValueChange: (value: string) => void; placeholder: string }) => <select aria-label={placeholder} value={value} onChange={event => onValueChange(event.target.value)}><option value="">{placeholder}</option>{options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select>,
}));

function renderProjectSheet() {
  window.localStorage.setItem("nexus-locale", "en");
  return render(<I18nProvider><ProjectSheet open onOpenChange={vi.fn()} embedded /></I18nProvider>);
}

describe("Project creation initial Annual Budget", () => {
  beforeEach(() => {
    fixtures.permissions = ["Project.Create", "ProjectAnnualBudget.Create"];
    fixtures.currencies = [{ id: "currency-usd", code: "USD", name: "US Dollar", symbol: "$", isBase: true }];
    fixtures.portfolios = [];
    fixtures.programs = [];
    fixtures.createProject.mockReset().mockResolvedValue({ publicId: "project-1" });
  });

  it("follows the lifetime ceiling until the user edits the proposed amount", async () => {
    renderProjectSheet();
    const numericInputs = screen.getAllByRole("spinbutton");
    const ceiling = numericInputs[0] as HTMLInputElement;
    const proposedAnnualFunding = screen.getByLabelText("New Funding") as HTMLInputElement;

    fireEvent.change(ceiling, { target: { value: "100000" } });
    await waitFor(() => expect(proposedAnnualFunding).toHaveValue(100000));

    fireEvent.change(proposedAnnualFunding, { target: { value: "75000" } });
    fireEvent.change(ceiling, { target: { value: "150000" } });
    expect(proposedAnnualFunding).toHaveValue(75000);

    fireEvent.change(screen.getByPlaceholderText("Name"), { target: { value: "Delivery Project" } });
    fireEvent.click(screen.getByRole("button", { name: "New Project" }));

    await waitFor(() => expect(fixtures.createProject).toHaveBeenCalledWith(expect.objectContaining({
      budget: 150_000,
      initialAnnualBudgetAmount: 75_000,
      currency: "USD",
    })));
  });

  it("rejects an initial Annual Budget when the selected currency is no longer configured", async () => {
    renderProjectSheet();
    fireEvent.change(screen.getAllByRole("spinbutton")[0], { target: { value: "100000" } });
    await waitFor(() => expect(screen.getByLabelText("New Funding")).toHaveValue(100000));
    fireEvent.change(screen.getByPlaceholderText("Name"), { target: { value: "Delivery Project" } });
    fixtures.currencies = [];
    fireEvent.change(screen.getByPlaceholderText("Name"), { target: { value: "Delivery Project Updated" } });
    fireEvent.click(screen.getByRole("button", { name: "New Project" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("A valid project currency is unavailable");
    expect(fixtures.createProject).not.toHaveBeenCalled();
  });

  it("lets a Project.Create-only user create directly under a Portfolio and filters Programs to that Portfolio", async () => {
    fixtures.permissions = ["Project.Create"];
    fixtures.portfolios = [
      { id: "portfolio-a", name: "Portfolio A", currency: "USD" },
      { id: "portfolio-b", name: "Portfolio B", currency: "USD" },
    ];
    fixtures.programs = [
      { id: "program-a", name: "Program A", portfolioId: "portfolio-a", currency: "USD" },
      { id: "program-b", name: "Program B", portfolioId: "portfolio-b", currency: "USD" },
    ];
    renderProjectSheet();

    fireEvent.change(screen.getByLabelText("Portfolio"), { target: { value: "portfolio-a" } });
    const programSelect = screen.getByLabelText("Program") as HTMLSelectElement;
    expect(Array.from(programSelect.options).map(option => option.textContent)).toEqual(["Program", "Program A"]);
    expect(screen.queryByRole("option", { name: "Program B" })).not.toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText("Name"), { target: { value: "Direct portfolio project" } });
    fireEvent.click(screen.getByRole("button", { name: "New Project" }));

    await waitFor(() => expect(fixtures.createProject).toHaveBeenCalledWith(expect.objectContaining({
      name: "Direct portfolio project",
      portfolioId: "portfolio-a",
      programId: undefined,
      currency: "USD",
    })));
  });
});
