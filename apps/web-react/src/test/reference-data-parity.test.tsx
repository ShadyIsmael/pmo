import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import arMessages from "../i18n/messages/ar.json";
import { ApiError } from "../api/client";
import { VendorCategoriesPage } from "../features/administration/VendorCategoriesPage";
import { ProjectRolesPage } from "../features/administration/ProjectRolesPage";
import { WorkItemStatusesPage } from "../features/administration/WorkItemStatusesPage";
import { ReferenceDataPage } from "../features/administration/AdminPages";
import { DocumentCategoriesPage } from "../features/administration/DocumentCategoriesPage";

const mocks = vi.hoisted(() => ({
  useAuth: vi.fn(),
  categories: vi.fn(),
  createHook: vi.fn(),
  updateHook: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  deactivateCategory: vi.fn(),
  deactivateCategoryHook: vi.fn(),
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
  deactivateStatus: vi.fn(),
  createStatusHook: vi.fn(),
  updateStatusHook: vi.fn(),
  deactivateStatusHook: vi.fn(),
  managedDocumentCategories: vi.fn(),
  createDocumentCategory: vi.fn(),
  updateDocumentCategory: vi.fn(),
  setDocumentCategoryStatus: vi.fn(),
  createDocumentCategoryHook: vi.fn(),
  updateDocumentCategoryHook: vi.fn(),
  setDocumentCategoryStatusHook: vi.fn(),
  projectLifecycleCatalog: vi.fn(),
  updateProjectPhase: vi.fn(),
  updateProjectLifecycleItemHook: vi.fn(),
  useManagement: vi.fn(),
  confirm: vi.fn(),
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: mocks.useAuth }));
vi.mock("@components/ui/ConfirmProvider", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@components/ui/ConfirmProvider")>()),
  useConfirm: () => mocks.confirm,
}));
vi.mock("@api/management", () => ({ useManagement: mocks.useManagement }));
vi.mock("../features/projects/api/documents", () => ({
  useManagedDocumentCategories: mocks.managedDocumentCategories,
  useCreateDocumentCategory: mocks.createDocumentCategoryHook,
  useUpdateDocumentCategory: mocks.updateDocumentCategoryHook,
  useSetDocumentCategoryStatus: mocks.setDocumentCategoryStatusHook,
}));
vi.mock("../features/administration/api/projectLifecycle", () => ({
  useProjectLifecycleCatalog: mocks.projectLifecycleCatalog,
  useUpdateProjectLifecycleItem: mocks.updateProjectLifecycleItemHook,
}));
vi.mock("../features/administration/api/admin", () => ({
  useVendorCategories: mocks.categories,
  useCreateVendorCategory: mocks.createHook,
  useUpdateVendorCategory: mocks.updateHook,
  useDeactivateVendorCategory: mocks.deactivateCategoryHook,
  useWorkItemStatuses: mocks.statuses,
  useCreateWorkItemStatus: mocks.createStatusHook,
  useUpdateWorkItemStatus: mocks.updateStatusHook,
  useDeactivateWorkItemStatus: mocks.deactivateStatusHook,
}));
vi.mock("../features/administration/api/settings", () => ({
  useOrganizationProfiles: () => ({ data: [], isLoading: false, isError: false }),
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

function renderProjectRolesPage(permissions = ["Settings.View", "ReferenceData.View", "ReferenceData.Create", "ReferenceData.Edit", "ReferenceData.Delete"]) {
  window.localStorage.setItem("nexus-locale", "en");
  mocks.useAuth.mockReturnValue({ user: { id: "admin-1", permissions }, status: "authenticated" });
  return render(<I18nProvider><MemoryRouter initialEntries={["/administration/reference-data/project-roles"]}><ProjectRolesPage /></MemoryRouter></I18nProvider>);
}

function renderWorkItemStatusesPage(permissions = ["Settings.View", "ReferenceData.View", "ReferenceData.Create", "ReferenceData.Edit", "ReferenceData.Delete"]) {
  window.localStorage.setItem("nexus-locale", "en");
  mocks.useAuth.mockReturnValue({ user: { id: "admin-1", permissions }, status: "authenticated" });
  return render(<I18nProvider><MemoryRouter initialEntries={["/administration/reference-data/work-item-statuses"]}><WorkItemStatusesPage /></MemoryRouter></I18nProvider>);
}

const managedDocumentCategories = [
  { id: "document-category-1", organizationId: "org-a", nameEn: "Policies", nameAr: "السياسات", description: "Approved policy documents", usageCount: 2, isActive: true, canManage: true },
  { id: "document-category-2", organizationId: "org-a", nameEn: "Archived Forms", nameAr: "النماذج المؤرشفة", description: "", usageCount: 0, isActive: false, canManage: true },
];

function renderDocumentCategoriesPage(locale: "en" | "ar" = "en", permissions = ["DocumentCategory.View", "DocumentCategory.Create", "DocumentCategory.Edit", "DocumentCategory.ActivateDeactivate"]) {
  window.localStorage.setItem("nexus-locale", locale);
  mocks.useAuth.mockReturnValue({ user: { id: "category-admin", permissions }, status: "authenticated" });
  return render(<I18nProvider><MemoryRouter initialEntries={["/administration/reference-data/document-categories"]}><DocumentCategoriesPage /></MemoryRouter></I18nProvider>);
}

describe("Vendor Categories source parity", () => {
  beforeEach(() => {
    mocks.useAuth.mockReset().mockReturnValue({ user: { id: "admin-1", permissions: ["Settings.View", "ReferenceData.View", "ReferenceData.Create", "ReferenceData.Edit", "ReferenceData.Delete"] }, status: "authenticated" });
    mocks.categories.mockReset().mockReturnValue({ data: initialCategories, isLoading: false, isError: false, refetch: mocks.refetch });
    mocks.create.mockReset().mockResolvedValue(undefined);
    mocks.update.mockReset().mockResolvedValue(undefined);
    mocks.refetch.mockReset();
    mocks.createHook.mockReturnValue({ mutateAsync: mocks.create, isPending: false });
    mocks.updateHook.mockReturnValue({ mutateAsync: mocks.update, isPending: false });
    mocks.deactivateCategory.mockReset().mockResolvedValue(undefined);
    mocks.deactivateCategoryHook.mockReturnValue({ mutateAsync: mocks.deactivateCategory, isPending: false });
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
    mocks.deactivateStatus.mockReset().mockResolvedValue(undefined);
    mocks.createStatusHook.mockReturnValue({ mutateAsync: mocks.createStatus, isPending: false });
    mocks.updateStatusHook.mockReturnValue({ mutateAsync: mocks.updateStatus, isPending: false });
    mocks.deactivateStatusHook.mockReturnValue({ mutateAsync: mocks.deactivateStatus, isPending: false });
    mocks.managedDocumentCategories.mockReset().mockReturnValue({ data: managedDocumentCategories, isLoading: false, isError: false, refetch: mocks.refetch });
    mocks.createDocumentCategory.mockReset().mockResolvedValue(undefined);
    mocks.updateDocumentCategory.mockReset().mockResolvedValue(undefined);
    mocks.setDocumentCategoryStatus.mockReset().mockResolvedValue(undefined);
    mocks.createDocumentCategoryHook.mockReturnValue({ mutateAsync: mocks.createDocumentCategory, isPending: false });
    mocks.updateDocumentCategoryHook.mockReturnValue({ mutateAsync: mocks.updateDocumentCategory, isPending: false });
    mocks.setDocumentCategoryStatusHook.mockReturnValue({ mutateAsync: mocks.setDocumentCategoryStatus, isPending: false });
    mocks.projectLifecycleCatalog.mockReset().mockReturnValue({ data: [], isLoading: false, isError: false, refetch: mocks.refetch });
    mocks.updateProjectPhase.mockReset().mockResolvedValue(undefined);
    mocks.updateProjectLifecycleItemHook.mockReturnValue({ mutateAsync: mocks.updateProjectPhase, isPending: false });
    mocks.useManagement.mockReset().mockReturnValue({ canMutate: true, loading: false, snapshot: {
      scope: { organizationIds: ["org-a", "org-b"], organizationReferences: [{ id: "org-a", name: "Alpha Organization" }, { id: "org-b", name: "Beta Organization" }] },
      selectedContext: null,
    } });
    mocks.confirm.mockReset().mockResolvedValue(true);
    vi.stubGlobal("confirm", vi.fn().mockReturnValue(true));
  });

  it("presents the Reference Data source hub with a permission-gated Document Categories link", () => {
    mocks.useAuth.mockReturnValue({ user: { id: "admin-1", permissions: ["ReferenceData.View", "DocumentCategory.View"] }, status: "authenticated" });
    render(<I18nProvider><MemoryRouter initialEntries={["/administration/reference-data"]}><ReferenceDataPage /></MemoryRouter></I18nProvider>);

    expect(screen.getByRole("link", { name: /Project Roles/ })).toHaveAttribute("href", "/administration/reference-data/project-roles");
    expect(screen.getByRole("link", { name: /Vendor Categories/ })).toHaveAttribute("href", "/administration/reference-data/vendor-categories");
    expect(screen.getByRole("link", { name: /Document Categories/ })).toHaveAttribute("href", "/administration/reference-data/document-categories");
    expect(screen.getByRole("link", { name: /Work Item Statuses/ })).toHaveAttribute("href", "/administration/reference-data/work-item-statuses");
    expect(screen.getByRole("link", { name: /Currencies/ })).toHaveAttribute("href", "/administration/currencies");
  });

  it("does not show Document Categories on the Reference Data hub without category View", () => {
    mocks.useAuth.mockReturnValue({ user: { id: "reader-1", permissions: ["ReferenceData.View"] }, status: "authenticated" });
    render(<I18nProvider><MemoryRouter initialEntries={["/administration/reference-data"]}><ReferenceDataPage /></MemoryRouter></I18nProvider>);
    expect(screen.queryByRole("link", { name: /Document Categories/ })).not.toBeInTheDocument();
  });

  it("navigates from the Reference Data card to the Document Categories screen", () => {
    mocks.useAuth.mockReturnValue({ user: { id: "admin-1", permissions: ["DocumentCategory.View"] }, status: "authenticated" });
    render(<I18nProvider><MemoryRouter initialEntries={["/administration/reference-data"]}><Routes>
      <Route path="/administration/reference-data" element={<ReferenceDataPage />} />
      <Route path="/administration/reference-data/document-categories" element={<DocumentCategoriesPage />} />
    </Routes></MemoryRouter></I18nProvider>);

    fireEvent.click(screen.getByRole("link", { name: /Document Categories/ }));
    expect(screen.getByRole("heading", { level: 1, name: "Document categories" })).toBeInTheDocument();
  });

  it("localizes the Document Categories hub link in Arabic and sets RTL direction", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    mocks.useAuth.mockReturnValue({ user: { id: "admin-1", permissions: ["DocumentCategory.View"] }, status: "authenticated" });
    render(<I18nProvider><MemoryRouter initialEntries={["/administration/reference-data"]}><ReferenceDataPage /></MemoryRouter></I18nProvider>);

    const title = arMessages.administration.areas.referenceData.documentCategories.title;
    expect(screen.getByRole("link", { name: new RegExp(title) })).toHaveAttribute("href", "/administration/reference-data/document-categories");
    await waitFor(() => expect(document.documentElement.dir).toBe("rtl"));
  });

  it("loads categories only for an authorized selected Organization and renders localized names and RTL", async () => {
    const { unmount } = renderDocumentCategoriesPage();
    const organizationSelect = screen.getByRole("combobox", { name: "Organization" });
    fireEvent.change(organizationSelect, { target: { value: "org-a" } });
    fireEvent.change(screen.getByRole("textbox", { name: "Search categories" }), { target: { value: "policies" } });
    await waitFor(() => expect(mocks.managedDocumentCategories).toHaveBeenCalledWith({ organizationId: "org-a", search: "policies", includeInactive: true }, true));
    expect(screen.getByText("Policies")).toBeInTheDocument();
    expect(screen.getByText("Used by 2 templates")).toBeInTheDocument();
    expect(screen.getByText("Inactive")).toBeInTheDocument();
    unmount();

    renderDocumentCategoriesPage("ar");
    fireEvent.change(screen.getByRole("combobox", { name: "المؤسسة" }), { target: { value: "org-a" } });
    await waitFor(() => expect(document.documentElement.dir).toBe("rtl"));
    expect(screen.getByText("السياسات")).toBeInTheDocument();
  });

  it("creates bilingual categories in the selected Organization and keeps duplicate errors in the side sheet", async () => {
    mocks.createDocumentCategory.mockRejectedValue(new ApiError(409, "Duplicate category", "CONFLICT"));
    renderDocumentCategoriesPage();
    fireEvent.change(screen.getByRole("combobox", { name: "Organization" }), { target: { value: "org-a" } });
    fireEvent.click(screen.getByRole("button", { name: "Create category" }));
    const sheet = screen.getByRole("dialog", { name: "Create category" });
    fireEvent.change(within(sheet).getByLabelText("English name"), { target: { value: "  Governance  " } });
    fireEvent.change(within(sheet).getByLabelText("Arabic name"), { target: { value: "الحوكمة" } });
    fireEvent.change(within(sheet).getByLabelText("Description"), { target: { value: "  Governance documents  " } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save category" }));

    await waitFor(() => expect(mocks.createDocumentCategory).toHaveBeenCalledWith({ organizationId: "org-a", nameEn: "Governance", nameAr: "الحوكمة", description: "Governance documents" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("A category with these names already exists in this organization.");
    expect(screen.getByRole("dialog", { name: "Create category" })).toBeInTheDocument();
  });

  it("edits a category and requires confirmation before deactivation", async () => {
    renderDocumentCategoriesPage();
    fireEvent.change(screen.getByRole("combobox", { name: "Organization" }), { target: { value: "org-a" } });
    fireEvent.click(screen.getByRole("button", { name: "Edit category Policies" }));
    const sheet = screen.getByRole("dialog", { name: "Edit category" });
    fireEvent.change(within(sheet).getByLabelText("English name"), { target: { value: "Policy Library" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save category" }));
    await waitFor(() => expect(mocks.updateDocumentCategory).toHaveBeenCalledWith({ id: "document-category-1", input: { nameEn: "Policy Library", nameAr: "السياسات", description: "Approved policy documents" } }));

    fireEvent.click(screen.getByRole("button", { name: "Deactivate category Policies" }));
    await waitFor(() => expect(mocks.confirm).toHaveBeenCalledWith(expect.objectContaining({ title: "Deactivate category", destructive: true })));
    await waitFor(() => expect(mocks.setDocumentCategoryStatus).toHaveBeenCalledWith({ id: "document-category-1", isActive: false }));
  });

  it("allows an authorized user to edit a tenant-shared system category", async () => {
    mocks.managedDocumentCategories.mockReturnValue({
      data: [{ id: "system-governance", organizationId: null, nameEn: "Governance", nameAr: "الحوكمة", description: "System category", usageCount: 3, isActive: true, canManage: true }],
      isLoading: false,
      isError: false,
      refetch: mocks.refetch,
    });
    renderDocumentCategoriesPage();
    fireEvent.change(screen.getByRole("combobox", { name: "Organization" }), { target: { value: "org-a" } });
    fireEvent.click(screen.getByRole("button", { name: "Edit category Governance" }));
    const sheet = screen.getByRole("dialog", { name: "Edit category" });
    fireEvent.change(within(sheet).getByLabelText("English name"), { target: { value: "Governance Records" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save category" }));

    await waitFor(() => expect(mocks.updateDocumentCategory).toHaveBeenCalledWith({
      id: "system-governance",
      input: { nameEn: "Governance Records", nameAr: "الحوكمة", description: "System category" },
    }));
  });

  it("assigns a category to selected Organization phases, including protected Pre-Initiation", async () => {
    const phases = [
      { id: "phase-preinit", tenantId: "tenant-a", organizationId: "org-a", code: "PRE-INITIATION", nameEn: "Pre-Initiation", nameAr: "ما قبل البدء", displayOrder: 0, isDefault: true, isActive: true, isContinuousActivity: false, documentCategoryId: null, referencedProjectCount: 0, rowVersion: "AQ==" },
      { id: "phase-planning", tenantId: "tenant-a", organizationId: "org-a", code: "PLANNING", nameEn: "Planning", nameAr: "التخطيط", displayOrder: 10, isDefault: false, isActive: true, isContinuousActivity: false, documentCategoryId: null, referencedProjectCount: 0, rowVersion: "Ag==" },
      { id: "tenant-phase", tenantId: "tenant-a", organizationId: null, code: "TENANT", nameEn: "Tenant phase", nameAr: "مرحلة عامة", displayOrder: 1, isDefault: false, isActive: true, documentCategoryId: null, referencedProjectCount: 0, rowVersion: "Aw==" },
    ];
    mocks.projectLifecycleCatalog.mockReturnValue({ data: phases, isLoading: false, isError: false, refetch: mocks.refetch });
    mocks.createDocumentCategory.mockResolvedValue({ id: "new-category", organizationId: "org-a", canManage: true });
    renderDocumentCategoriesPage("en", ["DocumentCategory.View", "DocumentCategory.Create", "DocumentCategory.Edit", "ProjectLifecyclePhase.View", "ProjectLifecyclePhase.Edit"]);
    fireEvent.change(screen.getByRole("combobox", { name: "Organization" }), { target: { value: "org-a" } });
    fireEvent.click(screen.getByRole("button", { name: "Create category" }));
    const sheet = screen.getByRole("dialog", { name: "Create category" });
    fireEvent.change(within(sheet).getByLabelText("English name"), { target: { value: "Planning Records" } });
    fireEvent.change(within(sheet).getByLabelText("Arabic name"), { target: { value: "سجلات التخطيط" } });
    fireEvent.click(within(sheet).getByLabelText("Pre-Initiation"));
    fireEvent.click(within(sheet).getByLabelText("Planning"));
    fireEvent.click(within(sheet).getByRole("button", { name: "Save category" }));

    await waitFor(() => expect(mocks.createDocumentCategory).toHaveBeenCalledWith({ organizationId: "org-a", nameEn: "Planning Records", nameAr: "سجلات التخطيط", description: undefined }));
    await waitFor(() => expect(mocks.updateProjectPhase).toHaveBeenCalledTimes(2));
    expect(mocks.updateProjectPhase).toHaveBeenCalledWith(expect.objectContaining({
      id: "phase-preinit",
      input: expect.objectContaining({ organizationId: "org-a", documentCategoryId: "new-category", rowVersion: "AQ==", isDefault: true }),
    }));
    expect(mocks.updateProjectPhase).toHaveBeenCalledWith(expect.objectContaining({ id: "phase-planning" }));
    expect(mocks.updateProjectPhase).not.toHaveBeenCalledWith(expect.objectContaining({ id: "tenant-phase" }));
  });

  it("shows only the exact category operations granted to the current role", () => {
    renderDocumentCategoriesPage("en", ["DocumentCategory.View", "DocumentCategory.Create"]);
    fireEvent.change(screen.getByRole("combobox", { name: "Organization" }), { target: { value: "org-a" } });
    expect(screen.getByRole("button", { name: "Create category" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit category Policies" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Deactivate category Policies" })).not.toBeInTheDocument();
  });

  it("sorts by display order, uses semantic text-only status colors, and hides management actions without a mutation permission", () => {
    mocks.useAuth.mockReturnValue({ user: { id: "reader-1", permissions: ["Settings.View"] }, status: "authenticated" });
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
    mocks.create.mockRejectedValue(new ApiError(409, "A category with this code already exists", "CONFLICT"));
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

  it("lists project roles in order with text-only status and gates management by granular Reference Data permissions", () => {
    renderProjectRolesPage(["Settings.View"]);

    const rows = screen.getAllByRole("row");
    expect(within(rows[1]).getByText("PM")).toBeInTheDocument();
    expect(within(rows[1]).getByText("Active")).toHaveClass("text-success");
    expect(within(rows[2]).getByText("ANALYST")).toBeInTheDocument();
    expect(within(rows[2]).getByText("Inactive")).toHaveClass("text-muted-foreground");
    expect(screen.queryByRole("button", { name: "Add Role" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
  });

  it("shows only the operation granted to an ordinary Reference Data role", () => {
    mocks.useAuth.mockReturnValue({ user: { id: "creator-1", permissions: ["Settings.View", "ReferenceData.View", "ReferenceData.Create"] }, status: "authenticated" });
    renderPage();

    expect(screen.getByRole("button", { name: "Add category" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit category" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Deactivate" })).not.toBeInTheDocument();
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

  it("shows status summaries, filters by status and search, and respects the ReferenceData mutation capabilities", () => {
    renderWorkItemStatusesPage(["Settings.View"]);
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
    mocks.updateStatus.mockRejectedValue(new ApiError(409, "A status currently used by work items cannot be deactivated", "CONFLICT"));
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
    await waitFor(() => expect(mocks.deactivateStatus).toHaveBeenCalledWith("status-1"));
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
