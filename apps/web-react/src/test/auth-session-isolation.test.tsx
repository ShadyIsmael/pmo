import * as React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthProvider, useAuth } from "../auth/AuthProvider";
import { RequirePermission } from "../auth/RequirePermission";
import { api, clearTokens, getClientSessionId, SessionChangedError, setManagementRequestContext, setTokens, type CurrentUser } from "../api/client";
import { createSessionQueryClient } from "../api/queryClient";
import { I18nProvider } from "../i18n/messages";

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => { resolve = done; });
  return { promise, resolve };
}

function response(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

const adminUser: CurrentUser = {
  id: "admin-user",
  email: "admin@example.test",
  firstName: "Admin",
  lastName: "User",
  fullName: "Admin User",
  authenticationSource: "Local",
  mustChangePassword: false,
  roles: [{ id: "admin-role", name: "SystemAdministrator" }],
  permissions: ["admin.read"],
};

const restrictedUser: CurrentUser = {
  ...adminUser,
  id: "restricted-user",
  email: "restricted@example.test",
  firstName: "Restricted",
  fullName: "Restricted User",
  roles: [{ id: "member-role", name: "Member" }],
  permissions: [],
};

afterEach(() => {
  clearTokens();
  window.localStorage.clear();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("authenticated session isolation", () => {
  it("discards a response that completes after another account becomes active", async () => {
    const pending = deferred<Response>();
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) => pending.promise);
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-user-a", "refresh-user-a");

    const oldRequest = api.get<{ privateValue: string }>("/api/private-data");
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get("Authorization")).toBe("Bearer access-user-a");

    setTokens("access-user-b", "refresh-user-b");
    pending.resolve(response({ privateValue: "user A secret" }));

    await expect(oldRequest).rejects.toBeInstanceOf(SessionChangedError);
    expect(window.localStorage.getItem("nexus_access_token")).toBe("access-user-b");
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("does not let a late 401 from the previous account refresh or clear the new account", async () => {
    const pending = deferred<Response>();
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) => pending.promise);
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-user-a", "refresh-user-a");

    const oldRequest = api.get("/api/private-data");
    setTokens("access-user-b", "refresh-user-b");
    pending.resolve(response({ message: "Expired" }, 401));

    await expect(oldRequest).rejects.toBeInstanceOf(SessionChangedError);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(window.localStorage.getItem("nexus_access_token")).toBe("access-user-b");
    expect(window.localStorage.getItem("nexus_refresh_token")).toBe("refresh-user-b");
  });

  it("discards requests issued under a management context that has since changed", async () => {
    const pending = deferred<Response>();
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) => pending.promise);
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-user-a", "refresh-user-a");
    const sessionId = getClientSessionId();
    expect(setManagementRequestContext({ perspective: "PortfolioManager", viewMode: "Native", portfolioId: "portfolio-a" }, sessionId)).toBe(true);

    const oldRequest = api.get("/api/private-data");
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get("X-Nexus-Portfolio-Id")).toBe("portfolio-a");
    expect(setManagementRequestContext({ perspective: "ProjectManager", viewMode: "Native", projectId: "project-b" }, sessionId)).toBe(true);
    pending.resolve(response({ privateValue: "old perspective data" }));

    await expect(oldRequest).rejects.toBeInstanceOf(SessionChangedError);
  });

  it("does not retry requests fenced by a session change or cancellation", () => {
    const queryClient = createSessionQueryClient();
    expect(queryClient.getDefaultOptions().mutations?.retry).toBe(false);
    queryClient.clear();
  });

  it("removes the prior user's protected view immediately on logout and never shows it to the next user", async () => {
    const logoutRequest = deferred<Response>();
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const requestUrl = String(input);
      if (requestUrl.endsWith("/api/auth/me")) {
        return new Headers(init?.headers).get("Authorization") === "Bearer access-user-a"
          ? response(adminUser)
          : response(restrictedUser);
      }
      if (requestUrl.endsWith("/api/auth/logout")) return logoutRequest.promise;
      if (requestUrl.endsWith("/api/auth/login")) {
        return response({ token: "access-user-b", refreshToken: "refresh-user-b", user: restrictedUser });
      }
      throw new Error(`Unexpected API request: ${requestUrl}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    function SessionControls() {
      const { user, status, logout, login } = useAuth();
      return (
        <div>
          <span data-testid="current-user">{user?.fullName ?? status}</span>
          <button onClick={() => void logout()}>Sign out</button>
          <button onClick={() => void login("restricted@example.test", "test-password")}>Sign in restricted</button>
        </div>
      );
    }

    setTokens("access-user-a", "refresh-user-a");
    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/private"]}>
          <AuthProvider>
            <SessionControls />
            <Routes>
              <Route path="/private" element={<RequirePermission permissions="admin.read"><div>admin-private-content</div></RequirePermission>} />
            </Routes>
          </AuthProvider>
        </MemoryRouter>
      </I18nProvider>
    );

    await screen.findByText("admin-private-content");
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));
    await waitFor(() => expect(screen.queryByText("admin-private-content")).not.toBeInTheDocument());
    expect(screen.getByTestId("current-user")).toHaveTextContent("unauthenticated");

    fireEvent.click(screen.getByRole("button", { name: "Sign in restricted" }));
    await waitFor(() => expect(screen.getByTestId("current-user")).toHaveTextContent("Restricted User"));
    expect(screen.queryByText("admin-private-content")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /admin/i })).not.toBeInTheDocument();

    logoutRequest.resolve(new Response(null, { status: 204 }));
  });
});
