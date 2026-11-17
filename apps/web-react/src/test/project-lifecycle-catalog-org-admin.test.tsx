import type { ReactNode } from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { ProjectLifecycleCatalogPage } from "@features/administration/ProjectLifecycleCatalogPage";
import { Permissions } from "@lib/permissions";

const lifecycleMocks = vi.hoisted(() => ({
  permissions: [] as string[],
  profiles: [] as Array<{ id: string; name: string; canManageSettings: boolean }>,
  catalogCalls: [] as Array<{ kind: string; organizationId?: string | null; enabled: boolean }>,
  create: vi.fn(async () => undefined),
  update: vi.fn(async () => undefined),
  action: vi.fn(async () => undefined),
  confirm: vi.fn(async () => false),
  phases: [] as Array<Record<string, unknown>>,
  statuses: [] as Array<Record<string, unknown>>,
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "org-admin", permissions: lifecycleMocks.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, loading: false, snapshot: {
  scope: { organizationIds: ["management-only"], organizationReferences: [{ id: "management-only", name: "Management Only" }] },
} }) }));
vi.mock("@components/ui/ConfirmProvider", () => ({
  ConfirmProvider: ({ children }: { children: ReactNode }) => children,
  useConfirm: () => lifecycleMocks.confirm,
}));
vi.mock("@features/administration/api/settings", () => ({
  useOrganizationProfiles: (enabled = true) => ({
    data: enabled ? lifecycleMocks.profiles : [],
    isLoading: false,
    isError: false,
  }),
}));
vi.mock("@features/administration/api/projectLifecycle", () => ({
  useProjectLifecycleCatalog: (kind: "phases" | "statuses", organizationId: string | null, _includeInactive: boolean, enabled: boolean) => {
    lifecycleMocks.catalogCalls.push({ kind, organizationId, enabled });
    return { data: kind === "phases" ? lifecycleMocks.phases : lifecycleMocks.statuses, isLoading: false, isError: false, refetch: vi.fn() };
  },
  useCreateProjectLifecycleItem: () => ({ mutateAsync: lifecycleMocks.create, isPending: false }),
  useUpdateProjectLifecycleItem: () => ({ mutateAsync: lifecycleMocks.update, isPending: false }),
  useProjectLifecycleItemAction: () => ({ mutateAsync: lifecycleMocks.action, isPending: false }),
}));

function renderCatalog() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/administration/reference-data/project-lifecycle"]}>
        <ProjectLifecycleCatalogPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

function phase(referencedProjectCount: number | null = 0) {
  return {
    id: "phase-1",
    tenantId: "tenant-1",
    organizationId: "org-authorized",
    code: "DISCOVERY",
    nameEn: "Discovery",
    nameAr: "الاستكشاف",
    descriptionEn: null,
    descriptionAr: null,
    displayOrder: 10,
    isDefault: false,
    isActive: true,
    isContinuousActivity: false,
    referencedProjectCount,
    rowVersion: "row-v1",
  };
}

function tenantPhase() {
  return {
    ...phase(),
    id: "phase-tenant",
    organizationId: null,
    code: "TENANT-DEFAULT",
    nameEn: "Tenant Shared",
    displayOrder: 0,
    isDefault: false,
  };
}

describe("Project Lifecycle Organization Admin scope", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    lifecycleMocks.permissions = [Permissions.OrganizationConfiguration.View, Permissions.OrganizationConfiguration.Manage];
    lifecycleMocks.profiles = [{ id: "org-authorized", name: "Authorized Organization", canManageSettings: true }];
    lifecycleMocks.catalogCalls = [];
    lifecycleMocks.phases = [phase()];
    lifecycleMocks.statuses = [];
    lifecycleMocks.create.mockClear();
    lifecycleMocks.update.mockClear();
    lifecycleMocks.action.mockClear();
    lifecycleMocks.confirm.mockReset().mockResolvedValue(false);
  });

  it("uses server-authorized organizations instead of management snapshot scope", async () => {
    renderCatalog();

    const scope = screen.getByLabelText("Catalog scope") as HTMLSelectElement;
    await waitFor(() => expect(scope.value).toBe("org-authorized"));
    expect(Array.from(scope.options).map((option) => option.textContent)).toEqual(["Authorized Organization"]);
    expect(screen.getByRole("button", { name: "Add entry" })).toBeInTheDocument();
    expect(lifecycleMocks.catalogCalls).toContainEqual(expect.objectContaining({ kind: "phases", organizationId: "org-authorized", enabled: true }));
  });

  it("shows organization catalog records without management actions when the scoped profile is not manageable", async () => {
    lifecycleMocks.profiles = [{ id: "org-authorized", name: "Authorized Organization", canManageSettings: false }];
    renderCatalog();

    await screen.findByText("Discovery");
    expect(screen.getByLabelText("Catalog scope")).toHaveValue("org-authorized");
    expect(screen.queryByRole("button", { name: "Add entry" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Deactivate Discovery/i })).not.toBeInTheDocument();
  });

  it("keeps inherited Tenant rows read-only for Organization Admin while allowing edits to its scoped row", async () => {
    lifecycleMocks.phases = [tenantPhase(), phase()];
    renderCatalog();

    const tenantRow = await screen.findByText("Tenant Shared").then((element) => element.closest("article"));
    const organizationRow = screen.getByText("Discovery").closest("article");
    expect(tenantRow).not.toBeNull();
    expect(organizationRow).not.toBeNull();

    expect(within(tenantRow as HTMLElement).queryByRole("button", { name: /Edit.*Tenant Shared/i })).not.toBeInTheDocument();
    expect(within(tenantRow as HTMLElement).queryByRole("button", { name: /Deactivate Tenant Shared/i })).not.toBeInTheDocument();
    expect(within(organizationRow as HTMLElement).getByRole("button", { name: /Edit.*Discovery/i })).toBeInTheDocument();
    expect(within(organizationRow as HTMLElement).getByRole("button", { name: /Deactivate Discovery/i })).toBeInTheDocument();
  });

  it("retains Tenant row controls for an actor with the canonical Tenant lifecycle permissions", async () => {
    lifecycleMocks.permissions = [
      Permissions.ProjectLifecyclePhase.View,
      Permissions.ProjectLifecyclePhase.Edit,
      Permissions.ProjectLifecyclePhase.ActivateDeactivate,
    ];
    lifecycleMocks.profiles = [];
    lifecycleMocks.phases = [tenantPhase()];
    renderCatalog();

    const tenantRow = await screen.findByText("Tenant Shared").then((element) => element.closest("article"));
    expect(tenantRow).not.toBeNull();
    expect(within(tenantRow as HTMLElement).getByRole("button", { name: /Edit.*Tenant Shared/i })).toBeInTheDocument();
    expect(within(tenantRow as HTMLElement).getByRole("button", { name: /Deactivate Tenant Shared/i })).toBeInTheDocument();
  });

  it("does not treat a scoped settings grant as Organization Configuration Manage", async () => {
    lifecycleMocks.permissions = [Permissions.OrganizationConfiguration.View];
    renderCatalog();

    await screen.findByText("Discovery");
    expect(screen.getByLabelText("Catalog scope")).toHaveValue("org-authorized");
    expect(screen.queryByRole("button", { name: "Add entry" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Deactivate Discovery/i })).not.toBeInTheDocument();
  });

  it("renders N/A for hidden project aggregates and keeps deactivation confirmation safe", async () => {
    lifecycleMocks.phases = [phase(null)];
    renderCatalog();

    const article = await screen.findByRole("article");
    expect(within(article).getByText("N/A")).toBeInTheDocument();
    fireEvent.click(within(article).getByRole("button", { name: /Deactivate Discovery/i }));

    await waitFor(() => expect(lifecycleMocks.confirm).toHaveBeenCalledWith(expect.objectContaining({
      description: expect.stringContaining("N/A"),
    })));
  });
});
