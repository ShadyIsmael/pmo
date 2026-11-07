import * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, useLocation } from "react-router-dom";
import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../auth/AuthProvider";
import { setTokens } from "../api/client";
import { ManagementProvider } from "../api/management";
import { I18nProvider } from "../i18n/messages";
import { ThemeProvider } from "../theme/ThemeProvider";
import { AppRoutes } from "../routes/AppRoutes";

vi.mock("../features/projects/ProjectDetailPage", async () => {
  const { Outlet } = await import("react-router-dom");
  return { ProjectDetailPage: () => <div data-testid="project-detail-shell"><Outlet /></div> };
});

vi.mock("../features/projects/ProjectOverviewTab", () => ({
  ProjectOverviewTab: () => <div>Project overview screen</div>,
}));

vi.mock("../features/projects/ProjectReportingConfigurationPage", () => ({
  ProjectReportingConfigurationPage: () => <div>Project reporting configuration</div>,
}));

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="current-location">{location.pathname}{location.search}</output>;
}

function wrapper() {
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

function setup(path: string, permissions: string[]) {
  setTokens("access");
  window.localStorage.setItem("nexus-locale", "en");
  const fetchMock = vi.fn().mockImplementation((input: string) => {
    if (input.includes("/api/auth/me")) {
      return Promise.resolve(new Response(JSON.stringify({
        id: "user-1", email: "user@example.com", firstName: "User", lastName: "One", fullName: "User One", roles: [], permissions,
      }), { status: 200 }));
    }
    if (input.includes("/api/me/perspectives")) {
      return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
    }
    if (input.includes("/api/timer/current") || input.includes("/api/notifications")) {
      return Promise.resolve(new Response("null", { status: 200 }));
    }
    return Promise.resolve(new Response(JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }), { status: 200 }));
  });
  vi.stubGlobal("fetch", fetchMock);

  render(
    <MemoryRouter initialEntries={[path]}>
      <LocationProbe />
      <AppRoutes />
    </MemoryRouter>,
    { wrapper: wrapper() },
  );
  return { fetchMock };
}

describe("Project Settings navigation compatibility", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it("opens Settings from an existing project settings deep link", async () => {
    setup("/projects/project-42/settings", ["Project.View", "ProjectSettings.View", "ProjectAnnualBudget.View"]);

    expect(await screen.findByText("Project overview screen")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId("current-location")).toHaveTextContent("/projects/project-42/overview?openSettings=1"));
    expect(screen.getByTestId("project-detail-shell")).toBeInTheDocument();
  });

  it.each([
    "/projects/project-42/workflows",
    "/projects/project-42/settings/workflows",
  ])("safely redirects the legacy Project Workflows link %s into Settings", async (path) => {
    setup(path, ["Project.View", "ProjectSettings.View"]);

    expect(await screen.findByText("Project overview screen")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId("current-location")).toHaveTextContent("/projects/project-42/overview?openSettings=1"));
    expect(screen.getByTestId("project-detail-shell")).toBeInTheDocument();
  });

  it.each([
    "/projects/project-42/workflows",
    "/projects/project-42/settings/workflows",
  ])("does not redirect a legacy Project Workflows link without ProjectSettings.View (%s)", async (path) => {
    setup(path, ["Project.View"]);

    expect(await screen.findByRole("alert")).toHaveTextContent(/permission/i);
    expect(screen.getByTestId("current-location")).toHaveTextContent(path);
    expect(screen.queryByText("Project overview screen")).not.toBeInTheDocument();
  });

  it("keeps the legacy deep link behind the same settings view permission", async () => {
    setup("/projects/project-42/settings", ["Project.View"]);

    expect(await screen.findByRole("alert")).toHaveTextContent(/permission/i);
    expect(screen.getByTestId("current-location")).toHaveTextContent("/projects/project-42/settings");
    expect(screen.queryByText("Project overview screen")).not.toBeInTheDocument();
  });

  it("keeps project reporting configuration on its existing route", async () => {
    setup("/projects/project-42/settings/reporting", ["Project.View", "StatusReport.ManageConfiguration"]);

    expect(await screen.findByText("Project reporting configuration")).toBeInTheDocument();
    expect(screen.getByTestId("current-location")).toHaveTextContent("/projects/project-42/settings/reporting");
  });
});
