import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { I18nProvider } from "../i18n/messages";
import { DocumentCategoriesPage } from "../features/administration/DocumentCategoriesPage";

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  confirm: vi.fn(),
}));

vi.mock("@api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@api/client")>();
  return { ...actual, api: { get: mocks.get, post: mocks.post, put: mocks.put, patch: mocks.patch } };
});
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "category-admin", permissions: ["DocumentCategory.View", "DocumentCategory.Create"] } }) }));
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
    mocks.confirm.mockReset().mockResolvedValue(true);
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
});
