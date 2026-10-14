import * as React from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { AccessPage, AccessUsersPage, UserPermissionsPage } from "../features/administration/AccessPages";
import { AdministrationLayout } from "../features/administration/AdministrationHub";
import { RolesPage } from "../features/administration/RolesPage";

const fixtures = vi.hoisted(() => ({
  users: [{ id: "user-1", email: "maya@example.com", firstName: "Maya", lastName: "Admin", fullName: "Maya Admin", isActive: true, authenticationSource: "Local", mustChangePassword: false, roles: ["Administrator"] }],
  permissions: [
    { id: "permission-1", name: "projects.read", resource: "projects", action: "read", isRoleGranted: true, overrideEffect: null, isEffective: true },
    { id: "permission-2", name: "users.write", resource: "users", action: "write", isRoleGranted: false, overrideEffect: null, isEffective: false },
    { id: "permission-finances-read", name: "finances.read", resource: "finances", action: "read", isRoleGranted: false, overrideEffect: null, isEffective: false },
  ] as Array<{ id: string; name: string; resource: string; action: string; isRoleGranted: boolean; overrideEffect: "Allow" | "Deny" | null; isEffective: boolean }>,
  actorPermissions: ["users.read", "users.write", "users.reset-password", "projects.read"] as string[],
  assignablePermissions: [] as Array<{ id: string; name: string; resource: string; action: string; description?: string }>,
  roles: [] as Array<{ id: string; name: string; description?: string; category?: string; isSystem: boolean; permissionCount: number; assignedUserCount: number }>,
  assignableRoles: [] as Array<{ id: string; name: string; description?: string; category?: string; isSystem: boolean; permissionCount: number; assignedUserCount: number }>,
  selectedRole: null as null | { id: string; name: string; description: string; category: string; isSystem: boolean; permissionCount: number; assignedUserCount: number; permissions: Array<{ id: string; name: string; resource: string; action: string }> },
  savePermissions: vi.fn(),
  createUser: vi.fn(),
  resetPassword: vi.fn(),
  updateUser: vi.fn(),
  saveRole: vi.fn(),
  refetch: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "actor-1", permissions: fixtures.actorPermissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ effectiveReadOnly: false, canMutate: true }) }));
vi.mock("../features/administration/api/admin", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../features/administration/api/admin")>();
  return {
    ...actual,
    useAdminUsers: () => ({ data: { items: fixtures.users, page: 1, pageSize: 25, totalCount: fixtures.users.length, totalPages: 1 }, isLoading: false, isError: false }),
    useAssignableRoles: () => ({ data: fixtures.assignableRoles, isLoading: false, isError: false, refetch: fixtures.refetch }),
    useCreateAdminUser: () => ({ mutateAsync: fixtures.createUser, isPending: false }),
    useResetAdminUserPassword: () => ({ mutateAsync: fixtures.resetPassword, isPending: false }),
    useUpdateAdminUser: () => ({ mutateAsync: fixtures.updateUser, isPending: false }),
    useUserPermissions: () => ({ data: fixtures.permissions, isLoading: false, isError: false, refetch: fixtures.refetch }),
    useSaveUserPermissions: () => ({ mutateAsync: fixtures.savePermissions, isPending: false }),
    useManagedRoles: () => ({ data: fixtures.roles, isLoading: false, isError: false, refetch: fixtures.refetch }),
    useAssignablePermissions: () => ({ data: fixtures.assignablePermissions, isLoading: false, isError: false, refetch: fixtures.refetch }),
    useManagedRole: () => ({ data: fixtures.selectedRole, isLoading: false, isFetching: false, isError: false, refetch: fixtures.refetch }),
    useSaveManagedRole: () => ({ mutateAsync: fixtures.saveRole, isPending: false }),
    useDeleteManagedRole: () => ({ mutateAsync: vi.fn(), isPending: false }),
  };
});

function renderWithI18n(element: React.ReactNode) {
  window.localStorage.setItem("nexus-locale", "en");
  return render(<I18nProvider>{element}</I18nProvider>);
}

describe("Access & Security component pages", () => {
  beforeEach(() => {
    window.localStorage.clear();
    fixtures.users = [{ id: "user-1", email: "maya@example.com", firstName: "Maya", lastName: "Admin", fullName: "Maya Admin", isActive: true, authenticationSource: "Local", mustChangePassword: false, roles: ["Administrator"] }];
    fixtures.permissions = [
      { id: "permission-1", name: "projects.read", resource: "projects", action: "read", isRoleGranted: true, overrideEffect: null, isEffective: true },
      { id: "permission-2", name: "users.write", resource: "users", action: "write", isRoleGranted: false, overrideEffect: null, isEffective: false },
      { id: "permission-finances-read", name: "finances.read", resource: "finances", action: "read", isRoleGranted: false, overrideEffect: null, isEffective: false },
    ];
    fixtures.roles = [];
    fixtures.assignableRoles = [{ id: "role-1", name: "Administrator", isSystem: true, permissionCount: 2, assignedUserCount: 1 }, { id: "role-2", name: "Project Manager", isSystem: false, permissionCount: 1, assignedUserCount: 0 }];
    fixtures.selectedRole = null;
    fixtures.actorPermissions = ["users.read", "users.write", "users.reset-password", "projects.read"];
    fixtures.assignablePermissions = [];
    fixtures.savePermissions.mockReset();
    fixtures.createUser.mockReset();
    fixtures.resetPassword.mockReset();
    fixtures.updateUser.mockReset();
    fixtures.saveRole.mockReset();
  });

  it("shows Access & Security components instead of repeating Administration categories", () => {
    renderWithI18n(<MemoryRouter><AccessPage /></MemoryRouter>);

    expect(screen.getByRole("heading", { name: "Access & Security" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Users/ })).toHaveAttribute("href", "/administration/access/users");
    expect(screen.getByRole("link", { name: /Roles & Permissions/ })).toHaveAttribute("href", "/administration/access/roles");
    expect(screen.queryByText("Organization")).not.toBeInTheDocument();
    expect(screen.queryByText("Reference Data")).not.toBeInTheDocument();
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
    const override = screen.getByLabelText("Direct override for Projects · Read");
    expect(roleGranted).toHaveClass("text-info");
    expect(roleGranted.className).not.toMatch(/bg-(success|warning|destructive|info)/);
    expect(effective).toHaveClass("text-success");
    expect(effective.className).not.toMatch(/bg-(success|warning|destructive|info)/);
    expect(screen.getByLabelText("Direct override for Finances · Read")).toBeDisabled();
    expect(screen.getByText("Your account does not have this permission to change.")).toBeInTheDocument();

    fireEvent.change(override, { target: { value: "Deny" } });
    expect(override).toHaveClass("text-destructive");
    fireEvent.change(override, { target: { value: "Allow" } });
    expect(override).toHaveClass("text-success");
    fireEvent.change(override, { target: { value: "Inherit" } });
    expect(override).toHaveClass("text-muted-foreground");
  });

  it("explains in Arabic why financial read cannot be delegated from the current account", () => {
    window.localStorage.setItem("nexus-locale", "ar");
    render(<I18nProvider><MemoryRouter initialEntries={["/administration/access/users"]}>
      <Routes>
        <Route path="/administration/access/users" element={<AccessUsersPage />} />
        <Route path="/administration/access/users/:userId/permissions" element={<UserPermissionsPage />} />
      </Routes>
    </MemoryRouter></I18nProvider>);

    fireEvent.click(screen.getByRole("button", { name: "إدارة الصلاحيات" }));
    expect(screen.getByLabelText("التجاوز المباشر للصلاحية الشؤون المالية · قراءة")).toBeDisabled();
    expect(screen.getByText("حسابك لا يملك هذه الصلاحية لتعديلها.")).toBeInTheDocument();
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
    fixtures.createUser.mockResolvedValue({ user: { ...fixtures.users[0], id: "user-2", email: "sam@example.com", fullName: "Sam Taylor", roles: ["Project Manager"] }, temporaryPassword: "secret-temp-pass" });
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
      jobTitle: null,
      phone: null,
      isActive: true,
      roles: ["Project Manager"],
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
    fixtures.updateUser.mockResolvedValue({ ...fixtures.users[0], roles: ["Administrator", "Project Manager"] });
    renderWithI18n(<MemoryRouter><AccessUsersPage /></MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: "Edit Maya Admin" }));
    const sheet = screen.getByRole("dialog", { name: "Edit User" });
    expect(within(sheet).getByLabelText("First Name")).toHaveValue("Maya");
    fireEvent.change(within(sheet).getByLabelText("Job Title"), { target: { value: "Head of Finance" } });
    fireEvent.click(within(sheet).getByRole("checkbox", { name: "Project Manager" }));
    fireEvent.click(within(sheet).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(fixtures.updateUser).toHaveBeenCalledWith({
      userId: "user-1",
      input: {
        email: "maya@example.com",
        firstName: "Maya",
        lastName: "Admin",
        jobTitle: "Head of Finance",
        phone: null,
        isActive: true,
        roles: ["Administrator", "Project Manager"],
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
    fixtures.users = [{ ...fixtures.users[0], roles: ["Elevated Admin"] }];
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
    fixtures.roles = [
      { id: "role-1", name: "Analyst", isSystem: false, permissionCount: 1, assignedUserCount: 0 },
      { id: "role-2", name: "System Admin", isSystem: true, permissionCount: 4, assignedUserCount: 1 },
    ];
    fixtures.selectedRole = { ...fixtures.roles[0], description: "Read-only analyst", category: "Delivery", permissions: [{ id: "permission-1", name: "projects.read", resource: "projects", action: "read" }] };
    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);

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

  it("explains why Financials Read is missing from the assignable role catalog", () => {
    fixtures.assignablePermissions = [];
    renderWithI18n(<MemoryRouter><RolesPage /></MemoryRouter>);

    fireEvent.click(screen.getByRole("button", { name: /New role/ }));
    expect(screen.getByText("If Finances · Read is missing, your account cannot currently delegate it. Ask a System Administrator who already has it to grant it to your account first.")).toBeInTheDocument();
  });
});
