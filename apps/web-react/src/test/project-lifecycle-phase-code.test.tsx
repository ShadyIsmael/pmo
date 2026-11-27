import * as React from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ProjectLifecycleCatalogPage } from "../features/administration/ProjectLifecycleCatalogPage";

type PhaseFixture = {
  id: string;
  tenantId: string;
  organizationId: string | null;
  code: string;
  nameEn: string;
  nameAr: string;
  descriptionEn: string | null;
  descriptionAr: string | null;
  displayOrder: number;
  isDefault: boolean;
  isActive: boolean;
  isContinuousActivity: boolean;
  referencedProjectCount: number;
  rowVersion: string;
};

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  phases: [] as PhaseFixture[],
  permissions: ["ProjectLifecyclePhase.View", "ProjectLifecyclePhase.Create", "ProjectLifecyclePhase.Edit"] as string[],
}));

vi.mock("@api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@api/client")>();
  return { ...actual, api: { ...actual.api, get: mocks.get, post: mocks.post, put: mocks.put } };
});
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { id: "lifecycle-admin", permissions: mocks.permissions } }) }));
vi.mock("@api/management", () => ({ useManagement: () => ({ canMutate: true, loading: false, snapshot: { scope: { organizationIds: ["org-a"] }, selectedContext: null } }) }));
vi.mock("../features/administration/api/settings", () => ({
  useOrganizationProfiles: () => ({ data: [{ id: "org-a", name: "Alpha Organization", canManageSettings: false }], isLoading: false }),
}));
vi.mock("@components/ui/ConfirmProvider", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@components/ui/ConfirmProvider")>();
  return { ...actual, useConfirm: () => vi.fn().mockResolvedValue(true) };
});

const createdPhase: PhaseFixture = {
  id: "phase-new",
  tenantId: "tenant-1",
  organizationId: "org-a",
  code: "PH-000001",
  nameEn: "Delivery",
  nameAr: "التنفيذ",
  descriptionEn: null,
  descriptionAr: null,
  displayOrder: 10,
  isDefault: false,
  isActive: true,
  isContinuousActivity: false,
  referencedProjectCount: 0,
  rowVersion: "created-row-version",
};

function renderPage() {
  window.localStorage.setItem("nexus-locale", "en");
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><I18nProvider><MemoryRouter><ProjectLifecycleCatalogPage /></MemoryRouter></I18nProvider></QueryClientProvider>);
}

describe("Project Phase generated Code UI", () => {
  beforeEach(() => {
    mocks.phases = [];
    mocks.permissions = ["ProjectLifecyclePhase.View", "ProjectLifecyclePhase.Create", "ProjectLifecyclePhase.Edit"];
    mocks.get.mockReset().mockImplementation(async (path: string) => path === "/api/project-lifecycle/phases" ? [...mocks.phases] : []);
    mocks.post.mockReset().mockImplementation(async (_path: string, input: Record<string, unknown>) => {
      const result = { ...createdPhase, ...input } as PhaseFixture;
      mocks.phases = [result];
      return result;
    });
    mocks.put.mockReset().mockImplementation(async (_path: string, input: Record<string, unknown>) => {
      const existing = mocks.phases[0];
      const result = { ...existing, ...input, code: existing.code, rowVersion: "updated-row-version" } as PhaseFixture;
      mocks.phases = [result];
      return result;
    });
  });

  it("shows a disabled Code field on creation and displays the server Code in the catalog", async () => {
    renderPage();
    await screen.findByText("No catalog entries are configured.");
    fireEvent.click(screen.getByRole("button", { name: "Add entry" }));

    const sheet = screen.getByRole("dialog", { name: "Add entry" });
    const codeField = within(sheet).getByLabelText("Stable code");
    expect(codeField).toBeDisabled();
    expect(codeField).toHaveValue("");
    fireEvent.change(within(sheet).getByLabelText("Name (English)"), { target: { value: "Delivery" } });
    fireEvent.change(within(sheet).getByLabelText("Name (Arabic)"), { target: { value: "التنفيذ" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save entry" }));

    await waitFor(() => expect(mocks.post).toHaveBeenCalledTimes(1));
    const [url, payload] = mocks.post.mock.calls[0] as [string, Record<string, unknown>];
    expect(url).toBe("/api/project-lifecycle/phases");
    expect(payload).not.toHaveProperty("code");
    expect(await screen.findByText("PH-000001")).toBeInTheDocument();
  });

  it("shows the existing Code read-only and omits it when editing phase names", async () => {
    mocks.phases = [{ ...createdPhase }];
    renderPage();
    await screen.findByText("Delivery");
    fireEvent.click(screen.getByRole("button", { name: "Edit entry Delivery" }));

    const sheet = screen.getByRole("dialog", { name: "Edit entry" });
    const codeField = within(sheet).getByLabelText("Stable code");
    expect(codeField).toHaveValue("PH-000001");
    expect(codeField).toBeDisabled();
    fireEvent.change(within(sheet).getByLabelText("Name (English)"), { target: { value: "Delivery Updated" } });
    fireEvent.click(within(sheet).getByRole("button", { name: "Save entry" }));

    await waitFor(() => expect(mocks.put).toHaveBeenCalledTimes(1));
    const [url, payload] = mocks.put.mock.calls[0] as [string, Record<string, unknown>];
    expect(url).toBe("/api/project-lifecycle/phases/phase-new");
    expect(payload).not.toHaveProperty("code");
    expect(await screen.findByText("Delivery Updated")).toBeInTheDocument();
    expect(screen.getByText("PH-000001")).toBeInTheDocument();
  });
});
