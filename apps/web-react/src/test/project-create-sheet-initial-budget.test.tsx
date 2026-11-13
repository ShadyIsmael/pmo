import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectSheet } from "../features/projects/components/CreateProjectSheet";

const fixtures = vi.hoisted(() => {
  const defaultPhase = {
    id: "phase-pre-initiation",
    code: "PRE_INITIATION",
    nameEn: "Pre-Initiation",
    nameAr: "ما قبل البدء",
    displayOrder: 1,
    isDefault: true,
    isActive: true,
  };
  const defaultStatus = {
    id: "status-not-started",
    code: "NOT_STARTED",
    nameEn: "Not Started",
    nameAr: "لم يبدأ",
    displayOrder: 1,
    isDefault: true,
    isActive: true,
  };
  return {
    permissions: ["Project.Create", "ProjectAnnualBudget.Create"] as string[],
    currencies: [{ id: "currency-usd", code: "USD", name: "US Dollar", symbol: "$", isBase: true }] as Array<{ id: string; code: string; name: string; symbol: string; isBase: boolean }>,
    portfolios: [{ id: "portfolio-1", name: "Portfolio One", currency: "USD" }, { id: "portfolio-2", name: "Portfolio Two", currency: "USD" }],
    programs: [{ id: "program-1", name: "Program One", portfolioId: "portfolio-1", currency: "USD" }, { id: "program-2", name: "Program Two", portfolioId: "portfolio-2", currency: "USD" }],
    managers: [{ id: "manager-1", fullName: "Eligible Manager" }],
    createProject: vi.fn(),
    lifecycle: {
      defaultPhase,
      defaultStatus,
      mode: "ready" as "ready" | "loading" | "error" | "empty" | "single-default" | "ambiguous-defaults",
      phasesByPortfolio: {} as Record<string, Array<typeof defaultPhase>>,
      loadingPortfolios: [] as string[],
      calls: [] as Array<{
        enabled?: boolean;
        purpose?: "create";
        portfolioId?: string;
        departmentId?: string;
        projectId?: string;
        organizationId?: string;
      }>,
      refetch: vi.fn(),
    },
  };
});

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
  useProjectManagerCandidateLookup: () => ({ data: fixtures.managers, isLoading: false, isFetching: false, error: null, refetch: vi.fn() }),
  useProgramLookup: (_open: boolean, portfolioId?: string) => ({ data: { items: portfolioId ? fixtures.programs.filter(program => program.portfolioId === portfolioId) : fixtures.programs }, isLoading: false, error: null }),
}));

vi.mock("../features/projects/lib/project-labels", () => ({
  useProjectEnumLabels: () => ({ statusLabel: (value: string) => value, priorityLabel: (value: string) => value, healthLabel: (value: string) => value }),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "actor-1", permissions: fixtures.permissions } }) }));

vi.mock("@api/management", () => ({
  useManagement: () => ({ canMutate: true, effectiveReadOnly: false }),
}));

vi.mock("../features/projects/api/project-lifecycle", () => {
  const phasesForScope = (scopeKey: string) => {
    switch (fixtures.lifecycle.mode) {
      case "empty":
        return [];
      case "single-default":
        return [{ ...fixtures.lifecycle.defaultPhase, id: "phase-tenant-default", code: "TENANT_DEFAULT", nameEn: "Initiation", nameAr: "البدء" }];
      case "ambiguous-defaults":
        return [
          { ...fixtures.lifecycle.defaultPhase, id: "phase-default-a", code: "DEFAULT_A", nameEn: "Default A", nameAr: "افتراضي أ" },
          { ...fixtures.lifecycle.defaultPhase, id: "phase-default-b", code: "DEFAULT_B", nameEn: "Default B", nameAr: "افتراضي ب" },
        ];
      default:
        return fixtures.lifecycle.phasesByPortfolio[scopeKey] ?? [fixtures.lifecycle.defaultPhase];
    }
  };
  return {
    projectLifecycleLabel: (option: { nameEn?: string; nameAr?: string } | undefined, locale: string) =>
      option ? (locale === "ar" ? option.nameAr || option.nameEn : option.nameEn || option.nameAr) || "" : "",
    useProjectLifecycleOptions: (args: {
      enabled?: boolean;
      purpose?: "create";
      portfolioId?: string;
      departmentId?: string;
      projectId?: string;
      organizationId?: string;
    } = {}) => {
      fixtures.lifecycle.calls.push(args);
      const scopeKey = args.portfolioId ?? args.departmentId ?? "";
      const isLoading = fixtures.lifecycle.mode === "loading" || fixtures.lifecycle.loadingPortfolios.includes(scopeKey);
      if (isLoading) {
        return { data: undefined, isLoading: true, isError: false, refetch: fixtures.lifecycle.refetch };
      }
      if (fixtures.lifecycle.mode === "error") {
        return { data: undefined, isLoading: false, isError: true, refetch: fixtures.lifecycle.refetch };
      }
      return {
        data: { phases: phasesForScope(scopeKey), statuses: [fixtures.lifecycle.defaultStatus] },
        isLoading: false,
        isError: false,
        refetch: fixtures.lifecycle.refetch,
      };
    },
    useChangeProjectStatus: () => ({ mutateAsync: vi.fn(), isPending: false }),
  };
});

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

function projectSheetElement(open = true) {
  return <I18nProvider><ProjectSheet open={open} onOpenChange={vi.fn()} embedded /></I18nProvider>;
}

function renderProjectSheet(locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(projectSheetElement());
}

function lastLifecycleCall() {
  return fixtures.lifecycle.calls[fixtures.lifecycle.calls.length - 1];
}

beforeEach(() => {
  fixtures.lifecycle.mode = "ready";
  fixtures.lifecycle.phasesByPortfolio = {};
  fixtures.lifecycle.loadingPortfolios = [];
  fixtures.lifecycle.calls = [];
  fixtures.lifecycle.refetch.mockReset();
});

describe("Project creation initial Annual Budget", () => {
  beforeEach(() => {
    fixtures.permissions = ["Project.Create", "ProjectAnnualBudget.Create"];
    fixtures.currencies = [{ id: "currency-usd", code: "USD", name: "US Dollar", symbol: "$", isBase: true }];
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
      phaseId: "phase-pre-initiation",
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

    expect(await screen.findByText(/A valid project currency is unavailable/)).toBeInTheDocument();
    expect(fixtures.createProject).not.toHaveBeenCalled();
  });
});

describe("Project creation UX and contract", () => {
  beforeEach(() => {
    fixtures.permissions = ["Project.Create"];
    fixtures.currencies = [{ id: "currency-usd", code: "USD", name: "US Dollar", symbol: "$", isBase: true }];
    fixtures.createProject.mockReset().mockResolvedValue({ publicId: "project-1" });
  });

  it.each(["en", "ar"] as const)("renders localized creation and document direction in %s", async locale => {
    renderProjectSheet(locale);
    await waitFor(() => expect(document.documentElement.dir).toBe(locale === "ar" ? "rtl" : "ltr"));
    expect(document.documentElement.lang).toBe(locale);
    expect(screen.getByRole("button", { name: locale === "ar" ? "مشروع جديد" : "New Project" })).toBeInTheDocument();
    expect(screen.getAllByText(locale === "ar" ? /ما قبل البدء/ : /Pre-Initiation/).length).toBeGreaterThan(0);
    expect(document.body.textContent).not.toMatch(/projects\.createSections\./);
  });

  it("filters programs by portfolio, clears a conflicting program, and submits an eligible initial manager", async () => {
    renderProjectSheet();
    const portfolio = screen.getByRole("combobox", { name: "Portfolio" });
    const program = screen.getByRole("combobox", { name: "Program" });
    fireEvent.change(portfolio, { target: { value: "portfolio-1" } });
    expect(screen.getByRole("option", { name: "Program One" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Program Two" })).not.toBeInTheDocument();
    fireEvent.change(program, { target: { value: "program-1" } });
    fireEvent.change(portfolio, { target: { value: "portfolio-2" } });
    expect(program).toHaveValue("");
    expect(screen.getByRole("option", { name: "Program Two" })).toBeInTheDocument();
    fireEvent.change(screen.getByRole("combobox", { name: "Project Manager" }), { target: { value: "manager-1" } });
    fireEvent.change(screen.getByPlaceholderText("Name"), { target: { value: "Delivery Project" } });
    fireEvent.click(screen.getByRole("button", { name: "New Project" }));
    await waitFor(() => expect(fixtures.createProject).toHaveBeenCalledWith(expect.objectContaining({ portfolioId: "portfolio-2", programId: undefined, projectManagerId: "manager-1" })));
    const payload = fixtures.createProject.mock.calls[0][0];
    expect(payload).not.toHaveProperty("phase");
    expect(payload).not.toHaveProperty("status");
    expect(payload.initialAnnualBudgetAmount).toBeUndefined();
  });

  it("keeps entered values and shows a submission error", async () => {
    fixtures.createProject.mockRejectedValue(new Error("network"));
    renderProjectSheet();
    const name = screen.getByPlaceholderText("Name");
    fireEvent.change(name, { target: { value: "Delivery Project" } });
    fireEvent.click(screen.getByRole("button", { name: "New Project" }));
    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(name).toHaveValue("Delivery Project");
  });

  it("rejects a missing project name before submission", async () => {
    renderProjectSheet();
    fireEvent.click(screen.getByRole("button", { name: "New Project" }));
    expect(await screen.findByText("Enter a project name.")).toBeInTheDocument();
    expect(fixtures.createProject).not.toHaveBeenCalled();
  });

  it("does not submit twice while a request is pending", async () => {
    let resolveRequest!: (value: { publicId: string }) => void;
    fixtures.createProject.mockImplementation(() => new Promise(resolve => { resolveRequest = resolve; }));
    renderProjectSheet();
    fireEvent.change(screen.getByPlaceholderText("Name"), { target: { value: "Delivery Project" } });
    const button = screen.getByRole("button", { name: "New Project" });
    fireEvent.click(button);
    fireEvent.click(button);
    await waitFor(() => expect(fixtures.createProject).toHaveBeenCalledTimes(1));
    resolveRequest({ publicId: "project-1" });
  });
});

describe("Project creation initial phase", () => {
  beforeEach(() => {
    fixtures.permissions = ["Project.Create"];
    fixtures.currencies = [{ id: "currency-usd", code: "USD", name: "US Dollar", symbol: "$", isBase: true }];
    fixtures.createProject.mockReset().mockResolvedValue({ publicId: "project-1" });
  });

  it.each(["en", "ar"] as const)("renders the resolved initial phase read-only and localized in %s", async locale => {
    renderProjectSheet(locale);

    expect(screen.queryByRole("combobox", { name: locale === "ar" ? "مرحلة المشروع" : "Project phase" })).not.toBeInTheDocument();
    const display = await screen.findByTestId("project-initial-phase");
    expect(display).toHaveTextContent(locale === "ar" ? "ما قبل البدء" : "Pre-Initiation");
    expect(display).toHaveAttribute("aria-readonly", "true");
    await waitFor(() => expect(screen.getByTestId("project-phase-id")).toHaveValue("phase-pre-initiation"));

    const request = lastLifecycleCall();
    expect(request).toMatchObject({ enabled: true, purpose: "create" });
    expect(request?.portfolioId).toBeUndefined();
    expect(request?.departmentId).toBeUndefined();
    expect(request?.projectId).toBeUndefined();
    expect(request?.organizationId).toBeUndefined();
  });

  it("falls back to the only active default option when no protected Pre-Initiation record is returned", async () => {
    fixtures.lifecycle.mode = "single-default";
    renderProjectSheet();

    expect(await screen.findByTestId("project-initial-phase")).toHaveTextContent("Initiation");
    await waitFor(() => expect(screen.getByTestId("project-phase-id")).toHaveValue("phase-tenant-default"));
    expect(screen.getByRole("button", { name: "New Project" })).toBeEnabled();
  });

  it.each(["empty", "ambiguous-defaults"] as const)("shows a truthful unavailable state and disables submission when the initial phase cannot be resolved (%s)", async mode => {
    fixtures.lifecycle.mode = mode;
    renderProjectSheet();

    expect(screen.getByText("The initial project phase is unavailable for the current lifecycle configuration. Select a portfolio or department with an active Organization lifecycle, or contact an administrator.")).toBeInTheDocument();
    expect(screen.queryByTestId("project-initial-phase")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New Project" })).toBeDisabled();
    await waitFor(() => expect(screen.getByTestId("project-phase-id")).toHaveValue(""));
  });

  it("shows the phase loading state and blocks submission while the lookup is unresolved", async () => {
    fixtures.lifecycle.mode = "loading";
    renderProjectSheet();

    expect(screen.getByText("Loading project phases…")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New Project" })).toBeDisabled();
    await waitFor(() => expect(screen.getByTestId("project-phase-id")).toHaveValue(""));
  });

  it("shows the lookup error and recovers through a working Retry action", async () => {
    fixtures.lifecycle.mode = "error";
    const view = renderProjectSheet();

    expect(screen.getByText("Lifecycle options could not be loaded.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New Project" })).toBeDisabled();

    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(fixtures.lifecycle.refetch).toHaveBeenCalledTimes(1);

    fixtures.lifecycle.mode = "ready";
    view.rerender(projectSheetElement());
    expect(await screen.findByTestId("project-initial-phase")).toHaveTextContent("Pre-Initiation");
    await waitFor(() => expect(screen.getByRole("button", { name: "New Project" })).toBeEnabled());
  });

  it("restores the resolved form phase id after the sheet is reopened", async () => {
    const view = renderProjectSheet();
    await waitFor(() => expect(screen.getByTestId("project-phase-id")).toHaveValue("phase-pre-initiation"));

    view.rerender(projectSheetElement(false));
    view.rerender(projectSheetElement(true));

    await waitFor(() => expect(screen.getByTestId("project-phase-id")).toHaveValue("phase-pre-initiation"));
  });

  it("requests create options for the selected portfolio and submits the newly resolved phase id", async () => {
    fixtures.lifecycle.phasesByPortfolio = {
      "portfolio-1": [{ ...fixtures.lifecycle.defaultPhase, id: "phase-pre-initiation-portfolio-1" }],
      "portfolio-2": [{ ...fixtures.lifecycle.defaultPhase, id: "phase-pre-initiation-portfolio-2" }],
    };
    renderProjectSheet();

    const portfolio = screen.getByRole("combobox", { name: "Portfolio" });
    fireEvent.change(portfolio, { target: { value: "portfolio-1" } });
    await waitFor(() => expect(screen.getByTestId("project-phase-id")).toHaveValue("phase-pre-initiation-portfolio-1"));

    fireEvent.change(portfolio, { target: { value: "portfolio-2" } });
    await waitFor(() => expect(screen.getByTestId("project-phase-id")).toHaveValue("phase-pre-initiation-portfolio-2"));

    const createCalls = fixtures.lifecycle.calls.filter(call => call.purpose === "create");
    expect(createCalls[createCalls.length - 1]).toMatchObject({ purpose: "create", portfolioId: "portfolio-2" });
    expect(createCalls.every(call => call.projectId === undefined && call.organizationId === undefined)).toBe(true);

    fireEvent.change(screen.getByPlaceholderText("Name"), { target: { value: "Delivery Project" } });
    fireEvent.click(screen.getByRole("button", { name: "New Project" }));
    await waitFor(() => expect(fixtures.createProject).toHaveBeenCalledWith(expect.objectContaining({
      phaseId: "phase-pre-initiation-portfolio-2",
    })));
  });

  it("clears the previous phase id immediately and blocks submission while the new scope is loading", async () => {
    fixtures.lifecycle.phasesByPortfolio = {
      "portfolio-1": [{ ...fixtures.lifecycle.defaultPhase, id: "phase-pre-initiation-portfolio-1" }],
    };
    fixtures.lifecycle.loadingPortfolios = ["portfolio-2"];
    renderProjectSheet();

    const portfolio = screen.getByRole("combobox", { name: "Portfolio" });
    fireEvent.change(portfolio, { target: { value: "portfolio-1" } });
    await waitFor(() => expect(screen.getByTestId("project-phase-id")).toHaveValue("phase-pre-initiation-portfolio-1"));

    fireEvent.change(portfolio, { target: { value: "portfolio-2" } });
    await waitFor(() => expect(screen.getByTestId("project-phase-id")).toHaveValue(""));

    expect(screen.getByText("Loading project phases…")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "New Project" })).toBeDisabled();
    expect(fixtures.createProject).not.toHaveBeenCalled();
  });
});
