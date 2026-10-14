import * as React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { AuthProvider } from "../auth/AuthProvider";
import { ManagementProvider } from "../api/management";
import { setTokens } from "../api/client";
import { I18nProvider } from "../i18n/messages";
import { ThemeProvider } from "../theme/ThemeProvider";
import { AppRoutes } from "../routes/AppRoutes";
import { DirectoryAccessPermissions, Permissions } from "../lib/permissions";

describe("Directory Integration permission access", () => {
  beforeEach(() => { window.localStorage.clear(); });

  it("allows a narrowly-scoped mapping permission to open the directory integration without broad Settings access", async () => {
    const permission = Permissions.Directory.ManageMappings;
    expect(DirectoryAccessPermissions).toContain(permission);
    setTokens("access", "refresh");
    const fetchMock = vi.fn().mockImplementation((input: string) => {
      const url = String(input);
      if (url.includes("/api/auth/me")) {
        return Promise.resolve(new Response(JSON.stringify({
          id: "user-1", tenantId: "tenant-1", email: "user@example.test", firstName: "Test", lastName: "User", fullName: "Test User", roles: [], permissions: [permission],
        }), { status: 200 }));
      }
      if (url.includes("/api/directory-integrations/connections")) return Promise.resolve(new Response("[]", { status: 200 }));
      if (url.includes("/api/me/perspectives")) return Promise.resolve(new Response(JSON.stringify({ availablePerspectives: [], scope: { portfolioIds: [], programIds: [], projectIds: [] } }), { status: 200 }));
      if (url.includes("/api/timer/current")) return Promise.resolve(new Response("null", { status: 200 }));
      if (url.includes("/api/notifications")) return Promise.resolve(new Response(JSON.stringify({ items: [], totalCount: 0 }), { status: 200 }));
      return Promise.resolve(new Response(JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }), { status: 200 }));
    });
    vi.stubGlobal("fetch", fetchMock);

    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    const Wrapper = ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={client}>
        <ThemeProvider>
          <I18nProvider>
            <AuthProvider><ManagementProvider user={null}>{children}</ManagementProvider></AuthProvider>
          </I18nProvider>
        </ThemeProvider>
      </QueryClientProvider>
    );

    render(<MemoryRouter initialEntries={["/administration/integrations/directory"]}><AppRoutes /></MemoryRouter>, { wrapper: Wrapper });
    await waitFor(() => expect(screen.getAllByText(/No directory connections are configured yet/).length).toBeGreaterThan(0));
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes("/api/directory-integrations/connections"))).toBe(true);
  });
});
