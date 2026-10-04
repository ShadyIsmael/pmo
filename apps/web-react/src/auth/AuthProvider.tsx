import * as React from "react";
import { api, clearTokens, hasTokens, setTokens, type CurrentUser } from "../api/client";

export type AuthStatus = "loading" | "authenticated" | "unauthenticated";
interface AuthContextValue { status: AuthStatus; user: CurrentUser | null; isAuthenticated: boolean; login: (email: string, password: string) => Promise<void>; logout: () => Promise<void>; refreshUser: () => Promise<void>; }
const AuthContext = React.createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = React.useState<AuthStatus>("loading");
  const [user, setUser] = React.useState<CurrentUser | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    if (!hasTokens()) { setStatus("unauthenticated"); return () => { cancelled = true; }; }
    api.getMe().then((nextUser) => { if (!cancelled) { setUser(nextUser); setStatus("authenticated"); } }).catch(() => { if (!cancelled) { clearTokens(); setStatus("unauthenticated"); } });
    return () => { cancelled = true; };
  }, []);

  const login = React.useCallback(async (email: string, password: string) => {
    const response = await api.login(email, password);
    setTokens(response.token, response.refreshToken);
    setUser(response.user);
    setStatus("authenticated");
  }, []);

  const logout = React.useCallback(async () => {
    const token = window.localStorage.getItem("nexus_refresh_token");
    try {
      await api.logout(token);
    } catch {
      /* Local session is still cleared. */
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

  const value = React.useMemo(() => ({ status, user, isAuthenticated: status === "authenticated", login, logout, refreshUser }), [status, user, login, logout, refreshUser]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const value = React.useContext(AuthContext);
  if (!value) throw new Error("useAuth must be used within AuthProvider");
  return value;
}
