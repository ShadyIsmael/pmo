import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { RisksPage } from "../features/risks/RisksPage";

const fixtures = vi.hoisted(() => ({ get: vi.fn(), permissions: ["Risk.View"] as string[] }));

vi.mock("@api/client", () => ({ api: { get: fixtures.get }, setApiErrorLocale: () => undefined }));
vi.mock("@auth/AuthProvider", () => ({ useAuth: () => ({ user: { permissions: fixtures.permissions } }) }));

function risk(id: string, title: string, ownerResourceId = "owner-1", ownerResourceName = "Risk Owner") {
  return {
    id,
    projectId: "project-1",
    projectName: "Delivery Project",
    title,
    description: "Risk description",
    riskLevel: "Critical",
    status: "ActionRequired",
    ownerResourceId,
    ownerResourceName,
    impactTypes: ["Schedule"],
    createdAt: "2026-01-01T00:00:00.000Z",
    updatedAt: "2026-01-02T00:00:00.000Z",
    rowVersion: [1],
  };
}

function renderRegister(initialEntry = "/risks-issues/risks") {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <MemoryRouter initialEntries={[initialEntry]}>
          <Routes><Route path="/risks-issues/risks" element={<RisksPage />} /></Routes>
        </MemoryRouter>
      </I18nProvider>
    </QueryClientProvider>,
  );
}

describe("risk register parity", () => {
  beforeEach(() => {
    window.localStorage.setItem("nexus-locale", "en");
    fixtures.permissions = ["Risk.View"];
    fixtures.get.mockReset().mockImplementation(async (url: string, params?: Record<string, string | number | boolean | undefined>) => {
      if (url === "/api/resources") {
        return { items: [{ id: "owner-1", name: "Risk Owner", email: "owner@example.com", jobTitle: "Manager", isActive: true }], page: 1, pageSize: 200, totalCount: 1, totalPages: 1 };
      }
      if (url === "/api/risks" && params?.ownerResourceId === "owner-1") {
        return { items: [risk("risk-owned", "Owner filtered risk")], totalCount: 1, totalPages: 1 };
      }
      if (url === "/api/risks" && params?.page === 1) {
        return { items: [risk("risk-first", "First page risk")], totalCount: 2, totalPages: 2 };
      }
      if (url === "/api/risks" && params?.page === 2) {
        return { items: [risk("risk-second", "Second page risk", "owner-2", "Another Owner")], totalCount: 2, totalPages: 2 };
      }
      throw new Error(`Unexpected request: ${url}`);
    });
  });

  it("loads every risk page while preserving the project context from the URL", async () => {
    renderRegister("/risks-issues/risks?projectId=project-1");

    expect(await screen.findByText("Second page risk")).toBeInTheDocument();
    await waitFor(() => {
      expect(fixtures.get).toHaveBeenCalledWith("/api/risks", { projectId: "project-1", page: 1, pageSize: 100 });
      expect(fixtures.get).toHaveBeenCalledWith("/api/risks", { projectId: "project-1", page: 2, pageSize: 100 });
    });
  });

  it("sends the selected owner to the server-side risk filter", async () => {
    renderRegister();
    await screen.findByText("First page risk");

    fireEvent.click(screen.getByRole("combobox", { name: "All owners" }));
    fireEvent.click(await screen.findByRole("option", { name: "Risk Owner" }));

    expect(await screen.findByText("Owner filtered risk")).toBeInTheDocument();
    await waitFor(() => expect(fixtures.get).toHaveBeenCalledWith("/api/risks", { ownerResourceId: "owner-1", page: 1, pageSize: 100 }));
  });

  it("localizes risk level and status options in Arabic", async () => {
    window.localStorage.setItem("nexus-locale", "ar");
    renderRegister();

    await screen.findByText("First page risk");

    expect(screen.getByRole("option", { name: "حرج" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "يتطلب إجراءً" })).toBeInTheDocument();
  });
});
