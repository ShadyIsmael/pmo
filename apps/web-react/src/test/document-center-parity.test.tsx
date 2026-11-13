import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { I18nProvider } from "../i18n/messages";
import { filterSections, navigationConfig } from "../lib/navigation";
import { DocumentsCenterPage, ProjectDocumentsTab } from "../features/projects/ProjectDocumentsTab";

const mocks = vi.hoisted(() => ({
  useProjectDocuments: vi.fn(),
  useDocumentCategories: vi.fn(),
  useManagedDocumentCategories: vi.fn(),
  useCreateDocumentCategory: vi.fn(),
  useUpdateDocumentCategory: vi.fn(),
  useSetDocumentCategoryStatus: vi.fn(),
  useTemplateCategoryOptions: vi.fn(),
  useProjectDocument: vi.fn(),
  useArchiveDocument: vi.fn(),
  useActivateDocument: vi.fn(),
  useUploadDocument: vi.fn(),
  useUploadDocumentVersion: vi.fn(),
  confirm: vi.fn(),
  permissions: ["Document.View", "DocumentCategory.View", "Document.Create", "Document.Edit", "Document.Archive", "Document.TemplateManage"],
}));

vi.mock("../features/projects/api/documents", () => ({
  documentApi: { list: vi.fn().mockResolvedValue({ totalCount: 1 }), download: vi.fn(), preview: vi.fn() },
  useProjectDocuments: mocks.useProjectDocuments,
  useDocumentCategories: mocks.useDocumentCategories,
  useManagedDocumentCategories: mocks.useManagedDocumentCategories,
  useCreateDocumentCategory: mocks.useCreateDocumentCategory,
  useUpdateDocumentCategory: mocks.useUpdateDocumentCategory,
  useSetDocumentCategoryStatus: mocks.useSetDocumentCategoryStatus,
  useTemplateCategoryOptions: mocks.useTemplateCategoryOptions,
  useProjectDocument: mocks.useProjectDocument,
  useArchiveDocument: mocks.useArchiveDocument,
  useActivateDocument: mocks.useActivateDocument,
  useUploadDocument: mocks.useUploadDocument,
  useUploadDocumentVersion: mocks.useUploadDocumentVersion,
}));

vi.mock("@components/ui/ConfirmProvider", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@components/ui/ConfirmProvider")>();
  return { ...actual, useConfirm: () => mocks.confirm };
});

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: mocks.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, effectiveReadOnly: false, context: null, snapshot: {
  scope: { portfolioIds: [], programIds: [], projectIds: [], organizationIds: ["org-native"], organizationReferences: [{ id: "org-native", name: "Native organization" }] },
  selectedContext: { selectedPerspective: "ProjectManager", viewMode: "Native", effectiveReadOnly: false, scope: { portfolioIds: [], programIds: [], projectIds: [], organizationIds: ["org-1"], organizationReferences: [{ id: "org-1", name: "FOPX Organization" }] } },
} }) }));

const listData = {
  items: [{ id: "doc-1", name: "Delivery Plan", fileName: "delivery-plan.pdf", fileType: "PDF", fileSize: 2048, contentType: "application/pdf", status: "Active", version: 2, isTemplate: false, isArchived: false, createdAt: "2026-03-01T00:00:00Z", updatedAt: "2026-03-02T00:00:00Z" }],
  totalCount: 21,
  page: 1,
  pageSize: 15,
  totalPages: 2,
};

const templateItem = { id: "template-1", name: "Approved Handbook", description: "Approved staff policy handbook", fileName: "handbook.pdf", fileType: "PDF", fileSize: 4096, contentType: "application/pdf", organizationId: "org-1", organizationName: "FOPX Organization", categoryId: "category-1", categoryName: "Policies", status: "Active", version: 4, ownerId: "owner-1", isTemplate: true, isArchived: false, createdAt: "2026-03-01T00:00:00Z", updatedAt: "2026-03-02T00:00:00Z" };
const archivedTemplateItem = { ...templateItem, id: "template-archived", name: "Archived Checklist", status: "Archived", isArchived: true };
const templateListData = { ...listData, items: [templateItem], totalCount: 1, totalPages: 1 };
const archivedTemplateListData = { ...listData, items: [archivedTemplateItem], totalCount: 1, totalPages: 1 };
const templateDetail = { document: templateItem, versions: [{ id: "version-4", versionNumber: 4, fileName: "handbook.pdf", fileSize: 4096, contentType: "application/pdf", uploadedAt: "2026-03-02T00:00:00Z", isCurrent: true }] };
const archivedTemplateDetail = { ...templateDetail, document: archivedTemplateItem };

function renderGlobalCenter() {
  return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><I18nProvider><MemoryRouter><DocumentsCenterPage /></MemoryRouter></I18nProvider></QueryClientProvider>);
}

function ProjectOutlet() {
  return <Outlet context={{ projectId: "public-42", project: { id: "internal-42", publicId: "public-42" } }} />;
}

function renderProjectCenter() {
  return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><I18nProvider><MemoryRouter initialEntries={["/projects/public-42/documents"]}><Routes><Route path="/projects/:id" element={<ProjectOutlet />}><Route path="documents" element={<ProjectDocumentsTab />} /></Route></Routes></MemoryRouter></I18nProvider></QueryClientProvider>);
}

describe("Document Center source parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.useProjectDocuments.mockReset();
    mocks.useProjectDocuments.mockImplementation((query: { projectId?: string; templatesOnly?: boolean; status?: string }) => ({ data: query.projectId ? listData : query.status === "Archived" ? archivedTemplateListData : templateListData, isLoading: false, isError: false, refetch: vi.fn() }));
    mocks.useDocumentCategories.mockReset();
    mocks.useDocumentCategories.mockReturnValue({ data: [{ id: "category-1", name: "Policies", nameEn: "Policies", nameAr: "السياسات", organizationId: "org-1", isActive: true, description: "Tenant policies", sortOrder: 1 }], isLoading: false, isError: false, refetch: vi.fn() });
    mocks.useManagedDocumentCategories.mockReset();
    mocks.useManagedDocumentCategories.mockReturnValue({ data: [{ id: "category-1", name: "Policies", nameEn: "Policies", nameAr: "السياسات", organizationId: "org-1", isActive: true, description: "Organization policies", usageCount: 2, canManage: true }], isLoading: false, isError: false, refetch: vi.fn() });
    mocks.useCreateDocumentCategory.mockReset();
    mocks.useCreateDocumentCategory.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.useUpdateDocumentCategory.mockReset();
    mocks.useUpdateDocumentCategory.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.useSetDocumentCategoryStatus.mockReset();
    mocks.useSetDocumentCategoryStatus.mockReturnValue({ mutateAsync: vi.fn().mockResolvedValue(undefined), isPending: false });
    mocks.useTemplateCategoryOptions.mockReset().mockReturnValue({ data: [{ id: "category-1", nameEn: "Policies", nameAr: "السياسات" }], isLoading: false, isError: false });
    mocks.confirm.mockReset();
    mocks.confirm.mockResolvedValue(true);
    mocks.useProjectDocument.mockReset();
    mocks.useProjectDocument.mockImplementation((id: string | null) => ({ data: id ? id === "template-archived" ? archivedTemplateDetail : templateDetail : undefined, isLoading: false }));
    mocks.useArchiveDocument.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.useActivateDocument.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.useUploadDocument.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.useUploadDocumentVersion.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.permissions = ["Document.View", "DocumentCategory.View", "Document.Create", "Document.Edit", "Document.Archive", "Document.TemplateManage"];
  });

  it("renders the approved Document Library and keeps category management outside the Library", () => {
    renderGlobalCenter();

    expect(screen.getByRole("heading", { name: "Document Library" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Browse Library" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Recent Documents" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Manage Categories" })).toHaveAttribute("href", "/administration/reference-data/document-categories");
    expect(screen.getByRole("button", { name: /Policies/ })).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Categories" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Upload document" })).toBeInTheDocument();
    expect(screen.getByText("Approved Handbook")).toBeInTheDocument();
    expect(mocks.useProjectDocuments).toHaveBeenCalledWith(expect.objectContaining({ page: 1, pageSize: 15, organizationId: "org-1", templatesOnly: false }), true);
    expect(mocks.useManagedDocumentCategories).toHaveBeenCalledWith({ organizationId: "org-1" }, true);
  });

  it("shows organization-scoped approved templates with metadata and only template-specific upload", () => {
    renderGlobalCenter();

    expect(screen.getByText("Approved Handbook")).toBeInTheDocument();
    expect(screen.getAllByText("Policies").length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText("FOPX Organization").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText(/Version: v4/)).toBeInTheDocument();
    expect(mocks.useProjectDocuments).toHaveBeenCalledWith(expect.objectContaining({ templatesOnly: false, organizationId: "org-1", status: "Active" }), true);

    fireEvent.click(screen.getByRole("button", { name: "Upload document" }));
    expect(screen.getByText("This template must already have external approval. Uploading it does not verify any external signature.")).toBeInTheDocument();
  });

  it("preserves search and folder filters when switching views and clears them", () => {
    renderGlobalCenter();
    fireEvent.click(screen.getByRole("button", { name: /Policies/ }));
    const search = screen.getByPlaceholderText("Search documents or categories");
    fireEvent.change(search, { target: { value: "handbook" } });
    expect(mocks.useProjectDocuments).toHaveBeenCalledWith(expect.objectContaining({ organizationId: "org-1", categoryId: "category-1", search: "handbook" }), true);
    fireEvent.click(screen.getByRole("button", { name: "Grid view" }));
    expect(search).toHaveValue("handbook");
    expect(screen.getByRole("button", { name: "Grid view" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("combobox", { name: "Filter by category" })).toHaveValue("category-1");
    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));
    expect(search).toHaveValue("");
    expect(mocks.useProjectDocuments).toHaveBeenCalledWith(expect.objectContaining({ pageSize: 15, categoryId: undefined, search: undefined }), true);
  });

  it("requires permission combinations for template actions and gates reactivation on Edit plus TemplateManage", () => {
    mocks.permissions = ["Document.View", "Document.TemplateManage"];
    const { unmount } = renderGlobalCenter();
    fireEvent.click(screen.getByRole("button", { name: /Approved Handbook/ }));
    expect(screen.queryByRole("button", { name: "Upload document" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "New version" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Archive" })).not.toBeInTheDocument();
    unmount();

    mocks.permissions = ["Document.View", "Document.TemplateManage", "Document.Edit"];
    renderGlobalCenter();
    fireEvent.change(screen.getByRole("combobox", { name: "Filter by status" }), { target: { value: "Archived" } });
    fireEvent.click(screen.getByRole("button", { name: /Archived Checklist/ }));
    expect(screen.getByRole("button", { name: "Activate" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Archive" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "New version" })).not.toBeInTheDocument();
  });

  it("shows category-only users a management shortcut without exposing Library documents", async () => {
    mocks.permissions = ["DocumentCategory.View"];
    renderGlobalCenter();

    await waitFor(() => expect(screen.getByRole("link", { name: "Manage Categories" })).toHaveAttribute("href", "/administration/reference-data/document-categories"));
    expect(screen.queryByText("Document categories")).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Documents" })).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Templates" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Upload document" })).not.toBeInTheDocument();
    expect(mocks.useProjectDocuments.mock.calls.every(([, enabled]) => enabled === false)).toBe(true);
    expect(mocks.useProjectDocument.mock.calls.every(([id]) => id === null)).toBe(true);
    const sections = filterSections(navigationConfig, ["DocumentCategory.View"]);
    expect(sections.find((section) => section.id === "governance")).toBeUndefined();
    expect(sections.find((section) => section.id === "administration")?.items.some((item) => item.id === "reference-data")).toBe(true);
  });

  it("loads active template category options for an authorized uploader without category-list permission", async () => {
    mocks.permissions = ["Document.View", "Document.Create", "Document.TemplateManage"];
    renderGlobalCenter();

    fireEvent.click(screen.getByRole("button", { name: "Upload document" }));
    const categorySelect = screen.getByRole("combobox", { name: "Category" });
    expect(categorySelect).toHaveDisplayValue("Select category");
    expect(within(categorySelect).getByRole("option", { name: "Policies" })).toHaveValue("category-1");
    await waitFor(() => expect(mocks.useTemplateCategoryOptions).toHaveBeenCalledWith("org-1", true));
    expect(screen.queryByRole("link", { name: "Manage Categories" })).not.toBeInTheDocument();
  });

  it("keeps the template upload sheet open and displays a localized API failure", async () => {
    const upload = vi.fn().mockRejectedValue(new Error("rejected upload"));
    mocks.useUploadDocument.mockReturnValue({ mutateAsync: upload, isPending: false });
    renderGlobalCenter();
    fireEvent.click(screen.getByRole("button", { name: "Upload document" }));
    const uploadName = screen.getByRole("textbox", { name: "Name" });
    const uploadForm = uploadName.closest("form");
    expect(uploadForm).not.toBeNull();
    if (uploadForm) fireEvent.submit(uploadForm);
    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("The document action could not be completed."));
    expect(screen.getByRole("heading", { name: "Register approved template" })).toBeInTheDocument();
  });

  it.each([
    { isMandatory: true, label: "Mandatory" },
    { isMandatory: false, label: "Optional" },
  ])("shows the reloaded template as $label", async ({ isMandatory, label }) => {
    const savedTemplate = { ...templateItem, isMandatory };
    mocks.useProjectDocuments.mockImplementation((query: { projectId?: string }) => ({
      data: query.projectId ? listData : { ...templateListData, items: [savedTemplate] },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    }));
    mocks.useProjectDocument.mockImplementation((id: string | null) => ({
      data: id ? { ...templateDetail, document: savedTemplate } : undefined,
      isLoading: false,
    }));

    renderGlobalCenter();
    fireEvent.click(screen.getByRole("button", { name: /Approved Handbook/ }));

    expect(await screen.findByText(label)).toBeInTheDocument();
  });

  it.each([
    { checked: true, expectedValue: "true" },
    { checked: false, expectedValue: null },
  ])("submits the template Mandatory=$checked designation", async ({ checked, expectedValue }) => {
    const upload = vi.fn().mockResolvedValue(undefined);
    mocks.useUploadDocument.mockReturnValue({ mutateAsync: upload, isPending: false });
    renderGlobalCenter();

    fireEvent.click(screen.getByRole("button", { name: "Upload document" }));
    const checkbox = screen.getByRole("checkbox", { name: "Mandatory Document" });
    if (checked) fireEvent.click(checkbox);
    fireEvent.change(screen.getByRole("textbox", { name: "Name" }), { target: { value: "Mandatory policy" } });
    fireEvent.change(screen.getByLabelText("File"), {
      target: { files: [new File(["%PDF-1.7"], "policy.pdf", { type: "application/pdf" })] },
    });
    const form = checkbox.closest("form");
    expect(form).not.toBeNull();
    if (form) fireEvent.submit(form);

    await waitFor(() => expect(upload).toHaveBeenCalledOnce());
    const submitted = upload.mock.calls[0][0] as FormData;
    expect(submitted.get("IsMandatory")).toBe(expectedValue);
    expect(submitted.get("IsTemplate")).toBe("true");
    expect(submitted.get("OrganizationId")).toBe("org-1");
  });

  it("keeps the embedded project center compact and scoped to its project", () => {
    renderProjectCenter();

    expect(screen.getByRole("heading", { name: "Project documents" })).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Categories" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Start from template" })).toBeInTheDocument();
    expect(mocks.useProjectDocuments).toHaveBeenCalledWith(expect.objectContaining({ projectId: "public-42", page: 1, pageSize: 8, templatesOnly: false }), true);
    expect(mocks.useProjectDocuments).toHaveBeenCalledWith(expect.objectContaining({ forProjectId: "internal-42", templatesOnly: true }), false);
  });
});
