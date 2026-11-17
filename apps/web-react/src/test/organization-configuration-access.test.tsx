import * as React from "react";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "@i18n/messages";
import { AppRoutes } from "../routes/AppRoutes";
import { Sidebar } from "@components/Sidebar";
import { AdministrationHub } from "@features/administration/AdministrationHub";
import { filterSections, navigationConfig } from "@lib/navigation";
import { Permissions } from "@lib/permissions";

const auth = vi.hoisted(() => ({
  current: {
    status: "authenticated" as const,
    user: { id: "org-admin", permissions: ["OrganizationConfiguration.View"] },
    logout: vi.fn(async () => undefined),
  },
}));
const settingsMocks = vi.hoisted(() => ({
  profileCalls: [] as boolean[],
  profiles: [{ id: "org-1", name: "Authorized Organization", canManageSettings: true }],
}));

vi.mock("../auth/AuthProvider", () => ({ useAuth: () => auth.current }));
vi.mock("../features/administration/api/settings", () => ({
  useSettingsAccess: () => ({
    data: { canReadTenant: false, canWriteTenant: false, canReadPlatform: false, canWritePlatform: false },
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
  }),
  useOrganizationProfiles: (enabled = true) => {
    settingsMocks.profileCalls.push(enabled);
    return { data: enabled ? settingsMocks.profiles : [], isLoading: false, isError: false };
  },
}));
vi.mock("../features/administration/api/emailNotifications", () => ({
  useEmailNotificationSettingsAccess: () => ({ data: { canReadTenant: false, canReadPlatform: false }, isLoading: false }),
}));
vi.mock("../auth/RequireAuth", () => ({
  RequireAuth: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  RequireManagementAuthorization: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));
vi.mock("../components/AppShell", () => ({ AppShell: ({ children }: { children: React.ReactNode }) => <>{children}</> }));
vi.mock("../features/administration/SettingsWorkspaces", () => ({
  OrganizationGeneralSettingsPage: () => <div>Organization configuration page mounted</div>,
  TenantGeneralSettingsPage: () => <div>Tenant settings page mounted</div>,
  PlatformGeneralSettingsPage: () => <div>Platform settings page mounted</div>,
}));
vi.mock("../features/administration/ProjectLifecycleCatalogPage", () => ({
  ProjectLifecycleCatalogPage: () => <div>Project Lifecycle catalog mounted</div>,
}));

describe("organization configuration access", () => {
  it("fetches authorized organization profiles for the scoped permission and exposes the hub entry", () => {
    settingsMocks.profileCalls.length = 0;
    render(
      <I18nProvider>
        <MemoryRouter>
          <AdministrationHub />
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(settingsMocks.profileCalls).toContain(true);
    expect(screen.getByRole("link", { name: /Organization General Settings/i })).toHaveAttribute("href", "/administration/organization-settings");
    expect(screen.queryByRole("link", { name: /Tenant Settings/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Platform Settings/i })).not.toBeInTheDocument();
  });

  it("enables the sidebar organization-profile lookup without tenant or platform access", () => {
    settingsMocks.profileCalls.length = 0;
    const { container } = render(
      <I18nProvider>
        <MemoryRouter>
          <Sidebar />
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(settingsMocks.profileCalls).toContain(true);
    expect(container.querySelector('a[href="/administration/organization-settings"]')).not.toBeNull();
    expect(container.querySelector('a[href="/administration/reference-data"]')).not.toBeNull();
    expect(container.querySelector('a[href="/administration/tenant-settings"]')).toBeNull();
    expect(container.querySelector('a[href="/administration/settings"]')).toBeNull();
    expect(container.querySelector('a[href="/administration/organization"]')).toBeNull();
  });

  it("allows the organization configuration permission through the actual route guard", async () => {
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/administration/organization-settings"]}>
          <AppRoutes />
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(await screen.findByText("Organization configuration page mounted")).toBeInTheDocument();
  });

  it("allows scoped Organization Configuration View through the Project Lifecycle route guard", async () => {
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/administration/reference-data/project-lifecycle"]}>
          <AppRoutes />
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(await screen.findByText("Project Lifecycle catalog mounted")).toBeInTheDocument();
  });

  it.each([
    "/administration/organization",
    "/administration/tenant-settings",
  ])("keeps tenant settings route %s unavailable to an organization-only actor", async (path) => {
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={[path]}>
          <AppRoutes />
        </MemoryRouter>
      </I18nProvider>,
    );

    expect(await screen.findByRole("alert")).toHaveTextContent(/do not have permission/i);
    expect(screen.queryByText("Tenant settings page mounted")).not.toBeInTheDocument();
  });

  it("shows only scoped organization settings in administration navigation", () => {
    const sections = filterSections(
      navigationConfig,
      [Permissions.OrganizationConfiguration.View],
      false,
      true,
      false,
    );
    const administration = sections.find((section) => section.id === "administration");

    expect(administration?.items.map((item) => item.id)).toEqual(["organization-settings", "reference-data"]);
    expect(administration?.items[0].href).toBe("/administration/organization-settings");
  });

  it("does not expose organization configuration navigation without an authorized profile", () => {
    const sections = filterSections(
      navigationConfig,
      [Permissions.OrganizationConfiguration.View],
      false,
      false,
      false,
    );
    const administration = sections.find((section) => section.id === "administration");

    expect(administration).toBeUndefined();
  });
});
