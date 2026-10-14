import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { VendorCategoriesPage } from "../features/administration/VendorCategoriesPage";
import { ProjectRolesPage } from "../features/administration/ProjectRolesPage";
import { WorkItemStatusesPage } from "../features/administration/WorkItemStatusesPage";
import { ReferenceDataPage } from "../features/administration/AdminPages";

const mocks = vi.hoisted(() => ({
  useAuth: vi.fn(),
  categories: vi.fn(),
  createHook: vi.fn(),
  updateHook: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  refetch: vi.fn(),
  projectRoles: vi.fn(),
  projectRoleNextCode: vi.fn(),
  createProjectRole: vi.fn(),
  updateProjectRole: vi.fn(),
  deactivateProjectRole: vi.fn(),
  createProjectRoleHook: vi.fn(),
  updateProjectRoleHook: vi.fn(),
  deactivateProjectRoleHook: vi.fn(),
  statuses: vi.fn(),
  createStatus: vi.fn(),
  updateStatus: vi.fn(),
  createStatusHook: vi.fn(),
  updateStatusHook: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: mocks.useAuth }));
vi.mock("../features/administration/api/admin", () => ({
  useVendorCategories: mocks.categories,
  useCreateVendorCategory: mocks.createHook,
  useUpdateVendorCategory: mocks.updateHook,
  useWorkItemStatuses: mocks.statuses,
  useCreateWorkItemStatus: mocks.createStatusHook,
  useUpdateWorkItemStatus: mocks.updateStatusHook,
}));
vi.mock("../features/resources/api/resources", () => ({
  useProjectRoles: mocks.projectRoles,
  useProjectRoleNextCode: mocks.projectRoleNextCode,
  useCreateProjectRole: mocks.createProjectRoleHook,
  useUpdateProjectRole: mocks.updateProjectRoleHook,
  useDeactivateProjectRole: mocks.deactivateProjectRoleHook,
}));

const initialCategories = [
  { id: "cat-2", code: "CONS", nameEn: "Consulting", nameAr: "استشارات", description: "Specialist work", color: "blue", displayOrder: 20, isActive: false },
  { id: "cat-1", code: "TECH", nameEn: "Technology", nameAr: "التقنية", description: "Technical suppliers", color: "green", displayOrder: 10, isActive: true },
];
const initialProjectRoles = [
  { id: "role-2", code: "ANALYST", nameEn: "Business Analyst", nameAr: "محلل أعمال", description: "Analysis", displayOrder: 20, isActive: false },
  { id: "role-1", code: "PM", nameEn: "Project Manager", nameAr: "مدير مشروع", description: "Delivery lead", displayOrder: 10, isActive: true },
];
const initialStatuses = [
  { id: "status-2", code: "BLOCKED", nameEn: "Blocked", nameAr: "متوقف", displayOrder: 20, isActive: false, color: "rose", isDefault: false },
  { id: "status-1", code: "OPEN", nameEn: "Open", nameAr: "مفتوح", displayOrder: 10, isActive: true, color: "blue", isDefault: true },
  { id: "status-3", code: "REVIEW", nameEn: "Review", nameAr: "قيد المراجعة", displayOrder: 30, isActive: true, color: "amber", isDefault: false },
];

function renderPage(path = "/administration/reference-data/vendor-categories") {
  window.localStorage.setItem("nexus-locale", "en");
  return render(<I18nProvider><MemoryRouter initialEntries={[path]}><VendorCategoriesPage /></MemoryRouter></I18nProvider>);
}

function renderProjectRolesPage(permissions = ["settings.read", "Resource.Manage"]) {
  window.localStorage.setItem("nexus-locale", "en");
  mocks.useAuth.mockReturnValue({ user: { id: "admin-1", permissions }, status: "authenticated" });
  return render(<I18nProvider><MemoryRouter initialEntries={["/administration/reference-data/project-roles"]}><ProjectRolesPage /></MemoryRouter></I18nProvider>);
}

function renderWorkItemStatusesPage(permissions = ["settings.read", "settings.write"]) {
  window.localStorage.setItem("nexus-locale", "en");
  mocks.useAuth.mockReturnValue({ user: { id: "admin-1", permissions }, status: "authenticated" });
  return render(<I18nProvider><MemoryRouter initialEntries={["/administration/reference-data/work-item-statuses"]}><WorkItemStatusesPage /></MemoryRouter></I18nProvider>);
}

describe("Vendor Categories source parity", () => {
  beforeEach(() => {
    mocks.useAuth.mockReset().mockReturnValue({ user: { id: "admin-1", permissions: ["settings.read", "settings.write"] }, status: "authenticated" });
    mocks.categories.mockReset().mockReturnValue({ data: initialCategories, isLoading: false, isError: false, refetch: mocks.refetch });
    mocks.create.mockReset().mockResolvedValue(undefined);
    mocks.update.mockReset().mockResolvedValue(undefined);
    mocks.refetch.mockReset();
    mocks.createHook.mockReturnValue({ mutateAsync: mocks.create, isPending: false });
    mocks.updateHook.mockReturnValue({ mutateAsync: mocks.update, isPending: false });
    mocks.projectRoles.mockReturnValue({ data: initialProjectRoles, isLoading: false, isError: false, refetch: mocks.refetch });
    mocks.projectRoleNextCode.mockImplementation((enabled: boolean) => enabled
      ? { data: { code: "PROJROLE-000001" }, isLoading: false }
      : { data: undefined, isLoading: false });
    mocks.createProjectRole.mockReset().mockResolvedValue(undefined);
    mocks.updateProjectRole.mockReset().mockResolvedValue(undefined);
    mocks.deactivateProjectRole.mockReset().mockResolvedValue(undefined);
    mocks.createProjectRoleHook.mockReturnValue({ mutateAsync: mocks.createProjectRole, isPending: false });
    mocks.updateProjectRoleHook.mockReturnValue({ mutateAsync: mocks.updateProjectRole, isPending: false });
    mocks.deactivateProjectRoleHook.mockReturnValue({ mutateAsync: mocks.deactivateProjectRole, isPending: false });
    mocks.statuses.mockReturnValue({ data: initialStatuses, isLoading: false, isError: false, refetch: mocks.refetch });
    mocks.createStatus.mockReset().mockResolvedValue(undefined);
    mocks.updateStatus.mockReset().mockResolvedValue(undefined);
    mocks.createStatusHook.mockReturnValue({ mutateAsync: mocks.createStatus, isPending: false });
    mocks.updateStatusHook.mockReturnValue({ mutateAsync: mocks.updateStatus, isPending: false });
    vi.stubGlobal("confirm", vi.fn().mockReturnValue(true));
  });

  it("presents the Reference Data source hub with links to all four canonical sections", () => {
    render(<I18nProvider><MemoryRouter initialEntries={["/administration/reference-data"]}><ReferenceDataPage /></MemoryRouter></I18nProvider>);

    expect(screen.getByRole("link", { name: /Project Roles/ })).toHaveAttribute("href", "/administration/reference-data/project-roles");
    expect(screen.getByRole("link", { name: /Vendor Categories/ })).toHaveAttribute("href", "/administration/reference-data/vendor-categories");
    expect(screen.getByRole("link", { name: /Work Item Statuses/ })).toHaveAttribute("href", "/administration/reference-data/work-item-statuses");
    expect(screen.getByRole("link", { name: /Currencies/ })).toHaveAttribute("href", "/administration/currencies");
  });

  it("sorts by display order, uses semantic text-only status colors, and hides management actions without Settings.Write", () => {
    mocks.useAuth.mockReturnValue({ user: { id: "reader-1", permissions: ["settings.read"] }, status: "authenticated" });
    renderPage();

    const rows = screen.getAllByRole("row");
    expect(within(rows[1]).getByText("TECH")).toBeInTheDocument();
    expect(within(rows[1]).getByText("Active")).toHaveClass("text-success");
    expect(within(rows[2]).getByText("CONS")).toBeInTheDocument();
    expect(within(rows[2]).getByText("Inactive")).toHaveClass("text-muted-foreground");
    expect(screen.queryByRole("button", { name: "Add category" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit category" })).not.toBeInTheDocument();
    expect(screen.queryByRole("tablist")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Work Item Statuses/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Currencies/ })).not.toBeInTheDocument();
  });

  it("validates required fields and creates a normalized category in the side sheet", async () => {
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Add category" }));
    const sheet = screen.getByRole("dialog", { name: "Add category" });
    fireEvent.change(within(sheet).getByLabelText("Name (English)"), { target: { value: " Technology Services " } });
    const form = sheet.querySelector("form");
    if (!form) throw new Error("Vendor category form was not rendered");
    fireEvent.submit(form);
    expect(await screen.findByText("Code is required.")).toBeInTheDocument();
    expect(mocks.create).not.toHaveBeenCalled();

    fireEvent.change(within(screen.getByRole("dialog", { name: "Add category" })).getByLabelText("Code"), { target: { value: "  TECH-SVC " } });
    fireEvent.change(within(screen.getByRole("dialog", { name: "Add category" })).getByLabelText("Name (Arabic)"), { target: { value: "خدمات تقنية" } });
    fireEvent.change(within(screen.getByRole("dialog", { name: "Add category" })).getByLabelText("Description"), { target: { value: "  External specialists  " } });
    fireEvent.click(within(screen.getByRole("dialog", { name: "Add category" })).getByRole("button", { name: "Save category" }));

    await waitFor(() => expect(mocks.create).toHaveBeenCalledWith({ code: "TECH-SVC", nameEn: "Technology Services", nameAr: "خدمات تقنية", description: "External specialists", color: undefined, displayOrder: 30, isActive: true }));
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Add category" })).not.toBeInTheDocument());
  });

  it("maps duplicate create errors to localized feedback without closing the sheet", async () => {
    mocks.create.mockRejectedValue(new Error("A category with this code already exists"));
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Add category" }));
    const sheet = screen.getByRole("dialog", { name: "Add category" });
    fireEvent.change(within(sheet).getByLabelText("Code"), { target: { value: "TECH" } });
    fireEvent.change(within(sheet).getByLabelText("Name (English)"), { target: { value: "Technology" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save category" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("A vendor category with this code already exists.");
    expect(screen.getByRole("dialog", { name: "Add category" })).toBeInTheDocument();
  });

  it("keeps category code and metadata on edit, and swaps display order through the API", async () => {
    renderPage();
    const rows = screen.getAllByRole("row");
    fireEvent.click(within(rows[1]).getByRole("button", { name: "Edit category" }));
    const sheet = screen.getByRole("dialog", { name: "Edit category" });
    expect(within(sheet).getByLabelText("Code")).toBeDisabled();
    fireEvent.change(within(sheet).getByLabelText("Name (English)"), { target: { value: "Technology Partners" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save category" }));
    await waitFor(() => expect(mocks.update).toHaveBeenCalledWith({ id: "cat-1", input: { code: "TECH", nameEn: "Technology Partners", nameAr: "التقنية", description: "Technical suppliers", color: "green", displayOrder: 10, isActive: true } }));

    mocks.update.mockClear();
    fireEvent.click(within(screen.getAllByRole("row")[1]).getByRole("button", { name: "Move down" }));
    await waitFor(() => expect(mocks.update).toHaveBeenCalledTimes(2));
    expect(mocks.update).toHaveBeenNthCalledWith(1, { id: "cat-1", input: expect.objectContaining({ displayOrder: 20, color: "green" }) });
    expect(mocks.update).toHaveBeenNthCalledWith(2, { id: "cat-2", input: expect.objectContaining({ displayOrder: 10, color: "blue" }) });
  });

  it("allows activation without confirmation and can retry a failed category load", async () => {
    renderPage();
    fireEvent.click(within(screen.getAllByRole("row")[2]).getByRole("button", { name: "Activate" }));
    await waitFor(() => expect(mocks.update).toHaveBeenCalledWith({ id: "cat-2", input: expect.objectContaining({ isActive: true }) }));

    mocks.categories.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch: mocks.refetch });
    renderPage();
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(mocks.refetch).toHaveBeenCalledOnce();
  });

  it("lists project roles in order with text-only status and gates management by Resource.Manage", () => {
    renderProjectRolesPage(["settings.read"]);

    const rows = screen.getAllByRole("row");
    expect(within(rows[1]).getByText("PM")).toBeInTheDocument();
    expect(within(rows[1]).getByText("Active")).toHaveClass("text-success");
    expect(within(rows[2]).getByText("ANALYST")).toBeInTheDocument();
    expect(within(rows[2]).getByText("Inactive")).toHaveClass("text-muted-foreground");
    expect(screen.queryByRole("button", { name: "Add Role" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
  });

  it("creates a role in a side sheet and leaves untouched generated codes to the server", async () => {
    renderProjectRolesPage();
    fireEvent.click(screen.getByRole("button", { name: "Add Role" }));
    const sheet = screen.getByRole("dialog", { name: "Create Role" });
    await waitFor(() => expect(within(sheet).getByLabelText("Code")).toHaveValue("PROJROLE-000001"));
    fireEvent.change(within(sheet).getByLabelText("Name (EN)"), { target: { value: "  Delivery Lead  " } });
    fireEvent.change(within(sheet).getByLabelText("Name (AR)"), { target: { value: "قائد التسليم" } });
    fireEvent.change(within(sheet).getByLabelText("Description"), { target: { value: "  Leads delivery  " } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(mocks.createProjectRole).toHaveBeenCalledWith({ nameEn: "Delivery Lead", nameAr: "قائد التسليم", description: "Leads delivery", displayOrder: 30, isActive: true }));
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Create Role" })).not.toBeInTheDocument());
  });

  it("sends a manually edited role code and localizes duplicate-code errors", async () => {
    mocks.createProjectRole.mockRejectedValue(new Error("A role with this code already exists"));
    renderProjectRolesPage();
    fireEvent.click(screen.getByRole("button", { name: "Add Role" }));
    const sheet = screen.getByRole("dialog", { name: "Create Role" });
    fireEvent.change(within(sheet).getByLabelText("Code"), { target: { value: "CUSTOM-ROLE" } });
    fireEvent.change(within(sheet).getByLabelText("Name (EN)"), { target: { value: "Custom Role" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(mocks.createProjectRole).toHaveBeenCalledWith(expect.objectContaining({ code: "CUSTOM-ROLE" })));
    expect(await screen.findByRole("alert")).toHaveTextContent("A role with this code already exists");
    expect(screen.getByRole("dialog", { name: "Create Role" })).toBeInTheDocument();
  });

  it("edits, reactivates, deactivates, and swaps order through the project-role API", async () => {
    renderProjectRolesPage();
    const rows = screen.getAllByRole("row");
    fireEvent.click(within(rows[1]).getByRole("button", { name: "Edit" }));
    const sheet = screen.getByRole("dialog", { name: "Edit Role" });
    fireEvent.change(within(sheet).getByLabelText("Name (EN)"), { target: { value: "Senior Project Manager" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save" }));
    await waitFor(() => expect(mocks.updateProjectRole).toHaveBeenCalledWith({ id: "role-1", body: expect.objectContaining({ nameEn: "Senior Project Manager", code: "PM" }) }));

    mocks.updateProjectRole.mockClear();
    fireEvent.click(within(screen.getAllByRole("row")[2]).getByRole("button", { name: "Activate" }));
    await waitFor(() => expect(mocks.updateProjectRole).toHaveBeenCalledWith({ id: "role-2", body: expect.objectContaining({ isActive: true }) }));

    fireEvent.click(within(screen.getAllByRole("row")[1]).getByRole("button", { name: "Deactivate" }));
    await waitFor(() => expect(mocks.deactivateProjectRole).toHaveBeenCalledWith("role-1"));

    mocks.updateProjectRole.mockClear();
    fireEvent.click(within(screen.getAllByRole("row")[1]).getByRole("button", { name: "Move down" }));
    await waitFor(() => expect(mocks.updateProjectRole).toHaveBeenCalledTimes(2));
    expect(mocks.updateProjectRole).toHaveBeenNthCalledWith(1, { id: "role-1", body: expect.objectContaining({ displayOrder: 20 }) });
    expect(mocks.updateProjectRole).toHaveBeenNthCalledWith(2, { id: "role-2", body: expect.objectContaining({ displayOrder: 10 }) });
  });

  it("shows status summaries, filters by status and search, and respects Settings.Write", () => {
    renderWorkItemStatusesPage(["settings.read"]);
    expect(screen.getByText("Total statuses")).toBeInTheDocument();
    expect(screen.getByText("Default status")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add status" })).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Actions" })).not.toBeInTheDocument();

    fireEvent.change(screen.getByRole("textbox", { name: "Search by name or code..." }), { target: { value: "BLOCKED" } });
    expect(screen.getByText("BLOCKED")).toBeInTheDocument();
    expect(screen.queryByText("OPEN")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Clear search" }));
    fireEvent.click(screen.getByRole("button", { name: "Inactive" }));
    expect(screen.getByText("BLOCKED")).toBeInTheDocument();
    expect(screen.queryByText("OPEN")).not.toBeInTheDocument();
  });

  it("creates a bilingual status with color/default settings in a side sheet", async () => {
    renderWorkItemStatusesPage();
    fireEvent.click(screen.getByRole("button", { name: "Add status" }));
    const sheet = screen.getByRole("dialog", { name: "Add status" });
    fireEvent.change(within(sheet).getByLabelText("Name (English)"), { target: { value: "  Ready for Review  " } });
    fireEvent.change(within(sheet).getByLabelText("Name (Arabic)"), { target: { value: "جاهز للمراجعة" } });
    fireEvent.change(within(sheet).getByLabelText("Code"), { target: { value: "  REVIEW  " } });
    fireEvent.change(within(sheet).getByLabelText("Color"), { target: { value: "emerald" } });
    fireEvent.click(within(sheet).getByRole("checkbox", { name: /Default/ }));
    fireEvent.click(within(sheet).getByRole("button", { name: "Save status" }));

    await waitFor(() => expect(mocks.createStatus).toHaveBeenCalledWith({ code: "REVIEW", nameEn: "Ready for Review", nameAr: "جاهز للمراجعة", displayOrder: 40, isActive: true, color: "emerald", isDefault: true }));
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Add status" })).not.toBeInTheDocument());
  });

  it("keeps codes immutable on edit and translates a protected-status update error", async () => {
    mocks.updateStatus.mockRejectedValue(new Error("A status currently used by work items cannot be deactivated"));
    renderWorkItemStatusesPage();
    const row = screen.getAllByRole("row")[1];
    fireEvent.click(within(row).getByRole("button", { name: "Edit status Open" }));
    const sheet = screen.getByRole("dialog", { name: "Edit status" });
    expect(within(sheet).getByLabelText("Code")).toBeDisabled();
    fireEvent.click(within(sheet).getByRole("button", { name: "Save status" }));
    await waitFor(() => expect(mocks.updateStatus).toHaveBeenCalledWith({ id: "status-1", input: expect.objectContaining({ code: "OPEN" }) }));
    expect(await screen.findByRole("alert")).toHaveTextContent("This status is currently used by work items and cannot be deactivated.");
    expect(screen.getByRole("dialog", { name: "Edit status" })).toBeInTheDocument();
  });

  it("sets the default, deactivates, reactivates, and reorders statuses through the API", async () => {
    renderWorkItemStatusesPage();
    const rows = screen.getAllByRole("row");
    fireEvent.click(within(rows[3]).getByRole("button", { name: "Set default" }));
    await waitFor(() => expect(mocks.updateStatus).toHaveBeenCalledWith({ id: "status-3", input: expect.objectContaining({ isDefault: true }) }));

    fireEvent.click(within(screen.getAllByRole("row")[1]).getByRole("button", { name: "Deactivate" }));
    await waitFor(() => expect(mocks.updateStatus).toHaveBeenCalledWith({ id: "status-1", input: expect.objectContaining({ isActive: false, isDefault: false }) }));
    fireEvent.click(within(screen.getAllByRole("row")[2]).getByRole("button", { name: "Activate" }));
    await waitFor(() => expect(mocks.updateStatus).toHaveBeenCalledWith({ id: "status-2", input: expect.objectContaining({ isActive: true }) }));

    mocks.updateStatus.mockClear();
    fireEvent.click(within(screen.getAllByRole("row")[1]).getByRole("button", { name: "Move down" }));
    await waitFor(() => expect(mocks.updateStatus).toHaveBeenCalledTimes(2));
    expect(mocks.updateStatus).toHaveBeenNthCalledWith(1, { id: "status-1", input: expect.objectContaining({ displayOrder: 20 }) });
    expect(mocks.updateStatus).toHaveBeenNthCalledWith(2, { id: "status-2", input: expect.objectContaining({ displayOrder: 10 }) });
  });

  it("shows a retry action when status data fails to load", () => {
    mocks.statuses.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch: mocks.refetch });
    renderWorkItemStatusesPage();
    fireEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(mocks.refetch).toHaveBeenCalledOnce();
  });
});
