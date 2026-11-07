import { QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Routes, Route, Outlet } from "react-router-dom";
import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n/messages";
import { AuthProvider } from "../auth/AuthProvider";
import { ManagementProvider } from "../api/management";
import { ProjectDetailPage } from "../features/projects/ProjectDetailPage";
import { ProjectOverviewTab } from "../features/projects/ProjectOverviewTab";
import { setTokens } from "../api/client";
import { createSessionQueryClient } from "../api/queryClient";

describe("visible-first loading", () => {
  it("fetches project shell once and defers overview until the tab mounts", async () => {
    setTokens("a");
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      if (input.includes("/api/auth/me")) {
        return Promise.resolve(new Response(JSON.stringify({ id: "u", email: "a@b.c", firstName: "A", lastName: "B", fullName: "A B", roles: [], permissions: ["Project.View"] }), { status: 200 }));
      }
      if (input.includes("/api/me/perspectives")) {
        return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
      }
      if (input.includes("/api/projects/p1/shell")) return Promise.resolve(new Response(JSON.stringify({ id: "p1", code: "P1", name: "Project 1", status: "Active", progress: 10, health: "Green", currency: "USD" }), { status: 200 }));
      if (input.includes("/api/projects/p1/overview")) return Promise.resolve(new Response(JSON.stringify({ risks: { openCount: 0, highCriticalOpenCount: 0, topRisks: [] }, issues: { openIssueCount: 0, overdueOpenIssueCount: 0, topIssues: [] }, milestones: [], pendingChangeRequestCount: 0, pendingHandoverCount: 0, allocations: { activeAllocationCount: 0, activeResourceCount: 0, allocatedHoursPerWeek: 0 }, meetings: { meetingCount: 0, finalMinutesCount: 0 }, statusReports: { totalCount: 0, publishedCount: 0 } }), { status: 200 }));
      return Promise.resolve(new Response("null", { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);
    const client = createSessionQueryClient();
    render(
      <QueryClientProvider client={client}>
        <I18nProvider>
          <AuthProvider>
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
          </AuthProvider>
        </I18nProvider>
      </QueryClientProvider>
    );
    await waitFor(() => expect(screen.getByText("Project 1")).toBeInTheDocument());
    const shells = fetchMock.mock.calls.filter(([input]) => String(input).includes("/api/projects/p1/shell"));
    expect(shells).toHaveLength(1);
    // Overview loads because its tab is the visible route…
    const overviewCalls = () => fetchMock.mock.calls.filter(([input]) =>
      new URL(String(input), window.location.origin).pathname === "/api/projects/p1/overview",
    );
    await waitFor(() => expect(overviewCalls()).toHaveLength(1));
    // …and hidden tabs (team/people) are never fetched.
    expect(fetchMock.mock.calls.filter(([input]) =>
      /\/api\/projects\/p1\/(people|team)$/.test(new URL(String(input), window.location.origin).pathname),
    )).toHaveLength(0);
  });
});



