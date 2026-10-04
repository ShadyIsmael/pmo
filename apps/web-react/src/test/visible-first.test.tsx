import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Routes, Route, Outlet } from "react-router-dom";
import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { ManagementProvider } from "../api/management";
import { ProjectDetailPage } from "../features/projects/ProjectDetailPage";
import { ProjectOverviewTab } from "../features/projects/ProjectOverviewTab";
import { setTokens } from "../api/client";

describe("visible-first loading", () => {
  it("fetches project shell once and defers overview until the tab mounts", async () => {
    setTokens("a", "r");
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/projects/p1/shell")) return Promise.resolve(new Response(JSON.stringify({ id: "p1", code: "P1", name: "Project 1", progress: 10, health: "Green", currency: "USD" }), { status: 200 }));
      if (input.includes("/api/projects/p1/overview")) return Promise.resolve(new Response(JSON.stringify({ progress: 10, health: "Green", openRisks: 0, openIssues: 0 }), { status: 200 }));
      return Promise.resolve(new Response("null", { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    render(
      <QueryClientProvider client={client}>
        <I18nProvider>
          <ManagementProvider user={null}>
          <MemoryRouter initialEntries={["/projects/p1/overview"]}>
            <Routes>
              <Route path="/projects/:id" element={<ProjectDetailPage />}>
                <Route path="overview" element={<ProjectOverviewTab />} />
              </Route>
              <Route path="*" element={<Outlet />} />
            </Routes>
          </MemoryRouter>
          </ManagementProvider>
        </I18nProvider>
      </QueryClientProvider>
    );
    await waitFor(() => expect(screen.getByText("Project 1")).toBeInTheDocument());
    const shells = fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/projects/p1/shell"));
    expect(shells).toHaveLength(1);
    // Overview loads because its tab is the visible route…
    await waitFor(() => expect(fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/projects/p1/overview"))).toHaveLength(1));
    // …and hidden tabs (team/people) are never fetched.
    expect(fetchMock.mock.calls.filter(([input]) => String(input).includes("/people") || String(input).includes("/team"))).toHaveLength(0);
  });
});
