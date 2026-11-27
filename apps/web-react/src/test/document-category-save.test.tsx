import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { I18nProvider } from "../i18n/messages";
import { ApiError } from "../api/client";
import { getLocalizedApiErrorMessage } from "../api/api-error-localization";
import { DocumentCategoriesPage } from "../features/administration/DocumentCategoriesPage";

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
  confirm: vi.fn(),
  permissions: ["DocumentCategory.View", "DocumentCategory.Create"] as string[],
}));

vi.mock("@api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@api/client")>();
  return { ...actual, api: { get: mocks.get, post: mocks.post, put: mocks.put, patch: mocks.patch, delete: mocks.delete } };
});
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "category-admin", permissions: mocks.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, loading: false, snapshot: {
  scope: { organizationIds: ["org-a"], organizationReferences: [{ id: "org-a", name: "Alpha Organization" }] },
  selectedContext: null,
} }) }));
vi.mock("@components/ui/ConfirmProvider", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@components/ui/ConfirmProvider")>();
  return { ...actual, useConfirm: () => mocks.confirm };
});

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><I18nProvider><MemoryRouter><DocumentCategoriesPage /></MemoryRouter></I18nProvider></QueryClientProvider>);
}

describe("Document Categories Save integration", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    mocks.get.mockReset().mockResolvedValue([]);
    mocks.post.mockReset().mockResolvedValue({ id: "category-new", organizationId: "org-a", nameEn: "Governance", nameAr: "الحوكمة", isActive: true });
    mocks.put.mockReset().mockResolvedValue({});
    mocks.patch.mockReset().mockResolvedValue({});
    mocks.delete.mockReset().mockResolvedValue(undefined);
    mocks.confirm.mockReset().mockResolvedValue(true);
    mocks.permissions = ["DocumentCategory.View", "DocumentCategory.Create"];
  });

  it("posts the selected organization and closes the form after the category is saved", async () => {
    renderPage();
    fireEvent.change(screen.getByRole("combobox", { name: "Organization" }), { target: { value: "org-a" } });
    fireEvent.click(screen.getByRole("button", { name: "Create category" }));
    const sheet = screen.getByRole("dialog", { name: "Create category" });
    fireEvent.change(within(sheet).getByLabelText("English name"), { target: { value: "  Governance  " } });
    fireEvent.change(within(sheet).getByLabelText("Arabic name"), { target: { value: " الحوكمة " } });
    fireEvent.change(within(sheet).getByLabelText("Description"), { target: { value: "  Governance documents  " } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save category" }));

    await waitFor(() => expect(mocks.post).toHaveBeenCalledWith("/api/document-categories", {
      organizationId: "org-a",
      nameEn: "Governance",
      nameAr: "الحوكمة",
      description: "Governance documents",
    }));
    await waitFor(() => expect(screen.queryByRole("dialog", { name: "Create category" })).not.toBeInTheDocument());
  });

  it("shows Delete only to authorized users", async () => {
    mocks.get.mockResolvedValue([{ id: "category-a", organizationId: "org-a", nameEn: "Policies", nameAr: "السياسات", isActive: true, usageCount: 0, canManage: true }]);
    renderPage();

    expect(await screen.findByText("Policies")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Delete category Policies" })).not.toBeInTheDocument();
  });

  it("confirms an authorized deletion and refreshes the category list", async () => {
    mocks.permissions = ["DocumentCategory.View", "DocumentCategory.Delete"];
    mocks.get.mockResolvedValueOnce([{ id: "category-a", organizationId: "org-a", nameEn: "Policies", nameAr: "السياسات", isActive: true, usageCount: 0, canManage: true }])
      .mockResolvedValueOnce([]);
    renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Delete category Policies" }));

    await waitFor(() => expect(mocks.confirm).toHaveBeenCalledWith(expect.objectContaining({
      title: "Delete category",
      description: 'Permanently delete "Policies"? Only unused categories can be deleted. Referenced categories and their records are kept; deactivate a category that is in use.',
      confirmLabel: "Delete category",
      destructive: true,
    })));
    await waitFor(() => expect(mocks.delete).toHaveBeenCalledWith("/api/document-categories/category-a"));
    await waitFor(() => expect(screen.queryByText("Policies")).not.toBeInTheDocument());
  });

  it.each([
    ["en", "This category is still used by documents, templates, versions, project records, or phase requirements. Deactivate it instead of deleting it."],
    ["ar", "لا تزال هذه الفئة مستخدمة في مستندات أو قوالب أو إصدارات أو سجلات مشاريع أو متطلبات المراحل. ألغِ تفعيلها بدلًا من حذفها."],
  ] as const)("localizes a referenced-category 409 in %s", async (locale, message) => {
    window.localStorage.setItem("nexus-locale", locale);
    mocks.permissions = ["DocumentCategory.View", "DocumentCategory.Delete"];
    mocks.get.mockResolvedValue([{ id: "category-a", organizationId: "org-a", nameEn: "Policies", nameAr: "السياسات", isActive: true, usageCount: 0, canManage: true }]);
    mocks.delete.mockRejectedValueOnce(new ApiError(409,
      getLocalizedApiErrorMessage("DOCUMENT_CATEGORY_REFERENCED", 409, locale),
      "DOCUMENT_CATEGORY_REFERENCED"));
    renderPage();

    fireEvent.click(await screen.findByRole("button", { name: locale === "ar" ? "حذف الفئة السياسات" : "Delete category Policies" }));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent(message));
  });
});
