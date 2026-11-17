import * as React from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import enMessages from "../i18n/messages/en.json";
import arMessages from "../i18n/messages/ar.json";
import { ApiError } from "../api/client";
import { AccessPage, AccessUsersPage, UserPermissionsPage } from "../features/administration/AccessPages";
import { AdministrationLayout } from "../features/administration/AdministrationHub";
import { RolesPage } from "../features/administration/RolesPage";

const fixtures = vi.hoisted(() => ({
  users: [{ id: "user-1", email: "maya@example.com", firstName: "Maya", lastName: "Admin", fullName: "Maya Admin", isActive: true, authenticationSource: "Local", mustChangePassword: false, roles: [{ id: "role-1", name: "Administrator", organizationId: null, organizationName: null, scope: "Tenant", isOrganizationOwned: false, isLegacyOrganizationOwnershipUnresolved: false, isOrganizationOwnershipUnresolved: false, isProtected: false, canRemove: true }] }],
  permissions: [
    { id: "permission-1", name: "Project.View", resource: "Project", action: "View", labelEn: "View projects", isActive: true, isAssignable: true, isRoleGranted: true, roleGrantSources: ["Finance Reviewer", "Project Manager"], scope: "Tenant", overrideEffect: null, isEffective: true, canDelegate: true },
    { id: "permission-2", name: "Legacy.Users.Write", resource: "Legacy.Users", action: "Write", labelEn: "Legacy permission", isActive: false, isAssignable: false, isRoleGranted: false, overrideEffect: null, isEffective: false },
    { id: "permission-finances-read", name: "ProjectFinance.View", resource: "ProjectFinance", action: "View", labelEn: "Open Project Finance", labelAr: "فتح مساحة إدارة تمويل المشروع", isActive: true, isAssignable: true, isRoleGranted: false, overrideEffect: null, isEffective: false },
  ] as Array<{ id: string; name: string; resource: string; action: string; labelEn?: string; labelAr?: string; isActive: boolean; isAssignable: boolean; isRoleGranted: boolean; roleGrantSources?: string[]; scope?: string; overrideEffect: "Allow" | "Deny" | null; isEffective: boolean; canDelegate?: boolean }>,
  actorPermissions: ["Users.View", "Users.ManageAccounts", "Users.ResetPassword", "Users.SetStatus", "Users.AssignRoles", "UserPermissions.Manage", "Roles.View", "Project.View"] as string[],
  assignablePermissions: [] as Array<{ id: string; name: string; resource: string; action: string; description?: string; moduleId?: string; moduleNameEn?: string; moduleNameAr?: string; screenId?: string; screenNameEn?: string; screenNameAr?: string; scope?: "Tenant" | "Organization" | "Portfolio" | "Program" | "Project"; canDelegate?: boolean; canDelegateToCustomRole?: boolean; canDelegateToOrdinaryRole?: boolean; canGrantToOrganizationRole?: boolean; level?: "View" | "Action"; labelEn?: string; labelAr?: string; descriptionEn?: string; descriptionAr?: string; sensitivity?: "Low" | "Medium" | "High" | "Critical"; isSensitive?: boolean; isAssignable?: boolean; allowedCategories?: string[]; requiredPermissionKeys?: string[]; isActive?: boolean }>,
  roles: [] as Array<{ id: string; name: string; description?: string; category?: string; roleKey?: string | null; isSystem: boolean; isProtected?: boolean; isAssignable?: boolean; canEditDetails?: boolean; canManagePermissions?: boolean; canDelete?: boolean; isProtectedSystemAdmin?: boolean; permissionCount: number; assignedUserCount: number }>,
  assignableRoles: [] as Array<{ id: string; name: string; description?: string; category?: string; roleKey?: string | null; isSystem: boolean; isProtected: boolean; isAssignable: boolean; permissionCount: number; assignedUserCount: number }>,
  selectedRole: null as null | { id: string; name: string; description: string; category: string; roleKey?: string | null; isSystem: boolean; isProtected?: boolean; isAssignable?: boolean; canEditDetails?: boolean; canManagePermissions?: boolean; canDelete?: boolean; isProtectedSystemAdmin?: boolean; permissionCount: number; assignedUserCount: number; permissions: Array<{ id: string; name: string; resource: string; action: string; moduleId?: string; moduleNameEn?: string; moduleNameAr?: string; screenId?: string; screenNameEn?: string; screenNameAr?: string; scope?: "Tenant" | "Organization" | "Portfolio" | "Program" | "Project"; canDelegate?: boolean; canDelegateToCustomRole?: boolean; canDelegateToOrdinaryRole?: boolean; level?: "View" | "Action"; labelEn?: string; labelAr?: string; descriptionEn?: string; descriptionAr?: string; sensitivity?: "Low" | "Medium" | "High" | "Critical"; isSensitive?: boolean; isAssignable?: boolean; requiredPermissionKeys?: string[]; isActive?: boolean }> },
  savePermissions: vi.fn(),
  createUser: vi.fn(),
  resetPassword: vi.fn(),
  updateUser: vi.fn(),
  saveRole: vi.fn(),
  refreshUser: vi.fn(),
  refetch: vi.fn(),
  roleLookupEnabled: false,
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "actor-1", permissions: fixtures.actorPermissions }, refreshUser: fixtures.refreshUser }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ effectiveReadOnly: false, canMutate: true }) }));
vi.mock("../features/administration/api/admin", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/administration/api/admin")>();
  const withRoleCapabilities = <T extends { isSystem: boolean; isProtectedSystemAdmin?: boolean; isProtected?: boolean; isAssignable?: boolean; canEditDetails?: boolean; canManagePermissions?: boolean; canDelete?: boolean }>(role: T) => {
    const isProtected = role.isProtected ?? role.isProtectedSystemAdmin ?? false;
    return {
      ...role,
      isProtected,
      isAssignable: role.isAssignable ?? !isProtected,
      canEditDetails: role.canEditDetails ?? !isProtected,
      canManagePermissions: role.canManagePermissions ?? !isProtected,
      canDelete: role.canDelete ?? !isProtected,
    };
  };
  type CatalogFixture = typeof fixtures.assignablePermissions;
  let catalogSource: CatalogFixture | null = null;
  let mappedCatalog: Array<CatalogFixture[number] & { allowedCategories: string[] }> = [];
  const permissionCatalogForTest = () => {
    if (catalogSource !== fixtures.assignablePermissions) {
      catalogSource = fixtures.assignablePermissions;
      mappedCatalog = catalogSource.map((permission) => ({
        ...permission,
        allowedCategories: permission.allowedCategories ?? ["Tenant", "Organization", "Management", "Operation", "Business"],
        canGrantToOrganizationRole: permission.canGrantToOrganizationRole ?? true,
      }));
    }
    return mappedCatalog;
  };
  return {
    ...actual,
    useAdminUsers: () => ({ data: { items: fixtures.users, page: 1, pageSize: 25, totalCount: fixtures.users.length, totalPages: 1 }, isLoading: false, isError: false }),
    useAssignableRoles: (enabled: boolean) => {
      fixtures.roleLookupEnabled = enabled;
      return { data: fixtures.assignableRoles.map((role) => ({
        organizationId: null,
        organizationName: null,
        isOrganizationOwned: false,
        isLegacyOrganizationOwnershipUnresolved: false,
        isOrganizationOwnershipUnresolved: false,
        scope: "Tenant",
        ...role,
      })), isLoading: false, isError: false, refetch: fixtures.refetch };
    },
    useCreateAdminUser: () => ({ mutateAsync: fixtures.createUser, isPending: false }),
    useResetAdminUserPassword: () => ({ mutateAsync: fixtures.resetPassword, isPending: false }),
    useUpdateAdminUser: () => ({ mutateAsync: fixtures.updateUser, isPending: false }),
    useUserPermissions: () => ({ data: fixtures.permissions, isLoading: false, isError: false, refetch: fixtures.refetch }),
    useSaveUserPermissions: () => ({ mutateAsync: fixtures.savePermissions, isPending: false }),
    useManagedRoles: () => ({ data: fixtures.roles.map(withRoleCapabilities), isLoading: false, isError: false, refetch: fixtures.refetch }),
    useManagedRoleOrganizations: () => ({ data: [{ id: "organization-1", name: "NEXUS Group" }], isLoading: false, isError: false, refetch: fixtures.refetch }),
    usePermissionCatalog: () => ({ data: permissionCatalogForTest(), isLoading: false, isError: false, refetch: fixtures.refetch }),
    useManagedRole: () => ({ data: fixtures.selectedRole ? withRoleCapabilities(fixtures.selectedRole) : null, isLoading: false, isFetching: false, isError: false, refetch: fixtures.refetch }),
    useSaveManagedRole: () => ({ mutateAsync: fixtures.saveRole, isPending: false }),
    useDeleteManagedRole: () => ({ mutateAsync: vi.fn(), isPending: false }),
  };
});

function renderWithI18n(element: React.ReactNode, locale: "en" | "ar" = "en") {
  window.localStorage.setItem("nexus-locale", locale);
  return render(<I18nProvider>{element}</I18nProvider>);
}

function permissionCheckbox(container: HTMLElement, permissionId: string) {
  const checkbox = container.querySelector<HTMLInputElement>(`[id="role-permission-${permissionId}"]`);
  if (!checkbox) throw new Error(`Permission checkbox not found: ${permissionId}`);
  return checkbox;
}

describe("Access & Security component pages", () => {
  beforeEach(() => {
    window.localStorage.clear();
    fixtures.users = [{ id: "user-1", email: "maya@example.com", firstName: "Maya", lastName: "Admin", fullName: "Maya Admin", isActive: true, authenticationSource: "Local", mustChangePassword: false, roles: [{ id: "role-1", name: "Administrator", organizationId: null, organizationName: null, scope: "Tenant", isOrganizationOwned: false, isLegacyOrganizationOwnershipUnresolved: false, isOrganizationOwnershipUnresolved: false, isProtected: false, canRemove: true }] }];
    fixtures.permissions = [
      { id: "permission-1", name: "Project.View", resource: "Project", action: "View", labelEn: "View projects", isActive: true, isAssignable: true, isRoleGranted: true, roleGrantSources: ["Finance Reviewer", "Project Manager"], scope: "Project", overrideEffect: null, isEffective: true, canDelegate: true },
      { id: "permission-2", name: "Legacy.Users.Write", resource: "Legacy.Users", action: "Write", labelEn: "Legacy permission", isActive: false, isAssignable: false, isRoleGranted: false, overrideEffect: null, isEffective: false },
      { id: "permission-finances-read", name: "ProjectFinance.View", resource: "ProjectFinance", action: "View", labelEn: "Open Project Finance", labelAr: "فتح مساحة إدارة تمويل المشروع", isActive: true, isAssignable: true, isRoleGranted: false, overrideEffect: null, isEffective: false },
    ];
    fixtures.roles = [];
    fixtures.assignableRoles = [
      { id: "role-1", name: "Administrator", isSystem: false, isProtected: false, isAssignable: true, permissionCount: 2, assignedUserCount: 1 },
      { id: "role-2", name: "Project Manager", roleKey: "tenant.project-manager", isSystem: false, isProtected: false, isAssignable: true, permissionCount: 1, assignedUserCount: 0 },
    ];
    fixtures.roleLookupEnabled = false;
    fixtures.selectedRole = null;
    fixtures.actorPermissions = ["Users.View", "Users.ManageAccounts", "Users.ResetPassword", "Users.SetStatus", "Users.AssignRoles", "UserPermissions.Manage", "Roles.View", "Project.View"];
    fixtures.assignablePermissions = [];
    fixtures.savePermissions.mockReset();
    fixtures.createUser.mockReset();
    fixtures.resetPassword.mockReset();
    fixtures.updateUser.mockReset();
    fixtures.saveRole.mockReset();
    fixtures.refreshUser.mockReset().mockResolvedValue(undefined);
  });

  it("shows Access & Security components instead of repeating Administration categories", () => {
    renderWithI18n(<MemoryRouter><AccessPage /></MemoryRouter>);

    expect(screen.getByRole("heading", { name: "Access & Security" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Users/ })).toHaveAttribute("href", "/administration/access/users");
    expect(screen.getByRole("link", { name: /Roles & Permissions/ })).toHaveAttribute("href", "/administration/access/roles");
    expect(screen.queryByText("Organization")).not.toBeInTheDocument();
    expect(screen.queryByText("Reference Data")).not.toBeInTheDocument();
  });

  it("shows the Roles & Permissions entry to a role-permission administrator", () => {
    fixtures.actorPermissions = ["Roles.ConfigurePermissions"];
    renderWithI18n(<MemoryRouter><AccessPage /></MemoryRouter>);

    expect(screen.getByRole("link", { name: /Roles & Permissions/ })).toHaveAttribute("href", "/administration/access/roles");
  });

  it("does not show the role workspace to Users.View-only accounts", () => {
    fixtures.actorPermissions = ["Users.View"];
    renderWithI18n(<MemoryRouter><AccessPage /></MemoryRouter>);

    expect(screen.queryByRole("link", { name: /Roles & Permissions/ })).not.toBeInTheDocument();
  });

  it("does not show Roles & Permissions for a Users.ManageAccounts-only account", () => {
    fixtures.actorPermissions = ["Users.ManageAccounts"];
    renderWithI18n(<MemoryRouter><AccessPage /></MemoryRouter>);

    expect(screen.queryByRole("link", { name: /Roles & Permissions/ })).not.toBeInTheDocument();
  });

  it("does not render the side-menu Administration categories a second time above the page", () => {
    renderWithI18n(
      <MemoryRouter initialEntries={["/administration/access"]}>
        <Routes>
          <Route element={<AdministrationLayout />}>
            <Route path="/administration/access" element={<div>Access screen</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByText("Access screen")).toBeInTheDocument();
    expect(screen.queryByRole("navigation", { name: "Administration" })).not.toBeInTheDocument();
  });

  it("returns from Users to the Access & Security landing page", () => {
    renderWithI18n(
      <MemoryRouter initialEntries={["/administration/access/users"]}>
        <Routes>
          <Route path="/administration/access/users" element={<AccessUsersPage />} />
          <Route path="/administration/access" element={<AccessPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByRole("heading", { name: "User directory" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Back to Access & Security" }));
    expect(screen.getByRole("heading", { name: "Access & Security" })).toBeInTheDocument();
  });

  it("lets role assigners change user roles without granting account edit authority", async () => {
    fixtures.actorPermissions = ["Users.View", "Users.AssignRoles"];
    renderWithI18n(<MemoryRouter><AccessUsersPage /></MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: "Edit Maya Admin" }));
    expect(screen.getByLabelText("First Name")).toBeDisabled();
    expect(screen.getByLabelText("Email")).toBeDisabled();
    expect(screen.getByLabelText("Active")).toBeDisabled();

    fireEvent.click(screen.getByLabelText(/Project Manager/));
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    await waitFor(() => expect(fixtures.updateUser).toHaveBeenCalledWith(expect.objectContaining({
      userId: "user-1",
      input: expect.objectContaining({ roleIds: ["role-1", "role-2"] }),
    })));
  });

  it("loads the assignable role list for role assignment without Roles.View", async () => {
    fixtures.actorPermissions = ["Users.View", "Users.ManageAccounts", "Users.AssignRoles"];
    fixtures.assignableRoles = [{
      id: "role-project-manager",
      name: "Project Manager",
      category: "Management",
      roleKey: "tenant.project-manager",
      isSystem: false,
      isProtected: false,
      isAssignable: true,
      permissionCount: 3,
      assignedUserCount: 0,
    }];
    renderWithI18n(<MemoryRouter><AccessUsersPage /></MemoryRouter>);

    expect(fixtures.roleLookupEnabled).toBe(true);
    expect(fixtures.actorPermissions).not.toContain("Roles.View");
    fireEvent.click(screen.getByRole("button", { name: "Add User" }));
    const sheet = screen.getByRole("dialog", { name: "Add User" });
    const projectManager = within(sheet).getByRole("checkbox", { name: "Project Manager" });
    expect(projectManager).toBeEnabled();
    fireEvent.click(projectManager);
    fireEvent.click(within(sheet).getByRole("button", { name: "Create user" }));

    await waitFor(() => expect(fixtures.createUser).toHaveBeenCalledWith(expect.objectContaining({
      roleIds: ["role-project-manager"],
    })));
  });

  it("lets status administrators activate or deactivate accounts without profile edit authority", async () => {
    fixtures.actorPermissions = ["Users.View", "Users.SetStatus"];
    renderWithI18n(<MemoryRouter><AccessUsersPage /></MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: "Edit Maya Admin" }));
    expect(screen.getByLabelText("First Name")).toBeDisabled();
    expect(screen.getByLabelText("Email")).toBeDisabled();
    const active = screen.getByLabelText("Active");
    expect(active).toBeEnabled();
    fireEvent.click(active);
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    await waitFor(() => expect(fixtures.updateUser).toHaveBeenCalledWith(expect.objectContaining({
      userId: "user-1",
      input: expect.objectContaining({ isActive: false, roleIds: ["role-1"] }),
    })));
  });

  it("does not let an account editor change activation status without the status capability", () => {
    fixtures.actorPermissions = ["Users.View", "Users.ManageAccounts"];
    renderWithI18n(<MemoryRouter><AccessUsersPage /></MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: "Edit Maya Admin" }));
    expect(screen.getByLabelText("First Name")).toBeEnabled();
    expect(screen.getByLabelText("Active")).toBeDisabled();
  });

  it("returns from Roles & Permissions to the Access & Security landing page", () => {
    renderWithI18n(
      <MemoryRouter initialEntries={["/administration/access/roles"]}>
        <Routes>
          <Route path="/administration/access/roles" element={<RolesPage />} />
          <Route path="/administration/access" element={<AccessPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByRole("heading", { name: "Roles & Permissions" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Back to Access & Security" }));
    expect(screen.getByRole("heading", { name: "Access & Security" })).toBeInTheDocument();
  });

  it("opens direct user permissions on their own two-column screen", () => {
    renderWithI18n(
      <MemoryRouter initialEntries={["/administration/access/users"]}>
        <Routes>
          <Route path="/administration/access/users" element={<AccessUsersPage />} />
          <Route path="/administration/access/users/:userId/permissions" element={<UserPermissionsPage />} />
        </Routes>
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole("button", { name: "Manage permissions" }));
    expect(screen.getByRole("heading", { name: "Permissions for Maya Admin" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "User directory" })).not.toBeInTheDocument();
    expect(Array.from(document.querySelectorAll(".grid")).some((element) => element.className.includes("md:grid-cols-2"))).toBe(true);

    const roleGranted = screen.getByText("Granted by a role");
    const effective = screen.getByText("Effective");
    expect(screen.getByText("Roles: Finance Reviewer, Project Manager")).toBeInTheDocument();
    expect(screen.getByText((_, element) => element?.textContent === "Permission scope: Tenant scope")).toBeInTheDocument();
    const override = screen.getByLabelText("Direct override for View projects");
    expect(roleGranted).toHaveClass("text-info");
    expect(roleGranted.className).not.toMatch(/bg-(success|warning|destructive|info)/);
    expect(effective).toHaveClass("text-success");
    expect(effective.className).not.toMatch(/bg-(success|warning|destructive|info)/);
    expect(screen.getByLabelText("Direct override for Open Project Finance")).toBeDisabled();
    expect(screen.getByLabelText("Direct override for Open Project Finance").closest(".grid")?.querySelector("p span:last-child")?.textContent).toBe("Your account does not have this permission to change.");

    fireEvent.change(override, { target: { value: "Deny" } });
    expect(override).toHaveClass("text-destructive");
    expect(screen.getByText("Unsaved override: Deny directly")).toBeInTheDocument();
    fireEvent.change(override, { target: { value: "Allow" } });
    expect(override).toHaveClass("text-success");
    fireEvent.change(override, { target: { value: "Inherit" } });
    expect(override).toHaveClass("text-muted-foreground");
  });

  it("allows an unheld operational override when the server confirms delegation authority", async () => {
    fixtures.actorPermissions = ["UserPermissions.Manage"];
    fixtures.permissions = [{
      id: "permission-unheld-edit",
      name: "Project.Edit",
      resource: "Project",
      action: "Edit",
      labelEn: "Edit projects",
      isActive: true,
      isAssignable: true,
      isRoleGranted: false,
      overrideEffect: null,
      isEffective: false,
      scope: "Tenant",
      canDelegate: true,
    }];
    fixtures.savePermissions.mockResolvedValue(fixtures.permissions);
    renderWithI18n(<MemoryRouter initialEntries={["/administration/access/users/user-1/permissions"]}>
      <Routes><Route path="/administration/access/users/:userId/permissions" element={<UserPermissionsPage />} /></Routes>
    </MemoryRouter>);

    const override = await screen.findByLabelText("Direct override for Edit projects");
    expect(fixtures.actorPermissions).not.toContain("Project.Edit");
    expect(override).toBeEnabled();
    fireEvent.change(override, { target: { value: "Allow" } });
    fireEvent.click(screen.getByRole("button", { name: "Save permissions" }));

    await waitFor(() => expect(fixtures.savePermissions).toHaveBeenCalledWith({
      userId: "user-1",
      overrides: [{ permissionId: "permission-unheld-edit", effect: "Allow" }],
    }));
  });

  it("keeps an override disabled when server delegation policy denies it despite operational possession", async () => {
    fixtures.actorPermissions = ["UserPermissions.Manage", "Project.Edit"];
    fixtures.permissions = [{
      id: "permission-denied-edit",
      name: "Project.Edit",
      resource: "Project",
      action: "Edit",
      labelEn: "Edit projects",
      isActive: true,
      isAssignable: true,
      isRoleGranted: false,
      overrideEffect: null,
      isEffective: false,
      canDelegate: false,
    }];
    renderWithI18n(<MemoryRouter initialEntries={["/administration/access/users/user-1/permissions"]}>
      <Routes><Route path="/administration/access/users/:userId/permissions" element={<UserPermissionsPage />} /></Routes>
    </MemoryRouter>);

    const override = await screen.findByLabelText("Direct override for Edit projects");
    expect(override).toBeDisabled();
    expect(fixtures.savePermissions).not.toHaveBeenCalled();
  });

  it("preserves a Deny for a centrally protected permission and prevents adding a direct Allow", async () => {
    fixtures.permissions = [...fixtures.permissions, {
      id: "permission-admin-protected",
      name: "Roles.Delete",
      resource: "Roles",
      action: "Delete",
      labelEn: "Delete roles",
      isActive: true,
      isAssignable: false,
      isRoleGranted: false,
      overrideEffect: "Deny",
      isEffective: true,
      scope: "Tenant",
      canDelegate: true,
    }];
    fixtures.savePermissions.mockResolvedValue(fixtures.permissions);

    renderWithI18n(<MemoryRouter initialEntries={["/administration/access/users"]}>
      <Routes>
        <Route path="/administration/access/users" element={<AccessUsersPage />} />
        <Route path="/administration/access/users/:userId/permissions" element={<UserPermissionsPage />} />
      </Routes>
    </MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: "Manage permissions" }));
    const protectedOverride = screen.getByLabelText("Direct override for Delete roles");
    expect(protectedOverride).toBeEnabled();
    expect(protectedOverride).toHaveValue("Deny");
    expect(within(protectedOverride).getByRole("option", { name: "Allow directly" })).toBeDisabled();
    fireEvent.change(protectedOverride, { target: { value: "Allow" } });
    expect(protectedOverride).toHaveValue("Deny");
    expect(screen.getByRole("button", { name: "Save permissions" })).toBeEnabled();

    fireEvent.click(screen.getByRole("button", { name: "Save permissions" }));
    await waitFor(() => expect(fixtures.savePermissions).toHaveBeenCalledWith({
      userId: "user-1",
      overrides: [{ permissionId: "permission-admin-protected", effect: "Deny" }],
    }));
  });

  it("explains in Arabic why financial read cannot be delegated from the current account", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    render(<I18nProvider><MemoryRouter initialEntries={[{ pathname: "/administration/access/users/user-1/permissions", state: { userName: "Maya Admin" } }]}>
      <Routes>
        <Route path="/administration/access/users/:userId/permissions" element={<UserPermissionsPage />} />
      </Routes>
    </MemoryRouter></I18nProvider>);

    const financeOverride = document.querySelector<HTMLSelectElement>("#user-permission-permission-finances-read");
    expect(financeOverride).toBeDisabled();
    expect(financeOverride?.closest(".grid")?.querySelector("p span:last-child")?.textContent).toMatch(/\p{Script=Arabic}/u);
  });

  it("requires an administrator with broader access to review existing out-of-scope overrides", () => {
    fixtures.permissions = fixtures.permissions.map((permission) => permission.id === "permission-finances-read"
      ? { ...permission, overrideEffect: "Allow", isEffective: true }
      : permission);
    renderWithI18n(<MemoryRouter initialEntries={["/administration/access/users"]}>
      <Routes>
        <Route path="/administration/access/users" element={<AccessUsersPage />} />
        <Route path="/administration/access/users/:userId/permissions" element={<UserPermissionsPage />} />
      </Routes>
    </MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: "Manage permissions" }));
    expect(screen.getByRole("button", { name: "Save permissions" })).toBeDisabled();
    expect(screen.getByText("This user has direct overrides outside your effective access. Another administrator must review them before you can save changes.")).toBeInTheDocument();
  });

  it("uses semantic text-only status colors and consistent primary/secondary button sizes", () => {
    renderWithI18n(<MemoryRouter><AccessUsersPage /></MemoryRouter>);

    const activeStatus = screen.getByText("Active");
    expect(activeStatus).toHaveClass("text-success");
    expect(activeStatus.className).not.toMatch(/bg-(success|warning|destructive|info)/);
    expect(screen.getByRole("button", { name: "Add User" })).toHaveClass("h-10", "px-4");
    expect(screen.getByRole("button", { name: "Edit Maya Admin" })).toHaveClass("h-9", "px-3");
  });

  it("creates a local user without password fields and only shows its temporary password once", async () => {
    fixtures.createUser.mockResolvedValue({ user: { ...fixtures.users[0], id: "user-2", email: "sam@example.com", fullName: "Sam Taylor", roles: [] }, temporaryPassword: "secret-temp-pass" });
    renderWithI18n(<MemoryRouter><AccessUsersPage /></MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: "Add User" }));
    const sheet = screen.getByRole("dialog", { name: "Add User" });
    fireEvent.change(within(sheet).getByLabelText("First Name"), { target: { value: "Sam" } });
    fireEvent.change(within(sheet).getByLabelText("Last Name"), { target: { value: "Taylor" } });
    fireEvent.change(within(sheet).getByLabelText("Email"), { target: { value: "sam@example.com" } });
    expect(within(sheet).queryByLabelText("Password")).not.toBeInTheDocument();
    fireEvent.click(within(sheet).getByRole("checkbox", { name: "Project Manager" }));
    fireEvent.click(within(sheet).getByRole("button", { name: "Create user" }));

    await waitFor(() => expect(fixtures.createUser).toHaveBeenCalledWith({
      email: "sam@example.com",
      firstName: "Sam",
      lastName: "Taylor",
      isActive: true,
      roleIds: ["role-2"],
      authenticationSource: "Local",
    }));
    expect(await screen.findByRole("heading", { name: "Temporary password generated" })).toBeInTheDocument();
    expect(screen.getByLabelText("Temporary password")).toHaveValue("secret-temp-pass");
    fireEvent.click(screen.getByRole("button", { name: "Close" }));
    expect(screen.queryByLabelText("Temporary password")).not.toBeInTheDocument();
  });

  it("offers password reset only for local users with the dedicated permission", async () => {
    fixtures.resetPassword.mockResolvedValue({ userId: "user-1", temporaryPassword: "reset-once", expiresAt: "2026-10-13T00:00:00Z" });
    renderWithI18n(<MemoryRouter><AccessUsersPage /></MemoryRouter>);
    fireEvent.click(screen.getByRole("button", { name: "Generate temporary password" }));
    await waitFor(() => expect(fixtures.resetPassword).toHaveBeenCalledWith("user-1"));
    expect(await screen.findByLabelText("Temporary password")).toHaveValue("reset-once");
  });

  it("edits profile and roles together in a Side Sheet", async () => {
    fixtures.updateUser.mockResolvedValue({ ...fixtures.users[0], roles: [
      ...fixtures.users[0].roles,
      { id: "role-2", name: "Project Manager", organizationId: null, organizationName: null, scope: "Tenant", isOrganizationOwned: false, isLegacyOrganizationOwnershipUnresolved: false, isOrganizationOwnershipUnresolved: false, isProtected: false, canRemove: true },
    ] });
    renderWithI18n(<MemoryRouter><AccessUsersPage /></MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: "Edit Maya Admin" }));
    const sheet = screen.getByRole("dialog", { name: "Edit User" });
    expect(within(sheet).getByLabelText("First Name")).toHaveValue("Maya");
    fireEvent.click(within(sheet).getByRole("checkbox", { name: "Project Manager" }));
    fireEvent.click(within(sheet).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(fixtures.updateUser).toHaveBeenCalledWith({
      userId: "user-1",
      input: {
        email: "maya@example.com",
        firstName: "Maya",
        lastName: "Admin",
        isActive: true,
        roleIds: ["role-1", "role-2"],
      },
    }));
  });

  it("keeps self role and active-status controls locked while allowing profile editing", () => {
    fixtures.users = [{ ...fixtures.users[0], id: "actor-1" }];
    renderWithI18n(<MemoryRouter><AccessUsersPage /></MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: "Edit Maya Admin" }));
    const sheet = screen.getByRole("dialog", { name: "Edit User" });
    expect(within(sheet).getByLabelText("First Name")).toBeEnabled();
    expect(within(sheet).getByRole("checkbox", { name: "Administrator" })).toBeDisabled();
    expect(within(sheet).getByRole("checkbox", { name: /Active/ })).toBeDisabled();
    expect(within(sheet).getByText("Your role assignments can only be changed by another administrator.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Manage permissions" })).not.toBeInTheDocument();
  });

  it("preserves an unassignable current role and requires removing it before assigning a replacement", () => {
    fixtures.users = [{ ...fixtures.users[0], roles: [{ id: "role-elevated", name: "Elevated Admin", organizationId: null, organizationName: null, scope: "Tenant", isOrganizationOwned: false, isLegacyOrganizationOwnershipUnresolved: false, isOrganizationOwnershipUnresolved: false, isProtected: false, canRemove: true }] }];
    renderWithI18n(<MemoryRouter><AccessUsersPage /></MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: "Edit Maya Admin" }));
    const sheet = screen.getByRole("dialog", { name: "Edit User" });
    const assignedRole = within(sheet).getByRole("checkbox", { name: /^Elevated Admin/ });
    const replacementRole = within(sheet).getByRole("checkbox", { name: "Project Manager" });

    expect(assignedRole).toBeChecked();
    expect(replacementRole).toBeDisabled();
    fireEvent.click(assignedRole);
    expect(assignedRole).not.toBeChecked();
    expect(replacementRole).toBeEnabled();
  });

  it("opens both new-role and edit-role forms in a Side Sheet", () => {
    fixtures.actorPermissions = ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions"];
    fixtures.roles = [
      { id: "role-1", name: "Analyst", isSystem: false, isProtected: false, isAssignable: true, canEditDetails: true, canManagePermissions: true, canDelete: true, permissionCount: 1, assignedUserCount: 0 },
      { id: "role-2", name: "Tenant Admin", roleKey: "tenant.admin", isSystem: true, isProtected: true, isAssignable: false, canEditDetails: false, canManagePermissions: false, canDelete: false, isProtectedSystemAdmin: true, permissionCount: 4, assignedUserCount: 1 },
      { id: "role-3", name: "Organization Admin", roleKey: "organization.admin", isSystem: true, isProtected: false, isAssignable: false, canEditDetails: false, canManagePermissions: false, canDelete: false, permissionCount: 2, assignedUserCount: 0 },
    ];
    fixtures.selectedRole = { ...fixtures.roles[0], description: "Read-only analyst", category: "Management", permissions: [{ id: "permission-1", name: "Project.View", resource: "Project", action: "View", moduleId: "project-management", moduleNameEn: "Project Management", moduleNameAr: "إدارة المشاريع", screenId: "projects", screenNameEn: "Projects", screenNameAr: "المشاريع", level: "View", labelEn: "View project records", labelAr: "عرض سجلات المشروع" }] };
    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);

    expect(screen.getAllByText("System role")).toHaveLength(2);
    for (const badge of screen.getAllByText("System role")) {
      expect(badge).toHaveClass("text-info", "bg-transparent");
      expect(badge.className).not.toMatch(/bg-(success|warning|destructive|info)/);
    }
    fireEvent.click(screen.getByRole("button", { name: /New role/ }));
    expect(screen.getByRole("dialog", { name: "New role" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));

    fireEvent.click(screen.getByRole("button", { name: /Analyst/ }));
    fireEvent.click(screen.getByRole("button", { name: "Edit" }));
    expect(screen.getByRole("dialog", { name: "Edit role" })).toBeInTheDocument();
  });

  it("labels generic ApprovalRequest permissions without a verified UI control in English and Arabic", async () => {
    const user = userEvent.setup();
    fixtures.actorPermissions = ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions"];
    const approvalView = {
      id: "permission-approval-request-view",
      name: "ApprovalRequest.View",
      resource: "ApprovalRequest",
      action: "View",
      moduleId: "project-delivery",
      moduleNameEn: "Delivery Management",
      moduleNameAr: "إدارة التسليم",
      screenId: "approvals",
      screenNameEn: "Approvals",
      screenNameAr: "الموافقات",
      scope: "Tenant" as const,
      level: "View" as const,
      labelEn: "View approval requests",
      labelAr: "عرض طلبات الموافقة",
      descriptionEn: "View approval requests within tenant and project scope.",
      descriptionAr: "عرض طلبات الموافقة ضمن نطاق المستأجر والمشروع.",
      sensitivity: "Low" as const,
      isSensitive: false,
      requiredPermissionKeys: [],
      canDelegate: true,
      canDelegateToCustomRole: true,
      isActive: true,
      isAssignable: true,
      allowedCategories: ["Management"],
    };
    fixtures.assignablePermissions = [approvalView];

    for (const locale of ["en", "ar"] as const) {
      const labels = locale === "ar" ? arMessages.administration.areas.roles : enMessages.administration.areas.roles;
      const rendered = renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>, locale);
      await user.click(screen.getByRole("button", { name: labels.newRole }));
      const editor = await screen.findByRole("dialog");
      await user.selectOptions(within(editor).getByRole("combobox", { name: labels.category }), "Management");
      fireEvent.change(within(editor).getByRole("textbox", { name: labels.searchPermissions }), {
        target: { value: approvalView.name },
      });

      const checkbox = permissionCheckbox(editor, approvalView.id);
      expect(within(editor).getByText(labels.unverifiedUiSurface)).toBeInTheDocument();
      const infoId = checkbox.getAttribute("aria-describedby");
      expect(infoId).toBeTruthy();
      expect(document.getElementById(infoId!)).toHaveTextContent(labels.unverifiedUiSurface);
      rendered.unmount();
    }

    fixtures.roles = [{ id: "role-approval-reviewer", name: "Approval Reviewer", category: "Management", isSystem: false, permissionCount: 1, assignedUserCount: 0 }];
    fixtures.selectedRole = {
      id: "role-approval-reviewer",
      name: "Approval Reviewer",
      description: "",
      category: "Management",
      isSystem: false,
      permissionCount: 1,
      assignedUserCount: 0,
      permissions: [approvalView],
    };
    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>, "en");
    await user.click(screen.getByRole("button", { name: /Approval Reviewer/ }));
    expect(screen.getByText(enMessages.administration.areas.roles.unverifiedUiSurface)).toBeInTheDocument();
  });

  it("creates a custom role from catalog-authorized permissions and preserves selection through search, filters, and collapse", async () => {
    const user = userEvent.setup();
    fixtures.actorPermissions = ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions", "Project.View", "Project.Edit"];
    fixtures.roles = [];
    fixtures.selectedRole = null;
    const projectView = {
      id: "permission-create-project-view",
      name: "Project.View",
      resource: "Project",
      action: "View",
      moduleId: "project-management",
      moduleNameEn: "Project Management",
      moduleNameAr: "إدارة المشاريع",
      screenId: "projects",
      screenNameEn: "Projects",
      screenNameAr: "المشاريع",
      scope: "Project" as const,
      level: "View" as const,
      allowedCategories: ["Management"],
      labelEn: "View project records",
      labelAr: "عرض سجلات المشروع",
      descriptionEn: "Open the project workspace.",
      descriptionAr: "افتح مساحة المشروع.",
      sensitivity: "Low" as const,
      isSensitive: false,
      canDelegate: true,
      canDelegateToCustomRole: true,
      isAssignable: true,
      requiredPermissionKeys: [],
      isActive: true,
    };
    const projectEdit = {
      ...projectView,
      id: "permission-create-project-edit",
      name: "Project.Edit",
      action: "Edit",
      level: "Action" as const,
      labelEn: "Edit project records",
      labelAr: "عدّل سجلات المشروع",
      descriptionEn: "Update project records.",
      descriptionAr: "حدّث سجلات المشروع.",
      requiredPermissionKeys: ["Project.View"],
    };
    const projectDelete = {
      ...projectView,
      id: "permission-create-project-delete",
      name: "Project.Delete",
      action: "Delete",
      level: "Action" as const,
      labelEn: "Delete project records",
      labelAr: "احذف سجلات المشروع",
      descriptionEn: "Delete project records.",
      descriptionAr: "احذف سجلات المشروع.",
      sensitivity: "High" as const,
      isSensitive: true,
      canDelegate: false,
      canDelegateToCustomRole: true,
      requiredPermissionKeys: ["Project.View"],
    };
    fixtures.assignablePermissions = [projectView, projectEdit, projectDelete];
    fixtures.saveRole.mockResolvedValue({
      id: "created-project-editor",
      name: "Project Editor",
      description: "",
      category: "Management",
      isSystem: false,
      permissionCount: 3,
      assignedUserCount: 0,
      permissions: [projectView, projectEdit, projectDelete],
    });

    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);
    await user.click(screen.getByRole("button", { name: "New role" }));
    const editor = await screen.findByRole("dialog", { name: "New role" });
    await user.selectOptions(within(editor).getByRole("combobox", { name: "Category" }), "Management");
    await user.click(within(editor).getByRole("button", { name: /Projects/ }));

    const viewCheckbox = permissionCheckbox(editor, "permission-create-project-view");
    const editCheckbox = permissionCheckbox(editor, "permission-create-project-edit");
    const deleteCheckbox = permissionCheckbox(editor, "permission-create-project-delete");
    expect(viewCheckbox).toBeEnabled();
    expect(editCheckbox).toBeEnabled();
    expect(fixtures.actorPermissions).not.toContain(projectDelete.name);
    expect(deleteCheckbox).toBeEnabled();

    await user.click(editCheckbox);
    await user.click(deleteCheckbox);
    expect(permissionCheckbox(editor, "permission-create-project-view")).toBeChecked();
    expect(permissionCheckbox(editor, "permission-create-project-edit")).toBeChecked();
    expect(permissionCheckbox(editor, "permission-create-project-delete")).toBeChecked();
    expect(within(editor).getByText("3 of 3 active permissions selected")).toBeInTheDocument();

    const search = within(editor).getByRole("textbox", { name: "Search names, keys, and descriptions..." });
    await user.type(search, "Project.Edit");
    expect(permissionCheckbox(editor, "permission-create-project-edit")).toBeChecked();
    expect(permissionCheckbox(editor, "permission-create-project-view")).toBeChecked();

    const projectGroupToggle = within(editor).getByRole("button", { name: /Projects/ });
    await user.click(projectGroupToggle);
    expect(projectGroupToggle).toHaveAttribute("aria-expanded", "false");
    await user.click(projectGroupToggle);
    expect(permissionCheckbox(editor, "permission-create-project-edit")).toBeChecked();

    await user.clear(search);
    await user.selectOptions(within(editor).getByRole("combobox", { name: "Permission level" }), "Action");
    expect(permissionCheckbox(editor, "permission-create-project-view")).toBeChecked();
    expect(permissionCheckbox(editor, "permission-create-project-edit")).toBeChecked();
    expect(permissionCheckbox(editor, "permission-create-project-delete")).toBeChecked();
    await user.selectOptions(within(editor).getByRole("combobox", { name: "Permission level" }), "all");
    await user.selectOptions(within(editor).getByRole("combobox", { name: "Grant status" }), "assigned");
    expect(permissionCheckbox(editor, "permission-create-project-view")).toBeChecked();
    expect(permissionCheckbox(editor, "permission-create-project-edit")).toBeChecked();
    expect(permissionCheckbox(editor, "permission-create-project-delete")).toBeChecked();

    await user.type(within(editor).getByLabelText("Role name"), "Project Editor");
    await user.click(within(editor).getByRole("button", { name: "Save role" }));
    await waitFor(() => expect(fixtures.saveRole).toHaveBeenCalledWith(expect.objectContaining({
      roleId: null,
      input: expect.objectContaining({
        name: "Project Editor",
        permissionIds: expect.arrayContaining([projectView.id, projectEdit.id, projectDelete.id]),
      }),
    })));
    const savedInput = fixtures.saveRole.mock.calls[0][0].input;
    expect(savedInput.permissionIds).toHaveLength(3);
    expect(savedInput.permissionIds).toContain(projectDelete.id);
    await waitFor(() => expect(fixtures.refreshUser).toHaveBeenCalledTimes(1));
  });

  it("keeps the protected Tenant Admin role read-only for role-permission operators", async () => {
    fixtures.actorPermissions = ["Roles.View", "Roles.ConfigurePermissions"];
    const baselinePermission = {
      id: "tenant-admin-roles-view",
      name: "Roles.View",
      resource: "Roles",
      action: "View",
      labelEn: "View role permissions",
      isActive: true,
      isAssignable: true,
    };
    const tenantAdmin = {
      id: "protected-tenant-admin",
      name: "Tenant Admin",
      roleKey: "tenant.admin",
      description: "Protected tenant administrator",
      category: "System",
      isSystem: true,
      isProtected: true,
      isAssignable: false,
      canEditDetails: false,
      canManagePermissions: false,
      canDelete: false,
      isProtectedSystemAdmin: true,
      permissionCount: 1,
      assignedUserCount: 1,
      permissions: [baselinePermission],
    };
    fixtures.roles = [tenantAdmin];
    fixtures.selectedRole = tenantAdmin;
    fixtures.assignablePermissions = [baselinePermission];

    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);
    await userEvent.setup().click(screen.getByRole("button", { name: /Tenant Admin/ }));

    expect(screen.getByRole("button", { name: "Edit" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Delete role" })).toBeDisabled();
    expect(screen.queryByRole("dialog", { name: "Edit role" })).not.toBeInTheDocument();
    expect(fixtures.saveRole).not.toHaveBeenCalled();
  });
  it("lets Tenant Administration scope configure role permissions and refreshes effective access after save", async () => {
    const user = userEvent.setup();
    fixtures.actorPermissions = ["Roles.ConfigurePermissions"];
    const projectView = {
      id: "permission-view",
      name: "Project.View",
      resource: "Project",
      action: "View",
      moduleId: "project-management",
      moduleNameEn: "Project Management",
      moduleNameAr: "تسليم المشاريع",
      screenId: "projects",
      screenNameEn: "Projects",
      screenNameAr: "المشاريع",
      level: "View" as const,
      labelEn: "View project data",
      labelAr: "عرض بيانات المشروع",
      descriptionEn: "View project data.",
      descriptionAr: "عرض بيانات المشروع.",
      sensitivity: "Low" as const,
      isSensitive: false,
      scope: "Project" as const,
      canDelegate: true,
      canDelegateToCustomRole: true,
      isAssignable: true,
      requiredPermissionKeys: [],
      isActive: true,
    };
    const projectSettingsView = {
      ...projectView,
      id: "permission-project-settings-view",
      name: "ProjectSettings.View",
      resource: "ProjectSettings",
      scope: "Tenant" as const,
      labelEn: "View project records",
      labelAr: "??? ????? ???????",
      descriptionEn: "Review project configuration settings.",
      requiredPermissionKeys: [],
    };
    const projectEdit = {
      ...projectView,
      id: "permission-edit",
      name: "Project.Edit",
      action: "Edit",
      level: "Action" as const,
      labelEn: "Edit project data",
      labelAr: "تعديل بيانات المشروع",
      requiredPermissionKeys: ["Project.View"],
    };
    fixtures.roles = [{ id: "role-tenant", name: "Project Reader", isSystem: false, permissionCount: 1, assignedUserCount: 0 }];
    fixtures.selectedRole = { ...fixtures.roles[0], description: "", category: "Management", permissions: [projectView] };
    fixtures.assignablePermissions = [projectView, projectSettingsView, projectEdit];
    fixtures.saveRole.mockResolvedValue(fixtures.selectedRole);

    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);

    expect(screen.getByRole("button", { name: "New role" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: /Project Reader/ }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Edit" })).toBeEnabled());
    expect(screen.getByRole("button", { name: "Delete role" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "Edit" }));

    const editRole = await screen.findByRole("dialog", { name: "Edit role" });
    expect(within(editRole).getByLabelText("Role name")).toBeDisabled();
    await user.click(within(editRole).getByRole("button", { name: /Projects/ }));
    const editPermission = within(editRole).getByRole("checkbox", { name: /Edit project data/ });
    expect(editPermission).toBeEnabled();
    await user.click(editPermission);
    expect(editPermission).toBeChecked();
    await user.click(within(editRole).getByRole("button", { name: "Save role" }));

    const confirmation = await screen.findByRole("alertdialog", { name: "Save permission changes?" });
    await user.click(within(confirmation).getByRole("button", { name: "Save role" }));

    await waitFor(() => expect(fixtures.saveRole).toHaveBeenCalled());
    await waitFor(() => expect(fixtures.refreshUser).toHaveBeenCalledTimes(1));
  });

  it("searches role permissions by canonical key and description and explains required permissions", async () => {
    const user = userEvent.setup();
    fixtures.actorPermissions = ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions"];
    const projectView = {
      id: "permission-project-view",
      name: "Project.View",
      resource: "Project",
      action: "View",
      moduleId: "project-management",
      moduleNameEn: "Project Management",
      moduleNameAr: "إدارة المشاريع",
      screenId: "projects",
      screenNameEn: "Projects",
      screenNameAr: "المشروعات",
      level: "View" as const,
      labelEn: "View project records",
      labelAr: "عرض سجلات المشروع",
      descriptionEn: "Open the project workspace and review project details.",
      scope: "Project" as const,
      canDelegate: true,
      canDelegateToCustomRole: true,
      descriptionAr: "افتح مساحة المشروع وراجع تفاصيل المشروع.",
      sensitivity: "Low" as const,
      isSensitive: false,
      isAssignable: true,
      requiredPermissionKeys: [],
      isActive: true,
    };
    const projectSettingsView = {
      ...projectView,
      id: "permission-project-settings-view",
      name: "ProjectSettings.View",
      resource: "ProjectSettings",
      screenId: "project-settings",
      screenNameEn: "Project Settings",
      screenNameAr: "إعدادات المشروع",
      scope: "Tenant" as const,
      labelEn: "View project records",
      labelAr: "عرض سجلات المشروع",
      descriptionEn: "Review project configuration settings.",
      descriptionAr: "راجع إعدادات تكوين المشروع.",
    };
    const projectEdit = {
      ...projectView,
      id: "permission-project-edit",
      name: "Project.Edit",
      action: "Edit",
      level: "Action" as const,
      labelEn: "Edit project records",
      labelAr: "تعديل سجلات المشروع",
      descriptionEn: "Change approved project records after validation.",
      descriptionAr: "غيّر سجلات المشروع المعتمدة بعد التحقق.",
      requiredPermissionKeys: ["Project.View"],
    };
    fixtures.assignablePermissions = [projectView, projectSettingsView, projectEdit];

    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);
    await user.click(screen.getByRole("button", { name: "New role" }));
    const editor = await screen.findByRole("dialog", { name: "New role" });
    await user.selectOptions(within(editor).getByRole("combobox", { name: "Category" }), "Management");
    const search = within(editor).getByRole("textbox", { name: "Search names, keys, and descriptions..." });

    fireEvent.change(search, { target: { value: "View project records" } });
    const viewPermission = permissionCheckbox(editor, "permission-project-view");
    const projectSettingsPermission = permissionCheckbox(editor, "permission-project-settings-view");
    expect(viewPermission).toBeInTheDocument();
    expect(projectSettingsPermission).toBeInTheDocument();
    expect(editor.querySelectorAll('[id="role-permission-permission-project-view"]')).toHaveLength(1);
    expect(editor.querySelectorAll('[id="role-permission-permission-project-settings-view"]')).toHaveLength(1);
    expect(within(editor).getByText("Project.View")).toBeInTheDocument();
    expect(within(editor).getByText("ProjectSettings.View")).toBeInTheDocument();
    expect(within(editor).getAllByText("Project scope").length).toBeGreaterThan(0);
    expect(within(editor).getAllByText("Tenant scope").length).toBeGreaterThan(0);

    fireEvent.change(search, { target: { value: "Project.Edit" } });
    const editPermission = within(editor).getByRole("checkbox", { name: /Edit project records/ });
    const editPermissionWithCanonicalKey = within(editor).getByRole("checkbox", { name: /\(Project\.Edit\)$/ });
    const requiredViewPermission = permissionCheckbox(editor, "permission-project-view");
    expect(editPermission).toBeInTheDocument();
    expect(editPermissionWithCanonicalKey).toBe(editPermission);
    expect(within(editor).getByText("Project.Edit")).toBeInTheDocument();
    expect(requiredViewPermission).toBeInTheDocument();
    expect(within(editor).getAllByText(/View project records/).length).toBeGreaterThan(1);
    expect(within(editor).getByText(/Requires:.*View project records/)).toBeInTheDocument();
    expect(within(editor).getAllByText("Project scope").length).toBeGreaterThan(0);

    await user.click(editPermission);
    expect(editPermission).toBeChecked();
    expect(requiredViewPermission).toBeChecked();

    fireEvent.change(search, { target: { value: "Edit project records" } });
    expect(within(editor).getByRole("checkbox", { name: /Edit project records/ })).toBeChecked();

    fireEvent.change(search, { target: { value: "change approved project records" } });
    expect(within(editor).getByRole("checkbox", { name: /Edit project records/ })).toBeChecked();
  });

  it("filters role permissions by business area, scope, level, grant state, and sensitivity", async () => {
    const user = userEvent.setup();
    fixtures.actorPermissions = ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions"];
    const projectView = {
      id: "permission-project-view",
      name: "Project.View",
      resource: "Project",
      action: "View",
      moduleId: "project-management",
      moduleNameEn: "Project Management",
      moduleNameAr: "إدارة المشاريع",
      screenId: "projects",
      screenNameEn: "Projects",
      screenNameAr: "المشروعات",
      level: "View" as const,
      allowedCategories: ["Management"],
      labelEn: "View project records",
      labelAr: "عرض سجلات المشروع",
      descriptionEn: "Open the project workspace and review project details.",
      scope: "Project" as const,
      canDelegate: true,
      canDelegateToCustomRole: true,
      descriptionAr: "افتح مساحة المشروع وراجع تفاصيل المشروع.",
      sensitivity: "Low" as const,
      isSensitive: false,
      isAssignable: true,
      requiredPermissionKeys: [],
      isActive: true,
    };
    const projectEdit = {
      ...projectView,
      id: "permission-project-edit",
      name: "Project.Edit",
      action: "Edit",
      level: "Action" as const,
      labelEn: "Edit project records",
      labelAr: "تعديل سجلات المشروع",
      descriptionEn: "Change approved project records after validation.",
      descriptionAr: "غيّر سجلات المشروع المعتمدة بعد التحقق.",
      requiredPermissionKeys: ["Project.View"],
    };
    const projectDelete = {
      ...projectView,
      id: "permission-project-delete",
      name: "Project.Delete",
      action: "Delete",
      level: "Action" as const,
      labelEn: "Delete project records",
      labelAr: "حذف سجلات المشروع",
      descriptionEn: "Delete project records after required approval.",
      descriptionAr: "احذف سجلات المشروع بعد الموافقة المطلوبة.",
      sensitivity: "High" as const,
      isSensitive: true,
      canDelegate: false,
      canDelegateToCustomRole: true,
      requiredPermissionKeys: ["Project.View"],
    };
    const portfolioView = {
      ...projectView,
      id: "permission-portfolio-view",
      name: "Portfolio.View",
      resource: "Portfolio",
      scope: "Portfolio" as const,
      moduleId: "portfolio-program-management",
      moduleNameEn: "Portfolio & Program Management",
      moduleNameAr: "إدارة المحافظ والبرامج",
      screenId: "portfolios",
      screenNameEn: "Portfolios",
      screenNameAr: "المحافظ",
      labelEn: "View portfolio records",
      labelAr: "عرض سجلات المحفظة",
      descriptionEn: "Open a portfolio and review its programs.",
      descriptionAr: "افتح المحفظة وراجع البرامج التابعة لها.",
    };
    fixtures.assignablePermissions = [projectView, projectEdit, projectDelete, portfolioView];

    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);
    await user.click(screen.getByRole("button", { name: "New role" }));
    const editor = await screen.findByRole("dialog", { name: "New role" });
    await user.selectOptions(within(editor).getByRole("combobox", { name: "Category" }), "Management");
    const moduleFilter = within(editor).getByRole("combobox", { name: "Business area" });
    const scopeFilter = within(editor).getByRole("combobox", { name: "Permission scope" });
    const levelFilter = within(editor).getByRole("combobox", { name: "Permission level" });
    const grantFilter = within(editor).getByRole("combobox", { name: "Grant status" });
    const sensitivityFilter = within(editor).getByRole("combobox", { name: "Sensitivity" });

    await user.selectOptions(scopeFilter, "Portfolio");
    const portfolioToggle = within(editor).getByRole("button", { name: /Portfolios/ });
    await user.click(portfolioToggle);
    expect(within(editor).getByRole("checkbox", { name: /View portfolio records/ })).toBeInTheDocument();
    expect(within(editor).queryByRole("checkbox", { name: /View project records/ })).not.toBeInTheDocument();
    await user.selectOptions(scopeFilter, "all");

    await user.selectOptions(moduleFilter, "project-management");
    const screenToggle = within(editor).getByRole("button", { name: /Projects/ });
    expect(screenToggle).toHaveAttribute("aria-expanded", "false");
    await user.click(screenToggle);
    expect(screenToggle).toHaveAttribute("aria-expanded", "true");
    expect(within(editor).getByRole("checkbox", { name: /Edit project records/ })).toBeInTheDocument();
    expect(within(editor).queryByRole("checkbox", { name: /View portfolio records/ })).not.toBeInTheDocument();

    await user.selectOptions(levelFilter, "Action");
    expect(within(editor).getByRole("checkbox", { name: /View project records/ })).toBeInTheDocument();
    expect(within(editor).getByText("Required by a matching permission")).toBeInTheDocument();
    expect(within(editor).getByRole("checkbox", { name: /Edit project records/ })).toBeInTheDocument();

    await user.selectOptions(sensitivityFilter, "High");
    const deletePermission = within(editor).getByRole("checkbox", { name: /Delete project records/ });
    expect(deletePermission).toBeInTheDocument();
    expect(fixtures.actorPermissions).not.toContain(projectDelete.name);
    expect(deletePermission).toBeEnabled();
    expect(within(editor).queryByText("Your current access does not allow you to grant this permission.")).not.toBeInTheDocument();
    await user.click(deletePermission);
    expect(deletePermission).toBeChecked();
    expect(within(editor).queryByRole("checkbox", { name: /Edit project records/ })).not.toBeInTheDocument();

    await user.selectOptions(sensitivityFilter, "all");
    await user.selectOptions(levelFilter, "all");
    await user.selectOptions(grantFilter, "unassigned");
    await user.click(within(editor).getByRole("checkbox", { name: /Edit project records/ }));
    await user.selectOptions(grantFilter, "assigned");
    expect(within(editor).getByRole("checkbox", { name: /View project records/ })).toBeChecked();
    expect(within(editor).getByRole("checkbox", { name: /Edit project records/ })).toBeChecked();
    expect(within(editor).getByRole("checkbox", { name: /Delete project records/ })).toBeChecked();

    await user.selectOptions(grantFilter, "unassigned");
    expect(within(editor).queryByRole("checkbox", { name: /Edit project records/ })).not.toBeInTheDocument();
    expect(within(editor).queryByRole("checkbox", { name: /Delete project records/ })).not.toBeInTheDocument();
  });

  it("allows role administrators to grant permissions across Role Categories and saves the selection", async () => {
    const user = userEvent.setup();
    fixtures.actorPermissions = ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions"];
    const permissionBase = {
      resource: "Project",
      moduleId: "project-management",
      moduleNameEn: "Project Management",
      moduleNameAr: "إدارة المشاريع",
      screenId: "projects",
      screenNameEn: "Projects",
      screenNameAr: "المشاريع",
      scope: "Project" as const,
      canDelegate: true,
      canDelegateToCustomRole: true,
      sensitivity: "Low" as const,
      isSensitive: false,
      isAssignable: true,
      isActive: true,
      requiredPermissionKeys: [] as string[],
    };
    const projectView = {
      ...permissionBase,
      id: "category-project-view",
      name: "Project.View",
      action: "View",
      level: "View" as const,
      labelEn: "View project records",
      labelAr: "عرض سجلات المشروع",
      descriptionEn: "View project records.",
      descriptionAr: "عرض سجلات المشروع.",
      allowedCategories: ["Management", "Operation"],
    };
    const managementView = {
      ...projectView,
      id: "category-management-view",
      name: "ProjectAudit.View",
      labelEn: "View project audit",
      labelAr: "عرض سجل تدقيق المشروع",
      allowedCategories: ["Management"],
    };
    const managementAction = {
      ...permissionBase,
      id: "category-management-action",
      name: "Project.Approve",
      action: "Approve",
      level: "Action" as const,
      labelEn: "Approve project changes",
      labelAr: "اعتماد تغييرات المشروع",
      descriptionEn: "Approve project changes.",
      descriptionAr: "اعتماد تغييرات المشروع.",
      requiredPermissionKeys: ["Project.View"],
      allowedCategories: ["Management"],
    };
    const crossCategoryAction = {
      ...permissionBase,
      id: "category-cross-action",
      name: "Project.Edit",
      action: "Edit",
      level: "Action" as const,
      labelEn: "Edit project records",
      labelAr: "تعديل سجلات المشروع",
      descriptionEn: "Edit project records.",
      descriptionAr: "تعديل سجلات المشروع.",
      requiredPermissionKeys: ["Project.View"],
      allowedCategories: ["Management", "Operation"],
    };
    fixtures.assignablePermissions = [projectView, managementView, managementAction, crossCategoryAction];
    fixtures.saveRole.mockResolvedValue({
      id: "created-cross-category-role",
      name: "Cross Category Reviewer",
      description: "",
      category: "Operation",
      isSystem: false,
      permissionCount: 4,
      assignedUserCount: 0,
      permissions: [projectView, managementView, managementAction, crossCategoryAction],
    });

    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);
    await user.click(screen.getByRole("button", { name: "New role" }));
    const editor = await screen.findByRole("dialog", { name: "New role" });
    await user.selectOptions(within(editor).getByRole("combobox", { name: "Organization" }), "organization-1");
    const category = within(editor).getByRole("combobox", { name: "Category" });
    expect([...category.querySelectorAll("option")].map((option) => option.value)).toEqual(["", "Tenant", "Organization", "Management", "Operation", "Business"]);
    await user.selectOptions(category, "Operation");
    await user.click(within(editor).getByRole("button", { name: /Projects/ }));

    expect(permissionCheckbox(editor, projectView.id)).toBeInTheDocument();
    expect(permissionCheckbox(editor, managementView.id)).toBeEnabled();
    expect(permissionCheckbox(editor, managementAction.id)).toBeEnabled();
    expect(permissionCheckbox(editor, crossCategoryAction.id)).toBeEnabled();
    await user.click(permissionCheckbox(editor, managementView.id));
    await user.click(permissionCheckbox(editor, managementAction.id));
    await user.click(permissionCheckbox(editor, crossCategoryAction.id));
    expect(permissionCheckbox(editor, projectView.id)).toBeChecked();
    expect(permissionCheckbox(editor, managementView.id)).toBeChecked();
    expect(permissionCheckbox(editor, managementAction.id)).toBeChecked();
    expect(permissionCheckbox(editor, crossCategoryAction.id)).toBeChecked();
    await user.type(within(editor).getByLabelText("Role name"), "Cross Category Reviewer");
    await user.click(within(editor).getByRole("button", { name: "Save role" }));
    await waitFor(() => expect(fixtures.saveRole).toHaveBeenCalledWith(expect.objectContaining({
      roleId: null,
      input: expect.objectContaining({
        category: "Operation",
        permissionIds: expect.arrayContaining([projectView.id, managementView.id, managementAction.id, crossCategoryAction.id]),
      }),
    })));
  });

  it("keeps delegable Project Plan actions enabled across Role Categories", async () => {
    const user = userEvent.setup();
    fixtures.actorPermissions = ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions"];
    const planningView = {
      id: "planning-view",
      name: "Planning.View",
      resource: "Planning",
      action: "View",
      moduleId: "project-delivery",
      moduleNameEn: "Project Delivery",
      moduleNameAr: "تسليم المشاريع",
      screenId: "planning",
      screenNameEn: "Planning",
      screenNameAr: "التخطيط",
      scope: "Project" as const,
      level: "View" as const,
      labelEn: "View project plans",
      labelAr: "عرض خطط المشاريع",
      descriptionEn: "View plans in the authorized project scope.",
      descriptionAr: "عرض الخطط ضمن نطاق المشروع المصرح به.",
      canDelegate: true,
      canDelegateToCustomRole: true,
      sensitivity: "Low" as const,
      isSensitive: false,
      isAssignable: true,
      requiredPermissionKeys: [],
      isActive: true,
      allowedCategories: ["Management", "Operation"],
    };
    const planningEdit = {
      ...planningView,
      id: "planning-edit",
      name: "Planning.Edit",
      action: "Edit",
      level: "Action" as const,
      labelEn: "Edit project plans",
      labelAr: "تعديل خطط المشاريع",
      descriptionEn: "Edit plan activities and content in the authorized project scope.",
      descriptionAr: "تعديل أنشطة الخطة ومحتواها ضمن نطاق المشروع المصرح به.",
      canDelegate: true,
      canDelegateToCustomRole: true,
      sensitivity: "High" as const,
      isSensitive: true,
      requiredPermissionKeys: ["Planning.View"],
      allowedCategories: ["Management", "Operation"],
    };
    const projectPlanCreate = {
      ...planningEdit,
      id: "project-plan-create",
      name: "ProjectPlan.Create",
      resource: "ProjectPlan",
      action: "Create",
      labelEn: "Create project plans",
      labelAr: "إنشاء خطط المشاريع",
      descriptionEn: "Create a Project Plan in the authorized project scope.",
      descriptionAr: "إنشاء خطة مشروع ضمن نطاق المشروع المصرح به.",
      canDelegate: true,
      canDelegateToCustomRole: true,
      allowedCategories: ["Management"],
    };
    fixtures.assignablePermissions = [planningView, planningEdit, projectPlanCreate];

    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);
    await user.click(screen.getByRole("button", { name: /New role/ }));
    const editor = await screen.findByRole("dialog", { name: "New role" });
    const category = within(editor).getByRole("combobox", { name: "Category" });
    await user.selectOptions(category, "Management");
    const planningToggle = within(editor).getAllByRole("button", { name: /Planning/ })
      .find((button) => button.getAttribute("aria-expanded") === "false");
    if (!planningToggle) throw new Error("Project Planning permissions section did not render");
    await user.click(planningToggle);

    expect(permissionCheckbox(editor, projectPlanCreate.id)).toBeEnabled();
    expect(permissionCheckbox(editor, planningEdit.id)).toBeEnabled();
    expect(within(editor).getByText("Create project plans")).toBeInTheDocument();
    expect(within(editor).getByText("Edit project plans")).toBeInTheDocument();

    await user.selectOptions(category, "Operation");
    expect(permissionCheckbox(editor, projectPlanCreate.id)).toBeEnabled();
    expect(permissionCheckbox(editor, planningEdit.id)).toBeEnabled();
    expect(within(editor).queryByText(/Unavailable for Operation roles/)).not.toBeInTheDocument();
  });

  it("selects and saves Annual Budget permissions across Role Categories", async () => {
    const user = userEvent.setup();
    fixtures.actorPermissions = ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions", "TenantAdministration.ManageScope"];
    const annualBudgetView = {
      id: "annual-budget-view",
      name: "ProjectAnnualBudget.View",
      resource: "ProjectAnnualBudget",
      action: "View",
      moduleId: "financial-management",
      moduleNameEn: "Financial Management",
      moduleNameAr: "الإدارة المالية",
      screenId: "annual-budgets",
      screenNameEn: "Annual Budgets",
      screenNameAr: "الموازنات السنوية",
      scope: "Project" as const,
      level: "View" as const,
      labelEn: "View Annual Budgets",
      labelAr: "عرض الميزانيات السنوية",
      descriptionEn: "View annual budgets in the authorized Project scope.",
      descriptionAr: "عرض الميزانيات السنوية ضمن نطاق المشروع المصرح به.",
      canDelegate: true,
      canDelegateToCustomRole: true,
      sensitivity: "High" as const,
      isSensitive: true,
      isAssignable: true,
      requiredPermissionKeys: [],
      isActive: true,
      allowedCategories: ["Business", "Management", "Operation"],
    };
    const annualBudgetCreate = {
      ...annualBudgetView,
      id: "annual-budget-create",
      name: "ProjectAnnualBudget.Create",
      action: "Create",
      level: "Action" as const,
      labelEn: "Create Annual Budget",
      labelAr: "إنشاء ميزانية سنوية",
      descriptionEn: "Create an annual-budget draft in the authorized Project scope.",
      descriptionAr: "إنشاء مسودة ميزانية سنوية ضمن نطاق المشروع المصرح به.",
      requiredPermissionKeys: ["ProjectAnnualBudget.View"],
    };
    const annualBudgetApprove = {
      ...annualBudgetCreate,
      id: "annual-budget-approve",
      name: "ProjectAnnualBudget.Approve",
      action: "Approve",
      labelEn: "Review annual budget",
      labelAr: "مراجعة الميزانية السنوية",
      descriptionEn: "Decide an assigned annual-budget review.",
      descriptionAr: "اتخاذ قرار في مراجعة ميزانية سنوية مُسندة.",
      requiredPermissionKeys: [],
      allowedCategories: ["Management"],
    };
    fixtures.assignablePermissions = [annualBudgetView, annualBudgetCreate, annualBudgetApprove];
    fixtures.saveRole.mockResolvedValue({
      id: "created-annual-budget-management",
      name: "Annual Budget Management",
      description: "",
      category: "Management",
      isSystem: false,
      permissionCount: 2,
      assignedUserCount: 0,
      permissions: [annualBudgetView, annualBudgetCreate],
    });

    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);
    await user.click(screen.getByRole("button", { name: "New role" }));
    const editor = await screen.findByRole("dialog", { name: "New role" });
    await user.selectOptions(within(editor).getByRole("combobox", { name: "Category" }), "Management");
    const annualBudgetScreenToggle = within(editor).getAllByRole("button", { name: /Annual Budgets/ })
      .find((button) => button.getAttribute("aria-expanded") === "false");
    if (annualBudgetScreenToggle) await user.click(annualBudgetScreenToggle);

    const viewCheckbox = permissionCheckbox(editor, annualBudgetView.id);
    const createCheckbox = permissionCheckbox(editor, annualBudgetCreate.id);
    const approveCheckbox = permissionCheckbox(editor, annualBudgetApprove.id);
    expect(createCheckbox).toBeEnabled();
    expect(approveCheckbox).toBeEnabled();
    await user.click(createCheckbox);
    expect(viewCheckbox).toBeChecked();
    expect(createCheckbox).toBeChecked();

    await user.selectOptions(within(editor).getByRole("combobox", { name: "Category" }), "Operation");
    expect(createCheckbox).toBeEnabled();
    expect(approveCheckbox).toBeEnabled();
    await user.click(approveCheckbox);
    expect(approveCheckbox).toBeChecked();
    await user.type(within(editor).getByLabelText("Role name"), "Annual Budget Management");
    await user.click(within(editor).getByRole("button", { name: "Save role" }));

    await waitFor(() => expect(fixtures.saveRole).toHaveBeenCalledWith(expect.objectContaining({
      roleId: null,
      input: expect.objectContaining({
        name: "Annual Budget Management",
        category: "Operation",
        permissionIds: expect.arrayContaining([annualBudgetApprove.id, annualBudgetCreate.id, annualBudgetView.id]),
      }),
    })));
    expect(fixtures.saveRole.mock.calls[0][0].input.permissionIds).toHaveLength(3);
    await waitFor(() => expect(fixtures.refreshUser).toHaveBeenCalledTimes(1));
  });

  it("grants delegated Budget Revisions actions to a custom role across categories without actor effective grants", async () => {
    const user = userEvent.setup();
    fixtures.actorPermissions = ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions"];
    const budgetRevisionView = {
      id: "budget-revision-view",
      name: "BudgetRevision.View",
      resource: "BudgetRevision",
      action: "View",
      moduleId: "financial-management",
      moduleNameEn: "Financial Management",
      moduleNameAr: "\u0627\u0644\u0625\u062f\u0627\u0631\u0629 \u0627\u0644\u0645\u0627\u0644\u064a\u0629",
      screenId: "budget-revisions",
      screenNameEn: "Budget Revisions",
      screenNameAr: "\u0645\u0631\u0627\u062c\u0639\u0627\u062a \u0627\u0644\u0645\u064a\u0632\u0627\u0646\u064a\u0629",
      scope: "Project" as const,
      level: "View" as const,
      labelEn: "View",
      labelAr: "\u0639\u0631\u0636",
      descriptionEn: "View budget revisions in the authorized Project scope.",
      descriptionAr: "\u0639\u0631\u0636 \u0645\u0631\u0627\u062c\u0639\u0627\u062a \u0627\u0644\u0645\u064a\u0632\u0627\u0646\u064a\u0629 \u0636\u0645\u0646 \u0646\u0637\u0627\u0642 \u0627\u0644\u0645\u0634\u0631\u0648\u0639 \u0627\u0644\u0645\u0635\u0631\u062d \u0628\u0647.",
      canDelegate: true,
      canDelegateToCustomRole: true,
      sensitivity: "Low" as const,
      isSensitive: false,
      isAssignable: true,
      requiredPermissionKeys: [],
      isActive: true,
      allowedCategories: ["Management"],
    };
    const budgetRevisionApprove = {
      ...budgetRevisionView,
      id: "budget-revision-approve",
      name: "BudgetRevision.Approve",
      action: "Approve",
      level: "Action" as const,
      labelEn: "Approve or reject",
      labelAr: "\u0627\u0639\u062a\u0645\u0627\u062f \u0623\u0648 \u0631\u0641\u0636",
      descriptionEn: "Approve or reject a submitted budget revision.",
      descriptionAr: "\u0627\u0639\u062a\u0645\u0627\u062f \u0645\u0631\u0627\u062c\u0639\u0629 \u0645\u064a\u0632\u0627\u0646\u064a\u0629 \u0645\u0642\u062f\u0645\u0629 \u0623\u0648 \u0631\u0641\u0636\u0647\u0627.",
      sensitivity: "High" as const,
      isSensitive: true,
      requiredPermissionKeys: ["BudgetRevision.View"],
    };
    const budgetRevisionCreate = {
      ...budgetRevisionView,
      id: "budget-revision-create",
      name: "BudgetRevision.Create",
      action: "Create",
      level: "Action" as const,
      labelEn: "Create",
      labelAr: "\u0625\u0646\u0634\u0627\u0621",
      descriptionEn: "Create a budget revision draft.",
      descriptionAr: "\u0625\u0646\u0634\u0627\u0621 \u0645\u0633\u0648\u062f\u0629 \u0645\u0631\u0627\u062c\u0639\u0629 \u0645\u064a\u0632\u0627\u0646\u064a\u0629.",
      sensitivity: "High" as const,
      isSensitive: true,
      requiredPermissionKeys: ["BudgetRevision.View"],
    };
    const budgetRevisionEditDraft = {
      ...budgetRevisionCreate,
      id: "budget-revision-edit-draft",
      name: "BudgetRevision.EditDraft",
      action: "EditDraft",
      labelEn: "Edit draft",
      labelAr: "\u062a\u0639\u062f\u064a\u0644 \u0627\u0644\u0645\u0633\u0648\u062f\u0629",
      descriptionEn: "Edit a draft budget revision.",
      descriptionAr: "\u062a\u0639\u062f\u064a\u0644 \u0645\u0633\u0648\u062f\u0629 \u0645\u0631\u0627\u062c\u0639\u0629 \u0627\u0644\u0645\u064a\u0632\u0627\u0646\u064a\u0629.",
    };
    const budgetRevisionPermissions = [budgetRevisionView, budgetRevisionApprove, budgetRevisionCreate, budgetRevisionEditDraft];
    fixtures.assignablePermissions = budgetRevisionPermissions;
    fixtures.saveRole.mockResolvedValue({
      id: "created-budget-revisions-operation",
      name: "Budget Revisions Operator",
      description: "",
      category: "Operation",
      isSystem: false,
      permissionCount: budgetRevisionPermissions.length,
      assignedUserCount: 0,
      permissions: budgetRevisionPermissions,
    });

    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);
    await user.click(screen.getByRole("button", { name: "New role" }));
    const editor = await screen.findByRole("dialog", { name: "New role" });
    await user.selectOptions(within(editor).getByRole("combobox", { name: "Category" }), "Operation");
    const budgetRevisionsToggle = within(editor).getAllByRole("button", { name: /Budget Revisions/ })
      .find((button) => button.getAttribute("aria-expanded") === "false");
    if (budgetRevisionsToggle) await user.click(budgetRevisionsToggle);

    for (const permission of budgetRevisionPermissions) {
      expect(fixtures.actorPermissions).not.toContain(permission.name);
      expect(permissionCheckbox(editor, permission.id)).toBeEnabled();
    }
    await user.click(permissionCheckbox(editor, budgetRevisionView.id));
    await user.click(permissionCheckbox(editor, budgetRevisionApprove.id));
    await user.click(permissionCheckbox(editor, budgetRevisionCreate.id));
    await user.click(permissionCheckbox(editor, budgetRevisionEditDraft.id));
    for (const permission of budgetRevisionPermissions) {
      expect(permissionCheckbox(editor, permission.id)).toBeChecked();
    }

    await user.type(within(editor).getByLabelText("Role name"), "Budget Revisions Operator");
    await user.click(within(editor).getByRole("button", { name: "Save role" }));
    await waitFor(() => expect(fixtures.saveRole).toHaveBeenCalledWith(expect.objectContaining({
      roleId: null,
      input: expect.objectContaining({
        name: "Budget Revisions Operator",
        category: "Operation",
        permissionIds: expect.arrayContaining(budgetRevisionPermissions.map((permission) => permission.id)),
      }),
    })));
    expect(fixtures.saveRole.mock.calls[0][0].input.permissionIds).toHaveLength(budgetRevisionPermissions.length);
    await waitFor(() => expect(fixtures.refreshUser).toHaveBeenCalledTimes(1));
  });
  it("keeps canonical Organization Admin read-only and unassignable", async () => {
    fixtures.actorPermissions = ["Roles.View", "Roles.ConfigurePermissions"];
    const organizationAdmin = {
      id: "organization-admin-role",
      name: "Organization Admin",
      roleKey: "organization.admin",
      description: "Organization-scoped configuration administrator",
      category: "Organization",
      isSystem: true,
      isProtected: false,
      isAssignable: false,
      canEditDetails: false,
      canManagePermissions: false,
      canDelete: false,
      permissionCount: 2,
      assignedUserCount: 0,
      permissions: [],
    };
    fixtures.roles = [organizationAdmin];
    fixtures.selectedRole = organizationAdmin;

    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);
    await userEvent.setup().click(screen.getByRole("button", { name: /Organization Admin/ }));

    expect(screen.getByText("System role")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Edit" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Delete role" })).toBeDisabled();
    expect(screen.queryByRole("dialog", { name: "Edit role" })).not.toBeInTheDocument();
    expect(fixtures.saveRole).not.toHaveBeenCalled();
  });
  it("explains when an action is disabled because its required permission cannot be granted", async () => {
    const user = userEvent.setup();
    fixtures.actorPermissions = ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions"];
    fixtures.assignablePermissions = [{
      id: "planning-view-for-create",
      name: "Planning.View",
      resource: "Planning",
      action: "View",
      moduleId: "project-delivery",
      moduleNameEn: "Project Delivery",
      moduleNameAr: "تسليم المشاريع",
      screenId: "planning",
      screenNameEn: "Planning",
      screenNameAr: "التخطيط",
      scope: "Project",
      level: "View",
      labelEn: "View project plans",
      labelAr: "عرض خطط المشاريع",
      descriptionEn: "View plans in the authorized project scope.",
      descriptionAr: "عرض الخطط ضمن نطاق المشروع المصرح به.",
      canDelegate: false,
      sensitivity: "Low",
      isSensitive: false,
      isAssignable: true,
      requiredPermissionKeys: [],
      isActive: true,
      allowedCategories: ["Management"],
    }, {
      id: "project-plan-create-not-delegable",
      name: "ProjectPlan.Create",
      resource: "ProjectPlan",
      action: "Create",
      moduleId: "project-delivery",
      moduleNameEn: "Project Delivery",
      moduleNameAr: "تسليم المشاريع",
      screenId: "planning",
      screenNameEn: "Planning",
      screenNameAr: "التخطيط",
      scope: "Project",
      level: "Action",
      labelEn: "Create project plans",
      labelAr: "إنشاء خطط المشاريع",
      descriptionEn: "Create a Project Plan in the authorized project scope.",
      descriptionAr: "إنشاء خطة مشروع ضمن نطاق المشروع المصرح به.",
      canDelegate: true,
      canDelegateToCustomRole: true,
      sensitivity: "High",
      isSensitive: true,
      isAssignable: true,
      requiredPermissionKeys: ["Planning.View"],
      isActive: true,
      allowedCategories: ["Management"],
    }];

    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);
    await user.click(screen.getByRole("button", { name: /New role/ }));
    const editor = await screen.findByRole("dialog", { name: "New role" });
    await user.selectOptions(within(editor).getByRole("combobox", { name: "Category" }), "Management");
    await user.click(within(editor).getByRole("button", { name: /Planning/ }));

    expect(permissionCheckbox(editor, "project-plan-create-not-delegable")).toBeDisabled();
    expect(within(editor).getByText("A required permission cannot be granted with your current access, so this action is unavailable.")).toBeInTheDocument();
  });

  it("keeps permissions editable after a Role Category change in Arabic", async () => {
    const user = userEvent.setup();
    window.localStorage.setItem("nexus-locale", "ar");
    fixtures.actorPermissions = ["Roles.View", "Roles.EditDetails", "Roles.ConfigurePermissions"];
    const permission = {
      id: "category-existing-approval",
      name: "Project.Approve",
      resource: "Project",
      action: "Approve",
      moduleId: "project-management",
      moduleNameEn: "Project Management",
      moduleNameAr: "إدارة المشاريع",
      screenId: "projects",
      screenNameEn: "Projects",
      screenNameAr: "المشاريع",
      scope: "Project" as const,
      level: "Action" as const,
      labelEn: "Approve project changes",
      labelAr: "اعتماد تغييرات المشروع",
      descriptionEn: "Approve project changes.",
      descriptionAr: "اعتماد تغييرات المشروع.",
      canDelegate: true,
      canDelegateToCustomRole: true,
      canDelegateToOrdinaryRole: true,
      sensitivity: "Medium" as const,
      isSensitive: false,
      isAssignable: true,
      requiredPermissionKeys: [],
      isActive: true,
      allowedCategories: ["Management"],
    };
    const role = {
      id: "category-role-management",
      name: "Project Manager",
      description: "Project manager role",
      category: "Management",
      roleKey: "tenant.project-manager",
      isSystem: false,
      isProtected: false,
      isAssignable: true,
      canEditDetails: false,
      canManagePermissions: true,
      canDelete: false,
      permissionCount: 1,
      assignedUserCount: 0,
      permissions: [permission],
    };
    fixtures.roles = [role];
    fixtures.selectedRole = role;
    fixtures.assignablePermissions = [permission];

    render(<I18nProvider><MemoryRouter><RolesPage /></MemoryRouter></I18nProvider>);
    await user.click(screen.getByRole("button", { name: /Project Manager/ }));
    expect(screen.queryByText("System role")).not.toBeInTheDocument();
    expect(screen.getByText("Assignable role")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "تعديل" }));
    const editor = await screen.findByRole("dialog", { name: "تعديل الدور" });
    await user.selectOptions(within(editor).getByRole("combobox", { name: "الفئة" }), "Operation");
    const projectsScreenToggle = within(editor).getAllByRole("button", { name: new RegExp(permission.screenNameAr) })
      .find((button) => button.getAttribute("aria-expanded") === "false");
    if (!projectsScreenToggle) throw new Error("Project permissions section did not render");
    await user.click(projectsScreenToggle);

    const approval = permissionCheckbox(editor, permission.id);
    expect(approval).toBeChecked();
    expect(approval).toBeEnabled();
    expect(within(editor).queryByText(/غير متاحة لأدوار فئة/)).not.toBeInTheDocument();
    await user.click(approval);
    expect(approval).not.toBeChecked();
  });
  it("sets RTL document direction for the Roles & Permissions page in Arabic", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    render(<I18nProvider><MemoryRouter><RolesPage /></MemoryRouter></I18nProvider>);

    await waitFor(() => expect(document.documentElement.dir).toBe("rtl"));
    expect(screen.getAllByRole("heading").length).toBeGreaterThan(0);
  });

  it("explains why Financials Read is missing from the assignable role catalog", () => {
    fixtures.actorPermissions = ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions"];
    fixtures.assignablePermissions = [];
    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: /New role/ }));
    expect(screen.getByText("If View Project Financial Foundation is missing, your account cannot currently delegate it. Ask a System Administrator who already has it to grant it to your account first.")).toBeInTheDocument();
  });
});
