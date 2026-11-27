import * as React from "react";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthProvider, useAuth } from "../auth/AuthProvider";
import { RequireAuth } from "../auth/RequireAuth";
import { clearTokens, setTokens, type CurrentUser } from "../api/client";
import { I18nProvider } from "../i18n/messages";
import { LoginPage } from "../features/home/LoginPage";

function response(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

function pathname(input: RequestInfo | URL): string {
  return new URL(String(input), window.location.origin).pathname;
}

const adminUser: CurrentUser = {
  id: "admin-user",
  email: "admin@example.test",
  firstName: "Admin",
  lastName: "User",
  fullName: "Admin User",
  authenticationSource: "Local",
  mustChangePassword: false,
  roles: [{ id: "admin-role", name: "Tenant Administrator" }],
  permissions: ["Dashboard.View"],
};

function AuthState() {
  const { status, user } = useAuth();
  return <output data-testid="auth-state">{status}{user ? `:${user.fullName}` : ""}</output>;
}

function renderProtectedApp(initialPath = "/private", strict = false) {
  const app = (
    <I18nProvider>
      <MemoryRouter initialEntries={[initialPath]}>
        <AuthProvider>
          <AuthState />
          <Routes>
            <Route path="/private" element={<RequireAuth><div>authorized-dashboard</div></RequireAuth>} />
            <Route path="/login" element={<div>login-screen</div>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </I18nProvider>
  );
  return render(strict ? <React.StrictMode>{app}</React.StrictMode> : app);
}

afterEach(() => {
  clearTokens();
  window.localStorage.clear();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe("authentication initialization", () => {
  it("restores a valid in-memory session and opens the authorized dashboard", async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, _init?: RequestInit) => {
      if (pathname(input) === "/api/auth/me") return response(adminUser);
      throw new Error(`Unexpected API request: ${pathname(input)}`);
    });
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-valid");

    renderProtectedApp();

    expect(await screen.findByText("authorized-dashboard")).toBeInTheDocument();
    expect(screen.getByTestId("auth-state")).toHaveTextContent("authenticated:Admin User");
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get("Authorization")).toBe("Bearer access-valid");
  });

  it("restores a valid session through the HttpOnly refresh-cookie flow after /me returns 401", async () => {
    let meRequests = 0;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = pathname(input);
      if (path === "/api/auth/me") {
        meRequests += 1;
        return new Headers(init?.headers).get("Authorization") === "Bearer access-refreshed"
          ? response(adminUser)
          : response({ message: "Access token expired" }, 401);
      }
      if (path === "/api/auth/csrf") return response({ csrfToken: "csrf-refresh-token" });
      if (path === "/api/auth/refresh") return response({
        token: "access-refreshed",
        expiresAt: "2026-11-26T20:00:00Z",
        user: adminUser,
        passwordChangeRequired: false,
      });
      throw new Error(`Unexpected API request: ${path}`);
    });
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-expired");

    renderProtectedApp();

    expect(await screen.findByText("authorized-dashboard")).toBeInTheDocument();
    expect(meRequests).toBe(2);
    expect(fetchMock.mock.calls.filter(([input]) => pathname(input) === "/api/auth/refresh")).toHaveLength(1);
    const refreshCall = fetchMock.mock.calls.find(([input]) => pathname(input) === "/api/auth/refresh");
    expect(refreshCall?.[1]?.credentials).toBe("include");
    expect(JSON.parse(String(refreshCall?.[1]?.body))).toEqual({});
    expect(new Headers(refreshCall?.[1]?.headers).get("X-Nexus-CSRF")).toBe("csrf-refresh-token");
  });

  it.each([
    ["an expired refresh cookie", { message: "Refresh session expired" }],
    ["a missing refresh cookie", { message: "Refresh cookie missing" }],
  ])("redirects to Login when %s", async (_scenario, refreshError) => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = pathname(input);
      if (path === "/api/auth/me") return response({ message: "Unauthorized" }, 401);
      if (path === "/api/auth/csrf") return response({ csrfToken: "csrf-token" });
      if (path === "/api/auth/refresh") return response(refreshError, 401);
      throw new Error(`Unexpected API request: ${path}`);
    });
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-expired");

    renderProtectedApp();

    expect(await screen.findByText("login-screen")).toBeInTheDocument();
    expect(screen.getByTestId("auth-state")).toHaveTextContent("unauthenticated");
    expect(fetchMock.mock.calls.filter(([input]) => pathname(input) === "/api/auth/refresh")).toHaveLength(1);
  });

  it("redirects to Login after a network failure instead of leaving authorization loading", async () => {
    const fetchMock = vi.fn(async () => { throw new TypeError("Failed to fetch"); });
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-network-failure");

    renderProtectedApp();

    expect(await screen.findByText("login-screen")).toBeInTheDocument();
    expect(screen.getByTestId("auth-state")).toHaveTextContent("unauthenticated");
  });

  it("settles a request that never responds after the initialization deadline", async () => {
    vi.useFakeTimers();
    const fetchMock = vi.fn(() => new Promise<Response>(() => undefined));
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-stalled");

    renderProtectedApp();
    expect(screen.getByTestId("auth-state")).toHaveTextContent("initializing");

    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000);
    });

    expect(screen.getByText("login-screen")).toBeInTheDocument();
    expect(screen.getByTestId("auth-state")).toHaveTextContent("unauthenticated");
  });

  it("shares repeated React Strict Mode initialization and commits the shared result", async () => {
    let resolveMe!: (value: Response) => void;
    const pendingMe = new Promise<Response>((resolve) => { resolveMe = resolve; });
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      if (pathname(input) === "/api/auth/me") return pendingMe;
      throw new Error(`Unexpected API request: ${pathname(input)}`);
    });
    vi.stubGlobal("fetch", fetchMock);
    setTokens("access-strict-mode");

    renderProtectedApp("/private", true);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    await act(async () => { resolveMe(response(adminUser)); });

    expect(await screen.findByText("authorized-dashboard")).toBeInTheDocument();
    expect(screen.getByTestId("auth-state")).toHaveTextContent("authenticated:Admin User");
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("navigates to the authorized dashboard after a successful login", async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = pathname(input);
      if (path === "/api/auth/csrf") return response({ csrfToken: "csrf-login-token" });
      if (path === "/api/auth/login") return response({
        token: "access-after-login",
        expiresAt: "2026-11-26T20:00:00Z",
        user: adminUser,
        passwordChangeRequired: false,
      });
      if (path === "/api/auth/me") return response(adminUser);
      throw new Error(`Unexpected API request: ${path}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    render(
      <I18nProvider>
        <MemoryRouter initialEntries={["/login"]}>
          <AuthProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/home" element={<RequireAuth><div>authorized-dashboard</div></RequireAuth>} />
            </Routes>
          </AuthProvider>
        </MemoryRouter>
      </I18nProvider>,
    );

    fireEvent.change(screen.getByLabelText(/^email$/i), { target: { value: "admin@example.test" } });
    fireEvent.change(screen.getByLabelText(/^password$/i), { target: { value: "test-password" } });
    fireEvent.click(screen.getByRole("button", { name: /sign in/i }));

    expect(await screen.findByText("authorized-dashboard")).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input]) => pathname(input) === "/api/auth/login")).toBe(true);
    expect(fetchMock.mock.calls.some(([input]) => pathname(input) === "/api/auth/me")).toBe(true);
  });
});
