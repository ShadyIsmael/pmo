"use client";

import * as React from "react";
import { api, hasTokens, setTokens, clearTokens, type CurrentUser } from "@/lib/api/client";

export type AuthStatus = "loading" | "authenticated" | "unauthenticated";

interface AuthContextValue {
  status: AuthStatus;
  user: CurrentUser | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = React.createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = React.useState<AuthStatus>("loading");
  const [user, setUser] = React.useState<CurrentUser | null>(null);

  React.useEffect(() => {
    let cancelled = false;

    async function loadSession() {
      if (!hasTokens()) {
        if (!cancelled) setStatus("unauthenticated");
        return;
      }

      try {
        const me = await api.getMe();
        if (!cancelled) {
          setUser(me);
          setStatus("authenticated");
        }
      } catch {
        if (!cancelled) {
          setUser(null);
          setStatus("unauthenticated");
        }
      }
    }

    loadSession();
    return () => {
      cancelled = true;
    };
  }, []);

  const login = React.useCallback(async (email: string, password: string) => {
    const response = await api.login(email, password);
    setTokens(response.token, response.refreshToken);
    setUser(response.user);
    setStatus("authenticated");
  }, []);

  const logout = React.useCallback(async () => {
    const refreshToken = getStoredRefreshToken();
    try {
      await api.logout(refreshToken);
    } catch {
      // Session is cleared locally even if the server call fails.
    }
    clearTokens();
    setUser(null);
    setStatus("unauthenticated");
  }, []);

  const refreshUser = React.useCallback(async () => {
    const me = await api.getMe();
    setUser(me);
    setStatus("authenticated");
  }, []);

  const value = React.useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      isAuthenticated: status === "authenticated",
      login,
      logout,
      refreshUser,
    }),
    [status, user, login, logout, refreshUser]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

function getStoredRefreshToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem("nexus_refresh_token");
}

export function useAuth(): AuthContextValue {
  const ctx = React.useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within an AuthProvider");
  return ctx;
}

