import * as React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../i18n/messages";
import { DocumentsCenterPage, ProjectDocumentsTab } from "../features/projects/ProjectDocumentsTab";

const mocks = vi.hoisted(() => ({
  useProjectDocuments: vi.fn(),
  useDocumentCategories: vi.fn(),
  useProjectDocument: vi.fn(),
  useArchiveDocument: vi.fn(),
  useUploadDocument: vi.fn(),
  useUploadDocumentVersion: vi.fn(),
  permissions: ["Document.Create", "Document.Edit", "Document.Archive", "Document.TemplateManage"],
}));

vi.mock("../features/projects/api/documents", () => ({
  documentApi: { download: vi.fn(), preview: vi.fn() },
  useProjectDocuments: mocks.useProjectDocuments,
  useDocumentCategories: mocks.useDocumentCategories,
  useProjectDocument: mocks.useProjectDocument,
  useArchiveDocument: mocks.useArchiveDocument,
  useUploadDocument: mocks.useUploadDocument,
  useUploadDocumentVersion: mocks.useUploadDocumentVersion,
}));

vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: mocks.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, effectiveReadOnly: false, context: null }) }));

const listData = {
  items: [{ id: "doc-1", name: "Delivery Plan", fileName: "delivery-plan.pdf", fileType: "PDF", fileSize: 2048, contentType: "application/pdf", status: "Active", version: 2, isTemplate: false, isArchived: false, createdAt: "2026-03-01T00:00:00Z", updatedAt: "2026-03-02T00:00:00Z" }],
  totalCount: 21,
  page: 1,
  pageSize: 15,
  totalPages: 2,
};

function renderGlobalCenter() {
  return render(<I18nProvider><MemoryRouter><DocumentsCenterPage /></MemoryRouter></I18nProvider>);
}

function ProjectOutlet() {
  return <Outlet context={{ projectId: "project-42" }} />;
}

function renderProjectCenter() {
  return render(<I18nProvider><MemoryRouter initialEntries={["/projects/project-42/documents"]}><Routes><Route path="/projects/:id" element={<ProjectOutlet />}><Route path="documents" element={<ProjectDocumentsTab />} /></Route></Routes></MemoryRouter></I18nProvider>);
}

describe("Document Center source parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.useProjectDocuments.mockReset();
    mocks.useProjectDocuments.mockReturnValue({ data: listData, isLoading: false, isError: false, refetch: vi.fn() });
    mocks.useDocumentCategories.mockReset();
    mocks.useDocumentCategories.mockReturnValue({ data: [{ id: "category-1", name: "Policies", description: "Tenant policies", sortOrder: 1 }], isLoading: false });
    mocks.useProjectDocument.mockReturnValue({ data: undefined, isLoading: false });
    mocks.useArchiveDocument.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.useUploadDocument.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.useUploadDocumentVersion.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    mocks.permissions = ["Document.Create", "Document.Edit", "Document.Archive", "Document.TemplateManage"];
  });

  it("renders the full tenant document center with search, categories, and source page size", () => {
    renderGlobalCenter();

    expect(screen.getByRole("heading", { name: "Document Centre" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Documents" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Templates" })).toBeInTheDocument();
    const categoriesTab = screen.getByRole("tab", { name: "Categories" });
    expect(screen.getByRole("button", { name: "Upload document" })).toBeInTheDocument();
    expect(screen.getByText("Delivery Plan")).toBeInTheDocument();
    expect(mocks.useProjectDocuments).toHaveBeenCalledWith(expect.objectContaining({ page: 1, pageSize: 15, templatesOnly: false }), true);

    fireEvent.click(categoriesTab);
    expect(screen.getByText("Document categories")).toBeInTheDocument();
    expect(screen.getByText("Policies")).toBeInTheDocument();
    expect(mocks.useDocumentCategories).toHaveBeenLastCalledWith(true);
  });

  it("keeps the embedded project center compact and scoped to its project", () => {
    renderProjectCenter();

    expect(screen.getByRole("heading", { name: "Project documents" })).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Categories" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Start from template" })).toBeInTheDocument();
    expect(mocks.useProjectDocuments).toHaveBeenCalledWith(expect.objectContaining({ projectId: "project-42", page: 1, pageSize: 8, templatesOnly: false }), true);
  });
});
