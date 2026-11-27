import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { I18nProvider } from "../i18n/messages";
import { DocumentsCenterPage, ProjectDocumentsTab } from "../features/projects/ProjectDocumentsTab";

const state = vi.hoisted(() => ({
  permissions: ["Document.View", "Document.Archive"],
  items: [] as Array<Record<string, unknown>>,
  history: [] as Array<Record<string, unknown>>,
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock("@api/client", () => ({
  api: {
    get: state.get,
    post: state.post,
    put: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
    upload: vi.fn(),
    download: vi.fn(),
  },
  setApiErrorLocale: vi.fn(),
  ApiError: class ApiError extends Error {
    constructor(public status: number, message: string, public code?: string, public details?: unknown) { super(message); }
  },
}));

vi.mock("@auth/AuthProvider", () => ({
  useAuth: () => ({ user: { id: "user-1", permissions: state.permissions } }),
}));

vi.mock("@api/management", () => ({
  useManagement: () => {
    const scope = { portfolioIds: [], programIds: [], projectIds: [], organizationIds: ["org-1"], organizationReferences: [{ id: "org-1", name: "FOPX Organization" }] };
    return { canMutate: true, snapshot: { scope, selectedContext: { scope, effectiveReadOnly: false } } };
  },
}));

const template = {
  id: "template-1", name: "Approved Handbook", description: "Approved staff handbook", fileName: "handbook.pdf",
  fileType: "PDF", fileSize: 4096, contentType: "application/pdf", organizationId: "org-1",
  organizationName: "FOPX Organization", categoryId: "category-1", categoryName: "Policies", status: "Active",
  version: 4, ownerId: "owner-1", isTemplate: true, isArchived: false,
  createdAt: "2026-03-01T00:00:00Z", updatedAt: "2026-03-02T00:00:00Z",
};
const ordinaryDocument = { ...template, id: "document-1", name: "Delivery Plan", fileName: "delivery-plan.pdf", isTemplate: false };
const currentTemplateRequest = {
  id: "request-1", templateId: "template-1", templateName: "Approved Handbook", ownerOrganizationId: "org-1",
  ownerScope: "Organization", requesterId: "user-1", requesterName: "Current User",
  reason: "Replaced by the current handbook.", status: "PendingApproval", requestedAt: "2026-11-19T10:00:00Z",
};

function renderFullCenter() {
  return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <I18nProvider><MemoryRouter><DocumentsCenterPage /></MemoryRouter></I18nProvider>
  </QueryClientProvider>);
}

function ProjectOutlet() {
  return <Outlet context={{ projectId: "public-42", project: { id: "internal-42", publicId: "public-42" } }} />;
}

function renderCompactCenter() {
  return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <I18nProvider><MemoryRouter initialEntries={["/projects/public-42/documents"]}>
      <Routes><Route path="/projects/:id" element={<ProjectOutlet />}><Route path="documents" element={<ProjectDocumentsTab />} /></Route></Routes>
    </MemoryRouter></I18nProvider>
  </QueryClientProvider>);
}

describe("template deletion request in Document Center", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    state.permissions = ["Document.View", "Document.Archive"];
    state.items = [template];
    state.history = [];
    state.get.mockReset().mockImplementation(async (path: string, params?: Record<string, unknown>) => {
      if (path === "/api/documents") return { items: state.items, totalCount: state.items.length, page: 1, pageSize: 15, totalPages: 1 };
      if (path === "/api/documents/categories" || path === "/api/document-categories" || path === "/api/document-categories/template-options") return [];
      if (path.startsWith("/api/documents/")) {
        const document = state.items.find((item) => item.id === path.split("/").pop()) ?? template;
        return { document, versions: [{ id: "version-1", versionNumber: 4, fileName: document.fileName, fileSize: 4096, contentType: "application/pdf", uploadedAt: "2026-03-02T00:00:00Z", isCurrent: true }] };
      }
      if (path === "/api/template-deletion-requests" && params?.templateId === "template-1") {
        return { items: state.history, page: 1, pageSize: 20, totalCount: state.history.length };
      }
      return [];
    });
    state.post.mockReset().mockImplementation(async (path: string, body?: { templateId?: string; reason?: string }) => {
      if (path === "/api/template-deletion-requests") {
        const request = { ...currentTemplateRequest, templateId: body?.templateId, reason: body?.reason };
        state.history = [request];
        return request;
      }
      return undefined;
    });
  });

  it("gates requests by permission, requires a reason, refreshes history, and excludes compact and ordinary documents", async () => {
    const denied = renderFullCenter();
    await screen.findByRole("button", { name: /Approved Handbook/ });
    fireEvent.click(screen.getByRole("button", { name: /Approved Handbook/ }));
    await screen.findByText("Versions");
    expect(screen.queryByRole("button", { name: "Request deletion" })).not.toBeInTheDocument();
    denied.unmount();

    state.permissions = ["Document.View", "Document.Archive", "Document.TemplateDeletion.Request", "Document.TemplateDeletion.View"];
    const requestedCenter = renderFullCenter();
    await screen.findByRole("button", { name: /Approved Handbook/ });
    fireEvent.click(screen.getByRole("button", { name: /Approved Handbook/ }));
    fireEvent.click(await screen.findByRole("button", { name: "Request deletion" }));
    const submit = screen.getByRole("button", { name: "Submit request" });
    expect(submit).toBeDisabled();
    fireEvent.submit(submit.closest("form")!);
    expect(state.post).not.toHaveBeenCalledWith("/api/template-deletion-requests", expect.anything());

    fireEvent.change(screen.getByPlaceholderText("Explain why this approved template should be deleted…"), {
      target: { value: "Replaced by the current handbook." },
    });
    expect(submit).toBeEnabled();
    fireEvent.click(submit);
    await waitFor(() => expect(state.post).toHaveBeenCalledWith("/api/template-deletion-requests", {
      templateId: "template-1", reason: "Replaced by the current handbook.",
    }));
    await waitFor(() => expect(state.get.mock.calls.filter(([path]) => path === "/api/template-deletion-requests").length).toBeGreaterThan(1));
    await waitFor(() => expect(screen.getAllByText("Pending approval").length).toBeGreaterThanOrEqual(2));
    expect(screen.queryByRole("dialog", { name: "Request template deletion" })).not.toBeInTheDocument();
    requestedCenter.unmount();

    state.items = [ordinaryDocument];
    const ordinaryCenter = renderFullCenter();
    await screen.findByRole("button", { name: /Delivery Plan/ });
    fireEvent.click(screen.getByRole("button", { name: /Delivery Plan/ }));
    await screen.findByText("Versions");
    expect(screen.queryByRole("button", { name: "Request deletion" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Archive" })).toBeInTheDocument();
    ordinaryCenter.unmount();

    state.items = [template];
    const compact = renderCompactCenter();
    await screen.findByRole("button", { name: /Approved Handbook/ });
    fireEvent.click(screen.getByRole("button", { name: /Approved Handbook/ }));
    await screen.findByText("Versions");
    expect(screen.queryByRole("button", { name: "Request deletion" })).not.toBeInTheDocument();
    compact.unmount();
  });
});
