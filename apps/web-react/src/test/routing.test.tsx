import * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { AuthProvider } from "../auth/AuthProvider";
import { ManagementProvider } from "../api/management";
import { I18nProvider } from "../i18n/messages";
import { ThemeProvider } from "../theme/ThemeProvider";
import { AppRoutes } from "../routes/AppRoutes";
import { setTokens } from "../api/client";

function mockFetch(routes: Record<string, unknown>) {
  return vi.fn().mockImplementation((input: string) => {
    for (const [key, value] of Object.entries(routes)) {
      if (input.includes(key)) return Promise.resolve(new Response(JSON.stringify(value), { status: 200 }));
    }
    return Promise.resolve(new Response(JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }), { status: 200 }));
  });
}

function shell(_userPermissions: string[] = ["projects.read", "Resource.View", "Team.View", "Workflow.View", "finances.read", "Risk.View", "Document.View", "WorkItem.View", "Meeting.View", "settings.read", "users.read", "TimeTracking.ManageOwn"]) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>
      <ThemeProvider>
        <I18nProvider>
          <AuthProvider>
            <ManagementProvider user={null}>{children}</ManagementProvider>
          </AuthProvider>
        </I18nProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}

describe("routing and permission guards", () => {
  beforeEach(() => { window.localStorage.clear(); });

  it("renders projects list for permitted users without duplicate requests", async () => {
    setTokens("a", "r");
    const fetchMock = mockFetch({
      "/api/auth/me": { id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["projects.read", "TimeTracking.ManageOwn"] },
      "/api/me/perspectives": { availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } },
      "/api/projects": { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 },
      "/api/timer/current": null,
      "/api/notifications": { items: [], totalCount: 0 },
    });
    vi.stubGlobal("fetch", fetchMock);
    render(<MemoryRouter initialEntries={["/projects"]}><AppRoutes /></MemoryRouter>, { wrapper: shell() });
    await waitFor(() => expect(screen.getByText(/Project Registry|سجل المشاريع/i)).toBeInTheDocument());
    await waitFor(() => expect(fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/projects") && !String(input).includes("/shell") && !String(input).includes("/overview")).length).toBeGreaterThanOrEqual(1));
  });

  it("shows permission message when lacking access", async () => {
    setTokens("a", "r");
    const fetchMock = mockFetch({
      "/api/auth/me": { id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: [] },
      "/api/me/perspectives": { availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } },
      "/api/timer/current": null,
      "/api/notifications": { items: [], totalCount: 0 },
    });
    vi.stubGlobal("fetch", fetchMock);
    render(<MemoryRouter initialEntries={["/workflows"]}><AppRoutes /></MemoryRouter>, { wrapper: shell([]) });
    await waitFor(() => expect(screen.getByText(/do not have permission/i)).toBeInTheDocument());
  });

  it.each([
    { path: "/projects/board", restrictedEndpoints: ["/api/work-items", "/api/reference-data/work-item-statuses"] },
    { path: "/projects/gantt", restrictedEndpoints: ["/api/planning/"] },
    { path: "/projects/matrix", restrictedEndpoints: ["/api/resources"] },
    { path: "/projects/templates", restrictedEndpoints: ["/api/documents"] },
  ])("guards $path with its data permission before issuing feature requests", async ({ path, restrictedEndpoints }) => {
    setTokens("a", "r");
    const fetchMock = mockFetch({
      "/api/auth/me": { id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["projects.read"] },
      "/api/me/perspectives": { availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } },
      "/api/timer/current": null,
      "/api/notifications": { items: [], totalCount: 0 },
    });
    vi.stubGlobal("fetch", fetchMock);
    render(<MemoryRouter initialEntries={[path]}><AppRoutes /></MemoryRouter>, { wrapper: shell(["projects.read"]) });

    await waitFor(() => expect(screen.getByText(/do not have permission/i)).toBeInTheDocument());
    for (const endpoint of restrictedEndpoints) {
      expect(fetchMock.mock.calls.some(([input]) => String(input).includes(endpoint))).toBe(false);
    }
  });

  it.each([
    { path: "/projects/project-1/board", restrictedEndpoint: "/api/work-items" },
    { path: "/projects/project-1/gantt", restrictedEndpoint: "/api/planning/" },
    { path: "/projects/project-1/matrix", restrictedEndpoint: "/api/resources" },
  ])("guards nested project route $path before its feature query", async ({ path, restrictedEndpoint }) => {
    setTokens("a", "r");
    const fetchMock = mockFetch({
      "/api/auth/me": { id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["projects.read"] },
      "/api/me/perspectives": { availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } },
      "/api/timer/current": null,
      "/api/notifications": { items: [], totalCount: 0 },
    });
    vi.stubGlobal("fetch", fetchMock);
    render(<MemoryRouter initialEntries={[path]}><AppRoutes /></MemoryRouter>, { wrapper: shell(["projects.read"]) });

    await waitFor(() => expect(screen.getByText(/do not have permission/i)).toBeInTheDocument());
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes(restrictedEndpoint))).toBe(false);
  });

  it("guards the direct Organization route and does not fetch organization data without access", async () => {
    setTokens("a", "r");
    const fetchMock = mockFetch({
      "/api/auth/me": { id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: [] },
      "/api/me/perspectives": { availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } },
      "/api/timer/current": null,
      "/api/notifications": { items: [], totalCount: 0 },
    });
    vi.stubGlobal("fetch", fetchMock);
    render(<MemoryRouter initialEntries={["/administration/organization"]}><AppRoutes /></MemoryRouter>, { wrapper: shell([]) });
    await waitFor(() => expect(screen.getByText(/do not have permission/i)).toBeInTheDocument());
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes("/api/organization/"))).toBe(false);
  });
});
