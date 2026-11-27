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
  window.sessionStorage.clear();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

function expectNoBrowserTokenStorage(): void {
  for (const storage of [window.localStorage, window.sessionStorage]) {
    expect(storage.getItem("nexus_access_token")).toBeNull();
    expect(storage.getItem("nexus_refresh_token")).toBeNull();
  }
}

describe("authenticated session isolation", () => {
  it("discards a response that completes after another account becomes active", async () => {
    const pending = deferred<Response>();
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) => pending.promise);
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-user-a");

    const oldRequest = api.get<{ privateValue: string }>("/api/private-data");
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get("Authorization")).toBe("Bearer access-user-a");

    setTokens("access-user-b");
    pending.resolve(response({ privateValue: "user A secret" }));

    await expect(oldRequest).rejects.toBeInstanceOf(SessionChangedError);
    expectNoBrowserTokenStorage();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("does not let a late 401 from the previous account refresh or clear the new account", async () => {
    const pending = deferred<Response>();
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) => pending.promise);
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-user-a");

    const oldRequest = api.get("/api/private-data");
    setTokens("access-user-b");
    pending.resolve(response({ message: "Expired" }, 401));

    await expect(oldRequest).rejects.toBeInstanceOf(SessionChangedError);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expectNoBrowserTokenStorage();
  });

  it("discards requests issued under a management context that has since changed", async () => {
    const pending = deferred<Response>();
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) => pending.promise);
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-user-a");
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

  it("keeps bearer credentials in memory and stores only the session coordination id", async () => {
    const fetchMock = vi.fn(async (_input: RequestInfo | URL, _init?: RequestInit) => response({ ok: true }));
    vi.stubGlobal("fetch", fetchMock);

    setTokens("access-memory-only");
    const sessionId = getClientSessionId();
    await api.get("/api/private-data");

    expect(new URL(String(fetchMock.mock.calls[0][0])).origin).toBe(window.location.origin);
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get("Authorization")).toBe("Bearer access-memory-only");
    expect(window.localStorage.getItem("nexus_auth_session_id")).toBe(sessionId);
    expectNoBrowserTokenStorage();
  });

  it("gets a CSRF token before login and never returns a refresh secret to JavaScript", async () => {
    const loginResponse = {
      token: "access-after-login",
      expiresAt: "2026-11-06T20:00:00Z",
      user: adminUser,
      passwordChangeRequired: false,
    };
    const fetchMock = vi.fn(async (input: RequestInfo | URL, _init?: RequestInit) => {
      const requestUrl = String(input);
      if (requestUrl.endsWith("/api/auth/csrf")) return response({ csrfToken: "csrf-test-token" });
      if (requestUrl.endsWith("/api/auth/login")) return response(loginResponse);
      throw new Error(`Unexpected API request: ${requestUrl}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    const auth = await api.login("admin@example.test", "test-password");

    const csrfRequest = fetchMock.mock.calls[0];
    expect(String(csrfRequest[0])).toMatch(/\/api\/auth\/csrf$/);
    expect(csrfRequest[1]?.credentials).toBe("include");
    const loginRequest = fetchMock.mock.calls[1];
    expect(String(loginRequest[0])).toMatch(/\/api\/auth\/login$/);
    expect(loginRequest[1]?.credentials).toBe("include");
    const loginHeaders = new Headers(loginRequest[1]?.headers);
    expect(loginHeaders.get("X-Nexus-CSRF")).toBe("csrf-test-token");
    expect(loginHeaders.get("X-Nexus-Auth-Mode")).toBe("cookie");
    expect(JSON.parse(String(loginRequest[1]?.body))).not.toHaveProperty("refreshToken");
    expect(auth.token).toBe("access-after-login");
    expect(auth).not.toHaveProperty("refreshToken");
    expectNoBrowserTokenStorage();
  });

  it("refreshes with the HttpOnly cookie and CSRF header without sending a refresh token in JSON", async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const requestUrl = String(input);
      if (requestUrl.endsWith("/api/private-data")) {
        const bearer = new Headers(init?.headers).get("Authorization");
        return bearer === "Bearer access-after-refresh"
          ? response({ ok: true })
          : response({ message: "Expired" }, 401);
      }
      if (requestUrl.endsWith("/api/auth/csrf")) return response({ csrfToken: "csrf-refresh-token" });
      if (requestUrl.endsWith("/api/auth/refresh")) return response({
        token: "access-after-refresh",
        expiresAt: "2026-11-06T20:00:00Z",
        user: adminUser,
        passwordChangeRequired: false,
      });
      throw new Error(`Unexpected API request: ${requestUrl}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    setTokens("access-expired");
    await expect(api.get("/api/private-data")).resolves.toEqual({ ok: true });

    const refreshRequest = fetchMock.mock.calls.find(([input]) => String(input).endsWith("/api/auth/refresh"));
    expect(refreshRequest).toBeDefined();
    const refreshInit = refreshRequest![1];
    expect(refreshInit?.credentials).toBe("include");
    const refreshHeaders = new Headers(refreshInit?.headers);
    expect(refreshHeaders.get("X-Nexus-CSRF")).toBe("csrf-refresh-token");
    expect(refreshHeaders.get("X-Nexus-Auth-Mode")).toBe("cookie");
    expect(JSON.parse(String(refreshInit?.body))).toEqual({});
    expect(new Headers(refreshInit?.headers).has("Authorization")).toBe(false);
    expectNoBrowserTokenStorage();
  });

  it("refreshes frontend permissions once per session after repeated protected API 403 responses", async () => {
    const initiallyAllowedUser = { ...adminUser, permissions: ["Project.Edit"] };
    const revokedUser = { ...restrictedUser, permissions: [] };
    let meRequests = 0;
    let deniedRequests = 0;
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const requestUrl = String(input);
      if (requestUrl.endsWith("/api/auth/csrf")) return response({ csrfToken: "csrf-session-token" });
      if (requestUrl.endsWith("/api/auth/me")) {
        meRequests += 1;
        return response(meRequests === 1 ? initiallyAllowedUser : revokedUser);
      }
      if (requestUrl.endsWith("/api/protected-edit")) {
        deniedRequests += 1;
        return response({ error: "Permission revoked" }, 403);
      }
      throw new Error(`Unexpected API request: ${requestUrl}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    function AuthorizationProbe() {
      const { user } = useAuth();
      return (
        <>
          {user?.permissions.includes("Project.Edit") ? <span>edit-enabled</span> : null}
          <button onClick={() => {
            void Promise.all(Array.from({ length: 21 }, () => api.get("/api/protected-edit").catch(() => undefined)));
          }}>Request edit</button>
        </>
      );
    }

    setTokens("access-admin");
    render(<AuthProvider><AuthorizationProbe /></AuthProvider>);

    await screen.findByText("edit-enabled");
    fireEvent.click(screen.getByRole("button", { name: "Request edit" }));
    await waitFor(() => expect(screen.queryByText("edit-enabled")).not.toBeInTheDocument());
    await waitFor(() => expect(deniedRequests).toBe(21));
    expect(meRequests).toBe(2);

    fireEvent.click(screen.getByRole("button", { name: "Request edit" }));
    await waitFor(() => expect(deniedRequests).toBe(42));
    expect(meRequests).toBe(2);
  });

  it("removes the prior user's protected view immediately on logout and never shows it to the next user", async () => {
    const logoutRequest = deferred<Response>();
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const requestUrl = String(input);
      if (requestUrl.endsWith("/api/auth/csrf")) return response({ csrfToken: "csrf-session-token" });
      if (requestUrl.endsWith("/api/auth/me")) {
        return new Headers(init?.headers).get("Authorization") === "Bearer access-user-a"
          ? response(adminUser)
          : response(restrictedUser);
      }
      if (requestUrl.endsWith("/api/auth/logout")) return logoutRequest.promise;
      if (requestUrl.endsWith("/api/auth/login")) {
        return response({ token: "access-user-b", user: restrictedUser });
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

    setTokens("access-user-a");
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

    const logoutCall = fetchMock.mock.calls.find(([input]) => String(input).endsWith("/api/auth/logout"));
    expect(logoutCall).toBeDefined();
    const logoutHeaders = new Headers(logoutCall![1]?.headers);
    expect(logoutHeaders.get("X-Nexus-Auth-Mode")).toBe("cookie");
    expect(logoutHeaders.get("X-Nexus-CSRF")).toBe("csrf-session-token");
    expect(logoutCall![1]?.credentials).toBe("include");

    const loginCall = fetchMock.mock.calls.find(([input]) => String(input).endsWith("/api/auth/login"));
    expect(loginCall).toBeDefined();
    expect(new Headers(loginCall![1]?.headers).get("X-Nexus-Auth-Mode")).toBe("cookie");

    logoutRequest.resolve(new Response(null, { status: 204 }));
  });

  it("does not repeat /api/auth/me on focus after an invalid session", async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const requestUrl = String(input);
      if (requestUrl.endsWith("/api/auth/csrf")) return response({ csrfToken: "csrf-token" });
      if (requestUrl.endsWith("/api/auth/refresh")) return response({ error: "refresh failed" }, 401);
      if (requestUrl.endsWith("/api/auth/me")) return response({ error: "Unauthorized" }, 401);
      throw new Error(`Unexpected API request: ${requestUrl}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    // Simulate a page reload with a stored session but no valid access token in memory
    setTokens("stored-access-token");

    render(<AuthProvider><div data-testid="app">loading</div></AuthProvider>);

    // Restoration should call /api/auth/me exactly once (Strict Mode single-flight)
    const meCalls = fetchMock.mock.calls.filter(([input]) => String(input).endsWith("/api/auth/me"));
    await waitFor(() => expect(meCalls.length).toBe(1));
    expect(meCalls.length).toBe(1);

    // Focus / timer should not trigger another /api/auth/me when the session is known invalid
    fireEvent.focus(window);
    await new Promise((resolve) => setTimeout(resolve, 100));
    expect(fetchMock.mock.calls.filter(([input]) => String(input).endsWith("/api/auth/me")).length).toBe(1);
  });
});
