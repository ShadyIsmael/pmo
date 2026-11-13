import { fireEvent, render, screen, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectSettingsTab } from "../features/projects/ProjectSettingsTab";

const fixtures = vi.hoisted(() => ({
  permissions: ["ProjectSettings.View", "ProjectAnnualBudget.View"],
  foundation: vi.fn(),
  updateMode: vi.fn(),
}));

vi.mock("../features/projects/api/project-financials", () => ({
  useProjectFinancialFoundation: fixtures.foundation,
  useUpdateProjectFinanceApprovalMode: fixtures.updateMode,
}));

vi.mock("@api/client", () => ({
  api: {
    get: vi.fn(async () => ({ projectId: "project-42", domains: [] })),
    put: vi.fn(),
    post: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  },
  setApiErrorLocale: vi.fn(),
}));

vi.mock("../features/projects/ProjectAnnualBudgetApprovalSettings", () => ({
  ProjectAnnualBudgetApprovalSettings: ({ canManage }: { canManage: boolean }) =>
    <div data-testid="approval-defaults" data-can-manage={String(canManage)} />,
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { permissions: fixtures.permissions } }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => ({ canMutate: true, effectiveReadOnly: false }),
}));

function SettingsOutlet() {
  return <Outlet context={{ projectId: "project-42", project: { name: "Project Atlas", code: "ATLAS" } }} />;
}

function renderSettings(locale = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <MemoryRouter initialEntries={["/projects/project-42/settings"]}>
          <Routes>
            <Route path="/projects/:id" element={<SettingsOutlet />}>
              <Route path="settings" element={<ProjectSettingsTab />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </I18nProvider>
    </QueryClientProvider>,
  );
}

describe("Project approval settings permissions", () => {
  beforeEach(() => {
    fixtures.permissions = ["ProjectSettings.View", "ProjectAnnualBudget.View"];
    fixtures.foundation.mockReturnValue({
      data: { financeApprovalMode: "Sequential", rowVersion: "v1" },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    });
    fixtures.updateMode.mockReturnValue({ isPending: false, isError: false, mutateAsync: vi.fn() });
  });

  it("shows approval defaults read-only with View permission", () => {
    renderSettings();

    expect(screen.getByRole("heading", { name: "Project settings" })).toBeInTheDocument();
    expect(screen.getByTestId("approval-defaults")).toHaveAttribute("data-can-manage", "false");
    expect(screen.queryByLabelText("Finance approval configuration")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save" })).not.toBeInTheDocument();
    const navigation = screen.getByRole("navigation", { name: "Project settings" });
    expect(within(navigation).getByRole("button", { name: /General settings/i })).toHaveAttribute("aria-current", "page");
    expect(within(navigation).queryByRole("button", { name: /Financial approval settings/i })).not.toBeInTheDocument();
    fireEvent.click(within(navigation).getByRole("button", { name: /Annual budget approval defaults/i }));
    expect(screen.getByTestId("approval-defaults")).toBeVisible();
  });

  it("enables foundation settings only with Settings View/Edit, foundation View, and the dedicated action permission", () => {
    fixtures.permissions = ["ProjectSettings.View", "ProjectSettings.Edit", "ProjectAnnualBudget.View", "ProjectFinancialFoundation.View", "ProjectAnnualBudget.ManageApprovalConfiguration"];
    renderSettings();

    expect(screen.getByTestId("approval-defaults")).toHaveAttribute("data-can-manage", "true");
    fireEvent.click(within(screen.getByRole("navigation", { name: "Project settings" })).getByRole("button", { name: /Financial approval settings/i }));
    const mode = screen.getByRole("combobox", { name: "Finance approval configuration" });
    expect(mode).toBeEnabled();
    fireEvent.change(mode, { target: { value: "Parallel" } });
    fireEvent.click(within(screen.getByRole("navigation", { name: "Project settings" })).getByRole("button", { name: /General settings/i }));
    fireEvent.click(within(screen.getByRole("navigation", { name: "Project settings" })).getByRole("button", { name: /Financial approval settings/i }));
    expect(screen.getByRole("combobox", { name: "Finance approval configuration" })).toHaveValue("Parallel");
    expect(screen.getByRole("button", { name: "Save" })).toBeInTheDocument();
  });

  it("does not fetch or display foundation data for the shell-only ProjectFinance.View key", () => {
    fixtures.permissions = ["ProjectFinance.View"];
    renderSettings();

    expect(fixtures.foundation).toHaveBeenCalledWith("project-42", false);
    expect(screen.queryByLabelText("Finance approval configuration")).not.toBeInTheDocument();
    expect(screen.queryByTestId("approval-defaults")).not.toBeInTheDocument();
  });

  it("renders the settings navigation in Arabic and RTL", () => {
    const { container } = renderSettings("ar");
    expect(container.querySelector('[dir="rtl"]')).toBeInTheDocument();
    expect(screen.getByRole("navigation")).toBeInTheDocument();
    expect(screen.getAllByText("Project Atlas").length).toBeGreaterThan(0);
  });
});
